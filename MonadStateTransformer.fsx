/// posted by holoed
/// http://fsharpcode.blogspot.de/2010/05/monad-state-transformer.html

// Reactive Extensions v1.0.10425 Computation Expression Builder
// http://msdn.microsoft.com/en-us/data/gg577609

#r @"C:\Users\boe\Documents\Visual Studio 2013\Projects\fsharp-wpf-mvc-series\Chapter 01 - Intro\packages\Rx-Core.2.1.30214.0\lib\Net40\System.Reactive.Core.dll"
#r @"C:\Users\boe\Documents\Visual Studio 2013\Projects\fsharp-wpf-mvc-series\Chapter 01 - Intro\packages\Rx-Linq.2.1.30214.0\lib\Net40\System.Reactive.Linq.dll"
#r @"C:\Users\boe\Documents\Visual Studio 2013\Projects\fsharp-wpf-mvc-series\Chapter 01 - Intro\packages\Rx-Interfaces.2.1.30214.0\lib\Net40\System.Reactive.Interfaces.dll"
#r @"C:\Users\boe\Documents\Visual Studio 2013\Projects\fsharp-wpf-mvc-series\Chapter 01 - Intro\packages\Rx-PlatformServices.2.1.30214.0\lib\Net40\System.Reactive.PlatformServices.dll"

open System
open System.Linq
open System.Runtime
open System.Reactive.Linq


type rxBuilder() =    
    member this.Bind ((xs:'a IObservable), (f:'a -> 'b IObservable)) =
        Observable.SelectMany (xs, f)
    member this.Delay (f: unit -> 'b IObservable) = Observable.Defer f  /// added type hints
    member this.Return x = Observable.Return x
    member this.ReturnFrom xs = xs
    member this.Combine (xs:'a IObservable, ys: 'a IObservable) =
        Observable.Concat (xs, ys)
    member this.For (xs : 'a seq, f: 'a -> 'b IObservable) =
        Observable.For(xs, new Func<_, IObservable<_>>(f)) 
    member this.TryFinally (xs: 'a IObservable, f : unit -> unit) =
        Observable.Finally(xs, new Action(f))
    member this.TryWith (xs: 'a IObservable, f: exn -> 'a IObservable) =
        Observable.Catch (xs, new Func<exn, 'a IObservable>(f))
    member this.While (f, xs: 'a IObservable) =
        Observable.While (new Func<bool>(f), xs)
    member this.Yield x = Observable.Return x
    member this.YieldFrom xs = xs
    member this.Zero () = Observable.Empty()
              
let rx = rxBuilder()

// Rx combinators

let repeat (xs:IObservable<_>) = xs.Repeat()

// Sample usages

let xs = rx { yield 42
              yield 43 }

let ys = rx { yield 42
              yield! xs }

let zs = rx { for i = 0 to 10 do yield i }

let redTime = rx { while (DateTime.Now.Second > 30) do
                      yield ConsoleColor.Red }

let blueTime = rx { while (DateTime.Now.Second < 30) do
                      yield ConsoleColor.Green }

let coloredTime  = rx { yield! redTime
                        yield! blueTime } |> repeat



redTime.Do( fun _ -> System.Console.WriteLine "test" ).Subscribe()
redTime.Materialize()
redTime.Publish()