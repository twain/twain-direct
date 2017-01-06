///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirectSupport.Dnssd
//
// Not going to be subtle about this one, this is a hack.  The problem we have is
// the lack of well maintained, well designed C# API's for interfacing to Bonjour
// and Avahi.  Mono.Zeroconf was the best candidate, but it's been abandoned.
// It's a complex problem, and not one lightly undertaken.  So we're going to cheat.
//
// When confronted by a situation like this the rule is to design a good interface
// to abstract the problem into a corner, and then do whatever is needed to solve it.
//
// In this case we're going to appeal to the diagnostic apps that come with
// Bonjour and Avahi and run those to monitor for devices.  On the plus side it
// won't take a lot of work, and it'll do the job.  On the downside...it feels a
// little icky, but that's probably because I'm more used to APIs than scripting.
//
// As for the mDNS / DNS-SD advertising, we're complying with the TWAIN Local
// Specification:  http://twaindirect.org
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    01-Jul-2016     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2016-2016 Kodak Alaris Inc.
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
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;

// Namespace for things shared across the system...
namespace TwainDirectSupport
{
    /// <summary>
    /// The interface for zeroconf stuff...
    /// </summary>
    public sealed class Dnssd : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Initialize stuff...
        /// </summary>
        /// <param name="a_reason">the reason were using the class</param>
        public Dnssd(Reason a_reason)
        {
            m_reason = a_reason;
            m_objectLockCache = new Object();
            m_processDnssdRegisterPrivetTcpLocal = null;
            m_processDnssdRegisterTwainDirectSubPrivetTcpLocal = null;
        }

        /// <summary>
        /// Destructor...
        /// </summary>
        ~Dnssd()
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
        /// Get a snapshot of the connected devices...
        /// </summary>
        /// <param name="a_adnssddeviceinfoCompare">array to compare against</param>
        /// <param name="a_blUpdated">true if updated</param>
        /// <returns>a list of devices or null</returns>
        public DnssdDeviceInfo[] GetSnapshot(DnssdDeviceInfo[] a_adnssddeviceinfoCompare, out bool a_blUpdated)
        {
            long ii;
            DnssdDeviceInfo[] dnssddeviceinfoCache = null;

            // Init stuff...
            a_blUpdated = false;

            // Validate...
            if (m_reason != Reason.Monitor)
            {
                Log.Error("This function can't be used at this time...");
                return (null);
            }

            // Lock us...
            lock (m_objectLockCache)
            {
                // Get the snapshot...
                if (m_adnssddeviceinfoCache != null)
                {
                    dnssddeviceinfoCache = new DnssdDeviceInfo[m_adnssddeviceinfoCache.Length];
                    m_adnssddeviceinfoCache.CopyTo(dnssddeviceinfoCache, 0);
                }
            }

            // Compare the snapshots...
            if ((a_adnssddeviceinfoCompare == null) && (dnssddeviceinfoCache == null))
            {
                a_blUpdated = false;
            }
            else if ((a_adnssddeviceinfoCompare == null) && (dnssddeviceinfoCache != null))
            {
                a_blUpdated = true;
            }
            else if ((a_adnssddeviceinfoCompare != null) && (dnssddeviceinfoCache == null))
            {
                a_blUpdated = true;
            }
            else if (a_adnssddeviceinfoCompare.Length != dnssddeviceinfoCache.Length)
            {
                a_blUpdated = true;
            }
            else
            {
                // They've been sorted, so they must exactly match, row for row...
                for (ii = 0; ii < a_adnssddeviceinfoCompare.Length; ii++)
                {
                    if (   (a_adnssddeviceinfoCompare[ii].lInterface != dnssddeviceinfoCache[ii].lInterface)
                        || (a_adnssddeviceinfoCompare[ii].szTxtTy != dnssddeviceinfoCache[ii].szTxtTy)
                        || (a_adnssddeviceinfoCompare[ii].szServiceName != dnssddeviceinfoCache[ii].szServiceName)
                        || (a_adnssddeviceinfoCompare[ii].szLinkLocal != dnssddeviceinfoCache[ii].szLinkLocal)
                        || (a_adnssddeviceinfoCompare[ii].lPort != dnssddeviceinfoCache[ii].lPort)
                        || (a_adnssddeviceinfoCompare[ii].szIpv4 != dnssddeviceinfoCache[ii].szIpv4)
                        || (a_adnssddeviceinfoCompare[ii].szIpv6 != dnssddeviceinfoCache[ii].szIpv6))
                    {
                        a_blUpdated = true;
                        break;
                    }
                }
            }

            // All done...
            return (dnssddeviceinfoCache);
        }

        /// <summary>
        /// Start the monitoring system...
        /// </summary>
        /// <returns>true on success</returns>
        public bool MonitorStart()
        {
            // Validate...
            if (m_reason != Reason.Monitor)
            {
                Log.Error("This function can't be used at this time...");
                return (false);
            }
            if (m_threadDnsdMonitor != null)
            {
                Log.Error("Monitor has already been started...");
                return (true);
            }

            // Init our program and the arguments needed to see when devices
            // come and go on the LAN...
            switch (TwainLocalScanner.GetPlatform())
            {
                default:
                    Log.Error("GetPlatform failed...");
                    return (false);

                case TwainLocalScanner.Platform.WINDOWS:
                    // Check that we can do this...
                    m_szDnssdPath = "C:\\Windows\\System32\\dns-sd.exe";
                    m_szDnssdArguments = "-B _privet._tcp";
                    if (!File.Exists(m_szDnssdPath))
                    {
                        Log.Error("dns-sd.exe not found...");
                        return (false);
                    }

                    // Start the thread...
                    m_threadDnsdMonitor = new Thread(DnssdMonitorWindows);
                    m_threadDnsdMonitor.Start();
                    break;

                case TwainLocalScanner.Platform.LINUX:
                    // Check that we can do this...
                    m_szDnssdPath = "/usr/bin/avahi-browse";
                    m_szDnssdArguments = "-vprk _privet._tcp";
                    if (!File.Exists(m_szDnssdPath))
                    {
                        m_szDnssdPath = "/usr/local/bin/avahi-browse";
                        if (!File.Exists(m_szDnssdPath))
                        {
                            Log.Error("avahi-browse not found...");
                            return (false);
                        }
                        Log.Error("/usr/bin/avahi-browse not found...");
                        return (false);
                    }

                    // Start the thread...
                    m_threadDnsdMonitor = new Thread(DnssdMonitorLinux);
                    m_threadDnsdMonitor.Start();
                    break;

                case TwainLocalScanner.Platform.MACOSX:
                    // Check that we can do this...
                    m_szDnssdPath = "/usr/bin/dns-sd";
                    m_szDnssdArguments = "-Z _privet._tcp .";
                    if (!File.Exists(m_szDnssdPath))
                    {
                        Log.Error("dns-sd not found...");
                        return (false);
                    }

                    // Start the thread...
                    m_threadDnsdMonitor = new Thread(DnssdMonitorMacOsX);
                    m_threadDnsdMonitor.Start();
                    break;
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Stop the monitoring system...
        /// </summary>
        public void MonitorStop()
        {
            // Validate...
            if (m_reason != Reason.Monitor)
            {
                Log.Error("This function can't be used at this time...");
                return;
            }

            // We'll use Dispose...
            Dispose(true);

            // All done...
            return;
        }

        /// <summary>
        /// Register the device on DNS-SD, and keep it there until killed...
        /// </summary>
        /// <param name="a_szInstanceName">the instance name: xxx._twaindirect._sub._privet._tcp.local</param>
        /// <param name="a_iPort">socket port number</param>
        /// <param name="a_szTy">the friendly name for the device, not forced to be unique</param>
        /// <param name="a_szNote">a helpful note about the device (optional)</param>
        /// <returns>true on success</returns>
        public bool RegisterStart
        (
            string a_szInstanceName,
            int a_iPort,
            string a_szTy,
            string a_szNote
        )
        {
            // Text...
            // txtvers=1
            // ty=a_szTy
            // type=twaindirect
            // id=
            // cs=offline
            // note=a_szNote

            // Validate...
            if (m_reason != Reason.Register)
            {
                Log.Error("This function can't be used at this time...");
                return (true);
            }
            if (    (m_threadDnsdRegisterPrivetTcpLocal != null)
                ||  (m_threadDnsdRegisterTwainDirectSubPrivetTcpLocal != null))
            {
                Log.Error("Register has already been started...");
                return (true);
            }

            // Init our program and the arguments needed to register a device
            // on the LAN...
            switch (TwainLocalScanner.GetPlatform())
            {
                default:
                    Log.Error("GetPlatform failed...");
                    return (false);

                case TwainLocalScanner.Platform.WINDOWS:
                    // Check that we can do this...
                    m_szDnssdPath = "C:\\Windows\\System32\\dns-sd.exe";
                    m_szDnssdArgumentsTwainDirectPrivetTcpLocal =
                        "-R " +
                        " " + "\"" + a_szInstanceName + "._twaindirect._sub\"" +
                        " " + "\"_privet._tcp\"" +
                        " " + "." +
                        " " + a_iPort +
                        " " + "\"txtvers=1\"" +
                        " " + "\"ty=" + a_szTy + "\"" +
                        " " + "\"type=twaindirect\"" +
                        " " + "\"id=\"" +
                        " " + "\"cs=offline\"" +
                        " " + "\"https=" + ((Config.Get("useHttps","no") == "yes") ? "1\"" : "0\"") +
                        ((a_szNote == null) ? "" : " " + "\"note=" + a_szNote + "\"");
                    if (!File.Exists(m_szDnssdPath))
                    {
                        Log.Error("dns-sd.exe not found...");
                        return (false);
                    }

                    // Start the thread for twaindirect.sub.privet.tcp.local...
                    m_threadDnsdRegisterTwainDirectSubPrivetTcpLocal = new Thread(DnssdRegisterWindowsTwainDirectSubPrivetTcpLocal);
                    m_threadDnsdRegisterTwainDirectSubPrivetTcpLocal.Start();

                    // Start the thread for privet.tcp.local...
                    m_szDnssdArgumentsPrivetTcpLocal = m_szDnssdArgumentsTwainDirectPrivetTcpLocal.Replace("._twaindirect._sub", "");
                    m_threadDnsdRegisterPrivetTcpLocal = new Thread(DnssdRegisterWindowsPrivetTcpLocal);
                    m_threadDnsdRegisterPrivetTcpLocal.Start();
                    break;

                case TwainLocalScanner.Platform.LINUX:
                    // Check that we can do this...
                    m_szDnssdPath = "/usr/bin/avahi-publish";
                    m_szDnssdArgumentsTwainDirectPrivetTcpLocal =
                        " " + "\"" + a_szInstanceName + "._twaindirect._sub\"" +
                        " " + "\"_privet._tcp\"" +
                        " " + "." +
                        " " + a_iPort +
                        " " + "\"txtvers=1\"" +
                        " " + "\"ty=" + a_szTy + "\"" +
                        " " + "\"type=twaindirect\"" +
                        " " + "\"id=\"" +
                        " " + "\"cs=offline\"" +
                        " " + "\"https=" + ((Config.Get("useHttps", "no") == "yes") ? "1\"" : "0\"") +
                        ((a_szNote == null) ? "" : " " + "\"note=" + a_szNote + "\"");
                    if (!File.Exists(m_szDnssdPath))
                    {
                        m_szDnssdPath = "/usr/local/bin/avahi-browse";
                        if (!File.Exists(m_szDnssdPath))
                        {
                            Log.Error("avahi-browse not found...");
                            return (false);
                        }
                        Log.Error("/usr/bin/avahi-browse not found...");
                        return (false);
                    }

                    // Start the thread for twaindirect.sub.privet.tcp.local...
                    m_threadDnsdRegisterTwainDirectSubPrivetTcpLocal = new Thread(DnssdRegisterWindowsTwainDirectSubPrivetTcpLocal);
                    m_threadDnsdRegisterTwainDirectSubPrivetTcpLocal.Start();

                    // Start the thread for privet.tcp.local...
                    m_szDnssdArgumentsPrivetTcpLocal = m_szDnssdArgumentsTwainDirectPrivetTcpLocal.Replace("._twaindirect._sub", "");
                    m_threadDnsdRegisterPrivetTcpLocal = new Thread(DnssdRegisterWindowsPrivetTcpLocal);
                    m_threadDnsdRegisterPrivetTcpLocal.Start();
                    break;

                case TwainLocalScanner.Platform.MACOSX:
                    // Check that we can do this...
                    m_szDnssdPath = "/usr/bin/dns-sd";
                    m_szDnssdArgumentsTwainDirectPrivetTcpLocal =
                        "-R " +
                        " " + "\"" + a_szInstanceName + "._twaindirect._sub\"" +
                        " " + "\"_privet._tcp\"" +
                        " " + "." +
                        " " + a_iPort +
                        " " + "\"txtvers=1\"" +
                        " " + "\"ty=" + a_szTy + "\"" +
                        " " + "\"type=twaindirect\"" +
                        " " + "\"id=\"" +
                        " " + "\"cs=offline\"" +
                        " " + "\"https=" + ((Config.Get("useHttps", "no") == "yes") ? "1\"" : "0\"") +
                        ((a_szNote == null) ? "" : " " + "\"note=" + a_szNote + "\"");
                    if (!File.Exists(m_szDnssdPath))
                    {
                        Log.Error("dns-sd not found...");
                        return (false);
                    }

                    // Start the thread for twaindirect.sub.privet.tcp.local...
                    m_threadDnsdRegisterTwainDirectSubPrivetTcpLocal = new Thread(DnssdRegisterWindowsTwainDirectSubPrivetTcpLocal);
                    m_threadDnsdRegisterTwainDirectSubPrivetTcpLocal.Start();

                    // Start the thread for privet.tcp.local...
                    m_szDnssdArgumentsPrivetTcpLocal = m_szDnssdArgumentsTwainDirectPrivetTcpLocal.Replace("._twaindirect._sub", "");
                    m_threadDnsdRegisterPrivetTcpLocal = new Thread(DnssdRegisterWindowsPrivetTcpLocal);
                    m_threadDnsdRegisterPrivetTcpLocal.Start();
                    break;
            }
 
            // All done...
            return (true);
        }

        /// <summary>
        /// Stop the registering system...
        /// </summary>
        public void RegisterStop()
        {
            // Validate...
            if (m_reason != Reason.Register)
            {
                Log.Error("This function can't be used at this time...");
                return;
            }

            // We'll use dispose...
            Dispose(true);

            // All done...
            return;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions...

        /// <summary>
        /// The reason we're using this class, either to register a
        /// device, or to monitor for them.  Technically, the class
        /// could do both at the same time, but for practical reasons
        /// it shouldn't be doing that so we're going to prevent
        /// accidents, and tag each function...
        /// </summary>
        public enum Reason
        {
            Monitor,
            Register
        }

        /// <summary>
        /// Data gleaned from zeroconf about a device, the fields are
        /// organized in the order we'd like to see them sorted, but
        /// the CompareTo function takes care of the actual comparisons...
        /// </summary>
        public class DnssdDeviceInfo : IComparable
        {
            /// <summary>
            /// Text ty, friendly name for the scanner...
            /// </summary>
            public string szTxtTy;

            /// <summary>
            /// The full, unique name of the device...
            /// </summary>
            public string szServiceName;

            /// <summary>
            /// The link local name for where the service is running...
            /// </summary>
            public string szLinkLocal;

            // The interface it lives on...
            public long lInterface;

            /// <summary>
            /// The IPv4 address (if any)...
            /// </summary>
            public string szIpv4;
            
            /// <summary>
            /// The IPv6 address (if any)...
            /// </summary>
            public string szIpv6;

            // The port to use...
            public long lPort;

            // The flags reported with it...
            public long lFlags;

            /// <summary>
            /// Text version...
            /// </summary>
            public string szTxtTxtvers;

            /// <summary>
            /// Text type, comma separated services supported by the device...
            /// </summary>
            public string szTxtType;

            /// <summary>
            /// Text id, TWAIN Cloud id, empty if one isn't available...
            /// </summary>
            public string szTxtId;

            /// <summary>
            /// Text cs, TWAIN Cloud status...
            /// </summary>
            public string szTxtCs;

            /// <summary>
            /// Text note, optional, a note about the device, like its location...
            /// </summary>
            public string szTxtNote;

            /// <summary>
            /// true if the scanner wants us to use HTTPS...
            /// </summary>
            public bool blTxtHttps;

            /// <summary>
            /// The TXT records...
            /// </summary>
            public string[] aszText;

            // Implement our IComparable interface...
            public int CompareTo(object obj)
            {
                if (obj is DnssdDeviceInfo)
                {
                    int iResult;
                    iResult = this.szTxtTy.CompareTo(((DnssdDeviceInfo)obj).szTxtTy);
                    if (iResult == 0)
                    {
                        iResult = this.szServiceName.CompareTo(((DnssdDeviceInfo)obj).szServiceName);
                    }
                    if (iResult == 0)
                    {
                        iResult = this.szLinkLocal.CompareTo(((DnssdDeviceInfo)obj).szLinkLocal);
                    }
                    if (iResult == 0)
                    {
                        iResult = this.lInterface.CompareTo(((DnssdDeviceInfo)obj).lInterface);
                    }
                    return (iResult);
                }
                return (0);
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
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        internal void Dispose(bool a_blDisposing)
        {
            // Free managed resources...
            if (a_blDisposing)
            {
                if (m_processDnssdMonitor != null)
                {
                    m_processDnssdMonitor.Kill();
                    m_processDnssdMonitor.Dispose();
                    m_processDnssdMonitor = null;
                }
                if (m_processDnssdRegisterPrivetTcpLocal != null)
                {
                    m_processDnssdRegisterPrivetTcpLocal.Kill();
                    m_processDnssdRegisterPrivetTcpLocal.Dispose();
                    m_processDnssdRegisterPrivetTcpLocal = null;
                }
                if (m_processDnssdRegisterTwainDirectSubPrivetTcpLocal != null)
                {
                    m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.Kill();
                    m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.Dispose();
                    m_processDnssdRegisterTwainDirectSubPrivetTcpLocal = null;
                }
                if (m_eventwaithandleRead != null)
                {
                    m_eventwaithandleRead.Close();
                    m_eventwaithandleRead = null;
                }
            }
        }

        /// <summary>
        /// Read data for the "dns-sd -L" command...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void procReadLookupData(object sender, DataReceivedEventArgs e)
        {
            // Filter for our interface number...
            if (e.Data.Replace("\n","").Replace("\r","").Contains(" " + m_dnssddeviceinfoTmp.lInterface + ")"))
            {
                int ii;
                string[] aszOutput = e.Data.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (aszOutput != null)
                {
                    for (ii = 0; ii < aszOutput.Length; ii++)
                    {
                        if (aszOutput[ii].Contains(".:"))
                        {
                            string[] aszTmp = aszOutput[ii].Split(new string[] { ".:" }, StringSplitOptions.None);
                            m_dnssddeviceinfoTmp.szLinkLocal = aszTmp[0];
                            long.TryParse(aszTmp[1], out m_dnssddeviceinfoTmp.lPort);
                            m_eventwaithandleRead.Set();
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Read data for the "dns-sd -Q A" command...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void procReadIpv4Data(object sender, DataReceivedEventArgs e)
        {
            long lInterface;
            string[] aszOutput = e.Data.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (aszOutput != null)
            {
                if ((aszOutput.Length < 8) || !long.TryParse(aszOutput[3],out lInterface))
                {
                    return;
                }
                if (lInterface == m_dnssddeviceinfoTmp.lInterface)
                {
                    m_dnssddeviceinfoTmp.szIpv4 = aszOutput[7];
                    if (m_dnssddeviceinfoTmp.szIpv4 == "No")
                    {
                        m_dnssddeviceinfoTmp.szIpv4 = null;
                    }
                    m_eventwaithandleRead.Set();
                    return;
                }
            }
        }

        /// <summary>
        /// Read data for the "dns-sd -Q AAAA" command...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void procReadIpv6Data(object sender, DataReceivedEventArgs e)
        {
            long lInterface;
            string[] aszOutput = e.Data.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (aszOutput != null)
            {
                if ((aszOutput.Length < 8) || !long.TryParse(aszOutput[3],out lInterface))
                {
                    return;
                }
                if (lInterface == m_dnssddeviceinfoTmp.lInterface)
                {
                    m_dnssddeviceinfoTmp.szIpv6 = aszOutput[7];
                    if (m_dnssddeviceinfoTmp.szIpv6 == "No")
                    {
                        m_dnssddeviceinfoTmp.szIpv6 = null;
                    }
                    m_eventwaithandleRead.Set();
                    return;
                }
            }
        }

        /// <summary>
        /// Read data for the "dns-sd -Q TXT" command...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void procReadTxtData(object sender, DataReceivedEventArgs e)
        {
            long ii;
            long lInterface;
            string[] aszOutput = e.Data.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (aszOutput != null)
            {
                if ((aszOutput.Length < 8) || !long.TryParse(aszOutput[3], out lInterface))
                {
                    return;
                }
                if (lInterface == m_dnssddeviceinfoTmp.lInterface)
                {
                    // Find the "bytes:" tag...
                    for (ii = 0; ii < aszOutput.Length; ii++)
                    {
                        if (aszOutput[ii].StartsWith("bytes:"))
                        {
                            break;
                        }
                    }
                    if (ii >= aszOutput.Length)
                    {
                        return;
                    }

                    // Process the TXT data, each hexit it a byte in a UTF-8
                    // string, and each string is of the form key=value, so
                    // we'll capture the data as a byte array, convert it to
                    // a string, and then parse out the contents of the key.
                    // The first hexit indicates the total number of bytes in
                    // the key=value string (it doesn't include the prefix
                    // byte).
                    long lPrefix;
                    byte[] abValue = new byte[aszOutput.Length + 1];
                    ii += 1;
                    while (ii < aszOutput.Length)
                    {
                        long bb;
                        long pp;
                        string szKey;
                        string szValue;

                        // Get the prefix byte...
                        if (!long.TryParse(aszOutput[ii], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out lPrefix))
                        {
                            return;
                        }

                        // Get the key, stop at the equal size (hex 3D)...
                        bb = 0;
                        pp = 0;
                        for (ii += 1; (pp < lPrefix) && (ii < aszOutput.Length) && (aszOutput[ii] != "3D"); ii++, pp++)
                        {
                            if (!byte.TryParse(aszOutput[ii], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out abValue[bb++]))
                            {
                                return;
                            }
                        }
                        szKey = Encoding.UTF8.GetString(abValue, 0, (int)bb);

                        // Get the value, stop at the end of the data...
                        bb = 0;
                        for (ii += 1, pp += 1; (pp < lPrefix) && (ii < aszOutput.Length); ii++, pp++)
                        {
                            if (!byte.TryParse(aszOutput[ii], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out abValue[bb++]))
                            {
                                return;
                            }
                        }
                        szValue = Encoding.UTF8.GetString(abValue, 0, (int)bb);

                        // Store the data...
                        if (m_dnssddeviceinfoTmp.aszText == null)
                        {
                            m_dnssddeviceinfoTmp.aszText = new string[1];
                            m_dnssddeviceinfoTmp.aszText[0] = szKey + "=" + szValue;
                        }
                        else
                        {
                            string[] asz = new string[m_dnssddeviceinfoTmp.aszText.Length + 1];
                            m_dnssddeviceinfoTmp.aszText.CopyTo(asz, 0);
                            asz[m_dnssddeviceinfoTmp.aszText.Length] = szKey + "=" + szValue;
                            m_dnssddeviceinfoTmp.aszText = asz;
                            asz = null;
                        }

                        // txtvers...
                        if (szKey == "txtvers")
                        {
                            m_dnssddeviceinfoTmp.szTxtTxtvers = szValue;
                        }

                        // ty...
                        else if (szKey == "ty")
                        {
                            m_dnssddeviceinfoTmp.szTxtTy = szValue;
                        }

                        // type...
                        else if (szKey == "type")
                        {
                            m_dnssddeviceinfoTmp.szTxtType = szValue;
                        }

                        // id...
                        else if (szKey == "id")
                        {
                            m_dnssddeviceinfoTmp.szTxtId = szValue;
                        }

                        // cs...
                        else if (szKey == "cs")
                        {
                            m_dnssddeviceinfoTmp.szTxtCs = szValue;
                        }

                        // https...
                        else if (szKey == "https")
                        {
                            m_dnssddeviceinfoTmp.blTxtHttps = (szValue != "0");
                        }

                        // note...
                        else if (szKey == "note")
                        {
                            m_dnssddeviceinfoTmp.szTxtNote = szValue;
                        }
                    }

                    m_eventwaithandleRead.Set();
                    return;
                }
            }
        }

        /// <summary>
        /// Handle the Windows dns-sd process output from this thread...
        /// </summary>
        internal void DnssdMonitorWindows()
        {
            string szOutput;
            Process process;

            // Start the child process.
            m_processDnssdMonitor = new Process();

            // Redirect the output stream of the child process.
            m_processDnssdMonitor.StartInfo.UseShellExecute = false;
            m_processDnssdMonitor.StartInfo.WorkingDirectory = Path.GetDirectoryName(m_szDnssdPath);
            m_processDnssdMonitor.StartInfo.CreateNoWindow = true;
            m_processDnssdMonitor.StartInfo.RedirectStandardOutput = true;
            m_processDnssdMonitor.StartInfo.RedirectStandardError = true;
            m_processDnssdMonitor.StartInfo.FileName = m_szDnssdPath;
            m_processDnssdMonitor.StartInfo.Arguments = m_szDnssdArguments;
            m_processDnssdMonitor.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            m_processDnssdMonitor.Start();

            // Read each line as it comes in from the process, when the
            // caller is done they'll kill it off and the read should
            // abort at that point...
            for (;;)
            {
                try
                {
                    // Read a line of data...
                    szOutput = m_processDnssdMonitor.StandardOutput.ReadLine();

                    // Something was added...
                    #region Something was added...
                    if (szOutput.Contains(" Add "))
                    {
                        int ii;
                        string szInstanceName;

                        // Give ourself a place to store what we find...
                        m_dnssddeviceinfoTmp = new DnssdDeviceInfo();


                        ///////////////////////////////////////////////////////////
                        // Parse the -B data (browsing)...
                        #region Parse -B

                        // We can get the flags and the interface by splitting, we
                        // need to remove empty entries because there could be a
                        // leading space...
                        string[] aszOutput = szOutput.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (aszOutput.Length < 4)
                        {
                            continue;
                        }

                        // The flags are at index 2...
                        if (!long.TryParse(aszOutput[2], out m_dnssddeviceinfoTmp.lFlags))
                        {
                            continue;
                        }

                        // The interface is at index 3...
                        if (!long.TryParse(aszOutput[3], out m_dnssddeviceinfoTmp.lInterface))
                        {
                            continue;
                        }

                        // Parse the instance name.  Unfortunately we can't just split it
                        // because the name may include spaces, so we'll sneak up on it.  Start
                        // by locating "_private._tcp"...
                        ii = szOutput.IndexOf("_privet._tcp");
                        if (ii <= 0)
                        {
                            continue;
                        }

                        // Skip over _privet._tcp, and skip all whitespace...
                        for (ii += "_privet._tcp.".Length; (ii < szOutput.Length); ii++)
                        {
                            if (!Char.IsWhiteSpace(szOutput[ii]))
                            {
                                break;
                            }
                        }
                        if (ii >= szOutput.Length)
                        {
                            continue;
                        }

                        // Everything from this index on is our instance name...
                        szInstanceName = szOutput.Substring(ii);

                        // Our instance name has to end with "._twaindirect._sub"
                        if (!szInstanceName.EndsWith("._twaindirect._sub"))
                        {
                            continue;
                        }

                        #endregion


                        ///////////////////////////////////////////////////////////
                        // Get the lookup data from dns-sd (link local and port)...
                        #region Parse -L

                        // Give us an event to know when we're done reading...
                        m_eventwaithandleRead = new EventWaitHandle(false, EventResetMode.ManualReset);

                        // Launch a process to resolve the link local name and the port number...
                        process = new Process();
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.WorkingDirectory = Path.GetDirectoryName(m_szDnssdPath);
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.FileName = m_szDnssdPath;
                        process.StartInfo.Arguments = "-L \"" + szInstanceName + "\" _privet._tcp";
                        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        process.OutputDataReceived += procReadLookupData;
                        process.ErrorDataReceived += procReadLookupData;
                        process.Start();

                        // Start reading the data, stop when we get it, or when we run
                        // out of time waiting for it...
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        // Wait for the data to arrive, the only way this should time
                        // out is if we lost our device between the time -B saw it and
                        // when we tried to get this data...
                        m_eventwaithandleRead.WaitOne(5000);

                        // End the process...
                        process.CancelOutputRead();
                        process.CancelErrorRead();
                        m_eventwaithandleRead = null;
                        process.Kill();

                        // If we didn't get a port number, we're done with this line...
                        if (m_dnssddeviceinfoTmp.lPort == 0)
                        {
                            continue;
                        }

                        // Squirrel away our full service name...
                        m_dnssddeviceinfoTmp.szServiceName = szInstanceName.Replace(".", "\\.") + "._privet._tcp.local";

                        // Seed the friendly name with the topmost part of the instance
                        // name, plus the topmost part of the link local, and the
                        // instance number, this should be unique...
                        m_dnssddeviceinfoTmp.szTxtTy = szInstanceName.Split(new char[] { '.' })[0] + "_" + m_dnssddeviceinfoTmp.szLinkLocal.Split(new char[] { '.' })[0] + "_" + m_dnssddeviceinfoTmp.lInterface;

                        #endregion


                        ///////////////////////////////////////////////////////////
                        // Get the -Q A record data (ipv4)...
                        #region Parse -Q A

                        // Give us an event to know when we're done reading...
                        m_eventwaithandleRead = new EventWaitHandle(false, EventResetMode.ManualReset);

                        // Launch a process to resolve the link local name and the port number...
                        process = new Process();
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.WorkingDirectory = Path.GetDirectoryName(m_szDnssdPath);
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.FileName = m_szDnssdPath;
                        process.StartInfo.Arguments = "-Q \"" + m_dnssddeviceinfoTmp.szLinkLocal + "\" A";
                        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        process.OutputDataReceived += procReadIpv4Data;
                        process.ErrorDataReceived += procReadIpv4Data;
                        process.Start();

                        // Start reading the data, stop when we get it, or when we run
                        // out of time waiting for it...
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        // Wait for the data to arrive, the only way this should time
                        // out is if we lost out device between the time -B saw it and
                        // when we tried to get this data...
                        m_eventwaithandleRead.WaitOne(5000);

                        // End the process...
                        process.CancelOutputRead();
                        process.CancelErrorRead();
                        m_eventwaithandleRead = null;
                        process.Kill();

                        #endregion


                        ///////////////////////////////////////////////////////////
                        // Get the -Q AAAA record data (ipv6)...
                        #region Parse -Q AAAA

                        // Give us an event to know when we're done reading...
                        m_eventwaithandleRead = new EventWaitHandle(false, EventResetMode.ManualReset);

                        // Launch a process to resolve the link local name and the port number...
                        process = new Process();
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.WorkingDirectory = Path.GetDirectoryName(m_szDnssdPath);
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.FileName = m_szDnssdPath;
                        process.StartInfo.Arguments = "-Q \"" + m_dnssddeviceinfoTmp.szLinkLocal + "\" AAAA";
                        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        process.OutputDataReceived += procReadIpv6Data;
                        process.ErrorDataReceived += procReadIpv6Data;
                        process.Start();

                        // Start reading the data, stop when we get it, or when we run
                        // out of time waiting for it...
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        // Wait for the data to arrive, the only way this should time
                        // out is if we lost out device between the time -B saw it and
                        // when we tried to get this data...
                        m_eventwaithandleRead.WaitOne(5000);

                        // End the process...
                        process.CancelOutputRead();
                        process.CancelErrorRead();
                        m_eventwaithandleRead = null;
                        process.Kill();

                        // We have to have either ipv4 or ipv6 data...
                        if (    ((m_dnssddeviceinfoTmp.szIpv4 == null) || (m_dnssddeviceinfoTmp.szIpv4.Length == 0))
                            &&  ((m_dnssddeviceinfoTmp.szIpv6 == null) || (m_dnssddeviceinfoTmp.szIpv6.Length == 0)))
                        {
                            continue;
                        }

                        #endregion


                        ///////////////////////////////////////////////////////////
                        // Get the -Q TXT record data...
                        #region Parse -Q TXT

                        // Give us an event to know when we're done reading...
                        m_eventwaithandleRead = new EventWaitHandle(false, EventResetMode.ManualReset);

                        // Launch a process to resolve the link local name and the port number...
                        process = new Process();
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.WorkingDirectory = Path.GetDirectoryName(m_szDnssdPath);
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.FileName = m_szDnssdPath;
                        process.StartInfo.Arguments = "-Q \"" + szInstanceName.Replace(".","\\.") + "._privet._tcp.local\" TXT";
                        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        process.OutputDataReceived += procReadTxtData;
                        process.ErrorDataReceived += procReadTxtData;
                        process.Start();

                        // Start reading the data, stop when we get it, or when we run
                        // out of time waiting for it...
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        // Wait for the data to arrive, the only way this should time
                        // out is if we lost out device between the time -B saw it and
                        // when we tried to get this data...
                        m_eventwaithandleRead.WaitOne(5000);

                        // End the process...
                        process.CancelOutputRead();
                        process.CancelErrorRead();
                        m_eventwaithandleRead = null;
                        process.Kill();

                        #endregion


                        // Update the cache...
                        lock (m_objectLockCache)
                        {
                            // Find the item (in case we're out of sync)...
                            if (m_adnssddeviceinfoCache != null)
                            {
                                // Look for it...
                                for (ii = 0; ii < m_adnssddeviceinfoCache.Length; ii++)
                                {
                                    if (    (m_adnssddeviceinfoCache[ii].lInterface == m_dnssddeviceinfoTmp.lInterface)
                                        &&  (m_adnssddeviceinfoCache[ii].szServiceName == m_dnssddeviceinfoTmp.szServiceName))
                                    {
                                        // Update it...
                                        m_adnssddeviceinfoCache[ii] = m_dnssddeviceinfoTmp;
                                        break;
                                    }
                                }

                                // Hey, we found it, so we're done...
                                if (ii < m_adnssddeviceinfoCache.Length)
                                {
                                    continue;
                                }
                            }

                            // Add the new item...
                            if (m_adnssddeviceinfoCache == null)
                            {
                                m_adnssddeviceinfoCache = new DnssdDeviceInfo[1];
                                m_adnssddeviceinfoCache[0] = m_dnssddeviceinfoTmp;
                            }
                            // Append the item...
                            else
                            {
                                DnssdDeviceInfo[] adnssddeviceinfo = new DnssdDeviceInfo[m_adnssddeviceinfoCache.Length + 1];
                                m_adnssddeviceinfoCache.CopyTo(adnssddeviceinfo, 0);
                                adnssddeviceinfo[m_adnssddeviceinfoCache.Length] = m_dnssddeviceinfoTmp;
                                m_adnssddeviceinfoCache = adnssddeviceinfo;
                                Array.Sort(m_adnssddeviceinfoCache);
                            }
                        }
                    }
                    #endregion

                    // Something was removed...
                    #region Something was removed...
                    else if (szOutput.Contains(" Rmv "))
                    {
                        // Parse the data...
                        m_dnssddeviceinfoTmp = new DnssdDeviceInfo();
                        string[] aszOutput = szOutput.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (aszOutput.Length < 7)
                        {
                            continue;
                        }
                        if (!long.TryParse(aszOutput[3], out m_dnssddeviceinfoTmp.lInterface))
                        {
                            continue;
                        }
                        m_dnssddeviceinfoTmp.szServiceName = aszOutput[6].Replace(".","\\.") + "._privet._tcp.local";

                        // Update the cache...
                        lock (m_objectLockCache)
                        {
                            // Find the item and remove it...
                            if (m_adnssddeviceinfoCache != null)
                            {
                                int ss;
                                int dd;

                                // Last item...
                                if (m_adnssddeviceinfoCache.Length == 1)
                                {
                                    if (    (m_adnssddeviceinfoCache[0].lInterface == m_dnssddeviceinfoTmp.lInterface)
                                        &&  (m_adnssddeviceinfoCache[0].szServiceName == m_dnssddeviceinfoTmp.szServiceName))
                                    {
                                        m_adnssddeviceinfoCache = null;
                                    }
                                }

                                // Not the last item...
                                else
                                {
                                    // Save everything except for the "Rmv" item that matches...
                                    DnssdDeviceInfo[] adnssddeviceinfo = new DnssdDeviceInfo[m_adnssddeviceinfoCache.Length];
                                    for (ss = dd = 0; ss < m_adnssddeviceinfoCache.Length; ss++)
                                    {
                                        if (m_adnssddeviceinfoCache[ss] == null)
                                        {
                                            continue;
                                        }
                                        if (    (m_adnssddeviceinfoCache[ss].lInterface != m_dnssddeviceinfoTmp.lInterface)
                                            ||  (m_adnssddeviceinfoCache[ss].szServiceName != m_dnssddeviceinfoTmp.szServiceName))
                                        {
                                            adnssddeviceinfo[dd++] = m_adnssddeviceinfoCache[ss];
                                        }
                                    }
                                    if (dd == 0)
                                    {
                                        m_adnssddeviceinfoCache = null;
                                    }
                                    else
                                    {
                                        m_adnssddeviceinfoCache = new DnssdDeviceInfo[dd];
                                        for (ss = 0; ss < dd; ss++)
                                        {
                                            m_adnssddeviceinfoCache[ss] = adnssddeviceinfo[ss];
                                        }
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }

                // Well, this is bad...
                catch (Exception exception)
                {
                    Log.Info("ReadLine failed..." + exception.Message);
                    break;
                }
            }

            // Wait for the process to exit...
            if (m_processDnssdMonitor != null)
            {
                m_processDnssdMonitor.Kill();
                m_processDnssdMonitor.Dispose();
                m_processDnssdMonitor = null;
            }
        }

        /// <summary>
        /// Handle the Linux avahi-browse process output from this thread...
        /// </summary>
        internal void DnssdMonitorLinux()
        {
            string szOutput;

            // Start the child process.
            m_processDnssdMonitor = new Process();

            // Redirect the output stream of the child process.
            m_processDnssdMonitor.StartInfo.UseShellExecute = false;
            m_processDnssdMonitor.StartInfo.WorkingDirectory = Path.GetDirectoryName(m_szDnssdPath);
            m_processDnssdMonitor.StartInfo.CreateNoWindow = true;
            m_processDnssdMonitor.StartInfo.RedirectStandardOutput = true;
            m_processDnssdMonitor.StartInfo.RedirectStandardError = true;
            m_processDnssdMonitor.StartInfo.FileName = m_szDnssdPath;
            m_processDnssdMonitor.StartInfo.Arguments = m_szDnssdArguments;
            m_processDnssdMonitor.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            m_processDnssdMonitor.Start();

            // Read each line as it comes in from the process, when the
            // caller is done they'll kill it off and the read should
            // abort at that point...
            szOutput = "";
            for (;;)
            {
                try
                {
                    // Read a line of data...
                    try
                    {
                        szOutput = m_processDnssdMonitor.StandardOutput.ReadLine();
                    }
                    catch
                    {
                        break;
                    }

                    // Something was added...
                    #region Something was added...
                    if (szOutput.StartsWith("="))
                    {
                        int ii;

                        // Give ourself a place to store what we find...
                        m_dnssddeviceinfoTmp = new DnssdDeviceInfo();


                        ///////////////////////////////////////////////////////////
                        // Parse the -pvrk data (browsing)...
                        #region Parse -pvrk

                        // We can get the flags and the interface by splitting, we
                        // need to remove empty entries because there could be a
                        // leading space...
                        string[] aszOutput = szOutput.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        if (aszOutput.Length < 9)
                        {
                            continue;
                        }

                        // Get the interface...
                        m_dnssddeviceinfoTmp.lInterface = 0;
                        if (aszOutput[1].StartsWith("eth"))
                        {
                            if (!long.TryParse(aszOutput[1].Substring(3), out m_dnssddeviceinfoTmp.lInterface))
                            {
                                continue;
                            }
                            m_dnssddeviceinfoTmp.lInterface += 1000; 
                        }
                        else if (aszOutput[1].StartsWith("wlan"))
                        {
                            if (!long.TryParse(aszOutput[1].Substring(4), out m_dnssddeviceinfoTmp.lInterface))
                            {
                                continue;
                            }
                            m_dnssddeviceinfoTmp.lInterface += 2000;
                        }
                        else
                        {
                            continue;
                        }

                        // Get the servicename...
                        m_dnssddeviceinfoTmp.szServiceName = aszOutput[3];

                        // Get the link local...
                        m_dnssddeviceinfoTmp.szLinkLocal = aszOutput[6];

                        // Get the ip address...
                        if ((m_dnssddeviceinfoTmp.lInterface >= 2000) && (m_dnssddeviceinfoTmp.lInterface <= 2999))
                        {
                            if (aszOutput[2] == "IPv4")
                            {
                                m_dnssddeviceinfoTmp.szIpv4 = aszOutput[7];
                            }
                            else if (aszOutput[2] == "IPv6")
                            {
                                m_dnssddeviceinfoTmp.szIpv6 = aszOutput[7];
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else if ((m_dnssddeviceinfoTmp.lInterface >= 1000) && (m_dnssddeviceinfoTmp.lInterface <= 1999))
                        {
                            if (aszOutput[2] == "IPv4")
                            {
                                m_dnssddeviceinfoTmp.szIpv4 = aszOutput[7];
                            }
                            else if (aszOutput[2] == "IPv6")
                            {
                                m_dnssddeviceinfoTmp.szIpv6 = aszOutput[7];
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            continue;
                        }

                        // Get the port...
                        if (!long.TryParse(aszOutput[8], out m_dnssddeviceinfoTmp.lPort))
                        {
                            continue;
                        }

                        // Get the whole text section...
                        string[] aszTxt = szOutput.Split(new string[] { ";" + m_dnssddeviceinfoTmp.lPort + ";" }, StringSplitOptions.RemoveEmptyEntries);
                        if ((aszTxt == null) || (aszTxt.Length < 2) || (aszTxt.Length < 2) || !aszTxt[1].Contains("\""))
                        {
                            continue;
                        }

                        // Lose the opening and closing quotes...
                        szOutput = aszTxt[1].Substring(1, aszTxt[1].Length - 1);

                        // Split the rest on " " boundaries...
                        aszTxt = szOutput.Split(new string[] { "\" \"" }, StringSplitOptions.RemoveEmptyEntries);

                        // Loop through those items...
                        for (ii = 0; ii < aszTxt.Length; ii++)
                        {
                            // txtvers...
                            if (aszTxt[ii].StartsWith("txtvers="))
                            {
                                m_dnssddeviceinfoTmp.szTxtTxtvers = aszTxt[ii].Substring(8);
                            }

                            // ty...
                            else if (aszTxt[ii].StartsWith("ty="))
                            {
                                m_dnssddeviceinfoTmp.szTxtTy = aszTxt[ii].Substring(3);
                            }

                            // type...
                            else if (aszTxt[ii].StartsWith("type="))
                            {
                                m_dnssddeviceinfoTmp.szTxtType = aszTxt[ii].Substring(5);
                            }

                            // id...
                            else if (aszTxt[ii].StartsWith("id="))
                            {
                                m_dnssddeviceinfoTmp.szTxtId = aszTxt[ii].Substring(3);
                            }

                            // cs...
                            else if (aszTxt[ii].StartsWith("cs="))
                            {
                                m_dnssddeviceinfoTmp.szTxtCs = aszTxt[ii].Substring(3);
                            }

                            // https...
                            else if (aszTxt[ii].StartsWith("https="))
                            {
                                m_dnssddeviceinfoTmp.blTxtHttps = (aszTxt[ii].Substring(3) != "0");
                            }

                            // note...
                            else if (aszTxt[ii].StartsWith("note="))
                            {
                                m_dnssddeviceinfoTmp.szTxtNote = aszTxt[ii].Substring(5);
                            }
                        }

                        #endregion


                        // Update the cache...
                        lock (m_objectLockCache)
                        {
                            // Find the item (in case we're out of sync)...
                            if (m_adnssddeviceinfoCache != null)
                            {
                                // Look for it...
                                for (ii = 0; ii < m_adnssddeviceinfoCache.Length; ii++)
                                {
                                    if (    (m_adnssddeviceinfoCache[ii].lInterface == m_dnssddeviceinfoTmp.lInterface)
                                        &&  (m_adnssddeviceinfoCache[ii].szServiceName == m_dnssddeviceinfoTmp.szServiceName))
                                    {
                                        // Update it...
                                        m_adnssddeviceinfoCache[ii] = m_dnssddeviceinfoTmp;
                                        break;
                                    }
                                }

                                // Hey, we found it, so we're done...
                                if (ii < m_adnssddeviceinfoCache.Length)
                                {
                                    continue;
                                }
                            }

                            // Add the new item...
                            if (m_adnssddeviceinfoCache == null)
                            {
                                m_adnssddeviceinfoCache = new DnssdDeviceInfo[1];
                                m_adnssddeviceinfoCache[0] = m_dnssddeviceinfoTmp;
                            }
                            // Append the item...
                            else
                            {
                                DnssdDeviceInfo[] adnssddeviceinfo = new DnssdDeviceInfo[m_adnssddeviceinfoCache.Length + 1];
                                m_adnssddeviceinfoCache.CopyTo(adnssddeviceinfo, 0);
                                adnssddeviceinfo[m_adnssddeviceinfoCache.Length] = m_dnssddeviceinfoTmp;
                                m_adnssddeviceinfoCache = adnssddeviceinfo;
                                Array.Sort(m_adnssddeviceinfoCache);
                            }
                        }
                    }
                    #endregion

                    // Something was removed...
                    #region Something was removed...
                    else if (szOutput.StartsWith("-"))
                    {
                        // Parse the data...
                        m_dnssddeviceinfoTmp = new DnssdDeviceInfo();
                        string[] aszOutput = szOutput.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (aszOutput.Length < 7)
                        {
                            continue;
                        }
                        if (!long.TryParse(aszOutput[3], out m_dnssddeviceinfoTmp.lInterface))
                        {
                            continue;
                        }
                        m_dnssddeviceinfoTmp.szServiceName = aszOutput[6].Replace(".", "\\.") + "._privet._tcp.local";

                        // Update the cache...
                        lock (m_objectLockCache)
                        {
                            // Find the item and remove it...
                            if (m_adnssddeviceinfoCache != null)
                            {
                                int ss;
                                int dd;

                                // Last item...
                                if (m_adnssddeviceinfoCache.Length == 1)
                                {
                                    if ((m_adnssddeviceinfoCache[0].lInterface == m_dnssddeviceinfoTmp.lInterface)
                                        && (m_adnssddeviceinfoCache[0].szServiceName == m_dnssddeviceinfoTmp.szServiceName))
                                    {
                                        m_adnssddeviceinfoCache = null;
                                    }
                                }

                                // Not the last item...
                                else
                                {
                                    // Save everything except for the "Rmv" item that matches...
                                    DnssdDeviceInfo[] adnssddeviceinfo = new DnssdDeviceInfo[m_adnssddeviceinfoCache.Length];
                                    for (ss = dd = 0; ss < m_adnssddeviceinfoCache.Length; ss++)
                                    {
                                        if (m_adnssddeviceinfoCache[ss] == null)
                                        {
                                            continue;
                                        }
                                        if ((m_adnssddeviceinfoCache[ss].lInterface != m_dnssddeviceinfoTmp.lInterface)
                                            || (m_adnssddeviceinfoCache[ss].szServiceName != m_dnssddeviceinfoTmp.szServiceName))
                                        {
                                            adnssddeviceinfo[dd++] = m_adnssddeviceinfoCache[ss];
                                        }
                                    }
                                    if (dd == 0)
                                    {
                                        m_adnssddeviceinfoCache = null;
                                    }
                                    else
                                    {
                                        m_adnssddeviceinfoCache = new DnssdDeviceInfo[dd];
                                        for (ss = 0; ss < dd; ss++)
                                        {
                                            m_adnssddeviceinfoCache[ss] = adnssddeviceinfo[ss];
                                        }
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }

                // Well, this is bad...
                catch (Exception exception)
                {
                    Log.Info("ReadLine failed..." + exception.Message);
                    break;
                }
            }

            // Wait for the process to exit...
            m_processDnssdMonitor.WaitForExit();
            m_processDnssdMonitor.Close();
            m_processDnssdMonitor = null;
        }

        /// <summary>
        /// Handle the Mac OS X (aka macOS) dns-sd process output from this thread.
        /// Right now it's identical to Windows, assuming you have the right version
        /// of Bonjour installed...
        /// </summary>
        internal void DnssdMonitorMacOsX()
        {
            DnssdMonitorWindows();
        }

        /// <summary>
        /// Handle the Windows dns-sd process output from this thread...
        /// </summary>
        internal void DnssdRegisterWindowsPrivetTcpLocal()
        {
            string szOutput;

            // Start the child process.
            m_processDnssdRegisterPrivetTcpLocal = new Process();

            // Redirect the output stream of the child process.
            m_processDnssdRegisterPrivetTcpLocal.StartInfo.UseShellExecute = false;
            m_processDnssdRegisterPrivetTcpLocal.StartInfo.WorkingDirectory = Path.GetDirectoryName(m_szDnssdPath);
            m_processDnssdRegisterPrivetTcpLocal.StartInfo.CreateNoWindow = true;
            m_processDnssdRegisterPrivetTcpLocal.StartInfo.RedirectStandardOutput = true;
            m_processDnssdRegisterPrivetTcpLocal.StartInfo.RedirectStandardError = true;
            m_processDnssdRegisterPrivetTcpLocal.StartInfo.FileName = m_szDnssdPath;
            m_processDnssdRegisterPrivetTcpLocal.StartInfo.Arguments = m_szDnssdArgumentsPrivetTcpLocal;
            m_processDnssdRegisterPrivetTcpLocal.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            m_processDnssdRegisterPrivetTcpLocal.Start();

            // Read each line as it comes in from the process, when the
            // caller is done they'll kill it off and the read should
            // abort at that point...
            for (; ; )
            {
                try
                {
                    // Read a line of data...
                    szOutput = m_processDnssdRegisterPrivetTcpLocal.StandardOutput.ReadLine();
                }

                // Well, this is bad...
                catch (Exception exception)
                {
                    Log.Info("ReadLine failed..." + exception.Message);
                    break;
                }
            }

            // Wait for the process to exit...
            if (m_processDnssdRegisterPrivetTcpLocal != null)
            {
                try
                {
                    m_processDnssdRegisterPrivetTcpLocal.WaitForExit();
                }
                catch
                {
                    // nothing needed here...
                }
                m_processDnssdRegisterPrivetTcpLocal.Dispose();
                m_processDnssdRegisterPrivetTcpLocal = null;
            }
        }

        /// <summary>
        /// Handle the Windows dns-sd process output from this thread...
        /// </summary>
        internal void DnssdRegisterWindowsTwainDirectSubPrivetTcpLocal()
        {
            string szOutput;

            // Start the child process.
            m_processDnssdRegisterTwainDirectSubPrivetTcpLocal = new Process();

            // Redirect the output stream of the child process.
            m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.StartInfo.UseShellExecute = false;
            m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.StartInfo.WorkingDirectory = Path.GetDirectoryName(m_szDnssdPath);
            m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.StartInfo.CreateNoWindow = true;
            m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.StartInfo.RedirectStandardOutput = true;
            m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.StartInfo.RedirectStandardError = true;
            m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.StartInfo.FileName = m_szDnssdPath;
            m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.StartInfo.Arguments = m_szDnssdArgumentsTwainDirectPrivetTcpLocal;
            m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.Start();

            // Read each line as it comes in from the process, when the
            // caller is done they'll kill it off and the read should
            // abort at that point...
            for (;;)
            {
                try
                {
                    // Read a line of data...
                    szOutput = m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.StandardOutput.ReadLine();
                }

                // Well, this is bad...
                catch (Exception exception)
                {
                    Log.Info("ReadLine failed..." + exception.Message);
                    break;
                }
            }

            // Wait for the process to exit...
            if (m_processDnssdRegisterTwainDirectSubPrivetTcpLocal != null)
            {
                try
                {
                    m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.WaitForExit();
                }
                catch
                {
                    // nothing needed here...
                }
                m_processDnssdRegisterTwainDirectSubPrivetTcpLocal.Dispose();
                m_processDnssdRegisterTwainDirectSubPrivetTcpLocal = null;

            }
        }

        /// <summary>
        /// Handle the Linux avahi-publish process output from this thread.
        /// Right now we don't need anything different from Windows...
        /// </summary>
        internal void DnssdRegisterLinuxPrivetTcpLocal()
        {
            DnssdRegisterWindowsPrivetTcpLocal();
        }

        /// <summary>
        /// Handle the Linux avahi-publish process output from this thread.
        /// Right now we don't need anything different from Windows...
        /// </summary>
        internal void DnssdRegisterLinuxTwainDirectSubPrivetTcpLocal()
        {
            DnssdRegisterWindowsTwainDirectSubPrivetTcpLocal();
        }

        /// <summary>
        /// Handle the Mac OS X (aka macOS) dns-sd process output from this thread.
        /// Right now it's identical to Windows, assuming you have the right version
        /// of Bonjour installed...
        /// </summary>
        internal void DnssdRegisterMacOsXPrivetTcpLocal()
        {
            DnssdRegisterWindowsPrivetTcpLocal();
        }

        /// <summary>
        /// Handle the Mac OS X (aka macOS) dns-sd process output from this thread.
        /// Right now it's identical to Windows, assuming you have the right version
        /// of Bonjour installed...
        /// </summary>
        internal void DnssdRegisterMacOsXTwainDirectSubPrivetTcpLocal()
        {
            DnssdRegisterWindowsTwainDirectSubPrivetTcpLocal();
        }

        #endregion

        
        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// The reason we're using this class, set in the constructor and
        /// verified in each public function...
        /// </summary>
        private Reason m_reason;

        /// <summary>
        /// The thread that'll monitor for devices...
        /// </summary>
        private Thread m_threadDnsdMonitor;

        /// <summary>
        /// The process that'll monitor for devices...
        /// </summary>
        private Process m_processDnssdMonitor;

        /// <summary>
        /// The thread for registering a device as a privet
        /// service...
        /// </summary>
        private Thread m_threadDnsdRegisterPrivetTcpLocal;

        /// <summary>
        /// The thread for registering a device as a twaindirect
        /// sub privet service...
        /// </summary>
        private Thread m_threadDnsdRegisterTwainDirectSubPrivetTcpLocal;

        /// <summary>
        /// The process for registering a device...
        /// </summary>
        private Process m_processDnssdRegisterPrivetTcpLocal;

        /// <summary>
        /// The process for registering a device...
        /// </summary>
        private Process m_processDnssdRegisterTwainDirectSubPrivetTcpLocal;

        /// <summary>
        /// The full path to the diagnostic program we'll be running...
        /// </summary>
        private string m_szDnssdPath;

        /// <summary>
        /// The browser arguments to the mDNS program...
        /// </summary>
        private string m_szDnssdArguments;

        /// <summary>
        /// The browser arguments to the mDNS program...
        /// </summary>
        private string m_szDnssdArgumentsPrivetTcpLocal;

        /// <summary>
        /// The browser arguments to the mDNS program...
        /// </summary>
        private string m_szDnssdArgumentsTwainDirectPrivetTcpLocal;

        /// <summary>
        /// Our cache of devices currently on the LAN, this is the
        /// official cache that can be referenced at any given time...
        /// </summary>
        private DnssdDeviceInfo[] m_adnssddeviceinfoCache;

        /// <summary>
        /// A scratchpad used to assemble data...
        /// </summary>
        private DnssdDeviceInfo m_dnssddeviceinfoTmp;

        /// <summary>
        /// An event to know when we are done reading data
        /// from a process...
        /// </summary>
        private EventWaitHandle m_eventwaithandleRead;

        /// <summary>
        /// Object used to lock the cache...
        /// </summary>
        private object m_objectLockCache;

        #endregion
    }
}
