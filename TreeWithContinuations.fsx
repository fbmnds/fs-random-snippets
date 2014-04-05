
type IntTree =
    | Leaf of int
    | Node of IntTree * IntTree


let tree = Node (Node (Node ( Leaf (1), Leaf(2)), Leaf(3)), Node ( Leaf(4), Leaf(5) ) )

let rec sumTreeCont tree cont = 
    match tree with
    | Leaf num -> cont (num)
    | Node (left, right) -> 
        sumTreeCont left (fun leftSum ->
            sumTreeCont right (fun rightSum -> cont (leftSum+rightSum) ) )

let rec printTree tree =
    match tree with
    | Leaf num -> printfn "%i" num
    | Node (left, right) -> printfn "*"; printfn "left:"; printTree left; printfn "right:"; printTree right 

printTree tree

//    *
//    left:
//    *
//    left:
//    *
//    left:
//    1
//    right:
//    2
//    right:
//    3
//    right:
//    *
//    left:
//    4
//    right:
//    5
//    val it : unit = ()

//                *
//         *             *
//     *       3     4       5
// 1       2

let rec sumTreeCont2 tree cont = 
    match tree with
    | Leaf num -> printfn "Leaf %i" num; cont (num)
    | Node (left, right) -> 
        printfn "left %A" left; sumTreeCont2 left (fun leftSum ->
            printfn "leftSum %i right %A" leftSum right; sumTreeCont2 right (fun rightSum -> printfn "rightSum %i; cont leftSum+rightSum %i" rightSum (cont (leftSum+rightSum)); cont (leftSum+rightSum) ) )

sumTreeCont2 tree id

//    left Node (Node (Leaf 1,Leaf 2),Leaf 3)
//    left Node (Leaf 1,Leaf 2)
//    left Leaf 1
//    Leaf 1
//    leftSum 1 right Leaf 2
//    Leaf 2
//    leftSum 3 right Leaf 3
//    Leaf 3
//    leftSum 6 right Node (Leaf 4,Leaf 5)
//    left Leaf 4
//    Leaf 4
//    leftSum 4 right Leaf 5
//    Leaf 5
//    rightSum 9; cont leftSum+rightSum 15
//    rightSum 5; cont leftSum+rightSum 15
//    rightSum 9; cont leftSum+rightSum 15
//    rightSum 3; cont leftSum+rightSum 15
//    leftSum 6 right Node (Leaf 4,Leaf 5)
//    left Leaf 4
//    Leaf 4
//    leftSum 4 right Leaf 5
//    Leaf 5
//    rightSum 9; cont leftSum+rightSum 15
//    rightSum 5; cont leftSum+rightSum 15
//    rightSum 9; cont leftSum+rightSum 15
//    rightSum 2; cont leftSum+rightSum 15
//    leftSum 3 right Leaf 3
//    Leaf 3
//    leftSum 6 right Node (Leaf 4,Leaf 5)
//    left Leaf 4
//    Leaf 4
//    leftSum 4 right Leaf 5
//    Leaf 5
//    rightSum 9; cont leftSum+rightSum 15
//    rightSum 5; cont leftSum+rightSum 15
//    rightSum 9; cont leftSum+rightSum 15
//    rightSum 3; cont leftSum+rightSum 15
//    leftSum 6 right Node (Leaf 4,Leaf 5)
//    left Leaf 4
//    Leaf 4
//    leftSum 4 right Leaf 5
//    Leaf 5
//    rightSum 9; cont leftSum+rightSum 15
//    rightSum 5; cont leftSum+rightSum 15
//    rightSum 9; cont leftSum+rightSum 15
//    val it : int = 15


type T = int


type Arg =
    | Leaf of T
    | Node of T*T


let c (x: Arg) (f1: T -> Arg) (f2: T -> T -> Arg) =
    let res = 
        match x with
        | Leaf x -> f1 x
        | Node (x,y) -> f2 x y
    res

let tree2 : Arg = Node (Node (Node ( Leaf (1), Leaf(2)), Leaf(3)), Node ( Leaf(4), Leaf(5) ) )
let res = c tree2 (fun x -> Arg.Leaf x) (fun Arg.Node (x,y) -> x + y)