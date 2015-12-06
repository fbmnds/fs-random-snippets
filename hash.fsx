// http://fssnip.net/g1

open System
open System.IO
open System.Security.Cryptography

let hashTypes = [|"MD5"; "SHA1"; "SHA256"; "SHA384"; "SHA512"; "RIPEMD160"|] |> Array.map (fun t -> t, HashAlgorithm.Create(t))

for arg in fsi.CommandLineArgs |> Seq.skip 1 do
  let itm = Path.GetFullPath(arg)

  if File.Exists(itm) then
    if (new FileInfo(itm)).Length <> 0L then
      printfn "%s" itm
      let content = File.ReadAllBytes(itm)
      for (t,f) in hashTypes do
        let before = System.DateTime.Now.Ticks
        let bytes = content |> f.ComputeHash
        let after = System.DateTime.Now.Ticks
        //Console.WriteLine("[{0}] {1, 9}: {2}", System.DateTime.Now.ToString("dd.MM.yyyy hh:mm:ss.fff tt"), t, BitConverter.ToString(bytes).Replace("-", "").ToLower())
        printfn "[%d ms] %s %s" ((after-before)/1000L) t (BitConverter.ToString(bytes).Replace("-", "").ToLower())
    else
      printfn "File %s has null length." arg
  else
    printfn "File %s does not exist." arg
