///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirectSupport.Xmpp
// TwainDirectSupport.SslTcpClient
// TwainDirectSupport.XmppCallback
//
// Interface to XMPP for clients and scanners (those as of this writing it's only
// confirmed to be working with scanners).  The intention is to rely on event
// notification instead of polling.
//
// I looked for existing libraries, but ran into the usual problem of being too
// big, too wrong-licensed, too complex, or too not-supported-anymore.  So the goal
// with this bit of code is to make something very small and very focused.
//
// Bits of weirdness to be aware of:
//
// - the first part of the transaction on the socket is unsecured, this the bit
//   where we make sure we can do this, technically, if it failed we could keep
//   using the socket in unsecure mode, but that seems like a bad idea.
//
// - after the server says to "proceed" we switch to a secure connection.
//
// - sometimes we see NULs or blanks arriving, these are discarded.
//
// - data doesn't always arrive in a single packet, so we stitch the data together
//   until we have a completed bit of XML.
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
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace TwainDirectSupport
{
    /// <summary>
    /// This is where we handle the bulk of the XMPP work...
    /// </summary>
    public sealed class Xmpp : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Destructor...
        /// </summary>
        ~Xmpp()
        {
            Dispose(true);
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
        /// Start monitoring for notifications...
        /// </summary>
        /// <param name="a_szAuthorizer">authorization entity</param>
        /// <param name="a_szUsername">user using us</param>
        /// <param name="a_szCredential">oauth2 access token</param>
        /// <param name="a_timercallbackEvent">a timer routine</param>
        /// <returns>true on success</returns>
        public bool NotificationsStart(string a_szAuthorizer, string a_szUsername, string a_szCredential, TimerCallback a_timercallbackEvent, object a_objectEvent)
        {
            int iRetry;
            int iRetryLimit = 5;

            // Save stuff...
            m_szAuthorizer = a_szAuthorizer;
            m_timercallbackEvent = a_timercallbackEvent;
            m_objectEvent = a_objectEvent;

            // We're willing to rety this a few times...
            for (iRetry = 0; iRetry < iRetryLimit; iRetry++)
            {
                // Try to get TLS...
                if (!TLSHandshake())
                {
                    Cleanup();
                    continue;
                }

                // Try to get SASL...
                if (!SASLAuthentication(a_szUsername, a_szCredential))
                {
                    Cleanup();
                    continue;
                }

                // Initialize the stream...
                if (!InitializeStream())
                {
                    Cleanup();
                    continue;
                }

                // Subscribe for notifications...
                if (!Subscribe())
                {
                    Cleanup();
                    continue;
                }

                // Woot!
                break;
            }

            // Ruh-roh...
            if (iRetry >= iRetryLimit)
            {
                Cleanup();
                return (false);
            }

            // Start monitoring...
            m_ssltcpclient.BeginRead();

            // All done...
            return (true);
        }

        /// <summary>
        /// Stop monitoring for notifications...
        /// </summary>
        public void NotificationsStop()
        {
            Cleanup();
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Cleanup...
        /// </summary>
        internal void Dispose(bool a_blDispose)
        {
            Cleanup();
        }

        /// <summary>
        /// Because we can't call Dispose without upsetting the analyzer...
        /// </summary>
        internal void Cleanup()
        {
            if (m_ssltcpclient != null)
            {
                m_ssltcpclient.Close();
                m_ssltcpclient = null;
            }
        }

        /// <summary>
        /// TLS handshake...
        /// </summary>
        /// <returns></returns>
        private bool TLSHandshake()
        {
            string sz;

            // Log stuff...
            Log.Info("");
            Log.Info("TLS Handlshake...");

            // Open...
            m_ssltcpclient = new SslTcpClient();
            m_ssltcpclient.Open("xmpp server", 5222, m_timercallbackEvent, m_objectEvent);

            // TLS: send #1...
            try
            {
                string szData = "<stream:stream to=\"" + m_szAuthorizer + "\" xml:lang=\"en\" version=\"1.0\" xmlns:stream=\"http://etherx.jabber.org/streams\" xmlns=\"jabber:client\">";
                Log.Info("SEND: " + szData);
                m_ssltcpclient.Write(szData);
            }
            catch (Exception exception)
            {
                Log.Error("send failed..." + exception.Message);
                return (false);
            }

            // TLS: receive #1.1...
            try
            {
                sz = "";
                while ((sz == "") || (sz == " "))
                {
                    sz = m_ssltcpclient.Read(1000);
                    if (sz == null)
                    {
                        Log.Error("receive timed out...");
                        return (false);
                    }
                }
                Log.Info("RECV: " + sz);
            }
            catch (Exception exception)
            {
                Log.Error("receive failed..." + exception.Message);
                return (false);
            }

            // TLS: receive #1.2...
            try
            {
                if (!sz.Contains("<stream:features>"))
                {
                    sz = "";
                    while ((sz == "") || (sz == " "))
                    {
                        sz = m_ssltcpclient.Read(5000);
                        if (sz == null)
                        {
                            Log.Error("receive timed out...");
                            return (false);
                        }
                    }
                    Log.Info("RECV: " + sz);
                }
                if (!sz.Contains("X-TOKENNAME-TOKEN"))
                {
                    Log.Error("unexpected response..." + sz);
                    return (false);
                }
            }
            catch (Exception exception)
            {
                Log.Error("receive failed..." + exception.Message);
                return (false);
            }

            // TLS: send #2...
            try
            {
                string szData = "<starttls xmlns=\"urn:ietf:params:xml:ns:xmpp-tls\"/>";
                Log.Info("SEND: " + szData);
                m_ssltcpclient.Write(szData);
            }
            catch (Exception exception)
            {
                Log.Error("send failed..." + exception.Message);
                return (false);
            }

            // TLS: receive #2.1...
            try
            {
                sz = "";
                while ((sz == "") || (sz == " "))
                {
                    sz = m_ssltcpclient.Read(1000);
                    if (sz == null)
                    {
                        Log.Error("receive timed out...");
                        return (false);
                    }
                }
                Log.Info("RECV: " + sz);
                if (!sz.Contains("proceed"))
                {
                    Log.Error("unexpected response..." + sz);
                    return (false);
                }
            }
            catch (Exception exception)
            {
                Log.Error("receive failed..." + exception.Message);
                return (false);
            }

            // Security...
            m_ssltcpclient.Secure("security server");

            // All done...
            return (true);
        }

        /// <summary>
        /// SASL authentication...
        /// </summary>
        /// <param name="a_szUsername">user trying to get in</param>
        /// <param name="a_szCredentials">oauth2 access token</param>
        /// <returns>true on success</returns>
        private bool SASLAuthentication(string a_szUsername, string a_szCredentials)
        {
            string sz;

            // Log stuff...
            Log.Info("");
            Log.Info("SASL Authentication...");

            // SASL: send #1...
            try
            {
                string szData = "<stream:stream to=\"" + m_szAuthorizer + "\" xml:lang=\"en\" version=\"1.0\" xmlns:stream=\"http://etherx.jabber.org/streams\" xmlns=\"jabber:client\">";
                Log.Info("SEND: " + szData);
                m_ssltcpclient.Write(szData);
            }
            catch (Exception exception)
            {
                Log.Error("send failed..." + exception.Message);
                return (false);
            }

            // SASL: receive #1.1...
            try
            {
                sz = "";
                while ((sz == "") || (sz == " "))
                {
                    sz = m_ssltcpclient.Read(1000);
                    if (sz == null)
                    {
                        Log.Error("receive timed out...");
                        return (false);
                    }
                }
                Log.Info("RECV: " + sz);
            }
            catch (Exception exception)
            {
                Log.Error("receive failed..." + exception.Message);
                return (false);
            }

            // SASL: receive #1.2...
            try
            {
                if (!sz.Contains("<stream:features>"))
                {
                    sz = "";
                    while ((sz == "") || (sz == " "))
                    {
                        sz = m_ssltcpclient.Read(1000);
                        if (sz == null)
                        {
                            Log.Error("receive timed out...");
                            return (false);
                        }
                    }
                    Log.Info("RECV: " + sz);
                }
            }
            catch (Exception exception)
            {
                Log.Error("receive failed..." + exception.Message);
                return (false);
            }

            // SASL: send #2...
            try
            {
                byte[] abCredential = Encoding.UTF8.GetBytes("\0" + a_szUsername + "\0" + a_szCredentials);
                string szCredential = Convert.ToBase64String(abCredential);
                string szData =
                    "<auth xmlns=\"urn:ietf:params:xml:ns:xmpp-sasl\" mechanism=\"X-OAUTH2\">" +
                    szCredential +
                    "</auth>";
                Log.Info("SEND: " + szData);
                m_ssltcpclient.Write(szData);
            }
            catch (Exception exception)
            {
                Log.Error("send failed..." + exception.Message);
                return (false);
            }

            // SASL: receive #2.1...
            try
            {
                sz = "";
                while ((sz == "") || (sz == " "))
                {
                    sz = m_ssltcpclient.Read(1000);
                    if (sz == null)
                    {
                        Log.Error("receive timed out...");
                        return (false);
                    }
                }
                Log.Info("RECV: " + sz);
                if (!sz.Contains("success"))
                {
                    Log.Error("unexpected response..." + sz);
                    return (false);
                }
            }
            catch (Exception exception)
            {
                Log.Error("receive failed..." + exception.Message);
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Initializing the stream...
        /// </summary>
        /// <returns>true on success</returns>
        private bool InitializeStream()
        {
            string sz;

            // Log stuff...
            Log.Info("");
            Log.Info("Initialize Stream...");

            // Init stuff...
            m_iId = 0;

            // InitializingStream: send #1...
            try
            {
                string szData = "<stream:stream to=\"" + m_szAuthorizer + "\" xml:lang=\"en\" version=\"1.0\" xmlns:stream=\"http://etherx.jabber.org/streams\" xmlns=\"jabber:client\">";
                Log.Info("SEND: " + szData);
                m_ssltcpclient.Write(szData);
            }
            catch (Exception exception)
            {
                Log.Error("send failed..." + exception.Message);
                return (false);
            }

            // InitializeStream: receive #1.1...
            try
            {
                sz = "";
                while ((sz == "") || (sz == " "))
                {
                    sz = m_ssltcpclient.Read(1000);
                    if (sz == null)
                    {
                        Log.Error("receive timed out...");
                        return (false);
                    }
                }
                Log.Info("RECV: " + sz);
                if (!sz.Contains(m_szAuthorizer))
                {
                    Log.Error("unexpected response..." + sz);
                    return (false);
                }
            }
            catch (Exception exception)
            {
                Log.Error("receive failed..." + exception.Message);
                return (false);
            }

            // InitializeStream: receive #1.2...
            try
            {
                if (!sz.Contains("<stream:features>"))
                {
                    sz = "";
                    while ((sz == "") || (sz == " "))
                    {
                        sz = m_ssltcpclient.Read(1000);
                        if (sz == null)
                        {
                            Log.Error("receive timed out...");
                            return (false);
                        }
                    }
                    Log.Info("RECV: " + sz);
                }
            }
            catch (Exception exception)
            {
                Log.Error("receive failed..." + exception.Message);
            }

            // InitializeStream: send #2...
            try
            {
                string szData =
                    "<iq type=\"set\" id=\"" + (++m_iId) + "\">" +
                    "<bind xmlns=\"urn:ietf:params:xml:ns:xmpp-bind\"/>" +
                    "</iq>";
                Log.Info("SEND: " + szData);
                m_ssltcpclient.Write(szData);
            }
            catch (Exception exception)
            {
                Log.Error("send failed..." + exception.Message);
                return (false);
            }

            // InitializeStream: receive #2.1...
            try
            {
                sz = "";
                while ((sz == "") || (sz == " "))
                {
                    sz = m_ssltcpclient.Read(1000);
                    if (sz == null)
                    {
                        Log.Error("receive timed out...");
                        return (false);
                    }
                }
                Log.Info("RECV: " + sz);
                m_szFullJid = sz.Remove(0, sz.IndexOf("<jid>") + 5);
                m_szFullJid = m_szFullJid.Remove(m_szFullJid.IndexOf("</jid>"));
                m_szBareJid = m_szFullJid.Remove(m_szFullJid.IndexOf("/"));
            }
            catch (Exception exception)
            {
                Log.Error("receive failed..." + exception.Message);
                return (false);
            }

            // InitializeStream: send #3...
            try
            {
                string szData =
                    "<iq type=\"set\" id=\"" + (++m_iId) + "\">" +
                    "<session xmlns=\"urn:ietf:params:xml:ns:xmpp-session\"/>" +
                    "</iq>";
                Log.Info("SEND: " + szData);
                m_ssltcpclient.Write(szData);
            }
            catch (Exception exception)
            {
                Log.Error("send failed..." + exception.Message);
                return (false);
            }

            // InitializeStream: receive #3.1...
            try
            {
                sz = "";
                while ((sz == "") || (sz == " "))
                {
                    sz = m_ssltcpclient.Read(1000);
                    if (sz == null)
                    {
                        Log.Error("receive timed out...");
                        return (false);
                    }
                    Log.Info("RECV: " + sz);
                }
            }
            catch (Exception exception)
            {
                Log.Error("receive failed..." + exception.Message);
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Susbscribe for notifications...
        /// </summary>
        /// <returns>true on success</returns>
        private bool Subscribe()
        {
            string sz;

            // Log stuff...
            Log.Info("");
            Log.Info("Subscribe...");

            // Subscribe: send #1...
            try
            {
                string szData =
                    "<iq type=\"set\" to=\"" + m_szBareJid + "\" id=\"3\">" +
                    "<subscribe xmlns=\"subscriber-name:push\">" +
                    "<item channel=\"cloud_devices\" from=\"\"/>" +
                    "</subscribe>" +
                    "</iq>";
                Log.Info("SEND: " + szData);
                m_ssltcpclient.Write(szData);
            }
            catch (Exception exception)
            {
                Log.Error("send failed..." + exception.Message);
                return (false);
            }

            // Subscribe: receive #1.1...
            try
            {
                sz = "";
                while ((sz == "") || (sz == " "))
                {
                    sz = m_ssltcpclient.Read(Timeout.Infinite);
                    if (sz == null)
                    {
                        Log.Error("receive timed out...");
                        return (false);
                    }
                }
                Log.Info("RECV: " + sz);
            }
            catch (Exception exception)
            {
                Log.Error("receive failed..." + exception.Message);
                return (false);
            }

            // All done...
            return (true);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// Our security...
        /// </summary>
        private SslTcpClient m_ssltcpclient;

        /// <summary>
        /// Callback to make when an event shows up...
        /// </summary>
        private TimerCallback m_timercallbackEvent;

        /// <summary>
        /// Object that provided the callback event...
        /// </summary>
        private object m_objectEvent;

        /// <summary>
        /// Authorization entity (ex: someserviceaccount.com)...
        /// </summary>
        private string m_szAuthorizer;

        /// <summary>
        /// Id for submitting for notifications...
        /// </summary>
        private int m_iId;

        /// <summary>
        /// Full JID...
        /// </summary>
        private string m_szFullJid;

        /// <summary>
        /// Bare JID...
        /// </summary>
        private string m_szBareJid;

        #endregion
    }


    /// <summary>
    /// We need TLS...this takes care of that for us...
    /// </summary>
    public sealed class SslTcpClient : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Destructor...
        /// </summary>
        ~SslTcpClient()
        {
            Dispose(true);
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
        /// Cleanup...
        /// </summary>
        public void Close()
        {
            Dispose(true);
        }

        /// <summary>
        /// Begin reading data, when complete the ReadAsyncCallback is fired...
        /// </summary>
        public void BeginRead()
        {
            if (m_sslstream != null)
            {
                m_abData = new byte[65536];
                m_sslstream.BeginRead(m_abData, 0, m_abData.Length, new AsyncCallback(ReadXmppCallback), null);
            }
        }

        /// <summary>
        /// Open a connection...
        /// </summary>
        /// <param name="a_szAddress"></param>
        /// <param name="a_iPort"></param>
        /// <param name="m_timercallbackEvent">the event to fire</param>
        /// <returns></returns>
        public bool Open(string a_szAddress, int a_iPort, TimerCallback a_timercallbackEvent, object a_objectEvent)
        {
            // Init stuff...
            m_timercallbackEvent = a_timercallbackEvent;
            m_objectEvent = a_objectEvent;

            // Get our client...
            try
            {
                m_tcpclient = new TcpClient(a_szAddress, a_iPort);
            }
            catch (Exception exception)
            {
                Log.Error("TcpClient failed..." + exception.Message);
                return (false);
            }

            // Get our stream...
            m_networkstream = m_tcpclient.GetStream();

            // All done...
            return (true);
        }

        /// <summary>
        /// Read, this quietly handles both non-secure and secure transactions
        /// on the socket...
        /// </summary>
        /// <returns>whatever data was read</returns>
        public string Read(long a_lReadTimeout)
        {
            bool blSuccess;
            IAsyncResult iasynresultRead;
            
            // Unsecure read from the socket, we have to support this so that
            // we can request a secure connection...
            if (m_sslstream == null)
            {
                int iLen = 0;
                byte[] abData = new byte[65536];
                try
                {
                    m_networkstream.ReadTimeout = (int)a_lReadTimeout;
                    iasynresultRead = m_networkstream.BeginRead(abData, 0, abData.Length, new AsyncCallback(ReadRawCallback), null);
                    blSuccess = iasynresultRead.AsyncWaitHandle.WaitOne((int)a_lReadTimeout);
                    if (!blSuccess)
                    {
                        Log.Error("Read timed out...");
                        return (null);
                    }
                    iLen = m_networkstream.EndRead(iasynresultRead);
                    iasynresultRead = null;
                }
                catch (Exception exception)
                {
                    Log.Error("Read failed..." + exception.Message);
                    return (null);
                }
                if (iLen == 0)
                {
                    return ("");
                }
                return (Encoding.UTF8.GetString(abData, 0, iLen));
            }

            // Secure, most of the calls should come here...
            else
            {
                int iLen = 0;
                byte[] abData = new byte[65536];
                try
                {
                    m_sslstream.ReadTimeout = (int)a_lReadTimeout;
                    iasynresultRead = m_sslstream.BeginRead(abData, 0, abData.Length, new AsyncCallback(ReadRawCallback), null);
                    blSuccess = iasynresultRead.AsyncWaitHandle.WaitOne((int)a_lReadTimeout);
                    if (!blSuccess)
                    {
                        Log.Error("Read timed out...");
                        return (null);
                    }
                    iLen = m_sslstream.EndRead(iasynresultRead);
                    iasynresultRead = null;
                }
                catch (Exception exception)
                {
                    Log.Error("Read failed..." + exception.Message);
                    return (null);
                }
                if (iLen == 0)
                {
                    return ("");
                }
                return (Encoding.UTF8.GetString(abData, 0, iLen));
            }
        }

        /// <summary>
        /// Secure the connection...
        /// </summary>
        /// <param name="a_szServerName">the machine we're validating</param>
        /// <returns></returns>
        public bool Secure(string a_szServerName)
        {
            // Make sure we're clean...
            m_networkstream.Flush();

            // Create an SSL stream that will close the client's stream...
            try
            {
                m_sslstream = new SslStream
                (
                    m_networkstream,
                    true,
                    new RemoteCertificateValidationCallback(ValidateServerCertificate),
                    null
                );
            }
            catch (Exception exception)
            {
                Log.Error("Exception..." + exception.Message);
                Log.Error("SslStream failed, closing the connection...");
                Log.Error("Make sure your certificates are up-to-date...");
                m_tcpclient.Close();
                return (false);
            }

            // Certificates...
            m_x509cerification2collection = new X509Certificate2Collection();

            // The server name must match the name on the server certificate... 
            try
            {
                m_sslstream.AuthenticateAsClient(a_szServerName, m_x509cerification2collection, SslProtocols.Tls, false);
            }
            catch (AuthenticationException authenticationexception)
            {
                Log.Error("Exception..." + authenticationexception.Message);
                if (authenticationexception.InnerException != null)
                {
                    Log.Error("Exception..." + authenticationexception.InnerException.Message);
                }
                Log.Error("Authentication failed, closing the connection...");
                m_tcpclient.Close();
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Write, this quietly handles both non-secure and secure transactions
        /// on the socket...
        /// </summary>
        /// <param name="a_szData">what we're writing</param>
        /// <returns>true on success</returns>
        public bool Write(string a_szData)
        {
            // Unsecure...
            if (m_sslstream == null)
            {
                try
                {
                    byte[] abData = Encoding.UTF8.GetBytes(a_szData);
                    m_networkstream.Write(abData, 0, abData.Length);
                }
                catch (Exception exception)
                {
                    Log.Error("Write failed..." + exception.Message);
                    return (false);
                }
                return (true);
            }

            // Secure...
            else
            {
                try
                {
                    byte[] abData = Encoding.UTF8.GetBytes(a_szData);
                    m_sslstream.Write(abData, 0, abData.Length);
                }
                catch (Exception exception)
                {
                    Log.Error("Write failed..." + exception.Message);
                    return (false);
                }
                return (true);
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Internal Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Internal Methods...

        /// <summary>
        /// Cleanup...
        /// </summary>
        internal void Dispose(bool a_blDispose)
        {
            // Dispose...
            if (a_blDispose)
            {
                // Lose the secure stream...
                if (m_sslstream != null)
                {
                    m_sslstream.Close();
                    m_sslstream = null;
                    // Closing the SSL Stream takes out these two beasties, so just
                    // null them out...
                    m_networkstream = null;
                    m_tcpclient = null;
                }

                // Lose the unsecure stream...
                if (m_networkstream != null)
                {
                    m_networkstream.Close();
                    m_networkstream = null;
                    // Closing the Network Stream takes out this one, so just
                    // null it out...
                    m_tcpclient = null;
                }

                // Lose the socket...
                if (m_tcpclient != null)
                {
                    m_tcpclient.Close();
                    m_tcpclient = null;
                }
            }
        }

        /// <summary>
        /// Handle the arrival of raw (unprocessed) data...
        /// </summary>
        /// <param name="ar">incoming data</param>
        internal void ReadRawCallback(IAsyncResult ar)
        {
            // Just return...
        }

        /// <summary>
        /// Handle the arrival of asynchronous XMPP data...
        /// </summary>
        /// <param name="ar">incoming data</param>
        internal void ReadXmppCallback(IAsyncResult ar)
        {
            int iLen;
            string szData;
            string szXmpp;
            byte[] abData;

            // Complete the read, if we throw an exception it's most likely
            // because we've been closed.  Closing the XMPP connection does
            // not kill off any asynchronous read requests.  Instead, the
            // little buggers linger around until the next packet comes in,
            // and THEN they fire.  Nice design.  Yike.
            //
            // So, this try/catch is going to detect that the m_sslstream
            // has been disposed of, and it'll hork, and we'll bail, and
            // this seems to work...
            try
            {
                iLen = m_sslstream.EndRead(ar);
            }
            catch
            {
                return;
            }

            // We have data...
            if ((iLen > 0) && (m_abData[0] != '\0'))
            {
                try
                {
                    // Convert what we got...
                    szXmpp = Encoding.UTF8.GetString(m_abData, 0, iLen);
                    if (szXmpp.StartsWith("<message"))
                    {
                        // We need more data...
                        while (!szXmpp.Contains("</message>"))
                        {
                            iLen = m_sslstream.Read(m_abData, 0, 65536);
                            if (iLen > 0)
                            {
                                szXmpp += Encoding.UTF8.GetString(m_abData, 0, iLen);
                            }
                        }

                        // Log what we got...
                        Log.Info("");
                        Log.Info("XMPP: " + szXmpp);

                        // Extract the <push:data>...
                        szData = szXmpp.Remove(0, szXmpp.IndexOf("<push:data>") + 11);
                        szData = szData.Remove(szData.IndexOf("</push:data>"));

                        // Convert it from binhex...
                        abData = Convert.FromBase64String(szData);

                        // Get it back as a string...
                        szData = Encoding.UTF8.GetString(abData);
                        Log.Info("EVNT: " + szData);

                        // Do the callback...
                        m_timercallbackEvent(new XmppCallback(m_objectEvent, szData));
                    }
                }
                catch (Exception exception)
                {
                    Log.Error("Exception..." + exception.Message);
                }
            }

            // Fire up the next read...
            BeginRead();
        }

        /// <summary>
        /// The following method is invoked by the RemoteCertificateValidationDelegate...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        internal bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return (true);
            }

            Log.Error("Certificate error..." + sslPolicyErrors);

            // Do not allow this client to communicate with unauthenticated servers. 
            return (false);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// Callback when data arrives, the TimerCallback was used to make it easier
        /// to use the same function here and in a timer when polling...
        /// </summary>
        private TimerCallback m_timercallbackEvent;

        /// <summary>
        /// The object that supplied the callback function...
        /// </summary>
        private object m_objectEvent;

        /// <summary>
        /// Our secure stream (from the network stream)...
        /// </summary>
        private SslStream m_sslstream;

        /// <summary>
        /// Our network stream (from the socket)...
        /// </summary>
        private NetworkStream m_networkstream;

        /// <summary>
        /// Our socket...
        /// </summary>
        private TcpClient m_tcpclient;

        /// <summary>
        /// The list of certificates...
        /// </summary>
        private X509Certificate2Collection m_x509cerification2collection;

        /// <summary>
        /// A buffer for the pending read operation...
        /// </summary>
        private byte[] m_abData;

        #endregion
    }


    /// <summary>
    /// The payload object for XMPP callback events...
    /// </summary>
    public sealed class XmppCallback
    {
        /// <summary>
        /// Store the goodies away for delivery to the callback function...
        /// </summary>
        /// <param name="a_object">The object of the beastie that registered for XMPP events</param>
        /// <param name="a_szData">Data from the current event</param>
        public XmppCallback(object a_object, string a_szData)
        {
            m_object = a_object;
            m_szData = a_szData;
        }

        /// <summary>
        /// The object of the beastie that registered for XMPP events...
        /// </summary>
        public object m_object;

        /// <summary>
        /// Data from the current event...
        /// </summary>
        public string m_szData;
    }
}
