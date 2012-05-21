namespace BadgerBlog.ViewModels

open System
open System.Web.Mvc
open System.Collections.Generic
open System.ComponentModel.DataAnnotations
open DataAnnotationsExtensions
open BadgerBlog
open BadgerBlog.Web.Helpers

[<AllowNullLiteral>]
type PostReference() =
    let mutable domainId = 0
    let mutable slug : string = null

    // if Id is: posts/1024, than domainId will be: 1024
    member val Id : string = null with get, set
    member val Title : string = null with get, set

    member x.DomainId =
        if domainId = 0 then
            domainId <- RavenIdResolver.Resolve(x.Id)

    member x.Slug
        with get () =
            if isNull slug then
                slug <- SlugConverter.TitleToSlug(x.Title)
            slug
        and set v =
            slug <- v

module AdminPostDetails =
    // F# doesn't allow nested types except in modules

    [<AllowNullLiteral>]
    type PostDetails() =
        member val Id = 0 with get, set
        member val Key : string = null with get, set
        member val Title = MvcHtmlString.Empty with get, set
        member val Slug : string = null with get, set
        member val Body = MvcHtmlString.Empty with get, set
    
        member val CreatedAt = DateTimeOffset.MinValue with get, set
        member val PublishedAt = DateTimeOffset.MaxValue with get, set

        member val Tags : ICollection<string> = null with get, set

    type Comment() =
        member val Id = 0 with get, set
        member val Body = MvcHtmlString.Empty with get, set
        member val Author : string = null with get, set
        member val Url : string = null with get, set    // Look for HTML injection
        member val EmailHash : string = null with get, set
        member val CreatedAt = DateTimeOffset.MinValue with get, set
        member val IsSpam = true with get, set
        member val IsImportant = false with get, set
        
type AdminPostDetailsViewModel() =
    member val PreviousPost : PostReference = null with get, set
    member val NextPost : PostReference = null with get, set

    member val Post : AdminPostDetails.PostDetails = null with get, set
    member val Comments : IList<AdminPostDetails.Comment> = null with get, set

    member val AreCommentsClosed = false with get, set

[<AllowNullLiteral>]
type CommentInput() =
    [<Required; Display(Name = "Name")>]
    member val Name : string = null with get, set

    [<Required; Display(Name = "Email"); Email>]
    member val Email : string = null with get, set

    [<Display(Name = "Url")>]
    member val Url : string = null with get, set

    [<AllowHtml; Required; Display(Name = "Comments"); DataType(DataType.MultilineText)>]
    member val Body : string = null with get, set

    [<HiddenInput>]
    member val CommenterKey = Nullable<Guid>() with get, set

type CurrentUserViewModel() =
    member val FullName : string = null with get, set
    member x.IsAuthenticated() =
        String.IsNotEmpty x.FullName

type FuturePostViewModel() as this =
    let distanceOfTimeInWords = function
        | n when n < 0. ->
            raise <| InvalidOperationException(sprintf "Future post error: the post is already published. Title: %s, PublishAt: %O, Now: %O" this.Title this.PublishAt DateTimeOffset.Now)
        | n when n < 1. ->
            "less than a minute"
        | n when n < 50. ->
            n |> round |> int |> sprintf "%i minutes"
        | n when n < 90. ->
            "about one hour"
        | n when n < 1080. ->
            n / 60. |> round |> int |> sprintf "%i hours"
        | n when n < 1440. ->
            "one day"
        | n when n < 2880. ->
            "about one day"
        | n ->
            n / 1440. |> round |> int |> sprintf "%i days"

    member val Title : string = null with get, set
    member val PublishAt = DateTimeOffset.MaxValue with get, set
    member x.Time =
        let totalMins = (x.PublishAt - DateTimeOffset.Now).TotalMinutes
        (distanceOfTimeInWords totalMins) + " from now"

type FuturePostsViewModel() =
    member val TotalCount = 0 with get, set
    member val Posts : IList<FuturePostViewModel> = null with get, set
    member val LastPostDate = Nullable<DateTimeOffset>() with get, set

type NewCommentEmailViewModel() =
    member val Id = 0 with get, set
    member val Author : string = null with get, set
    member val Url : string = null with get, set
    member val Email : string = null with get, set
    member val CreatedAt = DateTimeOffset.MinValue with get, set
    member val Body = MvcHtmlString.Empty with get, set
    member val IsSpam : bool = true with get, set

    member val PostId = 0 with get, set
    member val PostTitle : string = null with get, set
    member val PostSlug : string = null with get, set
    member val BlogName : string = null with get, set
    member val Key : string = null with get, set
    member val CommenterId : string = null with get, set

type PostsStatisticsViewModel() =
    member val PostsCount = 0 with get, set
    member val CommentsCount = 0 with get, set

type PostSummaryJson() =
    member val Id = 0 with get, set
    member val Title : string = null with get, set
    member val Start : string = null with get, set
    member val Url : string = null with get, set
    member val AllDay = false with get, set

type TagDetails() =
    let mutable slug = null
    member val Name : string = null with get, set
    member x.Slug =
        if isNull slug then
            slug <- SlugConverter.TitleToSlug(x.Name)
        slug

module Posts =
    // F# doesn't allow nested types except in modules.
    [<AllowNullLiteral>]
    type UserDetails() =
        member val TwitterNick : string = null with get, set
        member val RelatedTwitterNick : string = null with get, set
        member val RelatedTwitNickDes : string = null with get, set

    type PostSummary() =
        member val Id = 0 with get, set
        member val Title = MvcHtmlString.Empty with get, set
        member val Slug : string = null with get, set
        member val Body = MvcHtmlString.Empty with get, set
        member val Tags : ICollection<TagDetails> = null with get, set
        member val CreatedAt = DateTimeOffset.MinValue with get, set
        member val PublishedAt = DateTimeOffset.MaxValue with get, set
        member val CommentsCount = 0 with get, set
        member val Author : UserDetails = null with get, set

type PostsViewModel() =
    member val CurrentPage = 0 with get, set
    member val PostsCount = 0 with get, set
    member val Posts : IList<Posts.PostSummary> = null with get, set
    
    member x.HasNextPage() =
        x.CurrentPage * Constants.PageSize < x.PostsCount

    member x.HasPrevPage() =
        x.CurrentPage * Constants.PageSize > Constants.PageSize * Constants.DefaultPage

module Post =
    // F# doesn't allow nested types except in modules.
    type Comment() =
        member val Id = 0 with get, set
        member val Body = MvcHtmlString.Empty with get, set
        member val Author : string = null with get, set
        member val Tooltip : string = null with get, set
        member val Url : string = null with get, set
        member val EmailHash : string = null with get, set
        member val CreatedAt : string = null with get, set
        member val IsImportant = false with get, set

    [<AllowNullLiteral>]
    type UserDetails() =
        member val FullName : string = null with get, set
        member val TwitterNick : string = null with get, set
        member val RelatedTwitterNick : string = null with get, set
        member val RelatedTwitNickDes : string = null with get, set

    [<AllowNullLiteral>]
    type PostDetails() =
        member val Id = 0 with get, set
        member val ShowPostEvenIfPrivate = Guid.Empty with get, set
        member val Title = MvcHtmlString.Empty with get, set
        member val Slug : string = null with get, set
        member val Body = MvcHtmlString.Empty with get, set

        member val CreatedAt = DateTimeOffset.MinValue with get, set
        member val PublishedAt = DateTimeOffset.MaxValue with get, set
        member val IsCommentAllowed = true with get, set
        
        member val Tags : ICollection<TagDetails> = null with get, set
        member val Author : UserDetails = null with get, set

type PostViewModel() =
    member val PreviousPost : PostReference = null with get, set
    member val NextPost : PostReference = null with get, set
    
    member val Post : Post.PostDetails = null with get, set
    member val Comments : IList<Post.Comment> = null with get, set
    member val Input : CommentInput = null with get, set

    member val AreCommentsClosed = false with get, set
    member val IsTrustedCommenter = false with get, set
    member val IsLoggedIncommenter = false with get, set

type RecentCommentViewModel() =
    member val CommentId : string = null with get, set
    member val ShortBody : string = null with get, set
    member val Author : string = null with get, set
    member val PostTitle : string = null with get, set
    member val PostId = 0 with get, set
    member val PostSlug : string = null with get, set

type SectionDetails() =
    member val Title : string = null with get, set
    member val Body = MvcHtmlString.Empty with get, set
    member val ControllerName : string = null with get, set
    member val ActionName : string = null with get, set
    member x.IsActionSection =
        MvcHtmlString.IsNullOrEmpty x.Body

type TagsListViewModel() =
    let mutable slug : string = null
    member val Name : string = null with get, set
    member val Count = 0 with get, set
    member x.Slug =
        if isNull slug then
            slug <- SlugConverter.TitleToSlug(x.Name)
        slug

type UserInput() =
    [<HiddenInput>]
    member val Id = 0 with get, set

    [<Display(Name = "Full Name")>]
    member val FullName : string = null with get, set

    [<Required; Display(Name = "Email"); Email>]
    member val Email : string = null with get, set

    [<Display(Name = "Is Enabled?")>]
    member val Enabled = false with get, set

    [<Display(Name = "Twitter Nick")>]
    member val TwitterNick : string = null with get, set

    [<Display(Name = "Related Twitter Nick", Prompt = "This is a nick of someone that you want to recommend the user to follow.")>]
    member val RelatedTwitterNick : string = null with get, set

    [<Display(Name = "Related Twitter Description")>]
    member val RelatedTwitNickDes : string = null with get, set

    member x.IsNewUser() = x.Id = 0

type UserSummaryViewModel() =
    member val Id = 0 with get, set

    [<Display(Name = "Full Name")>]
    member val FullName : string = null with get, set

    [<Display(Name = "Email")>]
    member val Email : string = null with get, set

    [<Display(Name = "Enabled?")>]
    member val Enabled = false with get, set

    [<Display(Name = "Twitter Nick")>]
    member val TwitterNick : string = null with get, set

    [<Display(Name = "Related Twitter Nick", Prompt = "This is a nick of someone that you want to recommend the user to follow.")>]
    member val RelatedTwitterNick : string = null with get, set

