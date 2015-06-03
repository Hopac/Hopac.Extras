namespace Hopac.Extras

open Hopac
open Hopac.Infixes
open Hopac.Alt.Infixes
open Hopac.Job.Infixes

[<Sealed>]
type Semaphore(n: int) = 
    do assert (0 <= n)
    let inc = Ch()
    let dec = Ch()
    do server << Job.iterate n <| fun n -> 
       if 0 < n 
       then dec ^->. n - 1 <|> inc ^->. n + 1
       else inc ^->. n + 1
    member __.Release = inc *<- ()
    member __.Wait = dec *<- ()

[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module Semaphore =
    let inline wait (s: Semaphore) = s.Wait
    let inline release (s: Semaphore) = s.Release
    let holding s j = Job.tryFinallyJob (wait s >>. j) s.Release
