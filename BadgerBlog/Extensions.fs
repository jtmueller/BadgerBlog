[<AutoOpen>]
module BadgerBlog.Extensions

    open System
    open System.Web

    type Async with
        static member AwaitTask(task:System.Threading.Tasks.Task, ?timeout) =
            Async.AwaitIAsyncResult(task, ?millisecondsTimeout = timeout)
            |> Async.Ignore

        static member AsBeginEndHandlers(computation) =
            let start, finish, _ = Async.AsBeginEnd(computation)
            let bh = BeginEventHandler(fun sender e cb data -> start(sender, cb, data))
            let eh = EndEventHandler(finish >> ignore)
            bh, eh

    type String with
        static member IsNotEmpty = (String.IsNullOrEmpty >> not)

    type DateTimeOffset with
        static member ConvertFromUnixTimestamp (timestamp:int64) =
            let origin = DateTimeOffset(1970, 1, 1, 0, 0, 0, 0, DateTimeOffset.Now.Offset)
            origin.AddSeconds(float timestamp)

        static member ConvertFromJsTimestamp (timestamp:int64) =
            let origin = DateTimeOffset(1970, 1, 1, 0, 0, 0, 0, DateTimeOffset.Now.Offset)
            origin.AddMilliseconds(float timestamp)
               
        member this.AsMinutes() =
            DateTimeOffset(this.Year, this.Month, this.Day, this.Hour, this.Minute, 0, 0, this.Offset)

        member this.AtNoon() =
            DateTimeOffset(this.Year, this.Month, this.Day, 12, 0, 0, 0, this.Offset)

        member this.AtTime(time:DateTimeOffset) =
            DateTimeOffset(this.Year, this.Month, this.Day, time.Hour, time.Minute, time.Second, time.Millisecond, this.Offset)

        member this.WithDate(date:DateTimeOffset) =
            DateTimeOffset(date.Year, date.Month, date.Day, this.Hour, this.Minute, this.Second, this.Millisecond, this.Offset)

        member this.SkipToNextWorkDay() =
            // we explicitly choose not to user the CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek
            // because we want it to be fixed for what we need, not whatever the user for this is set to.
            match this.DayOfWeek with
            | DayOfWeek.Saturday ->
                this.AddDays(2.0)
            | DayOfWeek.Sunday ->
                this.AddDays(1.0)
            | _ ->
                this

