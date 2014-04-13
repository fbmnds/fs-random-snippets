open System
open System.IO
open System.Text.RegularExpressions

#r "FSharp.PowerPack.dll"

let (|NameSections|) (f: FileSystemInfo) =
    (f.Name,f.Extension,f.FullName)

let matchGitDir (f: DirectoryInfo) =
   match f with
      | NameSections(".git","",fn) -> Some fn
      | _ -> None

let matchGitConfig (f: FileInfo) =
   match f with
      | NameSections("config","",fn) -> Some fn
      | _ -> None

/// http://stackoverflow.com/questions/5684014/f-mapping-regular-expression-matches-with-active-patterns
///Match the pattern using a cached compiled Regex
let (|CompiledMatch|_|) pattern input =
    if input = null then None
    else
        let m = Regex.Match(input, pattern, RegexOptions.Compiled)
        if m.Success then Some [for x in m.Groups -> x]
        else None

let (|GitUrl|) (s: string) = (|CompiledMatch|_|) @"(?<spaces>\s*)url\s+=\s+http[s]{0,1}://github.com/fbmnds/(?<repo>.*)" s
    

let rec allFiles dir =
    try 
        match (matchGitDir (new DirectoryInfo (dir))) with
        | Some text -> [ for sub in Directory.GetDirectories dir do
                            yield! allFiles sub ]
        | _ -> [ for file in Directory.GetFiles dir do
                    match (matchGitConfig (new FileInfo (file))) with
                    | Some text -> yield text
                    | _ -> ignore "null"
                 for sub in Directory.GetDirectories dir do
                    yield! allFiles sub ]
    with | _ -> []

// Environment.SetEnvironmentVariable("PROJECTS", @"D:\projects")
let gitdir = Environment.GetEnvironmentVariable("PROJECTS")

let gitConfigFiles = allFiles gitdir

let setGitUrl (fullname:string) =
    try   
        let out = [ for c in (File.ReadLines fullname) do
                    printfn "%A" ((|GitUrl|) c)
                    match ((|GitUrl|) c) with
                    | Some [_;spaces;repo] -> 
                        yield spaces.Value + "url = git@github.com:fbmnds/" + repo.Value + ".git"
                    | _ -> yield c ]
        //Async.RunSynchronously( out )
        printfn "%A" out
        use outStream = new StreamWriter(fullname+"-copied.txt")
        out |> outStream.WriteLine |> ignore
        outStream.Close() 
    with | ex -> ignore ex

setGitUrl gitConfigFiles.Head
setGitUrl @"D:\projects\dfa\.git\config"