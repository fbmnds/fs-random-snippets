open System.Net
open System.IO
open System.Threading
open Microsoft.FSharp.Control

//let fetchAsync (url : string) = async {
//    let req = WebRequest.Create url
//    use resp = req.GetResponse()
//    use stream = resp.GetResponseStream() 
//    let reader = new StreamReader(stream)
//    let html = reader.ReadToEnd()
//    return html }

let result = ref ""

let updateResult s =
    result := s

let fetchAsync (url: string) = async {
    let req = WebRequest.Create(url)
    let! resp = req.AsyncGetResponse()
    let stream = resp.GetResponseStream()
    let reader = new StreamReader(stream)
    let html = reader.ReadToEnd()
    updateResult html }

let getLength url = async {
    let! html = fetchAsync url
    do! Async.Sleep 1000
    return result.contents.Length }

let a = getLength "http://www.spiegel.de"

Async.RunSynchronously a