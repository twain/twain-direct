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

        public HttpListenerContextBase(HttpListenerContext context)
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

        public event EventHandler StreamClosed;

        protected virtual void OnStreamClosed()
        {
            StreamClosed?.Invoke(this, EventArgs.Empty);
        }
    }

    public class HttpListenerResponseBase
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Handle TWAIN Cloud...
        /// </summary>
        /// <param name="deviceCloudSession"></param>
        public HttpListenerResponseBase(DeviceSession a_devicesessionCloud)
        {
            // This is how we know we're a TWAIN Cloud response...
            m_devicesessionCloud = a_devicesessionCloud;

            // Init all the other stuff...
            StatusCode = 200;
            Headers = new WebHeaderCollection();
            OutputStream = new ReactiveMemoryStream();
            Headers.Add(HttpResponseHeader.ContentType, "application/json; charset=UTF-8");
        }

        /// <summary>
        /// Handle TWAIN Local...
        /// </summary>
        /// <param name="response"></param>
        public HttpListenerResponseBase(HttpListenerResponse a_httplistenerresponse)
        {
            // This is how we know we're a TWAIN Local response...
            m_devicesessionCloud = null;

            Headers = a_httplistenerresponse.Headers;
            StatusDescription = a_httplistenerresponse.StatusDescription;
            StatusCode = a_httplistenerresponse.StatusCode;
            OutputStream = a_httplistenerresponse.OutputStream;
            ContentLength64 = a_httplistenerresponse.ContentLength64;
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

        private DeviceSession m_devicesessionCloud;

        //
        // Summary:
        //     Gets or sets the collection of header name/value pairs returned by the server.
        //
        // Returns:
        //     A System.Net.WebHeaderCollection instance that contains all the explicitly set
        //     HTTP headers to be included in the response.
        //
        // Exceptions:
        //   T:System.InvalidOperationException:
        //     The System.Net.WebHeaderCollection instance specified for a set operation is
        //     not valid for a response.
        public WebHeaderCollection Headers { get; set; }

        //
        // Summary:
        //     Gets or sets a text description of the HTTP status code returned to the client.
        //
        // Returns:
        //     The text description of the HTTP status code returned to the client. The default
        //     is the RFC 2616 description for the System.Net.HttpListenerResponse.StatusCode
        //     property value, or an empty string ("") if an RFC 2616 description does not exist.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     The value specified for a set operation is null.
        //
        //   T:System.ArgumentException:
        //     The value specified for a set operation contains non-printable characters.
        public string StatusDescription { get; set; }

        //
        // Summary:
        //     Gets or sets the HTTP status code to be returned to the client.
        //
        // Returns:
        //     An System.Int32 value that specifies the HTTP status code for the requested resource.
        //     The default is System.Net.HttpStatusCode.OK, indicating that the server successfully
        //     processed the client's request and included the requested resource in the response
        //     body.
        //
        // Exceptions:
        //   T:System.ObjectDisposedException:
        //     This object is closed.
        //
        //   T:System.Net.ProtocolViolationException:
        //     The value specified for a set operation is not valid. Valid values are between
        //     100 and 999 inclusive.
        public int StatusCode { get; set; }

        //
        // Summary:
        //     Gets a System.IO.Stream object to which a response can be written.
        //
        // Returns:
        //     A System.IO.Stream object to which a response can be written.
        //
        // Exceptions:
        //   T:System.ObjectDisposedException:
        //     This object is closed.
        public Stream OutputStream { get; set; }

        //
        // Summary:
        //     Gets or sets the number of bytes in the body data included in the response.
        //
        // Returns:
        //     The value of the response's Content-Length header.
        //
        // Exceptions:
        //   T:System.ArgumentOutOfRangeException:
        //     The value specified for a set operation is less than zero.
        //
        //   T:System.InvalidOperationException:
        //     The response is already being sent.
        //
        //   T:System.ObjectDisposedException:
        //     This object is closed.
        public long ContentLength64 { get; set; }

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

    public class HttpWebResponseBase
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
    }
}
