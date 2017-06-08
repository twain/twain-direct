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
using System.Diagnostics;
using System.IO;
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
        public Ipc(string a_szConnectionData, bool a_blInit)
        {
            // Init stuff...
            m_iPid = 0;
            m_connectiontype = ConnectionType.File;
            m_filestreamIn = null;
            m_filestreamOut = null;
            m_szPath = null;
            m_socketConnection = null;
            m_socketData = null;
            m_iPort = 0;
            m_szIpAddress = "(no ip)";
            m_iPort = 0;

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

            // File connection...
            else if (a_szConnectionData.StartsWith("file|"))
            {
                m_connectiontype = ConnectionType.File;
                m_szPath = a_szConnectionData.Split(new char[] { '|' })[1];

                // Initialize...
                if (a_blInit)
                {
                    // Make sure we can get to this folder...
                    if (!Directory.Exists(m_szPath))
                    {
                        try
                        {
                            Directory.CreateDirectory(m_szPath);
                        }
                        catch
                        {
                            Log.Error("Unable to create folder: " + m_szPath);
                            return;
                        }
                    }

                    // Cleanup...
                    if (File.Exists(Path.Combine(m_szPath, "filein")))
                    {
                        File.Delete(Path.Combine(m_szPath, "filein"));
                    }
                    if (File.Exists(Path.Combine(m_szPath, "fileout")))
                    {
                        File.Delete(Path.Combine(m_szPath, "fileout"));
                    }
                }
            }

            // Problem...
            else
            {
                Log.Error("Unrecognized connection data...");
                return;
            }

            // Allocate a buffer...
            m_abData = new byte[0x100000];
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
            string szFileIn;
            string szFileOut;

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

                // Success...
                return (true);
            }

            #endregion

            // File...
            #region File...

            else if (m_connectiontype == ConnectionType.File)
            {
                // Build the filenames...
                szFileIn = Path.Combine(m_szPath, "fileout");
                szFileOut = Path.Combine(m_szPath, "filein");

                // Wait for both files to exist...
                while (!File.Exists(szFileIn) || !File.Exists(szFileOut))
                {
                    Thread.Sleep(100);
                }

                // Open the files...
                try
                {
                    m_filestreamIn = new FileStream(szFileIn, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                    m_filestreamOut = new FileStream(szFileOut, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                }
                catch
                {
                    return (false);
                }

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
            if (m_filestreamIn != null)
            {
                m_filestreamIn.Close();
            }
            if (m_filestreamOut != null)
            {
                m_filestreamOut.Close();
            }
            if (m_socketConnection != null)
            {
                m_socketConnection.Close();
            }
            if (m_socketData != null)
            {
                m_socketData.Close();
            }
        }

        /// <summary>
        /// Connect to an IPC...
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            string szFileIn;
            string szFileOut;

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

                // Success...
                return (true);
            }

            #endregion

            // File...
            #region File...

            else if (m_connectiontype == ConnectionType.File)
            {
                // Make sure we can get to this folder...
                if (!Directory.Exists(m_szPath))
                {
                    try
                    {
                        Directory.CreateDirectory(m_szPath);
                    }
                    catch
                    {
                        Log.Error("Unable to create folder: " + m_szPath);
                        return (false);
                    }
                }

                // Build the filenames...
                szFileIn = Path.Combine(m_szPath, "filein");
                szFileOut = Path.Combine(m_szPath, "fileout");

                // Open the files...
                try
                {
                    m_filestreamIn = new FileStream(szFileIn, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                    m_filestreamOut = new FileStream(szFileOut, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                }
                catch
                {
                    return (false);
                }

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

            // File connection...
            else if (m_connectiontype == ConnectionType.File)
            {
                return ("file|" + m_szPath);
            }

            // Problem...
            else
            {
                Log.Error("Unrecognized connection type...");
                return (null);
            }
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
            // Socket...
            #region Socket...

            if (m_connectiontype == ConnectionType.Socket)
            {
                int iBytesTotal;

                // Read...
                try
                {
                    iBytesTotal = m_socketData.Receive(m_abData);
                }
                catch (Exception exception)
                {
                    Log.Error("ipcread> exception: " + exception.Message);
                    return (null);
                }

                // Log it...
                string sz = Encoding.UTF8.GetString(m_abData, 0, iBytesTotal);
                Log.Info("ipcread> " + sz);

                // All done...
                return (sz);
            }

            #endregion


            // File...
            #region File...

            else if (m_connectiontype == ConnectionType.File)
            {
                int iBytesRead;
                int iBytesTotal;

                // Init stuff...
                iBytesTotal = 0;
                m_filestreamOut.Position = 0;

                // Loop until we get all our data...
                while (true)
                {
                    // Read from the file...
                    try
                    {
                        iBytesRead = m_filestreamIn.Read(m_abData, iBytesTotal, m_abData.Length);
                    }
                    catch
                    {
                        iBytesRead = 0;
                    }

                    // No data on this read...
                    if (iBytesRead == 0)
                    {
                        // We have data from a previous call, so return with that...
                        if (iBytesTotal > 0)
                        {
                            break;
                        }

                        // Check to make sure our process is still running...
                        if (m_iPid != 0)
                        {
                            try
                            {
                                if (Process.GetProcessById(m_iPid) == null)
                                {
                                    Log.Error("Our process has died...");
                                    return (null);
                                }
                            }
                            catch
                            {
                                Log.Error("Our process has died...");
                                return (null);
                            }
                        }

                        // Sleep a bit and loop up to try again...
                        Thread.Sleep(100);
                        continue;
                    }

                    // Record the total bytes read so far...
                    iBytesTotal += iBytesRead;
                }

                // Clear the file...
                m_filestreamIn.SetLength(0);
                m_filestreamIn.Flush();

                // Log it...
                string sz = Encoding.UTF8.GetString(m_abData, 0, iBytesTotal);
                Log.Info("ipcread> " + sz);

                // All done...
                return (sz);
            }

            #endregion


            // Problem...
            else
            {
                Log.Error("Unrecognized connection type...");
                return (null);
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


            // File...
            #region File...
            else if (m_connectiontype == ConnectionType.File)
            {
                // Log it...
                Log.Info("ipcwrite> " + a_szData);

                // Convert the data...
                byte[] abData = Encoding.UTF8.GetBytes(a_szData);

                // Wait for the file to be cleared...
                while (m_filestreamOut.Length > 0)
                {
                    // Check to make sure our process is still running...
                    if (m_iPid != 0)
                    {
                        try
                        {
                            if (Process.GetProcessById(m_iPid) == null)
                            {
                                Log.Error("Our process has died...");
                                return (false);
                            }
                        }
                        catch
                        {
                            Log.Error("Our process has died...");
                            return (false);
                        }
                    }

                    // Wait a bit and try again...
                    Thread.Sleep(100);
                }

                // Write the data...
                m_filestreamOut.Position = 0;
                m_filestreamOut.Write(abData, 0, abData.Length);
                m_filestreamOut.Flush();

                // All done...
                return (true);
            }

            #endregion


            // Problem...
            else
            {
                Log.Error("Unrecognized connection type...");
                return (false);
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
            File,
            Socket
        }

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
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        internal void Dispose(bool a_blDisposing)
        {
            // Free managed resources...
            if (a_blDisposing)
            {
                if (m_filestreamIn != null)
                {
                    m_filestreamIn.Dispose();
                    m_filestreamIn = null;
                }
                if (m_filestreamOut != null)
                {
                    m_filestreamOut.Dispose();
                    m_filestreamOut = null;
                }
                if (m_socketConnection != null)
                {
                    m_socketConnection.Close();
                    m_socketConnection = null;
                }
                if (m_socketData != null)
                {
                    m_socketData.Close();
                    m_socketData = null;
                }
            }
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

        // The path to the folder we'll use for file IPC stuff...
        private string m_szPath;

        // The file providing incoming data for the entity that creates the
        // IPC.  This is the output for the other side...
        private FileStream m_filestreamIn;

        // The file providing outgoing data for the entity that creates the
        // IPC.  This is the input for the other side...
        private FileStream m_filestreamOut;

        // The socket we use to establish a connection for the data socket...
        private Socket m_socketConnection;

        // The socket we use for IPC communication...
        private Socket m_socketData;

        // The IP address we're using, if a socket...
        private string m_szIpAddress;

        // The port number we're using, if a socket...
        private int m_iPort;

        // The process id we need to monitor, so that we'll know if the
        // entity we're talking to goes ta-ta on us...
        private int m_iPid;

        // Our I/O buffer...
        private byte[] m_abData;

        #endregion
    }
}
