///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.Support.Dnssd
//
// What we have here is a class that lets us register or monitor for _twaindirect
// subtypes under _privet._tcp.  The code is currently for Bonjour on Windows.
// Adding Avahi for Linux and Bonjour for Mac shouldn't be too big of a deal, but
// it'll have to come later...
//
// As for the mDNS / DNS-SD advertising, we're complying with the TWAIN Local
// Specification:  http://twaindirect.org
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    01-Jul-2015     Initial Release
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
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

// Namespace for things shared across the system...
namespace TwainDirect.Support
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

            // Load the library...
            m_hmoduleDnssd = NativeMethods.LoadLibraryExW("dnssd.dll", IntPtr.Zero, 0);
            if (m_hmoduleDnssd == IntPtr.Zero)
            {
                Log.Error("dnssd.dll is not installed...");
                return;
            }

            // Load the functions...
            m_pfndnsservicebrowse = (pfnDNSServiceBrowse)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(m_hmoduleDnssd, "DNSServiceBrowse"), typeof(pfnDNSServiceBrowse));
            m_pfndnsserrvicecreateconnection = (pfnDNSServiceCreateConnection)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(m_hmoduleDnssd, "DNSServiceCreateConnection"), typeof(pfnDNSServiceCreateConnection));
            m_pfndnsserviceprocessresult = (pfnDNSServiceProcessResult)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(m_hmoduleDnssd, "DNSServiceProcessResult"), typeof(pfnDNSServiceProcessResult));
            m_pfndnsservicerefdeallocate = (pfnDNSServiceRefDeallocate)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(m_hmoduleDnssd, "DNSServiceRefDeallocate"), typeof(pfnDNSServiceRefDeallocate));
            m_pfndnsservicerefsockfd = (pfnDNSServiceRefSockFD)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(m_hmoduleDnssd, "DNSServiceRefSockFD"), typeof(pfnDNSServiceRefSockFD));
            m_pfndnsserviceresolve = (pfnDNSServiceResolve)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(m_hmoduleDnssd, "DNSServiceResolve"), typeof(pfnDNSServiceResolve));
            m_pfndnsservicequeryrecord = (pfnDNSServiceQueryRecord)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(m_hmoduleDnssd, "DNSServiceQueryRecord"), typeof(pfnDNSServiceQueryRecord));
            m_pfndnsserviceregister = (pfnDNSServiceRegister)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(m_hmoduleDnssd, "DNSServiceRegister"), typeof(pfnDNSServiceRegister));
            if (    (m_pfndnsservicebrowse == null)
                ||  (m_pfndnsserrvicecreateconnection == null)
                ||  (m_pfndnsserviceprocessresult == null)
                ||  (m_pfndnsservicerefdeallocate == null)
                ||  (m_pfndnsservicerefsockfd == null)
                ||  (m_pfndnsserviceresolve == null)
                ||  (m_pfndnsservicequeryrecord == null)
                || (m_pfndnsserviceregister == null))
            {
                Log.Error("dnssd.dll is missing functions...");
                NativeMethods.FreeLibrary(m_hmoduleDnssd);
                m_hmoduleDnssd = IntPtr.Zero;
                return;
            }
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
                    if (    (a_adnssddeviceinfoCompare[ii].GetInterface() != dnssddeviceinfoCache[ii].GetInterface())
                        ||  (a_adnssddeviceinfoCompare[ii].GetTxtTy() != dnssddeviceinfoCache[ii].GetTxtTy())
                        ||  (a_adnssddeviceinfoCompare[ii].GetServiceName() != dnssddeviceinfoCache[ii].GetServiceName())
                        ||  (a_adnssddeviceinfoCompare[ii].GetLinkLocal() != dnssddeviceinfoCache[ii].GetLinkLocal())
                        ||  (a_adnssddeviceinfoCompare[ii].GetPort() != dnssddeviceinfoCache[ii].GetPort())
                        ||  (a_adnssddeviceinfoCompare[ii].GetIpv4() != dnssddeviceinfoCache[ii].GetIpv4())
                        ||  (a_adnssddeviceinfoCompare[ii].GetIpv6() != dnssddeviceinfoCache[ii].GetIpv6()))
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
        /// The monitor thread...
        /// </summary>
        /// <returns>true on success</returns>
        public void MonitorThread()
        {
            // Windows...
            IntPtr instance;
            NativeMethods.WNDCLASS wcex;
            NativeMethods.MSG msg;
            Int32 dnsserviceerrortype;

            // Create the window. This window won't actually be shown, but it demonstrates how to use Bonjour
            // with Windows GUI applications by having Bonjour events processed as messages to a Window.
            instance = NativeMethods.GetModuleHandleW(null);
            NativeMethods.WndProcDelegate wndprocdelegate = WndProcLaunchpad;

            // Register our class...
            wcex = new NativeMethods.WNDCLASS();
            wcex.lpszClassName = "TwainDirectBonjourWindow";
            wcex.lpfnWndProc = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(wndprocdelegate);
            Int16 i16 = NativeMethods.RegisterClassW(ref wcex);

            // Create our window...
            m_hwnd = NativeMethods.CreateWindowExW
            (
                0x300 /*WS_EX_OVERLAPPEDWINDOW*/,
                wcex.lpszClassName,
                wcex.lpszClassName,
                0,
                -2147483648, // CW_USEDEFAULT
                0,
                -2147483648, // CW_USEDEFAULT
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                instance,
                IntPtr.Zero
            );
            if (m_hwnd == IntPtr.Zero)
            {
                Log.Error("NativeMethods.CreateWindowExW failed...");
                // Handle an error...
            }

            // Make sure the window has our object...
            GCHandle gchandleObject = GCHandle.Alloc(this);
            if (Config.GetMachineWordSize() == 32)
            {
                NativeMethods.SetWindowLong(m_hwnd, -21, (int)GCHandle.ToIntPtr(gchandleObject)); // GWL_USERDATA
            }
            else
            {
                NativeMethods.SetWindowLongPtr(m_hwnd, -21, GCHandle.ToIntPtr(gchandleObject)); // GWL_USERDATA
            }

            // Start browsing for services and associate the Bonjour browser with our window using the 
            // WSAAsyncSelect mechanism. Whenever something related to the Bonjour browser occurs, our 
            // private Windows message will be sent to our window so we can give Bonjour a chance to 
            // process it. This allows Bonjour to avoid using a secondary thread (and all the issues 
            // with synchronization that would introduce), but still process everything asynchronously.
            // This also simplifies app code because Bonjour will only run when we explicitly call it.

            // We start by creating a connection to the service.  We're going to share this with all of
            // the other calls.  Doing this simplifies life, allowing us to see our device across
            // every interface.
            dnsserviceerrortype = m_pfndnsserrvicecreateconnection(ref m_dnsservicerefClient);
            if (dnsserviceerrortype != 0 /*kDNSServiceErr_NoError*/)
            {
                Log.Error("m_pfndnsserrvicecreateconnection failed...");
                // Handle an error...
            }

            // TBD
            // Fire up the browser.  This will run until we tear it down.  It's not entirely clear to
            // me as to how we're supposed to manage dnsservicerefTmp.  It's value changes after the
            // call, so something was done to it.  We may need to save these and free them calls to the
            // deallocation function, which is why I have a TBD at the top of this.  Note that this
            // may apply to every place where dnsservicerefTmp is used...
            GCHandle gchandle = GCHandle.Alloc(this);
            DNSServiceBrowseReply dnsservicebrowsereply = BrowserCallBackLaunchpad;
            IntPtr dnsservicerefTmp = m_dnsservicerefClient;
            dnsserviceerrortype = m_pfndnsservicebrowse
            (
                ref dnsservicerefTmp,                       // Receives reference to Bonjour browser object.
                0x4000,                                     // kDNSServiceFlagsShareConnection.
                0, //kDNSServiceInterfaceIndexAny           // Browse on all network interfaces.
                "_privet._tcp,_twaindirect",                // Browse for privet service types, with a sub-type of _twaindirect
                null,                                       // Browse on the default domain (e.g. local.).
                dnsservicebrowsereply,                      // Callback function when Bonjour events occur.
                GCHandle.ToIntPtr(gchandle)                 // No callback context needed.
            );
            if (dnsserviceerrortype != 0 /*kDNSServiceErr_NoError*/)
            {
                Log.Error("m_pfndnsservicebrowse failed: " + dnsserviceerrortype);
                // Handle an error...
            }

            dnsserviceerrortype = NativeMethods.WSAAsyncSelect(m_pfndnsservicerefsockfd(m_dnsservicerefClient), m_hwnd, NativeMethods.BONJOUR_EVENT, (1 << 0) /*FD_READ*/ | (1 << 5) /*FD_CLOSE*/);
            if (dnsserviceerrortype != 0 /*kDNSServiceErr_NoError*/)
            {
                Log.Error("NativeMethods.WSAAsyncSelect failed: " + dnsserviceerrortype);
                // Handle an error...
            }

            // Main event loop for the application. All Bonjour events are dispatched while in this loop.
            while (NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0) != 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }

            // All done...
            return;
        }

        /// <summary>
        /// Start the monitoring system...
        /// </summary>
        /// <returns>true on success</returns>
        public bool MonitorStart(OsDnssdCallback a_osdnssdcallback, IntPtr a_pvOsdnssdcallbackArg)
        {
            // Hey, we already have one of these...
            if (m_threadMonitor != null)
            {
                return (true);
            }

            // No joy...
            if (m_hmoduleDnssd == IntPtr.Zero)
            {
                return (false);
            }

            // Init stuff...
            m_osdnssdcallback = a_osdnssdcallback;
            m_intptrOsdnssdcallbackArg = a_pvOsdnssdcallbackArg;

            // Start our thread...
            m_threadMonitor = new Thread(MonitorThread);
            m_threadMonitor.Start();

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
        /// <param name="a_szUrl">url of cloud server or empty string</param>
        /// <param name="a_szNote">a helpful note about the device (optional)</param>
        /// <returns>true on success</returns>
        public bool RegisterStart
        (
            string a_szInstanceName,
            int a_iPort,
            string a_szTy,
            string a_szUrl,
            string a_szNote
        )
        {
            // Text...
            // txtvers=1
            // ty=a_szTy
            // url=
            // type=twaindirect
            // id=
            // cs=offline
            // note=a_szNote
            int ii;
            int tt;
            Int32 dnsserviceerrortype;

            // Validate...
            if (m_reason != Reason.Register)
            {
                Log.Error("This function can't be used at this time...");
                return (true);
            }
            if (m_dnsservicerefRegister != IntPtr.Zero)
            {
                Log.Error("Register has already been started...");
                return (true);
            }

            // mDNS record...
            Log.Info
            (
                "mDNS: " +
                "instance=<" + a_szInstanceName + "._twaindirect._privet._tcp> " +
                "port=<" + a_iPort + "> " +
                "txtvers=<1> " +
                "ty=<" + a_szTy + "> " +
                "url=<" + a_szUrl + "> " +
                "type=<twaindirect> " +
                "id=<> " +
                "cs=<offline> " +
                "https=<" + ((Config.Get("useHttps", "yes") == "no") ? "0" : "1") + "> " +
                "note=<" + a_szNote + "> "
            );

            // Build the txt record, note that there is no "auto" for https=
            // on the registration side.  The user has to pick, and the default
            // is for https being true.  Once the record is built we have to
            // plug in the length prefix for each key=value pair, which is what
            // the tab stuff is all about.  That seemed like a reasonable
            // separator...
            string szTxt =
                "\t" + "txtvers=1" +
                "\t" + "ty=" + a_szTy +
                "\t" + "url=" + a_szUrl +
                "\t" + "type=twaindirect" +
                "\t" + "id=" +
                "\t" + "cs=offline" +
                "\t" + "https=" + ((Config.Get("useHttps", "yes") == "no") ? "0" : "1") +
                (string.IsNullOrEmpty(a_szNote) ? "" : "\t" + "note=" + a_szNote);
            byte[] abTxt = Encoding.UTF8.GetBytes(szTxt);
            for (tt = ii = 0; ii < abTxt.Length; ii++)
            {
                // Looking for placeholders...
                if (abTxt[ii] != '\t')
                {
                    continue;
                }

                // Dispatch...
                switch (tt)
                {
                    default:
                    case 0: abTxt[ii] = 9; break; // txtvers
                    case 1: abTxt[ii] = (byte)(3 + Encoding.UTF8.GetBytes(a_szTy).Length); break; // ty
                    case 2: abTxt[ii] = (byte)(4 + Encoding.UTF8.GetBytes(a_szUrl).Length); break; // url
                    case 3: abTxt[ii] = 16; break; // type
                    case 4: abTxt[ii] = 3; break; // id
                    case 5: abTxt[ii] = 10; break; // cs
                    case 6: abTxt[ii] = 7; break; // https
                    case 7: abTxt[ii] = (byte)(5 + Encoding.UTF8.GetBytes(a_szNote).Length); break; // note
                }

                // Next item...
                tt += 1;
            }

            // Register...
            dnsserviceerrortype = m_pfndnsserviceregister
            (
                ref m_dnsservicerefRegister,
                0,
                0,
                null,
                "_privet._tcp,_twaindirect",
                null,
                null,
                (short)WX((ushort)a_iPort),
                (short)abTxt.Length,
                abTxt,
                IntPtr.Zero,
                IntPtr.Zero
            );
            if (dnsserviceerrortype != 0) // kDNSServiceErr_NoError
            {
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
            // Public Methods
            #region Public Methods

            /// <summary>
            /// Init stuff...
            /// </summary>
            public DnssdDeviceInfo()
            {
                m_szTxtTy = "";
                m_szServiceName = "";
                m_szLinkLocal = "";
                m_lInterface = 0;
                m_szIpv4 = "";
                m_szIpv6 = "";
                m_lPort = 0;
                m_lFlags = 0;
                m_lTtl = 0;
                m_szTxtTxtvers = "";
                m_szTxtType = "";
                m_szTxtId = "";
                m_szTxtTy = "";
                m_szTxtCs = "";
                m_szTxtNote = "";
                m_blTxtHttps = false;
                m_aszTxt = null;
            }

            /// <summary>
            /// We need this so we can clone the object...
            /// </summary>
            /// <param name="a_dnssddeviceinfo">the beastie to copy</param>
            public DnssdDeviceInfo(DnssdDeviceInfo a_dnssddeviceinfo)
            {
                m_szTxtTy = a_dnssddeviceinfo.m_szTxtTy;
                m_szServiceName = a_dnssddeviceinfo.m_szServiceName;
                m_szLinkLocal = a_dnssddeviceinfo.m_szLinkLocal;
                m_lInterface = a_dnssddeviceinfo.m_lInterface;
                m_szIpv4 = a_dnssddeviceinfo.m_szIpv4;
                m_szIpv6 = a_dnssddeviceinfo.m_szIpv6;
                m_lPort = a_dnssddeviceinfo.m_lPort;
                m_lFlags = a_dnssddeviceinfo.m_lFlags;
                m_lTtl = a_dnssddeviceinfo.m_lTtl;
                m_szTxtTxtvers = a_dnssddeviceinfo.m_szTxtTxtvers;
                m_szTxtType = a_dnssddeviceinfo.m_szTxtType;
                m_szTxtId = a_dnssddeviceinfo.m_szTxtId;
                m_szTxtTy = a_dnssddeviceinfo.m_szTxtTy;
                m_szTxtCs = a_dnssddeviceinfo.m_szTxtCs;
                m_szTxtNote = a_dnssddeviceinfo.m_szTxtNote;
                m_blTxtHttps = a_dnssddeviceinfo.m_blTxtHttps;

                // Handle the weird one...
                if ((a_dnssddeviceinfo.m_aszTxt == null) || (a_dnssddeviceinfo.m_aszTxt.Length == 0))
                {
                    m_aszTxt = null;
                }
                else
                {
                    int ii;
                    m_aszTxt = new string[a_dnssddeviceinfo.m_aszTxt.Length];
                    for (ii = 0; ii < a_dnssddeviceinfo.m_aszTxt.Length; ii++)
                    {
                        m_aszTxt[ii] = a_dnssddeviceinfo.m_aszTxt[ii];
                    }
                }
            }

            /// <summary>
            /// Add a text record to the TXT array...
            /// </summary>
            /// <param name="a_szTxt">record to add</param>
            public void AddTxt(string a_szTxt)
            {
                if (m_aszTxt == null)
                {
                    m_aszTxt = new string[1];
                    m_aszTxt[0] = a_szTxt;
                }
                else
                {
                    string[] asz = new string[m_aszTxt.Length + 1];
                    Array.Copy(m_aszTxt, asz, m_aszTxt.Length);
                    asz[m_aszTxt.Length] = a_szTxt;
                    m_aszTxt = asz;
                }
            }

            /// <summary>
            /// Implement our IComparable interface...
            /// </summary>
            /// <param name="obj">the object to compare against</param>
            /// <returns>-1, 0, 1</returns>
            public int CompareTo(object obj)
            {
                // Well, that's odd...
                if (obj == null)
                {
                    return (0);
                }

                // This is our object...
                if (obj is DnssdDeviceInfo)
                {
                    int iResult;
                    iResult = this.m_szTxtTy.CompareTo(((DnssdDeviceInfo)obj).m_szTxtTy);
                    if (iResult == 0)
                    {
                        iResult = this.m_szServiceName.CompareTo(((DnssdDeviceInfo)obj).m_szServiceName);
                    }
                    if (iResult == 0)
                    {
                        iResult = this.m_szLinkLocal.CompareTo(((DnssdDeviceInfo)obj).m_szLinkLocal);
                    }
                    if (iResult == 0)
                    {
                        iResult = this.m_lInterface.CompareTo(((DnssdDeviceInfo)obj).m_lInterface);
                    }
                    return (iResult);
                }

                // No joy...
                return (0);
            }

            /// <summary>
            /// Get the flags reported with it...
            /// </summary>
            /// <returns>flags reported with it</returns>
            public long GetFlags()
            {
                return (m_lFlags);
            }

            /// <summary>
            /// Get the interface it lives on...
            /// </summary>
            /// <returns>interface it lives on</returns>
            public long GetInterface()
            {
                return (m_lInterface);
            }

            /// <summary>
            /// Get the IPv4 address (if any)...
            /// </summary>
            /// <returns>IPv4 address (if any)</returns>
            public string GetIpv4()
            {
                return (m_szIpv4);
            }

            /// <summary>
            /// Get the IPv6 address (if any)...
            /// </summary>
            /// <returns>IPv6 address (if any)</returns>
            public string GetIpv6()
            {
                return (m_szIpv6);
            }

            /// <summary>
            /// Get the link local name for where the service is running...
            /// </summary>
            /// <returns>link local name for where the service is running</returns>
            public string GetLinkLocal()
            {
                return (m_szLinkLocal);
            }

            /// <summary>
            /// Get the port to use...
            /// </summary>
            /// <returns>port to use</returns>
            public long GetPort()
            {
                return (m_lPort);
            }

            /// <summary>
            /// Get the full, unique name of the device...
            /// </summary>
            /// <returns>unique name of the device</returns>
            public string GetServiceName()
            {
                return (m_szServiceName);
            }

            /// <summary>
            /// Get our time to live...
            /// </summary>
            /// <returns>time to live</returns>
            public long GetTtl()
            {
                return (m_lTtl);
            }

            /// <summary>
            /// Get the array of TXT records...
            /// </summary>
            /// <returns>array of TXT records</returns>
            public string[] GetTxt()
            {
                int ii;
                string[] aszTxt;

                // No data...
                if ((m_aszTxt == null) || (m_aszTxt.Length == 0))
                {
                    return (null);
                }

                // Make a copy...
                aszTxt = new string[m_aszTxt.Length];
                for (ii = 0; ii < m_aszTxt.Length; ii++)
                {
                    aszTxt[ii] = m_aszTxt[ii];
                }
                return (aszTxt);
            }

            /// <summary>
            /// Get our cloud status...
            /// </summary>
            /// <returns>cloud status</returns>
            public string GetTxtCs()
            {
                return (m_szTxtCs);
            }

            /// <summary>
            /// Get the HTTPS flag...
            /// </summary>
            /// <returns>https flag</returns>
            public bool GetTxtHttps()
            {
                return (m_blTxtHttps);
            }

            /// <summary>
            /// Get our text id, cloud id, empty if one isn't available...
            /// </summary>
            /// <returns>text id, cloud id, empty if one isn't available</returns>
            public string GetTxtId()
            {
                return (m_szTxtId);
            }

            /// <summary>
            /// Get a note about the device...
            /// </summary>
            /// <returns>a note about the device</returns>
            public string GetTxtNote()
            {
                return (m_szTxtNote);
            }

            /// <summary>
            /// Get our TXT version...
            /// </summary>
            /// <returns>our TXT version</returns>
            public string GetTxtTxtvers()
            {
                return (m_szTxtTxtvers);
            }

            /// <summary>
            /// Get the friendly name for the scanner...
            /// </summary>
            /// <returns>friendly name for the scanner</returns>
            public string GetTxtTy()
            {
                return (m_szTxtTy);
            }

            /// <summary>
            /// Get the text type, comma separated services supported by the device...
            /// </summary>
            /// <returns>text type, comma separated services supported by the device</returns>
            public string GetTxtType()
            {
                return (m_szTxtType);
            }

            /// <summary>
            /// Set the flags reported with it...
            /// </summary>
            /// <param name="a_lFlags">flags reported with it</param>
            public void SetFlags(long a_lFlags)
            {
                m_lFlags = a_lFlags;
            }

            /// <summary>
            /// Set the interface it lives on...
            /// </summary>
            /// <param name="a_lInterface">interface it lives on</param>
            public void SetInterface(long a_lInterface)
            {
                m_lInterface = a_lInterface;
            }

            /// <summary>
            /// Set the IPv4 address (if any)...
            /// </summary>
            /// <param name="a_szIpv4">IPv4 address (if any)</param>
            public void SetIpv4(string a_szIpv4)
            {
                m_szIpv4 = a_szIpv4;
            }

            /// <summary>
            /// Set the IPv6 address (if any)...
            /// </summary>
            /// <param name="a_szIpv6">IPv6 address (if any)</param>
            public void SetIpv6(string a_szIpv6)
            {
                m_szIpv6 = a_szIpv6;
            }

            /// <summary>
            /// Set link local name for where the service is running...
            /// </summary>
            /// <param name="a_szLinkLocal">link local name for where the service is running</param>
            public void SetLinkLocal(string a_szLinkLocal)
            {
                m_szLinkLocal = a_szLinkLocal;
            }

            /// <summary>
            /// Set the port to use...
            /// </summary>
            /// <param name="a_lPort">port to use</param>
            public void SetPort(long a_lPort)
            {
                m_lPort = a_lPort;
            }

            /// <summary>
            /// Set the full, unique name of the device...
            /// </summary>
            /// <param name="a_szServiceName">unique name of the device</param>
            public void SetServiceName(string a_szServiceName)
            {
                m_szServiceName = a_szServiceName;
            }

            /// <summary>
            /// Set our time to live...
            /// </summary>
            /// <param name="a_lTtl">time to live</param>
            public void SetTtl(long a_lTtl)
            {
                m_lTtl = a_lTtl;
            }

            /// <summary>
            /// Set the array of TXT records...
            /// </summary>
            /// <param name="a_aszTxt">set the array of TXT records</param>
            public void SetTxt(string[] a_aszTxt)
            {
                int ii;

                // No data...
                if ((a_aszTxt == null) || (a_aszTxt.Length == 0))
                {
                    m_aszTxt = null;
                    return;
                }

                // Make a copy...
                m_aszTxt = new string[a_aszTxt.Length];
                for (ii = 0; ii < a_aszTxt.Length; ii++)
                {
                    m_aszTxt[ii] = a_aszTxt[ii];
                }
            }

            /// <summary>
            /// Set our cloud status...
            /// </summary>
            /// <param name="a_szTxtCs">cloud status</param>
            public void SetTxtCs(string a_szTxtCs)
            {
                m_szTxtCs = a_szTxtCs;
            }

            /// <summary>
            /// Set the HTTPS flag...
            /// </summary>
            /// <param name="a_blTxtHttps">https flag</param>
            public void SetTxtHttps(bool a_blTxtHttps)
            {
                m_blTxtHttps = a_blTxtHttps;
            }

            /// <summary>
            /// Set our text id, cloud id, empty if one isn't available...
            /// </summary>
            /// <param name="a_szTxtId">text id, cloud id, empty if one isn't available</param>
            public void SetTxtId(string a_szTxtId)
            {
                m_szTxtId = a_szTxtId;
            }

            /// <summary>
            /// Set a note about the device...
            /// </summary>
            /// <param name="a_szTxtNote">a note about the device</param>
            public void SetTxtNote(string a_szTxtNote)
            {
                m_szTxtNote = a_szTxtNote;
            }

            /// <summary>
            /// Set our TXT verison...
            /// </summary>
            /// <param name="a_szTxtTxtvers">our TXT version</param>
            public void SetTxtTxtvers(string a_szTxtTxtvers)
            {
                m_szTxtTxtvers = a_szTxtTxtvers;
            }

            /// <summary>
            /// Set the friendly name for the scanner...
            /// </summary>
            /// <param name="a_szTxtTy">friendly name for the scanner</param>
            public void SetTxtTy(string a_szTxtTy)
            {
                m_szTxtTy = a_szTxtTy;
            }

            /// <summary>
            /// Set the text type, comma separated services supported by the device...
            /// </summary>
            /// <param name="a_szTxtType">text type, comma separated services supported by the device</param>
            public void SetTxtType(string a_szTxtType)
            {
                m_szTxtType = a_szTxtType;
            }

            #endregion


            // Private Attributes
            #region Private Attributes

            /// <summary>
            /// The flags reported with it...
            /// </summary>
            private long m_lFlags;

            /// <summary>
            /// The interface it lives on...
            /// </summary>
            private long m_lInterface;

            /// <summary>
            /// The IPv4 address (if any)...
            /// </summary>
            private string m_szIpv4;

            /// <summary>
            /// The IPv6 address (if any)...
            /// </summary>
            private string m_szIpv6;

            /// <summary>
            /// The link local name for where the service is running...
            /// </summary>
            private string m_szLinkLocal;

            /// <summary>
            /// The port to use...
            /// </summary>
            private long m_lPort;

            /// <summary>
            /// The full, unique name of the device...
            /// </summary>
            private string m_szServiceName;

            /// <summary>
            /// Our time to live...
            /// </summary>
            private long m_lTtl;

            /// <summary>
            /// The array of TXT records...
            /// </summary>
            private string[] m_aszTxt;

            /// <summary>
            /// Text cs, cloud status...
            /// </summary>
            private string m_szTxtCs;

            /// <summary>
            /// true if the scanner wants us to use HTTPS...
            /// </summary>
            private bool m_blTxtHttps;

            /// <summary>
            /// Text id, cloud id, empty if one isn't available...
            /// </summary>
            private string m_szTxtId;

            /// <summary>
            /// Text note, optional, a note about the device, like its location...
            /// </summary>
            private string m_szTxtNote;

            /// <summary>
            /// Our TXT version...
            /// </summary>
            private string m_szTxtTxtvers;

            /// <summary>
            /// Text ty, friendly name for the scanner...
            /// </summary>
            private string m_szTxtTy;

            /// <summary>
            /// Text type, comma separated services supported by the device...
            /// </summary>
            private string m_szTxtType;

            #endregion
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
                if (m_threadMonitor != null)
                {
                    // Clean up Bonjour. This is not strictly necessary since the normal process cleanup will 
                    // close Bonjour socket(s) and release memory, but it's here to demonstrate how to do it.
                    if (m_dnsservicerefClient != IntPtr.Zero)
                    {
                        NativeMethods.WSAAsyncSelect(m_pfndnsservicerefsockfd(m_dnsservicerefClient), m_hwnd, NativeMethods.BONJOUR_EVENT, 0);
                        m_pfndnsservicerefdeallocate(m_dnsservicerefClient);
                        m_dnsservicerefClient = IntPtr.Zero;
                    }
                    NativeMethods.PostMessage(m_hwnd, 18, IntPtr.Zero, IntPtr.Zero); // WM_QUIT
                    if (!m_threadMonitor.Join(5000))
                    {
                        m_threadMonitor.Abort();
                    }
                    m_threadMonitor = null;
                }
                if (m_dnsservicerefRegister != IntPtr.Zero)
                {
                    m_pfndnsservicerefdeallocate(m_dnsservicerefRegister);
                    m_dnsservicerefRegister = IntPtr.Zero;
                }
                if (m_hmoduleDnssd != IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(m_hmoduleDnssd);
                    m_hmoduleDnssd = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// A helper function to flip unsigned shorts from network order
        /// to something that's actually useful...
        /// </summary>
        /// <param name="a">the value to flip</param>
        /// <returns>the flipped value</returns>
        internal UInt16 WX(UInt16 a)
        {
            return ((UInt16)((((a) & 0xFF) << 8) + (((a) >> 8) & 0xFF)));
        }

        /// <summary>
        /// This is the launchpad for WndProc.  It's main claim to fame is
        /// using the user data region to pass the Dnssd object into the
        /// callback...
        /// </summary>
        /// <param name="a_hwnd"></param>
        /// <param name="a_uiMsg"></param>
        /// <param name="a_wparam"></param>
        /// <param name="a_lparam"></param>
        /// <returns></returns>
        internal IntPtr WndProcLaunchpad
        (
            IntPtr a_hwnd,
            int a_iMsg,
            IntPtr a_wparam,
            IntPtr a_lparam
        )
        {
            Dnssd dnssd;
            GCHandle gchandle;
            IntPtr intptrGchandle;

            // Get our user defined data...
            if (Config.GetMachineWordSize() == 32)
            {
                intptrGchandle = (IntPtr)NativeMethods.GetWindowLong(a_hwnd, -21); // GWL_USERDATA
            }
            else
            {
                intptrGchandle = (IntPtr)NativeMethods.GetWindowLongPtr(a_hwnd, -21); // GWL_USERDATA
            }

            // If we don't have our object, then scoot...
            if (intptrGchandle == IntPtr.Zero)
            {
                return (NativeMethods.DefWindowProc(a_hwnd, a_iMsg, a_wparam, a_lparam));
            }

            // Otherwise we can pass this to our handler...
            gchandle = GCHandle.FromIntPtr(intptrGchandle);
            dnssd = (gchandle.Target as Dnssd);

            // Dispatch it...
            return (dnssd.WndProc(a_hwnd, a_iMsg, a_wparam, a_lparam));
        }

        /// <summary>
        /// Bonjour on Windows is a happier puppy when it has a window it can
        /// send messages to.  This is the launchpad for that.  When we get
        /// something we process the result, and that kicks off the brower
        /// callback function...
        /// </summary>
        /// <param name="a_hwnd"></param>
        /// <param name="a_uiMsg"></param>
        /// <param name="a_wparam"></param>
        /// <param name="a_lparam"></param>
        /// <returns></returns>
        internal IntPtr WndProc
        (
            IntPtr a_hwnd,
            int a_iMsg,
            IntPtr a_wparam,
            IntPtr a_lparam
        )
        {
            Int32 dnsserviceerrortype;

            // We shouldn't be here...
            if (m_hmoduleDnssd == null)
            {
                return (NativeMethods.DefWindowProc(a_hwnd, a_iMsg, a_wparam, a_lparam));
            }

            // Dispatch the message...
            switch (a_iMsg)
            {
                default:
                    return (NativeMethods.DefWindowProc(a_hwnd, a_iMsg, a_wparam, a_lparam));

                case NativeMethods.BONJOUR_EVENT:
                    // Process the Bonjour event. All Bonjour callbacks occur from within this function.
                    // If an error occurs while trying to process the result, it most likely means that
                    // something serious has gone wrong with Bonjour, such as it being terminated. This 
                    // does not normally occur, but code should be prepared to handle it. If the error 
                    // is ignored, the window will receive a constant stream of BONJOUR_EVENT messages so
                    // if an error occurs, we disassociate the DNSServiceRef from the window, deallocate
                    // it, and invalidate the reference so we don't try to deallocate it again on quit. 
                    // Since this is a simple example app, if this error occurs, we quit the app too.
                    try
                    {
                        dnsserviceerrortype = 0;
                        dnsserviceerrortype = m_pfndnsserviceprocessresult(m_dnsservicerefClient);
                        if (dnsserviceerrortype == 0) // kDNSServiceErr_NoError
                        {
                            // Do the callback...
                        }
                        else
                        {
                            Log.Error("bonjour error..." + dnsserviceerrortype);
                            NativeMethods.WSAAsyncSelect(m_pfndnsservicerefsockfd(m_dnsservicerefClient), a_hwnd, NativeMethods.BONJOUR_EVENT, 0);
                            m_pfndnsservicerefdeallocate(m_dnsservicerefClient);
                            m_dnsservicerefClient = IntPtr.Zero;
                            NativeMethods.PostMessage(a_hwnd, 18, IntPtr.Zero, IntPtr.Zero); // WM_QUIT
                        }
                    }
                    catch (Exception exception)
                    {
                        Log.Error("bonjour exception..." + exception.Message);
                    }

                    // All done...
                    return (IntPtr.Zero);
            }
        }

        /// <summary>
        /// The launchpad function for BrowserCallBack, coming in from WndProc...
        /// </summary>
        /// <param name="a_dnsserviceref"></param>
        /// <param name="a_dnsserviceflags"></param>
        /// <param name="a_i32Interface"></param>
        /// <param name="a_dnsserviceerrortype"></param>
        /// <param name="a_szName"></param>
        /// <param name="a_szType"></param>
        /// <param name="a_szDomain"></param>
        /// <param name="a_pvContext"></param>
        internal void BrowserCallBackLaunchpad
        (
	        IntPtr a_dnsserviceref,
	        Int32 a_dnsserviceflags,
	        Int32 a_i32Interface,
	        Int32 a_dnsserviceerrortype,
	        string a_szName,
            string a_szType,
            string a_szDomain,
	        IntPtr a_pvContext
        )
        {
            GCHandle gchandle = GCHandle.FromIntPtr(a_pvContext);
            Dnssd dnssd = (gchandle.Target as Dnssd);
            dnssd.BrowserCallBack
	        (
		        ref a_dnsserviceref,
		        a_dnsserviceflags,
		        a_i32Interface,
		        a_dnsserviceerrortype,
		        a_szName,
		        a_szType,
		        a_szDomain,
		        a_pvContext
	        );
        }

        /// <summary>
        /// The browser callback.  This is the point hit when new stuff is
        /// coming and going.  We're filtering for _privet._tcp,_twaindirect,
        /// so we shouldn't se anything else.  If we see that an item is
        /// leaving, we try to yank it from our cache.  If we see that an item
        /// is coming in, then we ask the service to resolve it to get more
        /// info...
        /// </summary>
        /// <param name="a_dnsserviceref"></param>
        /// <param name="a_dnsserviceflags"></param>
        /// <param name="a_i32Interface"></param>
        /// <param name="a_dnsserviceerrortype"></param>
        /// <param name="a_szName"></param>
        /// <param name="a_szType"></param>
        /// <param name="a_szDomain"></param>
        /// <param name="a_pvContext"></param>
        internal void BrowserCallBack
        (
            ref IntPtr a_dnsserviceref,
            Int32 a_dnsserviceflags,
            Int32 a_i32Interface,
            Int32 a_dnsserviceerrortype,
            string a_szName,
            string a_szType,
            string a_szDomain,
            IntPtr a_pvContext
        )
        {
            int ii;
	        Int32 dnsserviceerrortype;

	        // Ruh-roh...
	        if (a_dnsserviceerrortype != 0) // kDNSServiceErr_NoError
	        {
		        Log.Error("BrowserCallback failed..." + a_dnsserviceerrortype);
		        return;
	        }

	        // We're removing something...
	        if ((a_dnsserviceflags & 0x2) == 0) // kDNSServiceFlagsAdd
	        {
                // We have enough information to find our beastie, we just
                // need the link service name and the interface it's on...
                lock (m_objectLockCache)
                {
                    if (m_adnssddeviceinfoCache != null)
                    {
                        for (ii = 0; ii < m_adnssddeviceinfoCache.Length; ii++)
                        {
                            if (    (m_adnssddeviceinfoCache[ii].GetInterface() == a_i32Interface)
                                &&  (m_adnssddeviceinfoCache[ii].GetServiceName() == (a_szName + "." + a_szType + a_szDomain)))
                            {
                                // Last item...
                                if (m_adnssddeviceinfoCache.Length == 1)
                                {
                                    m_adnssddeviceinfoCache = null;
                                }
                                else
                                {
                                    DnssdDeviceInfo[] adnssddeviceinfo = new DnssdDeviceInfo[m_adnssddeviceinfoCache.Length - 1];
                                    // Drop the first item...
                                    if (ii == 0)
                                    {
                                        Array.Copy(m_adnssddeviceinfoCache, 1, adnssddeviceinfo, 0, m_adnssddeviceinfoCache.Length - 1);
                                    }
                                    // Drop the last item...
                                    else if (ii == (m_adnssddeviceinfoCache.Length - 1))
                                    {
                                        Array.Copy(m_adnssddeviceinfoCache, 0, adnssddeviceinfo, 0, m_adnssddeviceinfoCache.Length - 1);
                                    }
                                    // Drop a middle item...
                                    else
                                    {
                                        // 0 1 2 3 4 5
                                        // . . . i . .
                                        Array.Copy(m_adnssddeviceinfoCache, 0, adnssddeviceinfo, 0, ii); // ex: s[0] -> d[0] : 3
                                        Array.Copy(m_adnssddeviceinfoCache, ii + 1, adnssddeviceinfo, ii, m_adnssddeviceinfoCache.Length - (ii + 1)); // ex: s[4] -> d[3] : 2
                                    }
                                    m_adnssddeviceinfoCache = adnssddeviceinfo;
                                }
                                break;
                            }
                        }
                    }
                }
		        return;
	        }

            // Otherwise, we're adding something...

            // Build a callback context...
            IntPtr pcallbackcontext = NativeMethods.calloc((IntPtr)1, (IntPtr)Marshal.SizeOf(typeof(CallbackContext)));
            if (pcallbackcontext == IntPtr.Zero)
	        {
                Log.Error("NativeMethods.calloc failed...");
                //NativeMethods.free(pdnsserviceref);
                return;
	        }
            CallbackContext callbackcontext;
            callbackcontext.dnssddeviceinfo = new DnssdDeviceInfo();
            GCHandle gchandle = GCHandle.Alloc(this);
            callbackcontext.dnssd = GCHandle.ToIntPtr(gchandle);
            callbackcontext.dnssddeviceinfo.SetInterface(a_i32Interface);
            callbackcontext.dnssddeviceinfo.SetServiceName(a_szName + "." + a_szType + a_szDomain);
            Marshal.StructureToPtr(callbackcontext, pcallbackcontext, false);

            // Resolve the rest of the info...
            DNSServiceResolveReply dnsserviceresolvereply = ResolveCallbackLaunchpad;
            IntPtr dnsservicerefTmp = m_dnsservicerefClient;
            dnsserviceerrortype = m_pfndnsserviceresolve
	        (
                ref dnsservicerefTmp,
		        0x4000, // kDNSServiceFlagsShareConnection
                a_i32Interface,
		        a_szName,
		        a_szType,
		        a_szDomain,
                dnsserviceresolvereply,
		        pcallbackcontext
	        );
            if (dnsserviceerrortype != 0) // kDNSServiceErr_NoError
            {
                Log.Error("m_pfndnsserviceresolve failed..." + dnsserviceerrortype);
            }
        }

        /// <summary>
        /// The launchpad for ResolveCallback.  This is coming in from a call
        /// we made in the brower callback...
        /// </summary>
        /// <param name="a_dnsserviceref"></param>
        /// <param name="a_dnsserviceflags"></param>
        /// <param name="a_i32Interface"></param>
        /// <param name="a_dnsserviceerrortype"></param>
        /// <param name="a_szFullname"></param>
        /// <param name="a_szHosttarget"></param>
        /// <param name="a_u16Opaqueport"></param>
        /// <param name="a_u16Txtlen"></param>
        /// <param name="a_pu8Txt"></param>
        /// <param name="a_pvContext"></param>
        internal void ResolveCallbackLaunchpad
        (
            IntPtr a_dnsserviceref,
            Int32 a_dnsserviceflags,
            Int32 a_i32Interface,
            Int32 a_dnsserviceerrortype,
            string a_szFullname,
            string a_szHosttarget,
	        Int16 a_u16Opaqueport,
            Int16 a_u16Txtlen,
	        string a_pu8Txt,
	        IntPtr a_pvContext
        )
        {
            CallbackContext callbackcontext = (CallbackContext)Marshal.PtrToStructure(a_pvContext, typeof(CallbackContext));
            GCHandle gchandle = GCHandle.FromIntPtr(callbackcontext.dnssd);
            Dnssd dnssd = (gchandle.Target as Dnssd);
            dnssd.ResolveCallback
	        (
		        ref a_dnsserviceref,
		        a_dnsserviceflags,
		        a_i32Interface,
		        a_dnsserviceerrortype,
		        a_szFullname,
		        a_szHosttarget,
                unchecked((ushort)a_u16Opaqueport),
                unchecked((ushort)a_u16Txtlen),
		        a_pu8Txt,
		        a_pvContext
	        );
        }

        /// <summary>
        /// We collect most of what we need in this function.  But, of course,
        /// we don't get everything we want, so we do a query to get the IPv4
        /// and IPv4 data...
        /// </summary>
        /// <param name="a_dnsserviceref"></param>
        /// <param name="a_dnsserviceflags"></param>
        /// <param name="a_i32Interface"></param>
        /// <param name="a_dnsserviceerrortype"></param>
        /// <param name="a_szFullname"></param>
        /// <param name="a_szHosttarget"></param>
        /// <param name="a_u16Opaqueport"></param>
        /// <param name="a_u16Txtlen"></param>
        /// <param name="a_pu8Txt"></param>
        /// <param name="a_pvContext"></param>
        private void ResolveCallback
        (
            ref IntPtr a_dnsserviceref,
            Int32 a_dnsserviceflags,
            Int32 a_i32Interface,
            Int32 a_dnsserviceerrortype,
            string a_szFullname,
            string a_szHosttarget,
            UInt16 a_u16Opaqueport,
            UInt16 a_u16Txtlen,
            string a_pu8Txt,
            IntPtr a_pvContext
        )
        {
	        int oo;
	        int jj;
	        UInt16 u16Opaqueport;
	        Int32 dnsserviceerrortype;
            CallbackContext callbackcontext = (CallbackContext)Marshal.PtrToStructure(a_pvContext, typeof(CallbackContext));
	        IntPtr pcallbackcontextQuery;
            DNSServiceQueryRecordReply dnsservicequeryrecordreply;

            // Grab simple stuff...
            // Apparently bonjour gives us stuff in network (big endian) order...
            u16Opaqueport = WX(a_u16Opaqueport);
            callbackcontext.dnssddeviceinfo.SetInterface(a_i32Interface);
            callbackcontext.dnssddeviceinfo.SetPort(u16Opaqueport);
            callbackcontext.dnssddeviceinfo.SetLinkLocal(a_szHosttarget);

            // Handle the text data...
            // TXT stuff...
            bool blHttpsFound = false;
            for (oo = jj = 0; jj < a_u16Txtlen; jj += a_pu8Txt[jj] + 1)
	        {
		        // Skip empty stuff...
		        if (a_pu8Txt[jj] == 0)
		        {
			        continue;
		        }

                // Add this to our TXT array...
                string szTxt = a_pu8Txt.Substring(jj + 1, a_pu8Txt[jj]);
                callbackcontext.dnssddeviceinfo.AddTxt(szTxt);

                // txtvers...
                if (szTxt.StartsWith("txtvers="))
                {
                    callbackcontext.dnssddeviceinfo.SetTxtTxtvers(szTxt.Remove(0,8));
                }

                // ty...
                else if (szTxt.StartsWith("ty="))
                {
                    callbackcontext.dnssddeviceinfo.SetTxtTy(szTxt.Remove(0, 3));
                }

                // type...
                else if (szTxt.StartsWith("type="))
                {
                    callbackcontext.dnssddeviceinfo.SetTxtType(szTxt.Remove(0, 5));
                }

                // id...
                else if (szTxt.StartsWith("id="))
                {
                    callbackcontext.dnssddeviceinfo.SetTxtId(szTxt.Remove(0, 3));
                }

                // cs...
                else if (szTxt.StartsWith("cs="))
                {
                    callbackcontext.dnssddeviceinfo.SetTxtCs(szTxt.Remove(0, 3));
                }

                // https...
                else if (szTxt.StartsWith("https="))
                {
                    callbackcontext.dnssddeviceinfo.SetTxtHttps(szTxt.Remove(0, 6) != "0");
                    blHttpsFound = true;
                }

                // note...
                else if (szTxt.StartsWith("note="))
                {
                    callbackcontext.dnssddeviceinfo.SetTxtNote(szTxt.Remove(0, 5));
                }

                // Point to the next spot for data...
                oo += szTxt.Length + 1;
            }

            // If we're missing anything critical, we're done, note that
            // note= is optional in the spec...
            if (    (string.IsNullOrEmpty(callbackcontext.dnssddeviceinfo.GetTxtTxtvers()) || (callbackcontext.dnssddeviceinfo.GetTxtTxtvers() != "1"))
                ||  (callbackcontext.dnssddeviceinfo.GetTxtTy() == null)
                ||  (string.IsNullOrEmpty(callbackcontext.dnssddeviceinfo.GetTxtType()) || !callbackcontext.dnssddeviceinfo.GetTxtType().Contains("twaindirect"))
                ||  (callbackcontext.dnssddeviceinfo.GetTxtId() == null)
                ||  (callbackcontext.dnssddeviceinfo.GetTxtCs() == null)
                ||  !blHttpsFound)
            {
		        goto ABORT;
	        }

            // Query for our IPv4 address...
            if (Config.Get("useIpv4", "yes") == "yes")
            {
                // Build a callback context...
                pcallbackcontextQuery = NativeMethods.calloc((IntPtr)1, (IntPtr)Marshal.SizeOf(typeof(CallbackContext)));
                if (pcallbackcontextQuery == IntPtr.Zero)
                {
                    Log.Error("calloc failed...");
                    goto ABORT;
                }
                Marshal.StructureToPtr(callbackcontext, pcallbackcontextQuery, false);

                // Make the query...
                dnsservicequeryrecordreply = QueryCallbackLaunchpad;
                IntPtr dnsservicerefTmp = m_dnsservicerefClient;
                dnsserviceerrortype = m_pfndnsservicequeryrecord
                (
                    ref dnsservicerefTmp,
                    0x4000, // kDNSServiceFlagsShareConnection
                    a_i32Interface, // kDNSServiceInterfaceIndexAny,
                    callbackcontext.dnssddeviceinfo.GetLinkLocal(),
                    1, // kDNSServiceType_A
                    1, // kDNSServiceClass_IN
                    dnsservicequeryrecordreply,
                    pcallbackcontextQuery
                );
                if (dnsserviceerrortype != 0) // kDNSServiceErr_NoError
                {
                    Log.Error("m_pfndnsservicequeryrecord failed..." + dnsserviceerrortype);
                }
            }

            // Query for our IPv6 address...
            if (Config.Get("useIpv6", "yes") == "yes")
            {
                // Build a callback context...
                pcallbackcontextQuery = NativeMethods.calloc((IntPtr)1, (IntPtr)Marshal.SizeOf(typeof(CallbackContext)));
                if (pcallbackcontextQuery == IntPtr.Zero)
                {
                    Log.Error("calloc failed...");
                    goto ABORT;
                }
                Marshal.StructureToPtr(callbackcontext, pcallbackcontextQuery, false);

                // Make the query...
                dnsservicequeryrecordreply = QueryCallbackLaunchpad;
                IntPtr dnsservicerefTmp = m_dnsservicerefClient;
                dnsserviceerrortype = m_pfndnsservicequeryrecord
                (
                    ref dnsservicerefTmp,
                    0x4000, // kDNSServiceFlagsShareConnection
                    a_i32Interface, // kDNSServiceInterfaceIndexAny,
                    callbackcontext.dnssddeviceinfo.GetLinkLocal(),
                    28, // kDNSServiceType_AAAA
                    1, // kDNSServiceClass_IN
                    dnsservicequeryrecordreply,
                    pcallbackcontextQuery
                );
                if (dnsserviceerrortype != 0) // kDNSServiceErr_NoError
                {
                    Log.Error("m_pfndnsservicequeryrecord failed..." + dnsserviceerrortype);
                }
            }

            // Cleanup...
            ABORT:
                NativeMethods.free(a_pvContext);

	        // All done...
	        return;
        }

        ////////////////////////////////////////////////////////////////////////////////
        //	Description:
        //		Process a query record...
        ////////////////////////////////////////////////////////////////////////////////
        private void QueryCallbackLaunchpad
        (
	        IntPtr a_dnsserviceref,
	        Int32 a_dnsserviceflags,
	        Int32 a_u32Interface,
	        Int32 a_dnsserviceerrortype,
	        string a_szFullname,
	        Int16 a_u16Rrtype,
            Int16 a_u16Rrclass,
            Int16 a_u16Rdlen,
	        IntPtr a_pvRdata,
	        Int32 a_u32Ttl,
	        IntPtr a_pvContext
        )
        {
            // Don't go any further if we see a problem...
            if (a_dnsserviceerrortype != 0) // kDNSServiceErr_NoError
            {
                return;
            }

            // We're only handling A and AAAA, anything else is going to weird us out...
            if (    (a_u16Rrtype != 1)      // kDNSServiceType_A
                &&  (a_u16Rrtype != 28))    // kDNSServiceType_AAAA
            {
                return;
            }

            // It should be safe to continue, I have seen crashes when trying
            // to dereference this pointer, but only with AAAA records.  So if
            // this is blowing up, set useIpv6 to "no" in appdata.txt and see
            // if that resolves it...
            CallbackContext callbackcontext = (CallbackContext)Marshal.PtrToStructure(a_pvContext, typeof(CallbackContext));
            GCHandle gchandle = GCHandle.FromIntPtr(callbackcontext.dnssd);
            Dnssd dnssd = (gchandle.Target as Dnssd);
            dnssd.QueryCallback
	        (
                a_dnsserviceref,
                a_dnsserviceflags,
                a_u32Interface,
                a_dnsserviceerrortype,
                a_szFullname,
                unchecked((ushort)a_u16Rrtype),
                unchecked((ushort)a_u16Rrclass),
                unchecked((ushort)a_u16Rdlen),
                a_pvRdata,
                unchecked((ushort)a_u32Ttl),
                a_pvContext
            );
        }
        private void QueryCallback
        (
            IntPtr a_dnsserviceref,
            Int32 a_dnsserviceflags,
            Int32 a_i32Interface,
            Int32 a_dnsserviceerrortype,
            string a_szFullname,
            UInt16 a_u16Rrtype,
            UInt16 a_u16Rrclass,
            UInt16 a_u16Rdlen,
            IntPtr a_pvRdata,
            UInt32 a_u32Ttl,
            IntPtr a_pvContext
        )
        {
            int ii;
            byte[] abIpv6;
            UInt32 u32Ipv4;
            CallbackContext callbackcontext = (CallbackContext)Marshal.PtrToStructure(a_pvContext, typeof(CallbackContext));

	        // We're leaving...
	        if (a_u32Ttl == 0)
	        {
		        return;
	        }

	        // Dispatch the record type...
	        switch (a_u16Rrtype)
	        {
		        default:
			        // Ignore it...
			        goto ABORT;

                case 1: // kDNSServiceType_A
                    u32Ipv4 = (UInt32)Marshal.PtrToStructure(a_pvRdata, typeof(UInt32));
                    callbackcontext.dnssddeviceinfo.SetIpv4(new IPAddress(u32Ipv4).ToString());
                    callbackcontext.dnssddeviceinfo.SetTtl(a_u32Ttl);
                    break;

                case 28: // kDNSServiceType_AAAA
                    abIpv6 = new byte[a_u16Rdlen];
                    Marshal.Copy(a_pvRdata, abIpv6, 0, a_u16Rdlen);
                    callbackcontext.dnssddeviceinfo.SetIpv6(new IPAddress(abIpv6).ToString());
                    callbackcontext.dnssddeviceinfo.SetTtl(a_u32Ttl);
                    break;
	        }

            // Okay, we have enough info, add the beastie to the list...
            lock (m_objectLockCache)
            {
                // First item in the list...
                if (m_adnssddeviceinfoCache == null)
                {
                    m_adnssddeviceinfoCache = new DnssdDeviceInfo[1];
                    m_adnssddeviceinfoCache[0] = callbackcontext.dnssddeviceinfo;
                }

                // Check if we already have this item...
                for (ii = 0; ii < m_adnssddeviceinfoCache.Length; ii++)
                {
                    if (    (m_adnssddeviceinfoCache[ii].GetInterface() == a_i32Interface)
                        &&  (m_adnssddeviceinfoCache[ii].GetLinkLocal() == a_szFullname))
                    {
                        break;
                    }
                }

                // Yes we do, so see about updating it...
                if (ii < m_adnssddeviceinfoCache.Length)
                {
                    switch (a_u16Rrtype)
                    {
                        default:
                            // Ignore it...
                            goto ABORT;

                        case 1: // kDNSServiceType_A
                            m_adnssddeviceinfoCache[ii].SetIpv4(callbackcontext.dnssddeviceinfo.GetIpv4());
                            m_adnssddeviceinfoCache[ii].SetTtl(callbackcontext.dnssddeviceinfo.GetTtl());
                            break;

                        case 28: // kDNSServiceType_AAAA
                            m_adnssddeviceinfoCache[ii].SetIpv6(callbackcontext.dnssddeviceinfo.GetIpv6());
                            m_adnssddeviceinfoCache[ii].SetTtl(callbackcontext.dnssddeviceinfo.GetTtl());
                            break;
                    }
                }

                // No we don't, so add it to our list...
                else
                {
                    DnssdDeviceInfo[] adnssddeviceinfo = new DnssdDeviceInfo[m_adnssddeviceinfoCache.Length + 1];
                    m_adnssddeviceinfoCache.CopyTo(adnssddeviceinfo, 0);
                    adnssddeviceinfo[m_adnssddeviceinfoCache.Length] = callbackcontext.dnssddeviceinfo;
                    m_adnssddeviceinfoCache = adnssddeviceinfo;
                    Array.Sort(m_adnssddeviceinfoCache);
                }
            }

            // Cleanup...
            ABORT:
                //Freeing this memory is causing a crash on some systems...
                //NativeMethods.free(a_pvContext);

            // All done...
            return;
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
        /// Our cache of devices currently on the LAN, this is the
        /// official cache that can be referenced at any given time...
        /// </summary>
        private DnssdDeviceInfo[] m_adnssddeviceinfoCache;

        /// <summary>
        /// Object used to lock the cache...
        /// </summary>
        private object m_objectLockCache;

        struct CallbackContext
        {
	        public IntPtr dnssd;
            public DnssdDeviceInfo dnssddeviceinfo;
        };

        // The callback...
        public delegate int OsDnssdCallback(IntPtr a_pvArg, DnssdDeviceInfo a_dnssddeviceinfo);

        // The callback...
        OsDnssdCallback m_osdnssdcallback;
        IntPtr m_intptrOsdnssdcallbackArg;

        // Our thread...
        Thread m_threadMonitor;

        // The Bonjour functions...
        readonly pfnDNSServiceBrowse m_pfndnsservicebrowse;
        readonly pfnDNSServiceCreateConnection m_pfndnsserrvicecreateconnection;
        readonly pfnDNSServiceProcessResult m_pfndnsserviceprocessresult;
        readonly pfnDNSServiceRefDeallocate m_pfndnsservicerefdeallocate;
        readonly pfnDNSServiceRefSockFD m_pfndnsservicerefsockfd;
        readonly pfnDNSServiceResolve m_pfndnsserviceresolve;
        readonly pfnDNSServiceQueryRecord m_pfndnsservicequeryrecord;
        readonly pfnDNSServiceRegister m_pfndnsserviceregister;

        // Windows stuff...
        IntPtr m_hmoduleDnssd;
	    IntPtr m_dnsservicerefClient;
        IntPtr m_dnsservicerefRegister;
        IntPtr m_hwnd;

        // Linux stuff...
        // nothing at this time...

        // osMac stuff...
        // nothing at this time...

        /// <summary>
        /// Function and callback for browse...
        /// </summary>
        /// <param name="sdRef"></param>
        /// <param name="flags"></param>
        /// <param name="interfaceIndex"></param>
        /// <param name="regtype"></param>
        /// <param name="domain"></param>
        /// <param name="callBack"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate Int32 pfnDNSServiceBrowse
        (
            ref IntPtr sdRef,
            Int32 flags,
            Int32 interfaceIndex,
            string regtype,
            string domain,      // may be NULL
            DNSServiceBrowseReply callBack,
            IntPtr context      // may be NULL
        );
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DNSServiceBrowseReply
        (
            IntPtr sdRef,
            Int32 flags,
            Int32 interfaceIndex,
            Int32 errorCode,
            string serviceName,
            string regtype,
            string replyDomain,
            IntPtr context
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate Int32 pfnDNSServiceCreateConnection
        (
            ref IntPtr sdRef
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate Int32 pfnDNSServiceProcessResult
        (
            IntPtr sdRef
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void pfnDNSServiceRefDeallocate
        (
            IntPtr sdRef
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr pfnDNSServiceRefSockFD
        (
            IntPtr sdRef
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate Int32 pfnDNSServiceResolve
        (
            ref IntPtr sdRef,
            Int32 flags,
            Int32 interfaceIndex,
            string name,
            string regtype,
            string domain,      // may be NULL
            DNSServiceResolveReply callBack,
            IntPtr context      // may be NULL
        );
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DNSServiceResolveReply
        (
            IntPtr a_dnsserviceref,
            Int32 a_dnsserviceflags,
            Int32 a_i32Interface,
            Int32 a_dnsserviceerrortype,
            string a_szFullname,
            string a_szHosttarget,
            Int16 a_u16Opaqueport,
            Int16 a_u16Txtlen,
            string a_pu8Txt,
            IntPtr a_pvContext
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate Int32 pfnDNSServiceQueryRecord
        (
            ref IntPtr sdRef,
            Int32 flags,
            Int32 interfaceIndex,
            string fullname,
            Int16 rrtype,
            Int16 rrclass,
            DNSServiceQueryRecordReply callBack,
            IntPtr context
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DNSServiceQueryRecordReply
        (
            IntPtr sdRef,
            Int32 flags,
            Int32 interfaceIndex,
            Int32 errorCode,
            string fullname,
            Int16 rrtype,
            Int16 rrclass,
            Int16 rdlen,
            IntPtr rdata,
            Int32 ttl,
            IntPtr context
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate Int32 pfnDNSServiceRegister
        (
            ref IntPtr sdRef,
            Int32 flags,
            Int32 interfaceIndex,
            string name,
            string regtype,
            string domain,
            string host,
            Int16 port,
            Int16 txtLen,
            byte[] txtRecord,
            IntPtr callBack,
            IntPtr context
        );

        #endregion
    }
}
