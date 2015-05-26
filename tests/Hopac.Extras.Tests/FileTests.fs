module Hopac.Extras.Tests.FileTests

open NUnit.Framework
open Hopac
open Hopac.Job.Infixes
open Hopac.Extras
open System
open System.IO
open Swensen.Unquote

let private (</>) x y = Path.Combine(x, y)
let private uniqueFileName() = Path.GetTempPath() </> string (Guid.NewGuid())
let private newFile name =
    let file = File.CreateText name
    file.AutoFlush <- true
    file

let writeToFile (file: StreamWriter) = Seq.iter (string >> file.WriteLine)

[<Test>]
let ``Signals about existing, then appended lines``() =
    let fileName = uniqueFileName()
    use file = newFile fileName
    writeToFile file [1..3]
    Job.usingAsync (File.startReading fileName) <| fun reader -> 
        job {
            let! existingLines = reader.NewLine <&> reader.NewLine <&> reader.NewLine
            existingLines =! (("1", "2"), "3")
            do! timeOutMillis 1000
            writeToFile file [4..5]
            let! appendedLines = reader.NewLine <&> reader.NewLine
            appendedLines =! ("4", "5")
        }
    |> run

[<Test>]
let ``It's ok to start reading a file before it's created``() =
    let fileName = uniqueFileName()
    Job.usingAsync (File.startReading fileName) <| fun reader -> 
        job {
            use file = newFile fileName
            writeToFile file [1..2]
            let! existingLines = reader.NewLine <&> reader.NewLine
            existingLines =! ("1", "2")
            file.Dispose()
        }
    |> run

