namespace BadgerBlog.Web

open System
open System.Net
open System.Net.Sockets
open System.Web
open System.Web.Mvc
open System.Web.Routing
open DataAnnotationsExtensions.ClientValidation
open Raven.Client
open Raven.Client.Document
open Raven.Client.Indexes
open Raven.Client.MvcIntegration
open NLog
open BadgerBlog
open BadgerBlog.Web.Helpers
open BadgerBlog.Web.Helpers.Binders

type VersioningDoc = {
    Exclude : bool
    Id : string
}

type Global() as this =
    inherit System.Web.HttpApplication()

    static let tryCreateIndexes (docStore:IDocumentStore) =
        try
            Indexes.Definitions
            |> List.iter (docStore.DatabaseCommands.PutIndex >> ignore)
        with :? WebException as ex ->
            match ex.InnerException with
            | :? SocketException as sex ->
                match sex.SocketErrorCode with
                | SocketError.AddressNotAvailable
                | SocketError.NetworkDown
                | SocketError.NetworkUnreachable
                | SocketError.ConnectionAborted
                | SocketError.ConnectionReset
                | SocketError.TimedOut
                | SocketError.ConnectionRefused
                | SocketError.HostDown
                | SocketError.HostUnreachable
                | SocketError.HostNotFound ->
                    HttpContext.Current.Response.Redirect("~/RavenNotReachable.htm", true)
                | _ ->
                    reraise()
            | _ ->
                reraise()

    static let docStore = lazy(
        let docStore = DocumentStore.OpenInitializedStore("RavenDB")
        tryCreateIndexes docStore
        RavenProfiler.InitializeFor(docStore,
                                    //Fields to filter out of the output
                                    "Email", "HashedPassword", "AkismetKey", "GoogleAnalyticsKey", "ShowPostEvenIfPrivate",
                                    "PasswordSalt", "UserHostAddress")
        docStore
    )

    let endReqBegin, endReqEnd =
        Async.AsBeginEndHandlers (fun _ -> async {
            let lazySession = HttpContext.Current.Items.["CurrentRequestRavenSession"] :?> Lazy<IAsyncDocumentSession>

            if isNotNull lazySession && lazySession.IsValueCreated && isNull (this.Server.GetLastError()) then
                use session = lazySession.Value
                do! session.AsyncSaveChanges()
            
            match HttpContext.Current.Items.["DeferredTasks"] with
            | :? List<Async<unit>> as tasks ->
                do! tasks |> Async.Parallel |> Async.Ignore
            | _ -> ()
        })

    do
        this.BeginRequest.Add 
            (fun _ -> HttpContext.Current.Items.["CurrentRequestRavenSession"] <- lazy( docStore.Value.OpenAsyncSession() ))
        this.AddOnEndRequestAsync(endReqBegin, endReqEnd)

    static member RegisterGlobalFilters(filters:GlobalFilterCollection) =
        filters.Add(HandleErrorAttribute())

    static member DocumentStore with get () = docStore.Value 

    member this.Start() =
        AreaRegistration.RegisterAllAreas()
        LogManager.GetCurrentClassLogger().Info("Started Badger Blog")

        // Work around nasty .NET framework bug
        // See http://ayende.com/blog/4422/is-select-system-uri-broken
        try
            Uri("http://fail/first/time?only=%2bplus") |> ignore
        with _ -> ()

        Global.RegisterGlobalFilters GlobalFilters.Filters
        RouteConfigurator.Configure RouteTable.Routes
        ModelBinders.Binders.Add(typeof<Admin.Controllers.CommentCommandOptions>, RemoveSpacesEnumBinder())
        ModelBinders.Binders.Add(typeof<Guid>, GuidBinder())

        DataAnnotationsModelValidatorProviderExtensions.RegisterValidationExtensions()

        AutoMapperConfiguration.Configure()

        Controllers.RavenController.DocumentStore <- docStore.Value

        // In case the versioning bundle is installed, make sure it will version
        // only what we opt-in to version
        use session = docStore.Value.OpenSession()
        session.Store({ Exclude = true; Id = "Raven/Versioning/DefaultConfiguration" })
        session.SaveChanges()
