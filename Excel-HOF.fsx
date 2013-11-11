//
// Source: Kit Eason,
//         http://fssnip.net/aV
//         "Higher-Order Functions for Excel"
//
// Office interop may need http://www.microsoft.com/en-us/download/details.aspx?id=3508,
// check C:\Windows\assembly for already installed interop assemblies.
//
// Source adjusted to get it run in my environment.
// @fbmnds


// Add references to Excel interop:
#if INTERACTIVE
#r "Microsoft.Office.Interop.Excel"
#endif


open System
open Microsoft.Office.Interop.Excel


let CWD = "C:\\Users\\" + Environment.UserName + "\\projects\\Excel-HOF"


/// Helper function to represent string or floating point cell content as a string.
let cellContent (range : Range) =
    match range.Value2 with
    | :? string as _string -> sprintf "string: %s" _string
    | :? double as _double -> sprintf "double: %f" _double
    | _ -> "(unknown type)"

/// Helper function to return cell content as float if possible, if not as 0.0.
let cellDouble (range : Range) =
    match range.Value2 with
    | :? double as _double -> _double
    | _ -> 0.0

/// Returns the specified worksheet range as a sequence of indvidual cell ranges.
let toSeq (range : Range) =
    seq {
            for r in 1 .. range.Rows.Count do
                for c in 1 .. range.Columns.Count do
                    let cell = range.Item(r, c) :?> Range
                    yield cell
    }

/// Returns the specified worksheet range as a sequence of indvidual cell ranges, together with a 0-based
/// row-index and column-index for each cell.
let toSeqrc (range : Range) =
    seq {
            for r in 1 .. range.Rows.Count do
                for c in 1 .. range.Columns.Count do
                    let cell = range.Item(r, c) :?> Range
                    yield r, c, cell
    }

/// Takes a sequence of individual cell-ranges and returns an Excel range representation of the cells
/// (using Excel 'union' representation - eg. "R1C1, R2C1, R5C4").
let toRangeAsString (workSheet : Worksheet) (rangeSeq : seq<Range>) =
    let csvSeq sequence =
        let result =
            sequence
            |> Seq.fold (fun acc x -> acc + x + ",") ""
        result.Remove(result.Length-1)
    let rangeName =
        rangeSeq
        |> Seq.map (fun cell -> cell.Address())
        |> csvSeq
    //workSheet.Range(rangeName)
    rangeName

let toRange (workSheet : Worksheet) (rangeSeq : seq<Range>) =
    let rangeName =
        rangeSeq
        |> Seq.map (fun cell -> cell.Address().ToString())
    //workSheet.Range(rangeName)
    workSheet.Range( (Seq.head rangeName), (Seq.last rangeName))


/// Takes a function and an Excel range, and returns the results of applying the function to each individual cell.
let map (f : Range -> 'T) (range : Range) =
    range
    |> toSeq
    |> Seq.map f

/// Takes a function and an Excel range, and returns the results of applying the function to each individual cell,
/// providing 0-based row-index and column-index for each cell as arguments to the function.
let maprc (f : int -> int -> Range -> 'T) (range : Range) =
    range
    |> toSeqrc
    |> Seq.map (fun item -> match item with
                            | (r, c, cell) -> f r c cell)

/// Takes a function and an Excel range, and applies the function to each individual cell.
let iter (f : Range -> unit) (range : Range) =
    range
    |> toSeq
    |> Seq.iter (fun cell -> f cell)

/// Takes a function and an Excel range, and applies the function to each individual cell,
/// providing 0-based row-index and column-index for each cell as arguments to the function.
let iterrc (f : int -> int -> Range -> unit) (range : Range) =
    range
    |> toSeqrc
    |> Seq.iter (fun item -> match item with
                                | (r, c, cell) -> f r c cell)

/// Takes a function and an Excel range, and returns a sequence of individual cell ranges where the result
/// of applying the function to the cell is true.
let filter (f : Range -> bool) (range : Range) =
    range
    |> toSeq
    |> Seq.filter (fun cell -> f cell)

///// Examples /////

// Start Excel.
let excel = ApplicationClass(Visible = true)

// Open a workbook:
let workbookDir = CWD + "\\data"
let workbook = excel.Workbooks.Open(workbookDir + "\\Example1.xlsx")

// Get a reference to the workbook:
let exampleSheet = workbook.Sheets.["ExampleSheet"] :?> Worksheet

// Get a reference to a named range:
let exampleRange = exampleSheet.Range("MyRange")
printfn "MyRange: %s" (exampleRange.Address())

let smallRange = exampleSheet.Range("SmallRange")
printfn "SmallRange: %s" (smallRange.Address())


// toSeq example:
let cellCount range =
    range
    |> toSeq
    |> Seq.length
// 4

cellCount exampleRange

// toSeqrc example:
let listCellRC =
    exampleRange
    |> toSeqrc
    |> Seq.iter (fun item -> match item with
                             | (r, c, cell) -> printfn "row:%i col:%i cell:%s" r c (cellContent cell))
// row:1 col:1 cell:string: A
// row:2 col:1 cell:double: 1.000000
// row:3 col:1 cell:double: 2.000000
// row:4 col:1 cell:double: 3.000000
// ...
// row:4 col:3 cell:double: 9.000000

// toRange example:
let rangeAddress =
    let range =
        smallRange
        |> toSeq
        |> toRangeAsString exampleSheet
    //printfn "Range: %s" (range.Address())
    printfn "Range: %s" range
// Range: $A$1,$B$1,$C$1,$A$2,$B$2,$C$2,$A$3,$B$3,$C$3,$A$4,$B$4,$C$4

// map example:
let floatTotal =
    exampleRange
    |> map (fun cell -> cellDouble cell)
    |> Seq.sum
// 42.0

// maprc example:
let evenTotal =
    exampleRange
    |> maprc (fun r _ cell -> if r % 2 = 0 then
                                  cellDouble cell
                              else
                                  0.0)
    |> Seq.sum
// 28.0


// iterrc example
let chequerRange range =
    range
    |> iterrc (fun r c cell -> if    (r % 2 = 0) && (c % 2 <> 0)
                                    || (r % 2 <> 0) && (c % 2 = 0) then
                                    cell.Interior.Color <- 65535 // Yellow
                                else
                                    cell.Interior.Color <- 255) // Red
// Range is fetchingly chequered in red and yellow
exampleRange |> chequerRange

// iter example
let highlightRange () =
    exampleRange
    |> iter (fun cell -> cell.Interior.Color <- 65535) // Yellow
// Entire range is yellow

// toRange example
// Range is again fetchingly chequered in red and yellow
exampleRange |> toSeq |> toRange exampleSheet |> chequerRange

// filter and toRange example:
let colourOddInts =
    let oddIntRange =
        exampleRange
        |> filter (fun cell -> let cellVal = cellDouble cell
                               (cellVal = float(int(cellVal)))
                               && (int(cellVal)) % 2 <> 0)
        |> toRange exampleSheet
    oddIntRange.Interior.Color <- 255 // Red
// WRONG : Cells containing odd integers are coloured red; other colours are unchanged


(smallRange |> toSeq |> Seq.head).Address()

smallRange |> toSeq |> toRange exampleSheet |> cellContent

exampleSheet.Range("$D$27","$F$29") |> cellContent

exampleRange |> toSeq |> toRange exampleSheet |> cellCount
exampleRange |> cellCount

highlightRange()
