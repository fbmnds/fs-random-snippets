// apdapted from http://blogs.msdn.com/b/lukeh/archive/2010/09/05/twitter-oauth-in-f.aspx, Luke Hoban
// refer also to http://sergeytihon.wordpress.com/2012/07/22/linkedin-oauth-in-f/, Sergey Tihon

open System
open System.IO
open System.Net
open System.Security.Cryptography
open System.Text

#r @"C:\Program Files\Common Files\Microsoft Shared\Visual Studio\12.0\Newtonsoft.Json.dll"
open Newtonsoft.Json

#r @"C:\Program Files\Common Files\Microsoft Shared\Visual Studio\12.0\FSharp.Data.dll"
open FSharp.Data

// Twitter OAuth Constants

type Secret = { consumerKey : string;
                consumerSecret : string;
                accessToken : string;
                accessTokenSecret : string }

let secret =
    Environment.GetEnvironmentVariable("HOME") + @"\.ssh\secret.json"
    |> File.ReadAllText

let s = JsonConvert.DeserializeObject<Secret> secret

let requestTokenURI = "https://api.twitter.com/oauth/request_token"
let accessTokenURI = "https://api.twitter.com/oauth/access_token"
let authorizeURI = "https://api.twitter.com/oauth/authorize"
let verifyCredentialsURI = "https://api.twitter.com/1.1/account/verify_credentials.json"

// Utilities

let unreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
let urlEncode str =
    String.init (String.length str) (fun i ->
        let symbol = str.[i]
        if unreservedChars.IndexOf(symbol) = -1 then
            "%" + String.Format("{0:X2}", int symbol)
        else
            string symbol)

// Core Algorithms
let hmacsha1 signingKey str =
    let converter = new HMACSHA1(Encoding.ASCII.GetBytes(signingKey : string))
    let inBytes = Encoding.ASCII.GetBytes(str : string)
    let outBytes = converter.ComputeHash(inBytes)
    Convert.ToBase64String(outBytes)

let compositeSigningKey consumerSecret tokenSecret =
    urlEncode(consumerSecret) + "&" + urlEncode(tokenSecret)

let baseString httpMethod baseUri queryParameters =
    httpMethod + "&" +
    urlEncode(baseUri) + "&" +
      (queryParameters
       |> Seq.sortBy (fun (k,v) -> k)
       |> Seq.map (fun (k,v) -> urlEncode(k)+"%3D"+urlEncode(v))
       |> String.concat "%26")

let createAuthorizeHeader queryParameters =
    let headerValue =
        "OAuth " +
        (queryParameters
         |> Seq.map (fun (k,v) -> urlEncode(k)+"\x3D\""+urlEncode(v)+"\"")
         |> String.concat ",")
    headerValue

let currentUnixTime() =
  floor (DateTime.UtcNow - DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds
  |> int64
  |> sprintf "%d"

/// Request a token from LinkedIn and return:
/// oauth_token, oauth_token_secret, oauth_callback_confirmed
let requestToken() =
    let signingKey = compositeSigningKey s.consumerSecret ""

    let queryParameters =
        ["oauth_callback", "oob";
         "oauth_consumer_key", s.consumerKey;
         "oauth_nonce", System.Guid.NewGuid().ToString().Substring(24);
         "oauth_signature_method", "HMAC-SHA1";
         "oauth_timestamp", currentUnixTime();
         "oauth_version", "1.0"]

    let signingString = baseString "POST" requestTokenURI queryParameters
    let oauth_signature = hmacsha1 signingKey signingString

    let realQueryParameters = ("oauth_signature", oauth_signature)::queryParameters

    let req = WebRequest.Create(requestTokenURI, Method="POST")
    let headerValue = createAuthorizeHeader realQueryParameters
    req.Headers.Add(HttpRequestHeader.Authorization, headerValue)

    let resp = req.GetResponse()
    let stream = resp.GetResponseStream()
    let txt = (new StreamReader(stream)).ReadToEnd()

    let parts = txt.Split('&')
    (parts.[0].Split('=').[1],
     parts.[1].Split('=').[1],
     parts.[2].Split('=').[1] = "true")

/// Get an access token from LinkedIn and returns:
/// oauth_token, oauth_token_secret
let accessToken token tokenSecret verifier =
    let signingKey = compositeSigningKey s.consumerSecret tokenSecret

    let queryParameters =
        ["oauth_consumer_key", s.consumerKey;
         "oauth_nonce", System.Guid.NewGuid().ToString().Substring(24);
         "oauth_signature_method", "HMAC-SHA1";
         "oauth_token", token;
         "oauth_timestamp", currentUnixTime();
         "oauth_verifier", verifier;
         "oauth_version", "1.0"]

    let signingString = baseString "POST" accessTokenURI queryParameters
    let oauth_signature = hmacsha1 signingKey signingString

    let realQueryParameters = ("oauth_signature", oauth_signature)::queryParameters

    let req = WebRequest.Create(accessTokenURI, Method="POST")
    let headerValue = createAuthorizeHeader realQueryParameters
    req.Headers.Add(HttpRequestHeader.Authorization, headerValue)

    let resp = req.GetResponse()
    let stream = resp.GetResponseStream()
    let txt = (new StreamReader(stream)).ReadToEnd()

    let parts = txt.Split('&')
    (parts.[0].Split('=').[1],
     parts.[1].Split('=').[1])

/// Compute the 'Authorization' header for the given request data
let authHeaderAfterAuthenticated url httpMethod token tokenSecret queryParams =
    let signingKey = compositeSigningKey s.consumerSecret tokenSecret

    let queryParameters =
        ["oauth_consumer_key", s.consumerKey;
         "oauth_nonce", System.Guid.NewGuid().ToString().Substring(24);
         "oauth_signature_method", "HMAC-SHA1";
         "oauth_token", token;
         "oauth_timestamp", currentUnixTime();
         "oauth_version", "1.0"]

    let signingQueryParameters =
        List.append queryParameters queryParams

    let signingString = baseString httpMethod url signingQueryParameters
    let oauth_signature = hmacsha1 signingKey signingString
    let realQueryParameters = ("oauth_signature", oauth_signature)::queryParameters
    let headerValue = createAuthorizeHeader realQueryParameters
    headerValue

/// Add an Authorization header to an existing WebRequest
let addAuthHeaderForUser (webRequest : WebRequest) token tokenSecret queryParams =
    let url = webRequest.RequestUri.ToString()
    let httpMethod = webRequest.Method
    let header = authHeaderAfterAuthenticated url httpMethod token tokenSecret queryParams
    webRequest.Headers.Add(HttpRequestHeader.Authorization, header)

type System.Net.WebRequest with
    /// Add an Authorization header to the WebRequest for the provided user authorization tokens and query parameters
    member this.AddOAuthHeader(userToken, userTokenSecret, queryParams) =
        addAuthHeaderForUser this userToken userTokenSecret queryParams

let captureOAuth() =
    // Compute URL to send user to to allow our app to connect with their credentials,
    // then open the browser to have them accept
    let oauth_token'', oauth_token_secret'', oauth_callback_confirmed = requestToken()
    let url = authorizeURI + "?oauth_token=" + oauth_token''
    System.Diagnostics.Process.Start("iexplore.exe", url) |> ignore
    (oauth_token'', oauth_token_secret'')

    // *******NOTE********:
    // Get the 7 digit number from the web page, pass it to the function below to get oauth_token
    // Sample result if things go okay:
    // val oauth_token_secret' : string = "9e571e13-d054-44e6-956a-415ab3ee6d23"
    // val oauth_token' : string = "044da520-0edc-4083-a061-74e115712b61"
    // let oauth_token, oauth_token_secret = accessToken oauth_token'' oauth_token_secret'' ("3030558")


let getTweet ((oauth_token', oauth_token_secret'), pin) =
    // Test 1:
    let oauth_token, oauth_token_secret = accessToken oauth_token' oauth_token_secret' pin
    let streamSampleUrl2 = "https://api.twitter.com/1.1/statuses/home_timeline.json"
    let req = WebRequest.Create(streamSampleUrl2)
    req.AddOAuthHeader(oauth_token, oauth_token_secret, [])
    let resp = req.GetResponse()
    let strm = resp.GetResponseStream()
    let text = (new StreamReader(strm)).ReadToEnd()
    text

let postTweet ((oauth_token', oauth_token_secret'), pin) =
    // Test 2:
    let oauth_token, oauth_token_secret = accessToken oauth_token' oauth_token_secret' pin
    System.Net.ServicePointManager.Expect100Continue <- false
    let statusUrl = "https://api.twitter.com/1.1/statuses/update.json"
    let request = WebRequest.Create (statusUrl, Method="POST") :?> HttpWebRequest
    let r = System.Random(10)
    let tweet =
        "F# scripted tweet +++ " + sprintf "%d" (r.Next())
        |> urlEncode
    request.AddOAuthHeader(oauth_token,oauth_token_secret,["status",tweet])
    let bodyStream = request.GetRequestStream()
    let bodyWriter = new StreamWriter(bodyStream)
    bodyWriter.Write("status=" + tweet)
    bodyWriter.Close()
    let resp = request.GetResponse()
    let strm = resp.GetResponseStream()
    let text = (new StreamReader(strm)).ReadToEnd()
    text


// let parms = captureOAuth();;

// getTweet (parms, "0824995");;

// postTweet (parms, "8909517");;


let verifyCredentials() =
  let queryParameters =
      ["oauth_version","1.0";
       "oauth_consumer_key",s.consumerKey;
       "oauth_nonce",System.Guid.NewGuid().ToString().Substring(24);
       "oauth_signature_method","HMAC-SHA1";
       "oauth_timestamp",currentUnixTime();
       "oauth_token",s.accessToken ]
  let signingString = baseString "GET" verifyCredentialsURI queryParameters
  let signingKey = compositeSigningKey s.consumerSecret s.accessTokenSecret
  let oauth_signature = hmacsha1 signingKey signingString
  let AuthorizationHeader = ("oauth_signature",oauth_signature) :: queryParameters |> createAuthorizeHeader
  System.Net.ServicePointManager.Expect100Continue <- false
  let req = WebRequest.Create(verifyCredentialsURI)
  //req.AddOAuthHeader(s.accessToken, s.accessTokenSecret, [])
  req.Headers.Add("Authorization",AuthorizationHeader)
  req.Method <- "GET"
  req.ContentType <- "application/x-www-form-urlencoded"
  let resp = req.GetResponse()
  let strm = resp.GetResponseStream()
  let text = (new StreamReader(strm)).ReadToEnd()
  text
