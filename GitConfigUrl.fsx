/// Switch the Git configuration of all own projects in the user´s master directory from http[s] to git access

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

    
/// get all .git/config files
let rec allGitConfigFiles dir =
    try 
        match (matchGitDir (new DirectoryInfo (dir))) with
        | Some text -> [ for sub in Directory.GetDirectories dir do
                            yield! allGitConfigFiles sub ]
        | _ -> [ for file in Directory.GetFiles dir do
                    match (matchGitConfig (new FileInfo (file))) with
                    | Some text -> yield text
                    | _ -> ignore "null"
                 for sub in Directory.GetDirectories dir do
                    yield! allGitConfigFiles sub ]
    with | _ -> []


/// match, if the user´s repo is addressed by a http[s] url 
let (|GitUrl|) gitUser s =
    (|CompiledMatch|_|) (@"(?<spaces>\s*)url\s+=\s+http[s]{0,1}://github.com/" + gitUser + "/(?<repo>.*)") s


/// return a tuple of fullname and modified config
let modifyGitUrl gitUser (fullname:string) = 
    async { 
        /// read the file in one go in order to keep the file line sequence
        return (fullname, 
            try
                [ for c in (File.ReadLines fullname) do
                            match ((|GitUrl|) gitUser c) with
                            | Some [_;spaces;repo] -> 
                                yield spaces.Value + "url = git@github.com:" + gitUser + "/" + repo.Value + ".git"
                            | _ -> yield c ] 
            with | _ -> []) } 


/// write a modified config to disk using fullname
let writeGitUrl ((fullname:string), (config: string list)) =
    async { 
        try
            use outStream = new StreamWriter(fullname)
            /// write the file in one go in order to keep the file line sequence
            for c in config do outStream.WriteLine c
            outStream.Close()
        with | ex -> ignore ex
     }


///
Environment.SetEnvironmentVariable("PROJECTS", @"D:\projects")
let projectDir = Environment.GetEnvironmentVariable("PROJECTS")
let gitUser = "fbmnds"

/// pipe the config files through the task chain in 2 sequential steps:
projectDir |> allGitConfigFiles 
/// 1. pump all config files into main memory, modify them
|> List.map (modifyGitUrl gitUser) |> Async.Parallel |> Async.RunSynchronously 
/// 2. push the modified config files to disk
|> Array.map writeGitUrl |> Async.Parallel |> Async.RunSynchronously
 
