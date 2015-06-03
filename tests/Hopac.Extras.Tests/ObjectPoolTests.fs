module Hopac.Extras.Tests.ObjectPoolTests

open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Alt.Infixes
open NUnit.Framework
open Swensen.Unquote
open System
open System.Threading
open FsCheck 
open Hopac.Extras
open FsCheck.Prop

let private takeOrThrow ch = 
        ch 
    <|> timeOutMillis 10000 ^-> fun _ -> failwith "timeout"
     |> run 

let private timeoutOrThrow timeout alt = 
    timeOutMillis timeout ^-> fun _ -> () 
    <|>
    alt ^-> fun x -> failwithf "Expected timeout but was %A" x
    |> run

type private Entry = 
    { Value: int
      mutable Disposed: bool
      ThrowOnDispose: bool }
    interface IDisposable with
        member x.Dispose() =
            x.Disposed <- true
            if x.ThrowOnDispose then failwithf "%A: exception in Dispose" x

type private Creator =
    { NewInstance: unit -> Entry
      CreatedInstances: unit -> Entry list }

// todo this creator causes FsCheck tests to block randomly
// Investigate it (for learning purposes).

//let creator() = 
//    let createNewCh, getInstanceCountCh = Ch(), Ch()
//    run (Job.iterateServer [||] <| fun instances ->
//        (Alt.delay <| fun _ ->
//            let newInstance = { Value = instances.Length; Disposed = false }
//            Ch.give createNewCh newInstance >>%? Array.append instances [|newInstance|]) <|>?
//        (Ch.give getInstanceCountCh (Array.toList instances) >>%? instances))
//
//    { NewInstance = fun() -> run createNewCh
//      CreatedInstances = fun _ -> run getInstanceCountCh }

let private creatorWith throwOnDispose () = 
    let instances = ref [||]
    
    { NewInstance = fun() -> 
        let newInstance = { Value = (!instances).Length; Disposed = false; ThrowOnDispose = throwOnDispose }
        instances := Array.append !instances [|newInstance|]
        newInstance
      CreatedInstances = fun _ -> List.ofArray !instances }

let private creator = creatorWith false

[<Literal>]
let private timeout = 10000

[<Test; Timeout(timeout)>]
let ``reuse single instance``() = 
    let creator = creator()
    let pool = new ObjectPool<_>(creator.NewInstance, 1u)
    let c = Ch()
    startIgnore <| pool.WithInstanceJob (fun x -> c *<- x)
    let instance = takeOrThrow c
    startIgnore <| pool.WithInstanceJob (fun x -> c *<- x)
    takeOrThrow c =! instance 
    creator.CreatedInstances().Length =! 1

[<Test; Timeout(timeout)>]
let ``blocks if there is no available instance``() = 
    let creator = creator()
    let pool = new ObjectPool<_>(creator.NewInstance, 1u)
    // take an instance and block it forever
    let c1 = Ch()
    startIgnore <| pool.WithInstanceJob (fun _ -> c1 *<- ())
    // try to get the instance and block
    let c2 = Ch()
    startIgnore <| pool.WithInstanceJob (fun _ -> c2 *<- ())
    timeoutOrThrow 1000 c2

[<Test; Timeout(timeout)>]
let ``unblocks when an instance becomes available``() = 
    let creator = creator()
    let pool = new ObjectPool<_>(creator.NewInstance, 1u)
    // take an instance and block until the value is not taken from c1
    let c1 = Ch()
    startIgnore <| pool.WithInstanceJob (fun _ -> c1 *<- ())
    // try to get the instance and block
    let c2 = Ch()
    startIgnore <| pool.WithInstanceJob (fun _ -> c2 *<- ())
    timeoutOrThrow 1000 c2
    // release the instance
    run c1
    // now the second job should give the value to c2 channel
    takeOrThrow c2

[<Test; Timeout(timeout)>]
let ``does not deadlock if client quickly chooses another alternative``() = 
    let creator = creator()
    let pool = new ObjectPool<_>(creator.NewInstance, 1u)
    // take an instance and block until the value is not taken from c
    let c = Ch()
    startIgnore <| pool.WithInstanceJob (fun _ -> c *<- ())
    // try to get the instance or "always"
    runWith <| Alt.always (Ok())
        <| pool.WithInstanceJob (fun _ -> Job.unit())
        |> ignore
    run c
    // check whether the pool has not deadlocked
    startIgnore <| pool.WithInstanceJob (fun _ -> c *<- ())
    takeOrThrow c

[<Test; Timeout(timeout)>]
let ``dispose all instances when pool is disposing``() =
    let creator = creator()
    Job.usingAsync (new ObjectPool<_>(creator.NewInstance, 5u)) <| fun pool ->
        let cs =
            [1..5]
            |> List.map (fun _ ->
                let s, c = Ch(), Ch()
                startIgnore <| pool.WithInstanceJob (fun _ -> Ch.give s () >>. Ch.give c ())
                s, c)
        // ensure all jobs started and wait on "c" channels
        cs |> List.map fst |> Job.conIgnore |> run
        creator.CreatedInstances() |> List.map (fun e -> e.Disposed) =! List.init 5 (fun _ -> false)
        // unblock all jobs
        cs |> List.map snd |> Job.conIgnore
    |> run
    creator.CreatedInstances() |> List.map (fun e -> e.Disposed) =! List.init 5 (fun _ -> true)

[<Test; Timeout(timeout)>]
let ``instance is disposed and removed from pool after it's been unused for certain time``() =
    let creator = creator()
    let inactiveTime = TimeSpan.FromSeconds 1.
    let pool = new ObjectPool<_>(creator.NewInstance, 1u, inactiveTime)
    // force the pool to create an instance
    run <| pool.WithInstanceJob (fun _ -> Job.unit()) |> ignore
    // check the instance is not disposed yet
    let instance = creator.CreatedInstances() |> List.head
    instance.Disposed =! false
    // wait while the instance is considered obsolete 
    Thread.Sleep (int inactiveTime.TotalMilliseconds * 2)
    instance.Disposed =! true

[<Test; Timeout(timeout)>]
let ``it's ok if instance throws exception in Dispose``() =
    let creator = creatorWith true ()
    let inactiveTime = TimeSpan.FromSeconds 1.
    let pool = new ObjectPool<_>(creator.NewInstance, 1u, inactiveTime)
    // force the pool to create an instance
    run <| pool.WithInstanceJob (fun _ -> Job.unit()) |> ignore
    let instance = creator.CreatedInstances() |> List.head
    Thread.Sleep (int inactiveTime.TotalMilliseconds * 2)
    instance.Disposed =! true
    // check that the pool is still working ok
    run <| pool.WithInstanceJob (fun _ -> Job.result 1) =! Ok 1
    
[<Test; Timeout(timeout)>]
let ``instance is not disposed and is not removed from pool if it's reused before it becomes obsolete``() =
    let creator = creator()
    let inactiveTime = TimeSpan.FromMilliseconds 500.
    let pool = new ObjectPool<_>(creator.NewInstance, 1u, inactiveTime)
    // force the pool to create an instance
    run <| pool.WithInstanceJob (fun _ -> Job.unit()) |> ignore
    let instance = creator.CreatedInstances() |> List.head 
    // check that the instance is not disposed yet
    instance.Disposed =! false
    // reuse the instance each 100 ms for total time that is longer than "inactiveTime"
    for _ in 1..10 do
        Thread.Sleep (TimeSpan.FromMilliseconds 100.)
        run <| pool.WithInstanceJob (fun _ -> Job.unit()) |> ignore
    // check that the instance is still alive
    instance.Disposed =! false

[<Test; Timeout(timeout)>]
let ``returns instance to the pool even though job creation function raises exception``() = 
    let creator = creator()
    let pool = new ObjectPool<_>(creator.NewInstance, 1u)
    try run <| pool.WithInstanceJob (fun _ -> failwith "error") |> ignore with _ -> () 
    let c = Ch()
    startIgnore <| pool.WithInstanceJob (fun x -> c *<- x)
    takeOrThrow c |> ignore
    List.length <| creator.CreatedInstances() =! 1

[<Test; Timeout(timeout)>]
let ``returns instance to the pool even though the job raises exception``() = 
    let creator = creator()
    let pool = new ObjectPool<_>(creator.NewInstance, 1u)
    try run <| pool.WithInstanceJob (fun _ -> Job.delay <| fun _ -> failwith "error") |> ignore with _ -> ()
    let c = Ch()
    startIgnore <| pool.WithInstanceJob (fun x -> c *<- x)
    takeOrThrow c |> ignore
    List.length <| creator.CreatedInstances() =! 1

[<Test; Timeout(timeout)>]
let ``returns Fail if creator raise exception``() = 
    let pool = new ObjectPool<_>((fun _ -> failwith "error"), 1u)
    match run <| pool.WithInstanceJob (fun _ -> Job.result 0) with
    | Ok _ -> failwithf "Should return Fail but was Ok"
    | Fail _ -> ()

[<Test; Timeout(timeout)>]
let ``does not count failed to create instance as "given"``() =
    let callNumber = ref 0
    let pool = new ObjectPool<_>((fun _ ->
        incr callNumber
        match !callNumber with
        | 1 -> failwith "error"
        | _ -> ()), 1u)
    // first call, the creator raises exception and `given` counter should not be inceremented
    match run <| pool.WithInstanceJob (fun _ -> Job.result 0) with
    | Ok _ -> failwithf "Should return Fail but was Ok"
    | Fail _ -> ()
    // second call should sutisfy since capacity is not reached yet
    run <| pool.WithInstanceJob (fun _ -> Job.result 0) =! Ok 0

type TestCase = TestCase of capacity: uint32 * jobs: int32

type Generators = 
    static member Capacity =
        fun value jobs -> TestCase (uint32 value, jobs)
        <!> Gen.choose (1, 20)
        <*> Gen.choose (1, 100)
        |> Arb.fromGen 

[<Test; Timeout(timeout)>]
let ``all jobs are executed``() =
    let prop (TestCase (capacity, jobs)) =
        let creator = creator()
        let results = ref 0
        Job.usingAsync (new ObjectPool<_>(creator.NewInstance, capacity)) <| fun pool ->
            List.init jobs (fun i -> pool.WithInstanceJob (fun _ -> job { Interlocked.Add(results, i + 1) |> ignore })) 
            |> Job.conIgnore
        |> run
        let expected = List.sum [1..jobs]
        //printfn "Expected = %A" expected 
        //printfn "Actual = %A" !results
        !results = expected

    Check.VerboseThrowOnFailure (forAll Generators.Capacity prop)

[<Test; Timeout(timeout)>]
let ``does not create more instances than capacity``() =
    let prop (TestCase (capacity, jobs)) =
        let creator = creator()
        Job.usingAsync (new ObjectPool<_>(creator.NewInstance, capacity)) <| fun pool ->
            List.init jobs (fun _ -> pool.WithInstanceJob (fun _ -> Job.unit())) 
            |> Job.conIgnore
        |> run
        let actual = creator.CreatedInstances() |> List.length
        //printfn "Expected: < %A" capacity
        //printfn "Actual = %A" actual
        actual <= int capacity

    Check.VerboseThrowOnFailure (forAll Generators.Capacity prop)
