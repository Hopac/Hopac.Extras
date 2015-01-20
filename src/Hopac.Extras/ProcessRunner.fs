namespace Hopac.Extras

open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Alt.Infixes

module ProcessRunner =
    open System.Diagnostics

    let createStartInfo exePath args =
        ProcessStartInfo(
            FileName = exePath,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            ErrorDialog = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true)

    let createProcess startInfo = new Process(StartInfo = startInfo, EnableRaisingEvents = true)

    [<NoComparison>]
    type ExitResult = 
        | Exited of errorCode: int 
        | CannotExit
        | KillTimeout
        | CannotKill of exn

    let kill (p: Process) =
        p.CancelOutputRead()
        if p.HasExited then
            Exited p.ExitCode
        else
            try p.Kill() 
                if p.WaitForExit 10000 then 
                    Exited p.ExitCode 
                else KillTimeout
            with e -> CannotKill e

    type Line = string
    
    [<NoComparison>]
    type RunningProcess = 
        { LineOutput: Ch<Line>
          ProcessExited: IVar<ExitResult> }

    let execute (p: Process) : RunningProcess =
        let lineOutput = ch()
        let processExited = ivar()

//        let server() =
//            Streams.subscribingTo p.OutputDataReceived <| Streams.iterJob (fun args -> 
//                if args.Data <> null then 
//                    lineOutput <-+ args.Data
//                else Job.unit())
//            >>. 
//            (Streams.subscribingTo p.Exited <| Streams.iterJob (fun _ ->
//                Job.start (processExited <-= Exited p.ExitCode)))
        
//        start (server())
        if not <| p.Start() then failwithf "Cannot start %s." p.StartInfo.FileName
        p.BeginOutputReadLine()
        { LineOutput = lineOutput; ProcessExited = processExited }        

    