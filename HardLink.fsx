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


module Directory = 
    /// get all transformed files of a directory by extension
    let rec GetFilesTransformed transform caseSensitive ext dir =
        let files =  try Directory.GetFiles dir with | _ -> [||]
        let dirs = try Directory.GetDirectories dir with | _ -> [||]
        seq { for file in files do
                  if caseSensitive then 
                      if file.EndsWith(ext) then 
                          yield (transform file)
                  else
                      if file.ToLowerInvariant().EndsWith(ext.ToLowerInvariant()) then 
                          yield (transform file)
              for sub in dirs do
                  yield! GetFilesTransformed transform caseSensitive ext sub }


module Utils =
    /// Create hard link at dest with source
    let CreateHardLink dest source =
        Kernel32.CreateHardLink(dest, source, IntPtr.Zero)
    
    /// get all transformed files of a sequence of directories by extension
    let GetFilesTransformed transform caseSensitive ext dirs =
        dirs |> Seq.collect (Directory.GetFilesTransformed transform caseSensitive ext) |> Array.ofSeq

    /// get FileInfo of all files of a sequence of directories by extension
    let GetFileInfos caseSensitive ext dirs = 
        GetFilesTransformed (fun x -> new FileInfo(x)) caseSensitive ext dirs

    /// get FullName of all files of a sequence of directories by extension
    let GetFiles caseSensitive ext dirs = 
        GetFilesTransformed id caseSensitive ext dirs

    let hashTypes = 
        [|"MD5"; "SHA1"; "SHA256"; "SHA384"; "SHA512"; "RIPEMD160"|] 
        |> Array.map (fun t -> t, HashAlgorithm.Create(t))

    /// get array of variants of hashes for all given files
    let GetFilesHashedDiagnostic verbose (hashTypes: (string*HashAlgorithm)[]) (files : string[]) =
        let hashedFile file =       
            try
                let content = File.ReadAllBytes(file)
                let hashes =
                    [| for (t,f) in hashTypes do
                        let zero = System.DateTime.Now.Ticks
                        let bytes = content |> f.ComputeHash
                        let ms = (System.DateTime.Now.Ticks - zero) / 1000L
                        let hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant()
                        if verbose then printfn "[%6d ms] %s %s" ms t hash
                        yield ms, t, hash |]
                (hashes, file)
                |> Some
            with _ -> None
        files |> Array.map hashedFile |> Array.choose id

    /// get SHA1-hashes with label and elapsed calculation time for all given files
    let GetFileSHA1 verbose files = 
        files
        |> GetFilesHashedDiagnostic verbose [|"SHA1", HashAlgorithm.Create("SHA1")|] 
        |> Array.map (fun (h,f) -> 
            let h_ms, h_t, h_hash = h.[0]
            (h_ms, h_t, h_hash, f))

    /// get SHA1-grouping of the given files
    let GetFilesGroupedBySHA1 verbose files = 
        files
        |> GetFileSHA1 verbose 
        |> Seq.ofArray 
        |> Seq.groupBy (fun (_,_,h,_) -> h) 
        
    /// coerce files into hard links
    let rec CoerceFilesIntoHardLinks (files : seq<string>) = 
        let head = files |> Seq.head
        let tail = files |> Seq.skip 1
        let head2 = Seq.head tail
        if (try File.Delete(head2); true with | _ as ex -> false) then
            (try CreateHardLink head2 head with | _ as ex -> false) |> ignore
        else 
            CoerceFilesIntoHardLinks (seq { yield head; yield! (tail |> Seq.skip 1) })


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

    let files = 
        [|Environment.GetEnvironmentVariable("PROJECTS")
          Environment.GetEnvironmentVariable("HOMEPATH")|]
        |> fun x -> Utils.GetFiles false "dll" x
    let ``Utils.GetFile``() = files |> Array.length

    let hashedFilesDiagnostic = files.[0..90] |> Utils.GetFilesHashedDiagnostic true Utils.hashTypes
    let ``Utils.GetFileHashedDiagnostic``() =
        hashedFilesDiagnostic

    let zeroSHA1 = System.DateTime.Now.Ticks
    let hashedFilesSHA1 = files |> Utils.GetFileSHA1 true
    let msSHA1 = (System.DateTime.Now.Ticks - zeroSHA1) / 1000L
    let ``Utils.GetFileSHA1``() =
        hashedFilesSHA1        
    
    let hashedMultiFilesSHA1 = files |> Utils.GetFilesGroupedBySHA1 false
    let ``Utils.GetFilesGroupedBySHA1``() =
        hashedMultiFilesSHA1  

    let Run() =
        ``Kernel32.Create hard link``()
        ``Utils.GetFile``() |> printfn "%d"
        ``Utils.GetFileHashedDiagnostic``() |> printfn "%A"
        ``Utils.GetFileSHA1``()  |> printfn "%A"
        printfn "[SHA1] hashed %d files in %d ms" hashedFilesSHA1.Length msSHA1
        ``Utils.GetFilesGroupedBySHA1``() |> printfn "%A"
        printfn "[SHA1] %d multiple files grouped by hash" (hashedMultiFilesSHA1 |> Seq.filter (fun (_,x) -> x |> Seq.length > 1) |> Seq.length)
        printfn "[SHA1] %A min/max of multiple file counts" (hashedMultiFilesSHA1 |> Seq.filter (fun (_,x) -> x |> Seq.length > 1) |> Seq.map (fun (_,x) -> x |> Seq.length) |> fun x -> (x |> Seq.min), (x |> Seq.max))

