namespace Hopac.Extras

open System
open Hopac
open Hopac.Infixes
open Hopac.Alt.Infixes
open Hopac.Job.Infixes
open Hopac.Extensions
open System.Threading.Tasks
open Hopac.Core

[<Sealed>] 
type JobChoiceBuilder () =
  member inline job.Bind (xA: Async<Choice<'x, 'e>>, x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    job.Bind(Async.toJob xA, x2yJ)
  member inline job.Bind (xT: Task<Choice<'x, 'e>>, x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    job.Bind(Task.awaitJob xT, x2yJ)
  member inline __.Bind (uT: Task, u2xJ: unit -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Task.bindJob (uT, u2xJ)
  member inline __.Bind (xJ: Job<Choice<'x, 'e>>, x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    xJ >>= function
    | Fail error -> Job.result <| Fail error 
    | Ok x -> x2yJ x 

  member inline __.Combine (uA: Async<Choice<unit, 'e>>, xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Async.toJob uA >>. xJ
  member inline __.Combine (uT: Task<Choice<unit, 'e>>, xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Task.awaitJob uT >>. xJ
  member inline __.Combine (uT: Task, xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Task.awaitJob uT >>. xJ
  member inline __.Combine (uJ: Job<Choice<unit, 'e>>, xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = 
    uJ >>= function
    | Fail error -> Job.result <| Fail error
    | Ok() -> xJ 

  member inline __.Delay (u2xJ: unit -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = Job.delay u2xJ

  // Error	1	This construct causes code to be less generic than indicated by the type annotations. The type variable 'y has been constrained to be type 'unit'.	D:\git\Hopac.Extras\src\Hopac.Extras\Control.fs	39	5	Hopac.Extras

  //member inline job.For (xs: seq<'x>, x2uJ: 'x -> Job<Choice<unit, 'e>>) : Job<Choice<unit, 'e>> =
  //  job.Using(xs.GetEnumerator(), fun enum -> job.While(enum.MoveNext, job.Delay(fun () -> x2uJ enum.Current))) 
   
  member inline __.Return (x: 'x) : Job<Choice<'x, 'e>> = Job.result <| Ok x

  member inline __.ReturnFrom (xA: Async<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = Async.toJob xA
  member inline __.ReturnFrom (xT: Task<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = Task.awaitJob xT
  member inline job.ReturnFrom (uT: Task) : Job<Choice<unit, 'e>> = Task.bindJob (uT, job.Zero)
  member inline __.ReturnFrom (xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = xJ

  member inline __.TryFinally (xA: Async<Choice<'x, 'e>>, u2u: unit -> unit) : Job<Choice<'x, 'e>> =
    Job.tryFinallyFun (Async.toJob xA) u2u
  member inline __.TryFinally (xT: Task<Choice<'x, 'e>>, u2u: unit -> unit) : Job<Choice<'x, 'e>> =
    Job.tryFinallyFun (Task.awaitJob xT) u2u
  member inline job.TryFinally (uT: Task, u2u: unit -> unit) : Job<Choice<unit, 'e>> =
    Job.tryFinallyFun (Task.bindJob (uT, job.Zero)) u2u
  member inline __.TryFinally (xJ: Job<Choice<'x, 'e>>, u2u: unit -> unit) : Job<Choice<'x, 'e>> =
    Job.tryFinallyFun xJ u2u

  member inline __.TryWith (xA: Async<Choice<'x, 'e>>, e2xJ: exn -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Job.tryWith (Async.toJob xA) e2xJ
  member inline __.TryWith (xT: Task<Choice<'x, 'e>>, e2xJ: exn -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Job.tryWith (Task.awaitJob xT) e2xJ
  member inline job.TryWith (uT: Task, e2xJ: exn -> Job<Choice<unit, 'e>>) : Job<Choice<unit, 'e>> =
    Job.tryWith (Task.awaitJob (uT, job.Zero) e2xJ
  member inline __.TryWith (xJ: Job<Choice<'x, 'e>>, e2xJ: exn -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Job.tryWith xJ e2xJ

  member inline __.Using (x: 'x when 'x :> IDisposable, x2yJ: _ -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    Job.using x x2yJ
     
  member inline job.While (u2b: unit -> bool, uJ: Job<Choice<unit, 'e>>) : Job<Choice<unit, 'e>> =
    if u2b() then
      job.Bind(uJ, (fun () -> job.While(u2b, uJ)))
    else job.Zero()

  member inline __.Zero () : Job<Choice<unit, 'e>> = StaticData.unit |>> Ok

module TopLevel =
  let jobChoice = JobChoiceBuilder()