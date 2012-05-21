namespace BadgerBlog.Web.Controllers

open System.Web
open System.Web.Mvc

[<HandleError>]
type HomeController() =
    inherit RavenController()
    member this.Index () =
        this.View() :> ActionResult
