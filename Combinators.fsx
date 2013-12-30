/// Timer

/// This time combinator is a curried, higher-order function
/// that accepts a function f and returns a function that applies f ,
/// print the time taken and returns the result of the application of f
let time f x =
    let t = new System.Diagnostics.Stopwatch()
    t.Start()
    try f x finally
    printf "Took %dms\n" t.ElapsedMilliseconds

/// For example, the computation of 1000! may be timed with:
time (Seq.fold ( * ) 1I) {1I .. 1000I}


/// Derivative

/// A numerical form of the derivative of a one-argument function
let epsilon_float = 0.0000001
let d f x =
    let dx = sqrt epsilon_float
    (f(x + dx) - f(x - dx)) / (2. * dx)

/// The d combinator can be used to approximate f'(2)=11:
let f x = x * x * x - x - 1.
d f 2.


/// Nest

/// n times repeated applications of a function f to an argument x to give f(f(..f(x)..))
let rec nest n f x =
    if n=0 then x
    else nest (n-1) f (f x)

/// For example, raise-to-the power in terms of repeated multiplication
let pow n m =
    nest m (( * ) n) 1

/// For example, chop a list in two at the given index
/// by repeatedly decapitating the first element from the list.
/// Note that first list will be in reversed order.
let chop n list =
    let aux = function
      | front, h::back -> h::front, back
      | _, [] -> failwith "invalid argument"
    nest n aux ([], list)


/// Iterating to fixed point

let rec fixed_point f x =
    let f_x = f x
    if x = f_x then x else fixed_point f f_x

fixed_point (fun x -> sqrt (1. + x)) 1.
/// The function only terminated here because
/// the next result was rounded to the same float value.
/// This is a dangerous practice to use with floating-point numbers.

/// Memoization

let rec fib = function
    | 0 | 1 as n -> n
    | n -> fib(n-1) + fib(n-2)

/// Simple memoization
open System.Collections.Generic

let simple_memoize (f: 'x -> 'y) =
    let m = new Dictionary<'x,'y>(HashIdentity.Structural)
    let m_f = (fun x ->
      try m.[x] with _ ->
      let f_x = f x
      m.[x] <- f_x
      f_x)
    m_f

let mfib = simple_memoize fib;;
time mfib 35;;

/// Recursive memoization / Stack Overflow

let memoize (f: 'x -> 'x) =
    let m = new Dictionary<'x,'x>(HashIdentity.Structural)
    let rec f' x =
      try m.[x] with _ ->
        let f_x = f (f' x)
        m.[x] <- f_x
        f_x
    f'

let mfib2 = memoize fib;;
time mfib2 35;;
