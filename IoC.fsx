/// posted by holoed
/// http://fsharpcode.blogspot.de/2011/08/functional-ioc-container-example.html

open System

type StateMonad() =
    member this.ReturnFrom x = x
    member this.Return x s = (x, s)
    member this.Bind (m, f) s = let (x, s1) = m s
                                in (f x) s1
let state = new StateMonad()

type IEnvironment =
    abstract member UserName : string with get

type ILogger =
    abstract member Log : string -> unit

type Container = { Environment : IEnvironment
                   Logger : ILogger }

let getContainer = fun f -> (f(), f)

let run ctx m = m (fun () -> ctx) |> fst

let rec fac n acc  = state { let! container = getContainer
                             container.Logger.Log (sprintf "%d" acc)
                             if (n = 0) then return acc
                             else return! fac (n - 1) (n * acc) }

let compute n = state { let! container = getContainer                       
                        container.Logger.Log (sprintf "Begin fac %d" n)
                        let! result = fac n 1                                      
                        container.Logger.Log (sprintf "End fac %d" n)
                        let userName = container.Environment.UserName
                        container.Logger.Log (sprintf "Computed by %s" userName)
                        return result }
let prodContainer =
    { Environment = { new IEnvironment with                         
                          member this.UserName
                                  with get () =
                                       Environment.UserName }
      Logger = { new ILogger with
                    member this.Log s =
                        Console.WriteLine ("{0} : {1}", (DateTime.Now), s) } }

let testContainer =
    { Environment = { new IEnvironment with                         
                          member this.UserName
                                  with get () =
                                       "John Doe" }
      Logger = { new ILogger with
                    member this.Log s = Console.WriteLine (" {0} ",  s) } }

printfn "Result %A" (run prodContainer (compute 10))

printfn "Result %A" (run testContainer (compute 10))