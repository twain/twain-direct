///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirectSupport.TwainLocalScanner
// TwainDirectSupport.TwainLocalScanner.TwainLocalSession
//
// Interface to TWAIN Local scanners scanners.  This class is used by applications
// and scanners, since they share enough common features to make it worthwhile to
// consolodate the functionality.  Hopefully, it also helps to make things a little
// more clear as to what's going on.
//
// Functions that are used by applications are marked as "Client" and functions
// that are used by scanners are marked as "Device".
//
// There's no obvious reason to expose the Session class, so it's buried inside
// of TwainLocalScanner.
//
// ApiCmd is the payload for a ApiCmd command.  We must support multi-threading, so
// we need to be able to pass its objects up and down the stack.  This is why it's
// publically accessible.
//
// It is a central tenet of this class that communication with the device does
// not occur, unless the client believes that communication is warrented.  Therefore
// we test state based on the client's understanding of where it currently is in the
// finite state machine.  This means that it's not impossible to get out of sync
// with the device (though it's unlikely), so we have to confirm in every command
// that we are actually where we expect to be.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    15-Oct-2016     Initial Release
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
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using TwainDirectSupport;
[assembly: CLSCompliant(true)]

// This namespace supports applications and scanners...
namespace TwainDirectSupport
{
    /// <summary>
    /// A scanner interface to TWAIN Local scanners....
    /// </summary>
    public sealed class TwainLocalScanner : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Common Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Common Methods...

        /// <summary>
        /// Init us...
        /// </summary>
        /// <param name="a_timercallbackEvent">event callback, which really isn't a timer when we use it this way</param>
        /// <param name="a_confirmscan">user must confirm a scan request</param>
        /// <param name="a_fConfirmScanScale">scale the confirmation dialog</param>
        /// <param name="a_objectEvent">object that provided the event</param>
        public TwainLocalScanner
        (
            TimerCallback a_timercallbackEvent,
            ConfirmScan a_confirmscan,
            float a_fConfirmScanScale,
            object a_objectEvent
        )
        {
            int iDefault;

            // Init our command timeout for HTTPS communication...
            iDefault = 10000;
            m_iHttpTimeoutCommand = (int)Config.Get("httpTimeoutCommand", iDefault);
            if (m_iHttpTimeoutCommand < iDefault)
            {
                m_iHttpTimeoutCommand = iDefault;
            }

            // Init our data timeout for HTTPS communication...
            iDefault = 30000;
            m_iHttpTimeoutData = (int)Config.Get("httpTimeoutData", iDefault);
            if (m_iHttpTimeoutData < iDefault)
            {
                m_iHttpTimeoutData = iDefault;
            }

            // Init our event timeout for HTTPS communication...
            iDefault = 30000;
            m_iHttpTimeoutEvent = (int)Config.Get("httpTimeoutEvent", iDefault);
            if (m_iHttpTimeoutEvent < iDefault)
            {
                m_iHttpTimeoutEvent = iDefault;
            }

            // Init stuff...
            m_szWriteFolder = Config.Get("writeFolder", "");
            m_timercallbackEvent = a_timercallbackEvent;
            m_objectEvent = a_objectEvent;
            m_confirmscan = a_confirmscan;
            m_fConfirmScanScale = a_fConfirmScanScale;

            // TWAIN Local stuff, we're initializing this content to items that are
            // specific to our project.  That includes our application code, the
            // application key for it, and the client and client secret codes.
            m_twainlocalsession = new TwainLocalSession(this, a_timercallbackEvent, a_objectEvent);

            // This is our default location for storing images...
            try
            {
                m_szImagesFolder = Path.Combine(m_szWriteFolder, "images");
                if (!Directory.Exists(m_szImagesFolder))
                {
                    Directory.CreateDirectory(m_szImagesFolder);
                }
            }
            catch
            {
                throw new Exception("Can't set up an images folder...");
            }
        }

        /// <summary>
        /// Destructor...
        /// </summary>
        ~TwainLocalScanner()
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
        /// Collect information for our device info...
        /// </summary>
        /// <returns></returns>
        Dnssd.DnssdDeviceInfo GetDnssdDeviceInfo()
        {
            Dnssd.DnssdDeviceInfo dnssddeviceinfo;

            // Create it...
            dnssddeviceinfo = new Dnssd.DnssdDeviceInfo();

            // Stock it...
            dnssddeviceinfo.szLinkLocal = "";
            dnssddeviceinfo.szServiceName = m_twainlocalsession.DeviceRegisterGetTwainLocalInstanceName();
            dnssddeviceinfo.szTxtCs = "offline";
            dnssddeviceinfo.blTxtHttps = true;
            dnssddeviceinfo.szTxtId = "";
            dnssddeviceinfo.szTxtNote = m_twainlocalsession.DeviceRegisterGetTwainLocalNote();
            dnssddeviceinfo.szTxtTxtvers = "1";
            dnssddeviceinfo.szTxtTy = m_twainlocalsession.DeviceRegisterGetTwainLocalTy();
            dnssddeviceinfo.szTxtType = "twaindirect";

            // Return it...
            return (dnssddeviceinfo);
        }

        /// <summary>
        /// Access the device name
        /// </summary>
        /// <returns>TWAIN Local ty= field</returns>
        public string GetTwainLocalTy()
        {
            return (m_twainlocalsession.DeviceRegisterGetTwainLocalTy());
        }

        /// <summary>
        /// Return the current images folder...
        /// </summary>
        /// <returns>the images folder</returns>
        public string GetImagesFolder()
        {
            return (m_szImagesFolder);
        }

        /// <summary>
        /// Returns a path for scratch pad use...
        /// </summary>
        /// <param name="a_szFile">an optional file or folder to add to the path</param>
        /// <returns>the path</returns>
        public string GetPath(string a_szFile)
        {
            string szPath = m_szWriteFolder;
            if (!string.IsNullOrEmpty(a_szFile))
            {
                szPath = Path.Combine(szPath, a_szFile);
            }
            if (!Directory.Exists(Path.GetDirectoryName(szPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(szPath));
            }
            return (szPath);
        }

        /// <summary>
        /// Quick access to our platform id.  We probably need a better way to
        /// figure this all out...
        /// </summary>
        /// <returns>a platform</returns>
        public static Platform GetPlatform()
        {
            // First pass...
            if (ms_platform == Platform.UNKNOWN)
            {
                // We're Windows...
                if (Environment.OSVersion.ToString().Contains("Microsoft Windows"))
                {
                    ms_platform = Platform.WINDOWS;
                }

                // We're Mac OS X (this has to come before LINUX!!!)...
                else if (Directory.Exists("/Library/Application Support"))
                {
                    ms_platform = Platform.MACOSX;
                }

                // We're Linux...
                else if (Environment.OSVersion.ToString().Contains("Unix"))
                {
                    ms_platform = Platform.LINUX;
                }

                // We have a problem...
                else
                {
                    ms_platform = Platform.UNKNOWN;
                    throw new Exception("Unsupported platform...");
                }
            }

            // This is it...
            return (ms_platform);
        }

        /// <summary>
        /// Set a new images folder...
        /// </summary>
        /// <param name="a_szImagesFolder">the folder to set</param>
        /// <returns>true on success</returns>
        public bool SetImagesFolder(string a_szImagesFolder)
        {
            // See if we can use it...
            if (!Directory.Exists(a_szImagesFolder))
            {
                try
                {
                    Directory.CreateDirectory(a_szImagesFolder);
                }
                catch (Exception exception)
                {
                    Log.Error("CreateDirectory failed: " + exception.Message);
                    Log.Error("<" + a_szImagesFolder + ">");
                    return (false);
                }
            }

            // We're good...
            m_szImagesFolder = a_szImagesFolder;
            return (true);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Client Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Client Methods...

        /// <summary>
        /// Return the current image blocks...
        /// </summary>
        /// <returns>array of image blocks</returns>
        public int[] ClientGetImageBlocks()
        {
            return (m_twainlocalsession.m_aiSessionImageBlocks);
        }

        /// <summary>
        /// Return the current image blocks...
        /// </summary>
        /// <returns>array of image blocks</returns>
        public string ClientGetImageBlocks(ApiCmd a_apicmd)
        {
            return (a_apicmd.GetImageBlocks().Replace(" ",""));
        }

        /// <summary>
        /// Get info about the device...
        /// </summary>
        /// <param name="a_dnssddeviceinfo">info about the device</param>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientInfo(Dnssd.DnssdDeviceInfo a_dnssddeviceinfo, ref ApiCmd a_apicmd)
        {
            JsonLookup jsonlookup;
            string szFunction = "ClientInfo";

            // This command can be issued at any time, so we don't check state, we also
            // don't have to worry about locking anything...

            // Squirrel this away...
            m_dnssddeviceinfo = a_dnssddeviceinfo;

            // Make the ApiCmd scan request...
            jsonlookup = ClientHttpRequest
            (
                szFunction,
                m_dnssddeviceinfo,
                ref a_apicmd,
                "/privet/info",
                "GET",
                new string[] {
                    "X-Privet-Token:"
                },
                null,
                null,
                null,
                m_iHttpTimeoutCommand,
                HttpReplyStyle.SimpleReplyWithSessionInfo
            );
            if (jsonlookup == null)
            {
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// This is the serial version of the scan loop.  It has the benefit of being
        /// fairly easy to understand and debug.  The downside is a lot of dead time
        /// on the net, so it's really slow.
        /// 
        /// Supposedly, XMPP will be available to the client side at some point, so
        /// that polling won't be needed anymore.
        /// </summary>
        /// <param name="a_apicmd">a command object</param>
        /// <param name="a_blStopCapturing">a flag if capturing has been stopped</param>
        /// <param name="a_blGetThumbnails">the caller would like thumbnails</param>
        /// <param name="a_blGetMetadataWithImage">skip the standalone metadata call</param>
        /// <param name="a_scancallbackImageBlockMetadataCallback">a callback for metadata</param>
        /// <param name="a_scancallbackImageBlockCallback">a callback for images</param>
        /// <param name="a_szError">a string if there's an error</param>
        /// <returns>true on success</returns>
        public bool ClientScan
        (
            ref ApiCmd a_apicmd,
            ref bool a_blStopCapturing,
            bool a_blGetThumbnails,
            bool a_blGetMetadataWithImage,
            ScanCallback a_scancallbackImageBlockMetadataCallback,
            ScanCallback a_scancallbackImageBlockCallback,
            out string a_szError
        )
        {
            string szImageBlocks;
            int[] aiImageBlocks;

            // It is a message that can be shown to the caller...
            a_szError = "";

            // Get a session object until we have an image...
            // TBD need better error handling here to avoid both hangs
            // and locking the scanneer forever...
            while (true)
            {
                // Get the session data...
                if (!ClientScannerGetSession(ref a_apicmd))
                {
                    Log.Error("ClientScannerGetSession failed: " + a_apicmd.HttpResponseData());
                    a_szError = "ClientScannerGetSession failed, the reason follows:\n\n" + a_apicmd.HttpResponseData();
                    return (false);
                }

                // If we have an image, then pop out...
                aiImageBlocks = ClientGetImageBlocks();
                if ((aiImageBlocks != null) && (aiImageBlocks.Length > 0))
                {
                    break;
                }

                // Wait a bit before trying again...
                Thread.Sleep(100);
            }

            // Loop on each image, until we exhaust the imageBlocks array...
            while (true)
            {
                // We have the option to skip getting the metadata with this
                // call.  An application should get metadata if it wants to
                // examine it before getting the image.  If it always wants
                // the image, it really doesn't need this step to be separate,
                // which will save us a round-trip on the network...
                if (!a_blGetMetadataWithImage)
                {
                    if (!ClientScannerReadImageBlockMetadata(aiImageBlocks[0], a_blGetThumbnails, a_scancallbackImageBlockMetadataCallback, ref a_apicmd))
                    {
                        Log.Error("ClientScannerReadImageBlockMetadata failed: " + a_apicmd.HttpResponseData());
                        a_szError = "ClientScannerReadImageBlockMetadata failed, the reason follows:\n\n" + a_apicmd.HttpResponseData();
                        return (false);
                    }
                }

                // Get the first image block in the array...
                if (!ClientScannerReadImageBlock(aiImageBlocks[0], a_blGetMetadataWithImage, a_scancallbackImageBlockCallback, ref a_apicmd))
                {
                    Log.Error("ClientScannerReadImageBlock failed: " + a_apicmd.HttpResponseData());
                    a_szError = "ClientScannerReadImageBlock failed, the reason follows:\n\n" + a_apicmd.HttpResponseData();
                    return (false);
                }

                // Release the image...
                if (!ClientScannerReleaseImageBlocks(aiImageBlocks[0], aiImageBlocks[0], ref a_apicmd))
                {
                    Log.Error("ClientScannerReleaseImageBlocks failed: " + a_apicmd.HttpResponseData());
                    a_szError = "ClientScannerReleaseImageBlocks failed, the reason follows:\n\n" + a_apicmd.HttpResponseData();
                    return (false);
                }

                // Wait for another image, or evidence that we're all done...
                while (true)
                {
                    // If we have an image or no data, then pop out...
                    szImageBlocks = ClientGetImageBlocks(a_apicmd);
                    if (string.IsNullOrEmpty(szImageBlocks) || (szImageBlocks != "[]"))
                    {
                        break;
                    }

                    // Wait a bit before trying again...
                    Thread.Sleep(100);
                }

                // If we run out of images, then pop out...
                aiImageBlocks = ClientGetImageBlocks();
                if ((aiImageBlocks == null) || (aiImageBlocks.Length < 1))
                {
                    break;
                }
            }

            // Stop capturing...
            if (!a_blStopCapturing)
            {
                a_blStopCapturing = true;
                if (!ClientScannerStopCapturing(ref a_apicmd))
                {
                    Log.Error("ClientScannerStopCapturing failed: " + a_apicmd.HttpResponseData());
                    a_szError = "ClientScannerStopCapturing failed, the reason follows:\n\n" + a_apicmd.HttpResponseData();
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        // The naming convention for this bit is Executer / Package / Command.  So, since
        // this is the client section, the executer is the Client.  The TWAIN Local package is
        // "scanner" and the commands are TWAIN Direct Client-Scanner API commands.  If you want to find
        // the corresponding function used by scanners, just replace "Client" with "Device"...

        /// <summary>
        /// Close a session...
        /// </summary>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerCloseSession(ref ApiCmd a_apicmd)
        {
            JsonLookup jsonlookup;
            string szFunction = "ClientScannerCloseSession";

            // Lock this command to protect the session object...
            lock (m_twainlocalsession)
            {
                // Skip the command if we've no session state or if we're already closed.
                // But return true anyway, because one should never fail when trying to
                // shut something down...
                if ((m_twainlocalsession.GetSessionState() == SessionState.NoSession)
                    || (m_twainlocalsession.GetSessionState() == SessionState.ClosedPending)
                    || (m_twainlocalsession.GetSessionState() == SessionState.Closed))
                {
                    Log.Error(szFunction + ": already closed");
                    a_apicmd.DeviceResponseSetStatus(true, null, -1, null);
                    return (true);
                }

                // Make the ApiCmd scan request...
                jsonlookup = ClientHttpRequest
                (
                    szFunction,
                    m_dnssddeviceinfo,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    new string[] {
                    "Content-Type: application/json; charset=UTF-8",
                    "X-Privet-Token: " + m_szXPrivetToken
                },
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + m_twainlocalsession.ClientCreateCommandId() + "\"," +
                    "\"method\":\"closeSession\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + m_twainlocalsession.m_szSessionId + "\"" +
                    "}" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    HttpReplyStyle.SimpleReply
                );
                if (jsonlookup == null)
                {
                    return (false);
                }

                // A session can be closed with pending imageBlocks, in which case
                // it's in a closed state, but it can't transition to nosession
                // until all of the images have been released...
                if ((m_twainlocalsession.m_aiSessionImageBlocks == null)
                    || (m_twainlocalsession.m_aiSessionImageBlocks.Length == 0))
                {
                    m_twainlocalsession.SetSessionState(SessionState.NoSession);
                }
                else
                {
                    m_twainlocalsession.SetSessionState(SessionState.ClosedPending);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Create a session, basically seeing if the device is available for use.
        /// If it works out the session state will go to "ready".  Anything else
        /// is going to be an issue...
        /// </summary>
        /// <param name="a_dnssddeviceinfo">info about the device</param>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerCreateSession(Dnssd.DnssdDeviceInfo a_dnssddeviceinfo, ref ApiCmd a_apicmd)
        {
            JsonLookup jsonlookup;
            string szFunction = "ClientScannerCreateSession";

            // Lock this command to protect the session object...
            lock (m_twainlocalsession)
            {
                // Check our incoming state...
                if (m_twainlocalsession.GetSessionState() != SessionState.NoSession)
                {
                    Log.Error(szFunction + ": session already in progress");
                    a_apicmd.DeviceResponseSetStatus(false, "busy", -1, szFunction + " busy: " + m_twainlocalsession.GetSessionState());
                    return (false);
                }

                // Squirrel this away...
                m_dnssddeviceinfo = a_dnssddeviceinfo;
                if (m_dnssddeviceinfo.szIpv4 != null)
                {
                    if (Config.Get("useHttps", "false") == "false")
                    {
                        m_szHttpServer = "http://" + m_dnssddeviceinfo.szIpv4;
                    }
                    else
                    {
                        m_szHttpServer = "https://" + m_dnssddeviceinfo.szIpv4;
                    }
                }
                else if (m_dnssddeviceinfo.szIpv6 != null)
                {
                    if (Config.Get("useHttps", "false") == "false")
                    {
                        m_szHttpServer = "http://" + m_dnssddeviceinfo.szIpv6;
                    }
                    else
                    {
                        m_szHttpServer = "https://" + m_dnssddeviceinfo.szIpv6;
                    }
                }
                else
                {
                    m_szHttpServer = "http://***noipaddress***";
                }

                // Make the ApiCmd scan request...
                jsonlookup = ClientHttpRequest
                (
                    szFunction,
                    m_dnssddeviceinfo,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    new string[] {
                    "Content-Type: application/json; charset=UTF-8",
                    "X-Privet-Token: " + m_szXPrivetToken
                },
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + m_twainlocalsession.ClientCreateCommandId() + "\"," +
                    "\"method\":\"createSession\"" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (jsonlookup == null)
                {
                    return (false);
                }

                // Set our state (to get this far, things must be okay)...
                m_twainlocalsession.SetSessionState(SessionState.Ready);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Get the session information...
        /// </summary>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerGetSession(ref ApiCmd a_apicmd)
        {
            JsonLookup jsonlookup;
            string szFunction = "ClientScannerGetSession";

            // Lock this command to protect the session object...
            lock (m_twainlocalsession)
            {
                // Check our state...
                if ((m_twainlocalsession.GetSessionState() == SessionState.NoSession)
                    || (m_twainlocalsession.GetSessionState() == SessionState.Closed))
                {
                    Log.Error(szFunction + ": state must be ready, capturing, full or closedpending");
                    a_apicmd.DeviceResponseSetStatus(false, "invalidState", -1, szFunction + " invalid state: " + m_twainlocalsession.GetSessionState());
                    return (false);
                }

                // Make the ApiCmd scan request...
                jsonlookup = ClientHttpRequest
                (
                    szFunction,
                    m_dnssddeviceinfo,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    new string[] {
                    "Content-Type: application/json; charset=UTF-8",
                    "X-Privet-Token: " + m_szXPrivetToken
                },
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + m_twainlocalsession.ClientCreateCommandId() + "\"," +
                    "\"method\":\"getSession\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + m_twainlocalsession.m_szSessionId + "\"" +
                    "}" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (jsonlookup == null)
                {
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Read an image block from the scanner...
        /// </summary>
        /// <param name="a_iImageBlockNum">block number to read</param>
        /// <param name="a_blGetMetadataWithImage">ask for the metadata</param>
        /// <param name="a_scancallback">function to call</param>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerReadImageBlock
        (
            int a_iImageBlockNum,
            bool a_blGetMetadataWithImage,
            ScanCallback a_scancallback,
            ref ApiCmd a_apicmd
        )
        {
            string szImage;
            JsonLookup jsonlookup;
            string szFunction = "ClientScannerReadImageBlock";

            // Lock this command to protect the session object...
            lock (m_twainlocalsession)
            {
                // Check our state...
                if ((m_twainlocalsession.GetSessionState() != SessionState.Capturing)
                    && (m_twainlocalsession.GetSessionState() != SessionState.Closed))
                {
                    Log.Error(szFunction + ": state must be capturing or closed");
                    a_apicmd.DeviceResponseSetStatus(false, "invalidState", -1, szFunction + " invalid state: " + m_twainlocalsession.GetSessionState());
                    return (false);
                }

                // Build the full image path...
                szImage = Path.Combine(m_szImagesFolder, "img" + a_iImageBlockNum.ToString("D6") + ".pdf");

                // Make sure it's clean...
                if (File.Exists(szImage))
                {
                    try
                    {
                        File.Delete(szImage);
                    }
                    catch
                    {
                        Log.Error(szFunction + ": Unable to delete image file: " + szImage);
                        a_apicmd.DeviceResponseSetStatus(false, "accessDenied", -1, szFunction + " access denied: " + szImage);
                        return (false);
                    }
                }

                // Ask for an image block...
                jsonlookup = ClientHttpRequest
                (
                    szFunction,
                    m_dnssddeviceinfo,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    new string[] {
                    "Content-Type: application/json; charset=UTF-8",
                    "X-Privet-Token: " + m_szXPrivetToken
                },
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + m_twainlocalsession.ClientCreateCommandId() + "\"," +
                    "\"method\":\"readImageBlock\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + m_twainlocalsession.m_szSessionId + "\"," +
                    (a_blGetMetadataWithImage ? "\"withMetadata\": true," : "") +
                    "\"imageBlockNum\":" + a_iImageBlockNum +
                    "}" +
                    "}",
                    null,
                    szImage,
                    m_iHttpTimeoutData,
                    HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (jsonlookup == null)
                {
                    return (false);
                }

                // If we have a scanner callback, hit it now...
                if (a_scancallback != null)
                {
                    a_scancallback(szImage);
                }
            }

            // All done...
            Log.Info("image: " + szImage);
            return (true);
        }

        /// <summary>
        /// Read an image block's TWAIN Direct metadata from the scanner...
        /// </summary>
        /// <param name="a_iImageBlockNum">image block to read</param>
        /// <param name="a_blGetThumbnail">the caller would like a thumbnail</param>
        /// <param name="a_scancallback">function to call</param>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerReadImageBlockMetadata(int a_iImageBlockNum, bool a_blGetThumbnail, ScanCallback a_scancallback, ref ApiCmd a_apicmd)
        {
            string szThumbnail;
            string szMetaFile;
            JsonLookup jsonlookup;
            string szFunction = "ClientScannerReadImageBlockMetadata";

            // Lock this command to protect the session object...
            lock (m_twainlocalsession)
            {
                // Check our state...
                if ((m_twainlocalsession.GetSessionState() != SessionState.Capturing)
                    && (m_twainlocalsession.GetSessionState() != SessionState.Closed))
                {
                    Log.Error(szFunction + ": state must be capturing or closed");
                    a_apicmd.DeviceResponseSetStatus(false, "invalidState", -1, szFunction + " invalid state: " + m_twainlocalsession.GetSessionState());
                    return (false);
                }

                // We're asking for a thumbnail...
                szThumbnail = null;
                if (a_blGetThumbnail)
                {
                    // Build the full image thumbnail path...
                    szThumbnail = Path.Combine(m_szImagesFolder, "img" + a_iImageBlockNum.ToString("D6") + "_thumbnail.pdf");

                    // Make sure it's clean...
                    if (File.Exists(szThumbnail))
                    {
                        try
                        {
                            File.Delete(szThumbnail);
                        }
                        catch
                        {
                            Log.Error(szFunction + ": Unable to delete image file: " + szThumbnail);
                            a_apicmd.DeviceResponseSetStatus(false, "accessDenied", -1, szFunction + " access denied: " + szThumbnail);
                            return (false);
                        }
                    }
                }

                // Please send us metadata...
                jsonlookup = ClientHttpRequest
                (
                    szFunction,
                    m_dnssddeviceinfo,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    new string[] {
                    "Content-Type: application/json; charset=UTF-8",
                    "X-Privet-Token: " + m_szXPrivetToken
                },
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + m_twainlocalsession.ClientCreateCommandId() + "\"," +
                    "\"method\":\"readImageBlockMetadata\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + m_twainlocalsession.m_szSessionId + "\"," +
                    "\"imageBlockNum\":" + a_iImageBlockNum +
                    (a_blGetThumbnail ? ",\"withThumbnail\":true" : "") +
                    "}" +
                    "}",
                    null,
                    a_blGetThumbnail ? szThumbnail : null,
                    m_iHttpTimeoutCommand,
                    HttpReplyStyle.SimpleReplyWithSessionInfo
                );

                // How'd we do?
                if (jsonlookup == null)
                {
                    // That bad, eh?
                    return (false);
                }

                // Try to get the meta data...
                try
                {
                    m_twainlocalsession.m_szMetadata = jsonlookup.Get("results");
                }
                catch
                {
                    Log.Error(szFunction + ": data error");
                    m_twainlocalsession.m_szMetadata = null;
                    a_apicmd.DeviceResponseSetStatus(false, "invalidResponse", -1, szFunction + " 'results' missing");
                    return (false);
                }

                // Save the metadata to a file...
                szMetaFile = Path.Combine(m_szImagesFolder, "img" + a_iImageBlockNum.ToString("D6") + ".meta");
                try
                {
                    File.WriteAllText(szMetaFile, m_twainlocalsession.m_szMetadata);
                }
                catch (Exception exception)
                {
                    Log.Error(szFunction + ": write error, " + exception.Message);
                    m_twainlocalsession.m_szMetadata = null;
                    a_apicmd.DeviceResponseSetStatus(false, "accessDenied", -1, szFunction + " access denied: " + szMetaFile);
                    return (false);
                }

                // Give it to the callback...
                if (a_scancallback != null)
                {
                    a_scancallback(szMetaFile);
                }
            }

            // All done...
            Log.Info("metadata:  " + szMetaFile);
            if (a_blGetThumbnail)
            {
                Log.Info("thumbnail: " + szThumbnail);
            }
            return (true);
        }

        /// <summary>
        /// Release one or more image blocks
        /// </summary>
        /// <param name="a_iImageBlockNum">first block to release</param>
        /// <param name="a_iLastImageBlockNum">last block in range (inclusive)</param>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns></returns>
        public bool ClientScannerReleaseImageBlocks(int a_iImageBlockNum, int a_iLastImageBlockNum, ref ApiCmd a_apicmd)
        {
            JsonLookup jsonlookup;
            string szFunction = "ClientScannerReleaseImageBlocks";

            // Lock this command to protect the session object...
            lock (m_twainlocalsession)
            {
                // Check our state...
                if ((m_twainlocalsession.GetSessionState() != SessionState.Capturing)
                    && (m_twainlocalsession.GetSessionState() != SessionState.Closed))
                {
                    Log.Error(szFunction + ": state must be capturing or closed");
                    a_apicmd.DeviceResponseSetStatus(false, "invalidState", -1, szFunction + " invalid state: " + m_twainlocalsession.GetSessionState());
                    return (false);
                }

                // Make the ApiCmd scan request...
                jsonlookup = ClientHttpRequest
                (
                    szFunction,
                    m_dnssddeviceinfo,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    new string[] {
                    "Content-Type: application/json; charset=UTF-8",
                    "X-Privet-Token: " + m_szXPrivetToken
                },
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + m_twainlocalsession.ClientCreateCommandId() + "\"," +
                    "\"method\":\"releaseImageBlocks\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + m_twainlocalsession.m_szSessionId + "\"," +
                    "\"imageBlockNum\":" + a_iImageBlockNum + "," +
                    "\"lastImageBlockNum\":" + a_iLastImageBlockNum +
                    "}" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (jsonlookup == null)
                {
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Send a task to the scanner...
        /// </summary>
        /// <param name="a_szTask">the task to use</param>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerSetTwainDirectOptions(string a_szTask, ref ApiCmd a_apicmd)
        {
            JsonLookup jsonlookup;
            string szFunction = "ClientScannerSetTwainDirectOptions";

            // Lock this command to protect the session object...
            lock (m_twainlocalsession)
            {
                // Check our state...
                if (m_twainlocalsession.GetSessionState() != SessionState.Ready)
                {
                    Log.Error(szFunction + ": state must be ready");
                    a_apicmd.DeviceResponseSetStatus(false, "invalidState", -1, szFunction + " invalid state: " + m_twainlocalsession.GetSessionState());
                    return (false);
                }

                // Make the ApiCmd scan request...
                jsonlookup = ClientHttpRequest
                (
                    szFunction,
                    m_dnssddeviceinfo,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    new string[] {
                    "Content-Type: application/json; charset=UTF-8",
                    "X-Privet-Token: " + m_szXPrivetToken
                },
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + m_twainlocalsession.ClientCreateCommandId() + "\"," +
                    "\"method\":\"setTwainDirectOptions\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + m_twainlocalsession.m_szSessionId + "\"," +
                    "\"task\":" + a_szTask +
                    "}" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (jsonlookup == null)
                {
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Start capturing...
        /// </summary>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerStartCapturing(ref ApiCmd a_apicmd)
        {
            JsonLookup jsonlookup;
            string szFunction = "ClientScannerStartCapturing";

            // Lock this command to protect the session object...
            lock (m_twainlocalsession)
            {
                // Check our state...
                if (m_twainlocalsession.GetSessionState() != SessionState.Ready)
                {
                    Log.Error(szFunction + ": state must be ready");
                    a_apicmd.DeviceResponseSetStatus(false, "invalidState", -1, szFunction + " invalid state: " + m_twainlocalsession.GetSessionState());
                    return (false);
                }

                // Make the ApiCmd scan request...
                jsonlookup = ClientHttpRequest
                (
                    szFunction,
                    m_dnssddeviceinfo,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    new string[] {
                    "Content-Type: application/json; charset=UTF-8",
                    "X-Privet-Token: " + m_szXPrivetToken
                },
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + m_twainlocalsession.ClientCreateCommandId() + "\"," +
                    "\"method\":\"startCapturing\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + m_twainlocalsession.m_szSessionId + "\"" +
                    "}" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (jsonlookup == null)
                {
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Stop capturing...
        /// </summary>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerStopCapturing(ref ApiCmd a_apicmd)
        {
            JsonLookup jsonlookup;
            string szFunction = "ClientScannerStopCapturing";

            // Lock this command to protect the session object...
            lock (m_twainlocalsession)
            {
                // Check our state...
                if (m_twainlocalsession.GetSessionState() != SessionState.Capturing)
                {
                    Log.Error(szFunction + ": state must be capturing");
                    a_apicmd.DeviceResponseSetStatus(false, "invalidState", -1, szFunction + " invalid state: " + m_twainlocalsession.GetSessionState());
                    return (false);
                }

                // Make the ApiCmd scan request...
                jsonlookup = ClientHttpRequest
                (
                    szFunction,
                    m_dnssddeviceinfo,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    new string[] {
                    "Content-Type: application/json; charset=UTF-8",
                    "X-Privet-Token: " + m_szXPrivetToken
                },
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + m_twainlocalsession.ClientCreateCommandId() + "\"," +
                    "\"method\":\"stopCapturing\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + m_twainlocalsession.m_szSessionId + "\"" +
                    "}" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (jsonlookup == null)
                {
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Wait for one or more events...
        /// </summary>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerWaitForEvents(ref ApiCmd a_apicmd)
        {
            JsonLookup jsonlookup;
            string szFunction = "ClientScannerWaitForEvents";

            // We can't lock on this command, but we need to lock
            // to check the state...
            lock (m_twainlocalsession)
            {
                // Check our state...
                if (m_twainlocalsession.GetSessionState() == SessionState.NoSession)
                {
                    Log.Error(szFunction + ": state can't be noSession");
                    a_apicmd.DeviceResponseSetStatus(false, "invalidState", -1, szFunction + " invalid state: " + m_twainlocalsession.GetSessionState());
                    return (false);
                }
            }

            // Make the ApiCmd scan request...
            jsonlookup = ClientHttpRequest
            (
                szFunction,
                m_dnssddeviceinfo,
                ref a_apicmd,
                "/privet/twaindirect/session",
                "POST",
                new string[] {
                    "Content-Type: application/json; charset=UTF-8",
                    "X-Privet-Token: " + m_szXPrivetToken
                },
                "{" +
                "\"kind\":\"twainlocalscanner\"," +
                "\"commandId\":\"" + m_twainlocalsession.ClientCreateCommandId() + "\"," +
                "\"method\":\"waitForEvents\"," +
                "\"params\":{" +
                "\"sessionId\":\"" + m_twainlocalsession.m_szSessionId + "\"" +
                "}" +
                "}",
                null,
                null,
                m_iHttpTimeoutEvent,
                HttpReplyStyle.Event
            );
            if (jsonlookup == null)
            {
                return (false);
            }

            // All done...
            return (true);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Device Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Device Methods...

        /// <summary>
        /// Dispatch a command...
        /// </summary>
        /// <param name="a_szJsonCommand">the command we received</param>
        /// <param name="a_httplistenercontext">thr HTTP object that delivered the command</param>
        /// <returns>true on success</returns>
        public void DeviceDispatchCommand(string a_szJsonCommand, ref HttpListenerContext a_httplistenercontext)
        {
            int ii;
            bool blSuccess;
            int iTaskIndex;
            ApiCmd apicmd;
            string szUri;
            string szXPrivetToken;
            string szFunction = "DeviceDispatchCommand";

            // Confirm that this command is coming in on a good URI, if it's not
            // then ignore it...
            szUri = a_httplistenercontext.Request.RawUrl.ToString();
            if (    (szUri != "/privet/info")
                &&  (szUri != "/privet/twaindirect/session"))
            {
                return;
            }

            // Every command has to have X-Privet-Token in the header...
            for (ii = 0; ii < a_httplistenercontext.Request.Headers.Count; ii++)
            {
                if (a_httplistenercontext.Request.Headers.GetKey(ii) == "X-Privet-Token")
                {
                    break;
                }
            }

            // We didn't find the X-Privet-Token...
            if (ii >= a_httplistenercontext.Request.Headers.Count)
            {
                Log.Error("X-Privet-Token missing...");
                apicmd = new ApiCmd(m_dnssddeviceinfo, null, ref a_httplistenercontext);
                ReturnError(szFunction, apicmd, "invalid_x_privet_token", null, 0);
                return;
            }

            // We found it, squirrel away the value...
            szXPrivetToken = a_httplistenercontext.Request.Headers.Get(ii);

            // Handle the info command...
            if (szUri == "/privet/info")
            {
                apicmd = new ApiCmd(m_dnssddeviceinfo, null, ref a_httplistenercontext);
                DeviceInfo(ref apicmd);
                return;
            }

            // The rest of this must be coming in on /privet/twaindirect/session,
            // we'll start by validating our X-Privet-Token...
            if (szXPrivetToken != m_szXPrivetToken)
            {
                Log.Error("X-Privet-Token is invalid...");
                apicmd = new ApiCmd(m_dnssddeviceinfo, null, ref a_httplistenercontext);
                ReturnError(szFunction, apicmd, "invalid_x_privet_token", null, 0);
                return;
            }

            // Parse the command...
            long lResponseCharacterOffset;
            JsonLookup jsonlookup = new JsonLookup();
            blSuccess = jsonlookup.Load(a_szJsonCommand, out lResponseCharacterOffset);
            if (!blSuccess)
            {
                Log.Error("JSON error: " + a_szJsonCommand.Insert((int)lResponseCharacterOffset, "ERROR>>>"));
                apicmd = new ApiCmd(m_dnssddeviceinfo, jsonlookup, ref a_httplistenercontext);
                ReturnError(szFunction, apicmd, "invalidJson", null, lResponseCharacterOffset);
                return;
            }

            // Init stuff...
            apicmd = new ApiCmd(m_dnssddeviceinfo, jsonlookup, ref a_httplistenercontext);

            // If we are running a session, make sure that the command's id matches
            // the session's id...
            if (m_twainlocalsession.m_szSessionId != null)
            {
                if (jsonlookup.Get("params.sessionId") != m_twainlocalsession.m_szSessionId)
                {
                    Log.Error("SESSIONID error: <" + jsonlookup.Get("params.sessionId") + "> <" + m_twainlocalsession.m_szSessionId + ">");
                    ReturnError(szFunction, apicmd, "invalidSessionId", null, -1);
                    return;
                }
            }

            // Log it...
            if (Log.GetLevel() != 0)
            {
                Log.Info("");
                Log.Info("http>>> " + jsonlookup.Get("method"));
                Log.Info("http>>> " + a_httplistenercontext.Request.HttpMethod + " uri " + a_httplistenercontext.Request.Url.AbsoluteUri);
                NameValueCollection namevaluecollectionHeaders = a_httplistenercontext.Request.Headers;
                // Get each header and display each value.
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
                Log.Info("http>>> recvdata " + a_szJsonCommand);
           }

            // Dispatch the command...
            switch (jsonlookup.Get("method"))
            {
                default:
                    break;

                case "closeSession":
                    DeviceScannerCloseSession(ref apicmd);
                    break;

                case "createSession":
                    DeviceScannerCreateSession(ref apicmd);
                    break;

                case "getSession":
                    DeviceScannerGetSession(ref apicmd);
                    break;

                case "readImageBlock":
                    DeviceScannerReadImageBlock(ref apicmd);
                    break;

                case "readImageBlockMetadata":
                    DeviceScannerReadImageBlockMetadata(ref apicmd);
                    break;

                case "releaseImageBlocks":
                    DeviceScannerReleaseImageBlocks(ref apicmd);
                    break;

                case "setTwainDirectOptions":
                    // The task must be an object, we'll treat this as a JSON error,
                    // even though it's syntactically okay.  If the type is undefined
                    // it means we didn't find a task.
                    switch (jsonlookup.GetType("params.task"))
                    {
                        // We found the property, and it's an object, so drop down...
                        case JsonLookup.EPROPERTYTYPE.OBJECT:
                            break;

                        // We didn't find the property...
                        case JsonLookup.EPROPERTYTYPE.UNDEFINED:
                            Log.Error("TASK property is missing: " + a_szJsonCommand.Insert(0, "ERROR>>>"));
                            ReturnError(szFunction, apicmd, "invalidJson", null, 0);
                            return;

                        // We found the property, but it's not an object...
                        default:
                            iTaskIndex = a_szJsonCommand.IndexOf("\"task\":") + 7;
                            Log.Error("TASK must be an object: " + a_szJsonCommand.Insert(iTaskIndex, "ERROR>>>"));
                            ReturnError(szFunction, apicmd, "invalidJson", null, iTaskIndex);
                            return;
                    }

                    // Go ahead and process it...
                    DeviceScannerSetTwainDirectOptions(ref apicmd);
                    break;

                case "startCapturing":
                    // No prompt...
                    if (m_confirmscan == null)
                    {
                        DeviceScannerStartCapturing(ref apicmd);
                    }
                    // Prompt the user to begin scanning...
                    else
                    {
                        ButtonPress buttonpress = m_confirmscan(m_fConfirmScanScale);
                        if (buttonpress == ButtonPress.OK)
                        {
                            DeviceScannerStartCapturing(ref apicmd);
                        }
                    }
                    break;

                case "stopCapturing":
                    DeviceScannerStopCapturing(ref apicmd);
                    break;
            }

            // All done...
            return;
        }

        /// <summary>
        /// Return the note= field...
        /// </summary>
        /// <returns>users friendly name</returns>
        public string GetTwainLocalNote()
        {
            return (m_twainlocalsession.DeviceRegisterGetTwainLocalNote());
        }

        /// <summary>
        /// Start monitoring for HTTP commands...
        /// </summary>
        /// <returns></returns>
        public bool DeviceHttpServerStart()
        {
            int iPort;
            bool blSuccess;

            // Get our port...
            if (!int.TryParse(Config.Get("usePort","55555"), out iPort))
            {
                Log.Error("DeviceHttpServerStart: bas port..." + Config.Get("usePort", "55555"));
                return (false);
            }

            // Create our server...
            m_httpserver = new HttpServer();

            // Start us up...
            blSuccess = m_httpserver.ServerStart
            (
                DeviceDispatchCommand,
                m_twainlocalsession.DeviceRegisterGetTwainLocalInstanceName(),
                iPort,
                m_twainlocalsession.DeviceRegisterGetTwainLocalTy(),
                m_twainlocalsession.DeviceRegisterGetTwainLocalNote()
            );
            if (!blSuccess)
            {
                Log.Error("ServerStart failed...");
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Stop monitoring for HTTP commands...
        /// </summary>
        /// <returns></returns>
        public void DeviceHttpServerStop()
        {
            if (m_httpserver != null)
            {
                m_httpserver.ServerStop();
                m_httpserver = null;
            }
        }

        /// <summary>
        /// Register a device.  Actually, we're patching a registration ticket that
        /// was entered by an application.  They have to get us the registration id
        /// so we can complete the process.
        /// 
        /// We register the commands and finalize.  None of this requires anything
        /// more than our application key.
        /// </summary>
        /// <param name="a_jsonlookup">the twain driver info</param>
        /// <param name="a_iScanner">the index of the driver we want to register</param>
        /// <param name="a_szNote">a note for this scanner from the user</param>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool DeviceRegister(JsonLookup a_jsonlookup, int a_iScanner, string a_szNote, ref ApiCmd a_apicmd)
        {
            // We're being asked to clear the register...
            if (a_iScanner < 0)
            {
                m_twainlocalsession.DeviceRegisterClear();
                return (true);
            }

            // Get the scanner entry...
            string szScanner = "scanners[" + a_iScanner + "]";

            // Collect our data...
            string szDeviceName = a_jsonlookup.Get(szScanner + ".twidentity");
            if (string.IsNullOrEmpty(szDeviceName))
            {
                szDeviceName = a_jsonlookup.Get(szScanner + ".sane");
            }
            string szHostName = a_jsonlookup.Get(szScanner + ".hostName");
            string szSerialNumber = a_jsonlookup.Get(szScanner + ".serialNumber");

            // Get the device code...
            try
            {
                m_twainlocalsession.DeviceRegisterSet
                (
                    szDeviceName,
                    szSerialNumber,
                    a_szNote
                );
            }
            catch
            {
                Log.Error("DeviceRegister failed...");
                a_apicmd.DeviceResponseSetStatus(false, "invalidJason", -1, "DeviceRegister JSON syntax error...");
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Load the register data from a file...
        /// </summary>
        /// <returns>true on success</returns>
        public bool DeviceRegisterLoad()
        {
            // First load the data...
            if (!m_twainlocalsession.DeviceRegisterLoad(this, Path.Combine(m_szWriteFolder, "register.txt")))
            {
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Save the register data to a file...
        /// </summary>
        /// <returns>true on success</returns>
        public bool DeviceRegisterSave()
        {
            return (m_twainlocalsession.Save(Path.Combine(m_szWriteFolder, "register.txt")));
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions...

        /// <summary>
        /// Our supported platforms...
        /// </summary>
        public enum Platform
        {
            UNKNOWN,
            WINDOWS,
            LINUX,
            MACOSX
        };

        /// <summary>
        /// Our supported browsers...
        /// </summary>
        public enum Browser
        {
            UNKNOWN,
            CHROME,
            FIREFOX,
            IE,
            SAFARI
        }

        /// <summary>
        /// Buttons that a user can press...
        /// </summary>
        public enum ButtonPress
        {
            OK,
            Cancel
        };

        /// <summary>
        /// TWAIN Direct Client-Scanner API errors.  Be sure to only append to the list,
        /// treat the numbers as unmodifiable constants, so that we can
        /// guarantee their value across interfaces...
        /// </summary>
        public enum ApiStatus
        {
            success = 0,
            newSessionNotAllowed = 1,
            invalidSessionId = 2,
            closedSession = 3,
            notReady = 4,
            notCapturing = 5,
            invalidImageBlockNumber = 6,
            invalidCapturingOptions = 7
        }

        /// <summary>
        /// A place to keep our command information...
        /// </summary>
        public struct Command
        {
            public string szDeviceName;
            public string szJson;
        }

        /// <summary>
        /// Delegate for the scan callback...
        /// </summary>
        /// <param name="a_szImage"></param>
        /// <returns></returns>
        public delegate bool ScanCallback(string a_szImage);

        /// <summary>
        /// Prompt the user to confirm a request to scan...
        /// </summary>
        /// <returns>button the user pressed</returns>
        public delegate ButtonPress ConfirmScan(float a_fConfirmScanScale);

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Common methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Common Methods...

        /// <summary>
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        internal void Dispose(bool a_blDisposing)
        {
            // Free managed resources...
            if (a_blDisposing)
            {
                if (m_httpserver != null)
                {
                    m_httpserver.Dispose();
                    m_httpserver = null;
                }
                if (m_ipcTwainDirectOnTwain != null)
                {
                    m_ipcTwainDirectOnTwain.Dispose();
                    m_ipcTwainDirectOnTwain = null;
                }
                if (m_processTwainDirectOnTwain != null)
                {
                    m_processTwainDirectOnTwain.Kill();
                    m_processTwainDirectOnTwain.Dispose();
                    m_processTwainDirectOnTwain = null;
                }
            }
        }

        /// <summary>
        /// Convert a C# DateTime to a Unix Epoch-based timestamp in milliseconds...
        /// </summary>
        /// <param name="dateTime">value to convert</param>
        /// <returns>result</returns>
        public static long DateTimeToUnixTimeMs(DateTime a_datetime)
        {
            DateTime datetimeUnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            long u64Ticks = (long)(a_datetime.ToUniversalTime() - datetimeUnixEpoch).Ticks;
            return (u64Ticks / TimeSpan.TicksPerMillisecond);
        }

        // Run curl...get the stdout as a string, log the command and the result...
        private string Run(string szProgram, string a_szArguments)
        {
            // Log what we're doing...
            Log.Info("run>>> " + szProgram);
            Log.Info("run>>> " + a_szArguments);

            // Start the child process.
            Process p = new Process();

            // Redirect the output stream of the child process.
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(szProgram);
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = szProgram;
            p.StartInfo.Arguments = a_szArguments;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.Start();

            // Do not wait for the child process to exit before
            // reading to the end of its redirected stream.
            // p.WaitForExit();
            // Read the output stream first and then wait.
            string szOutput = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            // Log any output...
            Log.Info("run>>> " + szOutput);

            // All done...
            return (szOutput);
        }

        /// <summary>
        /// Convert a Unix Epoch-based timestamp in milliseconds to a C# DateTime...
        /// </summary>
        /// <param name="a_lUnixTimeMs">value to convert</param>
        /// <returns>result</returns>
        public static DateTime UnixTimeMsToDateTime(long a_lUnixTimeMs)
        {
            DateTime datetimeUnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            long u64Ticks = (long)(a_lUnixTimeMs * TimeSpan.TicksPerMillisecond);
            return (new DateTime(datetimeUnixEpoch.Ticks + u64Ticks));
        }

        /// <summary>
        /// Parse data from the session object.  We do this in a number
        /// of places, so it makes sense to centralize...
        /// </summary>
        /// <param name="a_apicmd">the command object</param>
        /// <param name="a_jsonlookup">data to check</param>
        /// <returns>true on success</returns>
        private bool ParseSession(ApiCmd a_apicmd, JsonLookup a_jsonlookup)
        {
            // Uh-oh...
            if (a_jsonlookup == null)
            {
                Log.Error("ParseSession: a_jsonlookup is null...");
                return (false);
            }

            // Is this info?
            if (a_apicmd.GetUri() == "/privet/info")
            {

                // Squirrel away the x-privet-token...
                m_szXPrivetToken = a_jsonlookup.Get("x-privet-token");

                // All done...
                return (true);
            }

            // If there's an error we won't find a session object,
            // so scoot and pretend that all is well...
            if (a_jsonlookup.Get("results.success") == "false")
            {
                return (true);
            }

            // Init stuff...
            m_twainlocalsession.m_szSessionId = a_jsonlookup.Get("results.session.sessionId");
            m_twainlocalsession.m_aiSessionImageBlocks = null;

            // Sanity check...
            if (string.IsNullOrEmpty(m_twainlocalsession.m_szSessionId))
            {
                return (false);
            }

            // Protect ourselves from weirdness...
            try
            {
                // Collect the image blocks data...
                m_twainlocalsession.m_aiSessionImageBlocks = null;
                string szImageBlocks = a_jsonlookup.Get("results.session.imageBlocks", false);
                if (szImageBlocks != null)
                {
                    string[] aszImageBlocks = szImageBlocks.Split(new char[] { '[', ' ', ',', ']', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (aszImageBlocks != null)
                    {
                        m_twainlocalsession.m_aiSessionImageBlocks = new int[aszImageBlocks.Length];
                        for (int ii = 0; ii < aszImageBlocks.Length; ii++)
                        {
                            m_twainlocalsession.m_aiSessionImageBlocks[ii] = int.Parse(aszImageBlocks[ii]);
                        }
                    }
                }

                // Change our state...
                switch (a_jsonlookup.Get("results.session.state"))
                {
                    default: Log.Error("Unrecognized results.session.state: " + a_jsonlookup.Get("results.session.state")); return (false);
                    case "capturing": m_twainlocalsession.SetSessionState(SessionState.Capturing); break;
                    case "closed":
                        // We can't truly close until all the imageblocks are resolved...
                        if (    (m_twainlocalsession.m_aiSessionImageBlocks == null)
                            ||  (m_twainlocalsession.m_aiSessionImageBlocks.Length == 0))
                        {
                            m_twainlocalsession.SetSessionState(SessionState.Closed);
                        }
                        else
                        {
                            m_twainlocalsession.SetSessionState(SessionState.ClosedPending);
                        }
                        break;
                    case "full": m_twainlocalsession.SetSessionState(SessionState.Full); break;
                    case "nosession": m_twainlocalsession.SetSessionState(SessionState.NoSession); break;
                    case "ready": m_twainlocalsession.SetSessionState(SessionState.Ready); break;
                }
            }
            catch
            {
                m_twainlocalsession.m_szSessionId = null;
                m_twainlocalsession.m_aiSessionImageBlocks = null;
                return (false);
            }

            // All done...
            return (true);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Client Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Client Methods...

        /// <summary>
        /// This function helps us with the command lifecycle, from the initial
        /// send, to the final result.  There are a fews way a command can turn
        /// out:
        ///
        /// -  the cloud/lan may be down or inaccessible, in which case we'll
        ///    time out trying to send the command
        ///
        /// -  The device may be in use or some critical error state, in which
        ///    case we'll get back an error, the same could happen if our
        ///    session is revoked, or the device is deleted
        ///
        /// -  and, of course, the command may succeed -- yay us!
        /// </summary>
        /// <param name="a_szReason">reason for the call, for logging</param>
        /// <param name="a_szUri">our target</param>
        /// <param name="a_szMethod">http method (ex: POST, DELETE...)</param>
        /// <param name="a_aszHeader">array of headers to send or null</param>
        /// <param name="a_szData">data to send or null</param>
        /// <param name="a_szUploadFile">upload data from a file</param>
        /// <param name="a_szOutputFile">redirect the data to a file</param>
        /// <param name="a_iTimeout">timeout in milliseconds</param>
        /// <param name="a_httpreplystyle">how we know when the command is complete</param>
        /// <returns>true on success</returns>
        private JsonLookup ClientHttpRequest
        (
            string a_szReason,
            Dnssd.DnssdDeviceInfo a_dnssddeviceinfo,
            ref ApiCmd a_apicmd,
            string a_szUri,
            string a_szMethod,
            string[] a_aszHeader,
            string a_szData,
            string a_szUploadFile,
            string a_szOutputFile,
            int a_iTimeout,
            HttpReplyStyle a_httpreplystyle
        )
        {
            bool blSuccess;
            long lRetryHttpRequest;
            long lRetryHttpRequestCount;
            long lResponseCharacterOffset;
            JsonLookup jsonlookup = null;

            // Squirrel this away...
            a_apicmd.SetSendCommand(a_szData);
            a_apicmd.SetUri(a_szUri);

            // The big loop for an unresponsive device...
            lRetryHttpRequestCount = 2;
            for (lRetryHttpRequest = 0; (lRetryHttpRequest < lRetryHttpRequestCount); lRetryHttpRequest++)
            {
                // Send the command and get back an acknowledgement that the command was received,
                // we'll try this once...

                // Make the ApiCmd scan request...
                blSuccess = a_apicmd.HttpRequest(a_szReason, a_dnssddeviceinfo, a_szUri, a_szMethod, a_aszHeader, a_szData, a_szUploadFile, a_szOutputFile, a_iTimeout);
                if (!blSuccess)
                {
                    continue;
                }

                // Parse the JSON that came from when the command was issued...
                jsonlookup = new JsonLookup();
                blSuccess = jsonlookup.Load(a_apicmd.HttpResponseData(), out lResponseCharacterOffset);
                if (!blSuccess)
                {
                    Log.Error(a_szReason + ": communication error");
                    a_apicmd.DeviceResponseSetStatus(false, "invalidJson", lResponseCharacterOffset, "ClientHttpRequest JSON syntax error...");
                    return (null);
                }

                // Try to get the session data, we do this because we need it for almost
                // all of the commands, and it can handle the pressure if the data isn't
                // present in the reply...
                if (a_httpreplystyle == HttpReplyStyle.SimpleReplyWithSessionInfo)
                {
                    blSuccess = ParseSession(a_apicmd, jsonlookup);
                    if (!blSuccess)
                    {
                        Log.Error(a_szReason + ": data error");
                        a_apicmd.DeviceResponseSetStatus(false, "invalidResponse", -1, "ClientHttpRequest response error...");
                        return (null);
                    }
                }

                // All done...
                return (jsonlookup);
            }

            // All done...
            return (null);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Device Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Device Methods...

        /// <summary>
        /// Return an error block...
        /// </summary>
        /// <param name="a_szReason">our caller</param>
        /// <param name="a_apicmd">info about the command</param>
        /// <param name="a_szCode">the status code</param>
        /// <param name="a_szJsonKey">json key to point of error or null</param>
        /// <param name="a_lResponseCharacterOffset">character offset of json error or -1</param>
        /// <returns>true on success</returns>
        private bool ReturnError(string a_szReason, ApiCmd a_apicmd, string a_szCode, string a_szJsonKey, long a_lResponseCharacterOffset)
        {
            string szResponse;

            // Our base response...
            szResponse =
                "{" +
                "\"kind\":\"twainlocalscanner\"," +
                "\"commandId\":\"" + a_apicmd.GetCommandId() + "\"," +
                "\"state\":\"error\"," +
                "\"method\":\"" + a_apicmd.GetCommandName() + "\"," +
                "\"results\":{" +
                "\"success\":false," +
                "\"code\":\"" + a_szCode + "\"";

            // Add a character offset, if needed...
            if (a_szCode == "invalidJson")
            {
                szResponse +=
                    ",\"characterOffset\":" + a_lResponseCharacterOffset;
            }
            
            // Add a JSON key, if needed...
            if (!string.IsNullOrEmpty(a_szJsonKey))
            {
                szResponse +=
                    ",\"jsonKey\":\"" + a_szJsonKey + "\"";
            }

            // Finish it...
            szResponse +=
                "}" +
                "}";

           // Send the response...
            a_apicmd.HttpRespond(a_szCode, szResponse);

            // All done...
            return (true);
        }

        /// <summary>
        /// Update the session object...
        /// </summary>
        /// <param name="a_szReason">something for logging</param>
        /// <param name="a_apicmd">the command object we're working on</param>
        /// <param name="a_szSessionState">the state of the Scanner API session</param>
        /// <returns>true on success</returns>
        private bool UpdateSession
        (
            string a_szReason,
            ApiCmd a_apicmd,
            SessionState a_esessionstate
        )
        {
            string szResponse;
            string szSessionState;
            string szSessionObject;

            //////////////////////////////////////////////////
            // We're responding to the /privet/info command...
            #region We're responding to the /privet/info command...
            if (a_apicmd.GetUri() == "/privet/info")
            {
                string szDeviceState;
                Dnssd.DnssdDeviceInfo dnssddeviceinfo = GetDnssdDeviceInfo();

                // Device state...
                switch (m_twainlocalsession.GetSessionState())
                {
                    default: szDeviceState = "stopped"; break;
                    case SessionState.NoSession: szDeviceState = "idle"; break;
                    case SessionState.Capturing: szDeviceState = "processing"; break;
                    case SessionState.Closed: szDeviceState = "processing"; break;
                    case SessionState.ClosedPending: szDeviceState = "processing"; break;
                    case SessionState.Full: szDeviceState = "processing"; break;
                    case SessionState.Ready: szDeviceState = "processing"; break;
                }

                // Generate a new privet token anytime we don't have a session...
                if (m_twainlocalsession.GetSessionState() == SessionState.NoSession)
                {
                    // If we don't have a timestamp, or if the timestamp has exceeded
                    // two minutes (120 second), then make a new one...
                    if (    (m_lPrivetTokenTimestamp == 0)
                        ||  ((m_lPrivetTokenTimestamp + 120) > (long)DateTime.Now.Subtract(DateTime.MinValue).TotalSeconds))
                    {
                        long lTicks;

                        // This is what's recommended...
                        // XSRF_token = base64( SHA1(device_secret + DELIMITER + issue_timecounter) + DELIMITER + issue_timecounter )
                        lTicks = DateTime.Now.Ticks;
                        m_szXPrivetTokenClear = Guid.NewGuid().ToString() + ":" + lTicks;
                        using (SHA1Managed sha1managed = new SHA1Managed())
                        {
                            byte[] abHash = sha1managed.ComputeHash(Encoding.UTF8.GetBytes(m_szXPrivetTokenClear));
                            m_szXPrivetToken = Convert.ToBase64String(abHash);
                        }
                        m_szXPrivetToken += ":" + lTicks;
                        m_szXPrivetTokenClear += ":" + lTicks;
                    }
                }

                // Refresh the privet token timeout...
                m_lPrivetTokenTimestamp = (long)DateTime.Now.Subtract(DateTime.MinValue).TotalSeconds;

                // Construct a response...
                szResponse =
                    "{" +
                    "\"version\":\"1.0\"," +
                    "\"name\":\"" + dnssddeviceinfo.szTxtTy + "\"," +
                    "\"description\":\"" + dnssddeviceinfo.szTxtNote + "\"," +
                    "\"url\":\"" + ((Config.Get("useHttps", "false") == "false") ? "http://" : "https://") + Dns.GetHostName() + ".local:" + m_httpserver.GetPort() + "/twaindirect" + "\"," +
                    "\"type\":\"" + dnssddeviceinfo.szTxtType + "\"," +
                    "\"id\":\"\"," +
                    "\"device_state\": \"" + szDeviceState + "\"," +
                    "\"connection_state\": \"offline\"," +
                    "\"manufacturer\":\"" + "" + "\"," +
                    "\"model\":\"" + "" + "\"," +
                    "\"serial_number\":\"" + "" + "\"," +
                    "\"firmware\":\"" + "" + "\"," +
                    "\"uptime\":\"" + "" + "\"," +
                    "\"setup_url\":\"" + "" + "\"," +
                    "\"support_url\":\"" + "" + "\"," +
                    "\"update_url\":\"" + "" + "\"," +
                    "\"x-privet-token\":\"" + m_szXPrivetToken + "\"," +
                    "\"api\":[" +
                    "\"/privet/twaindirect/session\"" +
                    "]," +
                    "\"semantic_state\":\"" + "" + "\"" +
                    "}";

                // Send the response...
                a_apicmd.HttpRespond("success", szResponse);

                // All done...
                return (true);
            }
            #endregion

            ////////////////////////////////////////////////////////////////
            // We're responding to a /privet/twaindirect/session command...
            #region We're responding to the /privet/info command...
            if (a_apicmd.GetUri() == "/privet/twaindirect/session")
            {
                // Change our state...
                m_twainlocalsession.SetSessionState(a_esessionstate);
                switch (a_esessionstate)
                {
                    default:
                    case SessionState.Capturing: szSessionState = "capturing"; break;
                    case SessionState.Closed:
                    case SessionState.ClosedPending:
                        szSessionState = "closed";
                        break;
                    case SessionState.Full: szSessionState = "full"; break;
                    case SessionState.NoSession: szSessionState = "nosession"; break;
                    case SessionState.Ready: szSessionState = "ready"; break;
                }

                // Make sure these are cleared, if we have no session...
                if ((a_esessionstate == SessionState.Closed)
                    || (a_esessionstate == SessionState.NoSession))
                {
                    m_twainlocalsession.m_szSessionId = null;
                    m_twainlocalsession.m_lSessionRevision = 0;
                    m_twainlocalsession.m_szPreviousSessionObject = "";
                }

                // Okay, you're going to love this.  So in order to change our revision
                // number in a meaningful way, we'll generate the string data we want
                // to send back and compare it to the previous string we generated, if
                // there is a difference, then we'll update the revision.  Of course we
                // only do this if we have an active session.
                //
                // The chief benefit of doing it this way, is that it's centralized and
                // easy to understand.  The chief drawback is that it feels groadie with
                // the if-statements...
                if (a_esessionstate == SessionState.NoSession)
                {
                    m_twainlocalsession.m_lSessionRevision = 0;
                    szSessionObject = "";
                    m_twainlocalsession.m_szPreviousSessionObject = "";
                }
                else
                {
                    szSessionObject =
                        "\"session\":{" +
                        "\"sessionId\":\"" + m_twainlocalsession.m_szSessionId + "\"," +
                        "\"revision\":" + m_twainlocalsession.m_lSessionRevision + "," +
                        "\"state\":\"" + szSessionState + "\"," +
                        a_apicmd.GetImageBlocksJson();

                    // Add the TWAIN Direct options, if any...
                    string szTaskReply = a_apicmd.GetTaskReply();
                    if (!string.IsNullOrEmpty(szTaskReply))
                    {
                        szSessionObject += "\"task\":" + szTaskReply + ",";
                    }

                    // End the session object...
                    if (szSessionObject.EndsWith(","))
                    {
                        szSessionObject = szSessionObject.Substring(0, szSessionObject.Length - 1);
                    }
                    szSessionObject += "}";

                    // Check to see if we have to update our revision number...
                    if (string.IsNullOrEmpty(m_twainlocalsession.m_szPreviousSessionObject)
                        || (szSessionObject != m_twainlocalsession.m_szPreviousSessionObject))
                    {
                        szSessionObject = szSessionObject.Replace
                        (
                            "\"revision\":" + m_twainlocalsession.m_lSessionRevision + ",",
                            "\"revision\":" + (m_twainlocalsession.m_lSessionRevision + 1) + ","
                        );
                        m_twainlocalsession.m_lSessionRevision += 1;
                        m_twainlocalsession.m_szPreviousSessionObject = szSessionObject;
                    }
                }

                // Construct a response...
                szResponse =
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + a_apicmd.GetCommandId() + "\"," +
                    "\"method\":\"" + a_apicmd.GetCommandName() + "\"," +
                    "\"results\":{" +
                    "\"success\": true," +
                    a_apicmd.GetMetadata() +
                    szSessionObject +
                    "}" + // results
                    "}";  // root

                // Send the response, note that any multipart contruction work
                // takes place in this function...
                a_apicmd.HttpRespond("success", szResponse);

                // All done...
                return (true);
            }
            #endregion

            // Getting this far is a bad thing.  We shouldn't be here
            // unless somebody upstream fall asleep at the switch...
            Log.Error("UpdateSession: bad uri..." + a_apicmd.GetUri());
            return (false);
        }

        /// <summary>
        /// return info about the device...
        /// </summary>
        /// <param name="a_apicmd">the info command the caller sent</param>
        /// <returns>true on success</returns>
        private bool DeviceInfo(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            string szFunction = "DeviceInfo";

            // Reply to the command with a session object...
            blSuccess = UpdateSession(szFunction, a_apicmd, m_twainlocalsession.GetSessionState());
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "communicationError", null, -1);
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Close a scanning session...
        /// </summary>
        /// <param name="a_apicmd">the close command the caller sent</param>
        /// <returns>true on success</returns>
        private bool DeviceScannerCloseSession(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            string szFunction = "DeviceScannerCloseSession";

            // State check...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    Log.Error(szFunction + ": unrecognized state..." + m_twainlocalsession.GetSessionState());
                    ReturnError(szFunction, a_apicmd, "notReady", null, -1);
                    return (false);
                case SessionState.Ready:
                case SessionState.Capturing:
                case SessionState.Full:
                    break;
                case SessionState.NoSession:
                case SessionState.Closed:
                case SessionState.ClosedPending:
                    ReturnError(szFunction, a_apicmd, "closedSession", null, -1);
                    return (false);
            }

            // Validate...
            if (m_ipcTwainDirectOnTwain == null)
            {
                ReturnError(szFunction, a_apicmd, "closedSession", null, -1);
                return (false);
            }

            // Close the scanner...
            m_ipcTwainDirectOnTwain.Write
            (
                "{" +
                "\"method\":\"closeSession\"" +
                "}"
            );

            // Get the result, we're not going to check it, though, because
            // we're going to close the session no matter what happens with
            // the TWAIN driver...
            m_ipcTwainDirectOnTwain.Read();

            // Exit the process...
            m_ipcTwainDirectOnTwain.Write
            (
                "{" +
                "\"method\":\"exit\"" +
                "}"
            );

            // We'll only fully shutdown if we have no outstanding
            // images, so the close reply should tell us that, then
            // we can issue and exit to shut it down.  If we know
            // that the session is closed, then the releaseImageBlocks
            // function is the one that'll do the final shutdown when
            // the last block is released...

            // Shut down the process...
            m_ipcTwainDirectOnTwain.Close();
            m_ipcTwainDirectOnTwain = null;

            // Make sure the process is gone...
            if (!m_processTwainDirectOnTwain.WaitForExit(5000))
            {
                m_processTwainDirectOnTwain.Kill();
            }
            m_processTwainDirectOnTwain = null;

            // Reply to the command with a session object...
            blSuccess = UpdateSession(szFunction, a_apicmd, SessionState.Closed);
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "communicationError", null, -1);
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Create a new scanning session...
        /// </summary>
        /// <param name="a_apicmd">the command the caller sent</param>
        /// <returns>true on success</returns>
        private bool DeviceScannerCreateSession(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            long lErrorErrorIndex;
            string szIpc;
            string szArguments;
            string szTwainDirectOnTwain;
            string szFunction = "DeviceScannerCreateSession";

            // Init stuff...
            szTwainDirectOnTwain = Config.Get("executablePath", "");
            szTwainDirectOnTwain = szTwainDirectOnTwain.Replace("TwainDirectScanner", "TwainDirectOnTwain");

            // State check...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    Log.Error(szFunction + ": unrecognized state..." + m_twainlocalsession.GetSessionState());
                    ReturnError(szFunction, a_apicmd, "notReady", null, -1);
                    return (false);
                case SessionState.Ready:
                case SessionState.Capturing:
                case SessionState.Full:
                case SessionState.ClosedPending:
                    ReturnError(szFunction, a_apicmd, "newSessionNotAllowed", null, -1);
                    return (false);
                case SessionState.NoSession:
                case SessionState.Closed:
                    break;
            }

            // We're already running a session, so let's not try to
            // add a new one...
            if (m_ipcTwainDirectOnTwain != null)
            {
                ReturnError(szFunction, a_apicmd, "newSessionNotAllowed", null, -1);
                return (false);
            }

            // Create an IPC...
            if (m_ipcTwainDirectOnTwain == null)
            {
                m_ipcTwainDirectOnTwain = new Ipc("socket|" + IPAddress.Loopback.ToString() + "|0", true);
            }

            // Arguments to the progream...
            szArguments = "ipc=\"" + m_ipcTwainDirectOnTwain.GetConnectionInfo() + "\"";

            // Get ready to start the child process...
            m_processTwainDirectOnTwain = new Process();
            m_processTwainDirectOnTwain.StartInfo.UseShellExecute = false;
            m_processTwainDirectOnTwain.StartInfo.WorkingDirectory = Path.GetDirectoryName(szTwainDirectOnTwain);
            m_processTwainDirectOnTwain.StartInfo.CreateNoWindow = true;
            m_processTwainDirectOnTwain.StartInfo.RedirectStandardOutput = false;
            if (TwainLocalScanner.GetPlatform() == Platform.WINDOWS)
            {
                m_processTwainDirectOnTwain.StartInfo.FileName = szTwainDirectOnTwain;
                m_processTwainDirectOnTwain.StartInfo.Arguments = szArguments;
            }
            else
            {
                m_processTwainDirectOnTwain.StartInfo.FileName = "/usr/bin/mono";
                m_processTwainDirectOnTwain.StartInfo.Arguments = "\"" + szTwainDirectOnTwain + "\"" + " " + szArguments;
            }
            m_processTwainDirectOnTwain.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            // Log what we're doing...
            Log.Info("run>>> " + m_processTwainDirectOnTwain.StartInfo.FileName);
            Log.Info("run>>> " + m_processTwainDirectOnTwain.StartInfo.Arguments);

            // Start the child process.
            m_processTwainDirectOnTwain.Start();

            // Monitor our new process...
            m_ipcTwainDirectOnTwain.MonitorPid(m_processTwainDirectOnTwain.Id);
            m_ipcTwainDirectOnTwain.Accept();

            // Open the scanner...
            string szCommand =
                "{" +
                "\"method\":\"createSession\"," +
                "\"scanner\":\"" + m_twainlocalsession.DeviceRegisterGetTwainLocalTy() + "\"" +
                "}";
            m_ipcTwainDirectOnTwain.Write(szCommand);

            // Get the result...
            JsonLookup jsonlookup = new JsonLookup();
            szIpc = m_ipcTwainDirectOnTwain.Read();
            blSuccess = jsonlookup.Load(szIpc, out lErrorErrorIndex);
            if (!blSuccess)
            {
                // Exit the process...
                m_ipcTwainDirectOnTwain.Write
                (
                    "{" +
                    "\"method\":\"exit\"" +
                    "}"
                );
                m_processTwainDirectOnTwain.WaitForExit(5000);
                m_processTwainDirectOnTwain.Close();
                m_processTwainDirectOnTwain = null;
                ReturnError(szFunction, a_apicmd, "invalidJson", null, lErrorErrorIndex);
                return (false);
            }

            // Handle errors...
            if (jsonlookup.Get("status") != "success")
            {
                // Exit the process...
                m_ipcTwainDirectOnTwain.Write
                (
                    "{" +
                    "\"method\":\"exit\"" +
                    "}"
                );
                m_processTwainDirectOnTwain.WaitForExit(5000);
                m_processTwainDirectOnTwain.Close();
                m_processTwainDirectOnTwain = null;
                ReturnError(szFunction, a_apicmd, jsonlookup.Get("status"), null, -1);
                return (false);
            }

            // Update the ApiCmd command object...
            a_apicmd.UpdateUsingIpcData(jsonlookup, false);

            // Reply to the command with a session object, this is where we create our
            // session id, public session id and set the revision to 0...
            m_twainlocalsession.m_szSessionId = DateTime.Now.Ticks.ToString();
            m_twainlocalsession.m_lSessionRevision = 0;
            blSuccess = UpdateSession(szFunction, a_apicmd, SessionState.Ready);
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "communicationError", null, -1);
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Get the current info on a scanning session...
        /// </summary>
        /// <param name="a_apicmd">our command object</param>
        /// <returns>true on success</returns>
        private bool DeviceScannerGetSession(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            long lResponseCharacterOffset;
            string szIpc;
            string szFunction = "DeviceScannerGetSession";

            // State check...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    Log.Error(szFunction + ": unrecognized state..." + m_twainlocalsession.GetSessionState());
                    ReturnError(szFunction, a_apicmd, "notReady", null, -1);
                    return (false);
                case SessionState.Ready:
                case SessionState.Capturing:
                case SessionState.Full:
                case SessionState.ClosedPending:
                    break;
                case SessionState.NoSession:
                case SessionState.Closed:
                    if (m_ipcTwainDirectOnTwain == null)
                    {
                        ReturnError(szFunction, a_apicmd, "closedSession", null, -1);
                        return (false);
                    }
                    break;
            }

            // Validate...
            if (m_ipcTwainDirectOnTwain == null)
            {
                Log.Error(szFunction + ": m_ipcTwainDirectOnTwain is null...");
                ReturnError(szFunction, a_apicmd, "invalidSessionId", null, -1);
                return (false);
            }

            // Get the current session info...
            m_ipcTwainDirectOnTwain.Write
            (
                "{" +
                "\"method\":\"getSession\"" +
                "}"
            );

            // Get the result...
            JsonLookup jsonlookup = new JsonLookup();
            szIpc = m_ipcTwainDirectOnTwain.Read();
            if (!jsonlookup.Load(szIpc, out lResponseCharacterOffset))
            {
                ReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                return (false);
            }

            // Update the ApiCmd command object...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    a_apicmd.UpdateUsingIpcData(jsonlookup, false);
                    break;
                case SessionState.Capturing:
                case SessionState.Full:
                    a_apicmd.UpdateUsingIpcData(jsonlookup, true);
                    break;
            }    

            // Reply to the command with a session object...
            blSuccess = UpdateSession(szFunction, a_apicmd, m_twainlocalsession.GetSessionState());
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "communicationError", null, -1);
                return (false);
            }

            // Parse it...
            if (!string.IsNullOrEmpty(a_apicmd.HttpResponseData()))
            {
                blSuccess = jsonlookup.Load(a_apicmd.HttpResponseData(), out lResponseCharacterOffset);
                if (!blSuccess)
                {
                    Log.Error(szFunction + ": error parsing the reply...");
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Get an image...
        /// </summary>
        /// <param name="a_apicmd">command object</param>
        /// <returns>true on success</returns>
        private bool DeviceScannerReadImageBlock(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            bool blWithMetadata;
            long lResponseCharacterOffset;
            string szIpc;
            string szFunction = "DeviceScannerReadImageBlock";

            // State check...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    Log.Error(szFunction + ": unrecognized state..." + m_twainlocalsession.GetSessionState());
                    ReturnError(szFunction, a_apicmd, "notCapturing", null, -1);
                    return (false);
                case SessionState.Ready:
                    ReturnError(szFunction, a_apicmd, "notCapturing", null, -1);
                    return (false);
                case SessionState.Capturing:
                case SessionState.Full:
                case SessionState.ClosedPending:
                    break;
                case SessionState.NoSession:
                case SessionState.Closed:
                    if (m_ipcTwainDirectOnTwain == null)
                    {
                        ReturnError(szFunction, a_apicmd, "closedSession", null, -1);
                        return (false);
                    }
                    break;
            }

            // Do we want the metadata?
            blWithMetadata = false;
            if (a_apicmd.GetJsonReceived("params.withMetadata") == "true")
            {
                blWithMetadata = true;
            }

            // Pass the data along to our helper...
            m_ipcTwainDirectOnTwain.Write
            (
                "{" +
                "\"method\":\"readImageBlock\"," +
                (blWithMetadata ? "\"withMetadata\": true," : "") +
                "\"imageBlockNum\":\"" + a_apicmd.GetJsonReceived("params.imageBlockNum") + "\"" +
                "}"
            );

            // Get the result...
            JsonLookup jsonlookup = new JsonLookup();
            szIpc = m_ipcTwainDirectOnTwain.Read();
            if (!jsonlookup.Load(szIpc, out lResponseCharacterOffset))
            {
                ReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                return (false);
            }

            // Update the ApiCmd command object...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    a_apicmd.UpdateUsingIpcData(jsonlookup, false);
                    break;
                case SessionState.Capturing:
                case SessionState.Full:
                    a_apicmd.UpdateUsingIpcData(jsonlookup, true);
                    break;
            }

            // Reply to the command with a session object...
            blSuccess = UpdateSession(szFunction, a_apicmd, m_twainlocalsession.GetSessionState());
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "communicationError", null, -1);
                return (false);
            }

            // Parse it...
            if (!string.IsNullOrEmpty(a_apicmd.HttpResponseData()))
            {
                blSuccess = jsonlookup.Load(a_apicmd.HttpResponseData(), out lResponseCharacterOffset);
                if (!blSuccess)
                {
                    Log.Error(szFunction + ": error parsing the reply...");
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Get TWAIN Direct metadata for an image...
        /// </summary>
        /// <param name="a_apicmd">our command object</param>
        /// <returns>true on success</returns>
        private bool DeviceScannerReadImageBlockMetadata(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            bool blWithThumbnail = false;
            long lResponseCharacterOffset;
            string szIpc;
            string szFunction = "DeviceScannerReadImageBlockMetadata";

            // State check...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    Log.Error(szFunction + ": unrecognized state..." + m_twainlocalsession.GetSessionState());
                    ReturnError(szFunction, a_apicmd, "notCapturing", null, -1);
                    return (false);
                case SessionState.Ready:
                    ReturnError(szFunction, a_apicmd, "notCapturing", null, -1);
                    return (false);
                case SessionState.Capturing:
                case SessionState.Full:
                case SessionState.ClosedPending:
                    break;
                case SessionState.NoSession:
                case SessionState.Closed:
                    if (m_ipcTwainDirectOnTwain == null)
                    {
                        ReturnError(szFunction, a_apicmd, "closedSession", null, -1);
                        return (false);
                    }
                    break;
            }

            // Do we want a thumbnail?
            if (a_apicmd.GetJsonReceived("params.withThumbnail") == "true")
            {
                blWithThumbnail = true;
            }

            // Pass this along to our helper process...
            m_ipcTwainDirectOnTwain.Write
            (
                "{" +
                "\"method\":\"readImageBlockMetadata\"," +
                "\"imageBlockNum\":\"" + a_apicmd.GetJsonReceived("params.imageBlockNum") + "\"," +
                "\"withThumbnail\":" + (blWithThumbnail ? "true" : "false") +
                "}"
            );

            // Get the result...
            JsonLookup jsonlookup = new JsonLookup();
            szIpc = m_ipcTwainDirectOnTwain.Read();
            if (!jsonlookup.Load(szIpc, out lResponseCharacterOffset))
            {
                ReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                return (false);
            }

            // Update the ApiCmd command object...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    a_apicmd.UpdateUsingIpcData(jsonlookup, false);
                    break;
                case SessionState.Capturing:
                case SessionState.Full:
                    a_apicmd.UpdateUsingIpcData(jsonlookup, true);
                    break;
            }

            // Reply to the command with a session object...
            blSuccess = UpdateSession(szFunction, a_apicmd, m_twainlocalsession.GetSessionState());
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "communicationError", null, -1);
                return (false);
            }

            // Parse it...
            if (!string.IsNullOrEmpty(a_apicmd.HttpResponseData()))
            {
                blSuccess = jsonlookup.Load(a_apicmd.HttpResponseData(), out lResponseCharacterOffset);
                if (!blSuccess)
                {
                    Log.Error(szFunction + ": error parsing the reply...");
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Release an image or a range of images...
        /// </summary>
        /// <param name="a_apicmd">command object</param>
        /// <returns>true on success</returns>
        private bool DeviceScannerReleaseImageBlocks(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            long lResponseCharacterOffset;
            string szIpc;
            string szFunction = "DeviceScannerReleaseImageBlocks";

            // State check...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    Log.Error(szFunction + ": unrecognized state..." + m_twainlocalsession.GetSessionState());
                    ReturnError(szFunction, a_apicmd, "notCapturing", null, -1);
                    return (false);
                case SessionState.Ready:
                    ReturnError(szFunction, a_apicmd, "notCapturing", null, -1);
                    return (false);
                case SessionState.Capturing:
                case SessionState.Full:
                case SessionState.ClosedPending:
                    break;
                case SessionState.NoSession:
                case SessionState.Closed:
                    if (m_ipcTwainDirectOnTwain == null)
                    {
                        ReturnError(szFunction, a_apicmd, "closedSession", null, -1);
                        return (false);
                    }
                    break;
            }

            // Get the current session info...
            m_ipcTwainDirectOnTwain.Write
            (
                "{" +
                "\"method\":\"releaseImageBlocks\"," +
                "\"imageBlockNum\":\"" + a_apicmd.GetJsonReceived("params.imageBlockNum") + "\"," +
                "\"lastImageBlockNum\":\"" + a_apicmd.GetJsonReceived("params.lastImageBlockNum") + "\"" +
                "}"
            );

            // Get the result...
            JsonLookup jsonlookup = new JsonLookup();
            szIpc = m_ipcTwainDirectOnTwain.Read();
            blSuccess = jsonlookup.Load(szIpc, out lResponseCharacterOffset);
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                return (false);
            }

            // Update the ApiCmd command object...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    a_apicmd.UpdateUsingIpcData(jsonlookup, false);
                    break;
                case SessionState.Capturing:
                case SessionState.Full:
                    a_apicmd.UpdateUsingIpcData(jsonlookup, true);
                    break;
            }

            // if the session has been closed and we have no more images,
            // then we need to close down twaindirect on twain...

            // Reply to the command with a session object...
            blSuccess = UpdateSession(szFunction, a_apicmd, m_twainlocalsession.GetSessionState());
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "communicationError", null, -1);
                return (false);
            }

            // Parse it...
            if (!string.IsNullOrEmpty(a_apicmd.HttpResponseData()))
            {
                blSuccess = jsonlookup.Load(a_apicmd.HttpResponseData(), out lResponseCharacterOffset);
                if (!blSuccess)
                {
                    Log.Error(szFunction + ": error parsing the reply...");
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Set the TWAIN Direct options...
        /// </summary>
        /// <param name="a_jsonlookup">data from the application/cloud</param>
        /// <returns>true on success</returns>
        private bool DeviceScannerSetTwainDirectOptions(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            long lResponseCharacterOffset;
            string szIpc;
            string szStatus;
            string szFunction = "DeviceScannerSetTwainDirectOptions";

            // State check, we're allowing this to happen in more
            // than just the ready state to support custom vendor
            // actions.  The current TWAIN Direct actions can only
            // be used in the Ready state...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    Log.Error(szFunction + ": unrecognized state..." + m_twainlocalsession.GetSessionState());
                    ReturnError(szFunction, a_apicmd, "notReady", null, -1);
                    return (false);
                case SessionState.Ready:
                case SessionState.Capturing:
                case SessionState.Full:
                case SessionState.ClosedPending:
                    break;
                case SessionState.NoSession:
                case SessionState.Closed:
                    ReturnError(szFunction, a_apicmd, "closedSession", null, -1);
                    return (false);
            }

            // Set the TWAIN Direct options...
            m_ipcTwainDirectOnTwain.Write
            (
                "{" +
                "\"method\":\"setTwainDirectOptions\"," +
                "\"task\":" + a_apicmd.GetJsonReceived("params.task") +
                "}"
            );

            // Get the result...
            JsonLookup jsonlookup = new JsonLookup();
            szIpc = m_ipcTwainDirectOnTwain.Read();
            blSuccess = jsonlookup.Load(szIpc, out lResponseCharacterOffset);
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                return (false);
            }

            // Check the status...
            szStatus = jsonlookup.Get("status");
            if (szStatus != "success")
            {
                switch (szStatus)
                {
                    default:
                        ReturnError(szFunction, a_apicmd, szStatus, null, -1);
                        break;
                    case "invalidCapturingOptions":
                        ReturnError(szFunction, a_apicmd, "invalidTwainDirectTask", jsonlookup.Get("jsonKey"), -1);
                        break;
                }
                return (false);
            }

            // Update the ApiCmd command object...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    a_apicmd.UpdateUsingIpcData(jsonlookup, false);
                    break;
                case SessionState.Capturing:
                case SessionState.Full:
                    a_apicmd.UpdateUsingIpcData(jsonlookup, true);
                    break;
            }

            // Reply to the command with a session object...
            blSuccess = UpdateSession(szFunction, a_apicmd, m_twainlocalsession.GetSessionState());
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                return (false);
            }

            // Parse it...
            if (!string.IsNullOrEmpty(a_apicmd.HttpResponseData()))
            {
                blSuccess = jsonlookup.Load(a_apicmd.HttpResponseData(), out lResponseCharacterOffset);
                if (!blSuccess)
                {
                    Log.Error(szFunction + ": error parsing the reply...");
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Start capturing images...
        /// </summary>
        /// <param name="a_apicmd">the command we're processing</param>
        /// <returns>true on success</returns>
        private bool DeviceScannerStartCapturing(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            long lResponseCharacterOffset;
            string szIpc;
            string szFunction = "DeviceScannerStartCapturing";

            // State check...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    Log.Error(szFunction + ": unrecognized state..." + m_twainlocalsession.GetSessionState());
                    ReturnError(szFunction, a_apicmd, "notReady", null, -1);
                    return (false);
                case SessionState.Ready:
                    break;
                case SessionState.Capturing:
                case SessionState.Full:
                    ReturnError(szFunction, a_apicmd, "notReady", null, -1);
                    return (false);
                case SessionState.NoSession:
                case SessionState.Closed:
                case SessionState.ClosedPending:
                    ReturnError(szFunction, a_apicmd, "closedSession", null, -1);
                    return (false);
            }

            // Start capturing...
            m_ipcTwainDirectOnTwain.Write
            (
                "{" +
                "\"method\":\"startCapturing\"" +
                "}"
            );

            // Get the result...
            JsonLookup jsonlookup = new JsonLookup();
            szIpc = m_ipcTwainDirectOnTwain.Read();
            blSuccess = jsonlookup.Load(szIpc, out lResponseCharacterOffset);
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                return (false);
            }

            // Update the ApiCmd command object...
            a_apicmd.UpdateUsingIpcData(jsonlookup, false);

            // Reply to the command with a session object...
            blSuccess = UpdateSession(szFunction, a_apicmd, SessionState.Capturing);
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "communicationError", null, -1);
                return (false);
            }

            // Parse it...
            if (!string.IsNullOrEmpty(a_apicmd.HttpResponseData()))
            {
                blSuccess = jsonlookup.Load(a_apicmd.HttpResponseData(), out lResponseCharacterOffset);
                if (!blSuccess)
                {
                    Log.Error(szFunction + ": error parsing the reply...");
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Gracefully stop capturing images...
        /// </summary>
        /// <param name="a_apicmd">command object</param>
        /// <returns>true on success</returns>
        private bool DeviceScannerStopCapturing(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            long lResponseCharacterOffset;
            string szIpc;
            string szFunction = "DeviceScannerStopCapturing";

            // State check...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    Log.Error(szFunction + ": unrecognized state..." + m_twainlocalsession.GetSessionState());
                    ReturnError(szFunction, a_apicmd, "notCapturing", null, -1);
                    return (false);
                case SessionState.Ready:
                    ReturnError(szFunction, a_apicmd, "notCapturing", null, -1);
                    return (false);
                case SessionState.Capturing:
                case SessionState.Full:
                    break;
                case SessionState.NoSession:
                case SessionState.Closed:
                case SessionState.ClosedPending:
                    ReturnError(szFunction, a_apicmd, "closedSession", null, -1);
                    return (false);
            }

            // Stop capturing...
            m_ipcTwainDirectOnTwain.Write
            (
                "{" +
                "\"method\":\"stopCapturing\"" +
                "}"
            );

            // Get the result...
            JsonLookup jsonlookup = new JsonLookup();
            szIpc = m_ipcTwainDirectOnTwain.Read();
            blSuccess = jsonlookup.Load(szIpc, out lResponseCharacterOffset);
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                return (false);
            }

            // Update the ApiCmd command object...
            switch (m_twainlocalsession.GetSessionState())
            {
                default:
                    a_apicmd.UpdateUsingIpcData(jsonlookup, false);
                    break;
                case SessionState.Capturing:
                case SessionState.Full:
                    a_apicmd.UpdateUsingIpcData(jsonlookup, true);
                    break;
            }

            // If endofjob is true, we're done...
            //if (    (jsonlookup.Get("endOfJob") == "true")
            //    &&  (m_twainlocalsession.GetSessionState() == SessionState.Capturing))
            //{
            //    m_twainlocalsession.SetSessionState(SessionState.Ready);
            //}

            // If we're out of images, we can go to a ready state, otherwise go to
            // closedPending...
            if (a_apicmd.GetEndOfJob() && string.IsNullOrEmpty(a_apicmd.GetImageBlocksJson()))
            {
                m_twainlocalsession.SetSessionState(SessionState.Ready);
            }
            else
            {
                m_twainlocalsession.SetSessionState(SessionState.ClosedPending);
            }

            // Reply to the command with a session object...
            blSuccess = UpdateSession(szFunction, a_apicmd, m_twainlocalsession.GetSessionState());
            if (!blSuccess)
            {
                ReturnError(szFunction, a_apicmd, "communicationError", null, -1);
                return (false);
            }

            // Parse it...
            if (!string.IsNullOrEmpty(a_apicmd.HttpResponseData()))
            {
                blSuccess = jsonlookup.Load(a_apicmd.HttpResponseData(), out lResponseCharacterOffset);
                if (!blSuccess)
                {
                    Log.Error(szFunction + ": error parsing the reply...");
                    return (false);
                }
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
        /// Ways of getting to the server...
        /// </summary>
        private enum HttpMethod
        {
            Undefined,
            Curl,
            WebRequest
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
        private enum HttpReplyStyle
        {
            Undefined,
            SimpleReply,
            SimpleReplyWithSessionInfo,
            Event
        }

        /// <summary>
        /// TWAIN Local Scanner API session states...
        /// </summary>
        private enum SessionState
        {
            NoSession,
            Ready,
            Capturing,
            Full,
            ClosedPending,
            Closed
        }

        /// <summary>
        /// TWAIN Local session information that we need to keep track of...
        /// </summary>
        private class TwainLocalSession
        {
            /// <summary>
            /// Init stuff...
            /// </summary>
            /// <param name="a_twainlocal">needed for our timer</param>
            /// <param name="a_timercallbackEvent">callback function for XMPP stuff</param>
            /// <param name="a_objectEvent">object that supplied the callback function</param>
            public TwainLocalSession
            (
                TwainLocalScanner a_twainlocalscanner,
                TimerCallback a_timercallbackEvent,
                object a_objectEvent
            )
            {
                // Callback stuff...
                m_timercallbackEvent = a_timercallbackEvent;
                m_objectEvent = a_objectEvent;

                // Our state...
                m_sessionstate = SessionState.NoSession;

                // The session object...
                m_szSessionId = null;
                m_lSessionRevision = 0;
                m_szPreviousSessionObject = "";
                m_aiSessionImageBlocks = null;
                m_iCommandIdCount = 0;

                // Metadata...
                m_szMetadata = null;

                // The place we'll keep our device information...
                m_deviceregister = new DeviceRegister();

                // Scratchpad stuff...
                m_szAuthorizationCode = "";
                m_szRegistrationId = "";
                m_szCommandId = "";
            }

            /// <summary>
            /// Create a unique command id...
            /// </summary>
            /// <returns>the device id</returns>
            public string ClientCreateCommandId()
            {
                return (Process.GetCurrentProcess().Id + "-" + Thread.CurrentThread.ManagedThreadId + "-" + Environment.TickCount + "-" + (m_iCommandIdCount++).ToString());
            }

            /// <summary>
            /// Add a device or modify the contents of an existing device.  We
            /// add data in bits and pieces, so expect to see this call made
            /// more than once.  We use two keys: the device name and the device
            /// id...
            /// </summary>
            /// <param name="a_szTwainLocalTy">TWAIN Local ty= field</param>
            /// <param name="a_szTwainLocalSerialNumber">TWAIN serial number (from CAP_SERIALNUMBER)</param>
            /// <param name="a_szTwainLocalNote">User's friendly name</param>
            public void DeviceRegisterSet
            (
                string a_szTwainLocalTy,
                string a_szTwainLocalSerialNumber,
                string a_szTwainLocalNote
            )
            {
                m_deviceregister.Set
                (
                    a_szTwainLocalTy,
                    a_szTwainLocalSerialNumber,
                    a_szTwainLocalNote
                );
            }

            /// <summary>
            /// Get the device id, we're using the instance name for now...
            /// </summary>
            /// <returns>the device id</returns>
            public string DeviceRegisterGetDeviceId()
            {
                return (m_deviceregister.GetTwainLocalInstanceName());
            }

            /// <summary>
            /// Get the TWAIN Local ty= field
            /// </summary>
            /// <returns>the vendors friendly name</returns>
            public string DeviceRegisterGetTwainLocalTy()
            {
                return (m_deviceregister.GetTwainLocalTy());
            }

            /// <summary>
            /// Get the TWAIN Local instance name...
            /// </summary>
            /// <returns>the mDNS instance name</returns>
            public string DeviceRegisterGetTwainLocalInstanceName()
            {
                return (m_deviceregister.GetTwainLocalInstanceName());
            }

            /// <summary>
            /// Get the TWAIN Local note= field...
            /// </summary>
            /// <returns>the users friendly name</returns>
            public string DeviceRegisterGetTwainLocalNote()
            {
                return (m_deviceregister.GetTwainLocalNote());
            }

            /// <summary>
            /// Load data from a file...
            /// </summary>
            /// <param name="a_szFile">the file to load it from</param>
            /// <returns>try if successful</returns>
            public bool DeviceRegisterLoad(TwainLocalScanner a_twainlocalscanner, string a_szFile)
            {
                return (m_deviceregister.Load(a_szFile));
            }

            /// <summary>
            /// Clear the device register...
            /// </summary>
            public void DeviceRegisterClear()
            {
                m_deviceregister.Clear();
            }

            /// <summary>
            /// Persist the data to a file...
            /// </summary>
            /// <param name="a_szFile">the file to save the data in</param>
            /// <returns>true if successful</returns>
            public bool Save(string a_szFile)
            {
                return (m_deviceregister.Save(a_szFile));
            }

            /// <summary>
            /// Get the session state...
            /// </summary>
            /// <returns></returns>
            public SessionState GetSessionState()
            {
                return (m_sessionstate);
            }

            /// <summary>
            /// Set the session state...
            /// </summary>
            /// <param name="a_sessionstate"></param>
            public void SetSessionState(SessionState a_sessionstate)
            {
                if (m_sessionstate != a_sessionstate)
                {
                    Log.Info("SetSessionState: " + m_sessionstate + " --> " + a_sessionstate);
                }
                m_sessionstate = a_sessionstate;
            }

            /// <summary>
            /// IN:  parameters.id
            /// OUT: results.session.sessionId
            /// This is the unique "secret" id that the scanner provides in response
            /// to a CreateSession command.  The scanner uses it to make sure that
            /// commands that it receives belong to this session...
            /// </summary>
            public string m_szSessionId;

            /// <summary>
            /// OUT:  results.session.revision
            /// Report when a change has been made to the session object.
            /// The most obvious use is when the state changes, or the
            /// imageBlocks data is being updated...
            /// </summary>
            public long m_lSessionRevision;

            /// <summary>
            /// This holds a JSON subset of the session object, so that we can
            /// detect changes and then update the revision number...
            /// </summary>
            public string m_szPreviousSessionObject;

            /// <summary>
            /// OUT:  results.session.imageBlocks
            /// Reports the index values of image blocks that are ready for transfer
            /// to the client...
            /// </summary>
            public int[] m_aiSessionImageBlocks;

            // Metadata...
            public string m_szMetadata;

            // The id of the current scan command...
            public string m_szCommandId;

            // Temporary content...
            public string m_szAuthorizationCode;
            public string m_szRegistrationId;

            // Persistant device information...
            private DeviceRegister m_deviceregister;

            // Our state...
            private SessionState m_sessionstate;

            // Callback stuff...
            private TimerCallback m_timercallbackEvent;
            private object m_objectEvent;

            /// <summary>
            /// Counter for the commandId...
            /// </summary>
            private int m_iCommandIdCount;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// All of the data we need to use TWAIN Local...
        /// </summary>
        private TwainLocalSession m_twainlocalsession;

        /// <summary>
        /// Information about our device...
        /// </summary>
        private Dnssd.DnssdDeviceInfo m_dnssddeviceinfo;

        /// <summary>
        /// Our HTTP server name...
        /// </summary>
        private string m_szHttpServer;

        /// <summary>
        /// Our HTTP server...
        /// </summary>
        private HttpServer m_httpserver;

        /// <summary>
        /// Privet requires this in the header for every
        /// command, except privet/info (which returns the
        /// value used by all other commands)...
        /// </summary>
        private string m_szXPrivetToken;

        /// <summary>
        /// Our token in the clear (for comparisons)...
        /// </summary>
        private string m_szXPrivetTokenClear;

        /// <summary>
        /// Timestamp of the last time someone asked for
        /// /privet/info while the state was NoSession...
        /// </summary>
        private long m_lPrivetTokenTimestamp;

        /// <summary>
        /// A place to store data, like logs and stuff...
        /// </summary>
        private string m_szWriteFolder;

        /// <summary>
        /// A place to store images and metadata...
        /// </summary>
        private string m_szImagesFolder;

        /// <summary>
        /// Our current platform...
        /// </summary>
        private static Platform ms_platform = Platform.UNKNOWN;

        /// <summary>
        /// Ipc object...
        /// </summary>
        private Ipc m_ipcTwainDirectOnTwain;

        /// <summary>
        /// Handle XMPP events...
        /// </summary>
        private TimerCallback m_timercallbackEvent;

        /// <summary>
        /// Object that provided the callback event...
        /// </summary>
        private object m_objectEvent;

        /// <summary>
        /// Use this to confirm a scan request...
        /// </summary>
        private ConfirmScan m_confirmscan;

        /// <summary>
        /// So we can have a bigger form...
        /// </summary>
        private float m_fConfirmScanScale;

        /// <summary>
        /// Process...
        /// </summary>
        private Process m_processTwainDirectOnTwain;

        /// <summary>
        /// Command timeout, this should be short (and in milliseconds)...
        /// </summary>
        private int m_iHttpTimeoutCommand;

        /// <summary>
        /// Data timeout, this should be long (and in milliseconds)...
        /// </summary>
        private int m_iHttpTimeoutData;

        /// <summary>
        /// Event timeout, this should be long (and in milliseconds)...
        /// </summary>
        private int m_iHttpTimeoutEvent;

        #endregion
    }
}
