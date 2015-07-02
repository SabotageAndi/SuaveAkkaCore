namespace SuaveAkkaCore

open Suave
open Suave.Http
open Suave.Http.Successful
open Suave.Http.Redirection
open Suave.Http.Applicatives
open Suave.Http.Files
open Suave.Web
open Suave.Http.Writers
open System.Threading
open System
open System.IO

open Akka
open Akka.Actor
open Akka.FSharp


type Program() =
  
  let webConfig = defaultConfig

  let mutable akkaSystem = None
  
  let sendRequestToActor url =
    let callActor = async { 
        match akkaSystem with
        | Some x ->
          let actor = select "akka://SuaveAkkaCore/user/root" x
          let! resp = actor <? url
          printfn "%s" resp
          return resp
        | None ->
          printfn "no system here"
          return ""
      } 
    let result = callActor |> Async.RunSynchronously
    printfn "Result: %s" result
    result

  let app = 
      choose 
        [
          GET >>= pathScan "/%s" (fun r -> OK <| sendRequestToActor r)
          POST >>= pathScan "/%s" (fun r -> OK <| sendRequestToActor r)
          Suave.Http.RequestErrors.NOT_FOUND "Found no handlers"
        ]

  
  member x.Main () =
  
    let system = System.create "SuaveAkkaCore" (Configuration.defaultConfig())
  
    akkaSystem <- Some system
    
  
    let cts = new CancellationTokenSource()
    let startingServer, shutdownServer = startWebServerAsync webConfig app

    Async.Start(shutdownServer, cts.Token)
    
    let rootActor = spawn system "root" (fun mailbox ->
                                          let rec loop() = actor {
                                            let! message = mailbox.Receive()
                                          
                                       
                                            printfn "Actor gets called"
                                            printfn "Message: %s" message
                                          
                                          
                                            return! loop()
                                          }
                                          loop())


    startingServer |> Async.RunSynchronously |> printfn "started: %A"

    rootActor <! ""

    printfn "Press Enter to stop"
    Console.Read() |> ignore

    system.Dispose()
    cts.Cancel()

