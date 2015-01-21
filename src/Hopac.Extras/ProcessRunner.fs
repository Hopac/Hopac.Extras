namespace Hopac.Extras

open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Alt.Infixes

module ProcessRunner =
    open System.Diagnostics
    open System

    /// Creates ProcessStartInfo to start a process with no window and redirected standard output and error.
    let createStartInfo exePath args =
        ProcessStartInfo(
            FileName = exePath,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            ErrorDialog = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true)

    /// Creates process with EnableRaisingEvents = true.
    let createProcess startInfo = new Process(StartInfo = startInfo, EnableRaisingEvents = true)

    [<NoComparison>]
    type ExitResult = 
        | Exited of errorCode: int 
        | CannotExit
        | KillTimeout
        | CannotKill of exn

    let private kill (p: Process) =
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
    
    [<NoComparison; NoEquality>]
    type RunningProcess = 
        { /// Process's standard output feed.
          LineOutput: Alt<Line>
          /// Available for picking when the process has exited.
          ProcessExited: Alt<ExitResult>
          /// Available for picking when the process StartTime + given timeout >= now.
          Timeout: TimeSpan -> Alt<unit>
          /// Synchronously kill the process.
          Kill: unit -> ExitResult }

    /// Starts given Process asynchronously and returns RunningProcess instance.
    let startProcess (p: Process) : RunningProcess =
        let lineOutput = mb()
        let processExited = ivar()

        // Subscribe for two events and use 'start' to execute a single message passing operation 
        // which guarantees that the operations can be/are observed in the order in which the events are triggered.
        // (If we would use queue the lines could be sent to the mailbox in some non-deterministic order.)
        p.OutputDataReceived.Add <| fun args -> 
            if args.Data <> null then 
                lineOutput <<-+ args.Data |> start
        
        p.Exited.Add <| fun _ -> processExited <-= kill p |> start
        if not <| p.Start() then failwithf "Cannot start %s." p.StartInfo.FileName
        p.BeginOutputReadLine()
        let startTime = DateTime.UtcNow
        
        let timeoutAlt timeout = Alt.delay <| fun _ ->
            match (startTime + timeout) - DateTime.UtcNow with
            | t when t > TimeSpan.Zero -> Timer.Global.timeOut t
            | _ -> Alt.always()

        { LineOutput = lineOutput   
          ProcessExited = processExited
          Timeout = timeoutAlt
          Kill = fun _ -> kill p }        

    /// Starts given process asynchronously and returns RunningProcess instance.
    let start exePath args = createStartInfo exePath args |> createProcess |> startProcess
    