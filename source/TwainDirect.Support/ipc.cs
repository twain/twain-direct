///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.Support.Ipc
//
// This provides a way for us to communicate between processes, the case we're
// targeting is the TwainDirect.Scanner talking to TwainDirect.OnTwain.  The class
// is not concerned with content, it just shuttles payloads around.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    13-Dec-2014     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2014-2017 Kodak Alaris Inc.
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
using System.Net;
using System.Net.Sockets;
using System.Security.Permissions;
using System.Text;
using System.Threading;

namespace TwainDirect.Support
{
    /// <summary>
    /// Interprocess communication.  This version has two kinds: a simple paired
    /// file scheme, and sockets.  More can be added, if needed, including calls
    /// that are suited for a specific OS.
    /// </summary>
    public sealed class Ipc : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Initialize an IPC object...
        /// </summary>
        /// <param name="a_szConnectionData">the data for the connection</param>
        /// <param name="a_blInit">initialize</param>
        public Ipc
        (
            string a_szConnectionData,
            bool a_blInit,
            DisconnectCallbackDelegate a_disconnectcallbackdelegate,
            object a_objectDisconnectCallbackDelegate
        )
        {
            // Init stuff...
            m_iPid = 0;
            m_connectiontype = ConnectionType.Socket;
            m_socketConnection = null;
            m_socketData = null;
            m_iPort = 0;
            m_szIpAddress = "(no ip)";
            m_iPort = 0;
            m_lszCommands = new List<string>();
            m_objectCommands = new object();
            m_autoresetEventCommands = new AutoResetEvent(false);
            m_threadCommands = new Thread(CommandsThread);
            m_blCancelCommands = false;
            m_disconnectcallbackdelegate = a_disconnectcallbackdelegate;
            m_objectDisconnectCallbackDelegate = a_objectDisconnectCallbackDelegate;

            // Socket connection...
            if (a_szConnectionData.StartsWith("socket|"))
            {
                m_connectiontype = ConnectionType.Socket;
                try
                {
                    // We expect the caller will normally hit us either with a local loopback
                    // address with a 0 port, or with specific information that was the result
                    // of a previous call to Ipc...
                    m_szIpAddress = a_szConnectionData.Split(new char[] { '|' })[1];
                    m_iPort = int.Parse(a_szConnectionData.Split(new char[] { '|' })[2]);
                    m_socketConnection = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    if (a_blInit)
                    {
                        // Try to bind with these values...
                        bool blZeroFailed = false;
                        IPAddress ipaddress = IPAddress.Parse(m_szIpAddress);
                        IPEndPoint ipendpoint = new IPEndPoint(ipaddress, m_iPort);
                        try
                        {
                            m_socketConnection.Bind(ipendpoint);
                        }
                        catch (Exception exception)
                        {
                            if (m_iPort == 0)
                            {
                                blZeroFailed = true;
                                Log.Info("Socket exception (recoverable): " + exception.Message + " " + m_szIpAddress + " " + m_iPort);
                            }
                            else
                            {
                                Log.Error("Socket exception: " + exception.Message + " " + m_szIpAddress + " " + m_iPort);
                            }
                        }

                        // If the user was asking for port 0, then we have a chance to try
                        // this again, this time with the first IP address we find...
                        if (blZeroFailed)
                        {
                            // Try to get our IP address...
                            IPHostEntry iphostentry = Dns.GetHostEntry(Dns.GetHostName());
                            foreach (IPAddress ipaddressFind in iphostentry.AddressList)
                            {
                                if (ipaddressFind.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    m_szIpAddress = ipaddressFind.ToString();
                                }
                            }
                            m_iPort = 0;

                            // Let's try this again...
                            ipaddress = IPAddress.Parse(m_szIpAddress);
                            ipendpoint = new IPEndPoint(ipaddress, m_iPort);
                            m_socketConnection.Bind(ipendpoint);
                        }

                        // If the incoming port was zero, then get the real port now...
                        if (m_iPort == 0)
                        {
                            m_iPort = ((IPEndPoint)m_socketConnection.LocalEndPoint).Port;
                        }
                        m_socketConnection.Listen(1);
                    }
                }
                catch (Exception exception)
                {
                    Log.Error("Socket exception: " + exception.Message + " " + m_szIpAddress + " " + m_iPort);
                }
            }

            // Problem...
            else
            {
                Log.Error("Unrecognized connection data...");
                return;
            }
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        [EnvironmentPermissionAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Accept an IPC connection...
        /// </summary>
        /// <returns></returns>
        public bool Accept()
        {
            // Socket...
            #region Socket...

            if (m_connectiontype == ConnectionType.Socket)
            {
                // Wait for a connection...
                try
                {
                    m_socketData = m_socketConnection.Accept();
                }
                catch
                {
                    Log.Error("Accept failed...");
                    return (false);
                }

                // Launch our reading thread...
                m_threadCommands.Start();

                // Success...
                return (true);
            }

            #endregion

            // Trouble...
            Log.Error("Unrecognized connection type: " + m_connectiontype);
            return (false);
        }

        /// <summary>
        /// Close the IPC...
        /// </summary>
        public void Close()
        {
            Dispose(true);
        }

        /// <summary>
        /// Connect to an IPC...
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            // Socket...
            #region Socket...

            if (m_connectiontype == ConnectionType.Socket)
            {
                Log.Info("Ipc.Connect: " + m_szIpAddress + ":" + m_iPort);

                // Connect...
                try
                {
                    m_socketConnection.Connect(new IPEndPoint(IPAddress.Parse(m_szIpAddress), m_iPort));
                    m_socketData = m_socketConnection;
                }
                catch (Exception exception)
                {
                    Log.Error("Connect exception: " + exception.Message);
                    return (false);
                }

                // Launch our reading thread...
                m_threadCommands.Start();

                // Success...
                return (true);
            }

            #endregion

            // Trouble...
            Log.Error("Unrecognized connection type: " + m_connectiontype);
            return (false);
        }

        /// <summary>
        /// Return the connection data for this object...
        /// </summary>
        /// <returns></returns>
        public string GetConnectionInfo()
        {
            // Socket connection...
            if (m_connectiontype == ConnectionType.Socket)
            {
                return ("socket|" + m_szIpAddress + "|" + m_iPort);
            }

            // Problem...
            Log.Error("Unrecognized connection type...");
            return (null);
        }

        /// <summary>
        /// Process id to monitor...
        /// </summary>
        /// <param name="a_u64Pid"></param>
        /// <returns></returns>
        public bool MonitorPid(int a_iPid)
        {
            m_iPid = a_iPid;
            return (true);
        }

        /// <summary>
        /// Read from the IPC...
        /// </summary>
        /// <returns>the data</returns>
        [EnvironmentPermissionAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        public string Read()
        {
            // If we've been cancelled, don't do any waiting...
            if (m_blCancelCommands)
            {
                return (null);
            }

            // Wait for something to do...
            m_autoresetEventCommands.WaitOne();
            m_autoresetEventCommands.Reset();

            // Ruh-ruh, we need to scoot...
            if (m_blCancelCommands)
            {
                return (null);
            }

            // Get the datum...
            lock (m_objectCommands)
            {
                // This shouldn't happen, but paranoia is a good thing...
                if (m_lszCommands.Count == 0)
                {
                    return (null);
                }

                // Grab the next item, and pop it off the list...
                string sz = m_lszCommands[0];
                m_lszCommands.RemoveAt(0);

                // Return it...
                return (sz);
            }
        }

        /// <summary>
        /// Write to the IPC...
        /// </summary>
        /// <param name="a_szData"></param>
        /// <returns></returns>
        [EnvironmentPermissionAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        public bool Write(string a_szData)
        {
            // Socket...
            #region Socket...

            if (m_connectiontype == ConnectionType.Socket)
            {
                // Log it...
                Log.Info("ipcwrite> " + a_szData);

                // Read...
                byte[] abData = Encoding.UTF8.GetBytes(a_szData);
                try
                {
                    m_socketData.Send(abData);
                }
                catch (Exception exception)
                {
                    Log.Error("ipcwrite> exception: " + exception.Message);
                    return (false);
                }

                // All done...
                return (true);
            }

            #endregion


            // Problem...
            Log.Error("Unrecognized connection type...");
            return (false);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions...

        /// <summary>
        /// A callback to invoke if we're unexpectedly disconnected...
        /// </summary>
        /// <param name="a_objectContext">a context for the callback</param>
        public delegate void DisconnectCallbackDelegate(object a_objectContext);

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Destructor...
        /// </summary>
        ~Ipc()
        {
            Dispose(false);
        }

        /// <summary>
        /// Cancel the commands thread...
        /// </summary>
        internal void CancelCommandsThread()
        {
            m_blCancelCommands = true;
            m_autoresetEventCommands.Set();
        }

        /// <summary>
        /// This is where we read from the socket.  We update an array of commands,
        /// and this gives us some flexibility to act on some commands quicker than
        /// others...
        /// </summary>
        internal void CommandsThread()
        {
            // Allocate a buffer...
            byte[] abData = new byte[0x80000];

            // Loopy until told to scoot, or until we lose the connection...
            while (true)
            {
                // Socket...
                #region Socket...

                if (m_connectiontype == ConnectionType.Socket)
                {
                    int iBytesTotal;

                    // Scoot...
                    if (m_blCancelCommands)
                    {
                        return;
                    }

                    // Read...
                    try
                    {
                        iBytesTotal = m_socketData.Receive(abData);
                    }
                    catch (Exception exception)
                    {
                        Log.Error("ipcread> exception: " + exception.Message);
                        m_blCancelCommands = true;
                        lock (m_objectCommands)
                        {
                            m_lszCommands.RemoveRange(0, m_lszCommands.Count);
                        }
                        m_autoresetEventCommands.Set();
                        if (m_disconnectcallbackdelegate != null)
                        {
                            m_disconnectcallbackdelegate(m_objectDisconnectCallbackDelegate);
                        }
                        return;
                    }

                    // Scoot...
                    if (m_blCancelCommands)
                    {
                        return;
                    }

                    // Log it...
                    string sz = Encoding.UTF8.GetString(abData, 0, iBytesTotal);
                    Log.Info("ipcread> " + sz);

                    // Add this to our list, notify the read command...
                    lock (m_objectCommands)
                    {
                        m_lszCommands.Add(sz);
                    }

                    // Notify anybody watching that we have data...
                    m_autoresetEventCommands.Set();
                }

                #endregion


                // Problem...
                else
                {
                    Log.Error("Unrecognized connection type...");
                    m_blCancelCommands = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        internal void Dispose(bool a_blDisposing)
        {
            // Shut the thread down...
            if (m_threadCommands != null)
            {
                CancelCommandsThread();
                m_threadCommands = null;
            }

            // No more new connections...
            if (m_socketConnection != null)
            {
                try
                {
                    m_socketConnection.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                    // We just want to catch it, we don't care about what happened...
                }
                m_socketConnection.Close();
                m_socketConnection = null;
            }

            // No more data...
            if (m_socketData != null)
            {
                try
                {
                    m_socketData.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                    // We just want to catch it, we don't care about what happened...
                }
                m_socketData.Close();
                m_socketData = null;
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Definitions...

        /// <summary>
        /// Ways to chat.  We prefer the socket style.  The file style was created
        /// first, because it was dirt simple, and I just never bothered to remove
        /// it, because you never know when you need a backup plan...
        /// </summary>
        private enum ConnectionType
        {
            Socket
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes

        /// <summary>
        /// The connection type: socket, file, that sort of thing...
        /// </summary>
        private ConnectionType m_connectiontype;

        /// <summary>
        /// The socket we use to establish a connection for the data socket...
        /// </summary>
        private Socket m_socketConnection;

        /// <summary>
        /// The socket we use for IPC communication...
        /// </summary>
        private Socket m_socketData;

        /// <summary>
        /// The IP address we're using, if a socket...
        /// </summary>
        private string m_szIpAddress;

        /// <summary>
        /// The port number we're using, if a socket...
        /// </summary>
        private int m_iPort;

        /// <summary>
        /// The process id we need to monitor, so that we'll know if the
        /// entity we're talking to goes ta-ta on us...
        /// </summary>
        private int m_iPid;

        /// <summary>
        /// The incoming commands, with a lock object, a notification
        /// event, and a thread...
        /// </summary>
        private List<string> m_lszCommands;
        private object m_objectCommands;
        private AutoResetEvent m_autoresetEventCommands;
        private Thread m_threadCommands;
        private bool m_blCancelCommands;
        private DisconnectCallbackDelegate m_disconnectcallbackdelegate;
        private object m_objectDisconnectCallbackDelegate;

        #endregion
    }
}
