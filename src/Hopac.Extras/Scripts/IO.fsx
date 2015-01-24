#load "load-project.fsx" 
#r "System.Runtime"
#r "System.Threading.Tasks"

open System
open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Alt.Infixes
open Hopac.Extras

// `Job.usingAsync` guaranties thet the rpocess will be killed when the given job finishes.
Job.usingAsync (ProcessRunner.start "notepad.exe" "") <| fun pr ->
    let timeoutAlt = timeOutMillis 5000
    let rec loop() =
        // Wait for any of the following alternatives enabled (became available (for picking)).
        (pr.LineOutput >>=? fun line -> job { printfn "Line: %s" line } >>. loop()) <|>?
        (pr.ProcessExited |>>? fun res -> printfn "Exited with %A." res) <|>?
        (timeoutAlt >>%? printfn "Timeout.")
    loop()        
|> start
