[<AutoOpen>]
module BadgerBlog.Operators

    open System

    let isNull (x:obj) = Object.ReferenceEquals(x, null)
    let isNotNull : obj -> bool = isNull >> not

    let tryCast<'a> (o:obj) =
        match o with
        | :? 'a -> Some(o :?> 'a)
        | _ -> None

    let (|?) = defaultArg