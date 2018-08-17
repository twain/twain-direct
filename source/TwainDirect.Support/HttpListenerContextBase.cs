using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HazyBits.Twain.Cloud.Device;
using Newtonsoft.Json.Linq;

namespace TwainDirect.Support
{
    public class HttpListenerContextBase
    {
        public HttpListenerContextBase()
        { }

        /// <summary>
        /// We need this to avoid a CA2214 error on the constructor...
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(HttpListenerContext context)
        {
            Request = new HttpListenerRequestBase(context.Request);
            Response = new HttpListenerResponseBase(context.Response);
        }

        public virtual HttpListenerRequestBase Request { get; set; }
        public virtual HttpListenerResponseBase Response { get; set; }
    }

    public class ReactiveMemoryStream : MemoryStream
    {
        public override void Close()
        {
            base.Close();
            OnStreamClosed();
        }

        protected virtual void OnStreamClosed()
        {
            StreamClosed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler StreamClosed;
    }

    public class HttpListenerResponseBase : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Handle TWAIN Cloud...
        /// </summary>
        /// <param name="a_devicesessionCloud"></param>
        public HttpListenerResponseBase(DeviceSession a_devicesessionCloud)
        {
            // This is how we know we're a TWAIN Cloud response...
            m_devicesessionCloud = a_devicesessionCloud;
            m_httplistenerresponseLocal = null;

            // Init all the other stuff...
            m_webheadercollection = new WebHeaderCollection();
            m_szStastusDescription = null;
            m_iStatusCode = 200;
            m_streamOutput = new ReactiveMemoryStream();
            m_lContentLength64 = 0;
            m_webheadercollection.Add(HttpResponseHeader.ContentType, "application/json; charset=UTF-8");
        }

        /// <summary>
        /// Handle TWAIN Local...
        /// </summary>
        /// <param name="a_httplistenerresponse"></param>
        public HttpListenerResponseBase(HttpListenerResponse a_httplistenerresponseLocal)
        {
            // This is how we know we're a TWAIN Local response...
            m_httplistenerresponseLocal = a_httplistenerresponseLocal;
            m_devicesessionCloud = null;

            // Init all the other stuff...
            m_webheadercollection = null;
            m_szStastusDescription = null;
            m_iStatusCode = 0;
            m_streamOutput = null;
            m_lContentLength64 = 0;
        }

        /// <summary>
        /// Destructor...
        /// </summary>
        ~HttpListenerResponseBase()
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
        /// Get the body as a string...
        /// </summary>
        /// <returns></returns>
        public string GetBodyString()
        {
            if (OutputStream is MemoryStream stream)
            {
                return (Encoding.UTF8.GetString(stream.ToArray()));
            }

            return (null);
        }

        /// <summary>
        /// Dispatch an image block to TWAIN Cloud or TWAIN Local...
        /// </summary>
        /// <param name="a_szResponse"></param>
        /// <param name="m_szThumbnailFile"></param>
        /// <param name="m_szImageFile"></param>
        /// <returns></returns>
        public bool WriteImageBlockResponse(string a_szResponse, string m_szThumbnailFile, string m_szImageFile)
        {
            // Handle TWAIN Cloud...
            if (m_devicesessionCloud != null)
            {
                return (WriteCloudResponse(a_szResponse, m_szThumbnailFile, m_szImageFile));
            }

            // Handle TWAIN Local...
            return (WriteMultipartResponse(a_szResponse, m_szThumbnailFile, m_szImageFile));
        }

        /// <summary>
        /// TWAIN Cloud and TWAIN Local have to do different things for Headers...
        /// </summary>
        public WebHeaderCollection Headers
        {
            get
            {
                // Handle TWAIN Cloud...
                if (m_devicesessionCloud != null)
                {
                    return (m_webheadercollection);
                }
                // Handle TWAIN Local...
                else if (m_httplistenerresponseLocal != null)
                {
                    return (m_httplistenerresponseLocal.Headers);
                }
                // Ruh-roh...
                else
                {
                    Log.Assert("Okay, how did you pull this off?  Gotta be Local or Cloud, can't be neither...");
                    return (null);
                }
            }
            set
            {
                // Handle TWAIN Cloud...
                if (m_devicesessionCloud != null)
                {
                    m_webheadercollection = value;
                }
                // Handle TWAIN Local...
                else if (m_httplistenerresponseLocal != null)
                {
                    m_httplistenerresponseLocal.Headers = value;
                }
                // Ruh-roh...
                else
                {
                    Log.Assert("Okay, how did you pull this off?  Gotta be Local or Cloud, can't be neither...");
                }
            }
        }

        /// <summary>
        /// TWAIN Cloud and TWAIN Local have to do different things for the StatusDescription...
        /// </summary>
        public string StatusDescription
        {
            get
            {
                // Handle TWAIN Cloud...
                if (m_devicesessionCloud != null)
                {
                    return (m_szStastusDescription);
                }
                // Handle TWAIN Local...
                else if (m_httplistenerresponseLocal != null)
                {
                    return (m_httplistenerresponseLocal.StatusDescription);
                }
                // Ruh-roh...
                else
                {
                    Log.Assert("Okay, how did you pull this off?  Gotta be Local or Cloud, can't be neither...");
                    return (null);
                }
            }
            set
            {
                // Handle TWAIN Cloud...
                if (m_devicesessionCloud != null)
                {
                    m_szStastusDescription = value;
                }
                // Handle TWAIN Local...
                else if (m_httplistenerresponseLocal != null)
                {
                    m_httplistenerresponseLocal.StatusDescription = value;
                }
                // Ruh-roh...
                else
                {
                    Log.Assert("Okay, how did you pull this off?  Gotta be Local or Cloud, can't be neither...");
                }
            }
        }

        /// <summary>
        /// TWAIN Cloud and TWAIN Local have to do different things for the StatusCode...
        /// </summary>
        public int StatusCode
        {
            get
            {
                // Handle TWAIN Cloud...
                if (m_devicesessionCloud != null)
                {
                    return (m_iStatusCode);
                }
                // Handle TWAIN Local...
                else if (m_httplistenerresponseLocal != null)
                {
                    return (m_httplistenerresponseLocal.StatusCode);
                }
                // Ruh-roh...
                else
                {
                    Log.Assert("Okay, how did you pull this off?  Gotta be Local or Cloud, can't be neither...");
                    return (404);
                }
            }
            set
            {
                // Handle TWAIN Cloud...
                if (m_devicesessionCloud != null)
                {
                    m_iStatusCode = value;
                }
                // Handle TWAIN Local...
                else if (m_httplistenerresponseLocal != null)
                {
                    m_httplistenerresponseLocal.StatusCode = value;
                }
                // Ruh-roh...
                else
                {
                    Log.Assert("Okay, how did you pull this off?  Gotta be Local or Cloud, can't be neither...");
                }
            }
        }

        /// <summary>
        /// TWAIN Cloud and TWAIN Local have to do different things for the OutputStream...
        /// </summary>
        public Stream OutputStream
        {
            get
            {
                // Handle TWAIN Cloud...
                if (m_devicesessionCloud != null)
                {
                    return (m_streamOutput);
                }
                // Handle TWAIN Local...
                else if (m_httplistenerresponseLocal != null)
                {
                    return (m_httplistenerresponseLocal.OutputStream);
                }
                // Ruh-roh...
                else
                {
                    Log.Assert("Okay, how did you pull this off?  Gotta be Local or Cloud, can't be neither...");
                    return (null);
                }
            }
        }

        /// <summary>
        /// TWAIN Cloud and TWAIN Local have to do different things for the ContentLength64...
        /// </summary>
        public long ContentLength64
        {
            get
            {
                // Handle TWAIN Cloud...
                if (m_devicesessionCloud != null)
                {
                    return (m_lContentLength64);
                }
                // Handle TWAIN Local...
                else if (m_httplistenerresponseLocal != null)
                {
                    return (m_httplistenerresponseLocal.ContentLength64);
                }
                // Ruh-roh...
                else
                {
                    Log.Assert("Okay, how did you pull this off?  Gotta be Local or Cloud, can't be neither...");
                    return (-1);
                }
            }
            set
            {
                // Handle TWAIN Cloud...
                if (m_devicesessionCloud != null)
                {
                    m_lContentLength64 = value;
                }
                // Handle TWAIN Local...
                else if (m_httplistenerresponseLocal != null)
                {
                    try
                    {
                        m_httplistenerresponseLocal.ContentLength64 = value;
                    }
                    catch (Exception exception)
                    {
                        //tbd:mlm why does this happen?
                        Log.Error("Look into this sometimes - " + exception.Message);
                    }
                }
                // Ruh-roh...
                else
                {
                    Log.Assert("Okay, how did you pull this off?  Gotta be Local or Cloud, can't be neither...");
                }
            }
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        protected virtual void Dispose(bool a_blDisposing)
        {
            // Free managed resources...
            if (a_blDisposing)
            {
                if (m_streamOutput != null)
                {
                    m_streamOutput.Dispose();
                    m_streamOutput = null;
                }
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Upload a block to the cloud...
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private async Task<string> UploadBlock(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
            {
                var bytes = File.ReadAllBytes(fileName);
                return await m_devicesessionCloud.UploadBlock(bytes);
            }

            return null;
        }

        /// <summary>
        /// Handle TWAIN Cloud response...
        /// </summary>
        /// <param name="a_szResponse"></param>
        /// <param name="m_szThumbnailFile"></param>
        /// <param name="m_szImageFile"></param>
        /// <returns></returns>
        private bool WriteCloudResponse(string a_szResponse, string m_szThumbnailFile, string m_szImageFile)
        {
            var task = Task.Run(async () => await WriteCloudResponseAsync(a_szResponse, m_szThumbnailFile, m_szImageFile));
            task.Wait();

            return task.Result;
        }

        /// <summary>
        /// Handle TWAIN Cloud async portion of response...
        /// </summary>
        /// <param name="a_szResponse"></param>
        /// <param name="m_szThumbnailFile"></param>
        /// <param name="m_szImageFile"></param>
        /// <returns></returns>
        private async Task<bool> WriteCloudResponseAsync(string a_szResponse, string m_szThumbnailFile, string m_szImageFile)
        {
            var thumbnailBlockId = await UploadBlock(m_szThumbnailFile);
            var imageBlockId = await UploadBlock(m_szImageFile);

            if (imageBlockId != null || thumbnailBlockId != null)
            {
                var jsonObject = JObject.Parse(a_szResponse);
                jsonObject["results"]["imageBlockId"] = imageBlockId;
                jsonObject["results"]["thumbnailBlockId"] = thumbnailBlockId;
                a_szResponse = jsonObject.ToString();
            }

            return WriteJsonResponse(a_szResponse);
        }

        /// <summary>
        /// Handle TWAIN Local response...
        /// </summary>
        /// <param name="a_szResponse"></param>
        /// <param name="m_szThumbnailFile"></param>
        /// <param name="m_szImageFile"></param>
        /// <returns></returns>
        private bool WriteMultipartResponse(string a_szResponse, string m_szThumbnailFile, string m_szImageFile)
        {
            byte[] abBufferJson = null;
            byte[] abBufferThumbnailHeader = null;
            byte[] abBufferThumbnail = null;
            byte[] abBufferImageHeader = null;

            FileStream filestreamThumbnail = null;
            FileStream filestreamImage = null;
            Stream streamResponse = null;
            string szBoundary = "WaFfLeSaReTaStY";

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
            if ((filestreamThumbnail == null)
                && (filestreamImage == null))
            {
                return WriteJsonResponse(a_szResponse);
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
                    abBufferThumbnail[filestreamThumbnail.Length] = 13; // '\r'
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

            // Don't forget the boundary terminator...
            byte[] abBoundaryTerminator = Encoding.UTF8.GetBytes("--" + szBoundary + "--\r\n");

            // Okay, send what we have so far, start by specifying the length,
            // note the +4 on the image for the terminating CRLF and the
            // final empty-line CRLF...
            long lLength =
                abBufferJson.Length +                                                           // separator + header + json + CRLFs
                ((abBufferThumbnailHeader != null) ? abBufferThumbnailHeader.Length : 0) +      // separator + thumbnail header + CRLFs (optional)
                ((abBufferThumbnail != null) ? abBufferThumbnail.Length : 0) +                  // thumbnail image + CRLFs              (optional)
                ((abBufferImageHeader != null) ? abBufferImageHeader.Length : 0) +              // separator + image header + CRLFs     (optional)
                ((filestreamImage != null) ? filestreamImage.Length + 4 : 0) +                  // image + CRLFs                        (optional)
                abBoundaryTerminator.Length;                                                    // terminator

            // We're doing a multipart/mixed reply, so fix the header in our response...
            Headers.Clear();
            Headers.Add(HttpResponseHeader.ContentType, "multipart/mixed; boundary=\"" + szBoundary + "\"");
            ContentLength64 = lLength;

            // Make things a little easier to read...
            streamResponse = OutputStream;

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
                            abData[iReadLength] = 13; // '\r'
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

            // Send the boundary terminator...
            streamResponse.Write(abBoundaryTerminator, 0, abBoundaryTerminator.Length);

            // Close the output stream...
            if (streamResponse != null)
            {
                streamResponse.Close();
            }

            // All done...
            return (true);
        }

        private bool WriteJsonResponse(string a_szResponse)
        {
            // Convert the JSON to UTF8...
            var abBufferJson = Encoding.UTF8.GetBytes(a_szResponse);

            // Fix the header in our response...
            Headers.Clear();
            Headers.Add(HttpResponseHeader.ContentType, "application/json; charset=UTF-8");
            ContentLength64 = abBufferJson.Length;

            // We need some protection...
            try
            {
                // Get a response stream and write the response to it...
                var streamResponse = OutputStream;
                streamResponse.Write(abBufferJson, 0, abBufferJson.Length);

                // Close the output stream...
                streamResponse.Close();
            }
            catch (Exception exception)
            {
                // This is most likely to happen if we lose communication,
                // or if the application poos itself at an inopportune
                // moment...
                Log.Error("response failed - " + exception.Message);
                return (false);
            }

            // All done...
            return (true);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Definitions...

        /// <summary>
        /// Handle TWAIN Cloud...
        /// </summary>
        private DeviceSession m_devicesessionCloud;

        /// <summary>
        /// Handle TWAIN Local, we have to use this object directly, otherwise
        /// we won't properly update items like ContentLength64...
        /// </summary>
        private HttpListenerResponse m_httplistenerresponseLocal;

        /// <summary>
        /// Local items for the Cloud...
        /// </summary>
        private WebHeaderCollection m_webheadercollection;
        private string m_szStastusDescription;
        private int m_iStatusCode;
        private Stream m_streamOutput;
        private long m_lContentLength64;

        #endregion
    }

    public class HttpListenerRequestBase
    {
        public HttpListenerRequestBase()
        {

        }

        public HttpListenerRequestBase(HttpListenerRequest request)
        {
            UserHostName = request.UserHostName;
            Url = request.Url;
            RawUrl = request.RawUrl;
            HttpMethod = request.HttpMethod;
            Headers = request.Headers;
        }

        //
        // Summary:
        //     Gets the DNS name and, if provided, the port number specified by the client.
        //
        // Returns:
        //     A System.String value that contains the text of the request's Host header.
        public string UserHostName { get; set; }
        //
        // Summary:
        //     Gets the System.Uri object requested by the client.
        //
        // Returns:
        //     A System.Uri object that identifies the resource requested by the client.
        public Uri Url { get; set; }
        //
        // Summary:
        //     Gets the URL information (without the host and port) requested by the client.
        //
        // Returns:
        //     A System.String that contains the raw URL for this request.
        public string RawUrl { get; set; }
        //
        // Summary:
        //     Gets the HTTP method specified by the client.
        //
        // Returns:
        //     A System.String that contains the method used in the request.
        public string HttpMethod { get; set; }
        //
        // Summary:
        //     Gets the collection of header name/value pairs sent in the request.
        //
        // Returns:
        //     A System.Net.WebHeaderCollection that contains the HTTP headers included in the
        //     request.
        public NameValueCollection Headers { get; set; }
    }

    public class HttpWebResponseBase : IDisposable
    {
        private readonly HttpWebResponse _response;
        private readonly Stream _responseStream;

        public HttpWebResponseBase(string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            _responseStream = new MemoryStream(bytes);

            ContentLength = bytes.Length;
        }

        public HttpWebResponseBase(HttpWebResponse response)
        {
            _response = response;

            StatusCode = response.StatusCode;
            Headers = response.Headers;
            ContentLength = response.ContentLength;
            ContentType = response.ContentType;
        }

        /// <summary>
        /// Destructor...
        /// </summary>
        ~HttpWebResponseBase()
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

        public string ContentType { get; set; }

        public long ContentLength { get; set; }

        public NameValueCollection Headers { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public void Close()
        {
            _response?.Close();
        }

        public Stream GetResponseStream()
        {
            return _response != null ? _response?.GetResponseStream() : _responseStream;
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        protected virtual void Dispose(bool a_blDisposing)
        {
            // Free managed resources...
            if (a_blDisposing)
            {
                if (_responseStream != null)
                {
                    _responseStream.Dispose();
                }
            }
        }
    }
}
