/// Real World Functional Programming, T. Petricek, J. Skeet
/// 13.3 "Exploring and obtaining the data"

/// updates on the World Bank data API
/// some minor error corrections

open System.Web
open System.IO
open System.Net
#r "FSharp.PowerPack.dll"

let downloadUrl (url: string) = async { 
    let request = HttpWebRequest.Create (url)
    let! response = request.AsyncGetResponse ()
    use response = response
    let stream = response.GetResponseStream ()
    use reader = new StreamReader (stream)
    return!  reader.AsyncReadToEnd() (* method extension via FSharp.PowerPack *) }

let worldBankUrl (functions, props) =
    seq { yield "http://api.worldbank.org"
          for item in functions do
              yield "/" + HttpUtility.UrlEncode(item: string)
          yield "?per_page=3" 
          for key, value in props do
              yield "&" + key + "=" + HttpUtility.UrlEncode(value: string) }
    |> String.concat ""


let props = (["countries"],["region", "LCN"])

let url = worldBankUrl props

let worldBankDownload (properties) =
    let url = worldBankUrl (properties)
    let rec loop (attempts) = async {
        try
            return! downloadUrl (url)
        with _ when attempts > 0 ->
            printf "Failed, retrying (%d): %A" attempts properties 
            do! Async.Sleep (500)
            return! loop (attempts - 1) }
    loop (20)

let pages = Async.RunSynchronously ( worldBankDownload props )

#r "System.Xml.Linq.dll"
open System.Xml.Linq

// hard coded document namespace
let wb = "http://www.worldbank.org"

/// unsafe helper functions, should rely on option values

let xattr s (el:XElement) = el.Attribute(XName.Get(s)).Value

let xelem s (el:XContainer) = el.Element((XName.Get(s,wb)))

let xvalue (el:XElement) = el.Value

let xelems s (el:XContainer) = el.Elements((XName.Get(s,wb)))

let xnested path (el:XContainer) =
    let f xn s = 
        let child = xelem s xn
        (child :> XContainer)
    let res = path |> Seq.fold f el
    (res :?> XElement)

let c = pages |> XDocument.Parse |> xnested ["countries";"country"] 

let prn = printfn "%s"

c |> xelem "region" |> xvalue |> prn

c |> xelem "incomeLevel" |> xattr "id" |> prn

c |> xelem "region" |> xattr "id" |> prn

/// System.NullReferenceException, if attribute does not exist:
(try c |> xelem "iso2Code" |> xattr "id" with | ex -> "NullReferenceException: " + ex.Message) |> prn