namespace BadgerBlog.Tests

open System
open Xunit
open BadgerBlog
open BadgerBlog.Web.Helpers

module ``SlugConverter Tests`` =

    let conv = SlugConverter.TitleToSlug

    [<Fact>]
    let ``Replaces spaces with dashes`` () =
        Assert.Equal<string>("ef-prof", conv "EF Prof")

// References:
// http://refactormycode.com/codes/333-sanitize-html
module ``MarkdownResolver Tests`` =
    let markdown = MarkdownResolver.Resolve >> string

    [<Fact>]
    let ``Allow Markdown Link`` () =
        let input = "[example](http://example.com \"merely an example\")"
        let result = markdown input
        let expected = "<a href=\"http://example.com\" title=\"merely an example\">example</a>"
        Assert.Contains(expected, result)

    [<Fact>]
    let ``Images cannot contain script code`` () =
        Assert.DoesNotContain("img", markdown "<img src=\"javascript:alert('hack!')\">")

    [<Fact>]
    let ``Allow Markdown Bold`` () =
        let input = "**bold**"
        let result = markdown input
        let expected = "<strong>bold</strong>"
        Assert.Contains(expected, result)

    [<Fact>]
    let ``Allow Markdown Italic`` () =
        let input = "*italic*, _italic_"
        let result = markdown input
        let expected = "<em>italic</em>, <em>italic</em>"
        Assert.Contains(expected, result)

    [<Fact>]
    let ``Allow Markdown code block`` () =
        let input = "`CodeBlock`"
        let result = markdown input
        let expected = "<code>CodeBlock</code>"
        Assert.Contains(expected, result)

    [<Fact>]
    let ``Allow Raw Link`` () =
        let url = "http://example.com?query=value#item"
        let result = markdown url
        let expected = sprintf "<a href=\"%s\">%s</a>" url url
        Assert.Contains(expected, result)

    [<Fact>]
    let ``Block all other HTML tags`` () =
        let blacklistTags = "abbr|acronym|address|applet|area|base|basefont|bdo|big|body|button|caption|center|cite|col|colgroup|dir|div|dfn|embed|fieldset|font|form|frame|frameset|head|html|iframe|img|input|ins|isindex|label|legend|link|map|menu|meta|noframes|noscript|object|optgroup|option|param|q|samp|script|select|small|span|style|table|tbody|td|textarea|tfoot|th|thead|title|tr|tt|var|xmp"
        let tags =
            blacklistTags.Split('|')
            |> Seq.map (fun tag -> String.Format("<{0}>{0}</{0}>", tag))
        for tag in tags do
            let result = markdown tag
            Assert.DoesNotContain(tag, result)

    [<Fact>]
    let ``Whitelist HTML tags`` () =
        let whitelistTags = "code|dd|del|dl|dt|kbd|pre|s|strike"
        let tags =
            whitelistTags.Split('|')
            |> Seq.map (fun tag -> String.Format("<{0}>{0}</{0}>", tag))
        for tag in tags do
            let result = markdown tag
            Assert.Contains(tag, result)

    [<Fact>]
    let ``Do not sanitize the less-than symbol when it's not part of a tag`` () =
        let input = @"
<pre>string s = """";
for (int i = 0; i < 13000; i++)
{
	s += (char) i;
}</pre>"
        let result = markdown input
        Assert.Contains("for (int i = 0; i < 13000; i++)", result)

    [<Fact>]
    let ``Will not crash for this comment`` () =
        let input = @"
 public static IEnumerable
<t RobustEnumerating
<t(
  
			IEnumerable
<t input, Func
<ienumerable<t, IEnumerable
<t> func)
  
		{
  
			// how to do this?
  
			IList
<t list = new List
<t();
  
			int counter = 0;
  
			foreach (var enumerable in input)
  
			{
  
				if (counter % 2 != 0)
  
					list.Add(enumerable);
  
				counter++;           
  
			}
  
			input = list.AsEnumerable();
  
			return func(input);
  
  
		}
>
"
        Assert.DoesNotThrow(fun () -> markdown input |> ignore)