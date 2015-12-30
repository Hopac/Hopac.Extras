[<AutoOpen>]
module Hopac.Extras.Tests.Utils

open Hopac.Infixes
open Hopac

let runWith defaultOp testedOp = testedOp <|> defaultOp |> run

