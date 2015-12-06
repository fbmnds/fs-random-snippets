/// Coerce recursively in a directory all unique DLL files to hard links 

open System
open System.IO
open System.Runtime.InteropServices
open System.Text.RegularExpressions
open System.Security.Cryptography


module Kernel32 =

  [<DllImport("Kernel32.dll", CharSet = CharSet.Unicode )>]
  extern bool CreateHardLink(
    string lpFileName,
    string lpExistingFileName,    
    IntPtr lpSecurityAttributes)

module Utils =
    /// Create hard link at dest with source
    let CreateHardLink dest source =
        Kernel32.CreateHardLink(dest, source, IntPtr.Zero)

    /// get all FileInfo for ext in dir
    let GetFileInfo caseSensitive ext dirs =
        let getFiles dir =  try Directory.GetFiles dir with | _ -> [||]
        let getDirectories dir = try Directory.GetDirectories dir with | _ -> [||]
        let rec getFileInfo caseSensitive ext dir =
            seq{ for file in getFiles dir do
                     if caseSensitive then 
                         if file.EndsWith(ext) then 
                             yield new FileInfo(file)
                     else
                         if file.ToLowerInvariant().EndsWith("dll") then 
                             yield new FileInfo(file)
                 for sub in getDirectories dir do
                     yield! getFileInfo caseSensitive ext sub }
        dirs |> Seq.collect (getFileInfo caseSensitive ext) |> Array.ofSeq 

    let hashTypes = 
        [|"MD5"; "SHA1"; "SHA256"; "SHA384"; "SHA512"; "RIPEMD160"|] 
        |> Array.map (fun t -> t, HashAlgorithm.Create(t))

    let HashFilesDiagnose verbose (hashTypes: (string*HashAlgorithm)[]) (fileInfos : FileInfo[]) =
        let hashFile (fileInfo : FileInfo) =   
            let hash =
                if fileInfo.Length = 0L then None
                else    
                    try
                        let content = File.ReadAllBytes(fileInfo.FullName)
                        [| for (t,f) in hashTypes do
                            let zero = System.DateTime.Now.Ticks
                            let bytes = content |> f.ComputeHash
                            let ms = (System.DateTime.Now.Ticks - zero) / 1000L
                            let h = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant()
                            if verbose then printfn "[%6d ms] %s %s" ms t h
                            yield ms, t, h |]
                        |> Some
                    with _ -> None
            match hash with
            | Some hash -> Some(hash,fileInfo)
            | _ -> None
        fileInfos |> Array.map hashFile |> Array.choose (id)


    let HashFilesSHA1 verbose (fileInfos : FileInfo[]) = 
        HashFilesDiagnose verbose [|"SHA1",HashAlgorithm.Create("SHA1")|] fileInfos


module Tests =
    let ``Kernel32.Create hard link``() =
        Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
        try File.Delete(@"data\Example2.xlsx") with | _ as ex -> ignore ex 
        if (Kernel32.CreateHardLink(
                    @"data\Example2.xlsx", 
                    @"data\Example1.xlsx", 
                    IntPtr.Zero))
        then printfn("success")
        else printfn("failure")

    let fileInfos = 
        [|Environment.GetEnvironmentVariable("PROJECTS")
          Environment.GetEnvironmentVariable("HOMEPATH")|]
        |> fun x -> Utils.GetFileInfo false "dll" x
    let ``Utils.GetFileInfo``() =
        fileInfos
        |> Array.length

    let hashedFilesDiagnose =
        fileInfos.[0..90]
        |> Utils.HashFilesDiagnose true Utils.hashTypes
    let ``Utils.HashFilesDiagnose``() =
        hashedFilesDiagnose

    let zeroSHA1 = System.DateTime.Now.Ticks
    let hashedFilesSHA1 =
        fileInfos
        |> Utils.HashFilesSHA1 true
    let msSHA1 = (System.DateTime.Now.Ticks - zeroSHA1) / 1000L
    let ``Utils.HashFilesSHA1``() =
        hashedFilesSHA1        

    let Run() =
        ``Kernel32.Create hard link``()
        ``Utils.GetFileInfo``() |> printfn "%d"
        ``Utils.HashFilesDiagnose``() |> printfn "%A"
        ``Utils.HashFilesSHA1``()  |> printfn "%A"
        printfn "[SHA1] hashed %d files in %d ms" hashedFilesSHA1.Length msSHA1