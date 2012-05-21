module BadgerBlog.RouteConfigurator

    open System.Web.Mvc
    open System.Web.Routing
    open BadgerBlog.Web.Helpers

    let inline (=>) a b = a, box b

    let private indexRoute = dict [ "controller" => "Posts"; "action" => "Index" ]
    let private matchPositiveInt = @"\d{1,10}"
    let private ns = [| "BadgerBlog.Web.Controllers" |]

    let private css (routes:RouteCollection) =
        routes.MapRouteLowerCase("CssController",
            "css",
            dict [ "controller" => "Css"; "action" => "Merge" ],
            namespaces = ns)

    let private search (routes:RouteCollection) =
        routes.MapRouteLowerCase("SearchController-GoogleCse",
            "search/google_cse.xml",
            dict [ "controller" => "Search"; "action" => "GoogleCse" ],
            namespaces = ns)

        routes.MapRouteLowerCase("SearchController",
            "search/{action}",
            dict [ "controller" => "Search"; "action" => "SearchResult" ],
            dict [ "action" => "SearchResult" ],
            namespaces = ns)

    let private post (routes:RouteCollection) =
        routes.MapRouteLowerCase("PostsByTag",
            "tags/{slug}",
            dict [ "controller" => "Posts"; "action" => "Tag" ],
            namespaces = ns)

        routes.MapRouteLowerCase("PostsByYearMonthDay",
            "archive/{year}/{month}/{day}",
            dict [ "controller" => "Posts"; "action" => "Archive" ],
            dict [ "year" => matchPositiveInt; "month" => matchPositiveInt; "day" => matchPositiveInt ],
            namespaces = ns)

        routes.MapRouteLowerCase("PostsByYearMonth",
            "archive/{year}/{month}",
            dict [ "controller" => "Posts"; "action" => "Archive" ],
            dict [ "year" => matchPositiveInt; "month" => matchPositiveInt ],
            namespaces = ns)

        routes.MapRouteLowerCase("PostsByYear",
            "archive/{year}",
            dict [ "controller" => "Posts"; "action" => "Archive" ],
            dict [ "year" => matchPositiveInt ],
            namespaces = ns)

    let private legacyPost (routes:RouteCollection) =
        routes.MapRouteLowerCase("RedirectLegacyPostUrl",
            "archive/{year}/{month}/{day}/{slug}.aspx",
            dict [ "controller" => "LegacyPost"; "action" => "RedirectLegacyPost" ],
            dict [ "year" => matchPositiveInt; "month" => matchPositiveInt; "day" => matchPositiveInt ],
            namespaces = ns)
        
        routes.MapRouteLowerCase("RedirectLegacyArchive",
            "archive/{year}/{month}/{day}.aspx",
            dict [ "controller" => "LegacyPost"; "action" => "RedirectLegacyArchive" ],
            dict [ "year" => matchPositiveInt; "month" => matchPositiveInt ],
            namespaces = ns)
            
    let private postDetail (routes:RouteCollection) =
        routes.MapRouteLowerCase("PostDetailsController-Comment",
            "{id}/comment",
            dict [ "controller" => "PostDetails"; "action" => "Comment" ],
            dict [ "httpMethod" => HttpMethodConstraint("POST"); "id" => matchPositiveInt ],
            namespaces = ns)
            
        routes.MapRouteLowerCase("PostDetailsController-Details",
            "{id}/{slug}",
            dict [ "controller" => "PostDetails"; "action" => "Details"; "slug" => UrlParameter.Optional ],
            dict [ "id" => matchPositiveInt ],
            namespaces = ns)

    let private syndication (routes:RouteCollection) =
        routes.MapRouteLowerCase("CommentsRssFeed",
            "rss/comments",
            dict [ "controller" => "Syndication"; "action" => "CommentsRss" ],
            namespaces = ns)

        routes.MapRouteLowerCase("RssFeed",
            "rss/{tag}",
            dict [ "controller" => "Syndication"; "action" => "Rss"; "tag" => UrlParameter.Optional ],
            namespaces = ns)

        routes.MapRouteLowerCase("RsdFeed",
            "rsd",
            dict [ "controller" => "Syndication"; "action" => "Rsd" ],
            namespaces = ns)

        routes.MapRouteLowerCase("RssFeed-LegacyUrl",
            "rss.aspx",
            dict [ "controller" => "Syndication"; "action" => "LegacyRss" ],
            namespaces = ns)

    let Configure (routes:RouteCollection) =
        routes.IgnoreRoute("{resource}.axd/{*pathInfo}")
        routes.IgnoreRoute("{*favicon}", dict [ "favicon" => @"(.*/)?favicon.ico(/.*)?" ])

        syndication routes

        post routes
        legacyPost routes
        postDetail routes

        search routes
        css routes

        routes.MapRouteLowerCase("Default", "{controller}/{action}", indexRoute, namespaces = ns)
        routes.MapRouteLowerCase("homepage", "", indexRoute, namespaces = ns)
