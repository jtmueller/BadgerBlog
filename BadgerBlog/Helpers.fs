namespace BadgerBlog

module Constants =
    let [<Literal>] DefaultPage = 1
    let [<Literal>] PageSize = 25

type DynamicContentType =
    | Markdown = 0
    | Html = 1
    | Video = 2
    | HttpRedirection = 3

type IDynamicContent =
    abstract member Body : string with get, set
    abstract member ContentType : DynamicContentType with get, set

type ISearchable =
    abstract Slug : string with get, set
    abstract Title : string with get, set
    abstract Content : string with get, set

namespace BadgerBlog.Web.Helpers.Attributes

open System
open System.ComponentModel.DataAnnotations
open System.Globalization
open System.Web.Mvc

[<Sealed; AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)>]
type AjaxOnlyAttribute() =
    inherit ActionFilterAttribute()

    override x.OnActionExecuting(filterContext) =
        let request = filterContext.HttpContext.Request
        if not <| request.IsAjaxRequest() then
            filterContext.Result <- HttpNotFoundResult("Only Ajax calls are permitted.")

[<Sealed; AttributeUsage(AttributeTargets.Field ||| AttributeTargets.Property, AllowMultiple = false, Inherited = true)>]
type ValidatePasswordLengthAttribute() =
    inherit ValidationAttribute(errorMessage = "'{0}' must be at least {1} characters long.")
    let minChars = 6

    override x.FormatErrorMessage(name) =
        String.Format(CultureInfo.CurrentCulture, x.ErrorMessageString, name, minChars)

    override x.IsValid(value) =
        match value with
        | :? string as s ->
            s.Length >= minChars
        | _ ->
            false

    interface IClientValidatable with
        member x.GetClientValidationRules(metadata, context) =
            seq { yield upcast ModelClientValidationStringLengthRule(x.FormatErrorMessage(metadata.GetDisplayName()), 
                                                                     minChars, Int32.MaxValue) }


namespace BadgerBlog.Web.Helpers.Binders

open System
open System.Web.Mvc
open BadgerBlog

type RemoveSpacesEnumBinder() =
    inherit DefaultModelBinder()

    override x.BindModel(controllerContext, bindingContext) =
        let value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName)
        if isNotNull value then
            match value.RawValue with
            | :? (string[]) as values ->
                for i = 0 to values.Length - 1 do
                    values.[i] <- values.[i].Replace(" ", String.Empty)
            | _ -> ()

        base.BindModel(controllerContext, bindingContext)

type GuidBinder() =
    inherit DefaultModelBinder()
    
    override x.BindModel(controllerContext, bindingContext) =
        let value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName)
        if isNull value then
            box Guid.Empty
        else
            match Guid.TryParse(value.AttemptedValue) with
            | true, parsed -> box parsed
            | _ -> box Guid.Empty

namespace BadgerBlog.Web.Helpers.Results

open System
open System.Diagnostics
open System.Text
open System.Web.Mvc
open Newtonsoft.Json
open BadgerBlog

type JsonNetResult() =
    inherit ActionResult()

    /// Gets or sets the serializer settings.
    member val Settings : JsonSerializerSettings = null with get, set

    /// Gets or sets the encoding of the response.
    member val ContentEncoding : Encoding = null with get, set

    /// Gets or sets the content type for the response.
    member val ContentType : string = null with get, set

    /// Gets or sets the body of the response
    member val ResponseBody : obj = null with get, set

    /// Gets the formatting types depending on whether we are in debug mode.
    member private x.Formatting
        with get () = if Debugger.IsAttached then Formatting.Indented else Formatting.None

    /// Serializes the response and writes it out to the response object.
    override x.ExecuteResult(context) =
        if isNull context then nullArg "context"

        let response = context.HttpContext.Response

        response.ContentType <-
            if String.IsNullOrEmpty(x.ContentType)
            then "application/json"
            else x.ContentType

        if isNotNull x.ContentEncoding then
            response.ContentEncoding <- x.ContentEncoding

        if isNotNull x.ResponseBody then
            JsonConvert.SerializeObject(x.ResponseBody, x.Formatting, x.Settings)
            |> response.Write


namespace BadgerBlog.Web.Helpers.Validation

open System
open System.ComponentModel.DataAnnotations

type NonEmptyGuidAttribute() =
    inherit ValidationAttribute()

    let testGuid g =
        if g = Guid.Empty then
            ValidationResult("Value cannot be empty.")
        else
            ValidationResult.Success

    override x.IsValid(value, context) =
        match value with
        | null ->
            ValidationResult.Success
        | :? Guid as g ->
            testGuid g
        | :? string as s ->
            match Guid.TryParse s with
            | true, g ->
                testGuid g
            | false, _ ->
                ValidationResult("The value is not compatible with the Guid type.")
        | _ ->
            ValidationResult.Success

open System.Configuration
open System.Web
open Recaptcha

type RecaptchaValidatorWrapper() =
    static let challengeFieldKey = "recaptcha_challenge_field"
    static let responseFieldKey = "recaptcha_response_field"

    static member Validate(context: HttpContextBase) =
        let request = context.Request

        let validator =
            RecaptchaValidator(PrivateKey = ConfigurationManager.AppSettings.["ReCaptchaPrivateKey"],
                               RemoteIP = request.UserHostAddress,
                               Challenge = request.Form.[challengeFieldKey],
                               Response = request.Form.[responseFieldKey])

        let response = validator.Validate()
        response.IsValid

open System.Web.Mvc
open BadgerBlog

type RequiredIfAttribute(dependentProperty, targetValue) =
    inherit ValidationAttribute()

    let innerAttribute = RequiredAttribute()
    
    member val DependentProperty : string = dependentProperty with get, set
    member val TargetValue : obj = targetValue with get, set

    override x.IsValid(value, context) =
        // get a reference to the property this validation depends upon
        let containerType = context.ObjectInstance.GetType()
        let field = containerType.GetProperty(x.DependentProperty)

        if isNull field then
            ValidationResult.Success
        else
            // get the value of the dependent property
            let dependentvalue = field.GetValue(context.ObjectInstance, null)

            // compare the value against the target value
            if (isNull dependentvalue && isNull x.TargetValue) ||
               (isNotNull dependentvalue && dependentvalue.Equals(x.TargetValue)) then
                if innerAttribute.IsValid(value) then
                    ValidationResult.Success
                else
                    ValidationResult(x.ErrorMessage, [ context.MemberName ])
            else
                ValidationResult.Success

    member private x.BuildDependentPropId(metadata:ModelMetadata, context:ViewContext) =
        // build the ID of the property
        let depProp = context.ViewData.TemplateInfo.GetFullHtmlFieldId(x.DependentProperty)
        // unfortunately this will have the name of the current field appended to the beginning,
        // because the TemplateInfo's context has had this fieldname appended to it. Instead, we
        // want to get the context as though it was one level higher (i.e. outside the current property,
        // which is the containing object (our Person), and hence the same level as the dependent property.
        let thisField = metadata.PropertyName + "_"
        if depProp.StartsWith(thisField) then
            // strip it off again
            depProp.Substring(thisField.Length)
        else
            depProp

    interface IClientValidatable with
        member x.GetClientValidationRules(metadata, context) =
            let rule = 
                ModelClientValidationRule(
                    ErrorMessage = x.FormatErrorMessage(metadata.GetDisplayName()),
                    ValidationType = "requiredif")

            let depProp = x.BuildDependentPropId(metadata, context :?> ViewContext)

            // find the value on the control we depend on;
            // if it's a bool, format it javascript style (lower case)

            let targetVal =
                match x.TargetValue with
                | null -> ""
                | :? bool as b -> b.ToString().ToLowerInvariant()
                | o -> o.ToString()

            rule.ValidationParameters.Add("dependentproperty", depProp)
            rule.ValidationParameters.Add("targetvalue", targetVal)

            seq { yield rule }

namespace BadgerBlog.Web.Helpers

open System
open System.Web
open System.Web.Mvc
open System.Web.Routing
open System.Collections.Generic
open BadgerBlog

module CommenterUtil =
    let [<Literal>] commenterCookieName = "commenter"

    let setCommenterCookie (response:HttpResponseBase) (commenterKey) =
        let cookie = HttpCookie(commenterCookieName, commenterKey, Expires = DateTime.Now.AddYears(1))
        response.Cookies.Add(cookie)

module ViewExtensions =
    let IsDebug () =
#if DEBUG
        true
#else
        false
#endif

[<AutoOpen>]
module MvcExtensions =

    type LowercaseRoute(url, defaults, constraints) =
        inherit Route(url, defaults, constraints, RouteValueDictionary(), MvcRouteHandler())

        override x.GetVirtualPath(requestContext, values) =
            let path = base.GetVirtualPath(requestContext, values)

            if isNotNull path then
                path.VirtualPath <- path.VirtualPath.ToLowerInvariant()

            path

    type RouteCollection with
        member x.MapRouteLowerCase(name, url, ?defaults:IDictionary<string,obj>, ?constraints:IDictionary<string,obj>, ?namespaces:string[]) =
            if String.IsNullOrEmpty(url) then nullArg "url"

            let route = LowercaseRoute(url, 
                                       RouteValueDictionary(defaults |? null),
                                       RouteValueDictionary(constraints |? null))
            
            match namespaces with
            | Some ns when ns.Length > 0 ->
                route.DataTokens.["Namespaces"] <- ns
            | _ -> ()
            
            x.Add(name, route)

    type ModelStateDictionary with
        member this.FirstErrorMessage() =
            let state = this.Values |> Seq.tryFind (fun v -> v.Errors.Count > 0)

            match state with
            | None    -> None
            | Some st ->
                st.Errors
                |> Seq.map (fun err -> err.ErrorMessage)
                |> Seq.tryFind (String.IsNullOrEmpty >> not)

    open System.Text.RegularExpressions

    let private codeBlockFinder = Regex(@"\[code lang=(.+?)\s*\](.*?)\[/code\]", RegexOptions.Compiled ||| RegexOptions.Singleline)
    let private firstLineSpacesFinder = Regex(@"^(\s|\t)+", RegexOptions.Compiled)

    let private getFirstLineSpaces firstLine =
        if isNull firstLine then String.Empty
        else
            let m = firstLineSpacesFinder.Match(firstLine)
            if m.Success then
                firstLine.Substring(0, m.Length)
            else
                String.Empty

    let private convertMdCodeStatement (code:string) =
        let lines = code.Split([| Environment.NewLine |], StringSplitOptions.None)
        let firstLineSpaces = if lines.Length = 0 then String.Empty else getFirstLineSpaces lines.[0]
        let flsLen = firstLineSpaces.Length
        let formattedLines =
            lines |> Seq.map (fun l -> sprintf "    %s" (l.Substring(if l.Length < flsLen then 0 else flsLen)))
        String.Join(Environment.NewLine, formattedLines)

    let private generateCodeBlock lang code =
        let code = HttpUtility.HtmlDecode(code)
        String.Format("<pre class=\"brush: {2}\">{0}{1}</pre>{0}", Environment.NewLine,
                      (convertMdCodeStatement code).Replace("<", "&lt;"), // to support syntax highlighting on pre tags
                      lang)

    type IDynamicContent with
        member this.CompiledContent(trustContent) =
            if isNull this then MvcHtmlString.Empty
            else
                match this.ContentType with
                | DynamicContentType.Markdown ->
                    let md = MarkdownDeep.Markdown(
                                AutoHeadingIDs = true,
                                ExtraMode = true,
                                NoFollowLinks = not trustContent,
                                SafeMode = false,
                                NewWindowForExternalLinks = true)
                    let contents = 
                        codeBlockFinder.Replace(this.Body,
                            fun (m:Match) -> generateCodeBlock (m.Groups.[1].Value.Trim()) m.Groups.[2].Value)

                    try
                        md.Transform(contents)
                    with _ ->
                        sprintf "<pre>%s</pre>" (HttpUtility.HtmlEncode contents)
                    |> MvcHtmlString.Create

                | DynamicContentType.Html when trustContent ->
                    MvcHtmlString.Create(this.Body)
                | _ ->
                    MvcHtmlString.Empty


open System.Runtime.CompilerServices
open System.Configuration
open System.Web.UI
open JetBrains.Annotations
open Recaptcha
open ImpromptuInterface.FSharp

type TwitterButtonDataCount =
    | None = 0
    | Horizontal = 1
    | Vertical = 2

[<AutoOpen>]
module UrlHelperExtensions =
    // UrlHelper extensions:

    let private cssDir, private scriptDir, private revisionNumber = "css", "js", 2
    let private contentPath = sprintf "~/Content/%s/%s?version=%i"
    let private absoluteAction (url:UrlHelper) relativeUrl =
        let request = url.RequestContext.HttpContext.Request
        let requestUrl = request.Url
        sprintf "%s://%s%s" requestUrl.Scheme requestUrl.Authority relativeUrl
    
    type UrlHelper with
        member this.Css(fileName) =
            let fileName =
                match IO.Path.GetExtension(fileName) with
                | ".css" | ".less" -> fileName
                | _ -> fileName + ".css"

            this.Content(contentPath cssDir fileName revisionNumber)

        member this.Script(fileName) =
            let fileName =
                match IO.Path.GetExtension(fileName) with
                | ".js" -> fileName
                | _ -> fileName + ".js"

            this.Content(contentPath scriptDir fileName revisionNumber)

        member this.AbsoluteAction([<AspMvcAction>] action:string, routeValues:obj) =
            this.Action(action, routeValues) |> absoluteAction this

        member this.AbsoluteAction([<AspMvcAction>] action:string) =
            this.Action(action) |> absoluteAction this

        member this.AbsoluteAction([<AspMvcAction>] action:string, [<AspMvcController>] controller:string) =
            this.Action(action, controller) |> absoluteAction this

        member this.AbsoluteAction([<AspMvcAction>] action:string, [<AspMvcController>] controller:string, routeValues) =
            this.Action(action, controller, routeValues) |> absoluteAction this

        member this.RelativeToAbsolute(relativeUrl) =
            absoluteAction this relativeUrl

        member this.ActionLinkWithArray([<AspMvcAction>] action:string, [<AspMvcController>] controller:string, routeData) =
            let href = this.Action(action, controller, dict [ "area", box "" ])
            let rv = RouteValueDictionary(routeData)
            let parameters =
                if isNull routeData then Seq.empty
                else
                    let rdType = routeData.GetType()
                    let formatPair : string -> obj -> string = sprintf "%s=%O"
                    seq {
                        for key in rv.Keys do
                            let pi = rdType.GetProperty(key)
                            match pi.GetValue(routeData, null) with
                            | :? string as s ->
                                yield formatPair key s
                            | :? Collections.IEnumerable as array ->
                                for item in array do
                                    yield formatPair key item
                            | o ->
                                yield formatPair key o
                    }

            let paramStr = String.Join("&", parameters)
            MvcHtmlString.Create(if String.IsNullOrEmpty(paramStr) then href else href + "?" + paramStr)

/// C# compatible extension methods to be called from views:
[<Extension; Sealed>]
type CsMvcExtensions private () = 

    // HtmlHelper extensions:

    [<Extension>]
    static member Link(this:HtmlHelper, text, href, htmlAttributes) =
        let tag = TagBuilder("a", InnerHtml = text)

        if String.IsNotEmpty(href) then
            tag.Attributes.["href"] <- href

        HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes)
        |> Seq.map (fun kvp -> kvp.Key, kvp.Value.ToString())
        |> Seq.filter (snd >> String.IsNullOrEmpty >> not)
        |> Seq.iter tag.Attributes.Add
            
        tag.ToString(TagRenderMode.Normal)
        |> MvcHtmlString.Create

    [<Extension>]
    static member GenerateCaptcha(this:HtmlHelper) =
        use control =
            new RecaptchaControl(
                    ID = "recaptcha",
                    Theme = "clean",
                    PublicKey = ConfigurationManager.AppSettings.["ReCaptchaPublicKey"],
                    PrivateKey = ConfigurationManager.AppSettings.["ReCaptchaPrivateKey"])
        use writer = new HtmlTextWriter(new IO.StringWriter())
        control.RenderControl(writer)
        let html = writer.InnerWriter.ToString()
        MvcHtmlString.Create(html)

    [<Extension>]
    static member TwitterButton(this:HtmlHelper, content, dataCount:TwitterButtonDataCount, 
                                url, title, author:obj) =
        let tag = TagBuilder("a")
        tag.AddCssClass("twitter-share-button")
        tag.Attributes.["href"] <- "http://twitter.com/share"
        tag.Attributes.["data-count"] <- dataCount.ToString()

        if String.IsNotEmpty(author?TwitterNick) then
            tag.Attributes.["data-via"] <- author?TwitterNick

        if String.IsNotEmpty(author?RelatedTwitterNick) then
            tag.Attributes.["data-related"] <-
                if String.IsNotEmpty(author?RelatedTwitNickDes) then
                    author?RelatedTwitterNick + ":" + author?RelatedTwitNickDes
                else
                    author?RelatedTwitterNick

        if String.IsNotEmpty(url) then
            tag.Attributes.["data-url"] <- url
            tag.Attributes.["data-counturl"] <- url

        if String.IsNotEmpty(title) then
            tag.Attributes.["data-text"] <- title

        tag.InnerHtml <- content

        tag.ToString(TagRenderMode.Normal)
        |> MvcHtmlString.Create

    [<Extension>]
    static member TwitterButton(this:HtmlHelper, content, dataCount, author) =
        CsMvcExtensions.TwitterButton(this, content, dataCount, null, null, author)

module SlugConverter =
    open System.Globalization
    open System.Text
    open System.Text.RegularExpressions

    let private trim (s:string) = s.Trim()
    let private toLower (s:string) = s.ToLowerInvariant()

    /// Strips the value from any non English character by replacing those with their English equivalent.
    // See also: http://blogs.msdn.com/michkap/archive/2007/05/14/2629747.aspx
    //           http://stackoverflow.com/questions/249087/how-do-i-remove-diacritics-accents-from-a-string-in-net  
    let private removeDiacritics (value:string) =
        let sb = StringBuilder()
        value.Normalize(NormalizationForm.FormD)
        |> Seq.filter (fun c -> CharUnicodeInfo.GetUnicodeCategory(c) <> UnicodeCategory.NonSpacingMark)
        |> Seq.iter (sb.Append >> ignore)
        sb.ToString().Normalize(NormalizationForm.FormC)

    let private replaceNonWordWithDashes value =
        // remove apostrophes of various sorts
        let sb = StringBuilder()
        Regex.Replace(value, "[’'“”\"&]{1,}", "", RegexOptions.None)  // remove apostrophes of various sorts
        |> Seq.map (fun c -> if Char.IsLetterOrDigit(c) then c else ' ')
        |> Seq.iter (sb.Append >> ignore)

        Regex.Replace(sb.ToString(), "[ ]{1,}", "-", RegexOptions.None)

    let TitleToSlug = removeDiacritics >> toLower >> replaceNonWordWithDashes >> trim

module EmailHashResolver =
    open System.Security.Cryptography
    open System.Text

    let GetMd5Hash (input:string) =
        let sb = StringBuilder()
        let formatByte = Printf.bprintf sb "%02x"
        use md5 = MD5.Create()

        Encoding.Default.GetBytes(input)
        |> md5.ComputeHash
        |> Array.iter formatByte
        
        sb.ToString()

    let Resolve (email:string) =
        if isNull email then null
        else
            let str = email.Trim().ToLowerInvariant()
            GetMd5Hash(str)

module RavenIdResolver =
    open System.Text.RegularExpressions

    let Resolve ravenId =
        let m = Regex.Match(ravenId, @"\d+")
        if m.Success then
            let id = int m.Value
            if id = 0 then
                invalidArg "ravenId" "ID cannot be zero."
            id
        else
            invalidArg "ravenId" "ID must contain a number."

module UrlResolver =
    let Resolve url =
        if String.IsNullOrEmpty(url) then
            null
        elif url.StartsWith("http://") || url.StartsWith("https://") then
            url
        else
            "http://" + url

module TagsResolver =
    let private tagsSeparator = "|"

    let ResolveTags (tags:seq<string>) =
        String.Join(tagsSeparator, tags)

    let ResolveTagsInput (tags:string) =
        tags.Split([| tagsSeparator |], StringSplitOptions.RemoveEmptyEntries)

module SanitizeHtml =
    open System.Diagnostics
    open System.Text
    open System.Text.RegularExpressions

    let private tags = Regex("<[^><]*(>|$)",
                             RegexOptions.Singleline ||| RegexOptions.ExplicitCapture |||
                             RegexOptions.Compiled)
    let private whiteList = 
        Regex(@"
              ^</?(b(lockquote)?|code|d(d|t|l|el)|em|h(1|2|3)|i|kbd|li|ol|p(re)?|s(ub|up|trong|trike)?|ul)>$|
              ^<(b|h)r\s?/?>$",
              RegexOptions.Singleline ||| RegexOptions.ExplicitCapture ||| RegexOptions.Compiled |||
              RegexOptions.IgnorePatternWhitespace)

    let private whiteListA =
        Regex(@"
              ^<a\s
              href=""(\#\d+|(https?|ftp)://[-a-z0-9+&@#/%?=~_|!:,.;\(\)]+)""
              (\stitle=""[^""<>]+"")?\s?>$|
              ^</a>$",
              RegexOptions.Singleline ||| RegexOptions.ExplicitCapture ||| RegexOptions.Compiled |||
              RegexOptions.IgnorePatternWhitespace)

    let private whiteListImg =
        Regex(@"
              ^<img\s
              src=""https?://[-a-z0-9+&@#/%?=~_|!:,.;\(\)]+""
              (\swidth=""\d{1,3}"")?
              (\sheight=""\d{1,3}"")?
              (\salt=""[^""<>]*"")?
              (\stitle=""[^""<>]*"")?
              \s?/?>$",
              RegexOptions.Singleline ||| RegexOptions.ExplicitCapture ||| RegexOptions.Compiled |||
              RegexOptions.IgnorePatternWhitespace)

    let Sanitize html =
        if String.IsNullOrEmpty html then html
        else
            let tags = tags.Matches html
            let output = StringBuilder(html)
            for i = tags.Count - 1 downto 0 do
                let tag = tags.[i]
                let tagname = tag.Value.ToLowerInvariant()
                if not (whiteList.IsMatch(tagname) || whiteListA.IsMatch(tagname) || whiteListImg.IsMatch(tagname)) then
                    output.Remove(tag.Index, tag.Length) |> ignore
                    Debug.WriteLine("tag sanitized: " + tagname)
            output.ToString()

module MarkdownResolver =
    let FormatMarkdown content =
        let md = MarkdownDeep.Markdown()
        try
            md.Transform(content)
        with _ ->
            sprintf "<pre>%s</pre>" (HttpUtility.HtmlEncode content)

    let Resolve =
        FormatMarkdown >> SanitizeHtml.Sanitize >> MvcHtmlString.Create