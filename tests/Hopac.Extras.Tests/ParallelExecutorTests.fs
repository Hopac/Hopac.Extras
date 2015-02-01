module Hopac.Extras.Tests.ParallelExecutorTests

open Hopac
open Hopac.Infixes
open Hopac.Job.Infixes
open Hopac.Alt.Infixes
open Hopac.Extras
open NUnit.Framework
open FsCheck
open System
open FsCheck.Prop

[<Test; Timeout(5000)>]
let ``processes all messages in source``() =
    let prop (degree: uint16) (messageCount: uint32) =
        (degree > 0us && messageCount > 0u) ==> lazy (
            let source = ch<int>()
            let results = mb<int>()
            let _ = ParallelExecutor(degree, source, fun x -> results <<-+ x >>% Ok())
            // synchronously send messages to the source 
            run <| Job.forUpTo 1 (int messageCount) (fun i -> source <-- i)
            let res = run <| Job.conCollect (seq { for _ in 1u..messageCount -> Mailbox.take results })
            let expected = [1u..messageCount]
            let actual = List.ofSeq res
            CollectionAssert.AreEquivalent (expected, actual))
    
    Check.VerboseThrowOnFailure prop
    
type MessageWithResults = 
    { Id: Guid
      mutable LastResult: Choice<unit, WorkerError<unit>> option
      mutable Results: Choice<unit, WorkerError<unit>> list }

type Generators = 
    static member MessageWithResultsArb = 
        fun id results -> { Id = id; Results = results @ [Ok()]; LastResult = None }
        <!> Arb.generate<Guid>
        <*> Arb.generate |> Gen.nonEmptyListOf
        |> Arb.fromGen

[<Test; Explicit; Timeout(5000)>]
let ``processes a message until worker returns OK``() =
    let prop (messages: MessageWithResults list) =
        let source = ch<MessageWithResults>()
        let completed = mb()
        let _ = ParallelExecutor(1us, source, (fun msg -> 
            Job.result <|
                match msg with
                | { Results = [] } -> 
                    failwith "Got empty results list. It should not ever happen because we deliberately add Ok() to each list."
                | { Results = h :: t } -> 
                    msg.Results <- t
                    msg.LastResult <- Some h
                    h 
            ), completed)

        messages |> List.map (fun x -> source <-- x) |> Job.conIgnore |> run
        let actual = 
            messages 
            |> List.mapi (fun i _ -> job {
                printfn "Waiting for msg #%d..." i
                let! msg = completed 
                printfn "Got msg #%d: %A" i msg
                return msg })
            |> Job.conCollect
            |> run
        
        actual 
        |> Seq.map (fun (m, _) -> m.LastResult) 
        |> Seq.forall (function Some Ok | Some (Fail (Fatal _)) -> true | _ -> false)
        

    Check.VerboseThrowOnFailure (forAll Generators.MessageWithResultsArb prop)