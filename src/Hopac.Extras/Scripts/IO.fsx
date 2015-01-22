#load "load-project.fsx" 
#r "System.Runtime"
#r "System.Threading.Tasks"

open System
open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Alt.Infixes
open Hopac.Extras

let pr = ProcessRunner.start "notepad.exe" ""
let timeoutAlt = Timer.Global.timeOutMillis 10000

let rec loop() =
    (pr.LineOutput >>=? fun line -> job { printfn "Line: %s" line } >>. loop()) <|>?
    (pr.ProcessExited |>>? fun res -> printfn "Exited with %A." res) <|>?
    (timeoutAlt >>=? fun _ -> pr.Kill() |>> printfn "Killed by timeout. Result: %A")
    // ...more Alts here...

start (loop())
