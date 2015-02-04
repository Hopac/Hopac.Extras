namespace Hopac.Extras

open System
open System.Diagnostics
open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Alt.Infixes

module ProcessRunner =
    /// Creates ProcessStartInfo to start a hidden process with standard output / error redirected.
    let createStartInfo exePath args =
        ProcessStartInfo(
            FileName = exePath,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            ErrorDialog = false,
            RedirectStandardOutput = true, 
            RedirectStandardError = true)

    /// Creates process with EnableRaisingEvents = true. Does not actually start it.
    let createProcess startInfo = new Process(StartInfo = startInfo, EnableRaisingEvents = true)

    [<NoComparison>]
    type ExitError =
        | NonZeroExitCode of int
        | KillTimeout of TimeSpan
        | CannotKill of exn

    let private exitCodeToError = function
        | 0 -> Ok()
        | code -> Fail (NonZeroExitCode code)

    let private getExitCodeSafe (p: Process) = try p.ExitCode with _ -> 0
    let private hasExitedSafe (p: Process) = try p.HasExited with _ -> true

    let private kill (p: Process) =
        try
            if hasExitedSafe p then
                exitCodeToError (getExitCodeSafe p)
            else
                let killTimeout = TimeSpan.FromSeconds 10.
                try p.Kill() 
                    if p.WaitForExit (int killTimeout.TotalMilliseconds) then 
                        exitCodeToError (getExitCodeSafe p)
                    else Fail (KillTimeout killTimeout)
                with e -> Fail (CannotKill e)
        finally try p.Dispose() with _ -> ()

    [<NoComparison; NoEquality>]
    type RunningProcess = 
        { /// Process's standard output feed.
          LineOutput: Alt<string>
          /// Available for picking when the process has exited.
          ProcessExited: Alt<Choice<unit, ExitError>>
          /// Job that kills the process.
          Kill: unit -> Job<Choice<unit, ExitError>> }
        interface IAsyncDisposable with
            member x.DisposeAsync() = x.Kill() |>> ignore

    /// Starts given Process asynchronously and returns RunningProcess instance.
    let startProcess (p: Process) : RunningProcess =
        let lineOutput = mb()
        let processExited = ivar()

        // Subscribe for two events and use 'start' to execute a single message passing operation 
        // which guarantees that the operations can be/are observed in the order in which the events are triggered.
        // (If we would use queue the lines could be sent to the mailbox in some non-deterministic order.)
        p.OutputDataReceived.Add (fun args ->
            if args.Data <> null then 
                lineOutput <<-+ args.Data |> start)
        
        p.Exited.Add (fun _ -> 
            let exitCode = getExitCodeSafe p
            IVar.tryFill processExited (exitCodeToError exitCode) |> start)
        if not <| p.Start() then failwithf "Cannot start %s." p.StartInfo.FileName
        p.BeginOutputReadLine()

        { LineOutput = lineOutput   
          ProcessExited = processExited
          Kill = fun _ -> job { return kill p }}

    /// Starts given process asynchronously and returns RunningProcess instance.
    let start exePath args = createStartInfo exePath args |> createProcess |> startProcess

module File =    
    open System.IO

    [<NoComparison; NoEquality>]
    type FileReader =
        { /// Available for picking when a new line appeared in the file.
          NewLine: Alt<string> 
          /// Creates a job that disposes the file.
          Close: Job<unit> }
        interface IAsyncDisposable with
            member x.DisposeAsync() = x.Close

    /// Reads a text file continuously, performing non-blocking pooling for new lines.
    /// It's safe to call when file does not exist yet. When the file is created, 
    // this function opens it and starts reading.
    let startReading (path: string) : FileReader =
        let newLine = mb()
        let close = ivar()

        /// Becomes available for picking if file exists. 
        /// If file does not exist, it keeps non-blocking pooling until `close` ivar is filled.
        let fileExists path = 
            let exists = ivar()
            let rec loop() = Job.delay <| fun _ ->
                close >>%? () <|>?
                (Alt.guard << Job.delay <| fun _ -> 
                    if File.Exists path then exists <-= () >>% Alt.always()
                    else Job.result <| Alt.never()) >>%? () <|>?
                (timeOutMillis 500 >>.? loop())
            start (loop())
            exists :> Alt<_>
        
        /// If file exists, it creates StreamReader over it and become available for picking.
        /// If file does not exists, it keeps non-blocking pooling until `close` ivar is filled.
        let openStreamReader path =
            fileExists path |>>? fun _ -> 
                let file = new FileStream (path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                new StreamReader(file)

        let readLineAlt (file: StreamReader) : Alt<string> = Alt.delay <| fun _ -> 
            if file.EndOfStream then Alt.never()
            else Alt.always (file.ReadLine())

        let rec openedStreamReader (file: StreamReader) = Job.delay <| fun _ ->
            (readLineAlt file >>=? Mailbox.send newLine >>.? openedStreamReader file) <|>?
            (close |>>? fun _ -> file.Dispose()) <|>?
            (timeOutMillis 500 >>.? openedStreamReader file)
        
        start (openStreamReader path >>=? openedStreamReader)
        { NewLine = newLine
          Close = IVar.tryFill close () }