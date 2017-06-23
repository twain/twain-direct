///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.Support.HttpServer
//
// A lightweight Http server using HttpListen.  Ideally the code will be written
// such that any server can do the job.  The plan for this is to keep as much
// procotol code out of the server as possible.  The server is just a transport
// layer...
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    13-Oct-2017     Initial Version
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2016-2017 Kodak Alaris Inc.
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Permissions;
using System.Text;

namespace TwainDirect.Support
{
    public sealed class HttpServer : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods

        /// <summary>
        /// Our constructor...
        /// </summary>
        public HttpServer()
        {
            // Nothing needed at this time...
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
        /// Get the port number...
        /// </summary>
        /// <returns></returns>
        public int GetPort()
        {
            return (m_iPort);
        }

        /// <summary>
        /// Start the HTTP server and register us in mDNS / DNS-SD...
        /// </summary>
        /// <param name="a_dispatchcommand">our callback</param>
        /// <param name="a_szInstanceName">the instance name: xxx._twaindirect._sub._privet._tcp.local</param>
        /// <param name="a_iPort">socket port number (can be 0 to auto select)</param>
        /// <param name="a_szTy">the friendly name for the device, not forced to be unique</param>
        /// <param name="a_szUrl">url of cloud server or empty string</param>
        /// <param name="a_szNote">a helpful note about the device (optional)</param>
        /// <returns>true on success</returns>
        public bool ServerStart
        (
            DispatchCommand a_dispatchcommand,
            string a_szInstanceName,
            int a_iPort,
            string a_szTy,
            string a_szUrl,
            string a_szNote
        )
        {
            string szUri;

            // Make a note of our callback...
            m_dispatchcommand = a_dispatchcommand;

            // Create the listener...
            m_httplistener = new HttpListener();

            // HTTPS support for mono, still have to sort out Windows
            // http://stackoverflow.com/questions/13379963/httplistener-with-https-on-monotouch

            // Find a port we can use...
            m_iPort = a_iPort;
            if (m_iPort == 0)
            {
                TcpListener tcplistener = new TcpListener(IPAddress.Any, 0);
                tcplistener.Start();
                m_iPort = ((IPEndPoint)tcplistener.LocalEndpoint).Port;
                tcplistener.Stop();
            }

            // Add our prefixes, we'll accept input from any address on this port
            // which is how the advertisement should work.  We won't register
            // until the service is up.  Note that our default is to require the
            // use of HTTPS...
            if (Config.Get("useHttps", "yes") == "yes")
            {
                szUri = @"https://+:" + m_iPort + "/privet/info/";
                m_httplistener.Prefixes.Add(szUri);
                szUri = @"https://+:" + m_iPort + "/privet/infoex/";
                m_httplistener.Prefixes.Add(szUri);
                szUri = @"https://+:" + m_iPort + "/privet/twaindirect/session/";
                m_httplistener.Prefixes.Add(szUri);
            }
            else
            {
                szUri = @"http://+:" + m_iPort + "/privet/info/";
                m_httplistener.Prefixes.Add(szUri);
                szUri = @"http://+:" + m_iPort + "/privet/infoex/";
                m_httplistener.Prefixes.Add(szUri);
                szUri = @"http://+:" + m_iPort + "/privet/twaindirect/session/";
                m_httplistener.Prefixes.Add(szUri);
            }

            // Start the service...
            try
            {
                m_httplistener.Start();
            }
            catch (Exception exception)
            {
                Log.Error("ServerStart: Start failed..." + exception.Message);
                return (false);
            }

            // Handle stuff async...
            m_iasyncresult = m_httplistener.BeginGetContext(new AsyncCallback(ListenerCallback), m_httplistener);

            // Register our new device...
            m_dnssd = new Dnssd(Dnssd.Reason.Register);
            m_dnssd.RegisterStart(a_szInstanceName, m_iPort, a_szTy, a_szUrl, a_szNote);

            // All done...
            return (true);
        }

        /// <summary>
        /// Stop the HTTP server...
        /// </summary>
        [EnvironmentPermissionAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        public void ServerStop()
        {
            // Kill registration...
            if (m_dnssd != null)
            {
                m_dnssd.RegisterStop();
                m_dnssd = null;
            }

            // Cleanup, the listener closing will
            // actually end this item...
            if (m_iasyncresult != null)
            {
                m_iasyncresult = null;
            }

            // Stop the listener...
            if (m_httplistener != null)
            {
                m_httplistener.Stop();
                m_httplistener.Close();
                m_httplistener = null;
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Definitions

        /// <summary>
        /// Dispatch the command to a callback...
        /// </summary>
        /// <param name="a_szCommand">the JSON command</param>
        /// <param name="a_httplistenerresponse">our response object</param>
        public delegate void DispatchCommand(string a_szCommand, ref HttpListenerContext a_httplistenercontext);

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods

        /// <summary>
        /// Destructor...
        /// </summary>
        ~HttpServer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        internal void Dispose(bool a_blDisposing)
        {
            // Free managed resources...
            if (a_blDisposing)
            {
                if (m_httplistener != null)
                {
                    m_httplistener.Close();
                    m_httplistener = null;
                }
                if (m_dnssd != null)
                {
                    m_dnssd.Dispose();
                    m_dnssd = null;
                }
            }
        }

        /// <summary>
        /// This gets called when we have incoming requests...
        /// </summary>
        /// <param name="result"></param>
        private void ListenerCallback(IAsyncResult a_iasyncresult)
        {
            string szFunction = "ListenerCallback";

            // Protect ourselves from any rug pulling...
            if (m_httplistener == null)
            {
                return;
            }
            if (m_dispatchcommand == null)
            {
                return;
            }

            // Get our listener...
            HttpListener httplistener = (HttpListener)a_iasyncresult.AsyncState;

            // Call EndGetContext to complete the asynchronous operation.
            HttpListenerContext httplistenercontext = httplistener.EndGetContext(a_iasyncresult);

            // Immediately fire off another context, so we can
            // process more stuff, while working on what we've
            // just received...
            m_iasyncresult = m_httplistener.BeginGetContext(new AsyncCallback(ListenerCallback), m_httplistener);

            // Get the request...
            HttpListenerRequest httplistenerrequest = httplistenercontext.Request;

            // Filter out favicon.ico requests if a browser is talking to us...
            string szUrl = httplistenerrequest.RawUrl;
            szUrl = szUrl.Substring(1);
            if (szUrl == "favicon.ico")
            {
                return;
            }

            // Get the payload...
            int iXfer = 0;
            byte[] abBuffer = new byte[65536];
            try
            {
                if (httplistenerrequest.InputStream != null)
                {
                    Stream stream = httplistenerrequest.InputStream;
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
                }
            }
            catch (WebException webexception)
            {
                Log.Error(szFunction + ": webexception, " + webexception.Message);
                return;
            }
            catch (Exception exception)
            {
                Log.Error(szFunction + ": exception, " + exception.Message);
                return;
            }

            // Convert the UTF8 byte array to a string...
            string szPayload = Encoding.UTF8.GetString(abBuffer, 0, iXfer);

            // Do the callback, it'll respond...
            m_dispatchcommand(szPayload, ref httplistenercontext);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes

        /// <summary>
        /// Our listener for incoming TWAIN Direct Client-Server API commands...
        /// </summary>
        private HttpListener m_httplistener;

        /// <summary>
        /// Our current outstanding context...
        /// </summary>
        private IAsyncResult m_iasyncresult;

        /// <summary>
        /// Our port...
        /// </summary>
        private int m_iPort;

        /// <summary>
        /// Used for advertising...
        /// </summary>
        private Dnssd m_dnssd;

        /// <summary>
        /// Our callback for incoming commands...
        /// </summary>
        DispatchCommand m_dispatchcommand;

        #endregion
    }
}
