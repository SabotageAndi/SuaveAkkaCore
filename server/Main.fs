namespace SuaveAkkaCore

open Suave
open Suave.Http
open Suave.Http.Successful
open Suave.Types
open Suave.Web
open System.Threading
open System

open Akka
open Akka.Actor
open Akka.FSharp

type Program() =
  
  let sendRequestToActor system (httpContext : HttpContext) =
    
    let callActor = async { 
      let actor = select "akka://SuaveAkkaCore/user/root" system
      let! resp = actor <? httpContext
      return Some resp
    }
   
    let response = 
      match callActor |> Async.RunSynchronously with
      | Some r ->
          r
      | None ->
          Suave.Http.ServerErrors.SERVICE_UNAVAILABLE "System not running"
    
    response httpContext    
                  
    
  let app system : WebPart = 
      fun (httpContext : HttpContext) ->
          async {
              let response = sendRequestToActor system httpContext
              return! response
          }
      
  let handleRequest (mailbox : Actor<'a>) (msg : HttpContext) =
    let url = msg.request.url.ToString()                                     
    mailbox.Sender() <! (OK url)
    
  
  member x.Main () =
    use akkaSystem = System.create "SuaveAkkaCore" (Configuration.defaultConfig())
  
    spawn akkaSystem "root" (actorOf2 handleRequest) |> ignore
  
    let cts = new CancellationTokenSource()
    
    let startingServer, shutdownServer = startWebServerAsync defaultConfig (app akkaSystem)
    Async.Start(shutdownServer, cts.Token)
    
    startingServer |> Async.RunSynchronously |> printfn "started: %A"

    printfn "Press Enter to stop"
    Console.Read() |> ignore

    
    cts.Cancel()

