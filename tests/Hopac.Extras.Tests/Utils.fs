[<AutoOpen>]
module Hopac.Extras.Tests.Utils

open Hopac.Alt.Infixes
open Hopac

let runWith defaultOp testedOp = testedOp <|> defaultOp |> run

