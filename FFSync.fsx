// curl -k -v "https://api.accounts.firefox.com/v1/account/status?uid=0123456789"

open System
open System.IO
open System.Net
open System.Text
open System.Security.Cryptography

// JSON Utilities

#r @"FSharpData\FSharp.Data.dll"
open FSharp.Data
open FSharp.Data.Json
open FSharp.Data.Json.Extensions

// Cryptography Utilities

// E:\projects\fs-random-snippets>"%HOME%\Documents\Visual Studio 2012\Projects\Tutorial3\.nuget\nuget" PBKDF2.NET
// Installing 'PBKDF2.NET 2.0.0'.
// Successfully installed 'PBKDF2.NET 2.0.0'.
#r @"PBKDF2.NET.2.0.0\lib\net45\PBKDF2.NET.dll"

// https://github.com/crowleym/HKDF
#r @"HKDF\RFC5869.dll"
open RFC5869

let stringToBytes (s : string) = s.ToCharArray() |> Array.map (fun x -> (byte) x)
let bytesToString (b : byte[]) = b |> Array.map (char) |> fun cs -> new string(cs)
let bytesToHex (b : byte[]) = b |> Array.map (sprintf "%x")


// Firefox Sync secrets

type Secret = { email : string;
                username : string;
                password : string;
                encryptionpassphrase : string }

let secret =
    Environment.GetEnvironmentVariable("HOME") + @"\.ssh\ff-secret.json"
    |> File.ReadAllText
    |> JsonValue.Parse


let s : Secret = { email = (secret?email).AsString()
                   username = (secret?username).AsString()
                   password = (secret?password).AsString()
                   encryptionpassphrase = (secret?encryptionpassphrase).AsString() }


// Net Utilities 

let unreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
let urlEncode str =
    String.init (String.length str) (fun i ->
        let symbol = str.[i]
        if unreservedChars.IndexOf(symbol) = -1 then
            "%" + String.Format("{0:X2}", int symbol)
        else
            string symbol)

let toHex (s : string) =
    s.ToCharArray()
    |> Array.map int
    |> Array.map (fun x -> sprintf "%x" x)
    |> String.concat ""


let fetchUrlResponse (url : string) requestMethod 
                     (credentials: (string*string) option)
                     (data : byte[] option) (contentType : string option) 
                     timeout =    
    try 
        let req = WebRequest.Create(url)
        match credentials with
        | Some(username, password) -> req.Credentials <- new NetworkCredential(username,password)
        | _ -> ignore credentials
        req.Method <- requestMethod
        match data, contentType with
        | Some data, Some contentType -> req.ContentType <- contentType; req.ContentLength <- (int64) data.Length
        | Some data, _ -> req.ContentLength <- (int64) data.Length
        | _ -> ignore data
        let sendData data = 
            use wstream = req.GetRequestStream() 
            wstream.Write(data , 0, (data.Length))
            wstream.Flush()
            wstream.Close()
        match data with
        | Some data -> data |> sendData
        | _ -> ignore data
        match timeout with
        | Some timeout -> req.Timeout <- timeout
        | _ -> req.Timeout <- 3 * 60 * 1000
        use resp = req.GetResponse()
        use strm = resp.GetResponseStream()
        let text = (new StreamReader(strm)).ReadToEnd()
        text
    with 
    // http://stackoverflow.com/questions/7261986/c-sharp-how-to-get-error-information-when-httpwebrequest-getresponse-fails
    | :? WebException as ex -> use stream = ex.Response.GetResponseStream()
                               use  reader = new StreamReader(stream)
                               Console.WriteLine(reader.ReadToEnd()); "" 
    | _ as ex -> printfn "%s" (ex.ToString()); ""





// Firefox Sync URI - First Attempt


// https://wiki.mozilla.org/User_Services/Sync
// https://github.com/mozilla/fxa-auth-server/blob/master/docs/api.md

//    curl -v \
//    -X POST \
//    -H "Content-Type: application/json" \
//    "https://api-accounts.dev.lcip.org/v1/account/create?keys=true" \
//    -d '{
//      "email": "me@example.com",
//      "authPW": "996bc6b1aa63cd69856a2ec81cbf19d5c8a604713362df9ee15c2bf07128efab"
//    }'


let accountUrl = "https://api.accounts.firefox.com/v1/account"
let loginUrl = accountUrl + "/login?keys=true"
//let loginUrl = "https://api-accounts.dev.lcip.org/v1/account/login?keys=true"


// Firefox Sync login

let setLoginData email authPW = 
    "{ \"email\" : \"" + email + "\", \"authPW\" : \"" + authPW + "\" }"
    |> System.Text.Encoding.ASCII.GetBytes

let requestLogin (loginUrl : string) (loginData : byte[]) =    
    try 
        let req = WebRequest.Create(loginUrl) 
        req.Credentials <- new NetworkCredential(s.username, s.encryptionpassphrase)
        //req.Headers.Add("Authorization",AuthorizationHeader)
        req.Method <- "POST"
        req.ContentType <- "application/json"
        req.ContentLength <- (int64) loginData.Length
        use wstream = req.GetRequestStream() 
        wstream.Write(loginData , 0, (loginData.Length))
        wstream.Flush()
        wstream.Close()
        req.Timeout <- 3 * 60 * 1000
        use resp = req.GetResponse()
        use strm = resp.GetResponseStream()
        let text = (new StreamReader(strm)).ReadToEnd()
        text
    with 
    // http://stackoverflow.com/questions/7261986/c-sharp-how-to-get-error-information-when-httpwebrequest-getresponse-fails
    | :? WebException as ex -> use stream = ex.Response.GetResponseStream()
                               use  reader = new StreamReader(stream)
                               Console.WriteLine(reader.ReadToEnd()); "" 
    | _ as ex -> printfn "%s" (ex.ToString()); ""

((s.password |> toHex) |> setLoginData s.email)
|> requestLogin loginUrl

// Firefox settings, logs, etc.

// https://mail.mozilla.org/pipermail/services-dev/2011-September/000398.html

// http://firefox.bi3.biz/content/aboutconfig-einstellungen-exportieren-speicherort/
//
//    about:config Einstellungen exportieren / Speicherort
//
//    Alle Daten, die man in “about:config” zu sehen bekommt, werden in der “prefs.js” Datei abgelegt. Die Datei befindet sich im Profilordner.
//
//        unter Ubuntu befindet sich der Ordner in “~.mozilla/firefox/*.default-*”
//        unter Windows: “%APPDATA%\Mozilla\Firefox\Profiles\*.default-*”
//
// about:config
//
/// signon.debug
/// javascript.options.showInConsole
/// search sync
//// identity.fxaccounts.remote.signin.uri = https://accounts.firefox.com/signin?service=sync&context=fx_desktop_v1
//// services.sync.serverURL = https://auth.services.mozilla.com/
// about:sync-log
/// file:///C:/Users/Friedrich/AppData/Roaming/Mozilla/Firefox/Profiles/hnpx2edw.default/weave/logs/error-1400431709882.txt 
/// User-Agent: Firefox/29.0.1 FxSync/1.31.0.20140506152807.
/// Caching URLs under storage user base: https://phx-sync-16-3-1.services.mozilla.com/1.1/jqd7.../
/// https://phx-sync-16-3-1.services.mozilla.com/1.1/jqd7.../info/collections
/// https://phx-sync-16-3-1.services.mozilla.com/1.1/jqd7.../storage/crypto/keys
/// https://phx-sync-16-3-1.services.mozilla.com/1.1/jqd7.../storage/meta/global
/// verifyLogin failed: App. Quitting JS Stack trace: Res_get@resource.js:413 < verifyLogin@service.js:698 < onNotify@service.js:975 < WrappedNotify@util.js:143 < WrappedLock@util.js:98 < WrappedCatch@util.js:72 < login@service.js:986 < sync/<@service.js:1232 < WrappedCatch@util.js:72 < sync@service.js:1228
//// 1400431682051	Sync.Status	DEBUG	Status.login: success.login => error.login.reason.network
//// 1400431707179  Sync.Service	INFO	Testing info/collections: {"passwords":1400399288.13,"tabs":1400430491.07,"clients":1400430397.24,"crypto":1384546399.19,"forms":1400419657.13,"meta":1384546502.99,"prefs":1399708819.41,"bookmarks":1400400209.92,"addons":1400393376.52,"history":1400430490.61}
//// 1400431707179	Sync.CollectionKeyManager	INFO	Testing for updateNeeded. Last modified: 0
//// 1400431707180	Sync.Service	INFO	collection keys reports that a key update is needed.
//// 1400431709881	Sync.ErrorHandler	DEBUG	addons failed: App. Quitting Stack trace: checkAppReady/onQuitApplication/Async.checkAppReady()@resource://services-common/async.js:123 < waitForSyncCallback()@resource://services-common/async.js:98 < makeSpinningCallback/callback.wait()@resource://services-common/async.js:141 < _ensureStateLoaded()@resource://gre/modules/services-sync/addonsreconciler.js:579 < pruneChangesBeforeDate()@resource://gre/modules/services-sync/addonsreconciler.js:526 < _syncCleanup()@resource://gre/modules/services-sync/engines/addons.js:216 < SyncEngine.prototype._sync()@resource://services-sync/engines.js:1484 < WrappedNotify()@resource://services-sync/util.js:143 < Engine.prototype.sync()@resource://services-sync/engines.js:655 < _syncEngine()@resource://services-sync/stages/enginesync.js:199 < sync()@resource://services-sync/stages/enginesync.js:149 < onNotify()@resource://gre/modules/services-sync/service.js:1255 < WrappedNotify()@resource://services-sync/util.js:143 < WrappedLock()@resource://services-sync/util.js:98 < _lockedSync()@resource://gre/modules/services-sync/service.js:1249 < sync/<()@resource://gre/modules/services-sync/service.js:1240 < WrappedCatch()@resource://services-sync/util.js:72 < sync()@resource://gre/modules/services-sync/service.js:1228 < <file:unknown>
// resource://gre/modules/Services.jsm
// resource://services-sync/util.js
// resource://gre/modules/Services.jsm
// resource://gre/modules/XPCOMUtils.jsm




// Firefox Sync URI - Second Attempt

let syncServerURL = "https://auth.services.mozilla.com/user/1.0/" + s.username + "/node/weave"
let syncClusterURL = fetchUrlResponse syncServerURL "GET" None None None None

let getCryptoKeys syncClusterURL username password =
    let url = syncClusterURL + "1.1/" + username + "/storage/crypto/keys"
    fetchUrlResponse url "GET" (Some (username, password)) None None None

getCryptoKeys syncClusterURL s.username s.password
//val it : string =
//  "{"payload": "{\"ciphertext\":\"...==\",\"IV\":\"...==\",\"hmac\":\"...\"}", "id": "keys", "modified": 1384546399.19}"  



// write secretKeys to disk
let secretKeys = getCryptoKeys syncClusterURL s.username s.password
let stream = new StreamWriter(Environment.GetEnvironmentVariable("HOME") + @"\.ssh\ff-secretKeys.json", false)
stream.WriteLine(secretKeys)
stream.Close()

// read secretKeys from disk
//let secretKeys = Environment.GetEnvironmentVariable("HOME") + @"\.ssh\ff-secretKeys.json"
//                 |> File.ReadAllText



// decrypt payload
// https://docs.services.mozilla.com/sync/storageformat5.html

let sk = secretKeys |> JsonValue.Parse
let pl = (sk?payload).AsString() |> JsonValue.Parse

type Payload = { iv         : string
                 ciphertext : string
                 hmac       : string }

let pl'' x = (pl.GetProperty x).AsString()

let pl' = { iv         = pl'' "IV"
            ciphertext = pl'' "ciphertext"
            hmac       = pl'' "hmac" }

let inline padChar len (c : 'T) (b : 'T[])  =
    [| for i in [0 .. len-1] do if i < b.Length then yield b.[i] else yield c |]

let iv = Convert.FromBase64String pl'.iv 
//let iv = ASCIIEncoding.ASCII.GetBytes pl'.iv
//let key = s.encryptionpassphrase.ToCharArray() |> Array.map (byte) |> padZeros 16
let key = s.password.ToCharArray() |> Array.map (byte) |> padChar 16 0uy
//let key = s.encryptionpassphrase |> ASCIIEncoding.ASCII.GetBytes
//let key = s.password.ToCharArray() |> Array.map (byte)
//let key = Convert.FromBase64String (s.encryptionpassphrase + "==")


// http://social.msdn.microsoft.com/Forums/en-US/aa51d82c-3868-4da8-b697-9a26926d0806/c-and-php-encryption-and-decryption-rijndaelaes-256?forum=csharplanguage
let DecryptIt (s : string) (key : byte[]) (iv : byte[]) =
    let rijn = new RijndaelManaged()
    rijn.Mode <- CipherMode.ECB
    rijn.Padding <- PaddingMode.Zeros
    use msDecrypt = new MemoryStream(Convert.FromBase64String(s))
    use decryptor = rijn.CreateDecryptor(key, iv)
    use csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read)
    use swDecrypt = new StreamReader(csDecrypt)
    let result = swDecrypt.ReadToEnd()
    rijn.Clear()
    result

// http://msdn.microsoft.com/en-us/library/system.security.cryptography.aescryptoserviceprovider%28v=vs.110%29.aspx
let DecryptAES (s : string) (key : byte[]) (iv : byte[]) =
    // Check arguments.
    if s.Length * key.Length * iv.Length = 0 then ""
    else
        // Create an AesCryptoServiceProvider object 
        // with the specified key and IV. 
        use aesAlg = new AesManaged()
        printfn "%d" aesAlg.KeySize

        aesAlg.Key <- key
        aesAlg.IV <- iv
        aesAlg.Padding <- PaddingMode.Zeros
        

        // Create a decrytor to perform the stream transform.
        use decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV)

        // Create the streams used for decryption. 
        use msDecrypt = new MemoryStream(Convert.FromBase64String(s)) 
        use csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read)
        use srDecrypt = new StreamReader(csDecrypt)
        
        // Read the decrypted bytes from the decrypting stream 
        // and place them in a string.
        let plaintext = srDecrypt.ReadToEnd()
        plaintext

s.encryptionpassphrase.Length
s.password.Length
let cleartext = DecryptAES pl'.ciphertext key iv
let cleartext' = DecryptIt pl'.ciphertext key iv
let c = pl'.ciphertext |> Convert.FromBase64String |> Array.map (char)



// https://bitbucket.org/devinmartin/base32/src/90d7d530beea52a2a82b187728a06404794600b9/Base32/Base32Encoder.cs?at=default
let base32Decode (s' : string) = 
    let undo89 (s : string) = 
        s.ToUpper().ToCharArray() 
        |> Array.map (fun x -> match x with | '8' -> 'L' | '9' -> 'O' | _ -> x )
    let s = s'.ToUpper() |> undo89 |> Array.filter (fun x -> if x = '=' then false else true) |> fun cs -> new string(cs)
    let encodingChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"
    let encodedBitCount = 5
    let byteBitCount = 8
    let isSubset (set : string) (subset :string) = 
        let set' = set.ToCharArray() |> Set.ofArray
        let mutable res = 1
        for c in subset do
            if set'.Contains c then res <- 1 * res else res <- 0 * res  
        if res = 1 then true else false
    if isSubset encodingChars s then       
        let outputBuffer = Array.create (s.Length * encodedBitCount / byteBitCount) 0uy
        let mutable workingByte = 0uy
        let mutable bitsRemaining = byteBitCount
        let mutable mask = 0
        let mutable arrayIndex = 0
        for c in s.ToCharArray() do 
            let value = encodingChars.IndexOf c
            if bitsRemaining > encodedBitCount then
                mask <- value <<< (bitsRemaining - encodedBitCount)
                workingByte <- (workingByte ||| (byte) mask)
                bitsRemaining <- bitsRemaining - encodedBitCount
            else
                mask <- value >>> (encodedBitCount - bitsRemaining)
                workingByte <- (workingByte ||| (byte) mask)
                outputBuffer.[arrayIndex] <- workingByte
                arrayIndex <- arrayIndex + 1
                workingByte <- (byte)(value <<< (byteBitCount - encodedBitCount + bitsRemaining))
                bitsRemaining <- bitsRemaining + byteBitCount - encodedBitCount
        outputBuffer
    else
        [||]

// Test : https://docs.services.mozilla.com/sync/storageformat5.html
// 
//"Y4NKPS6YXAVI75XNUVODSR472I" 
//val it : string [] =
//  [|"c7"; "1a"; "a7"; "cb"; "d8"; "b8"; "2a"; "8f"; "f6"; "ed"; "a5"; "5c";
//    "39"; "47"; "9f"; "d2"|]
// Python: \xc7\x1a\xa7\xcb\xd8\xb8\x2a\x8f\xf6\xed\xa5\x5c\x39\x47\x9f\xd2

let sync_key = s.encryptionpassphrase |> base32Decode

type SyncKeyBundle = { encryption_key : byte[]; hmac_local : byte[] }

let syncKeyBundle username key =
    let info = "Sync-AES_256_CBC-HMAC256" + username
    let hmac256 = new HMACSHA256(key)
    let T1 = hmac256.ComputeHash (Array.append (info |> stringToBytes ) [| 1uy |] )
    let T2 = hmac256.ComputeHash (Array.append T1 <| Array.append (info |> stringToBytes) [| 2uy |])   
    { encryption_key = T1 ; hmac_local = T2 }
    

//    sync_key = \xc7\x1a\xa7\xcb\xd8\xb8\x2a\x8f\xf6\xed\xa5\x5c\x39\x47\x9f\xd2
//    username = johndoe@example.com
//    HMAC_INPUT = Sync-AES_256_CBC-HMAC256
//
//    # Combine HMAC_INPUT and username to form HKDF info input.
//    info = HMAC_INPUT + username
//      -> "Sync-AES_256_CBC-HMAC256johndoe@example.com"
//
//    # Perform HKDF Expansion (1)
//    encryption_key = HKDF-Expand(sync_key, info + "\x01", 32)
//      -> 0x8d0765430ea0d9dbd53c536c6c5c4cb639c093075ef2bd77cd30cf485138b905
//
//    # Second round of HKDF
//    hmac = HKDF-Expand(sync_key, encryption_key + info + "\x02", 32)
//      -> 0xbf9e48ac50a2fcc400ae4d30a58dc6a83a7720c32f58c60fd9d02db16e406216
//
let ff_skb' = syncKeyBundle "johndoe@example.com" ("Y4NKPS6YXAVI75XNUVODSR472I" |> base32Decode)
let ff_skb'' = ( ff_skb'.encryption_key |> Array.map (sprintf "%x"), 
                 ff_skb'.hmac_local |> Array.map (sprintf "%x"))
//    val ff_skb' : SyncKeyBundle =
//      {encryption_key =
//        [|141uy; 7uy; 101uy; 67uy; 14uy; 160uy; 217uy; 219uy; 213uy; 60uy; 83uy;
//          108uy; 108uy; 92uy; 76uy; 182uy; 57uy; 192uy; 147uy; 7uy; 94uy; 242uy;
//          189uy; 119uy; 205uy; 48uy; 207uy; 72uy; 81uy; 56uy; 185uy; 5uy|];
//       hmac_local =
//        [|191uy; 158uy; 72uy; 172uy; 80uy; 162uy; 252uy; 196uy; 0uy; 174uy; 77uy;
//          48uy; 165uy; 141uy; 198uy; 168uy; 58uy; 119uy; 32uy; 195uy; 47uy; 88uy;
//          198uy; 15uy; 217uy; 208uy; 45uy; 177uy; 110uy; 64uy; 98uy; 22uy|];}
//    val ff_skb'' : string [] * string [] =
//      ([|"8d"; "7"; "65"; "43"; "e"; "a0"; "d9"; "db"; "d5"; "3c"; "53"; "6c";
//         "6c"; "5c"; "4c"; "b6"; "39"; "c0"; "93"; "7"; "5e"; "f2"; "bd"; "77";
//         "cd"; "30"; "cf"; "48"; "51"; "38"; "b9"; "5"|],
//       [|"bf"; "9e"; "48"; "ac"; "50"; "a2"; "fc"; "c4"; "0"; "ae"; "4d"; "30";
//         "a5"; "8d"; "c6"; "a8"; "3a"; "77"; "20"; "c3"; "2f"; "58"; "c6"; "f";
//         "d9"; "d0"; "2d"; "b1"; "6e"; "40"; "62"; "16"|])

let sync_key_bundle = syncKeyBundle s.username sync_key

