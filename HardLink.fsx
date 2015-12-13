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
    /// Select and transform the files of a directory recursively.
    let rec GetFilesTransformed transform select dir =
        let files =  try Directory.GetFiles dir with | _ -> [||]
        let dirs = try Directory.GetDirectories dir with | _ -> [||]
        seq { for file in files do
                  if file |> select then yield (transform file)
              for sub in dirs do
                  yield! GetFilesTransformed transform select sub }


module Utils =
    /// Create hard link at dest with source.
    let CreateHardLink dest source =
        Kernel32.CreateHardLink(dest, source, IntPtr.Zero)
    
    /// Get all transformed files of a sequence of directories by extension.
    let GetFilesTransformed transform caseSensitive ext dirs =
        let select (file : string) = 
            if caseSensitive then 
                if file.EndsWith(ext) then true else false
            else
                if file.ToLowerInvariant().EndsWith(ext.ToLowerInvariant()) then true else false
        dirs |> Seq.collect (Directory.GetFilesTransformed transform select) |> Array.ofSeq

    /// Get FileInfo of all files of a sequence of directories by extension.
    let GetFileInfos caseSensitive ext dirs = 
        GetFilesTransformed (fun x -> new FileInfo(x)) caseSensitive ext dirs

    /// Get FullName of all files of a sequence of directories by extension.
    let GetFiles caseSensitive ext dirs = 
        GetFilesTransformed id caseSensitive ext dirs

    let hashTypes = 
        [|"MD5"; "SHA1"; "SHA256"; "SHA384"; "SHA512"; "RIPEMD160"|] 
        |> Array.map (fun t -> t, HashAlgorithm.Create(t))

    /// Get the array of variants of hashes for each given file.
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

    /// Get SHA1-hashes with label and elapsed calculation time for each given file.
    let GetFilesSHA1 verbose files = 
        files
        |> GetFilesHashedDiagnostic verbose [|"SHA1", HashAlgorithm.Create("SHA1")|] 
        |> Array.map (fun (h,f) -> 
            let h_ms, h_t, h_hash = h.[0]
            (h_ms, h_t, h_hash, f))

    /// Get SHA1-hashes with label and elapsed calculation time for the given file.
    let GetFileSHA1 verbose file = [|file|] |> GetFilesSHA1 verbose

    /// Get SHA1-grouping of the given files.
    let GetFilesGroupedBySHA1 verbose files = 
        files
        |> GetFilesSHA1 verbose 
        |> Seq.ofArray 
        |> Seq.groupBy (fun (_,_,h,_) -> h) 
        
    /// Coerce a sequence of files using hard links.
    let CoerceFilesIntoHardLinks verbose (files : seq<string>) = 
        let pDel h msg = if verbose then printfn "Delete error %s\n%s" h msg
        let pHL h msg = if verbose then printfn "Hard link error %s\n%s" h msg
        let rec coerceFilesIntoHardLinks verbose (files : seq<string>) = 
            if files |> Seq.isEmpty then ()
            else 
                let head = files |> Seq.head
                let tail = files |> Seq.skip 1
                if tail |> Seq.isEmpty then ()
                else
                    let head2 = Seq.head tail
                    if (try File.Delete(head2); true with | _ as ex -> pDel head2 ex.Message; false) then
                        (try CreateHardLink head2 head with | _ as ex -> pHL head2 ex.Message; false) |> ignore
                    let tail2 = tail |> Seq.skip 1
                    if tail2 |> Seq.isEmpty then ()
                    else coerceFilesIntoHardLinks verbose (seq { yield head; yield! tail2 })
        files |> Array.ofSeq 
        |> GetFilesGroupedBySHA1 verbose
        |> Seq.map (fun (_,x) -> (x |> Seq.map (fun (_,_,_,y) -> y))) 
        |> Seq.iter (coerceFilesIntoHardLinks verbose)

module Tests =
    let ``Kernel32.Create hard link``() =
        Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
        try File.Delete(@"data\Example2.xlsx") with | _ as ex -> ignore ex 
        if (Kernel32.CreateHardLink(
                    @"data\Example2.xlsx", 
                    @"data\Example1.xlsx", 
                    IntPtr.Zero))
        then printfn "Basic test success"
        else failwith "Basic test failure" |> ignore

    let mutable zeroSHA1 = 0L
    let mutable msSHA1 = 0L
    let ``Utils.GetFileSHA1`` files = 
        let hashedFilesSHA1 files = 
            zeroSHA1 <- System.DateTime.Now.Ticks
            let res = files |> Utils.GetFilesSHA1 true
            msSHA1 <- (System.DateTime.Now.Ticks - zeroSHA1) / 1000L
            res
        files |> hashedFilesSHA1            
     
    let Run() =
        ``Kernel32.Create hard link``()
        
        // using the COPY "projects2" of "$PROJECTS=projects"
        let dirs = 
            [| sprintf "%s2" (Environment.GetEnvironmentVariable("PROJECTS")) |]
               //Environment.GetEnvironmentVariable("HOMEPATH") |]
        
        let files = dirs |> Utils.GetFiles false "dll"
        files |> Array.sortInPlace

        printfn "Utils.GetFiles: %d in directory %A" (files |> Array.length) dirs

        files.[0..9] |> Utils.GetFilesHashedDiagnostic true Utils.hashTypes |> Array.iter (printfn "%A")
        
        let hashedFilesSHA1 = files |> Utils.GetFilesSHA1 false
        printfn "[SHA1] hashed %d files in %d ms" hashedFilesSHA1.Length msSHA1
        hashedFilesSHA1.[0..9] |> Array.iter (printfn "[SHA1] %A")
         
        let hashedMultiFilesSHA1 = Utils.GetFilesGroupedBySHA1 false files
        hashedMultiFilesSHA1 |> Seq.take 10 |> Seq.iter (fun x -> printfn "file group by hash: %s\n%A" (fst x) (snd x))

        hashedMultiFilesSHA1 |> Seq.filter (fun (_,x) -> x |> Seq.length > 1) |> Seq.length
        |> printfn "[SHA1] %d multiple files grouped by hash"

        hashedMultiFilesSHA1 |> Seq.filter (fun (_,x) -> x |> Seq.length > 1) 
        |> Seq.map (fun (_,x) -> x |> Seq.length) |> fun x -> (x |> Seq.min), (x |> Seq.max) 
        |> printfn "[SHA1] %A min/max of multiple file counts"
        
        files |> Utils.CoerceFilesIntoHardLinks true

        let files2 = dirs |> Utils.GetFiles false "dll" 
        files2 |> Array.sortInPlace

        let getSHA1 files = files |> Utils.GetFilesSHA1 false |> Array.map (fun (_,_,h,f) -> h,f)
        if ((files |> getSHA1) <> (files2 |> getSHA1)) then printfn "Utils.CoerceFilesIntoHardLinks failure" 
        else printfn "Utils.CoerceFilesIntoHardLinks success"

        let files3 = 
            dirs.[0] 
            |> Directory.GetFilesTransformed id (fun _-> true) 
            |> Array.ofSeq
            |> Utils.GetFilesGroupedBySHA1 false
            |> Seq.map (fun (x,y) -> x, y|> Seq.map (fun (_,_,_,z) -> z))
            |> Seq.map (fun (x,y) -> x, (new FileInfo (y |> Seq.head)).Length, y)
            |> Seq.sortBy (fun (_,size,_) -> -1L*size)

        files, files2, files3

    