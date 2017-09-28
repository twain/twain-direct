///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.Support.ApiCmd
//
// ApiCmd is the payload for a TWAIN Local command.  We must to support multiple
// concurrent API calls, this means multi-threading, so we need to be able to
// pass the context of a single command up and down the stack.  This is why it's
// accessible at the dispatcher level.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    30-Jun-2015     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2015-2017 Kodak Alaris Inc.
//
//  Permission is hereby granted, free of charge, to any person obtaining a
//  copy of this software and associated documentation files (the "Software"),
//  to deal in the Software without restriction, including without limitation
//  the rights to use, copy, modify, merge, publish, distribute, sublicense,
//  and/or sell copies of the Software, and to permit persons to whom the
//  Software is furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
//  THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
//  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
//  DEALINGS IN THE SOFTWARE.
///////////////////////////////////////////////////////////////////////////////////////

// Helpers...
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Net.Mime;
using System.Text;
using System.Threading;

namespace TwainDirect.Support
{
    /// <summary>
    /// Manage a single command as it moves through the system, this includes
    /// its lifecycle and responses, including errors...
    /// </summary>
    public sealed class ApiCmd : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Use this constructor when we need an apicmd, but don't plan to use
        /// it to talk to anything...
        /// </summary>
        public ApiCmd()
        {
            HttpListenerContext httplistenercontext = null;
            ApiCmdHelper(null, null, ref httplistenercontext, null, null);
        }

        /// <summary>
        /// Use this constructor when initiating a command on the client
        /// side, which means we don't have any JSON data or an HTTP
        /// context...
        /// </summary>
        /// <param name="a_dnssddeviceinfo">the device we're talking to</param>
        public ApiCmd(Dnssd.DnssdDeviceInfo a_dnssddeviceinfo)
        {
            HttpListenerContext httplistenercontext = null;
            ApiCmdHelper(a_dnssddeviceinfo, null, ref httplistenercontext, null, null);
        }

        /// <summary>
        /// Use this constructor when initiating a waitForEvents on the
        /// client side, which means we don't have any JSON data or an HTTP
        /// context...
        /// </summary>
        /// <param name="a_dnssddeviceinfo">the device we're talking to</param>
        /// <param name="a_waitforeventprocessingcallback">callback for each event</param>
        /// <param name="a_objectWaitforeventprocessingcallback">object to pass to the callback</param>
        public ApiCmd
        (
            Dnssd.DnssdDeviceInfo a_dnssddeviceinfo,
            TwainLocalScanner.WaitForEventsProcessingCallback a_waitforeventprocessingcallback,
            object a_objectWaitforeventprocessingcallback
        )
        {
            HttpListenerContext httplistenercontext = null;
            ApiCmdHelper(a_dnssddeviceinfo, null, ref httplistenercontext, a_waitforeventprocessingcallback, a_objectWaitforeventprocessingcallback);
        }

        /// <summary>
        /// Use this constructor when making a copy of ApiCmd as part
        /// of waitForEvents...
        /// </summary>
        /// <param name="a_apicmd">object we're copying from</param>
        /// <param name="a_szSessionState">session state found in data</param>
        /// <param name="a_lSessionRevision">session revision found in data</param>
        public ApiCmd(ApiCmd a_apicmd, out string a_szSessionState, out long a_lSessionRevision)
        {
            bool blSuccess;
            long lResponseCharacterOffset;
            JsonLookup jsonlookup = new JsonLookup();

            // Create our object...
            HttpListenerContext httplistenercontext = null;
            ApiCmdHelper(a_apicmd.m_dnssddeviceinfo, null, ref httplistenercontext, a_apicmd.m_waitforeventprocessingcallback, a_apicmd.m_objectWaitforeventprocessingcallback);

            // If we have JSON in the response, and it has session
            // data, then we need to collect the session revision
            // and the state...
            a_szSessionState = "";
            a_lSessionRevision = 0;
            jsonlookup = new JsonLookup();
            blSuccess = jsonlookup.Load(a_apicmd.GetHttpResponseData(), out lResponseCharacterOffset);
            if (blSuccess)
            {
                if (jsonlookup.Get("results.success", false) == "true")
                {
                    // Look for the last values...
                    for (int ii = 0; true; ii++)
                    {
                        string szTmp;
                        string szEvent = "results.events[" + ii + "]";
                        if (string.IsNullOrEmpty(jsonlookup.Get(szEvent, false)))
                        {
                            break;
                        }

                        // Get the state...
                        szTmp = jsonlookup.Get(szEvent + ".session.state", false);
                        if (!string.IsNullOrEmpty(szTmp))
                        {
                            a_szSessionState = szTmp;
                        }

                        // Get the revision...
                        szTmp = jsonlookup.Get(szEvent + ".session.revision", false);
                        if (!string.IsNullOrEmpty(szTmp))
                        {
                            long.TryParse(szTmp, out a_lSessionRevision);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initialize the object in response a received command. The
        /// JsonLookup object contains the command.  The HttpListenerContext
        /// is the object that delivered the command, and what we use to get
        /// our HttpListenerResponse object for making our reply.
        /// </summary>
        /// <param name="a_dnssddeviceinfo">the device we're talking to</param>
        /// <param name="a_jsonlookup">the command data or null</param>
        /// <param name="a_httplistenercontext">the object that delivered the command</param>
        public ApiCmd
        (
            Dnssd.DnssdDeviceInfo a_dnssddeviceinfo,
            JsonLookup a_jsonlookup,
            ref HttpListenerContext a_httplistenercontext
        )
        {
            ApiCmdHelper(a_dnssddeviceinfo, a_jsonlookup, ref a_httplistenercontext, null, null);
        }

        /// <summary>
        /// Destructor...
        /// </summary>
        ~ApiCmd()
        {
            Dispose(false);
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Set the device response...
        /// </summary>
        /// <param name="a_blSuccess">true if successful</param>
        /// <param name="a_szResponseCode">things like invalidJson or invalidValue</param>
        /// <param name="a_lResponseCharacterOffset">index of a syntax error or -1</param>
        /// <param name="a_szResponseData">free form text about the problem</param>
        /// <param name="a_iResponseHttpStatusOverride">override the status if not 0</param>
        public void DeviceResponseSetStatus
        (
            bool a_blSuccess,
            string a_szResponseCode,
            long a_lResponseCharacterOffset,
            string a_szResponseData,
            int a_iResponseHttpStatusOverride = 0
        )
        {
            // Override the m_httpresponsedata.iResponseHttpStatus, we typically do this in
            // situations where we know we can't possibly have a valid value...
            if (a_iResponseHttpStatusOverride != 0)
            {
                m_httpresponsedata.iResponseHttpStatus = a_iResponseHttpStatusOverride;
            }

            // For non-successful status the text is our data, or if we're
            // overriding the status...
            if ((m_httpresponsedata.iResponseHttpStatus != 200) || (a_iResponseHttpStatusOverride != 0))
            {
                m_httpresponsedata.szResponseData = a_szResponseData;
            }

            // Squirrel away the rest of it...
            m_httpresponsedata.szTwainLocalResponseCode = a_szResponseCode;
            m_httpresponsedata.lResponseCharacterOffset = a_lResponseCharacterOffset;
        }

        /// <summary>
        /// Check if an event should be discarded...
        /// </summary>
        /// <param name="a_lSessionRevision">current session revisio</param>
        /// <returns></returns>
        public bool DiscardEvent(long a_lSessionRevision)
        {
            if (a_lSessionRevision >= m_lSessionRevision)
            {
                return (true);
            }
            return (false);
        }

        /// <summary>
        /// Return what we know about the scanner we're talking to...
        /// </summary>
        /// <returns>a point to the device info</returns>
        public Dnssd.DnssdDeviceInfo GetDnssdDeviceInfo()
        {
            return (m_dnssddeviceinfo);
        }

        /// <summary>
        /// Return the response data...
        /// </summary>
        /// <returns>the HTTP response</returns>
        public string GetResponseData()
        {
            return (m_httpresponsedata.szResponseData);
        }

        /// <summary>
        /// Return the facility that threw an error for this
        /// command...
        /// </summary>
        /// <returns>facility that detected an error</returns>
        public ApiErrorFacility GetApiErrorFacility()
        {
            return (m_apierrorfacility);
        }

        /// <summary>
        /// Return the error code(s)...
        /// </summary>
        /// <returns>error codes</returns>
        public string[] GetApiErrorCodes()
        {
            return (m_aszApiErrorCodes);
        }

        /// <summary>
        /// Return the error description(s)...
        /// </summary>
        /// <returns>error descriptions</returns>
        public string[] GetApiErrorDescriptions()
        {
            return (m_aszApiErrorDescriptions);
        }

        /// <summary>
        /// Return the HTTP response status...
        /// </summary>
        /// <returns>the HTTP response status</returns>
        public int GetResponseStatus()
        {
            return (m_httpresponsedata.iResponseHttpStatus);
        }

        /// <summary>
        /// Return the task reply...
        /// </summary>
        /// <returns>task reply in base64</returns>
        public string GetTaskReply()
        {
            return (m_szTaskReply);
        }

        /// <summary>
        /// Return the URI for this command...
        /// </summary>
        /// <returns>/privet/* URI</returns>
        public string GetUri()
        {
            return (m_httplistenerdata.szUri);
        }

        /// <summary>
        /// Get a transaction object for this http request/response...
        /// </summary>
        /// <returns></returns>
        public Transaction GetTransaction()
        {
            Transaction transaction = new Transaction(this);
            return (transaction);
        }

        /// <summary>
        /// Return the full URI for this command...
        /// </summary>
        /// <returns>method + uri</returns>
        public string GetUriFull()
        {
            return (m_httplistenerdata.szUriFull);
        }

        /// <summary>
        /// Return the unique command id for this command...
        /// </summary>
        /// <returns>the command id</returns>
        public string GetCommandId()
        {
            if (m_jsonlookupReceived == null)
            {
                return ("");
            }
            return (m_jsonlookupReceived.Get("commandId",false));
        }

        /// <summary>
        /// Return the scanner.command name for this command...
        /// </summary>
        /// <returns>the command name (scanner.name)</returns>
        public string GetCommandName()
        {
            if (m_jsonlookupReceived == null)
            {
                return ("");
            }
            return (m_jsonlookupReceived.Get("method"));
        }

        /// <summary>
        /// Are will still capturing new images?
        /// </summary>
        /// <returns>try if capturing is done</returns>
        public bool GetDoneCapturing()
        {
            return (m_sessiondata.blSessionDoneCapturing);
        }

        /// <summary>
        /// Return the image blocks drained flag for this command...
        /// </summary>
        /// <returns>true if we're out of images</returns>
        public bool GetImageBlocksDrained()
        {
            return (m_blSessionImageBlocksDrained);
        }

        /// <summary>
        /// Return the end of job flag for this command...
        /// </summary>
        /// <returns>true if we're out of images</returns>
        public HttpReplyStyle GetHttpReplyStyle()
        {
            return (m_httpreplystyle);
        }

        /// <summary>
        /// Return the HTTP request headers...
        /// </summary>
        /// <returns>string with all the data or null</returns>
        public string[] GetRequestHeaders()
        {
            return (m_httprequestdata.aszRequestHeaders);
        }

        /// <summary>
        /// Return the total number of bytes tranferred in this
        /// response...
        /// </summary>
        /// <returns>the number of bytes we got</returns>
        public long GetResponseBytesXferred()
        {
            return (m_lResponseBytesXferred);
        }

        /// <summary>
        /// Return the HTTP response headers...
        /// </summary>
        /// <returns>string with all the data or null</returns>
        public string[] GetResponseHeaders()
        {
            return (m_httpresponsedata.aszResponseHeaders);
        }

        /// <summary>
        /// Return the HTTP response headers for JSON data in a
        /// multipart/mixed block...
        /// </summary>
        /// <returns>string with all the data or null</returns>
        public string[] GetResponseMultipartHeadersJson()
        {
            return (m_aszMultipartHeadersJson);
        }

        /// <summary>
        /// Return the HTTP response headers for thumbnail data in a
        /// multipart/mixed block...
        /// </summary>
        /// <returns>string with all the data or null</returns>
        public string[] GetResponseMultipartHeadersThumbnail()
        {
            return (m_aszMultipartHeadersPdfThumbnail);
        }

        /// <summary>
        /// Return the HTTP response headers for image data in a
        /// multipart/mixed block...
        /// </summary>
        /// <returns>string with all the data or null</returns>
        public string[] GetResponseMultipartHeadersImage()
        {
            return (m_aszMultipartHeadersPdfImage);
        }

        /// <summary>
        /// Returns the array of image block numbers...
        /// </summary>
        /// <returns>image block numbers (ex: 1, 2)</returns>
        public string GetImageBlocks()
        {
            // We have no images...
            if (string.IsNullOrEmpty(m_szImageBlocks))
            {
                return ("");
            }

            // We have imageBlocks, remove whitespace...
            return (m_szImageBlocks.Replace(" ",""));
        }

        /// <summary>
        /// Our session revision number...
        /// </summary>
        /// <returns>session revision number</returns>
        public long GetSessionRevision()
        {
            return (m_lSessionRevision);
        }

        /// <summary>
        /// The name of the event (ex: imageBlocks)
        /// </summary>
        /// <returns>name of the event</returns>
        public string GetEventName()
        {
            return (m_szEventName);
        }

        /// <summary>
        /// The state of the session, as of this command...
        /// </summary>
        /// <returns>the session state as a string</returns>
        public string GetSessionState()
        {
            if (string.IsNullOrEmpty(m_szSessionState))
            {
                return ("noSession");
            }
            return (m_szSessionState);
        }

        /// <summary>
        /// False if we have a problem with the scanner...
        /// </summary>
        /// <returns>false if scanner has a boo-boo</returns>
        public bool GetSessionStatusSuccess()
        {
            return (m_sessiondata.blSessionStatusSuccess);
        }

        /// <summary>
        /// The reason the scanner is unhappy...
        /// </summary>
        /// <returns>the nature of the boo-boo</returns>
        public string GetSessionStatusDetected()
        {
            return (m_sessiondata.szSessionStatusDetected);
        }

        /// <summary>
        /// Returns the array of image block numbers in a format that allows
        /// it to be dropped as-is into a results object (part of the return
        /// for a session object)...
        /// </summary>
        /// <param name="a_szSessionState">session state for this data</param>
        /// <returns>an array of image block numbers (ex: [ 1, 2 ])</returns>
        public string GetImageBlocksJson(string a_szSessionState)
        {
            // We have no data, but that doesn't mean that we're
            // done.  What we report depends on our state...
            switch (a_szSessionState)
            {
                // Not a scanning state, so don't report this stuff...
                default:
                    return ("");

                // We're capturing or draining...
                case "capturing":
                case "draining":

                    // We've run out of images...
                    if (m_blSessionImageBlocksDrained)
                    {
                        return
                        (
                            "\"doneCapturing\":" + ((m_sessiondata.blSessionDoneCapturing) ? "true," : "false,") +
                            "\"imageBlocksDrained\":true," +
                            "\"imageBlocks\":[],"
                        );
                    }

                    // We may have more images coming...
                    return
                    (
                        "\"doneCapturing\":" + ((m_sessiondata.blSessionDoneCapturing) ? "true," : "false,") +
                        "\"imageBlocksDrained\":false," +
                        "\"imageBlocks\":" + (string.IsNullOrEmpty(m_szImageBlocks) ? "[]" : m_szImageBlocks) + ","
                    );
            }
        }

        /// <summary>
        /// Returns the imagefilename for this command (this can be null
        /// or empty)...
        /// </summary>
        /// <returns>the filename or null</returns>
        public string GetImageFile()
        {
            return (m_szImageFile);
        }

        /// <summary>
        /// Returns the thumbnail file for this command (this can be null
        /// or empty)...
        /// </summary>
        /// <returns>the filename or null</returns>
        public string GetThumbnailFile()
        {
            return (m_szThumbnailFile);
        }

        /// <summary>
        /// Get the data from the JSON object we received...
        /// </summary>
        /// <param name="a_szJsonKey">the key to lookup</param>
        /// <returns>the data we found</returns>
        public string GetJsonReceived(string a_szJsonKey)
        {
            // Nope, we ain't got one of those...
            if (m_jsonlookupReceived == null)
            {
                return ("");
            }

            // Return whatever we found...
            return (m_jsonlookupReceived.Get(a_szJsonKey, false));
        }

        /// <summary>
        /// Set the facility that threw an error for this
        /// command...
        /// </summary>
        /// <param name="a_apierrorfacility">facility that detected an error</param>
        public void SetApiErrorFacility(ApiErrorFacility a_apierrorfacility)
        {
            m_apierrorfacility = a_apierrorfacility;
        }

        /// <summary>
        /// Set the error codes...
        /// </summary>
        /// <param name="a_aszApiErrorCode">error code</param>
        public void AddApiErrorCode(string a_aszApiErrorCode)
        {
            if (m_aszApiErrorCodes == null)
            {
                m_aszApiErrorCodes = new string[1];
                m_aszApiErrorCodes[0] = a_aszApiErrorCode;
            }
            else
            {
                string[] asz = new string[m_aszApiErrorCodes.Length + 1];
                Array.Copy(m_aszApiErrorCodes, asz, m_aszApiErrorCodes.Length);
                asz[m_aszApiErrorCodes.Length] = a_aszApiErrorCode;
                m_aszApiErrorCodes = asz;
            }
        }

        /// <summary>
        /// Set the error descriptions...
        /// </summary>
        /// <param name="a_aszApiErrorDescriptions">error description</param>
        public void AddApiErrorDescription(string a_aszApiErrorDescription)
        {
            if (m_aszApiErrorDescriptions == null)
            {
                m_aszApiErrorDescriptions = new string[1];
                m_aszApiErrorDescriptions[0] = a_aszApiErrorDescription;
            }
            else
            {
                string[] asz = new string[m_aszApiErrorDescriptions.Length + 1];
                Array.Copy(m_aszApiErrorDescriptions, asz, m_aszApiErrorDescriptions.Length);
                asz[m_aszApiErrorDescriptions.Length] = a_aszApiErrorDescription;
                m_aszApiErrorDescriptions = asz;
            }
        }

        /// <summary>
        /// Store session information at the time of this event...
        /// </summary>
        /// <param name="a_szEventName"></param>
        /// <param name="a_szSessionState"></param>
        /// <param name="a_lSessionRevision"></param>
        public void SetEvent(string a_szEventName, string a_szSessionState, long a_lSessionRevision)
        {
            m_szEventName = a_szEventName;
            m_szSessionState = a_szSessionState;
            m_lSessionRevision = a_lSessionRevision;
        }

        /// <summary>
        /// Set the imageblocksdrained flag...
        /// </summary>
        /// <param name="a_blSessionImageBlocksDrained"></param>
        public void SetSessionImageBlocksDrained(bool a_blSessionImageBlocksDrained)
        {
            m_blSessionImageBlocksDrained = a_blSessionImageBlocksDrained;
        }

        /// <summary>
        /// Get our caller's hostname...
        /// </summary>
        /// <returns>the hostname</returns>
        public string HttpGetCallersHostName()
        {
            return (m_httplistenerdata.httplistenercontext.Request.UserHostName);
        }

        /// <summary>
        /// Get the send command...
        /// </summary>
        /// <returns>send command</returns>
        public string GetSendCommand()
        {
            return (m_szSendCommand);
        }

        /// <summary>
        /// Get the parameters task...
        /// </summary>
        /// <returns>get the task</returns>
        public string GetParametersTask()
        {
            return (m_jsonlookupReceived.Get("params.task"));
        }

        /// <summary>
        /// Return the metadata, if we have any. Given the way we're using
        /// this function, toss in a trailing comma to make the caller's
        /// life easier...
        /// </summary>
        /// <returns>metadata with a comma, or an empty string</returns>
        public string GetMetadata()
        {
            if (string.IsNullOrEmpty(m_szMetadata))
            {
                return ("");
            }
            return (m_szMetadata + ",");
        }

        /// <summary>
        /// Return the reply data from an HttpRequest...
        /// </summary>
        /// <returns>JSON data</returns>
        public string GetHttpResponseData()
        {
            if (string.IsNullOrEmpty(m_httpresponsedata.szResponseData))
            {
                return ("");
            }
            return (m_httpresponsedata.szResponseData);
        }

        /// <summary>
        /// Return the status from an HttpRequest...
        /// </summary>
        /// <returns>status</returns>
        public WebExceptionStatus HttpStatus()
        {
            return (m_webexceptionstatus);
        }

        /// <summary>
        /// Abort a pending HTTP request issued by a client,
        /// this is usually going to hit waitForEvents...
        /// </summary>
        public void HttpAbortClientRequest(bool a_blTimeout)
        {
            m_blTimeout = a_blTimeout;
            m_blAbortClientRequest = true;
            if (m_httprequestdata.httpwebrequest != null)
            {
                m_httprequestdata.httpwebrequest.Abort();
            }
        }

        /******************************************************************************************
        *******************************************************************************************
        **
        ** I'm leaving this code here for now (07-Sep-2017) because it's easier to follow
        ** than the async code, and because if we ever switch to .NET 4.5 which has better
        ** async functions, it might be nice to have this as a reference.  Just be aware
        ** that it's not being maintained, so as time passes bug fixes made to the current
        ** async code won't be reflected here...
        **
        /// <summary>
        /// We make decisions about how the HttpRequestAttempt went.  It keeps
        /// the code cleaner this way, especially for the retry loop.
        /// </summary>
        /// <param name="a_szReason">reason for the call, for logging</param>
        /// <param name="a_szUri">our target</param>
        /// <param name="a_szMethod">http method (ex: POST, DELETE...)</param>
        /// <param name="a_aszHeader">array of headers to send or null</param>
        /// <param name="a_szData">data to send or null</param>
        /// <param name="a_szUploadFile">upload data from a file</param>
        /// <param name="a_szOutputFile">redirect the data to a file</param>
        /// <param name="a_iTimeout">timeout in milliseconds</param>
        /// <param name="a_httpreplystyle">how the reply will be handled</param>
        /// <param name="a_blInitOnly">init only (used in error cases)</param>
        /// <returns>true on success</returns>
        public bool HttpRequestSync
        (
            string a_szReason,
            string a_szUri,
            string a_szMethod,
            string[] a_aszHeader,
            string a_szData,
            string a_szUploadFile,
            string a_szOutputFile,
            int a_iTimeout,
            HttpReplyStyle a_httpreplystyle,
            bool a_blInitOnly = false
        )
        {
            //
            // The WebRequest method of doing stuff...
            //
            int iXfer = 0;
            bool blMultipart = false;
            long lContentLength;
            long lImageBlockSeperator;
            string szUri;
            string szReply = "";
            string szMultipartBoundary = "";
            byte[] abBuffer;
            Stream stream = null;
            HttpWebResponse httpwebresponse;


            // Setup the HTTP Request
            #region Setup HTTP Request

            // Log a reason for being here...
            Log.Info("");
            Log.Info("http>>> " + a_szReason);

            // Squirrel these away...
            m_httplistenerdata.szUri = a_szUri;
            m_szSendCommand = a_szData;
            m_httpreplystyle = a_httpreplystyle;
            m_szOutputFile = a_szOutputFile;

            // Pick our URI, prefix the default server, unless the user gives us an override...
            //
            // A silent exception occurs on Webrequest.Create(), it's trapped and doesn't seem
            // to cause any problems, but on Windows if you want to make it go away, then add
            // the next two items to the registry (you only need Wow6432Node on 64-bit OSes)...
            //   HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\.NETFramework\LegacyWPADSupport dword:00000000
            //   HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\LegacyWPADSupport dword:00000000
            if (a_szUri == null)
            {
                Log.Error(a_szReason + ": a_szUri is null");
                return (false);
            }

            // For HTTPS we need a certificate for the DNS domain, I have no idea if
            // this can be done with a numeric IP, but I know it can be done with a
            // DNS name, and since we're doing mDNS in this case, we want the link local
            // name of the device...
            if (m_blUseHttps)
            {
                string szLinkLocal = m_dnssddeviceinfo.GetLinkLocal().Replace(".local.", ".local");
                szUri = "https://" + szLinkLocal + ":" + m_dnssddeviceinfo.GetPort() + a_szUri;
            }

            // Build the URI, for HTTP we can use the IP address to get to our device...
            else
            {
                szUri = "http://" + m_dnssddeviceinfo.GetIpv4() + ":" + m_dnssddeviceinfo.GetPort() + a_szUri;
            }
            m_httplistenerdata.szMethod = a_szMethod;
            m_httplistenerdata.szUriFull = szUri;

            // If all we want is to initialize the ApiCmd data, then scoot.
            // We do this to help stock the object when errors occur.
            if (a_blInitOnly)
            {
                return (false);
            }

            Log.Info("http>>> " + m_httplistenerdata.szMethod + " " + m_httplistenerdata.szUriFull);
            m_httprequestdata.httpwebrequest = (HttpWebRequest)WebRequest.Create(szUri);
            m_httprequestdata.httpwebrequest.AllowWriteStreamBuffering = true;
            m_httprequestdata.httpwebrequest.KeepAlive = true;

            // Pick our method...
            m_httprequestdata.httpwebrequest.Method = a_szMethod;

            // We'd like any data lengths done before the header, so that
            // we can offer a meaningful value for Content-Length...
            byte[] abData = null;
            if (!string.IsNullOrEmpty(a_szData))
            {
                abData = Encoding.UTF8.GetBytes(a_szData);
                m_httprequestdata.httpwebrequest.ContentLength = abData.Length;
            }

            // Add any headers we have laying about...
            if (a_aszHeader != null)
            {
                m_httprequestdata.httpwebrequest.Headers = new WebHeaderCollection();
                foreach (string szHeader in a_aszHeader)
                {
                    Log.Verbose("http>>> sendheader " + szHeader);
                    if (szHeader.ToLower().StartsWith("content-type: "))
                    {
                        m_httprequestdata.httpwebrequest.ContentType = szHeader.Remove(0, 14);
                    }
                    else
                    {
                        m_httprequestdata.httpwebrequest.Headers.Add(szHeader);
                    }
                }
            }
            m_httprequestdata.aszRequestHeaders = null;
            if (m_httprequestdata.httpwebrequest.Headers != null)
            {
                int hh = 0;
                if (abData == null)
                {
                    m_httprequestdata.aszRequestHeaders = new string[m_httprequestdata.httpwebrequest.Headers.Keys.Count];
                }
                else
                {
                    m_httprequestdata.aszRequestHeaders = new string[m_httprequestdata.httpwebrequest.Headers.Keys.Count + 1];
                    m_httprequestdata.aszRequestHeaders[hh++] = "Content-Length=" + m_httprequestdata.httpwebrequest.ContentLength;
                }
                for (int kk = 0; kk < m_httprequestdata.httpwebrequest.Headers.Keys.Count; kk++, hh++)
                {
                    if (m_httprequestdata.httpwebrequest.Headers.GetValues(kk) == null)
                    {
                        m_httprequestdata.aszRequestHeaders[hh] = m_httprequestdata.httpwebrequest.Headers.Keys.Get(kk) + "=";
                    }
                    else
                    {
                        m_httprequestdata.aszRequestHeaders[hh] = m_httprequestdata.httpwebrequest.Headers.Keys.Get(kk) + "=" + m_httprequestdata.httpwebrequest.Headers.GetValues(kk).GetValue(0);
                    }
                }
            }

            // Timeout...
            m_httprequestdata.httpwebrequest.Timeout = a_iTimeout;

            // Data we're sending...
            if (abData != null)
            {
                Log.Info("http>>> senddata " + a_szData);
                if (m_httprequestdata.httpwebrequest.ContentType == null)
                {
                    // We shouldn't be getting here...
                    m_httprequestdata.httpwebrequest.ContentType = "application/x-www-form-urlencoded";
                }
                try
                {
                    // This is where we expect to be...
                    stream = m_httprequestdata.httpwebrequest.GetRequestStream();
                    stream.Write(abData, 0, abData.Length);
                    stream.Close();
                }
                catch (WebException webexception)
                {
                    return (CollectWebException("SendData", webexception));
                }
                catch (Exception exception)
                {
                    return (CollectException("SendData", exception));
                }
            }

            // We're sending a file...
            if (a_szUploadFile != null)
            {
                Log.Info("http>>> sendfile " + a_szUploadFile);
                byte[] abFile = File.ReadAllBytes(a_szUploadFile);
                m_httprequestdata.httpwebrequest.ContentLength = abFile.Length;
                try
                {
                    stream = m_httprequestdata.httpwebrequest.GetRequestStream();
                    stream.Write(abFile, 0, abFile.Length);
                    stream.Close();
                }
                catch (WebException webexception)
                {
                    return (CollectWebException("SendFile", webexception));
                }
                catch (Exception exception)
                {
                    return (CollectException("SendFile", exception));
                }
            }

            #endregion


            // Handle the HTTP Response
            #region Handle the HTTP Response

            // Get the response...
            try
            {
                httpwebresponse = (HttpWebResponse)m_httprequestdata.httpwebrequest.GetResponse();
            }
            catch (WebException webexception)
            {
                return (CollectWebException("GetResponse", webexception));
            }
            catch (Exception exception)
            {
                return (CollectException("GetResponse", exception));
            }

            // Extra header for waitForEvents...
            if (a_httpreplystyle == HttpReplyStyle.Event)
            {
                Log.Info(" ");
                Log.Info("http>>> " + a_szReason + " (response)");
            }

            // Dump the status...
            m_httpresponsedata.iResponseHttpStatus = (int)(HttpStatusCode)httpwebresponse.StatusCode;
            Log.Info("http>>> recvsts " + m_httpresponsedata.iResponseHttpStatus + " (" + httpwebresponse.StatusCode + ")");

            // Get the response headers, if any...
            m_httpresponsedata.aszResponseHeaders = null;
            if (httpwebresponse.Headers != null)
            {
                m_httpresponsedata.aszResponseHeaders = new string[httpwebresponse.Headers.Keys.Count];
                for (int kk = 0; kk < m_httpresponsedata.aszResponseHeaders.Length; kk++)
                {
                    if (httpwebresponse.Headers.GetValues(kk) == null)
                    {
                        m_httpresponsedata.aszResponseHeaders[kk] = httpwebresponse.Headers.Keys.Get(kk) + "=";
                    }
                    else
                    {
                        m_httpresponsedata.aszResponseHeaders[kk] = httpwebresponse.Headers.Keys.Get(kk) + "=" + httpwebresponse.Headers.GetValues(kk).GetValue(0);
                    }
                }
            }

            // Dump the header info...
            if ((Log.GetLevel() & 0x0002) != 0)
            {
                // Get each header and display each value.
                NameValueCollection namevaluecollectionHeaders = httpwebresponse.Headers;
                foreach (string szKey in namevaluecollectionHeaders.AllKeys)
                {
                    string[] aszValues = namevaluecollectionHeaders.GetValues(szKey);
                    if (aszValues.Length == 0)
                    {
                        Log.Verbose("http>>> recvheader " + szKey + ": n/a");
                    }
                    else
                    {
                        foreach (string szValue in aszValues)
                        {
                            Log.Verbose("http>>> recvheader " + szKey + ": " + szValue);
                        }
                    }
                }
            }

            // Get the content length...
            lContentLength = httpwebresponse.ContentLength;

            // Get the content type...
            ContentType contenttype = new ContentType(httpwebresponse.ContentType);

            // application/json with UTF-8 is okay...
            if (contenttype.MediaType.ToLowerInvariant() == "application/json")
            {
                if (contenttype.CharSet.ToLowerInvariant() != "utf-8")
                {
                    Log.Error(a_szReason + ": application/json charset is not utf-8..." + contenttype.CharSet);
                    return (false);
                }
                blMultipart = false;
            }

            // multipart/mixed is okay, with a boundary...
            else if (contenttype.MediaType.ToLowerInvariant() == "multipart/mixed")
            {
                // Extract the boundary data...
                blMultipart = true;
                szMultipartBoundary = contenttype.Boundary;
                if (string.IsNullOrEmpty(szMultipartBoundary))
                {
                    Log.Error(a_szReason + ": bad multipart/mixed boundary...");
                    return (false);
                }

                // This is the form we expect to find in the data stream...
                szMultipartBoundary = "--" + szMultipartBoundary + "\r\n";
            }

            // Anything else is bad...
            else
            {
                Log.Error(a_szReason + ": unknown http content-type..." + contenttype.MediaType);
                return (false);
            }

            // Get the data coming back...
            try
            {
                // Grab the stream...
                stream = httpwebresponse.GetResponseStream();

                // All we have is just a JSON reply...
                if (!blMultipart)
                {
                    abBuffer = new byte[0x65536];
                    while (true)
                    {
                        int iRead = stream.Read(abBuffer, iXfer, abBuffer.Length - iXfer);
                        if (iRead == 0)
                        {
                            break;
                        }
                        iXfer += iRead;
                        if ((iRead > 0) && (iXfer >= abBuffer.Length))
                        {
                            byte[] ab = new byte[abBuffer.Length + 65536];
                            abBuffer.CopyTo(ab, 0);
                            abBuffer = ab;
                        }
                    }
                    if (iXfer > 0)
                    {
                        byte[] abReply = new byte[iXfer];
                        Buffer.BlockCopy(abBuffer, 0, abReply, 0, iXfer);
                        szReply = Encoding.UTF8.GetString(abReply, 0, iXfer);
                    }

                    // The total number of bytes transferred.  We want this number
                    // to be identical to the Content-Length for the response...
                    m_lResponseBytesXferred = iXfer;
                }

                // Else we have a multipart response, and we need to collect
                // and separate all of the data.  The data must arrive in the
                // following format, repeating as necessary to send all of
                // the data...
                //
                // --boundary + \r\n
                // Content-Type: ... + \r\n
                // Content-Length: # + \r\n
                // any other headers + \r\n
                // \r\n
                // data + \r\n
                // \r\n
                // --boundary + \r\n
                // Content-Type: ... + \r\n
                // Content-Length: # + \r\n
                // any other headers + \r\n
                // \r\n
                // data + \r\n
                // \r\n
                //
                // Getting the CRLF terminators right is part of the challenge...
                //
                // In theory we could have several parts, but in practice
                // we're only expecting two:  JSON and an image.  If we are
                // getting metadata, it will be the JSON and the thumbnail.
                // If we are reading an imageblock, it will be the JSON and
                // the image.  We'll try to set things up so that we can
                // get more bits if needed.  But I suspect that two segments
                // be easiest to support both for standard and vendor
                // specific behavior...
                else
                {
                    bool blApplicationJsonSeen = false;
                    FileStream filestreamOutputFile = null;

                    // Give us a large buffer to work with, we're going to limit
                    // transfers to this size, to avoid someone trying to get us
                    // to allocate a massive buffer...
                    abBuffer = new byte[0x200000];

                    // This is our block separator, it has the dashes and the
                    // terminating CRLF...
                    byte[] abImageBlockSeperator = Encoding.UTF8.GetBytes(szMultipartBoundary);

                    // This is our CRLF/CRLF detector that tells us that we've
                    // captured a complete header block...
                    byte[] abCRLFCRLF = new byte[] { 13, 10, 13, 10 };

                    // If we were given a filename, create the stream where we'll
                    // dump the binary data, assuming we get any binary data...
                    if (!string.IsNullOrEmpty(a_szOutputFile))
                    {
                        try
                        {
                            // Create the empty file...
                            if (!File.Exists(a_szOutputFile))
                            {
                                File.Delete(a_szOutputFile);
                            }
                            filestreamOutputFile = new FileStream(a_szOutputFile, FileMode.Create);
                        }
                        catch (Exception exception)
                        {
                            Log.Error(a_szReason + ": http delete or streamwriter failed..." + a_szOutputFile + ", " + exception.Message);
                            return (false);
                        }
                    }

                    // Okay, here's where the fun begins.  We'll loop around in here
                    // until we get all the data, or until something horrible happens...
                    long lRead = 0;
                    long lOffset = 0;
                    long lCRLF;
                    byte[] abCRLF = new byte[] { 13, 10 };
                    while (true)
                    {
                        // Keep reading here until we see a CRLF/CRLF combination in
                        // the byte array, or until something horrible happens.
                        //
                        // We're going to get the header data for the first block, and
                        // possibily data that goes with it.  We may even pick up stuff
                        // for the second block.  That's fine.  The abBuffer is going
                        // to either be empty or it'll contain the remainder data from
                        // the last block.  What we're going to guarantee is that the
                        // start of abBuffer is always going to point to the boundary
                        // string (assuming the data we've been sent is formatted correctly).
                        //
                        // lRead is the number of valid bytes in abBuffer.
                        // lOffet is where we are in abBuffer.
                        lOffset = 0;
                        while (true)
                        {
                            // Read data...
                            try
                            {
                                iXfer = stream.Read(abBuffer, (int)lRead, (int)(abBuffer.Length - lRead));
                                lRead += iXfer;
                                m_lResponseBytesXferred += iXfer;

                            }
                            catch
                            {
                                break;
                            }

                            // Bail if we see the CRLF/CRLF pattern...
                            if (IndexOf(abBuffer, abCRLFCRLF, 0, lRead) >= 0)
                            {
                                break;
                            }

                            // If we fill the buffer, we're a sad panda...
                            if (abBuffer.Length == lRead)
                            {
                                Log.Error(a_szReason + ": filled our buffer without finding CRLF/CRLF, that's not right...");
                                return (false);
                            }

                            // We're out of data...
                            if (lRead == 0)
                            {
                                break;
                            }
                        }

                        // We've run out of data, so we can bail...
                        if (lRead == 0)
                        {
                            if (filestreamOutputFile != null)
                            {
                                filestreamOutputFile.Close();
                                filestreamOutputFile = null;
                            }
                            break;
                        }

                        // The first item in this block must be --boundary\r\n, so the
                        // index we get back better match where we are in the iOffset...
                        lImageBlockSeperator = IndexOf(abBuffer, abImageBlockSeperator, lOffset);
                        if (lImageBlockSeperator == -1)
                        {
                            Log.Error(a_szReason + ": missing --boundary in multipart/mixed");
                            return (false);
                        }
                        if (lImageBlockSeperator != lOffset)
                        {
                            Log.Error(a_szReason + ": misaligned --boundary in multipart/mixed");
                            return (false);
                        }
                        lOffset += abImageBlockSeperator.Length;

                        // Okey-dokey, what we have now are a collection of HTTP headers
                        // separated by CRLF's with a blank line (also a CRLF) that
                        // terminates the header block.  So let's parse our way through
                        // that, collecting interesting data as we proceed...
                        int iHeaders = 0;
                        string[] aszHeaders = new string[32];
                        bool blMultipartApplicationJson = false;
                        bool blMultipartApplicationPdf = false;
                        bool blMultipartApplicationPdfThumbnail = false;
                        bool blMultipartApplicationPdfImage = false;
                        long lMultipartContentLength = 0;
                        while (true)
                        {
                            // Find the CRLF offset at the end of this header line...
                            lCRLF = IndexOf(abBuffer, abCRLF, lOffset);

                            // Ruh-roh, ran out of data, that's not good...
                            if (lCRLF == -1)
                            {
                                Log.Error(a_szReason + ": unexpected end of header block in multipart/mixed");
                                return (false);
                            }

                            // If the CRLF offset matches where we currently are, then
                            // we found a blank line, so scoot, the offset will now be
                            // pointing to the first byte of data...
                            if (lCRLF == lOffset)
                            {
                                lOffset += (lCRLF - lOffset) + abCRLF.Length;
                                break;
                            }

                            // Convert this header to a string...
                            string szHeader = Encoding.UTF8.GetString(abBuffer, (int)lOffset, (int)(lCRLF - lOffset));

                            // Squirrel this away...
                            if (iHeaders < aszHeaders.Length)
                            {
                                aszHeaders[iHeaders++] = szHeader;
                            }

                            // We found the content-type, so we now know what kind of
                            // data we're dealing with...
                            szHeader = szHeader.ToLowerInvariant();
                            if (szHeader.StartsWith("content-type"))
                            {
                                if (szHeader.Contains("application/json"))
                                {
                                    blMultipartApplicationJson = true;
                                }
                                else if (szHeader.Contains("application/pdf"))
                                {
                                    blMultipartApplicationPdf = true;
                                }
                            }

                            // We found the content-length, so know we know now much
                            // data we expect.  Note that -1 is NOT a valid length for
                            // us.  We must get the actual byte count...
                            else if (szHeader.ToLowerInvariant().StartsWith("content-length"))
                            {
                                string[] aszSplit = szHeader.Split(':');
                                if ((aszSplit != null) && (aszSplit.Length > 1))
                                {
                                    string szLength = aszSplit[1].Trim();
                                    if (!long.TryParse(szLength, out lMultipartContentLength))
                                    {
                                        Log.Error(a_szReason + ": badly constructed content-length");
                                        return (false);
                                    }
                                }
                            }

                            // The content-disposition helps us tell what kind of data
                            // we're getting with an application/pdf block...
                            else if (szHeader.ToLowerInvariant().StartsWith("content-disposition"))
                            {
                                if (szHeader.Contains("image.pdf"))
                                {
                                    blMultipartApplicationPdfImage = true;
                                }
                                if (szHeader.Contains("thumbnail.pdf"))
                                {
                                    blMultipartApplicationPdfThumbnail = true;
                                }
                            }

                            // We've read the header data, skip over it...
                            lOffset += (lCRLF - lOffset) + abCRLF.Length;
                        }

                        // Okay, inside of here we'll handle the application/json data...
                        if (blMultipartApplicationJson)
                        {
                            // Squirrel away this header information...
                            if (iHeaders > 0)
                            {
                                m_aszMultipartHeadersJson = new string[iHeaders];
                                Array.Copy(aszHeaders, m_aszMultipartHeadersJson, iHeaders);
                                foreach (string szTmp in m_aszMultipartHeadersJson)
                                {
                                    Log.Verbose("http>>> recvheader (multipart/mixed json) " + szTmp);
                                }
                            }

                            // If we find ourselves in here more than once for this data
                            // transfer, then we have an issue...
                            if (blApplicationJsonSeen)
                            {
                                Log.Error(a_szReason + ": multiple application/json blocks confuse and irritate us...");
                                return (false);
                            }
                            blApplicationJsonSeen = true;

                            // Validate that the content-length wasn't something insane.
                            // This could give us grief if the JSON data is ever more
                            // than the size of abBuffer.  That's not impossible, but it
                            // seems very unlikely...
                            if ((lOffset + lMultipartContentLength) > abBuffer.Length)
                            {
                                Log.Error(a_szReason + ": data overflow...");
                                return (false);
                            }

                            // Get the JSON data...
                            szReply = Encoding.UTF8.GetString(abBuffer, (int)lOffset, (int)lMultipartContentLength);

                            // This is our new offset...
                            lOffset += lMultipartContentLength;

                            // We're assuming that the CRLF/CRLF data is already in this
                            // block.  It seems reasonable, but if you see bad things
                            // happening with the multipart/mixed getting out of sequence,
                            // this is one place to start...
                        }

                        // Handle the application/pdf data...
                        else if (blMultipartApplicationPdf)
                        {
                            // Squirrel away this header information, we use the opportunity
                            // to save just the data we got...
                            if (blMultipartApplicationPdfThumbnail && (iHeaders > 0))
                            {
                                m_aszMultipartHeadersPdfThumbnail = new string[iHeaders];
                                Array.Copy(aszHeaders, m_aszMultipartHeadersPdfThumbnail, iHeaders);
                                foreach (string szTmp in m_aszMultipartHeadersPdfThumbnail)
                                {
                                    Log.Verbose("http>>> recvheader (multipart/mixed thumbnail) " + szTmp);
                                }
                            }
                            if (blMultipartApplicationPdfImage && (iHeaders > 0))
                            {
                                m_aszMultipartHeadersPdfImage = new string[iHeaders];
                                Array.Copy(aszHeaders, m_aszMultipartHeadersPdfImage, iHeaders);
                                foreach (string szTmp in m_aszMultipartHeadersPdfImage)
                                {
                                    Log.Verbose("http>>> recvheader (multipart/mixed image) " + szTmp);
                                }
                            }

                            // There's a possibility that the binary data is smaller than
                            // or equal to the number of bytes we read.  Handle that here...
                            if (lMultipartContentLength <= (lRead - lOffset))
                            {
                                // Write out the data segment we have...
                                filestreamOutputFile.Write(abBuffer, (int)lOffset, (int)(lRead - lOffset));
                                lOffset = 0;
                            }

                            // Otherwise we've not read enough data, so we'll be doing
                            // some looping...
                            else
                            {
                                // Write out the data segment we have, after this lRead and
                                // lOffset won't have any special meaning, until we're done
                                // with the data block...
                                if ((lRead - lOffset) > 0)
                                {
                                    filestreamOutputFile.Write(abBuffer, (int)lOffset, (int)(lRead - lOffset));
                                }

                                // Read and write the rest of the data in this block...
                                long lRemainder = lMultipartContentLength - (int)(lRead - lOffset);
                                while (lRemainder > 0)
                                {
                                    try
                                    {
                                        iXfer = stream.Read(abBuffer, (int)0, (int)((lRemainder > abBuffer.Length) ? abBuffer.Length : lRemainder));
                                        lRead = iXfer;
                                        m_lResponseBytesXferred += iXfer;
                                    }
                                    catch
                                    {
                                        break;
                                    }
                                    filestreamOutputFile.Write(abBuffer, 0, (int)lRead);
                                    lRemainder -= lRead;
                                }

                                // Get the terminating CRLF/CRLF, we'll process it further down...
                                lRead = 0;
                                lOffset = 0;
                                lRemainder = 4;
                                try
                                {
                                    while (lRemainder > 0)
                                    {
                                        iXfer = stream.Read(abBuffer, (int)lRead, (int)lRemainder);
                                        lRead = iXfer;
                                        m_lResponseBytesXferred += iXfer;
                                        lRemainder -= lRead;
                                        if (lRead == 0)
                                        {
                                            break;
                                        }
                                    }
                                }
                                catch
                                {
                                    // ruh-roh...
                                }
                                lRead = 4 - lRemainder;
                            }
                        }

                        // Uhhhhh...I have no idea what this is.  We could fix this to ignore
                        // the data.  So I'll tbd it...
                        // mlmtbd
                        else
                        {
                            Log.Error(a_szReason + ": unrecognized data block in multipart/mixed, it wasn't application/json or application/pdf...");
                            return (false);
                        }

                        // It looks like we've run out of data...
                        if (lOffset >= lRead)
                        {
                            lRead = 0;
                            break;
                        }

                        // Look for the terminating CRLF/CRLF before another block, the
                        // first one must be at the end of the data...
                        lCRLF = IndexOf(abBuffer, abCRLF, lOffset);
                        if (lCRLF == -1)
                        {
                            break;
                        }
                        if (lCRLF != lOffset)
                        {
                            Log.Error(a_szReason + ": badly constructed CRLF terminator following application.json");
                            return (false);
                        }

                        // And this CRLF is a blank line...
                        lOffset += 2;
                        lCRLF = IndexOf(abBuffer, abCRLF, lOffset);
                        if (lCRLF != lOffset)
                        {
                            Log.Error(a_szReason + ": badly constructed blank CRLF following application.json");
                            return (false);
                        }
                        lOffset += 2;

                        // Okay, now we've run out of data, go back up and try to read more...
                        if (lOffset >= lRead)
                        {
                            lRead = 0;
                            continue;
                        }

                        // We have a remainder, and our offset is not zero.  Shift it to
                        // the front of abBuffer, and modify lRead to indicate that amount.
                        // When we do the read up above we'll tack more data onto abBuffer,
                        // if there is any...
                        if (lRead > lOffset)
                        {
                            // Only do the shift if needed...
                            if (lOffset > 0)
                            {
                                Array.ConstrainedCopy(abBuffer, (int)lOffset, abBuffer, 0, (int)(lRead - lOffset));
                                lRead = (int)(lRead - lOffset);
                                lOffset = 0;
                            }
                        }
                        // Otherwise, we must not have any data...
                        else
                        {
                            lRead = 0;
                            lOffset = 0;
                        }
                    }
                }
            }
            catch (WebException webexception)
            {
                return (CollectWebException("GetData", webexception));
            }
            catch (Exception exception)
            {
                return (CollectException("GetData", exception));
            }

            // Cleanup...
            httpwebresponse.Close();

            // Log what we got back......
            Log.Info("http>>> recvdata " + szReply);

            // All done, final check...
            m_httpresponsedata.iResponseHttpStatus = (int)httpwebresponse.StatusCode;
            m_httpresponsedata.szResponseData = szReply;
            if (m_httpresponsedata.iResponseHttpStatus >= 300)
            {
                Log.Error(a_szReason + " failed...");
                Log.Error("http>>> sts " + m_httpresponsedata.iResponseHttpStatus);
                Log.Error("http>>> stsreason " + a_szReason + " (" + m_httpresponsedata.szResponseData + ")");
                return (false);
            }
            return (true);

            #endregion
        }
        *******************************************************************************************
        ******************************************************************************************/

        /// <summary>
        /// Log details of the data transfer...
        /// </summary>
        /// <param name="a_szFunction">three letter code for the function we're in</param>
        /// <param name="a_iXfer">number of bytes from last read</param>
        private void LogXfer(string a_szFunction, int a_iXfer)
        {
            Log.VerboseData
            (
                "http>>> " + a_szFunction + ":" +
                " xfr=" + a_iXfer.ToString("D7") +
                " mpx=" + m_lMultipartXferred.ToString("D7") +
                " mpt=" + m_lMultipartContentLength.ToString("D7") +
                " clx=" + m_lResponseBytesXferred.ToString("D7") +
                " clt=" + m_lContentLength.ToString("D7")
            );
        }

        /// <summary>
        /// Log details of the data transfer...
        /// </summary>
        /// <param name="a_szFunction">three letter code for the function we're in</param>
        /// <param name="a_iXfer">number of bytes from last read</param>
        private void LogWrite(string a_szFunction, long a_lOffset, long a_lXfer)
        {
            Log.VerboseData
            (
                "http>>> " + a_szFunction + ":" +
                " off=" + a_lOffset.ToString("D7") +
                " xfr=" + a_lXfer.ToString("D7") +
                " mpx=" + m_lWritten.ToString("D7") +
                " mpt=" + m_lMultipartContentLength.ToString("D7")
            );
        }

        /// <summary>
        /// Abort the request if the timer fires.
        /// </summary>
        /// <param name="state">our request object</param>
        /// <param name="a_blTimedOut">true if we've timed out</param>
        private static void TimeoutCallback(object a_objectState, bool a_blTimedOut)
        {
            if (a_blTimedOut)
            {
                ApiCmd apicmd = a_objectState as ApiCmd;
                apicmd.HttpAbortClientRequest(true);
            }
        }

        /// <summary>
        /// Handle the response to our request...
        /// </summary>
        /// <param name="asyncResult"></param>
        private static void ResponseCallBackLaunchpad(IAsyncResult a_iasyncresult)
        {
            ApiCmd apicmd = (ApiCmd)a_iasyncresult.AsyncState;
            apicmd.ResponseCallBack(a_iasyncresult);
        }

        /// <summary>
        /// Handle the response to our request...
        /// </summary>
        /// <param name="asyncResult"></param>
        private void ResponseCallBack(IAsyncResult a_iasyncresult)
        {
            bool blMultipart = false;

            // Get the response...
            try
            {
                m_httpresponsedata.httpwebresponse = (HttpWebResponse)m_httprequestdata.httpwebrequest.EndGetResponse(a_iasyncresult);
            }
            catch (WebException webexception)
            {
                CollectWebException("GetResponse", webexception, true);
                m_httprequestdata.autoreseteventHttpWebRequest.Set();
                return;
            }
            catch (Exception exception)
            {
                CollectException("GetResponse", exception, true);
                m_httprequestdata.autoreseteventHttpWebRequest.Set();
                return;
            }

            // Extra header for waitForEvents...
            if (m_httpreplystyle == HttpReplyStyle.Event)
            {
                Log.Info(" ");
                Log.Info("http>>> " + m_szReason + " (response)");
            }

            // Dump the status...
            m_httpresponsedata.iResponseHttpStatus = (int)(HttpStatusCode)m_httpresponsedata.httpwebresponse.StatusCode;
            Log.Info("http>>> recvsts " + m_httpresponsedata.iResponseHttpStatus + " (" + m_httpresponsedata.httpwebresponse.StatusCode + ")");

            // Get the response headers, if any...
            m_httpresponsedata.aszResponseHeaders = null;
            if (m_httpresponsedata.httpwebresponse.Headers != null)
            {
                m_httpresponsedata.aszResponseHeaders = new string[m_httpresponsedata.httpwebresponse.Headers.Keys.Count];
                for (int kk = 0; kk < m_httpresponsedata.aszResponseHeaders.Length; kk++)
                {
                    if (m_httpresponsedata.httpwebresponse.Headers.GetValues(kk) == null)
                    {
                        m_httpresponsedata.aszResponseHeaders[kk] = m_httpresponsedata.httpwebresponse.Headers.Keys.Get(kk) + "=";
                    }
                    else
                    {
                        m_httpresponsedata.aszResponseHeaders[kk] = m_httpresponsedata.httpwebresponse.Headers.Keys.Get(kk) + "=" + m_httpresponsedata.httpwebresponse.Headers.GetValues(kk).GetValue(0);
                    }
                }
            }

            // Dump the header info...
            if ((Log.GetLevel() & 0x0002) != 0)
            {
                // Get each header and display each value.
                NameValueCollection namevaluecollectionHeaders = m_httpresponsedata.httpwebresponse.Headers;
                foreach (string szKey in namevaluecollectionHeaders.AllKeys)
                {
                    string[] aszValues = namevaluecollectionHeaders.GetValues(szKey);
                    if (aszValues.Length == 0)
                    {
                        Log.Verbose("http>>> recvheader " + szKey + ": n/a");
                    }
                    else
                    {
                        foreach (string szValue in aszValues)
                        {
                            Log.Verbose("http>>> recvheader " + szKey + ": " + szValue);
                        }
                    }
                }
            }

            // Get the content length...
            m_lContentLength = m_httpresponsedata.httpwebresponse.ContentLength;

            // Get the content type...
            ContentType contenttype = new ContentType(m_httpresponsedata.httpwebresponse.ContentType);

            // application/json with UTF-8 is okay...
            if (contenttype.MediaType.ToLowerInvariant() == "application/json")
            {
                if (contenttype.CharSet.ToLowerInvariant() != "utf-8")
                {
                    Log.Error(m_szReason + ": application/json charset is not utf-8..." + contenttype.CharSet);
                    return;
                }
                blMultipart = false;
            }

            // multipart/mixed is okay, with a boundary...
            else if (contenttype.MediaType.ToLowerInvariant() == "multipart/mixed")
            {
                // Extract the boundary data...
                blMultipart = true;
                m_szMultipartBoundary = contenttype.Boundary;
                if (string.IsNullOrEmpty(m_szMultipartBoundary))
                {
                    Log.Error(m_szReason + ": bad multipart/mixed boundary...");
                    return;
                }

                // This is the form we expect to find in the data stream...
                m_szMultipartBoundary = "--" + m_szMultipartBoundary + "\r\n";
            }

            // Anything else is bad...
            else
            {
                Log.Error(m_szReason + ": unknown http content-type..." + contenttype.MediaType);
                return;
            }

            // Get the data coming back...
            try
            {
                // All we have is just a JSON reply...
                if (!blMultipart)
                {
                    m_lWritten = 0;
                    m_lMultipartXferred = 0;
                    m_httpresponsedata.streamHttpWebResponse = m_httpresponsedata.httpwebresponse.GetResponseStream();
                    m_abBufferHttpWebResponse = new byte[0x65536];
                    m_lResponseBytesXferred = 0;
                    IAsyncResult iasyncresult = m_httpresponsedata.streamHttpWebResponse.BeginRead
                    (
                        m_abBufferHttpWebResponse,
                        0,
                        m_abBufferHttpWebResponse.Length,
                        new AsyncCallback(ReadCallBackJsonLaunchpad),
                        this
                    );
                }

                // We're a multipart...
                else
                {
                    m_lWritten = 0;
                    m_lMultipartXferred = 0;
                    m_httpresponsedata.streamHttpWebResponse = m_httpresponsedata.httpwebresponse.GetResponseStream();
                    m_abBufferHttpWebResponse = new byte[0x65536];
                    m_lResponseBytesXferred = 0;

                    // This is our block separator, it has the dashes and the
                    // terminating CRLF...
                    m_abImageBlockSeperator = Encoding.UTF8.GetBytes(m_szMultipartBoundary);

                    // This is our CRLF/CRLF detector that tells us that we've
                    // captured a complete header block...
                    m_abCRLF = new byte[] { 13, 10 };
                    m_abCRLFCRLF = new byte[] { 13, 10, 13, 10 };

                    // Init more stuff...
                    m_blApplicationJsonSeen = false;
                    m_filestreamOutputFile = null;
                    m_lRead = 0;
                    m_lOffset = 0;

                    // If we were given a filename, create the stream where we'll
                    // dump the binary data, assuming we get any binary data...
                    if (!string.IsNullOrEmpty(m_szOutputFile))
                    {
                        try
                        {
                            // Create the empty file...
                            if (!File.Exists(m_szOutputFile))
                            {
                                File.Delete(m_szOutputFile);
                            }
                            m_filestreamOutputFile = new FileStream(m_szOutputFile, FileMode.Create);
                        }
                        catch (Exception exception)
                        {
                            Log.Error(m_szReason + ": http delete or streamwriter failed..." + m_szOutputFile + ", " + exception.Message);
                            return;
                        }
                    }

                    // Start the read...
                    IAsyncResult iasyncresult = m_httpresponsedata.streamHttpWebResponse.BeginRead
                    (
                        m_abBufferHttpWebResponse,
                        0,
                        m_abBufferHttpWebResponse.Length,
                        new AsyncCallback(ReadCallBackMultipartHeaderLaunchpad),
                        this
                    );
                }
            }
            catch (WebException webexception)
            {
                CollectWebException("GetResponse", webexception);
                m_httprequestdata.autoreseteventHttpWebRequest.Set();
                return;
            }
            catch (Exception exception)
            {
                CollectException("GetResponse", exception);
                m_httprequestdata.autoreseteventHttpWebRequest.Set();
                return;
            }
        }

        /// <summary>
        /// Read the HTTP payload for application/json...
        /// </summary>
        /// <param name="a_iasyncresult"></param>
        private void ReadCallBackJsonLaunchpad(IAsyncResult a_iasyncresult)
        {
            ApiCmd apicmd = (ApiCmd)a_iasyncresult.AsyncState;
            apicmd.ReadCallBackJson(a_iasyncresult);
        }

        /// <summary>
        /// Read the HTTP payload for application/json...
        /// </summary>
        /// <param name="a_iasyncresult"></param>
        private void ReadCallBackJson(IAsyncResult a_iasyncresult)
        {
            int iRead = 0;

            // Get the data coming back...
            try
            {
                // If we got data, then get more data, we'll keep doing
                // this callback until we've gotten all of the data...
                iRead = m_httpresponsedata.streamHttpWebResponse.EndRead(a_iasyncresult);
                m_lMultipartXferred += iRead;
                m_lResponseBytesXferred += iRead;
                LogXfer("jsn", iRead);
                if (iRead > 0)
                {
                    // Protect us against a buffer that's too small...
                    if ((m_abBufferHttpWebResponse.Length - m_lResponseBytesXferred) < 8192)
                    {
                        byte[] ab = new byte[m_abBufferHttpWebResponse.Length + 65536];
                        m_abBufferHttpWebResponse.CopyTo(ab, 0);
                        m_abBufferHttpWebResponse = ab;
                    }

                    // Get the next bit...
                    IAsyncResult iasyncresult = m_httpresponsedata.streamHttpWebResponse.BeginRead
                    (
                        m_abBufferHttpWebResponse,
                        (int)m_lResponseBytesXferred,
                        (int)(m_abBufferHttpWebResponse.Length - m_lResponseBytesXferred),
                        new AsyncCallback(ReadCallBackJsonLaunchpad),
                        this
                    );

                    // Scoot...
                    return;
                }

                // If we got this far, then we've collected all of our data...
                m_httpresponsedata.szResponseData = "";
                if (m_lResponseBytesXferred > 0)
                {
                    byte[] abReply = new byte[m_lResponseBytesXferred];
                    Buffer.BlockCopy(m_abBufferHttpWebResponse, 0, abReply, 0, (int)m_lResponseBytesXferred);
                    m_httpresponsedata.szResponseData = Encoding.UTF8.GetString(abReply, 0, (int)m_lResponseBytesXferred);
                }
            }
            catch (WebException webexception)
            {
                CollectWebException("GetData", webexception);
                return;
            }
            catch (Exception exception)
            {
                CollectException("GetData", exception);
                return;
            }

            // Success, we set the web request here, because we've
            // transferred all of the data...
            m_httprequestdata.autoreseteventHttpWebRequest.Set();
            return;
        }

        /// <summary>
        /// Read the HTTP header payload for a multipart/mixed...
        /// </summary>
        /// <param name="a_iasyncresult"></param>
        private void ReadCallBackMultipartHeaderLaunchpad(IAsyncResult a_iasyncresult)
        {
            ApiCmd apicmd = (ApiCmd)a_iasyncresult.AsyncState;
            apicmd.ReadCallBackMultipartHeader(a_iasyncresult);
        }

        /// <summary>
        /// Read an HTTP payload for the header for a multipart/mixed, once
        /// we have the header we'll know if the data block is going to be
        /// application/json or application/pdf, and we'll dispatch to the
        /// right function...
        /// </summary>
        /// <param name="a_iasyncresult"></param>
        private void ReadCallBackMultipartHeader(IAsyncResult a_iasyncresult)
        {
            int iXfer;
            long lCRLF;
            long lImageBlockSeperator;
            IAsyncResult iasyncresult;

            // Get the data coming back...
            try
            {
                // We have a multipart response, and we need to collect and
                // separate all of the data.  The data must arrive in the
                // following format, repeating as necessary to send all of
                // the data...
                //
                // --boundary + \r\n
                // Content-Type: ... + \r\n
                // Content-Length: # + \r\n
                // any other headers + \r\n
                // \r\n
                // data + \r\n
                // \r\n
                // --boundary + \r\n
                // Content-Type: ... + \r\n
                // Content-Length: # + \r\n
                // any other headers + \r\n
                // \r\n
                // data + \r\n
                // \r\n
                //
                // Getting the CRLF terminators right is part of the challenge...
                //
                // In theory we could have several parts, but in practice
                // we're only expecting two:  JSON and an image.  If we are
                // getting metadata, it will be the JSON and the thumbnail.
                // If we are reading an imageblock, it will be the JSON and
                // the image.  We'll try to set things up so that we can
                // get more bits if needed.  But I suspect that two segments
                // be easiest to support both for standard and vendor
                // specific behavior...

                // Keep reading here until we see a CRLF/CRLF combination in
                // the byte array, or until something horrible happens.
                //
                // We're going to get the header data for the first block, and
                // possibily data that goes with it.  We may even pick up stuff
                // for the second block.  That's fine.  The abBuffer is going
                // to either be empty or it'll contain the remainder data from
                // the last block.  What we're going to guarantee is that the
                // start of abBuffer is always going to point to the boundary
                // string (assuming the data we've been sent is formatted correctly).
                //
                // lRead is the number of valid bytes in abBuffer.
                // lOffet is where we are in abBuffer.

                // Read data...
                try
                {
                    iXfer = m_httpresponsedata.streamHttpWebResponse.EndRead(a_iasyncresult);
                    m_lRead += iXfer;
                    m_lResponseBytesXferred += iXfer;
                    LogXfer("hdr", iXfer);
                }
                catch (Exception exception)
                {
                    Log.Error("httphdr>>> EndRead failed - " + exception.Message);
                    m_httprequestdata.autoreseteventHttpWebRequest.Set();
                    return;
                }

                // We've run out of data, meaning that we didn't read anything, and
                // we don't have anything cached from a prior read, so we can bail...
                if (m_lRead == 0)
                {
                    Log.Verbose("httphdr>>> done...");
                    m_httprequestdata.autoreseteventHttpWebRequest.Set();
                    return;
                }

                // Read more data if we don't see the CRLF/CRLF pattern...
                if (IndexOf(m_abBufferHttpWebResponse, m_abCRLFCRLF, 0, m_lRead) < 0)
                {
                    // If we filled the buffer, we're a sad panda...
                    if (m_abBufferHttpWebResponse.Length == m_lRead)
                    {
                        Log.Error("httphdr>>> filled our buffer without finding CRLF/CRLF, that's not right...");
                        m_httprequestdata.autoreseteventHttpWebRequest.Set();
                        return;
                    }

                    // Start the read...
                    iasyncresult = m_httpresponsedata.streamHttpWebResponse.BeginRead
                    (
                        m_abBufferHttpWebResponse,
                        (int)m_lRead,
                        (int)(m_abBufferHttpWebResponse.Length - m_lRead),
                        new AsyncCallback(ReadCallBackJsonLaunchpad),
                        this
                    );

                    // Scoot, don't set the web request, we're not done yet...
                    return;
                }

                // The first item in this block must be --boundary\r\n, so the
                // index we get back better match where we are in the iOffset...
                lImageBlockSeperator = IndexOf(m_abBufferHttpWebResponse, m_abImageBlockSeperator, m_lOffset);
                if (lImageBlockSeperator == -1)
                {
                    Log.Error("httphdr>>> missing --boundary in multipart/mixed");
                    m_httprequestdata.autoreseteventHttpWebRequest.Set();
                    return;
                }
                if (lImageBlockSeperator != m_lOffset)
                {
                    Log.Error("httphdr>>> misaligned --boundary in multipart/mixed");
                    m_httprequestdata.autoreseteventHttpWebRequest.Set();
                    return;
                }
                m_lOffset += m_abImageBlockSeperator.Length;

                // Okey-dokey, what we have now are a collection of HTTP headers
                // separated by CRLF's with a blank line (also a CRLF) that
                // terminates the header block.  So let's parse our way through
                // that, collecting interesting data as we proceed...
                m_iHeaders = 0;
                m_aszHeaders = new string[32];
                m_blMultipartApplicationJson = false;
                m_blMultipartApplicationPdf = false;
                m_blMultipartApplicationPdfThumbnail = false;
                m_blMultipartApplicationPdfImage = false;
                m_lMultipartContentLength = 0;
                while (true)
                {
                    // Find the CRLF offset at the end of this header line...
                    lCRLF = IndexOf(m_abBufferHttpWebResponse, m_abCRLF, m_lOffset);

                    // Ruh-roh, ran out of data, that's not good...
                    if (lCRLF == -1)
                    {
                        Log.Error("httphdr>>> unexpected end of header block in multipart/mixed");
                        m_httprequestdata.autoreseteventHttpWebRequest.Set();
                        return;
                    }

                    // If the CRLF offset matches where we currently are, then
                    // we found a blank line, so scoot, the offset will now be
                    // pointing to the first byte of data...
                    if (lCRLF == m_lOffset)
                    {
                        m_lOffset += (lCRLF - m_lOffset) + m_abCRLF.Length;
                        break;
                    }

                    // Convert this header to a string...
                    string szHeader = Encoding.UTF8.GetString(m_abBufferHttpWebResponse, (int)m_lOffset, (int)(lCRLF - m_lOffset));

                    // Squirrel this away...
                    if (m_iHeaders < m_aszHeaders.Length)
                    {
                        m_aszHeaders[m_iHeaders++] = szHeader;
                    }

                    // We found the content-type, so we now know what kind of
                    // data we're dealing with...
                    szHeader = szHeader.ToLowerInvariant();
                    if (szHeader.StartsWith("content-type"))
                    {
                        if (szHeader.Contains("application/json"))
                        {
                            m_blMultipartApplicationJson = true;
                        }
                        else if (szHeader.Contains("application/pdf"))
                        {
                            m_blMultipartApplicationPdf = true;
                        }
                    }

                    // We found the content-length, so know we know now much
                    // data we expect.  Note that -1 is NOT a valid length for
                    // us.  We must get the actual byte count...
                    else if (szHeader.ToLowerInvariant().StartsWith("content-length"))
                    {
                        string[] aszSplit = szHeader.Split(':');
                        if ((aszSplit != null) && (aszSplit.Length > 1))
                        {
                            string szLength = aszSplit[1].Trim();
                            if (!long.TryParse(szLength, out m_lMultipartContentLength))
                            {
                                Log.Error("httphdr>>> badly constructed content-length");
                                m_httprequestdata.autoreseteventHttpWebRequest.Set();
                                return;
                            }
                        }
                    }

                    // The content-disposition helps us tell what kind of data
                    // we're getting with an application/pdf block...
                    else if (szHeader.ToLowerInvariant().StartsWith("content-disposition"))
                    {
                        if (szHeader.Contains("image.pdf"))
                        {
                            m_blMultipartApplicationPdfImage = true;
                        }
                        if (szHeader.Contains("thumbnail.pdf"))
                        {
                            m_blMultipartApplicationPdfThumbnail = true;
                        }
                    }

                    // We've read the header data, skip over it...
                    m_lOffset += (lCRLF - m_lOffset) + m_abCRLF.Length;
                }
            }
            catch (WebException webexception)
            {
                CollectWebException("GetData", webexception);
                return;
            }
            catch (Exception exception)
            {
                CollectException("GetData", exception);
                return;
            }

            // Capture JSON data...
            if (m_blMultipartApplicationJson)
            {
                // Squirrel away this header information...
                if (m_iHeaders > 0)
                {
                    m_aszMultipartHeadersJson = new string[m_iHeaders];
                    Array.Copy(m_aszHeaders, m_aszMultipartHeadersJson, m_iHeaders);
                    foreach (string szTmp in m_aszMultipartHeadersJson)
                    {
                        Log.Verbose("http>>> recvheader (multipart/mixed json) " + szTmp);
                    }
                }

                // If we find ourselves in here more than once for this data
                // transfer, then we have an issue...
                if (m_blApplicationJsonSeen)
                {
                    Log.Error("httphdr>>> multiple application/json blocks confuse and irritate us...");
                    m_httprequestdata.autoreseteventHttpWebRequest.Set();
                    return;
                }
                m_blApplicationJsonSeen = true;

                // Validate that the content-length wasn't something insane.
                // This could give us grief if the JSON data is ever more
                // than the size of abBuffer.  That's not impossible, but it
                // seems very unlikely...
                if ((m_lOffset + m_lMultipartContentLength) > m_abBufferHttpWebResponse.Length)
                {
                    Log.Error("httphdr>>> data overflow...");
                    m_httprequestdata.autoreseteventHttpWebRequest.Set();
                    return;
                }

                // Get the JSON data...
                m_httpresponsedata.szResponseData = Encoding.UTF8.GetString(m_abBufferHttpWebResponse, (int)m_lOffset, (int)m_lMultipartContentLength);

                // This is our new offset...
                m_lOffset += m_lMultipartContentLength;

                // We're assuming that the CRLF/CRLF data is already in this
                // block.  It seems reasonable, but if you see bad things
                // happening with the multipart/mixed getting out of sequence,
                // this is one place to start...
                ConsumeEndOfBoundary();

                // Start the read for the next multipart header...
                iasyncresult = m_httpresponsedata.streamHttpWebResponse.BeginRead
                (
                    m_abBufferHttpWebResponse,
                    (int)m_lRead,
                    (int)(m_abBufferHttpWebResponse.Length - m_lRead),
                    new AsyncCallback(ReadCallBackMultipartHeaderLaunchpad),
                    this
                );

                // All done, note that we're not setting the event here, because
                // we're not done!  Only returns that are happening because there
                // is no more data or errors set the event...
                return;
            }

            // Capture PDF data (image or thumbnail)...
            else if (m_blMultipartApplicationPdf)
            {
                // Squirrel away this header information, we use the opportunity
                // to save just the data we got...
                if (m_blMultipartApplicationPdfThumbnail && (m_iHeaders > 0))
                {
                    m_aszMultipartHeadersPdfThumbnail = new string[m_iHeaders];
                    Array.Copy(m_aszHeaders, m_aszMultipartHeadersPdfThumbnail, m_iHeaders);
                    foreach (string szTmp in m_aszMultipartHeadersPdfThumbnail)
                    {
                        Log.Verbose("http>>> recvheader (multipart/mixed thumbnail) " + szTmp);
                    }
                }
                if (m_blMultipartApplicationPdfImage && (m_iHeaders > 0))
                {
                    m_aszMultipartHeadersPdfImage = new string[m_iHeaders];
                    Array.Copy(m_aszHeaders, m_aszMultipartHeadersPdfImage, m_iHeaders);
                    foreach (string szTmp in m_aszMultipartHeadersPdfImage)
                    {
                        Log.Verbose("http>>> recvheader (multipart/mixed image) " + szTmp);
                    }
                }

                // There's a possibility that the binary data is smaller than
                // or equal to the number of bytes we read.  Handle that here...
                if (m_lMultipartContentLength <= (m_lRead - m_lOffset))
                {
                    // Write out the data segment we have...
                    m_lMultipartXferred += (m_lRead - m_lOffset);
                    m_lWritten += (m_lRead - m_lOffset);
                    LogWrite(m_blMultipartApplicationPdfThumbnail ? "thm" : "pdf", m_lOffset, (m_lRead - m_lOffset));
                    m_filestreamOutputFile.Write(m_abBufferHttpWebResponse, (int)m_lOffset, (int)(m_lRead - m_lOffset));
                    m_lOffset = 0;

                    // We're assuming that the CRLF/CRLF data is already in this
                    // block.  It seems reasonable, but if you see bad things
                    // happening with the multipart/mixed getting out of sequence,
                    // this is one place to start...
                    ConsumeEndOfBoundary();

                    // Start the read for the next multipart header...
                    iasyncresult = m_httpresponsedata.streamHttpWebResponse.BeginRead
                    (
                        m_abBufferHttpWebResponse,
                        (int)m_lRead,
                        (int)(m_abBufferHttpWebResponse.Length - m_lRead),
                        new AsyncCallback(ReadCallBackMultipartHeaderLaunchpad),
                        this
                    );

                    // All done, don't set the web request, we're not done yet...
                    return;
                }

                // Write out the data segment we have...
                if ((m_lRead - m_lOffset) > 0)
                {
                    m_lMultipartXferred += (m_lRead - m_lOffset);
                    m_lWritten += (m_lRead - m_lOffset);
                    LogWrite(m_blMultipartApplicationPdfThumbnail ? "thm" : "pdf", m_lOffset, (m_lRead - m_lOffset));
                    m_filestreamOutputFile.Write(m_abBufferHttpWebResponse, (int)m_lOffset, (int)(m_lRead - m_lOffset));
                    m_lRead = m_lRead - m_lOffset; // we're tracking the content-length
                }

                // Start the read for the multipart pdf data...
                m_lRead = 0;
                m_lOffset = 0;
                iasyncresult = m_httpresponsedata.streamHttpWebResponse.BeginRead
                (
                    m_abBufferHttpWebResponse,
                    0,
                    (int)m_abBufferHttpWebResponse.Length,
                    new AsyncCallback(ReadCallBackMultipartPdfLaunchpad),
                    this
                );

                // All done, we're not done yet, so don't set the web request...
                return;
            }

            // TBD
            // We've been given something that we don't recognize, we could
            // skip over this, but I'll add that later...
            else
            {
                Log.Error("httphdr>>> unrecognized data block in multipart/mixed, it wasn't application/json or application/pdf...");
                m_httprequestdata.autoreseteventHttpWebRequest.Set();
                return;
            }
        }

        /// <summary>
        /// Read the HTTP application/json payload for a multipart/mixed.
        /// </summary>
        /// <param name="a_iasyncresult"></param>
        private void ReadCallBackMultipartPdfLaunchpad(IAsyncResult a_iasyncresult)
        {
            ApiCmd apicmd = (ApiCmd)a_iasyncresult.AsyncState;
            apicmd.ReadCallBackMultipartPdf(a_iasyncresult);
        }

        /// <summary>
        /// Read the HTTP application/json payload for a multipart/mixed, this
        /// is just reading and writing to the file until we've exhausted all
        /// of the data indicated in the block's content-length...
        /// 
        /// On Windows with .NET you'll see reads coming in the following pattern:
        ///     1   byte
        ///     n-1 bytes
        ///     1   byte
        ///     n-1 bytes
        ///     ...
        /// 
        /// This apparently has to do with this knowledge base article.
        /// https://technet.microsoft.com/library/security/ms16-065
        /// Microsoft Security Bulletin MS16-065 - Important
        /// Security Update for .NET Framework (3156757)
        /// 
        /// Which splits the data this way as a security measure.  It has no
        /// significant impact on the performance of the system, though it does
        /// emphasize the value of a tight loop when reading this data.
        /// 
        /// </summary>
        /// <param name="a_iasyncresult"></param>
        private void ReadCallBackMultipartPdf(IAsyncResult a_iasyncresult)
        {
            int iXfer;
            IAsyncResult iasyncresult;

            // Get the data...
            iXfer = m_httpresponsedata.streamHttpWebResponse.EndRead(a_iasyncresult);
            m_lRead += iXfer;
            m_lMultipartXferred += iXfer;
            m_lResponseBytesXferred += iXfer;
            LogXfer("pdf", iXfer);

            // If we actually read data, write it out and read more data...
            if (iXfer > 0)
            {
                // We haven't reached the end of the PDF data, so we can
                // write out the whole thing and get more...
                if (m_lMultipartXferred <= m_lMultipartContentLength)
                {
                    // Write what we have...
                    m_lWritten += iXfer;
                    LogWrite(m_blMultipartApplicationPdfThumbnail ? "thm" : "pdf", 0, iXfer);
                    m_filestreamOutputFile.Write(m_abBufferHttpWebResponse, 0, iXfer);
                    m_lOffset += iXfer;

                    // Read more data...
                    iasyncresult = m_httpresponsedata.streamHttpWebResponse.BeginRead
                    (
                        m_abBufferHttpWebResponse,
                        0,
                        (int)m_abBufferHttpWebResponse.Length,
                        new AsyncCallback(ReadCallBackMultipartPdfLaunchpad),
                        this
                    );

                    // All done...
                    return;
                }
            }

            // Whatever we've read contains some image data, and some
            // stuff that isn't image data.  Write out just the image
            // data.  We figure this out from the difference between
            // the amount of data in the PDF and how much we've already
            // written to disk.
            long lLastWrite = iXfer - (m_lMultipartXferred - m_lMultipartContentLength);
            long lRemainder = iXfer - lLastWrite;
            if (lLastWrite > 0)
            {
                m_lWritten += lLastWrite;
                LogWrite(m_blMultipartApplicationPdfThumbnail ? "thm" : "pdf", 0, lLastWrite);
                m_filestreamOutputFile.Write(m_abBufferHttpWebResponse, 0, (int)lLastWrite);
            }

            // We've completed the file, so close it...
            m_filestreamOutputFile.Close();
            m_filestreamOutputFile = null;

            // Shift all of the remaining data to the beginning of our
            // buffer and fix the offset and read bytes to reflect the
            // actual data.
            //
            // TBD (we may need another read here, if we didn't get
            // all of the CRLF data!!!)
            if (lRemainder > 0)
            {
                Array.ConstrainedCopy(m_abBufferHttpWebResponse, (int)lLastWrite, m_abBufferHttpWebResponse, 0, (int)lRemainder);
            }
            m_lOffset = 0;
            m_lRead = lRemainder;

            // Consume the end of the block...
            ConsumeEndOfBoundary();

            // Start the read for the next multipart header...
            iasyncresult = m_httpresponsedata.streamHttpWebResponse.BeginRead
            (
                m_abBufferHttpWebResponse,
                0,
                (int)m_abBufferHttpWebResponse.Length,
                new AsyncCallback(ReadCallBackMultipartHeaderLaunchpad),
                this
            );

            // All done...
            return;
        }

        /// <summary>
        /// Consume the end of the multipart boundary for this chunk...
        /// </summary>
        private void ConsumeEndOfBoundary()
        {
            long lCRLF;

            // It looks like we've run out of data...
            if (m_lOffset >= m_lRead)
            {
                m_lRead = 0;
                return;
            }

            // Look for the terminating CRLF/CRLF before another block, the
            // first one must be at the end of the data...
            lCRLF = IndexOf(m_abBufferHttpWebResponse, m_abCRLF, m_lOffset);
            if (lCRLF == -1)
            {
                return;
            }
            if (lCRLF != m_lOffset)
            {
                Log.Error(m_szReason + ": badly constructed CRLF terminator following block");
                return;
            }

            // And this CRLF is a blank line...
            m_lOffset += 2;
            lCRLF = IndexOf(m_abBufferHttpWebResponse, m_abCRLF, m_lOffset);
            if (lCRLF != m_lOffset)
            {
                Log.Error(m_szReason + ": badly constructed blank CRLF following block");
                return;
            }
            m_lOffset += 2;

            // Okay, now we've run out of data, go back up and try to read more...
            if (m_lOffset >= m_lRead)
            {
                m_lRead = 0;
                return;
            }

            // We have a remainder, and our offset is not zero.  Shift it to
            // the front of abBuffer, and modify lRead to indicate that amount.
            // When we do the read up above we'll tack more data onto abBuffer,
            // if there is any...
            if (m_lRead > m_lOffset)
            {
                // Only do the shift if needed...
                if (m_lOffset > 0)
                {
                    Array.ConstrainedCopy(m_abBufferHttpWebResponse, (int)m_lOffset, m_abBufferHttpWebResponse, 0, (int)(m_lRead - m_lOffset));
                    m_lRead = (int)(m_lRead - m_lOffset);
                    m_lOffset = 0;
                }
            }

            // Otherwise, we must not have any data...
            else
            {
                m_lRead = 0;
                m_lOffset = 0;
            }

            // All done...
            return;
        }

        /// <summary>
        /// We make decisions about how the HttpRequestAttempt went.  It keeps
        /// the code cleaner this way, especially for the retry loop.
        /// </summary>
        /// <param name="a_szReason">reason for the call, for logging</param>
        /// <param name="a_szUri">our target</param>
        /// <param name="a_szMethod">http method (ex: POST, DELETE...)</param>
        /// <param name="a_aszHeader">array of headers to send or null</param>
        /// <param name="a_szData">data to send or null</param>
        /// <param name="a_szUploadFile">upload data from a file</param>
        /// <param name="a_szOutputFile">redirect the data to a file</param>
        /// <param name="a_iTimeout">timeout in milliseconds</param>
        /// <param name="a_httpreplystyle">how the reply will be handled</param>
        /// <param name="a_blInitOnly">init only (used in error cases)</param>
        /// <returns>true on success</returns>
        public bool HttpRequest
        (
            string a_szReason,
            string a_szUri,
            string a_szMethod,
            string[] a_aszHeader,
            string a_szData,
            string a_szUploadFile,
            string a_szOutputFile,
            int a_iTimeout,
            HttpReplyStyle a_httpreplystyle,
            bool a_blInitOnly = false
        )
        {
            //
            // The WebRequest method of doing stuff...
            //
            string szUri;
            Stream stream = null;


            // Setup the HTTP Request
            #region Setup HTTP Request

            // Log a reason for being here...
            Log.Info("");
            Log.Info("http>>> " + a_szReason);

            // Squirrel these away...
            m_httplistenerdata.szUri = a_szUri;
            m_szSendCommand = a_szData;
            m_httpreplystyle = a_httpreplystyle;
            m_szOutputFile = a_szOutputFile;
            m_szReason = a_szReason;
            m_httplistenerdata.szMethod = a_szMethod;


            // Pick our URI, prefix the default server, unless the user gives us an override...
            //
            // A silent exception occurs on Webrequest.Create(), it's trapped and doesn't seem
            // to cause any problems, but on Windows if you want to make it go away, then add
            // the next two items to the registry (you only need Wow6432Node on 64-bit OSes)...
            //   HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\.NETFramework\LegacyWPADSupport dword:00000000
            //   HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\LegacyWPADSupport dword:00000000
            if (a_szUri == null)
            {
                Log.Error(a_szReason + ": a_szUri is null");
                return (false);
            }

            // For HTTPS we need a certificate for the DNS domain, I have no idea if
            // this can be done with a numeric IP, but I know it can be done with a
            // DNS name, and since we're doing mDNS in this case, we want the link local
            // name of the device...
            if (m_blUseHttps)
            {
                string szLinkLocal = m_dnssddeviceinfo.GetLinkLocal().Replace(".local.", ".local");
                szUri = "https://" + szLinkLocal + ":" + m_dnssddeviceinfo.GetPort() + a_szUri;
            }

            // Build the URI, for HTTP we can use the IP address to get to our device...
            else
            {
                szUri = "http://" + m_dnssddeviceinfo.GetIpv4() + ":" + m_dnssddeviceinfo.GetPort() + a_szUri;
            }
            m_httplistenerdata.szUriFull = szUri;

            // If all we want is to initialize the ApiCmd data, then scoot.
            // We do this to help stock the object when errors occur.
            if (a_blInitOnly)
            {
                return (false);
            }

            // Log what we're doing and start building the request...
            Log.Info("http>>> " + m_httplistenerdata.szMethod + " " + m_httplistenerdata.szUriFull);
            m_httprequestdata.httpwebrequest = (HttpWebRequest)WebRequest.Create(szUri);
            m_httprequestdata.httpwebrequest.AllowWriteStreamBuffering = true;
            m_httprequestdata.httpwebrequest.KeepAlive = true;
            m_httprequestdata.httpwebrequest.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);

            // Pick our method...
            m_httprequestdata.httpwebrequest.Method = a_szMethod;

            // We'd like any data lengths done before the header, so that
            // we can offer a meaningful value for Content-Length...
            byte[] abData = null;
            if (!string.IsNullOrEmpty(a_szData))
            {
                abData = Encoding.UTF8.GetBytes(a_szData);
                m_httprequestdata.httpwebrequest.ContentLength = abData.Length;
            }

            // Add any headers we have laying about...
            if (a_aszHeader != null)
            {
                m_httprequestdata.httpwebrequest.Headers = new WebHeaderCollection();
                foreach (string szHeader in a_aszHeader)
                {
                    Log.Verbose("http>>> sendheader " + szHeader);
                    if (szHeader.ToLower().StartsWith("content-type: "))
                    {
                        m_httprequestdata.httpwebrequest.ContentType = szHeader.Remove(0, 14);
                    }
                    else
                    {
                        m_httprequestdata.httpwebrequest.Headers.Add(szHeader);
                    }
                }
            }
            m_httprequestdata.aszRequestHeaders = null;
            if (m_httprequestdata.httpwebrequest.Headers != null)
            {
                int hh = 0;
                if (abData == null)
                {
                    m_httprequestdata.aszRequestHeaders = new string[m_httprequestdata.httpwebrequest.Headers.Keys.Count];
                }
                else
                {
                    m_httprequestdata.aszRequestHeaders = new string[m_httprequestdata.httpwebrequest.Headers.Keys.Count + 1];
                    m_httprequestdata.aszRequestHeaders[hh++] = "Content-Length=" + m_httprequestdata.httpwebrequest.ContentLength;
                }
                for (int kk = 0; kk < m_httprequestdata.httpwebrequest.Headers.Keys.Count; kk++, hh++)
                {
                    if (m_httprequestdata.httpwebrequest.Headers.GetValues(kk) == null)
                    {
                        m_httprequestdata.aszRequestHeaders[hh] = m_httprequestdata.httpwebrequest.Headers.Keys.Get(kk) + "=";
                    }
                    else
                    {
                        m_httprequestdata.aszRequestHeaders[hh] = m_httprequestdata.httpwebrequest.Headers.Keys.Get(kk) + "=" + m_httprequestdata.httpwebrequest.Headers.GetValues(kk).GetValue(0);
                    }
                }
            }

            // Timeout...
            m_httprequestdata.httpwebrequest.Timeout = a_iTimeout;

            // Data we're sending...
            if (abData != null)
            {
                Log.Info("http>>> senddata " + a_szData);
                if (m_httprequestdata.httpwebrequest.ContentType == null)
                {
                    // We shouldn't be getting here...
                    m_httprequestdata.httpwebrequest.ContentType = "application/x-www-form-urlencoded";
                }
                try
                {
                    // This is where we expect to be...
                    stream = m_httprequestdata.httpwebrequest.GetRequestStream();
                    stream.Write(abData, 0, abData.Length);
                    stream.Close();
                }
                catch (WebException webexception)
                {
                    return (CollectWebException("SendData", webexception));
                }
                catch (Exception exception)
                {
                    return (CollectException("SendData", exception));
                }
            }

            // We're sending a file...
            if (a_szUploadFile != null)
            {
                Log.Info("http>>> sendfile " + a_szUploadFile);
                byte[] abFile = File.ReadAllBytes(a_szUploadFile);
                m_httprequestdata.httpwebrequest.ContentLength = abFile.Length;
                try
                {
                    stream = m_httprequestdata.httpwebrequest.GetRequestStream();
                    stream.Write(abFile, 0, abFile.Length);
                    stream.Close();
                }
                catch (WebException webexception)
                {
                    return (CollectWebException("SendFile", webexception));
                }
                catch (Exception exception)
                {
                    return (CollectException("SendFile", exception));
                }
            }

            #endregion


            // Handle the HTTP Response
            #region Handle the HTTP Response

            // We're async, but we wait here for the response...
            try
            {
                // Start the asynchronous request.
                m_httprequestdata.autoreseteventHttpWebRequest = new AutoResetEvent(false);
                IAsyncResult iasyncresult = (IAsyncResult)m_httprequestdata.httpwebrequest.BeginGetResponse(new AsyncCallback(ResponseCallBackLaunchpad), this);

                // this line implements the timeout, if there is a timeout, the callback fires and the request becomes aborted
                m_waithandle = iasyncresult.AsyncWaitHandle;
                m_registeredwaithandle = ThreadPool.RegisterWaitForSingleObject(iasyncresult.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), this, a_iTimeout, true);

                // KEYWORD:RESPONSE
                // The response came in the allowed time. The work processing will happen in the 
                // callback function.  The if-statement is the best place to break if all you
                // want to do it catch the reponse coming back before it's processed...
                m_httprequestdata.autoreseteventHttpWebRequest.WaitOne();
                if (m_registeredwaithandle != null)
                {
                    m_registeredwaithandle.Unregister(m_waithandle);
                }

                // Release the HttpWebResponse.  We've already processed any bad things, and we
                // have all the data, so don't let a throw from this call mess us up...
                try
                {
                    m_httprequestdata.httpwebrequest.GetResponse().Close();
                }
                catch
                {
                    Log.Error(a_szReason + " failed...");
                    return (false);
                }

                // Handle a timeout...
                if (m_blTimeout)
                {
                    return (CollectWebException("GetResponse", new WebException("Timeout", WebExceptionStatus.Timeout)));
                }
            }
            catch (WebException webexception)
            {
                if (m_registeredwaithandle != null)
                {
                    m_registeredwaithandle.Unregister(m_waithandle);
                }
                return (CollectWebException("GetResponse", webexception));
            }
            catch (Exception exception)
            {
                if (m_registeredwaithandle != null)
                {
                    m_registeredwaithandle.Unregister(m_waithandle);
                }
                return (CollectException("GetResponse", exception));
            }

            // Log what we got back......
            Log.Info("http>>> recvdata " + m_httpresponsedata.szResponseData);

            // All done, cleanup and final check...
            if (m_httpresponsedata.httpwebresponse != null)
            {
                m_httpresponsedata.httpwebresponse.Close();
            }
            if (m_httpresponsedata.iResponseHttpStatus >= 300)
            {
                Log.Error(a_szReason + " failed...");
                Log.Error("http>>> sts " + m_httpresponsedata.iResponseHttpStatus);
                Log.Error("http>>> stsreason " + a_szReason + " (" + m_httpresponsedata.szResponseData + ")");
                return (false);
            }
            return (true);

            #endregion
        }

        /// <summary>
        /// Respond to our caller...
        /// </summary>
        /// <param name="a_szCode">error code</param>
        /// <param name="a_szResponse">JSON data</param>
        /// <returns></returns>
        public bool HttpRespond(string a_szCode, string a_szResponse)
        {
            byte[] abBufferJson = null;
            byte[] abBufferThumbnailHeader = null;
            byte[] abBufferThumbnail = null;
            byte[] abBufferImageHeader = null;
            Stream streamResponse = null;
            FileStream filestreamThumbnail = null;
            FileStream filestreamImage = null;
            string szBoundary = "WaFfLeSaReTaStY";

            // Handle a bad X-Privet-Token, we must do this before we do
            // anything else...
            if (a_szCode == "invalid_x_privet_token")
            {
                // Log it...
                Log.Error("http>>> invalid_x_privet_token (error 400)");

                // Build the error...
                string szError =
                    "{" +
                    "\"error\":\"invalid_x_privet_token\"," +
                    "\"description\":\"X-Privet-Token missing or invalid...\"" +
                    "}";
                byte[] abError = Encoding.UTF8.GetBytes(szError);

                // Set the status code...
                m_httplistenerdata.httplistenerresponse.StatusCode = (int)HttpStatusCode.BadRequest;
                m_httplistenerdata.httplistenerresponse.StatusDescription = "Missing X-Privet-Token header.";

                // Set the headers...
                m_httplistenerdata.httplistenerresponse.Headers.Clear();
                m_httplistenerdata.httplistenerresponse.Headers.Add(HttpResponseHeader.ContentType, "application/json; charset=UTF-8");
                m_httplistenerdata.httplistenerresponse.ContentLength64 = abError.Length;

                // Get a response stream and write the response to it...
                streamResponse = m_httplistenerdata.httplistenerresponse.OutputStream;
                streamResponse.Write(abError, 0, abError.Length);
                streamResponse.Close();

                // Cleanup...
                m_httplistenerdata.httplistenerresponse = null;

                // All done...
                return (true);
            }

            // Log it...
            Log.Info("http>>> senddata " + a_szResponse);
            if (!string.IsNullOrEmpty(m_szThumbnailFile))
            {
                Log.Info("http>>> sendthumbnailfile " + m_szThumbnailFile);
            }
            if (!string.IsNullOrEmpty(m_szImageFile))
            {
                Log.Info("http>>> sendimagefile " + m_szImageFile);
            }

            // Protect ourselves from weirdness, we'll only get here if HttpRespond
            // was previously called for this command.  The most likely goof-up is a
            // call to DeviceReturnError() after already responding (please don't
            // ask how I know this)...
            if (m_httplistenerdata.httplistenerresponse == null)
            {
                Log.Error("HttpRespond: second attempt to respond to a command, spank the programmer...");
                return (true);
            }

            // Open our thumbnail file, if we have one...
            if (!string.IsNullOrEmpty(m_szThumbnailFile) && File.Exists(m_szThumbnailFile))
            {
                try
                {
                    filestreamThumbnail = new FileStream(m_szThumbnailFile, FileMode.Open);
                }
                catch (Exception exception)
                {
                    Log.Error("HttpRespond: failed to open..." + exception.Message);
                }
            }

            // Open our image file, if we have one...
            if (!string.IsNullOrEmpty(m_szImageFile) && File.Exists(m_szImageFile))
            {
                try
                {
                    filestreamImage = new FileStream(m_szImageFile, FileMode.Open);
                }
                catch (Exception exception)
                {
                    Log.Error("HttpRespond: failed to open..." + exception.Message);
                }
            }

            // We don't have any files, so just send the JSON data...
            if (    (filestreamThumbnail == null)
                &&  (filestreamImage == null))
            {
                // Convert the JSON to UTF8...
                abBufferJson = Encoding.UTF8.GetBytes(a_szResponse);

                // Fix the header in our response...
                m_httplistenerdata.httplistenerresponse.Headers.Clear();
                m_httplistenerdata.httplistenerresponse.Headers.Add(HttpResponseHeader.ContentType, "application/json; charset=UTF-8");
                m_httplistenerdata.httplistenerresponse.ContentLength64 = abBufferJson.Length;

                // We need some protection...
                try
                {
                    // Get a response stream and write the response to it...
                    streamResponse = m_httplistenerdata.httplistenerresponse.OutputStream;
                    streamResponse.Write(abBufferJson, 0, abBufferJson.Length);

                    // Close the output stream...
                    if (streamResponse != null)
                    {
                        streamResponse.Close();
                    }
                }
                catch (Exception exception)
                {
                    // This is most likely to happen if we lose communication,
                    // or if the application poos itself at an inopportune
                    // moment...
                    Log.Error("response failed - " + exception.Message);
                    m_httplistenerdata.httplistenerresponse = null;
                    return (false);
                }

                // We can't use this anymore, so blow it away...
                m_httplistenerdata.httplistenerresponse = null;

                // All done...
                return (true);
            }

            // Build the JSON portion, don't send anything yet, note the use
            // of newlines, which are essential to parsing multipart content...
            abBufferJson = Encoding.UTF8.GetBytes
            (
                "--" + szBoundary + "\r\n" +
                "Content-Type: application/json; charset=UTF-8\r\n" +
                "Content-Length: " + a_szResponse.Length + "\r\n" +
                "\r\n" +
                a_szResponse + "\r\n" +
                "\r\n"
            );

            // Build the thumbnail portion, if we have one, don't send
            // anything yet...
            if (filestreamThumbnail != null)
            {
                // Build the thumbnail header portion, don't send anything yet...
                abBufferThumbnailHeader = Encoding.UTF8.GetBytes
                (
                    "--" + szBoundary + "\r\n" +
                    "Content-Type: application/pdf\r\n" +
                    "Content-Length: " + filestreamThumbnail.Length + "\r\n" +
                    "Content-Transfer-Encoding: binary\r\n" +
                    "Content-Disposition: inline; filename=\"thumbnail.pdf\"\r\n" +
                    "\r\n"
                );

                // Read the thumbnail data, be sure to add an extra two bytes for
                // the terminating newline and the empty-line newline...
                try
                {
                    abBufferThumbnail = new byte[filestreamThumbnail.Length + 4];
                    filestreamThumbnail.Read(abBufferThumbnail, 0, abBufferThumbnail.Length);
                    abBufferThumbnail[filestreamThumbnail.Length]     = 13; // '\r'
                    abBufferThumbnail[filestreamThumbnail.Length + 1] = 10; // '\n'
                    abBufferThumbnail[filestreamThumbnail.Length + 2] = 13; // '\r'
                    abBufferThumbnail[filestreamThumbnail.Length + 3] = 10; // '\n'
                }
                // Drat...
                catch (Exception exception)
                {
                    Log.Error("HttpRespond: exception..." + exception.Message);
                    abBufferThumbnailHeader = null;
                    abBufferThumbnail = null;
                }

                // Cleanup...
                filestreamThumbnail.Close();
                filestreamThumbnail = null;
            }

            // Build the image header, if we have one...
            if (filestreamImage != null)
            {
                // Build the image header portion, don't send anything yet...
                abBufferImageHeader = Encoding.UTF8.GetBytes
                (
                    "--" + szBoundary + "\r\n" +
                    "Content-Type: application/pdf\r\n" +
                    "Content-Length: " + filestreamImage.Length + "\r\n" +
                    "Content-Transfer-Encoding: binary\r\n" +
                    "Content-Disposition: inline; filename=\"image.pdf\"\r\n" +
                    "\r\n"
                );
            }

            // Okay, send what we have so far, start by specifying the length,
            // note the +4 on the image for the terminating CRLF and the
            // final empty-line CRLF...
            long lLength =
                abBufferJson.Length +
                ((abBufferThumbnailHeader != null) ? abBufferThumbnailHeader.Length : 0) +
                ((abBufferThumbnail != null) ? abBufferThumbnail.Length : 0) +
                ((abBufferImageHeader != null) ? abBufferImageHeader.Length : 0);

            // We're doing a multipart/mixed reply, so fix the header in our response...
            m_httplistenerdata.httplistenerresponse.Headers.Clear();
            m_httplistenerdata.httplistenerresponse.Headers.Add(HttpResponseHeader.ContentType, "multipart/mixed; boundary=\"" + szBoundary + "\"");
            m_httplistenerdata.httplistenerresponse.ContentLength64 =
                lLength +
                ((filestreamImage != null) ? filestreamImage.Length + 4 : 0);

            // Make things a little easier to read...
            streamResponse = m_httplistenerdata.httplistenerresponse.OutputStream;

            // Write the JSON data to the stream, this includes its header...
            streamResponse.Write(abBufferJson, 0, (int)abBufferJson.Length);
            abBufferJson = null;

            // Write the thumbnail header, if we have one...
            if (abBufferThumbnailHeader != null)
            {
                streamResponse.Write(abBufferThumbnailHeader, 0, (int)abBufferThumbnailHeader.Length);
                abBufferThumbnailHeader = null;
            }

            // Write the thumbnail, if we have one...
            if (abBufferThumbnail != null)
            {
                streamResponse.Write(abBufferThumbnail, 0, (int)abBufferThumbnail.Length);
                abBufferThumbnail = null;
            }

            // Write the image header, if we have one...
            if (abBufferImageHeader != null)
            {
                streamResponse.Write(abBufferImageHeader, 0, (int)abBufferImageHeader.Length);
                abBufferImageHeader = null;
            }

            // Now let's send the image portion (if we have one), this could be
            // big, so we'll do it in chunks...
            if (filestreamImage != null)
            {
                try
                {
                    // Loopy on the image...
                    int iReadLength;
                    bool blCrlfsSent = false;
                    byte[] abData = new byte[0x200000];
                    while ((iReadLength = filestreamImage.Read(abData, 0, abData.Length)) > 0)
                    {
                        if ((iReadLength + 4) < abData.Length)
                        {
                            blCrlfsSent = true;
                            abData[iReadLength]     = 13; // '\r'
                            abData[iReadLength + 1] = 10; // '\n'
                            abData[iReadLength + 2] = 13; // '\r'
                            abData[iReadLength + 3] = 10; // '\n'
                            iReadLength += 4;
                        }
                        streamResponse.Write(abData, 0, iReadLength);
                    }

                    // Send the closing newlines, if we could snooker them into
                    // the stream up above...
                    if (!blCrlfsSent)
                    {
                        abData[0] = 13; // '\r'
                        abData[1] = 10; // '\n'
                        abData[2] = 13; // '\r'
                        abData[3] = 10; // '\n'
                        streamResponse.Write(abData, 0, 4);
                    }
                }
                catch (Exception exception)
                {
                    Log.Error("HttpRespond: exception..." + exception.Message);
                }

                // Cleanup...
                filestreamImage.Close();
                filestreamImage = null;
            }

            // Close the output stream...
            if (streamResponse != null)
            {
                streamResponse.Close();
            }

            // We can't use this anymore, so blow it away...
            m_httplistenerdata.httplistenerresponse = null;

            // All done...
            return (true);
        }

        /// <summary>
        /// Are we on a local area network?
        /// </summary>
        /// <returns>return true if we are</returns>
        public bool IsLocal()
        {
            return (m_httplistenerdata.httplistenerresponse != null);
        }

        /// <summary>
        /// Update using data from the IPC...
        /// </summary>
        /// <param name="a_jsonlookup">data being collected</param>
        /// <param name="a_blCapturing">we're capturing or draining</param>
        /// <param name="a_szTdImagesFolder">output to app</param>
        /// <param name="a_szTwImagesFolder">input from twain</param>
        public void UpdateUsingIpcData(JsonLookup a_jsonlookup, bool a_blCapturing, string a_szTdImagesFolder, string a_szTwImagesFolder)
        {
            int iImageBlock;
            string szMeta;

            // Okay, let's turn all this stuff into an array of imageblocks...
            m_szImageBlocks = "";
            if (a_blCapturing)
            {
                // We used to get this from TwainDirect.OnTwain, and we could
                // get away with that when there was a 1:1 correspondence
                // between images and imageBlocks.  But that doesn't fly when
                // we're splitting things up.  So we have to look at our
                // tdimages folder to see what we currently have...
                string[] aszImageBlocks = null;
                try
                {
                    aszImageBlocks = Directory.GetFiles(a_szTdImagesFolder, "img*.meta");
                    if ((aszImageBlocks != null) && (aszImageBlocks.Length > 0))
                    {
                        Array.Sort(aszImageBlocks);
                    }
                }
                catch
                {
                    Log.Error("UpdateUsingIpcData: Directory.GetFiles failed...");
                    return;
                }

                // Build our imageBlocks array...
                if ((aszImageBlocks != null) && (aszImageBlocks.Length > 0))
                {
                    m_szImageBlocks = "[";
                    foreach (string szImageBlock in aszImageBlocks)
                    {
                        // Get the image number from the name, if this proves to
                        // be a stupid idea, then open the file, JSON parse it,
                        // and get it that way...
                        if (!int.TryParse(Path.GetFileNameWithoutExtension(szImageBlock).Replace("img", ""), out iImageBlock))
                        {
                            Log.Error("UpdateUsingIpcData: parsing failed..." + szImageBlock);
                            return;
                        }

                        // Tack it on, we're making [1,2,3...]
                        m_szImageBlocks += (m_szImageBlocks == "[") ? iImageBlock.ToString() : "," + iImageBlock;
                    }
                    m_szImageBlocks += "]";
                }
            }

            // Get the image file (if we have one)...
            m_szImageFile = a_jsonlookup.Get("imageFile",false);

            // Get the thumbnail file (if we have one)...
            m_szThumbnailFile = a_jsonlookup.Get("thumbnailFile", false);

            // Unfortunately, we can't just rely on the imageBlocks array,
            // we have to know if there's pending *.tw* content...
            string[] aszTw = null;
            try
            {
                aszTw = Directory.GetFiles(a_szTwImagesFolder, "*.tw*");
            }
            catch
            {
                // This shouldn't happen...
                Log.Error("UpdateUsingIpcData: Directory.GetFiles failed...");
                return;
            }

            // End of job...
            // - we must be capturing -and-
            // - if we have imageBlocks, we're not done -or-
            // - if we have intermediate *.tw* files, we're not done -or-
            // - if we don't have imageBlocksDrained.meta, we're not done
            m_blSessionImageBlocksDrained = true;
            m_sessiondata.blSessionDoneCapturing = File.Exists(Path.Combine(a_szTdImagesFolder, "imageBlocksDrained.meta"));
            if (    a_blCapturing
                &&  (!string.IsNullOrEmpty(m_szImageBlocks)
                ||  ((aszTw != null) && (aszTw.Length > 0))
                ||  !m_sessiondata.blSessionDoneCapturing))
            {
                m_blSessionImageBlocksDrained = false;
            }

            // The task reply...
            m_szTaskReply = a_jsonlookup.Get("taskReply", false);

            // Get the metadata (if we have any)...
            szMeta = a_jsonlookup.Get("meta",false);
            if (!string.IsNullOrEmpty(szMeta))
            {
                try
                {
                    m_szMetadata = File.ReadAllText(szMeta).TrimEnd(new char[] { '\r', '\n', ' ', '\t' });
                    m_szMetadata = m_szMetadata.Substring(1, m_szMetadata.Length - 2); // remove the outermost {}
                }
                catch (Exception exception)
                {
                    Log.Error("UpdateUsingIpcData: File.ReadAllText failed...<" + szMeta + ">, " + exception.Message);
                }
            }
        }

        /// <summary>
        /// Call the user's function when we get an event.  This should
        /// have been provided in the constructor, just prior to the call
        /// to ClientScannerWaitForEvents...
        /// </summary>
        public void WaitForEventsCallback()
        {
            if (m_waitforeventprocessingcallback != null)
            {
                m_waitforeventprocessingcallback(this, m_objectWaitforeventprocessingcallback);
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions...

        /// <summary>
        /// General error that wasn't caused by HTTP...
        /// </summary>
        public const int c_iNonHttpError = 999999999;

        /// <summary>
        /// TWAIN Direct errors can come from one of several facilities, it's
        /// important to know which one reported a problem.
        /// https - a bad HTTP status code was received
        /// security - a security violation (ex: invalid_x_privet_token)
        /// protocol - an error in TWAIN Local (invalidJson, invalidState, etc)
        /// language - an error in the TWAIN Direct language (invalidTask, invalidValue, etc)
        /// </summary>
        public enum ApiErrorFacility
        {
            undefined,
            httpstatus,
            security,
            protocol,
            language
        }

        /// <summary>
        /// To make it a bit more obvious how the RESTful API commands complete
        /// we have this enumeration.  There are two basic flavors: a simple reply
        /// comes back immediately with the requested data.  A life cycle command
        /// goes through one or more states from queued, to inProgress, to done
        /// (any maybe others along the way).
        /// 
        /// We also have life cycle commands that return with a session object
        /// payload.  We could auto-detect that, but it seems to make more sense
        /// to know that we expect to find it, because it's not supposed to be
        /// optional...
        /// </summary>
        public enum HttpReplyStyle
        {
            Undefined,
            SimpleReply,
            SimpleReplyWithSessionInfo,
            Event
        }

        /// <summary>
        /// A class that captures the request/response information for one
        /// HTTP transaction...
        /// </summary>
        public class Transaction
        {
            /// <summary>
            /// Squirrel away the transaction data...
            /// </summary>
            /// <param name="a_apicmd"></param>
            public Transaction(ApiCmd a_apicmd)
            {
                m_szUriMethod = a_apicmd.m_httplistenerdata.szMethod;
                m_szUriFull = a_apicmd.m_httplistenerdata.szUriFull;
                m_aszRequestHeaders = a_apicmd.GetRequestHeaders();
                m_szRequestData = a_apicmd.GetSendCommand();
                m_lResponseBytesXferred = a_apicmd.GetResponseBytesXferred();
                m_szResponseStatus = a_apicmd.HttpStatus().ToString();
                m_iResponseStatus = a_apicmd.GetResponseStatus();
                m_aszResponseHeaders = a_apicmd.GetResponseHeaders();
                m_aszMultipartHeadersJson = a_apicmd.GetResponseMultipartHeadersJson();
                m_aszMultipartHeadersImage = a_apicmd.GetResponseMultipartHeadersImage();
                m_aszMultipartHeadersThumbnail = a_apicmd.GetResponseMultipartHeadersThumbnail();
                m_szResponseData = a_apicmd.GetResponseData();
            }

            /// <summary>
            /// Get the transaction data in a form suitable for display...
            /// </summary>
            /// <returns></returns>
            public List<string> GetAll()
            {
                List<string> lszTransation = new List<string>();

                // The request...
                lszTransation.Add("REQURI: " + m_szUriMethod + " " + m_szUriFull);
                if (m_aszRequestHeaders != null)
                {
                    foreach (string sz in m_aszRequestHeaders)
                    {
                        lszTransation.Add("REQHDR: " + sz);
                    }
                }
                if (!string.IsNullOrEmpty(m_szRequestData))
                {
                    lszTransation.Add("REQDAT: " + m_szRequestData);
                }

                // The response...
                lszTransation.Add("RSPSTS: " + m_iResponseStatus);
                if (m_aszResponseHeaders != null)
                {
                    foreach (string sz in m_aszResponseHeaders)
                    {
                        lszTransation.Add("RSPHDR: " + sz);
                    }
                }
                if (!string.IsNullOrEmpty(m_szResponseData))
                {
                    lszTransation.Add("RSPDAT: " + m_szResponseData);
                }

                // Return the result...
                return (lszTransation);
            }

            /// <summary>
            /// Get the multipart JSON response headers for this transaction...
            /// </summary>
            /// <returns></returns>
            public string[] GetMultipartHeadersJson()
            {
                return (m_aszMultipartHeadersJson);
            }

            /// <summary>
            /// Get the multipart image response headers for this transaction...
            /// </summary>
            /// <returns></returns>
            public string[] GetMultipartHeadersImage()
            {
                return (m_aszMultipartHeadersImage);
            }

            /// <summary>
            /// Get the multipart thumbnail response headers for this transaction...
            /// </summary>
            /// <returns></returns>
            public string[] GetMultipartHeadersThumbnail()
            {
                return (m_aszMultipartHeadersThumbnail);
            }

            /// <summary>
            /// Get the total number of bytes in the response payload...
            /// </summary>
            /// <returns>total bytes xferred</returns>
            public long GetResponseBytesXferred()
            {
                return (m_lResponseBytesXferred);
            }

            /// <summary>
            /// Get the response data for this transaction, this should
            /// usually be JSON, or include JSON data...
            /// </summary>
            /// <returns></returns>
            public string GetResponseData()
            {
                return (m_szResponseData);
            }

            /// <summary>
            /// Get the response headers for this transaction...
            /// </summary>
            /// <returns></returns>
            public string[] GetResponseHeaders()
            {
                return (m_aszResponseHeaders);
            }

            /// <summary>
            /// Get the HTTP response status for this transaction...
            /// </summary>
            /// <returns></returns>
            public int GetResponseStatus()
            {
                return (m_iResponseStatus);
            }

            /// <summary>
            /// The method: GET, POST, etc...
            /// </summary>
            private string m_szUriMethod;

            /// <summary>
            ///  The full URI that we used...
            /// </summary>
            private string m_szUriFull;

            /// <summary>
            /// Request headers, or null...
            /// </summary>
            private string[] m_aszRequestHeaders;

            /// <summary>
            /// The total number of bytes in the response payload...
            /// </summary>
            private long m_lResponseBytesXferred;

            /// <summary>
            /// Request data or null...
            /// </summary>
            private string m_szRequestData;

            /// <summary>
            /// Response status...
            /// </summary>
            private string m_szResponseStatus;
            private int m_iResponseStatus;

            /// <summary>
            /// Response headers, or null...
            /// </summary>
            private string[] m_aszResponseHeaders;
            private string[] m_aszMultipartHeadersJson;
            private string[] m_aszMultipartHeadersImage;
            private string[] m_aszMultipartHeadersThumbnail;

            /// <summary>
            /// Response data, or null...
            /// </summary>
            private string m_szResponseData;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Initialize the command with the JSON we received, we use this
        /// in places where we don't have any JSON data, so we allow that
        /// field to be null
        /// </summary>
        /// <param name="a_dnssddeviceinfo">the device we're talking to</param>
        /// <param name="a_jsonlookup">the command data or null</param>
        /// <param name="a_httplistenercontext">the request that delivered the jsonlookup data</param>
        /// <param name="a_waitforeventprocessingcallback">callback for each event</param>
        /// <param name="a_objectWaitforeventprocessingcallback">object to pass to the callback</param>
        public void ApiCmdHelper
        (
            Dnssd.DnssdDeviceInfo a_dnssddeviceinfo,
            JsonLookup a_jsonlookup,
            ref HttpListenerContext a_httplistenercontext,
            TwainLocalScanner.WaitForEventsProcessingCallback a_waitforeventprocessingcallback,
            object a_objectWaitforeventprocessingcallback
        )
        {
            // Should we use HTTP or HTTPS?  Our default behavior is to
            // require HTTPS...
            switch (Config.Get("useHttps", "yes"))
            {
                // auto causes us to check the
                // https= field in the mDNS TXT record...
                case "auto":
                    if (a_dnssddeviceinfo != null)
                    {
                        m_blUseHttps = a_dnssddeviceinfo.GetTxtHttps();
                    }
                    break;

                // Force us to use HTTPS, use this to guarantee
                // a secure connection...
                default:
                case "yes":
                    m_blUseHttps = true;
                    break;

                // Force us to use HTTP, use this to force us to
                // use an unsecure connection...
                case "no":
                    m_blUseHttps = false;
                    break;
            }

            // We always need this...
            m_dnssddeviceinfo = a_dnssddeviceinfo;
            m_waitforeventprocessingcallback = a_waitforeventprocessingcallback;
            m_objectWaitforeventprocessingcallback = a_objectWaitforeventprocessingcallback;
            m_httpresponsedata.iResponseHttpStatus = 0;
            m_httpresponsedata.szTwainLocalResponseCode = null;
            m_httpresponsedata.lResponseCharacterOffset = -1;
            m_httpresponsedata.szResponseData = null;
            m_szImageBlocks = "[]";
            m_jsonlookupReceived = null;
            m_httplistenerdata.szUri = null;
            m_httplistenerdata.httplistenercontext = null;
            m_httplistenerdata.httplistenerresponse = null;
            m_sessiondata.blSessionStatusSuccess = true;
            m_sessiondata.szSessionStatusDetected = "nominal";

            // If this is null, we're the initiator, meaning that we're running
            // inside of the application (like TwainDirect.App), so we're
            // done.  Later on this could be TwainDirect.Scanner talking to TWAIN
            // Cloud, but we'll worry about that later...
            if (a_httplistenercontext == null)
            {
                return;
            }

            // Code from this point on is only going to run inside of the
            // TwainDirect.Scanner program for TWAIN Local...

            // Squirrel these away...
            m_jsonlookupReceived = a_jsonlookup;
            m_httplistenerdata.szUri = a_httplistenercontext.Request.RawUrl.ToString();
            m_httplistenerdata.httplistenercontext = a_httplistenercontext;
            m_httplistenerdata.httplistenerresponse = m_httplistenerdata.httplistenercontext.Response;
        }

        /// <summary>
        /// Collect and log information about an exception...
        /// </summary>
        /// <param name="a_szReason">source of the message</param>
        /// <param name="a_exception">the exception we're processing</param>
        /// <returns>true on success</returns>
        private bool CollectException(string a_szReason, Exception a_exception, bool a_blSkipThrow = false)
        {
            // Scoot without any other action...
            if (m_blAbortClientRequest)
            {
                if (a_blSkipThrow)
                {
                    return (false);
                }
                throw a_exception;
            }

            // If it's an event, it's probably our thread being aborted...
            // COR_E_THREADABORTED / 0x80131530 / -2146233040
            if (    (m_httpreplystyle != HttpReplyStyle.Event)
                ||  (System.Runtime.InteropServices.Marshal.GetHRForException(a_exception) != -2146233040))
            {
                Log.Error(a_szReason + " failed...");
                Log.Error("http>>> sts -1");
                Log.Error("http>>> stsreason " + a_szReason + " (" + a_exception.Message + ")");
            }

            // Data to return...
            m_httpresponsedata.iResponseHttpStatus = ApiCmd.c_iNonHttpError;
            m_httpresponsedata.szTwainLocalResponseCode = "communicationError";
            m_httpresponsedata.szResponseData = a_exception.Message;

            // Alert the request that we're done...
            m_httprequestdata.autoreseteventHttpWebRequest.Set();
            return (false);
        }

        /// <summary>
        /// Collect and log information about a web exception...
        /// </summary>
        /// <param name="a_szReason">source of the message</param>
        /// <param name="a_webexception">the web exception we're processing</param>
        /// <returns>true on success</returns>
        private bool CollectWebException(string a_szReason, WebException a_webexception, bool a_blSkipThrow = false)
        {
            string szStatusData = "";
            string szHttpStatusDescription;
            HttpWebResponse httpwebresponse;

            // Scoot without any other action...
            if (m_blAbortClientRequest)
            {
                if (a_blSkipThrow)
                {
                    return (false);
                }
                throw a_webexception;
            }

            // Validate...
            if ((a_webexception == null) || ((HttpWebResponse)a_webexception.Response == null))
            {
                // If it's an event, it's probably our connection being forcibly closed...
                // COR_E_INVALIDOPERATION / 0x80131509 / -2146233079
                if (    (m_httpreplystyle != HttpReplyStyle.Event)
                    ||  (System.Runtime.InteropServices.Marshal.GetHRForException(a_webexception) != -2146233079))
                {
                    Log.Error("http>>> sts web exception (null exception data)");
                    Log.Error("http>>> stsreason " + a_szReason);
                    if (a_webexception == null)
                    {
                        Log.Error("http>>> null web exception data, best guess (if Windows, and HTTPS) is the URL ACL isn't right.  Read up on 'netsh http add/delete urlacl' for more info.");
                    }
                    else
                    {
                        Log.Error("http>>> we have web exception data, let's see what we can dump...");
                        if (!string.IsNullOrEmpty(a_webexception.Message))
                        {
                            Log.Error("http>>> message: " + a_webexception.Message);
                        }
                        if ((a_webexception.GetBaseException() != null) && !string.IsNullOrEmpty(a_webexception.GetBaseException().Message))
                        {
                            Log.Error("http>>> message: " + a_webexception.GetBaseException().Message);
                        }
                    }
                }

                // Handle it...
                if (a_webexception != null)
                {
                    switch (a_webexception.Status)
                    {
                        default:
                            m_webexceptionstatus = a_webexception.Status;
                            m_httpresponsedata.iResponseHttpStatus = 0;
                            m_httpresponsedata.szTwainLocalResponseCode = "critical";
                            m_httpresponsedata.szResponseData = "(no data)";
                            break;
                        case WebExceptionStatus.Timeout:
                            m_webexceptionstatus = a_webexception.Status;
                            m_httpresponsedata.iResponseHttpStatus = 0;
                            m_httpresponsedata.szTwainLocalResponseCode = "timeout";
                            m_httpresponsedata.szResponseData = "(no data)";
                            break;
                    }
                }
                else
                {
                    m_webexceptionstatus = WebExceptionStatus.SendFailure;
                    m_httpresponsedata.iResponseHttpStatus = 503;
                    m_httpresponsedata.szTwainLocalResponseCode = "critical";
                    m_httpresponsedata.szResponseData = "(no data)";
                }
                return (false);
            }

            // Get the status information...
            m_httpresponsedata.iResponseHttpStatus = (int)((HttpWebResponse)a_webexception.Response).StatusCode;
            szHttpStatusDescription = ((HttpWebResponse)a_webexception.Response).StatusDescription;

            // Collect data about the problem...
            httpwebresponse = (HttpWebResponse)a_webexception.Response;
            using (StreamReader streamreader = new StreamReader(httpwebresponse.GetResponseStream()))
            {
                szStatusData = streamreader.ReadToEnd();
            }

            // Log it...
            Log.Error("http>>> sts " + m_httpresponsedata.iResponseHttpStatus + " (" + szHttpStatusDescription + ")");
            Log.Error("http>>> stsreason " + a_szReason + " (" + a_webexception.Message + ")");
            Log.Error("http>>> stsdata " + szStatusData);

            // Data to return...
            m_webexceptionstatus = a_webexception.Status;
            m_httpresponsedata.szTwainLocalResponseCode = "critical";
            m_httpresponsedata.szResponseData = szStatusData;

            // Alert the request that we're done...
            m_httprequestdata.autoreseteventHttpWebRequest.Set();
            return (false);
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        internal void Dispose(bool a_blDisposing)
        {
            // Free managed resources...
            if (m_filestreamOutputFile != null)
            {
                m_filestreamOutputFile.Dispose();
                m_filestreamOutputFile = null;
            }
        }

        /// <summary>
        /// Get the index where target appears in source
        /// </summary>
        /// <param name="a_abSource">source to search</param>
        /// <param name="a_abTarget">target to find</param>
        /// <param name="a_lSourceOffset">optional offset</param>
        /// <param name="a_lSourceLength">optional length override</param>
        /// <returns>index where target starts in source, or -1</returns>
        private long IndexOf(byte[] a_abSource, byte[] a_abTarget, long a_lSourceOffset = 0, long a_lSourceLength = -1)
        {
            long ss;
            long tt;
            long lLength;
            long lSourceLength;

            // Validate...
            if (    (a_abSource == null)
                ||  (a_abTarget == null)
                ||  (a_lSourceOffset < 0)
                ||  (a_lSourceOffset >= a_abSource.Length))
            {
                return (-1);
            }

            // Handle the length override...
            lSourceLength = a_abSource.Length;
            if (a_lSourceLength >= 0)
            {
                lSourceLength = a_lSourceLength;
                if (lSourceLength > a_abSource.Length)
                {
                    return (-1);
                }
            }

            // Edge cases...
            if ((lSourceLength == 0) && (a_abTarget.Length == 0))
            {
                return (0);
            }
            if ((lSourceLength == 0) || (a_abTarget.Length == 0))
            {
                return (-1);
            }
            if (a_abTarget.Length > (lSourceLength - a_lSourceOffset))
            {
                return (-1);
            }

            // Walk the source...
            lLength = (lSourceLength - a_abTarget.Length) + 1;
            for (ss = a_lSourceOffset; (ss < lLength) && (ss < a_abSource.Length); ss++)
            {
                // Walk the target when we get a match...
                if (a_abSource[ss] == a_abTarget[0])
                {
                    for (tt = 0; ((ss + tt) < a_abSource.Length) && (tt < a_abTarget.Length) && (a_abSource[ss + tt] == a_abTarget[tt]); tt++) ;
                    if (tt == a_abTarget.Length)
                    {
                        return (ss);
                    }
                }
            }

            // No joy...
            return (-1);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Definitions...

        /// <summary>
        /// Information about the session...
        /// </summary>
        private struct SessionData
        {
            /// <summary>
            /// We're no longer capturing.  Depending on processing
            /// more imageBlocks could show up.  This information can
            /// be used by an application to closeSession, if they
            /// have no plans to issue another startCapturing, which
            /// means they can release control of a scanner sooner for
            /// those scanners that support multiple sessions...
            /// </summary>
            public bool blSessionDoneCapturing;

            /// <summary>
            /// Set to false if we've detected a problem...
            /// </summary>
            public bool blSessionStatusSuccess;

            /// <summary>
            /// We have a problem, like a paper jam...
            /// </summary>
            public string szSessionStatusDetected;
        }

        /// <summary>
        /// Data for the listener used by TwainDirect.Scanner...
        /// </summary>
        private struct HttpListenerData
        {
            /// <summary>
            /// The HTTP listener context of the command we received...
            /// </summary>
            public HttpListenerContext httplistenercontext;

            /// <summary>
            /// The HTTP response object we use to reply to local area
            /// network commands, this is obtained from m_httplistenerdata.httplistenercontext... 
            /// </summary>
            public HttpListenerResponse httplistenerresponse;

            /// <summary>
            /// The URI used to call us, the method, the base URI, and the full URI with
            /// the port number...
            /// </summary>
            public string szMethod;
            public string szUri;
            public string szUriFull;
        }

        /// <summary>
        /// Data for an HTTP request issued by TwainDirect.App or
        /// TwainDirect.Certification...
        /// </summary>
        private struct HttpRequestData
        {
            /// <summary>
            /// A request object.  We need it broken out so that we have a
            /// way to abort it...
            /// </summary>
            public HttpWebRequest httpwebrequest;

            /// <summary>
            /// An event we set when a request has been completed, that is
            /// when the response callbacks have been fully processed...
            /// </summary>
            public AutoResetEvent autoreseteventHttpWebRequest;

            /// <summary>
            /// The headers that went with the request...
            /// </summary>
            public string[] aszRequestHeaders;
        }

        /// <summary>
        /// Data received in response to an HTTP request...
        /// </summary>
        private struct HttpResponseData
        {
            /// <summary>
            /// The web response to HttpRequestData.httpwebrequest
            /// </summary>
            public HttpWebResponse httpwebresponse;

            /// <summary>
            /// The payload that goes with this response...
            /// </summary>
            public Stream streamHttpWebResponse;

            /// <summary>
            /// The HTTP status that comes with the reply...
            /// </summary>
            public int iResponseHttpStatus;

            /// <summary>
            /// The TWAIN Direct code that goes with the reply, this
            /// includes things like critical and timeout...
            /// </summary>
            public string szTwainLocalResponseCode;

            /// <summary>
            /// Character offset for when JSON hits an error...
            /// </summary>
            public long lResponseCharacterOffset;

            /// <summary>
            /// Headers that went with the data returned to us...
            /// </summary>
            public string[] aszResponseHeaders;

            /// <summary>
            /// Data returned to us...
            /// </summary>
            public string szResponseData;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        // Information about the device we're communicating with...
        private Dnssd.DnssdDeviceInfo m_dnssddeviceinfo;

        /// <summary>
        /// Data for the listener used by TwainDirect.Scanner...
        /// </summary>
        private HttpListenerData m_httplistenerdata;

        /// <summary>
        /// Data for an HTTP request issued by TwainDirect.App or
        /// TwainDirect.Certification...
        /// </summary>
        private HttpRequestData m_httprequestdata;

        /// <summary>
        /// Data received in response to an HTTP request...
        /// </summary>
        private HttpResponseData m_httpresponsedata;

        /// <summary>
        /// Information about the session...
        /// </summary>
        private SessionData m_sessiondata;

        // The callback and its object for the waitForEvents command...
        TwainLocalScanner.WaitForEventsProcessingCallback m_waitforeventprocessingcallback;
        object m_objectWaitforeventprocessingcallback;

        /// <summary>
        /// The command that was sent to us, as a parsed JSON object...
        /// </summary>
        private JsonLookup m_jsonlookupReceived;

        /// <summary>
        /// The facility in the communication that generated an error,
        /// such as http, or the TWAIN Direct language.  There can only
        /// be one of these.
        /// </summary>
        private ApiErrorFacility m_apierrorfacility;

        /// <summary>
        /// The response...
        /// </summary>
        private byte[] m_abBufferHttpWebResponse;
        private long m_lContentLength;
        private string m_szMultipartBoundary;
        private byte[] m_abImageBlockSeperator;
        private byte[] m_abCRLF;
        private byte[] m_abCRLFCRLF;
        private bool m_blApplicationJsonSeen;
        private FileStream m_filestreamOutputFile;
        private long m_lRead;
        private long m_lOffset;
        private bool m_blMultipartApplicationJson;
        private bool m_blMultipartApplicationPdf;
        private bool m_blMultipartApplicationPdfThumbnail;
        private bool m_blMultipartApplicationPdfImage;
        private long m_lMultipartContentLength;
        private long m_lMultipartXferred;
        private long m_lWritten;
        private int m_iHeaders;
        private string[] m_aszHeaders;
        private WaitHandle m_waithandle;
        private RegisteredWaitHandle m_registeredwaithandle;

        /// <summary>
        /// Error codes.  We need the array because a task with multiple
        /// actions can report more than one problem...
        /// </summary>
        private string[] m_aszApiErrorCodes;

        /// <summary>
        /// Brief messages about the problems we detected.  We need the
        /// array because a task with multiple actions can report more
        /// than one problem.
        /// </summary>
        private string[] m_aszApiErrorDescriptions;

        /// <summary>
        /// Event information...
        /// </summary>
        private string m_szEventName;
        private string m_szSessionState;
        private long m_lSessionRevision;

        // Arguments to the request...
        private string m_szReason;
        private string m_szOutputFile;

        /// <summary>
        /// True if we should use HTTPS...
        /// </summary>
        private bool m_blUseHttps;

        // Image blocks (can be null)...
        private string m_szImageBlocks;

        // An image file (can be null or empty)...
        private string m_szImageFile;

        // An a thumbnail image file (can be null or empty)...
        private string m_szThumbnailFile;

        // End of job (true if we're not scanning)...
        private bool m_blSessionImageBlocksDrained;

        /// <summary>
        /// The way we want to respond to an HTTP command...
        /// </summary>
        private HttpReplyStyle m_httpreplystyle;

        /// <summary>
        /// The number of bytes transferred in the response...
        /// </summary>
        private long m_lResponseBytesXferred;

        /// <summary>
        /// The reply task or an empty string...
        /// </summary>
        private string m_szTaskReply;

        /// <summary>
        /// Our TWAIN Direct metadata for an image...
        /// </summary>
        private string m_szMetadata;

        /// <summary>
        /// The command we've sent (or what we tried to send)...
        /// </summary>
        private string m_szSendCommand;

        /// <summary>
        /// Error returns, such as timeout...
        /// </summary>
        private WebExceptionStatus m_webexceptionstatus;

        /// <summary>
        /// We need to scoot on any HTTP exceptions...
        /// </summary>
        private bool m_blAbortClientRequest;
        private bool m_blTimeout;

        /// <summary>
        /// Header information we can get from a multipart/mixed
        /// block of data...
        /// </summary>
        private string[] m_aszMultipartHeadersJson;
        private string[] m_aszMultipartHeadersPdfThumbnail;
        private string[] m_aszMultipartHeadersPdfImage;

        #endregion
    }
}
