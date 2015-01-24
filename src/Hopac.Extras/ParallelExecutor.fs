namespace Hopac.Extras

open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Alt.Infixes

/// Worker execution error.
type WorkerError<'a> =
    /// The message which causes this error is queued to `failedMessages` queue 
    /// and is executed later.
    | Recoverable of 'a
    /// The message which causes this error is not executed again. 
    | Fatal of 'a

/// Distributes messages among up to `degree` `worker`s which run in parallel. Degree of parallelism can be 
/// dynamically changed. If `worker` returns `Recoverable` WorkerError as a result, the message is queued to
/// special `failedMessages` Mailbox which is used as an alternative source of messages, i.e. messages are 
/// taken from `source` and `failedMessages` non deterministically. 
type ParallelExecutor<'msg, 'res, 'error>
    (
        degree: int,
        source: Alt<'msg>, 
        worker: 'msg -> Job<Choice<'res, 'msg * WorkerError<'error>>>
    ) =
    let setDegree = ch<int>() 
    let workDone = ch<Choice<'res, 'msg * WorkerError<'error>>>()
    let failedMessages = mb()
     
    let pool = Job.iterateServer (degree, 0)  <| fun (degree, usage) ->
        (setDegree |>>? fun degree -> degree, usage) <|>? 
        (workDone |>>? fun _ -> degree, usage - 1) <|>?
        (if usage < degree then
            source <~>? failedMessages >>=? fun msg -> 
                job {
                    let! result = worker msg 
                    match result with
                    | Fail (msg, Recoverable _) -> do! failedMessages <<-+ msg
                    | Fail (_, Fatal _)
                    | Ok _ -> () 
                    do! workDone <-- result }
                |> Job.queue
            >>% (degree, usage + 1)
         else Alt.never()) 
    do start pool
    /// Sets new degree of parallelism.
    member __.SetDegree value = setDegree <-+ value |> run