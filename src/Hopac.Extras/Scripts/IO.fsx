#load "load-project.fsx" 
#r "System.Runtime"
#r "System.Threading.Tasks"

open System
open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Alt.Infixes
open Hopac.Extras

let reqCh = ch<Ch<unit>>()

Job.foreverServer (
    reqCh >>=? fun respCh ->
        // `Job.usingAsync` guaranties thet the rpocess will be killed when the given job finishes.
        Job.usingAsync (ProcessRunner.start "notepad.exe" "") <| fun pr ->
            let timeoutAlt = timeOutMillis 1000
            let rec loop() =
                // Wait for any of the following alternatives enabled (became available (for picking)).
                (pr.LineOutput >>=? fun line -> job { printfn "Line: %s" line } >>. loop()) <|>?
                (pr.ProcessExited |>>? fun res -> printfn "Exited with %A." res) <|>?
                (timeoutAlt >>=? fun _ -> job { printfn "Timeout." })
            loop()
    >>= Ch.give respCh
)
|> start

let exec() = Job.delay <| fun _ ->
    let respCh = ch()
    reqCh <-+ respCh >>. respCh

run (exec())

let i = ivar<unit>()
run (IVar.tryFill i ())

// File.startReading

let file = @"l:\temp\test.txt"

Job.usingAsync (File.startReading file) <| fun reader ->
    let rec loop() = Job.delay <| fun _ ->
        (reader.NewLine >>=? fun line -> 
            printfn "new line: %s" line 
            //if line = "5. -----" then 
            //    printfn "found the line, stopping"
            //    Job.unit()
            //else 
            loop()) <|>?
        (Timer.Global.timeOutMillis 30000 >>=? fun _ ->
            printfn "Timeout."
            Job.unit())
    loop()
|>> fun _ -> printfn "Beyond the loop."
|> start