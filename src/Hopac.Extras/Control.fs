namespace Hopac.Extras

open System
open Hopac
open Hopac.Infixes
open System.Threading.Tasks
open Hopac.Core

module Job =
  let catchResult (j: Job<_>) =
    job {
      try
        let! r = j
        return Ok r
      with e ->
        return Error e
    }

module JobResult = 

  let bind (xJ: Job<Result<'x, 'e>>) (x2yJ: 'x -> Job<Result<'y, 'e>>) : Job<Result<'y, 'e>> =
    xJ >>= function
    | Error error -> Job.result <| Error error 
    | Ok x -> x2yJ x 
  
  let bindAsync (xA: Async<Result<'x, 'e>>) (x2yJ: 'x -> Job<Result<'y, 'e>>) : Job<Result<'y, 'e>> =
    bind (Job.fromAsync xA) x2yJ
  
  let bindTask (xT: Task<Result<'x, 'e>>) (x2yJ: 'x -> Job<Result<'y, 'e>>) : Job<Result<'y, 'e>> =
    bind (Job.awaitTask xT) x2yJ
  
  let bindVoidTask (uT: Task) (u2xJ: unit -> Job<Result<'x, 'e>>) : Job<Result<'x, 'e>> = 
    Job.bindUnitTask u2xJ uT
  
  let bindResult (xJ: Result<'x, 'e>) (x2yJ: 'x -> Job<Result<'y, 'e>>) : Job<Result<'y, 'e>> =
    match xJ with
    | Error error -> Job.result <| Error error 
    | Ok x -> x2yJ x 

  let result (x: 'x) : Job<Result<'x, 'e>> = Job.result <| Ok x
  let map (x2y: 'x -> 'y) (x: Job<Result<'x, 'e>>): Job<Result<'y, 'e>> = 
    x >>- function
    | Ok x -> Ok (x2y x)
    | Error e -> Error e 
  let mapError (e2f: 'e -> 'f) (x: Job<Result<'x, 'e>>): Job<Result<'x, 'f>> =
    x >>- function
    | Ok x -> Ok x
    | Error e -> Error (e2f e)

open Hopac.Core
open JobResult
open System

[<Sealed>] 
type JobResultBuilder () =
  member inline __.Bind (xJ: Job<Result<'x, 'e>>, x2yJ: 'x -> Job<Result<'y, 'e>>) : Job<Result<'y, 'e>> = bind xJ x2yJ
  member inline __.Bind (xA: Async<Result<'x, 'e>>, x2yJ: 'x -> Job<Result<'y, 'e>>) : Job<Result<'y, 'e>> = bindAsync xA x2yJ
  member inline __.Bind (xT: Task<Result<'x, 'e>>, x2yJ: 'x -> Job<Result<'y, 'e>>) : Job<Result<'y, 'e>> = bindTask xT x2yJ 
  member inline __.Bind (uT: Task, u2xJ: unit -> Job<Result<'x, 'e>>) : Job<Result<'x, 'e>> = bindVoidTask uT u2xJ
  member inline __.Bind (xJ: Result<'x, 'e>, x2yJ: 'x -> Job<Result<'y, 'e>>) : Job<Result<'y, 'e>> = bindResult xJ x2yJ
  member inline __.Combine (uA: Async<Result<unit, 'e>>, xJ: Job<Result<'x, 'e>>) : Job<Result<'x, 'e>> = Job.fromAsync uA >>=. xJ
  member inline __.Combine (uT: Task<Result<unit, 'e>>, xJ: Job<Result<'x, 'e>>) : Job<Result<'x, 'e>> = Job.awaitTask uT >>=. xJ
  member inline __.Combine (uT: Task, xJ: Job<Result<'x, 'e>>) : Job<Result<'x, 'e>> = Job.awaitUnitTask uT >>=. xJ
  
  member inline __.Combine (uJ: Job<Result<unit, 'e>>, xJ: Job<Result<'x, 'e>>) : Job<Result<'x, 'e>> =
    uJ >>= function
    | Ok() -> xJ
    | Error e -> Job.result <| Error e

  member inline __.Delay (u2xJ: unit -> Job<Result<'x, 'e>>) : Job<Result<'x, 'e>> = Job.delay u2xJ

  member inline __.For (xs: seq<'x>, x2uJ: 'x -> Job<Result<unit, 'e>>) : Job<Result<unit, 'e>> =
    Job.using (xs.GetEnumerator()) <| fun enum ->
      let rec loop() =
        if enum.MoveNext() then
          x2uJ enum.Current >>= function
          | Ok _ -> loop()
          | e -> Job.result e
        else Job.result <| Ok()
      loop()
   
  member inline __.Return (x: 'x) : Job<Result<'x, 'e>> = result x

  member inline __.ReturnFrom (xA: Async<Result<'x, 'e>>) : Job<Result<'x, 'e>> = Job.fromAsync xA
  member inline __.ReturnFrom (xT: Task<Result<'x, 'e>>) : Job<Result<'x, 'e>> = Job.awaitTask xT
  member inline job.ReturnFrom (uT: Task) : Job<Result<unit, 'e>> = Job.bindUnitTask job.Zero uT
  member inline __.ReturnFrom (xJ: Job<Result<'x, 'e>>) : Job<Result<'x, 'e>> = xJ
  member inline __.ReturnFrom (xJ: Result<'x, 'e>) : Job<Result<'x, 'e>> = Job.result xJ

  member inline __.TryFinally (xA: Async<Result<'x, 'e>>, u2u: unit -> unit) : Job<Result<'x, 'e>> =
    Job.tryFinallyFun (Job.fromAsync xA) u2u
  member inline __.TryFinally (xT: Task<Result<'x, 'e>>, u2u: unit -> unit) : Job<Result<'x, 'e>> =
    Job.tryFinallyFun (Job.awaitTask xT) u2u
  member inline job.TryFinally (uT: Task, u2u: unit -> unit) : Job<Result<unit, 'e>> =
    Job.tryFinallyFun (Job.bindUnitTask job.Zero uT) u2u
  member inline __.TryFinally (xJ: Job<Result<'x, 'e>>, u2u: unit -> unit) : Job<Result<'x, 'e>> =
    Job.tryFinallyFun xJ u2u 

  member inline __.TryWith (xA: Async<Result<'x, 'e>>, e2xJ: exn -> Job<Result<'x, 'e>>) : Job<Result<'x, 'e>> =
    Job.tryWith (Job.fromAsync xA) e2xJ
  member inline __.TryWith (xT: Task<Result<'x, 'e>>, e2xJ: exn -> Job<Result<'x, 'e>>) : Job<Result<'x, 'e>> =
    Job.tryWith (Job.awaitTask xT) e2xJ
  member inline job.TryWith (uT: Task, e2xJ: exn -> Job<Result<unit, 'e>>) : Job<Result<unit, 'e>> =
    Job.tryWith (Job.bindUnitTask job.Zero uT) e2xJ
  member inline __.TryWith (xJ: Job<Result<'x, 'e>>, e2xJ: exn -> Job<Result<'x, 'e>>) : Job<Result<'x, 'e>> =
    Job.tryWith xJ e2xJ

  member inline __.Using (x: 'x when 'x :> IDisposable, x2yJ: _ -> Job<Result<'y, 'e>>) : Job<Result<'y, 'e>> =
    Job.using x x2yJ
     
  member job.While (u2b: unit -> bool, uJ: Job<Result<unit, 'e>>) : Job<Result<unit, 'e>> =
    if u2b() then job.Bind(uJ, (fun () -> job.While(u2b, uJ)))
    else job.Zero()

  member __.Zero () : Job<Result<unit, 'e>> = StaticData.unit >>- Ok

[<AutoOpen>]
module TopLevel =
  let jobResult = JobResultBuilder() 