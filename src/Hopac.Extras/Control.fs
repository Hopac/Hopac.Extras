namespace Hopac.Extras

open System
open Hopac
open Hopac.Infixes
open System.Threading.Tasks
open Hopac.Core

module JobChoice = 
  let bind (xJ: Job<Choice<'x, 'e>>) (x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    xJ >>= function
    | Fail error -> Job.result <| Fail error 
    | Ok x -> x2yJ x 
  let bindAsync (xA: Async<Choice<'x, 'e>>) (x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    bind (Job.fromAsync xA) x2yJ
  let bindTask (xT: Task<Choice<'x, 'e>>) (x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    bind (Job.awaitTask xT) x2yJ
  let bindVoidTask (uT: Task) (u2xJ: unit -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = 
    Job.bindUnitTask u2xJ uT
  let bindChoice (xJ: Choice<'x, 'e>) (x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    match xJ with
    | Fail error -> Job.result <| Fail error 
    | Ok x -> x2yJ x 

  let result (x: 'x) : Job<Choice<'x, 'e>> = Job.result <| Ok x
  let map (x2y: 'x -> 'y) (x: Job<Choice<'x, 'e>>): Job<Choice<'y, 'e>> = 
    x >>- function
    | Ok x -> Ok (x2y x)
    | Fail e -> Fail e 
  let mapError (e2f: 'e -> 'f) (x: Job<Choice<'x, 'e>>): Job<Choice<'x, 'f>> =
    x >>- function
    | Ok x -> Ok x
    | Fail e -> Fail (e2f e)

open JobChoice

[<Sealed>] 
type JobChoiceBuilder () =
  member inline __.Bind (xJ: Job<Choice<'x, 'e>>, x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> = bind xJ x2yJ
  member inline __.Bind (xA: Async<Choice<'x, 'e>>, x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> = bindAsync xA x2yJ
  member inline __.Bind (xT: Task<Choice<'x, 'e>>, x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> = bindTask xT x2yJ 
  member inline __.Bind (uT: Task, u2xJ: unit -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = bindVoidTask uT u2xJ
  member inline __.Bind (xJ: Choice<'x, 'e>, x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> = bindChoice xJ x2yJ
  member inline __.Combine (uA: Async<Choice<unit, 'e>>, xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = Job.fromAsync uA >>=. xJ
  member inline __.Combine (uT: Task<Choice<unit, 'e>>, xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = Job.awaitTask uT >>=. xJ
  member inline __.Combine (uT: Task, xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = Job.awaitUnitTask uT >>=. xJ
  
  member inline __.Combine (uJ: Job<Choice<unit, 'e>>, xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    uJ >>= function
    | Ok() -> xJ
    | Fail e -> Job.result <| Fail e

  member inline __.Delay (u2xJ: unit -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = Job.delay u2xJ

  member inline __.For (xs: seq<'x>, x2uJ: 'x -> Job<Choice<unit, 'e>>) : Job<Choice<unit, 'e>> =
    Job.using (xs.GetEnumerator()) <| fun enum ->
      let rec loop() =
        if enum.MoveNext() then
          x2uJ enum.Current >>= function
          | Ok _ -> loop()
          | fail -> Job.result fail
        else Job.result <| Ok()
      loop()
   
  member inline __.Return (x: 'x) : Job<Choice<'x, 'e>> = result x

  member inline __.ReturnFrom (xA: Async<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = Job.fromAsync xA
  member inline __.ReturnFrom (xT: Task<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = Job.awaitTask xT
  member inline job.ReturnFrom (uT: Task) : Job<Choice<unit, 'e>> = Job.bindUnitTask job.Zero uT
  member inline __.ReturnFrom (xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = xJ
  member inline __.ReturnFrom (xJ: Choice<'x, 'e>) : Job<Choice<'x, 'e>> = Job.result xJ

  member inline __.TryFinally (xA: Async<Choice<'x, 'e>>, u2u: unit -> unit) : Job<Choice<'x, 'e>> =
    Job.tryFinallyFun (Job.fromAsync xA) u2u
  member inline __.TryFinally (xT: Task<Choice<'x, 'e>>, u2u: unit -> unit) : Job<Choice<'x, 'e>> =
    Job.tryFinallyFun (Job.awaitTask xT) u2u
  member inline job.TryFinally (uT: Task, u2u: unit -> unit) : Job<Choice<unit, 'e>> =
    Job.tryFinallyFun (Job.bindUnitTask job.Zero uT) u2u
  member inline __.TryFinally (xJ: Job<Choice<'x, 'e>>, u2u: unit -> unit) : Job<Choice<'x, 'e>> =
    Job.tryFinallyFun xJ u2u 

  member inline __.TryWith (xA: Async<Choice<'x, 'e>>, e2xJ: exn -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Job.tryWith (Job.fromAsync xA) e2xJ
  member inline __.TryWith (xT: Task<Choice<'x, 'e>>, e2xJ: exn -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Job.tryWith (Job.awaitTask xT) e2xJ
  member inline job.TryWith (uT: Task, e2xJ: exn -> Job<Choice<unit, 'e>>) : Job<Choice<unit, 'e>> =
    Job.tryWith (Job.bindUnitTask job.Zero uT) e2xJ
  member inline __.TryWith (xJ: Job<Choice<'x, 'e>>, e2xJ: exn -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Job.tryWith xJ e2xJ

  member inline __.Using (x: 'x when 'x :> IDisposable, x2yJ: _ -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    Job.using x x2yJ
     
  member job.While (u2b: unit -> bool, uJ: Job<Choice<unit, 'e>>) : Job<Choice<unit, 'e>> =
    if u2b() then job.Bind(uJ, (fun () -> job.While(u2b, uJ)))
    else job.Zero()

  member __.Zero () : Job<Choice<unit, 'e>> = StaticData.unit >>- Ok

[<AutoOpen>]
module TopLevel =
  let jobChoice = JobChoiceBuilder() 
