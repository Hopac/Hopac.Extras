#load "load-references.fsx"
#r "System.Runtime"
#r "System.Threading.Tasks"
#load @"..\Utils.fs" @"..\Control.fs"

open Hopac
open Hopac.Infixes
open Hopac.Extras

jobChoice { return 1 } |> JobChoice.map (fun x -> x + 1) |> run

let stopTime (f: unit -> Job<Choice<'a, 'e>>) = 
  jobChoice {
    let stopwatch = System.Diagnostics.Stopwatch.StartNew()
    let! result = f()
    stopwatch.Stop() 
    return result, stopwatch.Elapsed
  }

let stage name f = 
  let st = stopTime f
  st |> JobChoice.map (fun c -> c) 