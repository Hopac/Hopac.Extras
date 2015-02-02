[<AutoOpen>]
module Hopac.Extras.Utils

open System

let inline Ok a: Choice<_, _> = Choice1Of2 a
let inline Fail a: Choice<_, _> = Choice2Of2 a

let (|Ok|Fail|) =
    function 
    | Choice1Of2 a -> Ok a
    | Choice2Of2 a -> Fail a

let inline dispose (x: IDisposable) = x.Dispose()