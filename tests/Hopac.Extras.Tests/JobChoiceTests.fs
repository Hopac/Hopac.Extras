module Hopac.Extras.Tests.JobChoiceTests

open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Extras
open FsCheck
open System.Threading.Tasks
open NUnit.Framework

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