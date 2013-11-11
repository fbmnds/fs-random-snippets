open System
open System.IO


let (|FileInfoNameSections|) (f:FileInfo) =
             (f.Name,f.Extension,f.FullName)


let matchGz (f:FileInfo) =
   match f with
      | FileInfoNameSections(_,".gz",fn) -> Some fn
      | _ -> None

matchGz (new FileInfo @"C:\Users\boe\.emacs.d")
matchGz (new FileInfo @"C:\Users\boe\.emacs.gz")


let rec allFiles dir =
  [ for file in Directory.GetFiles dir do
      match (matchGz (new FileInfo (file))) with
      | Some text -> yield text
      | _ -> ignore "null"
    for sub in Directory.GetDirectories dir do
      yield! allFiles sub ]

allFiles @"C:\Users\boe\.emacs.d"
// allFiles @"C:\Users\boe\.emacs.d" |> List.map File.Delete


let fileDelete (fn : string) =
  try
    File.Delete fn
  with
    | _ -> ignore "exception"

allFiles @"C:\Users\boe\.emacs.d" |> List.map fileDelete ;;




// let (|FileInfoExtension|) (f:FileInfo) = f.FullName
//
// let f =  (new FileInfo("ActivePatternsTests.exe")) in
//   match f with
//     | FileInfoExtension n ->
//       Console.WriteLine("File name: {0}",n)
