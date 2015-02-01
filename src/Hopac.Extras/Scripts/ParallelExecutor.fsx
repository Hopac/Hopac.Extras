#load "load-project.fsx"

#r "System.Runtime"
#r "System.Threading"
#r "System.Threading.Tasks"

open Hopac
open Hopac.Infixes
open Hopac.Alt.Infixes
open Hopac.Job.Infixes
open Hopac.Extras

type Msg = int ref

let src = ch()
let completed = mb()

let _ = ParallelExecutor<Msg, unit>(1us, src, (fun msg -> 
    job { 
        incr msg
        return if !msg = 1 then Fail (Recoverable ()) else Ok() 
    }), completed)

Job.forUpTo 1 5 (fun i -> src <-- ref i) 
>>. ([1..5] |> List.map (fun _ -> completed) |> Job.conCollect)
|> run