///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirectSupport.ApiCmd
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
//  Copyright (C) 2015-2016 Kodak Alaris Inc.
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
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading;

namespace TwainDirectSupport
{
    /// <summary>
    /// Manage a single command as it moves through the system, this includes
    /// its lifecycle and responses, including errors...
    /// </summary>
    public sealed class ApiCmd
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Use this constructor when we're initiating a command, which
        /// doesn't happen with TWAIN Local, but will happen with TWAIN
        /// Cloud.
        /// </summary>
        /// <param name="a_dnssddeviceinfo">the device we're talking to</param>
        /// <param name="a_jsonlookup">the command data or null</param>
        public ApiCmd(Dnssd.DnssdDeviceInfo a_dnssddeviceinfo)
        {
            HttpListenerContext httplistenercontext = null;
            ApiCmdHelper(a_dnssddeviceinfo, null, ref httplistenercontext);
        }

        /// <summary>
        /// Initialize the object in response a received command which is
        /// what we mostly doing.  The JsonLookup object contains the
        /// command.  The HttpListenerContext is the object that delivered
        /// the command, and what we use to get our HttpListenerResponse
        /// object for making our reply.
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
            ApiCmdHelper(a_dnssddeviceinfo, a_jsonlookup, ref a_httplistenercontext);
        }

        /// <summary>
        /// Set the device response...
        /// </summary>
        /// <param name="a_blSuccess">true if successful</param>
        /// <param name="a_szResponseCode">things like invalidJson or invalidValue</param>
        /// <param name="a_lResponseCharacterOffset">index of a syntax error or -1</param>
        /// <param name="a_szResponseText">free form text about the problem</param>
        public void DeviceResponseSetStatus(bool a_blSuccess, string a_szResponseCode, long a_lResponseCharacterOffset, string a_szResponseText)
        {
            m_blResponseSuccess = a_blSuccess;
            m_szResponseCode = a_szResponseCode;
            m_lResponseCharacterOffset = a_lResponseCharacterOffset;
            m_szResponseText = a_szResponseText;
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
            return (m_szUri);
        }

        /// <summary>
        /// Set the URI for this command...
        /// </summary>
        /// <param name="a_szUri">uri to remember</param>
        public void SetUri(string a_szUri)
        {
            m_szUri = a_szUri;
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
        /// Return the end of job flag for this command...
        /// </summary>
        /// <returns>true if we're out of images</returns>
        public bool GetEndOfJob()
        {
            return (m_blEndOfJob);
        }

        /// <summary>
        /// Return the HTTP headers...
        /// </summary>
        /// <returns>string with all the data or null</returns>
        public string GetResponseHeaders()
        {
            return (m_szResponseHeaders);
        }

        /// <summary>
        /// Returns the array of image block numbers...
        /// </summary>
        /// <returns>image block numbers (ex: 1, 2)</returns>
        public string GetImageBlocks()
        {
            if (string.IsNullOrEmpty(m_szImageBlocks))
            {
                return ("");
            }
            else
            {
                return (m_szImageBlocks.Replace(" ",""));
            }
        }

        /// <summary>
        /// Returns the array of image block numbers in a format that allows
        /// it to be dropped as-is into a results object (part of the return
        /// for a session object)...
        /// </summary>
        /// <returns>an array of image block numbers (ex: [ 1, 2 ])</returns>
        public string GetImageBlocksJson()
        {
            if (string.IsNullOrEmpty(m_szImageBlocks))
            {
                if (!m_blEndOfJob)
                {
                    return ("\"imageBlocks\":[],");
                }
                else
                {
                    return ("");
                }
            }
            else
            {
                return ("\"imageBlocks\":" + m_szImageBlocks + ",");
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
            return (m_jsonlookupReceived.Get(a_szJsonKey));
        }

        /// <summary>
        /// The command we've sent...
        /// </summary>
        /// <returns>the command string</returns>
        public string GetSendCommand()
        {
            return (m_szSendCommand);
        }

        // Get the parameters task...
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
        public string HttpResponseData()
        {
            if (string.IsNullOrEmpty(m_szResponseData))
            {
                return ("");
            }
            return (m_szResponseData);
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
        /// <returns>true on success</returns>
        public bool HttpRequest
        (
            string a_szReason,
            Dnssd.DnssdDeviceInfo a_dnssddeviceinfo,
            string a_szUri,
            string a_szMethod,
            string[] a_aszHeader,
            string a_szData,
            string a_szUploadFile,
            string a_szOutputFile,
            int a_iTimeout
        )
        {
            int iStatus;
            int iRetry;
            bool blSuccess;

            // Loopy...
            // TDB: the retry count is 1 for TWAIN Local, a larger
            // number might be better for TWAIN Cloud...
            for (iRetry = 0; iRetry < 1; iRetry++)
            {
                // Make an attempt...
                blSuccess = HttpRequestAttempt(a_szReason, a_dnssddeviceinfo, a_szUri, a_szMethod, a_aszHeader, a_szData, a_szUploadFile, a_szOutputFile, a_iTimeout);

                // The attempt was good, so bail...
                if (blSuccess)
                {
                    return (true);
                }

                // Try to get the code, bail if no joy...
                if (!int.TryParse(m_szResponseHttpStatus, out iStatus))
                {
                    return (false);
                }

                // See if we can retry this...
                switch (iStatus)
                {
                    // No idea...so scoot...
                    default:
                        return (false);

                    // Things we can try to recover from...
                    case 500: // Internal server error
                    case 502: // Bad gateway
                    case 503: // Service unavailable
                    case 504: // Gateway timeout
                    case 598: // Network read timeout error (Microsoft)
                    case 599: // Network connect timeout error (Microsoft)
                        Log.Error("http>>> retrying command, attempt #" + (iRetry + 1));
                        Thread.Sleep(1000);
                        continue;
                }
            }

            // Oh well...
            return (false);
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

            // Handle a bad X-Privet-Token...
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
                m_httplistenerresponse.StatusCode = (int)HttpStatusCode.BadRequest;

                // Get a response stream and write the response to it...
                m_httplistenerresponse.ContentLength64 = abError.Length;
                streamResponse = m_httplistenerresponse.OutputStream;
                streamResponse.Write(abError, 0, abError.Length);
                streamResponse.Close();

                // Cleanup...
                m_httplistenerresponse = null;

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
            // call to ReturnError() after already responding (please don't ask how
            // I know this)...
            if (m_httplistenerresponse == null)
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
                m_httplistenerresponse.Headers.Clear();
                m_httplistenerresponse.Headers.Add(HttpResponseHeader.ContentType, "application/json; charset=UTF-8");
                m_httplistenerresponse.ContentLength64 = abBufferJson.Length;

                // Get a response stream and write the response to it...
                streamResponse = m_httplistenerresponse.OutputStream;
                streamResponse.Write(abBufferJson, 0, abBufferJson.Length);

                // Close the output stream...
                if (streamResponse != null)
                {
                    streamResponse.Close();
                }

                // We can't use this anymore, so blow it away...
                m_httplistenerresponse = null;

                // All done...
                return (true);
            }

            // Build the JSON portion, don't send anything yet, note the use
            // of newlines, which are essential to parsing multipart content...
            abBufferJson = Encoding.UTF8.GetBytes
            (
                "--" + szBoundary + "\n" +
                "Content-Type: application/json; charset=UTF-8\n" +
                "Content-Length: " + a_szResponse.Length + "\n" +
                "\n" +
                a_szResponse + "\n" +
                "\n"
            );

            // Build the thumbnail portion, if we have one, don't send
            // anything yet...
            if (filestreamThumbnail != null)
            {
                // Build the thumbnail header portion, don't send anything yet...
                abBufferThumbnailHeader = Encoding.UTF8.GetBytes
                (
                    "--" + szBoundary + "\n" +
                    "Content-Type: application/pdf\n" +
                    "Content-Length: " + filestreamThumbnail.Length + "\n" +
                    "Content-Transfer-Encoding: binary\n" +
                    "Content-Disposition: inline; filename=\"thumbnail.pdf\"" +
                    "\n"
                );

                // Read the thumbnail data, be sure to add an extra two bytes for
                // the terminating newline and the empty-line newline...
                try
                {
                    abBufferThumbnail = new byte[filestreamThumbnail.Length + 2];
                    filestreamThumbnail.Read(abBufferThumbnail, 0, abBufferThumbnail.Length);
                    abBufferThumbnail[abBufferThumbnail.Length] = 10; // '\n'
                    abBufferThumbnail[abBufferThumbnail.Length + 1] = 10; // '\n'
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
                filestreamThumbnail.Dispose();
                filestreamThumbnail = null;
            }

            // Build the image header, if we have one...
            if (filestreamImage != null)
            {
                // Build the image header portion, don't send anything yet...
                abBufferImageHeader = Encoding.UTF8.GetBytes
                (
                    "--" + szBoundary + "\n" +
                    "Content-Type: application/pdf\n" +
                    "Content-Length: " + filestreamImage.Length +"\n" +
                    "Content-Transfer-Encoding: binary\n" +
                    "Content-Disposition: inline; filename=\"image.pdf\"" +
                    "\n"
                );
            }

            // Okay, send what we have so far, start by specifying the length,
            // note the +2 on the image for the terminating newline and the
            // final empty-line newline...
            long lLength =
                abBufferJson.Length +
                ((abBufferThumbnailHeader != null) ? abBufferThumbnailHeader.Length : 0) +
                ((abBufferThumbnail != null) ? abBufferThumbnail.Length : 0) +
                ((abBufferImageHeader != null) ? abBufferImageHeader.Length : 0);

            // We're doing a multipart/mixed reply, so fix the header in our response...
            m_httplistenerresponse.Headers.Clear();
            m_httplistenerresponse.Headers.Add(HttpResponseHeader.ContentType, "multipart/mixed; boundary=\"" + szBoundary + "\"");
            m_httplistenerresponse.ContentLength64 =
                lLength +
                ((filestreamImage != null) ? filestreamImage.Length + 2 : 0);

            // TBD
            // Combine the buffers for what we have so far.  This is done for two
            // reasons.  To make the transfer more efficient, because the system
            // will almost certainly split this into multiple transmissions across
            // the network, and to make parsing easier on the receiving end, because
            // if this content shows up piecemeal we'll have challenges trying to
            // figure out when we have enough data to proceed (note that it can be
            // done using the Content-Length field, and at some point that will be
            // a good fix to make, which is why I have a TBD on this)...
            streamResponse = m_httplistenerresponse.OutputStream;
            byte[] abBuffer = new byte[lLength];
            lLength = 0;
            Buffer.BlockCopy(abBufferJson, 0, abBuffer, 0, abBufferJson.Length);
            lLength = abBufferJson.Length;
            abBufferJson = null;
            if (abBufferThumbnailHeader != null)
            {
                Buffer.BlockCopy(abBufferThumbnailHeader, 0, abBuffer, (int)lLength, abBufferThumbnailHeader.Length);
                lLength += abBufferThumbnailHeader.Length;
                abBufferThumbnailHeader = null;
            }
            if (abBufferThumbnail != null)
            {
                Buffer.BlockCopy(abBufferThumbnail, 0, abBuffer, (int)lLength, abBufferThumbnail.Length);
                lLength += abBufferThumbnail.Length;
                abBufferThumbnail = null;
            }
            if (abBufferImageHeader != null)
            {
                Buffer.BlockCopy(abBufferImageHeader, 0, abBuffer, (int)lLength, abBufferImageHeader.Length);
                lLength += abBufferImageHeader.Length;
                abBufferImageHeader = null;
            }

            // Write this buffer...
            streamResponse.Write(abBuffer, 0, (int)lLength);
            abBuffer = null;

            // Now let's send the image portion (if we have one), this could be
            // big, so we'll do it in chunks...
            if (filestreamImage != null)
            {
                try
                {
                    // Loopy on the image...
                    int iReadLength;
                    byte[] abData = new byte[0x200000];
                    while ((iReadLength = filestreamImage.Read(abData, 0, abData.Length)) > 0)
                    {
                        streamResponse.Write(abData, 0, iReadLength);
                    }

                    // Send the closing newlines...
                    abData[0] = 10; // '\n'
                    abData[1] = 10; // '\n'
                    streamResponse.Write(abData, 0, 2);
                }
                // Drat...
                catch (Exception exception)
                {
                    Log.Error("HttpRespond: exception..." + exception.Message);
                }

                // Cleanup...
                filestreamImage.Close();
                filestreamImage.Dispose();
                filestreamImage = null;
            }

            // Close the output stream...
            if (streamResponse != null)
            {
                streamResponse.Close();
            }

            // We can't use this anymore, so blow it away...
            m_httplistenerresponse = null;

            // All done...
            return (true);
        }

        /// <summary>
        /// Are we on a local area network?
        /// </summary>
        /// <returns>return true if we are</returns>
        public bool IsLocal()
        {
            return (m_httplistenerresponse != null);
        }

        /// <summary>
        /// The command we've sent
        /// </summary>
        /// <param name="a_szSendCommand">the command string</param>
        public void SetSendCommand(string a_szSendCommand)
        {
            m_szSendCommand = a_szSendCommand;
        }

        /// <summary>
        /// Update using data from the IPC...
        /// </summary>
        public void UpdateUsingIpcData(JsonLookup a_jsonlookup, bool a_blCapturing)
        {
            string szMeta;

            // Get the image blocks (if we have any)...
            m_szImageBlocks = a_jsonlookup.Get("session.imageBlocks",false);
            if (m_szImageBlocks != null)
            {
                m_szImageBlocks = m_szImageBlocks.Replace("\r", "").Replace("\n", "");
            }

            // Get the image file (if we have one)...
            m_szImageFile = a_jsonlookup.Get("imageFile",false);

            // Get the thumbnail file (if we have one)...
            m_szThumbnailFile = a_jsonlookup.Get("thumbnailFile", false);

            // End of job...
            if (a_jsonlookup.Get("endOfJob", false) == "false")
            {
                if (a_blCapturing)
                {
                    m_blEndOfJob = false;
                }
                else
                {
                    m_blEndOfJob = true;
                }
            }
            else
            {
                m_blEndOfJob = true;
            }
       
            // The task reply...
            m_szTaskReply = a_jsonlookup.Get("taskReply", false);

            // Get the metadata (if we have any)...
            szMeta = a_jsonlookup.Get("meta",false);
            if (!string.IsNullOrEmpty(szMeta))
            {
                try
                {
                    m_szMetadata = File.ReadAllText(szMeta).TrimEnd(new char[] { '\r', '\n' });
                }
                catch (Exception exception)
                {
                    Log.Error("UpdateUsingIpcData: File.ReadAllText failed...<" + szMeta + ">, " + exception.Message);
                }
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
        public const string c_szNonHttpError = "999999999";

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Collect and log information about an exception...
        /// </summary>
        /// <param name="a_szReason">source of the message</param>
        /// <param name="a_exception">the exception we're processing</param>
        /// <returns>true on success</returns>
        private bool CollectException(string a_szReason, Exception a_exception)
        {
            Log.Error(a_szReason + " failed...");
            Log.Error("http>>> sts " + -1);
            Log.Error("http>>> stsreason " + a_szReason + " (" + a_exception.Message + ")");
            m_blResponseSuccess = false;
            m_szResponseHttpStatus = ApiCmd.c_szNonHttpError;
            m_szResponseCode = "communicationError";
            m_szResponseText = a_exception.Message;
            return (false);
        }

        /// <summary>
        /// Collect and log information about a web exception...
        /// </summary>
        /// <param name="a_szReason">source of the message</param>
        /// <param name="a_webexception">the web exception we're processing</param>
        /// <returns>true on success</returns>
        private bool CollectWebException(string a_szReason, WebException a_webexception)
        {
            HttpWebResponse httpwebresponse;
            string szStatusData = "";
            int iStatuscode;
            string szHttpStatusDescription;

            // Validate...
            if ((a_webexception == null) || ((HttpWebResponse)a_webexception.Response == null))
            {
                Log.Error("http>>> sts web exception (null exception data)");
                Log.Error("http>>> stsreason " + a_szReason);
                m_blResponseSuccess = false;
                m_szResponseHttpStatus = "503";
                m_szResponseCode = "communicationError";
                m_szResponseText = "(no data)";
                return (false);
            }

            // Get the status information...
            iStatuscode = (int)((HttpWebResponse)a_webexception.Response).StatusCode;
            szHttpStatusDescription = ((HttpWebResponse)a_webexception.Response).StatusDescription;

            // Collect data about the problem...
            httpwebresponse = (HttpWebResponse)a_webexception.Response;
            using (StreamReader streamreader = new StreamReader(httpwebresponse.GetResponseStream()))
            {
                szStatusData = streamreader.ReadToEnd();
            }

            // Log it...
            Log.Error("http>>> sts " + iStatuscode + " (" + szHttpStatusDescription + ")");
            Log.Error("http>>> stsreason " + a_szReason + " (" + a_webexception.Message + ")");
            Log.Error("http>>> stsdata " + szStatusData);

            // Return it...
            m_blResponseSuccess = false;
            m_szResponseHttpStatus = iStatuscode.ToString();
            m_szResponseCode = "communicationError";
            m_szResponseText = szStatusData;
            return (false);
        }

        /// <summary>
        /// Use this for any access to CURL...
        /// </summary>
        /// <param name="a_szArguments">reason to log</param>
        /// <param name="a_szArguments">data to send</param>
        /// <returns></returns>
#if USECURL
        private string Curl(string a_szReason, string a_szArguments)
        {
            bool blRedirectStandardOutput = !a_szArguments.Contains(" -o ");

            // Log what we're sending...
            Log.Info("");
            Log.Info("http>>> " + a_szReason);
            Log.Info("http>>> send" + " " + a_szArguments);

            // Start the child process.
            Process p = new Process();

            // Redirect the output stream of the child process.
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = blRedirectStandardOutput;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = "curl";
            p.StartInfo.Arguments = a_szArguments;
            p.Start();

            // Grab the standard output, then wait for the
            // process to fully exit...
            string szReply = "";
            string szError = "";
            if (blRedirectStandardOutput)
            {
                szReply = p.StandardOutput.ReadToEnd();
            }
            szError = p.StandardError.ReadToEnd();
            p.WaitForExit();

            // Sort out the result...
            // TBD: we need a lot more smarts in this area to capture
            // errors, catagorize them, get a human readable bit of
            // text and work out a response.  In most cases we'll just
            // have to flat out fail, but the more data we can give to
            // the user, the better off they'll be...
            if ((szReply == "") && (szError != ""))
            {
                szReply = "ERROR: " + szError;
                //m_szLastHttpError = szError;
            }

            // Log what we got back......
            Log.Info("http>>> recv " + szReply);

            // Return the data as a string (it's usually json)...
            return (szReply);
        }
#endif

        /// <summary>
        /// Initialize the command with the JSON we received, we use this
        /// in places where we don't have any JSON data, so we allow that
        /// field to be null
        /// </summary>
        /// <param name="a_dnssddeviceinfo">the device we're talking to</param>
        /// <param name="a_jsonlookup">the command data or null</param>
        /// <param name="a_httplistenercontext">the request that delivered the jsonlookup data</param>
        public void ApiCmdHelper
        (
            Dnssd.DnssdDeviceInfo a_dnssddeviceinfo,
            JsonLookup a_jsonlookup,
            ref HttpListenerContext a_httplistenercontext
        )
        {
            // Should we use HTTP or HTTPS?
            switch (Config.Get("useHttps", "auto"))
            {
                // Default to auto, which causes us to check the
                // https= field in the mDNS TXT record...
                default:
                case "auto":
                    m_blUseHttps = a_dnssddeviceinfo.blTxtHttps;
                    break;

                // Force us to use HTTPS, use this to guarantee
                // a secure connection...
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
            m_dnssdeviceinfo = a_dnssddeviceinfo;
            m_blResponseSuccess = false;
            m_szResponseHttpStatus = null;
            m_szResponseCode = null;
            m_szResponseText = null;
            m_lResponseCharacterOffset = -1;
            m_szResponseData = null;
            m_szImageBlocks = null;
            m_jsonlookupReceived = null;
            m_szUri = null;
            m_httplistenercontext = null;
            m_httplistenerresponse = null;

            // If this is null, we're the initiator, meaning that we're running
            // inside of the application (like TwainDirectApplication), so we're
            // done.  Later on this could be TwainDirectScanner talking to TWAIN
            // Cloud, but we'll worry about that later...
            if (a_httplistenercontext == null)
            {
                return;
            }

            // Code from this point on is only going to run inside of the
            // TwainDirectScanner program for TWAIN Local...

            // Squirrel these away...
            m_jsonlookupReceived = a_jsonlookup;
            m_szUri = a_httplistenercontext.Request.RawUrl.ToString();
            m_httplistenercontext = a_httplistenercontext;
            m_httplistenerresponse = m_httplistenercontext.Response;
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
            if (a_abTarget.Length > (lSourceLength + a_lSourceOffset))
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

        /// <summary>
        /// Funnel all of our http requests through a single function, so that we have
        /// a centralized way of accessing our target.
        /// 
        /// This function is thread safe so that we can use it to overlap calls to
        /// boost performance, please avoid any modifications that might change that...
        /// </summary>
        /// <param name="a_szReason">reason for the call, for logging</param>
        /// <param name="a_szUri">our target</param>
        /// <param name="a_szMethod">http method (ex: POST, DELETE...)</param>
        /// <param name="a_aszHeader">array of headers to send or null</param>
        /// <param name="a_szData">data to send or null</param>
        /// <param name="a_szUploadFile">upload data from a file</param>
        /// <param name="a_szOutputFile">redirect the data to a file</param>
        /// <param name="a_iTimeout">timeout in milliseconds</param>
        /// <returns>true on success</returns>
        private bool HttpRequestAttempt
        (
            string a_szReason,
            Dnssd.DnssdDeviceInfo a_dnssddeviceinfo,
            string a_szUri,
            string a_szMethod,
            string[] a_aszHeader,
            string a_szData,
            string a_szUploadFile,
            string a_szOutputFile,
            int a_iTimeout
        )
        {
            //
            // The WebRequest method of doing stuff...
            //
            int ii;
            int iXfer = 0;
            bool blMultipart = false;
            long lContentLength;
            long lImageBlockSeperator;
            string szUri;
            string szReply = "";
            string szOutputHeaders = "";
            string szMultipartBoundary = "";
            byte[] abBuffer;
            Stream stream = null;
            HttpWebRequest httpwebrequest;
            HttpWebResponse httpwebresponse;
            string szFunction = "HttpRequestAttempt";

            // Log a reason for being here...
            Log.Info("");
            Log.Info("http>>> " + a_szReason);

            // Pick our URI, prefix the default server, unless the user gives us an override...
            //
            // A silent exception occurs on Webrequest.Create(), it's trapped and doesn't seem
            // to cause any problems, but on Windows if you want to make it go away, then add
            // the next two items to the registry (you only need Wow6432Node on 64-bit OSes)...
            //   HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\.NETFramework\LegacyWPADSupport dword:00000000
            //   HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\LegacyWPADSupport dword:00000000
            if (a_szUri == null)
            {
                Log.Error(szFunction + ": a_szUri is null");
                return (false);
            }
            if ((a_szUri != "/privet/info") && (a_szUri != "/privet/twaindirect/session"))
            {
                Log.Error(szFunction + ": bad a_szUri '" + a_szUri + "'");
                return (false);
            }

            // For HTTPS we need a certificate for the DNS domain, I have no idea if
            // this can be done with a numeric IP, but I know it can be done with a
            // DNS name, and since we're doing mDNS in this case, we want the link local
            // name of the device...
            if (m_blUseHttps)
            {
                szUri = "https://" + a_dnssddeviceinfo.szLinkLocal + ":" + a_dnssddeviceinfo.lPort + a_szUri;
            }

            // Build the URI, for HTTP we can use the IP address to get to our device...
            else
            {
                szUri = "http://" + a_dnssddeviceinfo.szIpv4 + ":" + a_dnssddeviceinfo.lPort + a_szUri;
            }
            Log.Info("http>>> " + a_szMethod + " uri " + szUri);
            httpwebrequest = (HttpWebRequest)WebRequest.Create(szUri);
            httpwebrequest.AllowWriteStreamBuffering = true;
            httpwebrequest.KeepAlive = true;

            // Pick our method...
            httpwebrequest.Method = a_szMethod;

            // Add any headers we have laying about...
            if (a_aszHeader != null)
            {
                httpwebrequest.Headers = new WebHeaderCollection();
                foreach (string szHeader in a_aszHeader)
                {
                    Log.Verbose("http>>> sendheader " + szHeader);
                    if (szHeader.ToLower().StartsWith("content-type: "))
                    {
                        httpwebrequest.ContentType = szHeader.Remove(0, 14);
                    }
                    else if (szHeader.ToLower().StartsWith("content-length: "))
                    {
                        httpwebrequest.ContentLength = long.Parse(szHeader.Remove(0, 16));
                    }
                    else
                    {
                        httpwebrequest.Headers.Add(szHeader);
                    }
                }
            }

            // Timeout...
            httpwebrequest.Timeout = a_iTimeout;

            // Data we're sending...
            if (a_szData != null)
            {
                Log.Info("http>>> senddata " + a_szData);
                byte[] abData = null;
                abData = Encoding.UTF8.GetBytes(a_szData);
                httpwebrequest.ContentLength = abData.Length;
                if (httpwebrequest.ContentType == null)
                {
                    httpwebrequest.ContentType = "application/x-www-form-urlencoded";
                }
                try
                {
                    stream = httpwebrequest.GetRequestStream();
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
                httpwebrequest.ContentLength = abFile.Length;
                try
                {
                    stream = httpwebrequest.GetRequestStream();
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

            // Get the response...
            try
            {
                httpwebresponse = (HttpWebResponse)httpwebrequest.GetResponse();
            }
            catch (WebException webexception)
            {
                return (CollectWebException("GetResponse", webexception));
            }
            catch (Exception exception)
            {
                return (CollectException("GetResponse", exception));
            }

            // Dump the status...
            Log.Info("http>>> recvsts " + (int)(HttpStatusCode)httpwebresponse.StatusCode + " (" + httpwebresponse.StatusCode + ")");

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
            string sz = httpwebresponse.ContentType;
            ContentType contenttype = new ContentType(httpwebresponse.ContentType);

            // application/json with UTF-8 is okay...
            if (contenttype.MediaType.ToLowerInvariant() == "application/json")
            {
                if (contenttype.CharSet.ToLowerInvariant() != "utf-8")
                {
                    Log.Error("HttpRequestAttempt: json encoding not utf-8..." + contenttype.CharSet);
                    return (false);
                }
                blMultipart = false;
            }

            // multipart/mixed is okay, with a boundary...
            else if (contenttype.MediaType.ToLowerInvariant() == "multipart/mixed")
            {
                blMultipart = true;
                szMultipartBoundary = contenttype.Boundary;
                if (string.IsNullOrEmpty(szMultipartBoundary))
                {
                    Log.Error("HttpRequestAttempt: bad boundary...");
                    return (false);
                }
                szMultipartBoundary = "--" + szMultipartBoundary;
            }

            // Anything else is bad...
            else
            {
                Log.Error("HttpRequestAttempt: unknown media type..." + contenttype.MediaType);
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
                        if (a_szOutputFile == null)
                        {
                            szReply = Encoding.UTF8.GetString(abReply, 0, iXfer);
                        }
                        else
                        {
                            File.WriteAllBytes(a_szOutputFile, abReply);
                        }
                    }
                }

                // Else we have a multipart response, and we need to collect
                // and separate all of the data.  The data will arrive in the
                // following format, repeating as necessary to send all of
                // the data...
                //
                // boundary + \n
                // Content-Type: ... + \n
                // Content-Length: # + \n
                // \n
                // data \n
                // \n
                // boundary + \n
                // Content-Type: ... + \n
                // Content-Length: # + \n
                // \n
                // data \n
                // \n
                //
                // Getting the newlines right is part of the challenge...
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
                    // Give us a large buffer to work with, convert the multipart
                    // boundary to a byte array, and init other stuff...
                    abBuffer = new byte[0x200000];
                    byte[] abImageBlockSeperator = Encoding.UTF8.GetBytes(szMultipartBoundary);
                    bool blFirstPass = true;
                    FileStream filestreamOutputFile = null;

                    // If we received a file, this is where we'll dump the second part...
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
                            Log.Error("HttpRequestAttempt: Delete or StreamWriter failed..." + a_szOutputFile + ", " + exception.Message);
                            return (false);
                        }
                    }

                    // Loopy on reading the data...
                    while (true)
                    {
                        // Read some data...
                        int iRead = stream.Read(abBuffer, iXfer, abBuffer.Length - iXfer);

                        // No more data, we can bail...
                        if (iRead == 0)
                        {
                            if (filestreamOutputFile != null)
                            {
                                filestreamOutputFile.Close();
                                filestreamOutputFile.Dispose();
                                filestreamOutputFile = null;
                            }
                            break;
                        }

                        // Handle the JSON / data split...
                        if (blFirstPass)
                        {
                            // Don't come back into here...
                            blFirstPass = false;

                            // Keep a tally of the number of bytes we've read...
                            iXfer += iRead;

                            // We must find our first separator at the 0th index...
                            lImageBlockSeperator = IndexOf(abBuffer, abImageBlockSeperator, 0, iXfer);
                            if (lImageBlockSeperator != 0)
                            {
                                Log.Error("HttprRequestAttempt: failed to find first multipart boundary string...");
                                if (filestreamOutputFile != null)
                                {
                                    filestreamOutputFile.Close();
                                    filestreamOutputFile.Dispose();
                                    filestreamOutputFile = null;
                                }
                                return (false);
                            }

                            // Find the second seperator...
                            lImageBlockSeperator = IndexOf(abBuffer, abImageBlockSeperator, abImageBlockSeperator.Length, iXfer);

                            // If we didn't find it, convert all of the data, otherwise
                            // just convert what we found...
                            if (lImageBlockSeperator < 0)
                            {
                                szReply = Encoding.UTF8.GetString(abBuffer, 0, (int)abBuffer.Length);
                            }
                            else
                            {
                                szReply = Encoding.UTF8.GetString(abBuffer, 0, (int)lImageBlockSeperator);
                            }

                            // Remove everything up to the first {, and after the last }...
                            long lCurly = szReply.IndexOf('{');
                            if (lCurly > 0)
                            {
                                szReply = szReply.Remove(0, (int)lCurly);
                            }
                            lCurly = szReply.LastIndexOf('}');
                            if ((lCurly > 0) && (lCurly < (szReply.Length - 1)))
                            {
                                szReply = szReply.Remove((int)(lCurly + 1));
                            }

                            iXfer = 0;

                            // Look for the image data...
                            /*
                            if (lImageBlockSeperator >= 0)
                            {
                                blFirstPass = false;
                                szReply = Encoding.UTF8.GetString(abBuffer, 0, (int)lImageBlockSeperator);
                                if ((lImageBlockSeperator + abImageBlockSeperator.Length) < iXfer)
                                {
                                    filestreamOutputFile.Write(abBuffer, (int)(lImageBlockSeperator + abImageBlockSeperator.Length), (int)(iXfer - (lImageBlockSeperator + abImageBlockSeperator.Length)));
                                }
                                iXfer = 0;
                            }
                            */ 
                        }
                        else
                        {
                            filestreamOutputFile.Write(abBuffer, 0, iRead);
                        }

                        // Grow the buffer, if needed...
                        if ((iRead > 0) && (iXfer >= abBuffer.Length))
                        {
                            byte[] ab = new byte[abBuffer.Length + 0x200000];
                            abBuffer.CopyTo(ab, 0);
                            abBuffer = ab;
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
            m_szResponseHttpStatus = ((int)httpwebresponse.StatusCode).ToString();
            m_szResponseHeaders = szOutputHeaders;
            m_szResponseData = szReply;
            if (int.Parse(m_szResponseHttpStatus) >= 300)
            {
                Log.Error(a_szReason + " failed...");
                Log.Error("http>>> sts " + m_szResponseHttpStatus);
                Log.Error("http>>> stsreason " + a_szReason + " (" + m_szResponseData + ")");
                m_blResponseSuccess = false;
                return (false);
            }
            m_blResponseSuccess = true;
            return (true);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Definitions...

        /// <summary>
        /// HttpRequest return indecies...
        /// </summary>
        private const int RETURN_STATUS = 0;
        private const int RETURN_DATA = 1;
        private const int RETURN_HEADERS = 2;

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        // Information about the device we're communicating with...
        private Dnssd.DnssdDeviceInfo m_dnssdeviceinfo;

        /// <summary>
        /// The HTTP listener context of the command we received...
        /// </summary>
        private HttpListenerContext m_httplistenercontext;

        /// <summary>
        /// The HTTP response object we use to reply to local area
        /// network commands, this is obtained from m_httplistenercontext... 
        /// </summary>
        private HttpListenerResponse m_httplistenerresponse;

        /// <summary>
        /// The command that was sent to us, as a parsed JSON object...
        /// </summary>
        private JsonLookup m_jsonlookupReceived;

        /// <summary>
        /// The URI used to call us...
        /// </summary>
        private string m_szUri;

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
        private bool m_blEndOfJob;

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
        /// true if the reply indicates success...
        /// </summary>
        private bool m_blResponseSuccess;

        /// <summary>
        /// The HTTP status that comes with the reply...
        /// </summary>
        private string m_szResponseHttpStatus;

        /// <summary>
        /// Data that goes with the reply...
        /// </summary>
        private string m_szResponseCode;

        /// <summary>
        /// A blast of text that goes with an error...
        /// </summary>
        private string m_szResponseText;

        /// <summary>
        /// Character offset for when JSON hits an error...
        /// </summary>
        private long m_lResponseCharacterOffset;

        /// <summary>
        /// Headers that went with the sata returned to us...
        /// </summary>
        private string m_szResponseHeaders;

        /// <summary>
        /// Data returned to us...
        /// </summary>
        private string m_szResponseData;

        #endregion
    }
}
