/// posted by holoed
/// http://fsharpcode.blogspot.de/2010/05/composing-contracts-part-1-data-types.html

#r @"C:\Users\boe\Documents\Visual Studio 2013\Projects\fsharp-wpf-mvc-series\Chapter 01 - Intro\packages\Rx-Core.2.1.30214.0\lib\Net40\System.Reactive.Core.dll"
#r @"C:\Users\boe\Documents\Visual Studio 2013\Projects\fsharp-wpf-mvc-series\Chapter 01 - Intro\packages\Rx-Linq.2.1.30214.0\lib\Net40\System.Reactive.Linq.dll"
#r @"C:\Users\boe\Documents\Visual Studio 2013\Projects\fsharp-wpf-mvc-series\Chapter 01 - Intro\packages\Rx-Interfaces.2.1.30214.0\lib\Net40\System.Reactive.Interfaces.dll"
#r @"C:\Users\boe\Documents\Visual Studio 2013\Projects\fsharp-wpf-mvc-series\Chapter 01 - Intro\packages\Rx-PlatformServices.2.1.30214.0\lib\Net40\System.Reactive.PlatformServices.dll"

open System.Reactive.Linq

module ContractDataTypes =

    open System
    open System.Linq


    type Date = Date of DateTime
    type Days = Days of TimeSpan

    type Currency = GBP | USD

    type Money = Money of float * Currency
        with static member (*) (k, Money (v, c)) = Money (k * v, c)

    type Contract = | One of Currency
                    | Scale of (IObservable<double> * Contract)
                    | When of (IObservable<bool> * Contract)

    type RxBuilder() =
        member this.Bind(m:IObservable<'a>, f:'a -> IObservable<'b>) =
            Observable.SelectMany(m, new Func<'a, IObservable<'b>> (f))
        member this.Return x = Observable.Return x
        member this.ReturnFrom x = x
        member this.Zero() = Observable.Empty()

    let rx = RxBuilder()

module Contracts =

    open System
    open System.Linq
    open ContractDataTypes

    let date s = Date (DateTime.Parse s)

    let one c = One c

    let scale k c = Scale(k, c)

    let konst x = rx { return x }

    let (==*) l r = rx { let! x = l
                         let! y = r
                         return x = y }

    let obsTime = rx { let! x = Observable.Interval(TimeSpan.FromSeconds(1.0))
                       return Date(DateTime.Today.AddSeconds (float x)) }

    let at t = obsTime ==* (konst t)

    let cWhen t c = When (t, c)

    // zero-coupon bond
    let zcb t x k = cWhen (at t) (scale (konst x) (one k))

    let rec eval c = match c with
                     | When (t, c) -> rx { let! x = t
                                           if x then
                                                return! (eval c)  }
                     | Scale (k, c) -> rx { let! x = k
                                            let! y = eval c
                                            return x * y }
                     | One c -> match c with
                                | GBP -> rx { return Money (1.0, GBP) }
                                | USD -> rx { return Money (0.675310643, GBP) }

open System
open ContractDataTypes
open Contracts

let t1 = Date(DateTime.Today.AddSeconds(10.0))

// zero-coupon bond
let c1 = zcb t1 10.0 USD

let ret = eval c1

ret.Subscribe(printf "%A")

Console.ReadLine ()
