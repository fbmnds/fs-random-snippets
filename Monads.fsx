/// http://stevegilham.blogspot.de/2013/12/an-introduction-to-functional_30.html

/// Continuations

/// Example: abstraction of reading the keyboard

type Request = Getq | Putq of char
type Response = Getp of char | Putp
type Dialog = Response list -> Request list

let readChar f p =
    Getq ::
    (match p with
     | (Getp c) :: p1 -> f c p1  // (*)
     | _ -> []);;
/// The hidden causality here is that putting the Getq at the head of the request queue
/// causes the Getp c (*) to exist at the head of the response queue
/// (perhaps by reading the keyboard).

let print (c:char) (p: Response list): Request list = printf "%c" c; [(Putq c)]

/// Generic Continuations

type continuation = K of (unit -> unit)
let execute (K f) = f()
let stop = K (fun () -> ());;

/// Producer: (X -> continuation) -> continuation
/// Consumer: X -> continuation -> continuation // strictly a continuation generator, as it produces a continuation from the output of the producer.
/// Operation: continuation -> continuation

/// Example -- Console I/O

let putc (c: char) (k: continuation) =
    let write () = System.Console.Write(c);
                   execute k
    K write
let getc (g: char -> continuation) =
    let read () = execute (g <| System.Console.ReadKey(true).KeyChar)
    K read;;

let (<|) f x y = (f x);;

let rec echo k =
    let echoChar c = if c = '\r'
                     then putc '\n' k
                     else putc c (echo k)
    getc echoChar;;


/// Example -- The Lexer revisited

// Define a result type of a lexeme list or error message:
type lexerResult<'lexeme> =
    Success of 'lexeme list
  | Failure of string;;

// Define a type with a character source, a 'lexeme sink and an error string sink:
type lexerSystem<'lexeme> = {
  input: unit -> char option;
  output: 'lexeme -> unit;
  error: string -> unit;
  result: unit -> lexerResult<'lexeme> };;

// Read the output result and continue with (g result)
let lexComplete s g =
    let complete () =
        g (s.result ())
    K complete;;

// Set the output to the failure state and continue with k
let lexFail s e k =
    let storeError () =
        s.error e
        execute k
    K storeError;;

// s is our lexerSystem
let lexGetChar s g =
       K (fun () -> execute (g (s.input())));;


let lexPutLexeme s lexeme (k: continuation) =
    let storeLexeme () =
        s.output lexeme;
        execute k
    K storeLexeme;;

// (^^) associates on the right, which means that we can chain instances without requiring nested brackets
let ( ^^ ) (test, g1) (g2: 'x -> continuation) x =
      (if test x then g1 else g2) x;;


let rec ( ^* ) (generator: ('x option -> continuation) -> continuation, test)
            (consumer: 'x list -> 'x option -> continuation) =
    generator (function Some x -> if test x
                                  then (generator, test) ^* (fun l -> consumer(x::l))
                                  else consumer [] (Some x)
                      | None -> consumer [] None);;

type LispLex = Bra | Ket | Dot | Str of string | Num of int | Symbol of string
let alpha c = ((c >= 'a') && (c <= 'z')) || ((c >= 'A') && (c <= 'Z'))
let digit c = (c >= '0') && (c <= '9')
let eqChar c c' = c = c'
let l2s l = string(List.toArray l)
let ( ^| ) p q = fun x -> (p x) || (q x)

// Read a symbol from the input
let readSymbol s char g =
     (lexGetChar s, alpha ^| digit ^| eqChar '_') ^*
     (fun l ->
             fun opt ->
                    lexPutLexeme s (Symbol (l2s ([char]@l))) (g opt));;
