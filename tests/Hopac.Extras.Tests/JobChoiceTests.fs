module Hopac.Extras.Tests.JobChoiceTests

open System
open Hopac
open Hopac.Job.Infixes
open Hopac.Extras
open FsCheck
open System.Threading.Tasks
open NUnit.Framework
open Swensen.Unquote

let private check = Check.VerboseThrowOnFailure

[<Test>]
let result() = check (fun x -> run (JobChoice.result x |>> fun r -> r = Ok x))

[<Test>]
let bind() =
  check (fun (x: Choice<_,_>) (x2yJ: _ -> Choice<_,_>) ->
    run (JobChoice.bind (Job.result x) (x2yJ >> Job.result)) = Choice.bind x2yJ x)

[<Test>]
let bindAsync() =
  check (fun (x: Choice<int,_>) (Function.F(_, x2yJ: _ -> Choice<_,_>)) ->
    run (JobChoice.bindAsync (async.Return x) (x2yJ >> Job.result)) = Choice.bind x2yJ x)

[<Test>]
let bindTask() =
  check (fun (x: Choice<int,_>) (Function.F(_, x2yJ: _ -> Choice<_,_>)) ->
    run (JobChoice.bindTask (Task.FromResult x) (x2yJ >> Job.result)) = Choice.bind x2yJ x)

[<Test>]
let bindVoidTask() =
  check (fun (Function.F(_, x2yJ: _ -> Choice<_,_>)) ->
    run (JobChoice.bindVoidTask (Task.Factory.StartNew(fun() -> ())) (x2yJ >> Job.result)) = x2yJ ())

[<Test>]
let map() =
  check (fun (x: Choice<int, _>) (Function.F(_, x2y: int -> _)) ->
    run (JobChoice.map x2y (Job.result x)) = Choice.map x2y x)

[<Test>]
let mapError() =
  check (fun (x: Choice<_, int>) (Function.F(_, e2f: int -> _)) ->
    run (JobChoice.mapError e2f (Job.result x)) = Choice.mapError e2f x)

[<Test>]
let ``using``() =
  let disposed = ref false
  jobChoice {
    use! __ = Job.result (Ok { new IDisposable with member __.Dispose() = disposed := true })
    return ()
  } |> run |> ignore
  !disposed =! true

[<Test>]
let ``using if exception in subsequent code``() =
  let disposed = ref false
  jobChoice {
    use! __ = Job.result (Ok { new IDisposable with member __.Dispose() = disposed := true })
    failwith "error"
  } |> Job.catch |> run |> ignore
  !disposed =! true

[<Test>]
let ``for``() =
  let r = ref []
  jobChoice {
    for x in 1..20 do
      let! y = if x <= 10 then JobChoice.result x else Job.result (Fail ())
      r := !r @ [y]
  } |> run |> ignore
  !r =! [1..10]

[<Test>]
let ``while``() =
  let r = ref 0
  jobChoice {
    while true do
      do! if !r < 10 then JobChoice.result () else Job.result (Fail ()) 
      incr r
  } |> run |> ignore
  !r =! 10

[<Test>]
let complex() =
  jobChoice {
    let! _ = jobChoice { return 1 }
    let! _ = 
      jobChoice { 
        let! _ = JobChoice.result 1
        return! Job.result (Fail 2)
      }
    return 3
  } |> run =! Fail 2

[<Test>]
let zero() = run (jobChoice { () }) =! Ok ()

[<Test>]
let ``try with job``() =
  jobChoice {
    return
      try 1
      with _ -> 2
  } |> run =! Ok 1  
  
  jobChoice {
    return
      try failwith "error"
      with _ -> 2
  } |> run =! Ok 2

[<Test>]
let ``try finally``() =
  let x = ref false
  jobChoice {
    return
      try 1
      finally x := true
  } |> run =! Ok 1
  !x =! true

[<Test>]
let ``try finally, exception in try``() =
  let x = ref false
  jobChoice {
    return
      try failwith "error"
      finally x := true
  } |> Job.catch |> run |> ignore
  !x =! true