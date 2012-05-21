namespace BadgerBlog.Tests

open Xunit
open FsUnit.Xunit
open AutoMapper
open BadgerBlog.Web
open BadgerBlog.Models
open BadgerBlog.ViewModels

type AutoMapperTests() =

    static do AutoMapperConfiguration.Configure()

    [<Fact>] 
    member test.``Assert configuration is valid`` () =
        Mapper.AssertConfigurationIsValid()

    [<Fact>]
    member test.``Can map from Comment to NewCommentViewModel`` () =
        let comment = PostComment.Comment(Author = "Joel", Body = "*test*")
        let ncvm = comment.MapTo<NewCommentEmailViewModel>()
        ncvm.Author |> should equal comment.Author
        // mapping should have resolved Markup formatting to HTML:
        ncvm.Body.ToString().Trim() |> should equal "<p><em>test</em></p>"
