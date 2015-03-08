// Firefox Sync Service
// (formerly Firefox Weave)

// TODO
// ----
// railway oriented programming, consistent error/exception handling
// better types (e.g. GUID, URL), http://fsharpforfunandprofit.com/posts/designing-with-types-single-case-dus/
// proper testing
// complete functionality
// async http
// parallel tasks
// secure password store (PasswordVault?)
// secure in memory strings (IBuffer?)
// convert script into library


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

// Types
[<AutoOpen>]
module DataSignatures =
 
    // Firefox Object Formats

    // http://docs.services.mozilla.com/sync/objectformats.html

    type URI = URI of string
    
    // Firefox docs refer to GUID, but not according to RFC4122 
    // https://docs.services.mozilla.com/sync/storageformat5.html#metaglobal-record
    // "The Firefox client uses 12 randomly generated base64url characters, much like for WBO IDs."
    type WeaveGUID = WeaveGUID of string 

    type Addons = 
        { addonID       : string
          applicationID : string
          enabled       : Boolean
          source        : string }

    type Bookmarks = 
        { id            : WeaveGUID
          ``type``      : string
          title         : string
          parentName    : string
          bmkUri        : URI
          tags          : string []
          keyword       : string
          description   : string
          loadInSidebar : Boolean
          parentid      : WeaveGUID
          children      : WeaveGUID [] }

    type Microsummary = 
        { generatorUri  : string
          staticTitle   : string
          title         : string
          bmkUri        : string
          description   : string
          loadInSidebar : Boolean
          tags          : string []
          keyword       : string
          parentid      : string
          parentName    : string
          predecessorid : string
          ``type``      : string }

    type Query = 
        { folderName    : string
          queryId       : string
          title         : string
          bmkUri        : string
          description   : string
          loadInSidebar : Boolean
          tags          : string []
          keyword       : string
          parentid      : string
          parentName    : string
          predecessorid : string
          ``type``      : string }

    type Folder = 
        { title         : string
          parentid      : string
          parentName    : string
          predecessorid : string
          ``type``      : string }

    type Livemark = 
        { siteUri       : string
          feedUri       : string
          title         : string
          parentid      : string
          parentName    : string
          predecessorid : string
          ``type``      : string }

    type Separator = 
        { pos           : string
          parentid      : string
          parentName    : string
          predecessorid : string
          ``type``      : string 
          children      : string [] }

    type Clients = 
        { name      : string
          ``type``  : string
          commands  : string []
          version   : string
          protocols : string [] }

    type ClientsPayload = 
        { name         : string
          formfactor   : string
          application  : string
          version      : string
          capabilities : string
          mpEnabled    : Boolean }

    type Commands = 
        { receiverID : string
          senderID   : string
          created    : Int64
          action     : string
          data       : string }

    type Forms =  
        { name  :  string
          value : string }

    type HistoryTransition = 
    | TRANSITION_LINK  = 1
    | TRANSITION_TYPED = 2
    | TRANSITION_BOOKMARK = 3
    | TRANSITION_EMBED = 4
    | TRANSITION_REDIRECT_PERMANENT = 5
    | TRANSITION_REDIRECT_TEMPORARY = 6
    | TRANSITION_DOWNLOAD = 7
    | TRANSITION_FRAMED_LINK = 8

    type HistoryPayloadVisits = 
        { uri    : string
          title  : string
          visits : string [] }

    type HistoryPayload = { items : HistoryPayloadVisits [] }

    type History = 
        { histUri  : string
          title    : string
          visits   : HistoryPayload
          date     : Int64 // datetime of the visit
          ``type`` : HistoryTransition }
    
    type Passwords = 
        { hostname      : string
          formSubmitURL : string
          httpRealm     : string
          username      : string
          password      : string
          usernameField : string
          passwordField : string }

    type Preferences = 
        { value    : string
          name     : string
          ``type`` : string }

    module TabsVersions =
        
        type StringOrInteger =
        | String of string
        | Integer of int 

        type Version1 = 
            { clientName : string
              tabs       : string []
              title      : string
              urlHistory : string []
              icon       : string
              lastUsed   : StringOrInteger }

        type Version2 = 
            { clientID  : string
              title     : string
              history   : string []
              lastUsed  : Int64 // Time in seconds since Unix epoch that tab was last active.
              icon      : string
              groupName : string }

    type Tabs = 
    | Version1 of TabsVersions.Version1
    | Version2 of TabsVersions.Version2


    // Firefox Sync Secrets

    type Secret = 
        { email                : string
          username             : string
          password             : string
          encryptionpassphrase : string }


    // CryptoKeys

    type SyncKeyBundle = 
        { encryption_key : byte[] 
          hmac_key       : byte[] }

    type EncryptedCollection = 
        { iv         : string
          ciphertext : string
          hmac       : string }

    type CryptoKeys = { ``default`` : byte [] [] }


    // MetaGlobal
    
    type MetaGlobalVersionInfo = 
        { version : int
          syncID  : WeaveGUID }

    type Engine = Engine of string

    type MetaGlobalPayload = 
        { syncID         : WeaveGUID
          storageVersion : int        
          engines        : Map<Engine,MetaGlobalVersionInfo>
          declined       : Engine [] }

    type MetaGlobal =
        { username : string         // 8 digits, what kind of mapping?
          payload  : MetaGlobalPayload
          id       : string         // "global"
          modified : float }

    

// Firefox Sync Secrets
module Secrets = 

    let mutable defaultLocalSecretFile = Environment.GetEnvironmentVariable("HOME") + @"\.ssh\ff-local-secret.json"
    let mutable defaultRemoteSecretFile = Environment.GetEnvironmentVariable("HOME") + @"\.ssh\ff-remote-secret.json"
    
    /// Read the local Firefox Sync Secrets from disk,
    /// throw an exception on error cases.
    let readSecretFile file =
        let mutable file' = ""
        match file with
        | Some file -> file' <- file
        | _ -> file' <- defaultLocalSecretFile
        file'
        |> File.ReadAllText
        |> JsonValue.Parse
        
    let secrets' = readSecretFile None

    let secrets = { email = (secrets'?email).AsString()
                    username = (secrets'?username).AsString()
                    password = (secrets'?password).AsString()
                    encryptionpassphrase = (secrets'?encryptionpassphrase).AsString() }


// Utilities
[<AutoOpen>]
module Utilities = 
    
    // JSON

    let tryGetString (jsonvalue : JsonValue) property = 
        try 
            property 
            |> jsonvalue.TryGetProperty 
            |> fun x -> match x with | Some x -> x.AsString() | _ -> ""
            |> fun x -> if x = null then "" else x
        with | _ -> ""

    let tryGetBoolean (jsonvalue : JsonValue) defaultboolean property  = 
        try
            property 
            |> jsonvalue.TryGetProperty 
            |> fun x -> match x with | Some x -> x.AsBoolean() | _ -> defaultboolean
        with | _ -> defaultboolean

    let tryGetArray (jsonvalue : JsonValue) property = 
        try
            property 
            |> jsonvalue.TryGetProperty 
            |> fun x -> match x with | Some x -> x.AsArray() | _ -> [||]
            |> fun x -> if x = null then [||] else x 
        with | _ -> [||]
    


    let tryGetInteger (jsonvalue : JsonValue) property = 
        try
            property 
            |> jsonvalue.TryGetProperty 
            |> fun x -> match x with | Some x -> Some (x.AsInteger()) | _ -> None 
        with | _ -> None

    let tryGetIntegerWithDefault (jsonvalue : JsonValue) defaultinteger property = 
        try
            property 
            |> jsonvalue.TryGetProperty 
            |> fun x -> match x with | Some x -> x.AsInteger() | _ -> defaultinteger 
        with | _ -> defaultinteger

    // Misc.

    let inline padArray len (c : 'T) (b : 'T[])  =
        [| for i in [0 .. len-1] do if i < b.Length then yield b.[i] else yield c |]

    let stringToBytes (s : string) = s.ToCharArray() |> Array.map (fun x -> (byte) x)
    let bytesToString (b : byte[]) = b |> Array.map (char) |> fun cs -> new string(cs)
    let bytesToHex (b : byte[]) = b |> Array.map (sprintf "%x")

    let inline isSubset set subset = 
        let set' = set |> Set.ofSeq
        let mutable res = 1
        for c in subset do
            if set'.Contains c then res <- 1 * res else res <- 0 * res  
        if res = 1 then true else false

    let removeChars (chars : string) (x : string) = 
        let set = chars.ToCharArray() |> Set.ofArray
        x.ToCharArray() |> Array.filter (fun x -> if set.Contains x then false else true) |> fun x -> new string (x)

    let keepAsciiPrintableChars (x : string) =
        x.ToCharArray() |> Array.filter (fun x -> if int x < 32 || int x > 126 then false else true) |> fun x -> new string (x)

    // http://www.fssnip.net/3y
    let getRecordFields (r: 'record) =
        typeof<'record> |> Microsoft.FSharp.Reflection.FSharpType.GetRecordFields 
        
    let getRecordField (r: 'record) (field : Reflection.PropertyInfo) =
        Microsoft.FSharp.Reflection.FSharpValue.GetRecordField(r,field) |> unbox
       
    // Encoding schemes

    let random = new Random()
    
    let base32Chars     = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"
    let base32'8'9Chars = "ABCDEFGHIJK8MN9PQRSTUVWXYZ234567"
    let base64Chars     = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890+/"
    let base64urlChars  = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890-_"

    let doBase32'8'9 (s : string) = 
        s.ToUpper().ToCharArray() 
        |> Array.map (fun x -> match x with | 'L' -> '8' | 'O' -> '9' | _ -> x )
        |> fun cs -> new string(cs)

    let undoBase32'8'9 (s : string) = 
        s.ToUpper().ToCharArray() 
        |> Array.map (fun x -> match x with | '8' -> 'L' | '9' -> 'O' | _ -> x )
        |> fun cs -> new string(cs)
    
    let generateWeaveGUID() = 
        [| for i in [0 .. 11] do yield base64urlChars.Substring(random.Next(63), 1) |]
        |> Array.fold (fun r s -> r + s) ""
        |> (WeaveGUID)


    // https://bitbucket.org/devinmartin/base32/src/90d7d530beea52a2a82b187728a06404794600b9/Base32/Base32Encoder.cs?at=default
    let base32Decode (s' : string) = 
        let s = 
            s'.ToUpper().ToCharArray() 
            |> Array.filter (fun x -> if x = '=' then false else true) 
            |> fun cs -> new string(cs)        
        let encodedBitCount = 5
        let byteBitCount = 8
        if isSubset base32Chars s then       
            let outputBuffer = Array.create (s.Length * encodedBitCount / byteBitCount) 0uy
            let mutable workingByte = 0uy
            let mutable bitsRemaining = byteBitCount
            let mutable mask = 0
            let mutable arrayIndex = 0
            for c in s.ToCharArray() do 
                let value = base32Chars.IndexOf c
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

    let base32'8'9Decode (s' : string) = s' |> undoBase32'8'9 |> base32Decode
     

    // Cryptography, Hashes

    // E:\projects\fs-random-snippets>"%HOME%\Documents\Visual Studio 2012\Projects\Tutorial3\.nuget\nuget" PBKDF2.NET
    // Installing 'PBKDF2.NET 2.0.0'.
    // Successfully installed 'PBKDF2.NET 2.0.0'.
    // #r @"PBKDF2.NET.2.0.0\lib\net45\PBKDF2.NET.dll"

    // https://github.com/crowleym/HKDF
    // #r @"HKDF\RFC5869.dll"
    // open RFC5869


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

    /// Return the server response as a string,
    /// return an empty string in case of error, log error messages to the console.
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


// Firefox Sync Server URLs
[<AutoOpen>]
module ServerUrls =

    let serverURL username = "https://auth.services.mozilla.com/user/1.0/" + username + "/node/weave"
    let clusterURL username = fetchUrlResponse (username |> serverURL) "GET" None None None None

 

// Firefox Crypto Keys
[<AutoOpen>]
module CryptoKeys =

    /// Return the Firefox Crypto Keys as a string (encrypted),
    /// return an empty string in case of error, log error messages to the console.
    let fetchCryptoKeys username password =
        let url = (clusterURL username) + "1.1/" + username + "/storage/crypto/keys"
        fetchUrlResponse url "GET" (Some (username, password)) None None None

    /// Fetch the remote Firefox Crypto Keys and write them to disk,
    /// throw an exception and/or log to console depending on error case.
    let writeCryptoKeysToDisk username password (file : string option) = 
        let secretKeys = fetchCryptoKeys username password
        let mutable file' = ""
        match file with 
        | Some file -> file' <- file
        | _ -> file' <- Secrets.defaultRemoteSecretFile
        use stream = new StreamWriter(file', false)
        stream.WriteLine(secretKeys)
        stream.Close()
    
    /// Read the prefetched remote Firefox Crypto Keys as a string (encrypted) from disk,
    /// return an empty string on error.
    let readCryptoKeysFromDisk (file : string option) = 
        try 
            match file with 
            | Some file -> file |> File.ReadAllText
            | _ -> Secrets.defaultRemoteSecretFile |> File.ReadAllText
        with | _ -> ""


    let decryptCryptoKeys (secrets : Secret) cryptoKeys = 
        let ck = cryptoKeys |> JsonValue.Parse
        let ck_pl = (ck?payload).AsString() |> JsonValue.Parse

        // https://docs.services.mozilla.com/sync/storageformat5.html
        //
        //    ciphertext  = record.ciphertext
        //    iv          = record.iv
        //    record_hmac = record.hmac
        let record = { iv         = (ck_pl?IV).AsString()
                       ciphertext = (ck_pl?ciphertext).AsString()
                       hmac       = (ck_pl?hmac).AsString() }
        //
        //    encryption_key = bundle.encryption_key
        //    hmac_key       = bundle.hmac_key
        let bundle = syncKeyBundle secrets.username (secrets.encryptionpassphrase |> base32'8'9Decode)
        //
        //    local_hmac = HMACSHA256(hmac_key, base64(ciphertext))
        let local_hmac = record.ciphertext |> Convert.FromBase64String |> (new HMACSHA256(bundle.hmac_key)).ComputeHash
        //
        //    if local_hmac != record_hmac:
        //      throw Error("HMAC verification failed.")
        //
        //    cleartext = AESDecrypt(ciphertext, encryption_key, iv)         
        record.iv 
        |> Convert.FromBase64String 
        |> DecryptAES record.ciphertext bundle.encryption_key 
        |> keepAsciiPrintableChars
        
    let getCryptoKeysFromString secrets cryptokeys =
        try
            { ``default`` =
                cryptokeys
                |> decryptCryptoKeys secrets
                |> JsonValue.Parse 
                |> fun x -> (x.GetProperty "default").AsArray()
                |> Array.map (fun x -> x.AsString())
                |> Array.map Convert.FromBase64String }
        with 
        | _ -> { ``default`` = [||] }
                

    let getCryptoKeys (secrets : Secret) =
        try
            fetchCryptoKeys secrets.username secrets.password
            |> getCryptoKeysFromString secrets
        with 
        | _ -> { ``default`` = [||] }

    
    let getCryptokeysFromDisk secrets file =
        try
            readCryptoKeysFromDisk file
            |> getCryptoKeysFromString secrets
        with 
        | _ -> { ``default`` = [||] }


[<AutoOpen>]
module GeneralInfo =  

    let fetchInfoCollections username password =
        let url = (clusterURL username) + "1.1/" + username + "/info/collections"
        fetchUrlResponse url "GET" (Some (username, password)) None None None

    let fetchInfoQuota username password =
        let url = (clusterURL username) + "1.1/" + username + "/info/quota"
        fetchUrlResponse url "GET" (Some (username, password)) None None None

    let fetchInfoCollectionUsage username password =
        let url = (clusterURL username) + "1.1/" + username + "/info/collection_usage"
        fetchUrlResponse url "GET" (Some (username, password)) None None None

    let fetchInfoCollectionCounts username password =
        let url = (clusterURL username) + "1.1/" + username + "/info/collection_counts"
        fetchUrlResponse url "GET" (Some (username, password)) None None None

    let deleteStorage username password =
        let url = (clusterURL username) + "1.1/" + username
        fetchUrlResponse url "GET" (Some (username, password)) None None None

    let fetchMetaGlobal username password =
        let url = (clusterURL username) + "1.1/" + username + "/storage/meta/global"
        fetchUrlResponse url "GET" (Some (username, password)) None None None

    let getMetaGlobal (secrets : Secret) =
        let parseMetaGlobalPayload p =
            let p' = JsonValue.Parse p
            { syncID         = "syncID" |> tryGetString p' |> (WeaveGUID)
              storageVersion = "storageVersion" |> tryGetIntegerWithDefault p' -99 
              engines        = "engines" 
                               |> p'.GetProperty 
                               |> fun x -> x.Properties 
                               |> Seq.map (fun (x,y) -> 
                                               let v = "version" |> tryGetIntegerWithDefault y -99 
                                               let s = "syncID"  |> tryGetString y |> (WeaveGUID)
                                               ((Engine) x, { version = v; syncID = s } ))
                               |> Map.ofSeq
              declined       = "declined" |> tryGetArray p' |> Array.map (fun x -> x.AsString() |> (Engine)) }            
        let parseMetaGlobal mg = 
            { username = "username" |> tryGetString mg
              payload  = "payload" |> tryGetString mg |> parseMetaGlobalPayload
              id       = "id" |> tryGetString mg
              modified = "modified" |> tryGetString mg |> (float) }
        try 
            fetchMetaGlobal secrets.username secrets.password
            |> JsonValue.Parse
            |> parseMetaGlobal
            |> Some
        with | _ -> None


[<AutoOpen>]
module Collections =

    // https://github.com/mikerowehl/firefox-sync-client-php/blob/master/sync.php#L94
    let fetchFullCollection username password collection =
        let url = (clusterURL username) + "1.1/" + username + "/storage/" + collection + "?full=1"
        fetchUrlResponse url "GET" (Some (username, password)) None None None


    let parseEncryptedCollectionArray collection = 
        try
            collection
            |> JsonValue.Parse 
            |> fun x -> x.AsArray() 
            |> Array.map string 
            |> Array.map (fun x -> JsonValue.Parse x) 
            |> Array.map (fun x -> (x?payload).AsString())
            |> Array.map (fun x -> JsonValue.Parse x)
            |> Array.map (fun x -> { iv         = x?IV.AsString()
                                     ciphertext = x?ciphertext.AsString()
                                     hmac       = x?hmac.AsString() } )
        with | _ -> [||]


    let getFirstCryptoKey (cryptokeys : CryptoKeys) = 
        match cryptokeys.``default`` with
        | [||] -> [||]
        | _ as x -> x.[0]


    let decryptCollectionArray cryptokeys collection = 
        try
            let key = cryptokeys |> getFirstCryptoKey
            [|for b in collection do yield DecryptAES b.ciphertext key (b.iv |> Convert.FromBase64String) |]
            |> Array.map keepAsciiPrintableChars
        with | _ -> [||]


    let getDecryptedCollection (secrets : Secret) cryptokeys collection = 
        try
            collection
            |> fetchFullCollection secrets.username secrets.password 
            |> parseEncryptedCollectionArray
            |> decryptCollectionArray cryptokeys
        with | _ -> [||]       


    let getBookmarks secrets cryptokeys =
        let parseBookmark bm = 
            { id            = "id" |> tryGetString bm |> (WeaveGUID)
              ``type``      = "type" |> tryGetString bm
              title         = "title" |> tryGetString bm
              parentName    = "parentName" |> tryGetString bm
              bmkUri        = "bmkUri" |> tryGetString bm |> (URI)
              tags          = "tags" |> tryGetArray bm |> Array.map (fun x -> x.AsString())
              keyword       = "keyword" |> tryGetString bm
              description   = "description" |> tryGetString bm
              loadInSidebar = "loadInSidebar" |> tryGetBoolean bm false
              parentid      = "parentid" |> tryGetString bm |> (WeaveGUID)
              children      = "children" |> tryGetArray bm |> Array.map (fun x -> x.AsString() |> (WeaveGUID)) }
        try 
            "bookmarks" 
            |> getDecryptedCollection secrets cryptokeys
            |> Array.map JsonValue.Parse
            |> Array.map parseBookmark
        with | _ -> [||]


module Test = 
    
    let s = Secrets.secrets

    // base32Decode

    // https://docs.services.mozilla.com/sync/storageformat5.html
    // 
    //"Y4NKPS6YXAVI75XNUVODSR472I" 
    // Python: \xc7\x1a\xa7\xcb\xd8\xb8\x2a\x8f\xf6\xed\xa5\x5c\x39\x47\x9f\xd2
    if ("Y4NKPS6YXAVI75XNUVODSR472I" |> base32'8'9Decode |> bytesToHex) <> [|"c7"; "1a"; "a7"; "cb"; "d8"; "b8"; "2a"; "8f"; "f6"; "ed"; "a5"; "5c"; "39"; "47"; "9f"; "d2"|] then 
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
    let ff_skb' = syncKeyBundle "johndoe@example.com" ("Y4NKPS6YXAVI75XNUVODSR472I" |> base32'8'9Decode)
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

    
    // writeCryptoKeysToDisk

    try
        writeCryptoKeysToDisk Secrets.secrets.username Secrets.secrets.password None
    with | _ -> failwith "writeCryptoKeysToDisk failed"


    // getRecordFields/getRecordField

    let x = {value = "value"; name = "name"; ``type`` = "type"; }

    let x' = getRecordFields x

    let x'' = { value = getRecordField x x'.[0]; name = getRecordField x x'.[1]; ``type`` = getRecordField x x'.[2] }

    if x <> x'' then 
        failwith "getRecordFields/getRecordField failed"


    // GeneralInfo

    try
        let ic = fetchInfoCollections s.username s.password
        let iq = fetchInfoQuota s.username s.password
        let icu = fetchInfoCollectionUsage s.username s.password
        let icc = fetchInfoCollectionCounts s.username s.password
        "result" |> ignore
    with 
    | _ -> failwith "GeneralInfo failed" 
  
    
    // Collections

    let bm = getBookmarks Secrets.secrets (getCryptokeysFromDisk Secrets.secrets None)
    if bm.Length < 800 then 
        failwith "Collection Bookmark failed (retrieve bookmarks)"

    let bm' = bm |> Array.filter (fun x -> if x.children <> [||] then true else false)
    if bm'.Length < 40 then 
        failwith "Collection Bookmark failed (select children)"

    let bm'' = bm |> Array.filter (fun x -> if x.id = (WeaveGUID) "dkqtmNFIvhbg" then true else false)
    if bm''.Length <> 1 then 
        failwith "Collection Bookmark failed (select by id)"

    let bm''' = bm |> Array.filter (fun x -> if x.tags <> [||] then true else false)
    if bm'''.Length < 1 then 
        failwith "Collection Bookmark failed (select tags)"

    let mg = getMetaGlobal Secrets.secrets
    
