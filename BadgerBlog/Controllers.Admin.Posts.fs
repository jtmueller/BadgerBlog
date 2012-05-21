namespace BadgerBlog.Web.Admin.Controllers

type PostsController() =
    inherit AdminController()

type CommentCommandOptions =
    | Delete = 0
    | MarkHam = 1
    | MarkSpam = 2