
#r @"FSharp.Compiler.Service.1.4.2.3\lib\net45\FSharp.Compiler.Service.dll"

open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.FSharp.Compiler.Interactive.Shell


open System
open System.IO
open System.Text

// Intialize output and input streams
let sbOut = new StringBuilder()
let sbErr = new StringBuilder()
let inStream = new StringReader("")
let outStream = new StringWriter(sbOut)
let errStream = new StringWriter(sbErr)

// Build command line arguments & start FSI session
let argv = [| @"C:\Program Files (x86)\Microsoft SDKs\F#\4.0\Framework\v4.0\Fsi.exe" |]
let allArgs = Array.append argv [|"--noninteractive"|]

let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
let fsiSession = FsiEvaluationSession.Create(fsiConfig, allArgs, inStream, outStream, errStream) 

/// Evaluate expression & return the result
let evalExpression text =
  match fsiSession.EvalExpression(text) with
  | Some value -> printfn "%A" value.ReflectionValue
  | None -> printfn "Got no result!"


/// Evaluate interaction & ignore the result
let evalInteraction text = 
  fsiSession.EvalInteraction(text)

/// Evaluate script & ignore the result
let evalScript scriptPath = 
  fsiSession.EvalScript(scriptPath)

//evalScript "SharpGLTemplate.fsx" 

let mutable forever = true
while forever do
    Console.Write " > "
    let input = Console.ReadLine()
    printfn "input: %s" input
    if input = "#q;;" then forever <- false else input |> evalExpression
