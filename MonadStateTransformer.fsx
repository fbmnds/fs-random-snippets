/// original source:
/// http://fsharpcode.blogspot.de/2011/05/reactive-extensions-v1010425.html
/// http://blogs.msdn.com/b/dsyme/archive/2011/05/30/nice-f-syntax-for-rx-reactive-extensions.aspx

/// current F# reference implementation:
/// https://github.com/fsprojects/FSharp.Control.Reactive

// Rx home page:
// Reactive Extensions v1.0.10425 Computation Expression Builder
// http://msdn.microsoft.com/en-us/data/gg577609

#I @"Rx\"
#r @"System.Reactive.Core.dll"
#r @"System.Reactive.Linq.dll"
#r @"System.Reactive.Interfaces.dll"
#r @"System.Reactive.PlatformServices.dll"

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
                      yield ConsoleColor.Blue }

let coloredTime  = rx { yield! redTime
                        yield! blueTime } |> repeat


// implementation issue:
// http://stackoverflow.com/questions/6162288/how-do-i-change-the-rx-builder-implementation-to-fix-the-stack-overflow-exception
//coloredTime.Do( fun _ -> System.Console.WriteLine "test" ).Subscribe()
//coloredTime.Materialize()
//coloredTime.Publish()

// test ConsoleColor visibility: 
// in cmd.exe ok, in VS not visible  
let sfc = System.Console.ForegroundColor
if System.Console.ForegroundColor = ConsoleColor.DarkRed then
    System.Console.ForegroundColor <- ConsoleColor.DarkGreen
else 
    System.Console.ForegroundColor <- ConsoleColor.DarkRed
System.Console.WriteLine "test"
System.Console.ForegroundColor <- sfc