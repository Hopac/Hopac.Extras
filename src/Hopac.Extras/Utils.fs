[<AutoOpen>]
module Hopac.Extras.Utils

open System

let inline Ok a: Choice<_, _> = Choice1Of2 a
let inline Fail a: Choice<_, _> = Choice2Of2 a

let inline (|Ok|Fail|) x =
    match x with
    | Choice1Of2 a -> Ok a
    | Choice2Of2 a -> Fail a

let inline dispose (x: IDisposable) = x.Dispose()

[<RequireQualifiedAccess>]
module Choice =
  let inline bind x2yC = function Ok x -> x2yC x | Fail e -> Fail e
  let inline map x2y = function Ok x -> Ok (x2y x) | Fail e -> Fail e
  let inline mapError e2f = function Ok x -> Ok x | Fail e -> Fail (e2f e)