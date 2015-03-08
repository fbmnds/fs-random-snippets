(**
#[Windows Live space](http://clivetong.wordpress.com/)

##[I can live within those constraints](http://clivetong.wordpress.com/2010/12/04/i-can-live-within-those-constraints/)
Posted on December 4, 2010	by Clive Tong

I've just spent a little time reading the [F# specification](http://research.microsoft.com/en-us/um/cambridge/projects/fsharp/manual/spec.pdf).

I must confess that I hadn't come across the upcast and downcast functions before, which can be used to change the type associated with a value.
*)
(*** define-output: x ***)
let x : obj = upcast "hello";;
(*** hide ***)
(*** define-output: y ***)
let y : string = downcast x;;


(**
My main reason for reading the specification was to get a better mental model of the type system that the language uses. In the past I was part of a team that implemented an ML compiler. This used a variant of the Hindley-Milner type checking algorithm. In contrast, the F# system type checks types using constraint solving, and there are a lot more types of constraint that need to be checked.
*)
type First() = class end;;
type Second() = class inherit First() end;;
(*** define-output:f ***)
let f x : ^T when ^T :> First = x;;


(**
The `:>` constraint demands a subtype relationship between the argument and the target type.
*)
(*** define-output:fFirst ***)
f(First());;

(*** define-output:fSecond ***)
f(Second());;
(**
and has a short form using the hash symbol.
*)

(*** hide ***)
[module=two]
type First() = class end;;
(*** define-output:fhashFirst ***)
let f x : #First = x;;

(**
The member constraint can be used to ensure that a type supports a certain operation. It is used in conjunction with type variables that cannot be generalised and which are written in the form `^T`.
*)
(*** define-output:fMemberConstraint ***)
[module=three]
let f x : ^a = x;;


(**
Because of this lack of generalisation, the `^` types are most often used in the context of inline functions.
*)
(*** define-output:finline ***)
let inline doit (x: ^a) = (^a : (static member speak: int -> string) (20));;


(**
The previous function ensures that the argument is of a type that has a static member called `speak`.
*)
type Boo() = static member speak x = "Called on " + x.ToString();;
(*** define-output:Boo ***)
doit(Boo());;


(**
It took me quite a while to figure out how to call a non-static member, mainly because I failed to notice that a tuple was required to apply the member.
*)
(*** define-output: doit ***)
let inline doit (x: ^a) = (^a : (member speak: int -> string) (x,20));;

type Test3() = class member x.speak(y) = "Hello" end;;
(*** define-output: doitTest3 ***)
doit(Test3());;

