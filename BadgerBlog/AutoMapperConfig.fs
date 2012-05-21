namespace BadgerBlog.Web

open System
open System.Web
open System.Web.Mvc
open AutoMapper
open BadgerBlog
open BadgerBlog.Models
open BadgerBlog.ViewModels
open BadgerBlog.Web.Helpers

module private Converters =

    type DateTimeTypeConverter() =
        inherit TypeConverter<DateTimeOffset, DateTime>()
        override x.ConvertCore(source) =
            source.DateTime

    type GuidToStringConverter() =
        inherit TypeConverter<Guid, string>()
        override x.ConvertCore(source) =
            source.ToString("N")

    type StringToGuidConverter() =
        inherit TypeConverter<string, Guid>()
        override x.ConvertCore(source) =
            match Guid.TryParse(source) with
            | true, guid -> guid
            | false, _   -> Guid.Empty

    type StringToNullableGuidConverter() =
        inherit TypeConverter<string, Nullable<Guid>>()
        override x.ConvertCore(source) =
            match Guid.TryParse(source) with
            | true, guid -> Nullable<_>(guid)
            | false, _   -> Nullable<_>()

    type MvcHtmlStringConverter() =
        inherit TypeConverter<string, MvcHtmlString>()
        override x.ConvertCore(source) =
            MvcHtmlString.Create(source)


module private Profiles =
    open AutoMap

    let inline (=>) a b = a, box b

    [<AbstractClass>]
    type AbstractProfile() =
        inherit Profile()
        member x.UrlHelper = UrlHelper(HttpContext.Current.Request.RequestContext)

    type EmailViewModelMapperProfile() =
        inherit Profile()

        override x.Configure() =
            Mapper.CreateMap<PostComment.Comment, NewCommentEmailViewModel>()
            |> mapMember <@ fun x -> x.Body @> <@ fun m -> MarkdownResolver.Resolve(m.Body) @>
            |> ignoreMember <@ fun x -> x.PostId @>
            |> ignoreMember <@ fun x -> x.PostTitle @>
            |> ignoreMember <@ fun x -> x.PostSlug @>
            |> ignoreMember <@ fun x -> x.BlogName @>
            |> ignoreMember <@ fun x -> x.Key @>
            |> ignore

    type PostsAdminViewModelMapperProfile() =
        inherit AbstractProfile()

        override this.Configure() =
            Mapper.CreateMap<Post, PostSummaryJson>()
            |> mapMember <@ fun x -> x.Id    @> <@ fun m -> RavenIdResolver.Resolve(m.Id) @>
            |> mapMember <@ fun x -> x.Title @> <@ fun m -> HttpUtility.HtmlDecode(m.Title) @>
            |> mapMember <@ fun x -> x.Start @> <@ fun m -> m.PublishAt.ToString("yyyy-MM-ddTHH:mm:ssZ") @>
            |> mapMember <@ fun x -> x.Url   @> <@ fun m -> this.UrlHelper.Action("Details", "Posts", dict ["Id" => RavenIdResolver.Resolve(m.Id); "Slug" => SlugConverter.TitleToSlug(m.Title)]) @>
            |> forMember <@ fun x -> x.AllDay @> (fun o -> o.UseValue(false))
            |> ignore

            Mapper.CreateMap<Post, PostInput>()
            |> mapMember <@ fun x -> x.Id @> <@ fun m -> RavenIdResolver.Resolve(m.Id) @>
            |> mapMember <@ fun x -> x.Tags @> <@ fun m -> TagsResolver.ResolveTags(m.Tags) @>
            |> ignore

            Mapper.CreateMap<PostInput, Post>()
            |> ignoreMember <@ fun x -> x.Id @>
            |> ignoreMember <@ fun x -> x.AuthorId @>
            |> ignoreMember <@ fun x -> x.LegacySlug @>
            |> ignoreMember <@ fun x -> x.ShowPostEvenIfPrivate @>
            |> ignoreMember <@ fun x -> x.SkipAutoReschedule @>
            |> ignoreMember <@ fun x -> x.IsDeleted @>
            |> ignoreMember <@ fun x -> x.CommentsCount @>
            |> ignoreMember <@ fun x -> x.CommentsId @>
            |> ignoreMember <@ fun x -> x.LastEditedByUserId @>
            |> ignoreMember <@ fun x -> x.LastEditedAt @>
            |> mapMember <@ fun x -> x.Tags @> <@ fun m -> TagsResolver.ResolveTagsInput(m.Tags) @>
            |> ignore

            Mapper.CreateMap<Post, AdminPostDetails.PostDetails>()
            |> mapMember <@ fun x -> x.Id   @> <@ fun m -> RavenIdResolver.Resolve(m.Id) @>
            |> mapMember <@ fun x -> x.Slug @> <@ fun m -> SlugConverter.TitleToSlug(m.Title) @>
            |> mapMember <@ fun x -> x.Key  @> <@ fun m -> m.ShowPostEvenIfPrivate @>
            |> mapMember <@ fun x -> x.PublishedAt @> <@ fun m -> m.PublishAt @>
            |> ignore

            Mapper.CreateMap<PostComment.Comment, AdminPostDetails.Comment>()
            |> mapMember <@ fun x -> x.Body        @> <@ fun m -> MarkdownResolver.Resolve(m.Body) @>
            |> mapMember <@ fun x -> x.EmailHash   @> <@ fun m -> EmailHashResolver.Resolve(m.Email) @>
            |> mapMember <@ fun x -> x.IsImportant @> <@ fun m -> m.Important @>
            |> ignore

    type UserAdminMapperProfile() =
        inherit Profile()

        override x.Configure() =
            Mapper.CreateMap<User, UserSummaryViewModel>()
            |> mapMember <@ fun x -> x.Id @> <@ fun m -> RavenIdResolver.Resolve(m.Id) @>
            |> ignore

            Mapper.CreateMap<UserInput, User>()
            |> ignoreMember <@ fun x -> x.Id @>
            |> ignore

            Mapper.CreateMap<User, UserInput>()
            |> mapMember <@ fun x -> x.Id @> <@ fun m -> RavenIdResolver.Resolve(m.Id) @>
            |> ignore

    type TagsListViewModelMapperProfile() =
        inherit Profile()
    
        override x.Configure() =
            Mapper.CreateMap<Indexes.Tags_Count_Result, TagsListViewModel>() |> ignore

    type SectionMapperProfile() =
        inherit Profile()

        let maxLength text len =
            if String.IsNullOrEmpty(text) then
                text
            elif text.Length > len then
                text.Substring(0, len - 3) + "..."
            else
                text

        override x.Configure() =
            Mapper.CreateMap<Section, SectionDetails>()
            |> ignore

            Mapper.CreateMap<Post, FuturePostViewModel>()
            |> mapMember <@ fun x -> x.Title @> <@ fun m -> HttpUtility.HtmlDecode(m.Title) @>
            |> ignore

            Mapper.CreateMap<Indexes.Posts_Statistics_Result, PostsStatisticsViewModel>()
            |> ignore

            Mapper.CreateMap<Post, RecentCommentViewModel>()
            |> mapMember <@ fun x -> x.PostId    @> <@ fun m -> RavenIdResolver.Resolve(m.Id) @>
            |> mapMember <@ fun x -> x.PostTitle @> <@ fun m -> m.Title @>
            |> mapMember <@ fun x -> x.PostSlug  @> <@ fun m -> SlugConverter.TitleToSlug(m.Title) @>
            |> ignoreMember <@ fun x -> x.Author @>
            |> ignoreMember <@ fun x -> x.CommentId @>
            |> ignoreMember <@ fun x -> x.ShortBody @>
            |> ignore

            Mapper.CreateMap<PostComment.Comment, RecentCommentViewModel>()
            |> ignoreMember <@ fun x -> x.PostId @>
            |> mapMember <@ fun x -> x.ShortBody @> <@ fun m -> maxLength m.Body 128 @>
            |> mapMember <@ fun x -> x.CommentId @> <@ fun m -> m.Id @>
            |> ignoreMember <@ fun x -> x.PostSlug @>
            |> ignoreMember <@ fun x -> x.PostTitle @>
            |> ignore

    type PostsViewModelMapperProfile() =
        inherit Profile()

        override x.Configure() =
            Mapper.CreateMap<Post, Posts.PostSummary>()
            |> mapMember <@ fun x -> x.Id   @> <@ fun m -> RavenIdResolver.Resolve(m.Id) @>
            |> mapMember <@ fun x -> x.Slug @> <@ fun m -> SlugConverter.TitleToSlug(m.Title) @>
            |> ignoreMember <@ fun x -> x.Author @>
            |> mapMember <@ fun x -> x.PublishedAt @> <@ fun m -> m.PublishAt @>
            |> ignore

            Mapper.CreateMap<User, Posts.UserDetails>()
            |> ignore

            Mapper.CreateMap<string, TagDetails>()
            |> mapMember <@ fun x -> x.Name @> <@ id @>
            |> ignore

    type PostViewModelMapperProfile() =
        inherit AbstractProfile()

        override this.Configure() =
            Mapper.CreateMap<Post, Post.PostDetails>()
            |> mapMember <@ fun x -> x.Id               @> <@ fun m -> RavenIdResolver.Resolve(m.Id) @>
            |> mapMember <@ fun x -> x.Slug             @> <@ fun m -> SlugConverter.TitleToSlug(m.Title) @>
            |> mapMember <@ fun x -> x.PublishedAt      @> <@ fun m -> m.PublishAt @>
            |> mapMember <@ fun x -> x.IsCommentAllowed @> <@ fun m -> m.AllowComments @>
            |> ignoreMember <@ fun x -> x.Author @>
            |> ignore

            Mapper.CreateMap<PostComment.Comment, Post.Comment>()
            |> mapMember <@ fun x -> x.Body        @> <@ fun m -> MarkdownResolver.Resolve(m.Body) @>
            |> mapMember <@ fun x -> x.EmailHash   @> <@ fun m -> EmailHashResolver.Resolve(m.Email) @>
            |> mapMember <@ fun x -> x.IsImportant @> <@ fun m -> m.Important @>
            |> mapMember <@ fun x -> x.Url         @> <@ fun m -> UrlResolver.Resolve(m.Url) @>
            |> mapMember <@ fun x -> x.Tooltip     @> <@ fun m -> if String.IsNullOrEmpty(m.Url) then "Comment by " + m.Author else m.Url @>
            |> mapMember <@ fun x -> x.CreatedAt   @> <@ fun m -> m.CreatedAt.ToUniversalTime().ToString("MM/dd/yyyy hh:mm tt") @>
            |> ignore

            Mapper.CreateMap<Post, PostReference>()
            |> mapMember <@ fun x -> x.Title @> <@ fun m -> HttpUtility.HtmlDecode(m.Title) @>
            |> ignoreMember <@ fun x -> x.Slug @>
            |> ignore

            Mapper.CreateMap<Commenter, CommentInput>()
            |> ignoreMember <@ fun x -> x.Body @>
            |> mapMember <@ fun x -> x.CommenterKey @> <@ fun m -> m.Key @>
            |> ignore

            Mapper.CreateMap<CommentInput, Commenter>()
            |> ignoreMember <@ fun x -> x.Id @>
            |> ignoreMember <@ fun x -> x.IsTrustedCommenter @>
            |> ignoreMember <@ fun x -> x.Key @>
            |> ignoreMember <@ fun x -> x.OpenId @>
            |> ignoreMember <@ fun x -> x.NumberOfSpamComments @>
            |> ignore

            Mapper.CreateMap<User, CommentInput>()
            |> mapMember <@ fun x -> x.Name @> <@ fun m -> m.FullName @>
            |> mapMember <@ fun x -> x.Url  @> <@ fun m -> this.UrlHelper.RouteUrl("homepage") |> this.UrlHelper.RelativeToAbsolute @>
            |> ignoreMember <@ fun x -> x.Body @>
            |> ignoreMember <@ fun x -> x.CommenterKey @>
            |> ignore

// TODO: have to convert tasks before I can turn this on
//            Mapper.CreateMap<HttpRequestWrapper, Tasks.AddCommentTask.RequestValues>()
//            |> ignore

            Mapper.CreateMap<User, Post.UserDetails>()
            |> ignore


module AutoMapperConfiguration =
    open Converters
    open Profiles

    let Configure () =
        Mapper.CreateMap<string, MvcHtmlString>().ConvertUsing<MvcHtmlStringConverter>() |> ignore
        Mapper.CreateMap<Guid, string>().ConvertUsing<GuidToStringConverter>() |> ignore
        Mapper.CreateMap<DateTimeOffset, DateTime>().ConvertUsing<DateTimeTypeConverter>() |> ignore

        // TODO: It would make sense to add all of these automatically with an IoC.
        Mapper.AddProfile(PostViewModelMapperProfile())
        Mapper.AddProfile(PostsViewModelMapperProfile())
        Mapper.AddProfile(TagsListViewModelMapperProfile())
        Mapper.AddProfile(SectionMapperProfile())
        Mapper.AddProfile(SectionMapperProfile())
        Mapper.AddProfile(EmailViewModelMapperProfile())
        Mapper.AddProfile(UserAdminMapperProfile())
        Mapper.AddProfile(PostsAdminViewModelMapperProfile())
