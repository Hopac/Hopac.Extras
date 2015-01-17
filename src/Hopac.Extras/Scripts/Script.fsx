#load "load-project.fsx"
#r "System.Runtime"
#r "System.Threading.Tasks"

open Hopac
open Hopac.Infixes
open Hopac.Alt.Infixes
open Hopac.Job.Infixes
open Hopac.Extras
open System
open System.Threading

type Entry = 
    { Value: int }
    interface IDisposable with
        member x.Dispose() = () //printfn "%A disposed." x

let iteration () =
    let pool = new ObjectPool<_>((fun _ -> { Value = 0 }), 100u)
    let results = ref 0L
    let jobs = 100000
    List.init jobs (fun i -> pool.WithInstanceJob (fun _ -> job { Interlocked.Add(results, int64 i + 1L) |> ignore })) 
    |> Job.conIgnore
    |> run
    (pool :> IDisposable).Dispose()

iteration()


// old, 100000 = Real: 00:00:12.585, CPU: 00:00:25.786, GC gen0: 3, gen1: 2, gen2: 1

//for i in 1..100 do
//    printfn "Start %d." i
//    iteration()
//    printfn "Done %d." i


//let pool = 
//    new ObjectPool<_>(
//        (fun() -> 
//            let e = { Value = int DateTime.Now.Ticks }
//            printfn "Created: %A." e
//            e),
//        1u,
//        TimeSpan.FromSeconds 500.)
//
//run <| pool.WithInstanceJob (fun e -> Job.result <| printfn "Using %A..." e)
//(pool :> IDisposable).Dispose()

//let entry v = PoolEntry.Create { Value = v }
//let timeout = TimeSpan.FromSeconds 5.
//
//let es = [entry 1; entry 2; entry 3]
//
//es |> List.map (fun e -> Alt.delay <| fun _ ->
//    match (e.LastUsed + timeout) - DateTime.Now with
//    | t when t <= TimeSpan.Zero -> Alt.always e
//    | t -> Timer.Global.timeOut t >>%? e)
//|> Alt.choose
//|> run

//let ch = ch<int>()
//
//let take() = Ch.take ch
//
//let g (f: int -> Alt<_>) = Alt.guard << Job.delay <| fun _ ->
//    take() |>>? fun x -> f x
//    
//run (ch <-+ 0)
//
//(g (fun _ -> Timer.Global.timeOutMillis 10000 |>>? fun _ -> printfn "g timeout")) <|>?
//(Timer.Global.timeOutMillis 3000 |>>? fun _ -> printfn "outer timeout")
//|> run