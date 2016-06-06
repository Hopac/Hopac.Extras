open System.Threading
open System

[<EntryPoint>]
let main = function
    | [|exitAfterSeconds; exitCode|] ->
        let exitAfterSeconds = int exitAfterSeconds
        let exitCode = int exitCode
        Console.WriteLine (sprintf "Will be exit with code = %d after %d ms timeout..." exitCode exitAfterSeconds)
        Thread.Sleep (TimeSpan.FromSeconds (float exitAfterSeconds))
        exitCode
    | args -> failwithf "Wrong arguments: %A, expected <exit after seconds> <exit code>" args