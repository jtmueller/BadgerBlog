﻿
#if INTERACTIVE
#r "FSharp.PowerPack.Linq.dll"
#r "AutoMapper.dll" 
#endif

namespace Microsoft.FSharp.Quotations

module Expr =
    open System
    open System.Linq.Expressions
    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Linq.QuotationEvaluation

    /// Translates simple F# quotation to LINQ expression
    /// (the function supports only variables, property getters,
    /// method calls and static method calls)
    // http://www.fssnip.net/c6
    let rec private translateSimpleExpr expr =
        match expr with
        | Patterns.Var(var) ->
            // Variable access
            Expression.Variable(var.Type, var.Name) :> Expression
        | Patterns.PropertyGet(Some inst, pi, []) ->
            // Getter of an instance property
            let instExpr = translateSimpleExpr inst
            Expression.Property(instExpr, pi) :> Expression
        | Patterns.Call(Some inst, mi, args) ->
            // Method call - translate instance & arguments recursively
            let argsExpr = Seq.map translateSimpleExpr args
            let instExpr = translateSimpleExpr inst
            Expression.Call(instExpr, mi, argsExpr) :> Expression
        | Patterns.Call(None, mi, args) ->
            // Static method call - no instance
            let argsExpr = Seq.map translateSimpleExpr args
            Expression.Call(mi, argsExpr) :> Expression
        | _ -> failwith "not supported"

    // http://stackoverflow.com/questions/10658207/using-automapper-with-f
    let ToAutoMapperGet (expr:Expr<'a -> 'b>) =
        match expr with
        | Patterns.Lambda(v, body) ->
            // Build LINQ style lambda expression
            let bodyExpr = Expression.Convert(translateSimpleExpr body, typeof<obj>)
            let paramExpr = Expression.Parameter(v.Type, v.Name)
            Expression.Lambda<Func<'a, obj>>(bodyExpr, paramExpr)
        | _ -> failwith "not supported"

    // http://stackoverflow.com/questions/10647198/how-to-convert-expra-b-to-expressionfunca-obj
    let ToFuncExpression (expr:Expr<'a -> 'b>) =
        let call = expr.ToLinqExpression() :?> MethodCallExpression
        let lambda = call.Arguments.[0] :?> LambdaExpression
        Expression.Lambda<Func<'a, 'b>>(lambda.Body, lambda.Parameters) 

namespace AutoMapper

/// Functions for working with AutoMapper using F# quotations,
/// in a manner that is compatible with F# type-inference.
module AutoMap =
    open System
    open Microsoft.FSharp.Quotations

    let forMember (destMember: Expr<'dest -> 'mbr>) 
                  (memberOpts: IMemberConfigurationExpression<'source> -> unit) 
                  (map: IMappingExpression<'source, 'dest>) =
        map.ForMember(Expr.ToAutoMapperGet destMember, memberOpts)

    let mapMember destMember (sourceMap:Expr<'source -> 'mapped>) =
        forMember destMember (fun o -> o.MapFrom(Expr.ToFuncExpression sourceMap))

    let ignoreMember destMember =
        forMember destMember (fun o -> o.Ignore())

    let forMemberName (destMember: string) 
                      (memberOpts: IMemberConfigurationExpression<'source> -> unit) 
                      (map: IMappingExpression<'source, 'dest>) =
        map.ForMember(destMember, memberOpts)

    let mapMemberName destMember (sourceMap:Expr<'source -> 'mapped>) =
        forMemberName destMember (fun o -> o.MapFrom(Expr.ToFuncExpression sourceMap))

    let ignoreMemberName destMember =
        forMemberName destMember (fun o -> o.Ignore())
        
[<AutoOpen>]
module AutoMapperExtensions =
    open System
    open System.Collections.Generic

    let inline isNull x = Object.ReferenceEquals(x, null)
    
    type IList<'a> with
        member this.MapTo<'b> () =
            if isNull this then nullArg "this"
            Mapper.Map(this, this.GetType(), typeof<ResizeArray<'b>>) :?> IList<'b>

    type IEnumerable<'a> with
        member this.MapTo<'b> () =
            if isNull this then nullArg "this"
            Mapper.Map(this, this.GetType(), typeof<IEnumerable<'b>>) :?> IEnumerable<'b>

    type System.Collections.IEnumerable with
        member this.MapTo<'b> () =
            if isNull this then nullArg "this"
            Mapper.Map(this, this.GetType(), typeof<IEnumerable<'b>>) :?> IEnumerable<'b>

    type System.Object with
        member this.MapTo<'b> () =
            if isNull this then nullArg "this"
            Mapper.Map(this, this.GetType(), typeof<'b>) :?> 'b

        member this.MapPropertiesToInstance<'b>(value:'b) =
            if isNull this then nullArg "this"
            Mapper.Map(this, value, this.GetType(), typeof<'b>) :?> 'b

        member this.DynamicMapTo<'b> () =
            if isNull this then nullArg "this"
            Mapper.DynamicMap(this, this.GetType(), typeof<'b>) :?> 'b

