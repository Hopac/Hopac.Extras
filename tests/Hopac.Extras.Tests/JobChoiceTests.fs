module Hopac.Extras.Tests.JobChoiceTests

open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Extras
open FsCheck
open FsCheck.Prop
open System.Threading.Tasks

module Choice =
  let bind x2yC x = match x with Ok x -> x2yC x | Fail e -> Fail e

let ``result``() = Check.VerboseThrowOnFailure (fun x -> run (JobChoice.result x |>> fun r -> r = Ok x))

let ``bind``() =
  Check.VerboseThrowOnFailure (fun (x: Choice<_,_>) (x2yJ: _ -> Choice<_,_>) ->
    run (JobChoice.bind (Job.result x) (x2yJ >> Job.result)) = Choice.bind x2yJ x)

let ``bindAsync``() =
  Check.VerboseThrowOnFailure (fun (x: Choice<int,_>) (Function.F(_, x2yJ: _ -> Choice<_,_>)) ->
    run (JobChoice.bindAsync (async.Return x) (x2yJ >> Job.result)) = Choice.bind x2yJ x)

let ``bindTask``() =
  Check.VerboseThrowOnFailure (fun (x: Choice<int,_>) (Function.F(_, x2yJ: _ -> Choice<_,_>)) ->
    run (JobChoice.bindTask (Task.FromResult x) (x2yJ >> Job.result)) = Choice.bind x2yJ x)

let ``bindVoidTask``() =
  Check.VerboseThrowOnFailure (fun (Function.F(_, x2yJ: _ -> Choice<_,_>)) ->
    run (JobChoice.bindVoidTask (Task.Factory.StartNew(fun() -> ())) (x2yJ >> Job.result)) = x2yJ ())

  

