namespace SuaveAkkaCore

open Suave
open Suave.Http
open Suave.Http.Successful
open Suave.Http.Redirection
open Suave.Http.Applicatives
open Suave.Http.Files
open Suave.Types
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
  
  let sendRequestToActor (httpContext : HttpContext) =
    let callActor = async { 
        match akkaSystem with
        | Some x ->
          let actor = select "akka://SuaveAkkaCore/user/root" x
          let! resp = actor <? httpContext
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
      
  let app : WebPart = 
      fun (httpContext : HttpContext) ->
        async {
          let response = sendRequestToActor httpContext
          return response
        }
        
  let mutable counter = 0       
  
  let handleRequest (mailbox: Actor<'a>) (msg : HttpContext) =
  
    let url = msg.request.url.ToString()
    printfn "Message: %s" url      
                         
    counter <- counter + 1
    
    printfn "Request %i" counter
                                     
    mailbox.Sender() <! OK url
    

  
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

