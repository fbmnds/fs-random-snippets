open System
open System.IO
open System.Net
open System.Text
open System.Security.Cryptography

// JSON
#r @"FSharpData\FSharp.Data.dll"
open FSharp.Data
open FSharp.Data.Json
open FSharp.Data.Json.Extensions

// Firefox Sync Secrets
[<AutoOpen>]
module Secrets = 
    type Secret = { email : string;
                    username : string;
                    password : string;
                    encryptionpassphrase : string }

    let secrets' =
        Environment.GetEnvironmentVariable("HOME") + @"\.ssh\ff-secret.json"
        |> File.ReadAllText
        |> JsonValue.Parse


    let secrets : Secret = { email = (secrets'?email).AsString()
                             username = (secrets'?username).AsString()
                             password = (secrets'?password).AsString()
                             encryptionpassphrase = (secrets'?encryptionpassphrase).AsString() }

// Utilities
[<AutoOpen>]
module Utilities = 
    
    // Misc.

    let inline padArray len (c : 'T) (b : 'T[])  =
        [| for i in [0 .. len-1] do if i < b.Length then yield b.[i] else yield c |]

    let stringToBytes (s : string) = s.ToCharArray() |> Array.map (fun x -> (byte) x)
    let bytesToString (b : byte[]) = b |> Array.map (char) |> fun cs -> new string(cs)
    let bytesToHex (b : byte[]) = b |> Array.map (sprintf "%x")

    let removeChars (chars : string) (x : string) = 
        let set = chars.ToCharArray() |> Set.ofArray
        x.ToCharArray() |> Array.filter (fun x -> if set.Contains x then false else true) |> fun x -> new string (x)

    let keepAsciiPrintableChars (x : string) =
        x.ToCharArray() |> Array.filter (fun x -> if int x < 32 || int x > 126 then false else true) |> fun x -> new string (x)
    
    // Cryptography

    // E:\projects\fs-random-snippets>"%HOME%\Documents\Visual Studio 2012\Projects\Tutorial3\.nuget\nuget" PBKDF2.NET
    // Installing 'PBKDF2.NET 2.0.0'.
    // Successfully installed 'PBKDF2.NET 2.0.0'.
    // #r @"PBKDF2.NET.2.0.0\lib\net45\PBKDF2.NET.dll"

    // https://github.com/crowleym/HKDF
    // #r @"HKDF\RFC5869.dll"
    // open RFC5869

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


    type SyncKeyBundle = { encryption_key : byte[]; hmac_key : byte[] }

    let syncKeyBundle username key =
        let info = "Sync-AES_256_CBC-HMAC256" + username
        let hmac256 = new HMACSHA256(key)
        let T1 = hmac256.ComputeHash (Array.append (info |> stringToBytes ) [| 1uy |] )
        let T2 = hmac256.ComputeHash (Array.append T1 <| Array.append (info |> stringToBytes) [| 2uy |])   
        { encryption_key = T1 ; hmac_key = T2 }


    // http://msdn.microsoft.com/en-us/library/system.security.cryptography.aescryptoserviceprovider%28v=vs.110%29.aspx
    let DecryptAES (s : string) (key : byte[]) (iv : byte[]) =
        // Check arguments.
        if s.Length * key.Length * iv.Length = 0 then ""
        else
            // Create an AesCryptoServiceProvider object 
            // with the specified key and IV. 
            use aesAlg = new AesManaged()

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


    
    // Net Utilities 

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


// Firefox Sync ServerAPI
[<AutoOpen>]
module ServerAPI =
    let serverURL username = "https://auth.services.mozilla.com/user/1.0/" + username + "/node/weave"
    let clusterURL username = fetchUrlResponse (username |> serverURL) "GET" None None None None

    let fetchCryptoKeys username password =
        let url = (clusterURL username) + "1.1/" + username + "/storage/crypto/keys"
        fetchUrlResponse url "GET" (Some (username, password)) None None None



    type CryptoKeysPayload = { iv         : string
                               ciphertext : string
                               hmac       : string }

    let writeSecretKeysToDisk username password (file : string option) = 
        let secretKeys = fetchCryptoKeys username password
        let mutable file' = ""
        match file with 
        | Some file -> file' <- file
        | _ -> file' <- Environment.GetEnvironmentVariable("HOME") + @"\.ssh\ff-secretKeys.json"
        let stream = new StreamWriter(file', false)
        stream.WriteLine(secretKeys)
        stream.Close()

    let readSecretKeysFromDisk (file : string option) = 
        try 
            match file with 
            | Some file -> file |> File.ReadAllText
            | _ -> Environment.GetEnvironmentVariable("HOME") + @"\.ssh\ff-secretKeys.json" |> File.ReadAllText
        with | _ -> "" 


module Test = 
    open Utilities

    // base32Decode

    // https://docs.services.mozilla.com/sync/storageformat5.html
    // 
    //"Y4NKPS6YXAVI75XNUVODSR472I" 
    // Python: \xc7\x1a\xa7\xcb\xd8\xb8\x2a\x8f\xf6\xed\xa5\x5c\x39\x47\x9f\xd2
    if ("Y4NKPS6YXAVI75XNUVODSR472I" |> base32Decode |> bytesToHex) <> [|"c7"; "1a"; "a7"; "cb"; "d8"; "b8"; "2a"; "8f"; "f6"; "ed"; "a5"; "5c"; "39"; "47"; "9f"; "d2"|] then 
        failwith "base32Decode failed"


    // syncKeyBundle

    // https://docs.services.mozilla.com/sync/storageformat5.html
    // 
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
                     ff_skb'.hmac_key |> Array.map (sprintf "%x"))
    let ff_skb''' = ([|"8d"; "7"; "65"; "43"; "e"; "a0"; "d9"; "db"; "d5"; "3c"; "53"; "6c";
                       "6c"; "5c"; "4c"; "b6"; "39"; "c0"; "93"; "7"; "5e"; "f2"; "bd"; "77";
                       "cd"; "30"; "cf"; "48"; "51"; "38"; "b9"; "5"|],
                     [|"bf"; "9e"; "48"; "ac"; "50"; "a2"; "fc"; "c4"; "0"; "ae"; "4d"; "30";
                       "a5"; "8d"; "c6"; "a8"; "3a"; "77"; "20"; "c3"; "2f"; "58"; "c6"; "f";
                       "d9"; "d0"; "2d"; "b1"; "6e"; "40"; "62"; "16"|])
    if ff_skb'' <> ff_skb''' then 
        failwith "syncKeyBundle failed"

    
    // writeSecretKeysToDisk

    try
        writeSecretKeysToDisk secrets.username secrets.password None
    with | _ -> failwith "writeSecretKeysToDisk failed"


module CryptoKeys =
    open Secrets
    open Utilities

    let sk = readSecretKeysFromDisk None |> JsonValue.Parse
    let sk_pl = (sk?payload).AsString() |> JsonValue.Parse

// https://docs.services.mozilla.com/sync/storageformat5.html
//
//    ciphertext  = record.ciphertext
//    iv          = record.iv
//    record_hmac = record.hmac
    let record = { iv         = (sk_pl?IV).AsString()
                   ciphertext = (sk_pl?ciphertext).AsString()
                   hmac       = (sk_pl?hmac).AsString() }
//
//    encryption_key = bundle.encryption_key
//    hmac_key       = bundle.hmac_key
    let bundle = syncKeyBundle secrets.username (secrets.encryptionpassphrase |> base32Decode)
//
//    local_hmac = HMACSHA256(hmac_key, base64(ciphertext))
    let local_hmac = record.ciphertext |> Convert.FromBase64String |> (new HMACSHA256(bundle.hmac_key)).ComputeHash
//
//    if local_hmac != record_hmac:
//      throw Error("HMAC verification failed.")
//
//    cleartext = AESDecrypt(ciphertext, encryption_key, iv)
    let cleartext = DecryptAES record.ciphertext bundle.encryption_key (record.iv |> Convert.FromBase64String)

module Collections =

    let s = Secrets.secrets
    let url = clusterURL s.username
    
    // https://github.com/mikerowehl/firefox-sync-client-php/blob/master/sync.php#L94
    let fetchCollection username password collection =
        let url = (clusterURL username) + "1.1/" + username + "/storage/" + collection + "?full=1"
        fetchUrlResponse url "GET" (Some (username, password)) None None None

    let bm' = fetchCollection s.username s.password "bookmarks"

    let parseCollection collection = 
        collection
        |> JsonValue.Parse 
        |> fun x -> x.AsArray() 
        |> Array.map string 
        |> Array.map (fun x -> JsonValue.Parse x) 
        |> Array.map (fun x -> (x?payload).AsString())
        |> Array.map (fun x -> JsonValue.Parse x)
        |> Array.map (fun x -> { iv = x?IV.AsString(); ciphertext = x?ciphertext.AsString(); hmac = x?hmac.AsString() } )

    let bm = parseCollection bm'
    let key = 
        CryptoKeys.cleartext 
        |> keepAsciiPrintableChars
        |> JsonValue.Parse 
        |> fun x -> (x.GetProperty "default").AsArray()
        |> fun x -> x.[0].AsString()
        |> Convert.FromBase64String
    let cleartext = [|for b in bm do yield DecryptAES b.ciphertext key (b.iv |> Convert.FromBase64String) |]
 