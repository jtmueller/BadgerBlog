namespace BadgerBlog.Tests

open System
open System.Web.Mvc
open System.Web.Routing
open MvcContrib.TestHelper
open BadgerBlog

type RoutingTestBase() =
    static let testGuid = Guid.NewGuid()
    do RouteConfigurator.Configure(RouteTable.Routes)

    member x.GetMethod(url:string, ?httpMethod) =
        let route = url.WithMethod(httpMethod |? HttpVerbs.Get)
        route.Values.["key"] <- testGuid
        route


// TODO: controllers need to exist before route tests can be ported.