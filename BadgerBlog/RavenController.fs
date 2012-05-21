namespace BadgerBlog.Web.Controllers

open System
open System.Web.Mvc
open System.Xml
open System.Xml.Linq
open Raven.Client
open BadgerBlog

type XmlResult(document:XDocument, etag) =
    inherit ActionResult()

    override x.ExecuteResult context =
        if not <| String.IsNullOrEmpty(etag) then
            context.HttpContext.Response.AddHeader("ETag", etag)

        context.HttpContext.Response.ContentType <- "text/xml"

        use xmlWriter = XmlWriter.Create(context.HttpContext.Response.OutputStream)
        document.WriteTo(xmlWriter)
        xmlWriter.Flush()

[<AbstractClass>]
type RavenController() =
    inherit Controller()

    let mutable lazySession : Lazy<IAsyncDocumentSession> option = None

    static member val DocumentStore : IDocumentStore = null with get, set
    
    member x.RavenSession =
        match lazySession with
        | None ->
            invalidOp "Document session not initialized!"
        | Some s ->
            s.Value

    override x.OnActionExecuting filterContext =
        lazySession <-
            match x.HttpContext.Items.["CurrentRequestRavenSession"] with
            | :? Lazy<IAsyncDocumentSession> as s ->
                Some s
            | _ ->
                None

    member x.ExecuteLater(task : Async<unit>) =
        x.HttpContext.Items.["DeferredTasks"] <-
            match x.HttpContext.Items.["DeferredTasks"] with
            | null -> box [task]
            | :? List<Async<unit>> as tasks -> box (task :: tasks)
            | o -> o

    member x.HttpNotModified() =
        HttpStatusCodeResult(304)

    member x.Xml(xml, etag) =
        XmlResult(xml, etag)


namespace BadgerBlog.Web.Admin.Controllers

    open System
    open System.Web.Mvc
    open BadgerBlog.Web.Controllers

    [<AbstractClass; Authorize>]
    type AdminController() =
        inherit RavenController()

        let mutable disableAggressiveCaching : IDisposable = null

        override x.OnActionExecuting(filterContext) =
            disableAggressiveCaching <- RavenController.DocumentStore.DisableAggressiveCaching()
            base.OnActionExecuting(filterContext)

        override x.OnActionExecuted(filterContext) =
            use dummy = disableAggressiveCaching
            base.OnActionExecuted(filterContext)