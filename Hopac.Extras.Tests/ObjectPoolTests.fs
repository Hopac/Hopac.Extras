module Hopac.Extras.Tests.ObjectPoolTests

open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Alt.Infixes
open NUnit.Framework
open Hopac.Extras
open Swensen.Unquote
open System
open System.Threading
open FsCheck 

let private takeOrThrow ch = 
    ch <|>? (Timer.Global.timeOutMillis 10000 |>>? fun _ -> failwith "timeout") |> run 

let private timeoutOrThrow timeout alt = 
    (Timer.Global.timeOutMillis timeout |>>? fun _ -> ()) <|>?
    (alt |>>? fun x -> failwithf "Expected timeout but was %A" x) |> run

type private Entry = 
    { Value: int
      mutable Disposed: bool }
    interface IDisposable with
        member x.Dispose() = x.Disposed <- true

type private Creator =
    { NewInstance: unit -> Entry
      CreatedInstances: unit -> Entry list }

// todo this creator causes FsCheck tests to block randomly
// Investigate it (for learning purposes).

//let creator() = 
//    let createNewCh, getInstanceCountCh = ch(), ch()
//    run (Job.iterateServer [||] <| fun instances ->
//        (Alt.delay <| fun _ ->
//            let newInstance = { Value = instances.Length; Disposed = false }
//            Ch.give createNewCh newInstance >>%? Array.append instances [|newInstance|]) <|>?
//        (Ch.give getInstanceCountCh (Array.toList instances) >>%? instances))
//
//    { NewInstance = fun() -> run createNewCh
//      CreatedInstances = fun _ -> run getInstanceCountCh }

let private creator() = 
    let instances = ref [||]
    
    { NewInstance = fun() -> 
        let newInstance = { Value = (!instances).Length; Disposed = false }
        instances := Array.append !instances [|newInstance|]
        newInstance
      CreatedInstances = fun _ -> List.ofArray !instances }

[<Literal>]
let private timeout = 10000

[<Test; Timeout(timeout)>]
let ``reuse single instance``() = 
    let creator = creator()
    let pool = new ObjectPool<_>(creator.NewInstance, 1u)
    let c = ch()
    start <| pool.WithInstanceJob (fun x -> c <-- x)
    let instance = takeOrThrow c
    start <| pool.WithInstanceJob (fun x -> c <-- x)
    takeOrThrow c =? instance 
    List.length <| creator.CreatedInstances() =? 1

[<Test; Timeout(timeout)>]
let ``blocks if there is no available instance``() = 
    let creator = creator()
    let pool = new ObjectPool<_>(creator.NewInstance, 1u)
    // take an instance and block it forever
    let c1 = ch()
    start <| pool.WithInstanceJob (fun _ -> c1 <-- ())
    // try to get the instance and block
    let c2 = ch()
    start <| pool.WithInstanceJob (fun _ -> c2 <-- ())
    timeoutOrThrow 1000 c2

[<Test; Timeout(timeout)>]
let ``unblocks when an instance becomes available``() = 
    let creator = creator()
    let pool = new ObjectPool<_>(creator.NewInstance, 1u)
    // take an instance and block until the value is not taken from c1
    let c1 = ch()
    start <| pool.WithInstanceJob (fun _ -> c1 <-- ())
    // try to get the instance and block
    let c2 = ch()
    start <| pool.WithInstanceJob (fun _ -> c2 <-- ())
    timeoutOrThrow 1000 c2
    // release the instance
    run c1
    // now the second job should give the value to c2 channel
    takeOrThrow c2

[<Test; Timeout(timeout)>]
let ``dispose all instances when pool is disposing``() =
    let creator = creator()
    let pool = new ObjectPool<_>(creator.NewInstance, 5u)
    let cs =
        [1..5]
        |> List.map (fun _ ->
            let s, c = ch(), ch()
            start <| pool.WithInstanceJob (fun _ -> Ch.give s () >>. Ch.give c ())
            s, c)
    // ensure all jobs started and wait on "c" channels
    cs |> List.map fst |> Job.conIgnore |> run
    creator.CreatedInstances() |> List.map (fun e -> e.Disposed) =? List.init 5 (fun _ -> false)
    // unblock all jobs
    cs |> List.map snd |> Job.conIgnore |> run
    (pool :> IDisposable).Dispose()

    creator.CreatedInstances() |> List.map (fun e -> e.Disposed) =? List.init 5 (fun _ -> true)

[<Test; Timeout(timeout)>]
let ``instance is disposed and removed from pool after it's been unused for certain time``() =
    let creator = creator()
    let inactiveTime = TimeSpan.FromSeconds 1.
    let pool = new ObjectPool<_>(creator.NewInstance, 1u, inactiveTime)
    // force the pool to create an instance
    run <| pool.WithInstanceJob (fun _ -> Job.unit())
    // check the instance is not disposed yet
    let instance = creator.CreatedInstances() |> List.head
    instance.Disposed =? false
    // wait while the instance is considered obsolete 
    Thread.Sleep (int inactiveTime.TotalMilliseconds * 2)
    instance.Disposed =? true
    
[<Test; Timeout(timeout)>]
let ``instance is not disposed and removed from pool if it reused before it becomes obsolete``() =
    let creator = creator()
    let inactiveTime = TimeSpan.FromMilliseconds 500.
    let pool = new ObjectPool<_>(creator.NewInstance, 1u, inactiveTime)
    // force the pool to create an instance
    run <| pool.WithInstanceJob (fun _ -> Job.unit())
    let instance = creator.CreatedInstances() |> List.head 
    // check that the instance is not disposed yet
    instance.Disposed =? false
    // reuse the instance each 100 ms for total time that is longer than "inactiveTime"
    for _ in 1..10 do
        Thread.Sleep (TimeSpan.FromMilliseconds 100.)
        run <| pool.WithInstanceJob (fun _ -> Job.unit())
    // check that the instance is still alive
    instance.Disposed =? false

type TestCase = TestCase of capacity: uint32 * jobs: int32

type Generators = 
    static member Capacity() = 
        { new Arbitrary<TestCase>() with
              member __.Generator = 
                  gen { 
                      let! value = Gen.choose (1, 20)
                      let! jobs = Gen.choose (1, 100)
                      return TestCase (uint32 value, jobs)
                  }}

[<TestFixtureSetUp>]
let setUp() = Arb.register<Generators>() |> ignore

[<Test; Timeout(timeout)>]
let ``all jobs are executed``() =
    let prop (TestCase (capacity, jobs)) =
        let creator = creator()
        let pool = new ObjectPool<_>(creator.NewInstance, capacity)
        let results = ref 0
        List.init jobs (fun i -> pool.WithInstanceJob (fun _ -> job { Interlocked.Add(results, i + 1) |> ignore })) 
        |> Job.conIgnore
        |> run
        (pool :> IDisposable).Dispose()
        let expected = List.sum [1..jobs]
        //printfn "Expected = %A" expected 
        //printfn "Actual = %A" !results
        !results = expected

    Check.VerboseThrowOnFailure prop 

[<Test; Timeout(timeout)>]
let ``does not create more instances than capacity``() =
    let prop (TestCase (capacity, jobs)) =
        let creator = creator()
        let pool = new ObjectPool<_>(creator.NewInstance, capacity)
        List.init jobs (fun _ -> pool.WithInstanceJob (fun _ -> Job.unit())) 
        |> Job.conIgnore
        |> run
        (pool :> IDisposable).Dispose()
        let actual = creator.CreatedInstances() |> List.length
        //printfn "Expected: < %A" capacity
        //printfn "Actual = %A" actual
        actual <= int capacity

    Check.VerboseThrowOnFailure prop 
