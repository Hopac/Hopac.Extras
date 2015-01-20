#load "load-project.fsx" 

open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Alt.Infixes
open Hopac.Extras

let pr =
    ProcessRunner.createStartInfo "notepad.exe" ""
    |> ProcessRunner.createProcess
    |> ProcessRunner.execute 

let rec loop() =
    (pr.LineOutput >>=? fun line -> 
        Job.start (job { printfn "Line: %s" line }) >>. loop()) <|>?
    (pr.ProcessExited |>>? fun res ->
        printfn "Exited with code %A." res)

start (loop())