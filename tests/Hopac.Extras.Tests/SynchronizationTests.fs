module Hopac.Extras.Tests.SynchronizationTests

open NUnit.Framework
open Hopac.Extras
open Hopac
open Hopac.Infixes
open Hopac.Alt.Infixes

[<Test; Timeout (5000)>]
let ``Wait blocks if semaphor is full``() =
    let s = Semaphore(2)
    run s.Wait
    run s.Wait
    (s.Wait |>>? fun _ -> failwith "Semaphore is full, but Wait is synchronized.") <|>?
    (timeOutMillis 100 >>%? ())
    |> run

[<Test; Timeout (5000)>]
let ``Wait unblocks if Release is called on a full semaphor``() =
    let s = Semaphore(2)
    run s.Wait
    run s.Wait
    let v = ivar()
    start (s.Wait >>=? fun _ -> v <-= ())
    // check that IVar has not been filled yet
    (v >>=? fun _ -> failwith "Semaphore is full, but Wait is synchronized.") <|>?
    (timeOutMillis 100 >>%? ()) |> run
    run s.Release
    // now it should be filled
    run (IVar.read v)

[<Test; Timeout (5000)>]
let holding() =
    let s = Semaphore(2)
    let v = ivar()
    start (Semaphore.holding s (IVar.read v))
    start (Semaphore.holding s (IVar.read v))
    // check that the semaphor is full
    (s.Wait >>=? fun _ -> failwith "Semaphore is full, but Wait is synchronized.") <|>?
    (timeOutMillis 100 >>%? ()) |> run
    // unblock the both jobs
    run (v <-= ())
    // now the semaphor is available
    run s.Wait
