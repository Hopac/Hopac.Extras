module Hopac.Extras.Tests.JobResultTests

open System
open Hopac
open Hopac.Infixes
open Hopac.Extras
open FsCheck
open System.Threading.Tasks
open NUnit.Framework
open Swensen.Unquote

let private check = Check.VerboseThrowOnFailure

[<Test>]
let result() = check (fun x -> run (JobResult.result x >>- fun r -> r = Ok x))

[<Test>]
let bind() =
  check (fun (x: Result<_,_>) (x2yJ: _ -> Result<_,_>) ->
    run (JobResult.bind (Job.result x) (x2yJ >> Job.result)) = Result.bind x2yJ x)

[<Test>]
let bindAsync() =
  check (fun (x: Result<int,_>) (Function.F(_, x2yJ: _ -> Result<_,_>)) ->
    run (JobResult.bindAsync (async.Return x) (x2yJ >> Job.result)) = Result.bind x2yJ x)

[<Test>]
let bindTask() =
  check (fun (x: Result<int,_>) (Function.F(_, x2yJ: _ -> Result<_,_>)) ->
    run (JobResult.bindTask (Task.FromResult x) (x2yJ >> Job.result)) = Result.bind x2yJ x)

[<Test>]
let bindVoidTask() =
  check (fun (Function.F(_, x2yJ: _ -> Result<_,_>)) ->
    run (JobResult.bindVoidTask (Task.Factory.StartNew(fun() -> ())) (x2yJ >> Job.result)) = x2yJ ())

[<Test>]
let bindChoice() =
  check (fun (x: Result<_,_>) (x2yJ: _ -> Result<_,_>) ->
    run (JobResult.bindResult x (x2yJ >> Job.result)) = Result.bind x2yJ x)

[<Test>]
let map() =
  check (fun (x: Result<int, _>) (Function.F(_, x2y: int -> _)) ->
    run (JobResult.map x2y (Job.result x)) = Result.map x2y x)

[<Test>]
let mapError() =
  check (fun (x: Result<_, int>) (Function.F(_, e2f: int -> _)) ->
    run (JobResult.mapError e2f (Job.result x)) = Result.mapError e2f x)

[<Test>]
let ``using``() =
  let disposed = ref false
  jobResult {
    use! __ = Job.result (Ok { new IDisposable with member __.Dispose() = disposed := true })
    return ()
  } |> run |> ignore
  !disposed =! true

[<Test>]
let ``using if exception in subsequent code``() =
  let disposed = ref false
  jobResult {
    use! __ = Job.result (Ok { new IDisposable with member __.Dispose() = disposed := true })
    failwith "error"
  } |> Job.catch |> run |> ignore
  !disposed =! true

[<Test>]
let ``for``() =
  let r = ref []
  jobResult {
    for x in 1..20 do
      let! y = if x <= 10 then JobResult.result x else Job.result (Error ())
      r := !r @ [y]
  } |> run |> ignore
  !r =! [1..10]

[<Test>]
let ``while``() =
  let r = ref 0
  jobResult {
    while true do
      do! if !r < 10 then JobResult.result () else Job.result (Error ()) 
      incr r
  } |> run |> ignore
  !r =! 10

[<Test>]
let complex() =
  jobResult {
    let! _ = jobResult { return 1 }
    let! _ = 
      jobResult { 
        let! _ = JobResult.result 1
        return! Job.result (Error 2)
      }
    return 3
  } |> run =! Error 2

[<Test>]
let zero() = run (jobResult { () }) =! Ok ()

[<Test>]
let ``try with job``() =
  jobResult {
    return
      try 1
      with _ -> 2
  } |> run =! Ok 1  
  
  jobResult {
    return
      try failwith "error"
      with _ -> 2
  } |> run =! Ok 2

[<Test>]
let ``try finally``() =
  let x = ref false
  jobResult {
    return
      try 1
      finally x := true
  } |> run =! Ok 1
  !x =! true

[<Test>]
let ``try finally, exception in try``() =
  let x = ref false
  jobResult {
    return
      try failwith "error"
      finally x := true
  } |> Job.catch |> run |> ignore
  !x =! true