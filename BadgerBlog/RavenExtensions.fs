[<AutoOpen>]
module BadgerBlog.RavenExtensions

    type Raven.Client.IAsyncDocumentSession with
        member x.AsyncSaveChanges() = 
            x.SaveChangesAsync() |> Async.AwaitTask

        member x.AsyncLoad(ids:string[]) =
            x.LoadAsync(ids) |> Async.AwaitTask

        member x.AsyncLoad(id:string) =
            x.LoadAsync(id) |> Async.AwaitTask

        member x.AsyncLoad(id:System.ValueType) =
            x.LoadAsync(id) |> Async.AwaitTask

        // TODO: stuff from DocumentSessionExtensions

    type Raven.Client.Indexes.IndexCreation with
        static member AsyncCreateIndexes(assembly:System.Reflection.Assembly, docStore) =
            Raven.Client.Indexes.IndexCreation.CreateIndexesAsync(assembly, docStore)
            |> Async.AwaitTask

