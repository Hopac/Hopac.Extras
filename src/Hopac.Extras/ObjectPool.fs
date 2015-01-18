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
    let reqCh = ch<Promise<unit> * 'a PoolEntry Ch>()
    let releaseCh = ch<'a PoolEntry>()
    let maybeExpiredCh = ch<'a PoolEntry>() 
    let dispose = ivar()
    let hasDisposed = ivar()
    
    let rec loop (available: 'a PoolEntry list, given: uint32) = Job.delay <| fun _ ->
        // an instance returns to pool
        let releaseAlt() =
            releaseCh >>=? fun instance ->
                instance.LastUsed <- DateTime.UtcNow
                Job.start (Timer.Global.timeOut inactiveTimeBeforeDispose >>.
                           (maybeExpiredCh <-+ instance)) >>.
                loop (instance :: available, given - 1u)
        // request for an instance
        let reqAlt() =
            reqCh >>=? fun (nack, replyCh) ->
                let instance, available = 
                    match available with
                    | [] -> PoolEntry.Create (createNew()), []
                    | h :: t -> h, t
                (replyCh <-- instance >>.? loop (available, given + 1u)) <|>?
                (nack >>.? loop (instance :: available, given))
        // an instance was inactive for too long
        let expiredAlt() =
            maybeExpiredCh >>=? fun instance ->
                if DateTime.UtcNow - instance.LastUsed > inactiveTimeBeforeDispose 
                   && List.exists (fun x -> obj.ReferenceEquals(x, instance)) available then
                    instance.Value.Dispose()
                    loop (available |> List.filter (fun x -> not <| obj.ReferenceEquals(x, instance)), given)
                else loop (available, given)

        // the entire pool is disposing
        let disposeAlt() = 
            dispose >>=? fun _ ->
                // dispose all instances that are in pool
                available |> List.iter (fun x -> x.Value.Dispose())
                // wait until all given instances are returns to pool and disposing them on the way
                Job.forN (int given) (releaseCh |>> fun instance -> instance.Value.Dispose()) >>. 
                (hasDisposed <-= ())

        if given < capacity then
            // if number of given objects has not reached the capacity, synchronize on request channel as well
            releaseAlt() <|>? expiredAlt() <|>? disposeAlt() <|>? reqAlt()
        else
            releaseAlt() <|>? expiredAlt() <|>? disposeAlt()
    
    do start (loop ([], 0u)) 
     
    let get() = Alt.withNack <| fun nack ->
        let replyCh = ch()
        reqCh <-+ (nack, replyCh) >>% replyCh

    /// Gets an available instance from pool or create a new one, then passes it to function f,
    /// then returns the instance back to the pool (even if f or the job returned by f raise exceptions).
    member __.WithInstanceJob<'r> (f: 'a -> Job<'r>) : Alt<'r> =
        get() >>=? fun entry -> 
            Job.tryFinallyJob 
                (Job.delay (fun _ -> f entry.Value)) 
                (releaseCh <-- entry)
             
    /// Gets an available instance from pool or create a new one, then passes it to function f,
    /// then returns the instance back to the pool (even if f or the alt returned by f raise exceptions).
    member __.WithInstanceAlt<'r> (f: 'a -> Alt<'r>) : Alt<'r> =
        get() >>=? fun entry -> 
            Job.tryFinallyJob 
                (Job.delay (fun _ -> f entry.Value))  
                (releaseCh <-- entry)

    interface IAsyncDisposable with
        member __.DisposeAsync() = IVar.tryFill dispose () >>. hasDisposed

    interface IDisposable with
        /// Runs disposing asynchronously. Does not wait until the disposing finishes. 
        member x.Dispose() = (x :> IAsyncDisposable).DisposeAsync() |> start

type ObjectPool with
    member x.WithInstance f = x.WithInstanceJob (fun a -> Job.result (f a))
    member x.WithInstanceAsync f = async { run (x.WithInstance f) }
