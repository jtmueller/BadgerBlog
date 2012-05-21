namespace BadgerBlog.Models

open System
open System.Collections.Generic
open System.ComponentModel.DataAnnotations
open System.Security.Cryptography
open System.Text
open System.Web.Mvc
open BadgerBlog
open BadgerBlog.Web.Helpers
open BadgerBlog.Web.Helpers.Validation

type BlogConfig() =
    [<HiddenInput>]
    member val Id = "Blog/Config" with get, set

    [<Required; Display(Name = "Blog Title")>]
    member val Title : string = null with get, set

    [<Required; Display(Name = "Owner Email")>]
    member val OwnerEmail : string = null with get, set

    [<Display(Name = "Custom CSS")>]
    member val CustomCss = "hibernatingrhinos" with get, set

    [<Display(Name = "Copyright String")>]
    member val Copyright : string = null with get, set

    [<Display(Name = "Akismet Key")>]
    member val AkismetKey : string = null with get, set

    [<Display(Name = "Google-Analytics Key")>]
    member val GoogleAnalyticsKey : string = null with get, set

    [<Display(Name = "Future Posts Encryption IV")>]
    member val FuturePostsEncryptionIV : string = null with get, set

    [<Display(Name = "Future Posts Encryption Key")>]
    member val FuturePostsEncryptionKey : string = null with get, set

    [<Display(Name = "Meta Description")>]
    member val MetaDescription : string = null with get, set

    [<Display(Name = "Minimum Posts for Significant Tag")>]
    member val MinNumberOfPostsForSignificantTag = 0 with get, set

    [<Display(Name = "Number of Days to Close Comments")>]
    member val NumberOfDaysToCloseComments = 0 with get, set

    static member NewDummy() =
        BlogConfig(Id = "Blog/Config/Dummy")

// Section can contain:
//   1. Body = any html text
//   2. Can point to any internal action.
type Section() =
    [<HiddenInput>]
    member val Id : string = null with get, set

    [<Required; Display(Name = "Title")>]
    member val Title : string = null with get, set

    [<Required; Display(Name = "Active?")>]
    member val IsActive = false with get, set

    [<Display(Name = "Position")>]
    member val Position = 0 with get, set

    [<AllowHtml; Display(Name = "Body"); DataType(DataType.MultilineText)>]
    member val Body : string = "" with get, set

    [<RequiredIf("Body", ""); Display(Name = "Controller Name")>]
    member val ControllerName : string = null with get, set

    [<RequiredIf("Body", ""); Display(Name = "Action Name")>]
    member val ActionName : string = null with get, set

    member x.IsNewSection() =
        String.IsNullOrEmpty(x.Id)

type User() =
    static let [<Literal>] saltConst = "xi07cevs01q4#"
    static let getHashedPwd pwd pwSalt =
        use sha = SHA256.Create()
        (pwSalt + pwd + saltConst)
        |> Encoding.Unicode.GetBytes
        |> sha.ComputeHash
        |> Convert.ToBase64String

    let mutable pwSalt : string = null

    member val Id : string = null with get, set
    member val FullName : string = null with get, set
    member val Email : string = null with get, set
    member val Enabled = false with get, set
    
    member val TwitterNick : string = null with get, set
    member val RelatedTwitterNick : string = null with get, set
    member val RelatedTwitNickDes : string = null with get, set

    member val internal HashedPassword : string = null with get, set
    member private x.PasswordSalt
        with get () =
            if isNull pwSalt then
                pwSalt <- Guid.NewGuid().ToString("N")
            pwSalt
        and set v =
            pwSalt <- v

    member x.SetPassword pwd =
        x.HashedPassword <- getHashedPwd pwd x.PasswordSalt

    member x.ValidatePassword maybePwd =
        if isNull x.HashedPassword then
            true
        else
            x.HashedPassword = getHashedPwd maybePwd x.PasswordSalt

type Post() =
    let mutable _showPostEvenIfPrivate = Guid.Empty
    
    member val Id : string = null with get, set
    member val Title : string = null with get, set
    member val LegacySlug : string = null with get, set
    member val Body : string = null with get, set
    member val ContentType = DynamicContentType.Html with get, set
    member val Tags : ICollection<string> = null with get, set
    member val AuthorId : string = null with get, set
    member val CreatedAt = DateTimeOffset.MinValue with get, set
    member val PublishAt = DateTimeOffset.MaxValue with get, set
    member val SkipAutoReschedule = false with get, set
    member val LastEditedByUserId : string = null with get, set
    member val LastEditedAt : DateTimeOffset option = None with get, set
    member val IsDeleted = false with get, set
    member val AllowComments = true with get, set

    member x.ShowPostEvenIfPrivate
        with get () =
            if _showPostEvenIfPrivate = Guid.Empty then
                _showPostEvenIfPrivate <- Guid.NewGuid()
            _showPostEvenIfPrivate
        and set v =
            _showPostEvenIfPrivate <- v

    member val CommentsCount = 0 with get, set
    member val CommentsId : string = null with get, set

    member x.TagsAsSlugs
        with get () =
            seq {
                if isNotNull x.Tags then
                    for tag in x.Tags do
                        yield SlugConverter.TitleToSlug tag
            }

    member x.IsPublicPost(key:Guid) =
        if x.IsDeleted then
            false
        elif x.PublishAt <= DateTimeOffset.Now then
            true
        else
            key <> Guid.Empty && key = x.ShowPostEvenIfPrivate

    interface IDynamicContent with
        member x.Body
            with get () = x.Body
            and  set v  = x.Body <- v

        member x.ContentType
            with get () = x.ContentType
            and  set v  = x.ContentType <- v
    
type PostInput() =
    [<HiddenInput>]
    member val Id = 0 with get, set

    [<Required; Display(Name = "Title")>]
    member val Title = "" with get, set

    [<AllowHtml; Required; Display(Name = "Body"); DataType(DataType.MultilineText)>]
    member val Body = "" with get, set

    [<Required; Display(Name = "Content Type")>]
    member val ContentType = DynamicContentType.Html with get, set

    [<Display(Name = "Created At"); DataType(DataType.DateTime)>]
    member val CreatedAt = DateTimeOffset.MinValue with get, set

    [<Display(Name = "Publish At"); DataType(DataType.DateTime)>]
    member val PublishAt = DateTimeOffset.MaxValue with get, set

    [<Display(Name = "Tags")>]
    member val Tags = "" with get, set

    [<Display(Name = "Allow Comments?")>]
    member val AllowComments = true with get, set

    member x.IsNewPost() = x.Id = 0
    

module PostComment =
    // F# doesn't allow nested types except in modules
    type PostReference() =
        member val Id : string = null with get, set
        member val PublishAt : DateTimeOffset = DateTimeOffset.MaxValue with get, set

    type Comment() =
        member val Id = 0 with get, set
        member val Body : string = null with get, set
        member val Author : string = null with get, set
        member val Email : string = null with get, set
        member val Url : string = null with get, set
        member val Important : bool = false with get, set
        member val IsSpam : bool = true with get, set
        member val CreatedAt = DateTimeOffset.MinValue with get, set
        member val UserHostAddress : string = null with get, set
        member val UserAgent : string = null with get, set
        member val CommenterId : string = null with get, set
    
type PostComments() =
    member val Id : string = null with get, set
    member val Post : PostComment.PostReference option = None with get, set
    member val Comments : ResizeArray<PostComment.Comment> = null with get, set
    member val Spam : ResizeArray<PostComment.Comment> = null with get, set
    member val LastCommentId = 0 with get, set
    
    member x.GenerateNewCommentId() =
        let next = x.LastCommentId + 1
        x.LastCommentId <- next
        next

    member x.AreCommentsClosed(post:Post, daysToClose) =
        if daysToClose < 1 then false
        else
            let lastCommentDate =
                if x.Comments.Count = 0 then post.PublishAt
                else x.Comments |> Seq.map (fun c -> c.CreatedAt) |> Seq.max
            DateTimeOffset.Now - lastCommentDate > TimeSpan.FromDays(float daysToClose)

type Commenter() =
    member val Id : string = null with get, set
    member val Key = Guid.Empty with get, set
    member val IsTrustedCommenter : bool option = None with get, set
    
    member val Name : string = null with get, set
    member val Email : string = null with get, set
    member val Url : string = null with get, set

    member val OpenId : string = null with get, set

    member val NumberOfSpamComments = 0 with get, set
