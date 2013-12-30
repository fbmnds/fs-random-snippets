open System.Threading


/// execute the given function f concurrently on a separate thread
let spawn (f : unit -> unit) =
    let thread = new Thread(f)
    thread.Start()
    thread

/// spawns several threads and wait for them all to complete
let execute n f =
    [|for i in 1 .. n -> spawn f|]
    |> Array.iter (fun t -> t.Join())

/// naive implementation of a parallel map,
/// can give a significant performance improvement
/// when the input array contains one element for each CPU
let map_naive f a =
    let b = Array.create (Array.length a) None
    Array.mapi (fun i a -> spawn (fun () -> b.[i] <- Some(f a))) a
    |> Array.iter (fun t -> t.Join())
    Array.map Option.get b

/// time combinator
let time f x =
    let t = new System.Diagnostics.Stopwatch()
    t.Start()
    try f x finally
    printf "Took %dms\n" t.ElapsedMilliseconds

/// Fibonacci function
let rec fib = function
    | 0 | 1 as n -> n
    | n -> fib(n-1)+fib(n-2)

time (Array.map fib) [|40; 40|]

time (map_naive fib) [|40; 40|]

time (Array.map (( + ) 1)) [|1 .. 10000|]

time (map_naive (( + ) 1)) [|1 .. 10000|]

//Microsoft.FSharp.Idioms.lock is now Microsoft.FSharp.lock
let l() = lock()

/// a threadsafe increment of an int ref called n
let l2 n = lock n (fun () -> incr n)

/// increment the counter n and return the next unmapped element (if any)
let next n =
    let i = ref 0
    fun () ->
      (fun () ->
         if !i = n then None else
           incr i
           Some(!i - 1))
      |> lock i

/// a worker thread that repeatedly maps array elements until none remain
let rec worker next apply =
    match next() with
    | None -> ()
    | Some i ->
        apply i;
        worker next apply

let print_int x =  printf "%d" x
worker (next 10) print_int


/// The parallel map itself spawns threads executing a loop function
/// which repeatedly maps a single element and looks for the next unmapped element.
/// Note that this implementation is also careful to use the main thread as
/// one of the workers, avoiding the creation of one thread.
let map n f a =
    let m = Array.length a
    let next = next m
    let b = Array.create m None
    let apply i = b.[i] <- Some(f a.[i])
    let x() = worker next apply
    let ts =
      [|for i in 2 .. min n m ->
          spawn x |]
    worker next apply
    ts |> Seq.iter (fun t -> t.Join())
    Array.map Option.get b

let cpu_map f a =
    map System.Environment.ProcessorCount f a

time (cpu_map fib) [|40; 40|]

/// A parallel map that uses the global thread pool by invoking asynchronous delegates
let global_map f a =
    let d = new System.Converter<'a, 'b>(f)
    Array.map (fun x -> d.BeginInvoke(x, null, null)) a |>
      Array.map (fun a -> d.EndInvoke(a))

time (global_map (( + ) 1)) [|1 .. 10000|]
