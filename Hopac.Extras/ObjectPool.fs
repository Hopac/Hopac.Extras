namespace Hopac.Extras

open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Alt.Infixes
open System

type private PoolEntry<'a when 'a :> IDisposable> = 
    { Value: 'a
      mutable LastUsed: DateTime }
    with static member Create(value: 'a) = { Value = value; LastUsed = DateTime.UtcNow }

/// Bounded pool of disposable objects. If number of given objects is equal to capacity then client will be blocked as it tries to get an instance. 
/// If an object in pool is not used more then inactiveTimeBeforeDispose period of time, it's disposed and removed from the pool. 
/// When the pool is disposing itself, it disposes all objects it caches first.
type ObjectPool<'a when 'a :> IDisposable>(createNew: unit -> 'a, ?capacity: uint32, ?inactiveTimeBeforeDispose: TimeSpan) =
    let capacity = defaultArg capacity 50u
    let inactiveTimeBeforeDispose = defaultArg inactiveTimeBeforeDispose (TimeSpan.FromMinutes 1.)
    let reqCh = ch<'a PoolEntry Ch>()
    let releaseCh = ch<'a PoolEntry>()
    let maybeExpiredCh = ch<'a PoolEntry>()
    let dispose = ivar()
    let hasDisposed = ivar()
    
    let rec loop (available: 'a PoolEntry list, given: uint32, disposed: bool) = Job.delay <| fun _ ->
        // an instance returns to pool
        let releaseAlt() =
            releaseCh >>=? fun instance ->
                instance.LastUsed <- DateTime.UtcNow
                Job.start (Timer.Global.timeOut inactiveTimeBeforeDispose >>.
                           (maybeExpiredCh <-+ instance)) >>.
                loop (instance :: available, given - 1u, disposed)
        // request for an instance
        let reqAlt() =
            reqCh >>=? fun replyCh ->
                let instance, available = 
                    match available with
                    | [] -> PoolEntry.Create (createNew()), []
                    | h :: t -> h, t
                (replyCh <-- instance >>.? loop (available, given + 1u, disposed))
        // an instance was inactive for too long
        let expiredAlt() =
            maybeExpiredCh >>=? fun instance ->
                if DateTime.UtcNow - instance.LastUsed > inactiveTimeBeforeDispose 
                   && List.exists (fun x -> obj.ReferenceEquals(x, instance)) available then
                    instance.Value.Dispose()
                    loop (available |> List.filter (fun x -> not <| obj.ReferenceEquals(x, instance)), given, disposed)
                else loop (available, given, disposed)

        // the entire pool is disposing
        let disposeAlt() = dispose >>.? loop (available, given, true)

        // immedeately available for picking if there is no given instances
        let waitUntilAllReleasedAlt() = Alt.guard << Job.delay <| fun _ ->
            if given = 0u then 
                // dispose all instances
                available |> List.iter (fun x -> x.Value.Dispose())
                // signal the disposing procedure completed
                hasDisposed <-= () 
                >>% Alt.always()
            else 
                Job.result <| Alt.never()

        if disposed then
            releaseAlt() <|>? 
            waitUntilAllReleasedAlt()
        elif given < capacity then
            // if number of given objects is not reach the capacity, synchronize on request channel as well
            releaseAlt() <|>? 
            expiredAlt() <|>? 
            disposeAlt() <|>? 
            reqAlt()
        else
            releaseAlt() <|>? 
            expiredAlt() <|>? 
            disposeAlt()
    
    do start (loop ([], 0u, false)) 
     
    let get() = Alt.guard << Job.delay <| fun _ ->
        let replyCh = ch()
        reqCh <-+ replyCh >>% replyCh

    /// Gets an available instance from pool or create a new one, then passes it to function f,
    /// then returns the instance back to the pool (even if the job returned by f raises an exception).
    member __.WithInstanceJob (f: 'a -> #Job<unit>) =
        get() >>=? fun entry -> 
            Job.tryFinallyJob (f entry.Value) (releaseCh <-- entry)

    interface IDisposable with
        member __.Dispose() = run (dispose <-= () >>. hasDisposed)

type ObjectPool with
    member x.WithInstance f = x.WithInstanceJob (fun a -> Job.result (f a))
    member x.WithInstanceSync f = run (x.WithInstance f)
