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
          return Some resp
        | None ->
          printfn "no system here"
          return None
      } 
    let result = callActor |> Async.RunSynchronously
    
    match result with
    | Some response ->
      response
    | None ->
      Suave.Http.ServerErrors.SERVICE_UNAVAILABLE "System not running"
      
  let app = 
      choose 
        [
          GET >>= pathScan "/%s" (fun r -> (sendRequestToActor r))
          POST >>= pathScan "/%s" (fun r -> (sendRequestToActor r))
          Suave.Http.RequestErrors.NOT_FOUND "Found no handlers"
        ]
        
  let handleRequest (mailbox: Actor<'a>) msg =
    printfn "Actor gets called"
    printfn "Message: %s" msg      
                                     
    mailbox.Sender() <! OK msg
    

  
  member x.Main () =
  
    let system = System.create "SuaveAkkaCore" (Configuration.defaultConfig())
  
    akkaSystem <- Some system
    
  
    let cts = new CancellationTokenSource()
    let startingServer, shutdownServer = startWebServerAsync webConfig app

    Async.Start(shutdownServer, cts.Token)
    
    let rootActor = spawn system "root" (actorOf2 handleRequest)
    

    startingServer |> Async.RunSynchronously |> printfn "started: %A"


    printfn "Press Enter to stop"
    Console.Read() |> ignore

    system.Dispose()
    cts.Cancel()

