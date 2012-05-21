module BadgerBlog.Indexes

    // NOTE: RavenDB indexes defined in C# can use strongly-typed LINQ expressions
    // and anonymous types. F# doesn't have anonymous types, so our only option is to
    // define the indexes using C#-style LINQ expressions inside strings.

    open System
    open Raven.Abstractions.Indexing
    open BadgerBlog.Models

    type Tags_Count_Result = {
        Name : string
        Count : int
        LastSeenAt : DateTimeOffset
    }

    type Posts_Statistics_Result = {
        PostsCount : int
        CommentsCount : int
    }

    type Posts_ByMonthPublished_Result = {
        Year : int
        Month : int
        Count : int
    }

    type PostComments_CreationDate_Result = {
        CreatedAt : DateTimeOffset
        CommentId : int
        PostCommentsId : int
        PostId : int
        PostPublishAt : DateTimeOffset
    }

    let Definitions = [

        "Tags_Count",
        IndexDefinition(
            Map = "from post in posts \
                   from tag in post.Tags \
                   select new {Name = tag.ToString().ToLower(), Count = 1, LastSeenAt = post.PublishAt}",
            Reduce = "from tagCount in results \
                      group tagCount by tagCount.Name \
                      into g \
                      select new {Name = g.Key, Count = g.Sum(x => x.Count), LastSeenAt = g.Max(x=>(DateTimeOffset)x.LastSeenAt) }"
        )

        "Posts_Statistics",
        IndexDefinition(
            Map = "from postComment in postComments \
                   select new { PostsCount = 1, CommentsCount = postComment.Comments.Count }",
            Reduce = "from result in results \
                      group result by \"constant\" into g \
                      select new \
                      { \
	                      PostsCount = g.Sum(x => x.PostsCount), \
	                      CommentsCount = g.Sum(x => x.CommentsCount) \
                      }"
        )

        "Posts_ByMonthPublished_Count",
        IndexDefinition(
            Map = "from post in posts \
                   select new { post.PublishAt.Year, post.PublishAt.Month, Count = 1 }",
            Reduce = "from result in results \
                      group result by new { result.Year, result.Month } \
                      into g \
                      select new { g.Key.Year, g.Key.Month, Count = g.Sum(x => x.Count) }",
            SortOptions = dict [ "Month", SortOptions.Int
                                 "Year",  SortOptions.Int ]
        )

        "PostComments_CreationDate",
        IndexDefinition(
            Map = "from postComment in postComments \
                   from comment in postComment.Comments \
                   where comment.IsSpam == false \
                   select new \
                          { \
                              comment.CreatedAt, \
                              CommentId = comment.Id, \
                              PostCommentsId = postComment.Id, \
                              PostId = postComment.Post.Id, \
                              PostPublishAt = postComment.Post.PublishAt \
                          }",

            Stores = dict [ "CreatedAt", FieldStorage.Yes
                            "CommentId", FieldStorage.Yes
                            "PostId", FieldStorage.Yes
                            "PostCommentsId", FieldStorage.Yes
                            "PostPublishAt", FieldStorage.Yes ]
        )

    ]