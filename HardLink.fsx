open System
open System.IO
open System.Runtime.InteropServices

module InteropWithNative =

  [<DllImport("Kernel32.dll", CharSet = CharSet.Unicode )>]
  extern bool CreateHardLink(
    string lpFileName,
    string lpExistingFileName,    
    IntPtr lpSecurityAttributes)


try File.Delete(@"data\Example2.xlsx") with | _ as ex -> ignore ex 
if (InteropWithNative.CreateHardLink(
            @"data\Example2.xlsx", 
            @"data\Example1.xlsx", 
            IntPtr.Zero))
then printf("success")
else printf("failure")
