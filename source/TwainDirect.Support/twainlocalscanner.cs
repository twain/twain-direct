///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.Support.TwainLocalScannerDevice
// TwainDirect.Support.TwainLocalScannerClient
// TwainDirect.Support.TwainLocalScanner
// TwainDirect.Support.TwainLocalScanner.FileSystemWatcherHelper
// TwainDirect.Support.TwainLocalScanner.TwainLocalSession
//
// Interface to TWAIN Local scanners.  This module is used by applications and
// scanners, since they share enough common features to make it worthwhile to
// consolodate the functionality.  Hopefully, it also helps to make things a
// little more clear as to what's going on.  However, they are split across
// classes, with TwainLocalScanner providing content common to both.
//
// Functions used by applications are marked as "Client" and functions used by
// scanners are marked as "Device".  Functions common to both have no designation.
//
// There's no obvious reason to expose the Session class, so it's buried inside
// of TwainLocalScanner.  One device can support more than one session, so data
// that's session specific must be located in this class.
//
// ApiCmd is the payload for an ApiCmd command.  We must support multi-threading,
// so we need to be able to pass its objects up and down the stack.  This is why
// it's publically accessible.
//
// The scanner manages the state machine.  Clients could check state, but that's
// not recommended.  A client's state comes from the scanner.
//
// TWAIN Local moves image data around using imageBlocks.  An imageBlock holds
// either all or part of an image. TwainDirect.OnTwain generates .twpdf and
// .twmeta files inside of TwainDirect.Scanner's twimages folder.  These files
// are split into one or more imageBlocks by TwainDirect.Scanner and moves into
// its tdimages folder.  This constitutes the array of imageBlocks, which the
// application must transfer.  TwainDirect.Scanner also generates the thumbnail
// file, if it's asked for.  The thumbnail file contains the metadata for the
// final imageBlock.  When files are moved from twimages to tdimages, they no
// longer exist in twimages.
//
// TwainDirect.App and TwainDirect.Certification get data from TwainDirect.Scanner's
// tdimages folder (meta, pdf, and thumbnail).  When they release an imageBlock
// the corresponding files are deleted in TwainDirect.Scanner's tdimages folder,
// and this updates the session.imageBlocks array.  These data are given .td*
// names in the images folder (ex: .tdpdf, .tdmeta, _thumbnail.tdpdf).
//
// The ClientFinishImage() function examines the metadata, and when it sees that
// it has all of the (.tdmeta) imageBlocks for a complete image it stitches the
// .tdpdf files into a single .pdf file with a basename representing the current
// image count.  The .tdmeta and _thumbnail.pdf (if present) are renamed to have
// the same basename as the .pdf file.  This operating deletes the .td* files.
//
// Here's an example, we're tranferring two images, the first one takes up three
// image blocks, the second one takes two:
// TwainDirect.OnTwain      TwainDirect.Scanner     imageBlock      TwainDirect.App
// img000001.twpdf          img0000001.tdpdf        1               
//                          img0000002.tdpdf        2
//                          img0000003.tdpdf        3               img000001.pdf
// img000002.twpdf          img0000004.tdpdf        4   
//                          img0000005.tdpdf        5               img000002.pdf
//
// There are two reasons for splitting up the images.  First, if the image is too
// large it may fail to be transferred, especially on wifi networks.  Smaller blocks
// have greater success of completing transfer.  Second, the application has the
// option to ask for multiple imageBlocks, which boosts performance by avoiding the
// transaction delays that occur between transations.  If the application asks for
// imageBlock 1, transfers it, and then asks for imageBlock 2, there's a period of
// time when no data is being transferred.  Instead the application should ask for
// imageBlock 1 and imageBlock 2 (and maybe more), and as soon as any of them are
// complete it should immediately ask for the next imageBlock.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    15-Oct-2016     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2016-2018 Kodak Alaris Inc.
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
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;
[assembly: CLSCompliant(true)]

// This namespace supports applications and scanners...
namespace TwainDirect.Support
{
    /// <summary>
    /// TWAIN Local support for the Device (TWAIN Bridge)...
    /// </summary>
    public sealed class TwainLocalScannerDevice : TwainLocalScanner
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods

        /// <summary>
        /// Init us...
        /// </summary>
        /// <param name="a_confirmscan">user must confirm a scan request</param>
        /// <param name="a_fConfirmScanScale">scale the confirmation dialog</param>
        /// <param name="a_displaycallback">display callback</param>
        /// <param name="a_blCreateTwainLocalSession">true for the server only</param>
        public TwainLocalScannerDevice
        (
            ConfirmScan a_confirmscan,
            float a_fConfirmScanScale,
            DisplayCallback a_displaycallback,
            bool a_blCreateTwainLocalSession
        ) : base
        (
            a_blCreateTwainLocalSession
        )
        {
            bool blSuccess;

            // Save the confirmscan callback, if given one...
            m_confirmscan = a_confirmscan;

            // So we can change the size of the confirmation dialog...
            m_fConfirmScanScale = a_fConfirmScanScale;

            // Keep this for the life of the bridge...
            m_szDeviceSecret = Guid.NewGuid().ToString();

            // Used to display status on the console...
            m_displaycallback = a_displaycallback;

            // Get our folder paths and clean them out...
            m_szTdImagesFolder = Path.Combine(m_szWriteFolder, "tdimages");
            m_szTwImagesFolder = Path.Combine(m_szWriteFolder, "twimages");
            m_szImageBlocksDrainedMeta = Path.Combine(m_szTwImagesFolder, "imageBlocksDrained.meta");
            Log.Info("TWAIN images folder (input):         " + m_szTwImagesFolder);
            Log.Info("TWAIN Direct images folder (output): " + m_szTdImagesFolder);
            blSuccess = CleanImageFolders();
            if (!blSuccess)
            {
                throw new Exception("Can't set up the tdimages/twimages folders...");
            }

            // Our locks...
            m_objectLockDeviceApi = new object();
            m_objectLockDeviceHttpServerStop = new object();
            m_objectLockOnChangedBridge = new object();

            // Init our idle session timeout...
            long lSessionTimeout = 300000; // five minutes
            m_lSessionTimeout = Config.Get("sessionTimeout", lSessionTimeout);
            if (m_lSessionTimeout < 10000)
            {
                m_lSessionTimeout = lSessionTimeout;
            }

            // Create the timer we'll use for expiring sessions...
            m_timerSession = new Timer(DeviceSessionTimerCallback, this, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Dispatch a command.  Commands sent by applications show up here as
        /// callbacks.  So in theory we're architected to handle multiple commands
        /// arriving at the same time, since each one should be appearing inside
        /// of its own thread.  That means we have to protect some data structures,
        /// such as the session object (which includes the current state).
        /// 
        /// We ignore commands that aren't meant for us.  That is, that don't
        /// satisfy the URI requirements.  We validate the X-Privet-Token before
        /// taking any other action.
        /// 
        /// The info and infoex commands are handled first, since we're likely to
        /// see more of them than any other command.
        /// 
        /// After that we're processing /privet/twaindirect/session commands.  We
        /// validate our X-Privet-Token (received from a prior call to info or
        /// infoex) and if that goes well we parse the JSON.
        /// 
        /// There are three properties: kind, commandId, and method.  The kind
        /// identifies the format of the REST command.  At this time we only
        /// understand twainlocalscanner.  twaincloudscanner should show up at
        /// some point.
        /// 
        /// The commandId is a unique GUID for every command, and is part of our
        /// strategy for making commands idempotent, and for cleanly handling
        /// requests and responses in different threads.  This is an idea that is
        /// still in development at this time (14-Aug-2017), and will probably be
        /// fully realized in the TwainDirect.MobileApp before it gets here.
        /// 
        /// The method is the TWAIN Local command, we dispatch each of those to a
        /// function.
        /// 
        /// </summary>
        /// <param name="a_szJsonCommand">the command we received</param>
        /// <param name="a_httplistenercontext">thr HTTP object that delivered the command</param>
        /// <returns>true on success</returns>
        public void DeviceDispatchCommand
        (
            string a_szJsonCommand,
            ref HttpListenerContext a_httplistenercontext
        )
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
                &&  (szUri != "/privet/infoex")
                &&  (szUri != "/privet/twaindirect/session"))
            {
                return;
            }

            // Every command must have X-Privet-Token in the header...
            for (ii = 0; ii < a_httplistenercontext.Request.Headers.Count; ii++)
            {
                if (a_httplistenercontext.Request.Headers.GetKey(ii) == "X-Privet-Token")
                {
                    break;
                }
            }
            if (ii >= a_httplistenercontext.Request.Headers.Count)
            {
                apicmd = new ApiCmd(null, null, ref a_httplistenercontext);
                DeviceReturnError(szFunction, apicmd, "invalid_x_privet_token", null, 0);
                return;
            }

            // We found X-Privet-Token, squirrel away the value, remove any double quotes...
            szXPrivetToken = a_httplistenercontext.Request.Headers.Get(ii).Replace("\"", "");

            // Handle the /privet/info and /privet/infoex commands...
            if ((szUri == "/privet/info")
                || (szUri == "/privet/infoex"))
            {
                // Log it...
                Log.Info("");
                Log.Info("http>>> " + szUri.Replace("/privet/", ""));
                Log.Info("http>>> " + a_httplistenercontext.Request.HttpMethod + " uri " + a_httplistenercontext.Request.Url.AbsoluteUri);

                // Get each header and display each value.
                NameValueCollection namevaluecollectionHeaders = a_httplistenercontext.Request.Headers;
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

                // Run it...
                apicmd = new ApiCmd(null, null, ref a_httplistenercontext);
                DeviceInfo(ref apicmd);
                return;
            }

            // If we get here, it implies that a command has been issued before making
            // a call to info or infoex, so we'll reject it.  This is technically a
            // state violation, but invalid_x_privet_token takes priority...
            if (string.IsNullOrEmpty(szXPrivetToken))
            {
                apicmd = new ApiCmd(null, null, ref a_httplistenercontext);
                DeviceReturnError(szFunction, apicmd, "invalid_x_privet_token", null, -1);
                return;
            }

            // The rest of this must be coming in on /privet/twaindirect/session,
            // we'll start by validating our X-Privet-Token.  We check the session
            // first, because if it has the token, it wins...
            else if ((m_twainlocalsession != null) && (szXPrivetToken == m_twainlocalsession.GetXPrivetToken()))
            {
                // Woot! We're good, keep going...
            }

            // We should only come here if we don't have a session with a token,
            // which means this should be a createSession command...
            else
            {
                bool blValid = false;
                long lXPrivetTokenTicks;

                // Crack the token open, if it looks valid, and if its timestamp falls
                // inside of our window, we'll take it.  The window is small, just 30
                // seconds, but it can be overridden, if needed...
                if (!string.IsNullOrEmpty(szXPrivetToken))
                {
                    // Get at the ticks...
                    string[] aszTokens = szXPrivetToken.Split(new string[] { ":" }, StringSplitOptions.None);
                    if ((aszTokens != null) && (aszTokens.Length == 2) && (aszTokens[1] != null) && long.TryParse(aszTokens[1], out lXPrivetTokenTicks))
                    {
                        // Check the ticks against our current tick count...
                        long lCurrentTicks = DateTime.Now.Ticks;
                        if ((lCurrentTicks >= lXPrivetTokenTicks) && (((lCurrentTicks - lXPrivetTokenTicks) / TimeSpan.TicksPerSecond) < Config.Get("createSessionWindow", 30000)))
                        {
                            // So far so good, now see if we can generate the same token
                            // from the data we have...
                            string szTest = CreateXPrivetToken(lXPrivetTokenTicks);
                            if (szXPrivetToken == szTest)
                            {
                                blValid = true;
                            }
                        }
                    }
                }

                // Nope, we're done...
                if (!blValid)
                {
                    apicmd = new ApiCmd(null, null, ref a_httplistenercontext);
                    DeviceReturnError(szFunction, apicmd, "invalid_x_privet_token", null, 0);
                    return;
                }
            }

            // Parse the JSON...
            long lResponseCharacterOffset;
            JsonLookup jsonlookup = new JsonLookup();
            blSuccess = jsonlookup.Load(a_szJsonCommand, out lResponseCharacterOffset);
            if (!blSuccess)
            {
                apicmd = new ApiCmd(null, jsonlookup, ref a_httplistenercontext);
                DeviceReturnError(szFunction, apicmd, "invalidJson", null, lResponseCharacterOffset);
                return;
            }

            // Init our API command object, this will track our progress
            // and receieve either the result or any errors...
            apicmd = new ApiCmd(null, jsonlookup, ref a_httplistenercontext);

            // Validate the kind property, we only support twainlocalscanner at this time...
            if (jsonlookup.Get("kind") != "twainlocalscanner")
            {
                DeviceReturnError(szFunction, apicmd, "invalidValue", "kind", -1);
                return;
            }

            // Validate the commandId property, it must be present, and it must be a GUID...
            Guid guidCommandid;
            if (!Guid.TryParse(jsonlookup.Get("commandId"), out guidCommandid))
            {
                DeviceReturnError(szFunction, apicmd, "invalidValue", "commandId", -1);
                return;
            }

            // We'll handle method further down...

            // If we are running a session, make sure that the command's session id matches
            // our session's id...
            lock (m_objectLockDeviceApi)
            {
                // If we have no session, and we're not processing "createSession" then
                // we have a problem.  We can get here if the session timeout was hit...
                if ((m_twainlocalsession == null) && (jsonlookup.Get("method") != "createSession"))
                {
                    Log.Error(szFunction + ": sessionId error: <" + jsonlookup.Get("params.sessionId") + "> <(no session)>");
                    DeviceReturnError(szFunction, apicmd, "invalidSessionId", null, -1);
                    return;
                }

                // If we have a session, and the command is "createSession", then we're
                // busy, so bug off...
                if ((m_twainlocalsession != null) && (jsonlookup.Get("method") == "createSession"))
                {
                    Log.Error(szFunction + ": busy, we're already running a session");
                    DeviceReturnError(szFunction, apicmd, "busy", null, -1);
                    return;
                }

                // If we have a session, the call must match our sessionId...
                if ((m_twainlocalsession != null) && !string.IsNullOrEmpty(m_twainlocalsession.GetSessionId()))
                {
                    if (jsonlookup.Get("params.sessionId") != m_twainlocalsession.GetSessionId())
                    {
                        Log.Error(szFunction + ": sessionId error: <" + jsonlookup.Get("params.sessionId") + "> <" + m_twainlocalsession.GetSessionId() + ">");
                        DeviceReturnError(szFunction, apicmd, "invalidSessionId", null, -1);
                        return;
                    }
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
                Log.Info("http>>> recvdata  " + a_szJsonCommand);
            }

            // Dispatch the command...
            switch (jsonlookup.Get("method"))
            {
                default:
                    DeviceReturnError(szFunction, apicmd, "invalidValue", "method", -1);
                    return;

                case "closeSession":
                    DeviceScannerCloseSession(ref apicmd);
                    break;

                case "createSession":
                    DeviceScannerCreateSession(ref apicmd, szXPrivetToken);
                    break;

                case "getSession":
                    DeviceScannerGetSession(ref apicmd, false, false, null);
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

                case "sendTask":
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
                            Log.Error(szFunction + ": JSON property is missing...");
                            DeviceReturnError(szFunction, apicmd, "invalidJson", null, 0);
                            return;

                        // We found the property, but it's not an object...
                        default:
                            iTaskIndex = a_szJsonCommand.IndexOf("\"task\":") + 7;
                            Log.Error(szFunction + ": JSON must be an object...");
                            DeviceReturnError(szFunction, apicmd, "invalidJson", null, iTaskIndex);
                            return;
                    }

                    // Go ahead and process it...
                    DeviceScannerSendTask(ref apicmd);
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
                        else
                        {
                            DeviceReturnError(szFunction, apicmd, "aborted", null, -1);
                        }
                    }
                    break;

                case "stopCapturing":
                    DeviceScannerStopCapturing(ref apicmd);
                    break;

                case "waitForEvents":
                    DeviceScannerWaitForEvents(ref apicmd);
                    break;
            }

            // All done...
            return;
        }

        /// <summary>
        /// The DNS name of the user owning the current createSession...
        /// </summary>
        /// <returns></returns>
        public string DeviceGetSessionUserDns()
        {
            if (m_twainlocalsession != null)
            {
                return (m_twainlocalsession.GetCallersHostName());
            }
            return ("");
        }

        /// <summary>
        /// Start monitoring for HTTP commands...
        /// </summary>
        /// <returns></returns>
        public bool DeviceHttpServerStart()
        {
            int iPort;
            bool blSuccess;

            // We already have one of these...
            if (m_httpserver != null)
            {
                return (true);
            }

            // Get our port...
            if (!int.TryParse(Config.Get("usePort", "34034"), out iPort))
            {
                Log.Error("DeviceHttpServerStart: bad port..." + Config.Get("usePort", "34034"));
                return (false);
            }

            // Validate values, note is optional, so we don't test it...
            if (string.IsNullOrEmpty(m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalInstanceName()))
            {
                Log.Error("DeviceHttpServerStart: bad instance name...");
                return (false);
            }
            if (iPort == 0)
            {
                Log.Error("DeviceHttpServerStart: bad port...");
                return (false);
            }
            if (string.IsNullOrEmpty(m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalTy()))
            {
                Log.Error("DeviceHttpServerStart: bad ty...");
                return (false);
            }

            // Create our server...
            m_httpserver = new HttpServer();

            // Start us up...
            blSuccess = m_httpserver.ServerStart
            (
                DeviceDispatchCommand,
                m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalInstanceName(),
                iPort,
                m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalTy(),
                "",
                m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalNote()
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
            lock (m_objectLockDeviceHttpServerStop)
            {
                // Nothing to do...
                if (m_httpserver == null)
                {
                    return;
                }

                // Shut it down...
                m_httpserver.ServerStop();
                m_httpserver = null;
            }
        }

        /// <summary>
        /// Register a device.
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
                m_twainlocalsessionInfo.DeviceRegisterClear();
                return (true);
            }

            // Get the scanner entry...
            string szScanner = "scanners[" + a_iScanner + "]";

            // Collect our data...
            string szDeviceName = a_jsonlookup.Get(szScanner + ".twidentityProductName");
            if (string.IsNullOrEmpty(szDeviceName))
            {
                szDeviceName = a_jsonlookup.Get(szScanner + ".sane");
            }
            string szHostName = a_jsonlookup.Get(szScanner + ".hostName");
            string szSerialNumber = a_jsonlookup.Get(szScanner + ".serialNumber");
            string szScannerRecord = a_jsonlookup.Get(szScanner);

            // Set the register.txt file...
            try
            {
                m_twainlocalsessionInfo.DeviceRegisterSet(szDeviceName, szSerialNumber, a_szNote, szScannerRecord);
            }
            catch
            {
                DeviceReturnError("DeviceRegister", a_apicmd, "invalidJson", null, 0);
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
            if (!m_twainlocalsessionInfo.DeviceRegisterLoad(Path.Combine(m_szWriteFolder, "register.txt")))
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
            return (m_twainlocalsessionInfo.DeviceRegisterSave(Path.Combine(m_szWriteFolder, "register.txt")));
        }

        /// <summary>
        /// Our device session exited in some unexpected fashion.  This
        /// might happen if the TWAIN driver crashes...
        /// <param name="a_blUserShutdown">the user requested the close</param>
        /// </summary>
        public void DeviceSessionExited(bool a_blUserShutdown)
        {
            // We only need to do this if we're running a session...
            if ((m_twainlocalsession != null) && (m_twainlocalsession.GetSessionState() != SessionState.noSession))
            {
                // Change state...
                SetSessionState(SessionState.noSession, "Session critical...", a_blUserShutdown);

                // Tell the application that we're in trouble...
                DeviceSendEvent("critical", true);

                // Make a note of what we're doing...
                Log.Error("DeviceSessionExited: session critical...");

                // Give the system time to deliver the message, otherwise
                // what will happen is the client will see that it's lost
                // communication.  Which it should interpret as the loss of the
                // session.  This is just a nicer way of getting there...
                Thread.Sleep(1000);

                // Scrag the session...
                EndSession();
            }
        }

        /// <summary>
        /// End a session, and do all the cleanup work...
        /// </summary>
        public override void EndSession()
        {
            // Let the base cleanup...
            base.EndSession();

            // Cleanup the device stuff..
            if (m_timerEvent != null)
            {
                m_timerEvent.Change(Timeout.Infinite, Timeout.Infinite);
                m_timerEvent = null;
            }
            if (m_timerSession != null)
            {
                m_timerSession.Change(Timeout.Infinite, Timeout.Infinite);
            }

            // Lose the session...
            if (m_twainlocalsession != null)
            {
                m_twainlocalsession.Dispose();
                m_twainlocalsession = null;
            }

            // Lose this...
            if (m_apicmdEvent != null)
            {
                m_apicmdEvent = null;
            }

            // Try to leave nothing behind...
            CleanImageFolders();
        }

        /// <summary>
        /// Return the note= field...
        /// </summary>
        /// <returns>users friendly name</returns>
        public string GetTwainLocalNote()
        {
            return (m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalNote());
        }

        /// <summary>
        /// Access the device name
        /// </summary>
        /// <returns>TWAIN Local ty= field</returns>
        public string GetTwainLocalTy()
        {
            return (m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalTy());
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions

        /// <summary>
        /// Prompt the user to confirm a request to scan...
        /// </summary>
        /// <returns>button the user pressed</returns>
        public delegate ButtonPress ConfirmScan(float a_fConfirmScanScale);

        /// <summary>
        /// Display callback...
        /// </summary>
        /// <param name="a_szText">text to display</param>
        public delegate void DisplayCallback(string a_szText);

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods

        /// <summary>
        /// One stop shop for handling state transitions.  All functions come here
        /// to figure this out.  The combination of a function and a status causes
        /// us to work out a transition, if there is one.
        /// 
        /// Some status values can force us to make a transition, regardless of the
        /// current function.
        /// 
        /// Note that this function only determines the state we want to be in, it
        /// does not set that state.  That's because we have to report the state
        /// before fully acting on it, like with noSession.
        /// </summary>
        /// <param name="a_szFunction"></param>
        /// <param name="a_szStatus"></param>
        /// <returns></returns>
        private SessionState DeviceUpdateSessionState(string a_szFunction, string a_szStatus)
        {
            // If we have no session object, then we have no session...
            if (m_twainlocalsession == null)
            {
                return (SessionState.noSession);
            }

            // A critical status sends us to noSession...
            if (a_szStatus == "critical")
            {
                return (SessionState.noSession);
            }

            // If the session timed out, we go to noSession...
            if (a_szStatus == "sessionTimedOut")
            {
                return (SessionState.noSession);
            }

            // Any other non-success status leaves us in the current state...
            if (a_szStatus != "success")
            {
                return (m_twainlocalsession.GetSessionState());
            }

            // If our state is draining or closed, or if the method calling
            // us is stopCapturing, closeSession, or releaseImageBlocks then
            // we need to check our drain status.  We'd like OnChangedBridge
            // to do this for us, but there's a race condition between the
            // time the OS hits the callback, and this call, so we need to
            // be explict about it...
            if (    (a_szFunction == "stopCapturing")
                ||  (a_szFunction == "closeSession")
                ||  (a_szFunction == "releaseImageBlocks")
                ||  (m_twainlocalsession.GetSessionState() == SessionState.draining)
                ||  (m_twainlocalsession.GetSessionState() == SessionState.closed))
            {
                // Check if we're done capturing, this involves seeing the drained
                // file in either folder...
                if (!m_twainlocalsession.GetSessionDoneCapturing())
                {
                    if (    File.Exists(Path.Combine(m_szTdImagesFolder, "imageBlocksDrained.meta"))
                        ||  File.Exists(Path.Combine(m_szTwImagesFolder, "imageBlocksDrained.meta")))
                    {
                        m_twainlocalsession.SetSessionDoneCapturing(true);
                    }
                }

                // Check if we're drained.  We have to be done capturing, and we
                // can't find any image block files...
                if (    m_twainlocalsession.GetSessionDoneCapturing()
                    &&  !m_twainlocalsession.GetSessionImageBlocksDrained())
                {
                    string[] aszTd = Directory.GetFiles(m_szTdImagesFolder, "*.*pdf");
                    string[] aszTw = Directory.GetFiles(m_szTwImagesFolder, "*.tw*");
                    if (    ((aszTd == null) || (aszTd.Length == 0))
                        &&  ((aszTw == null) || (aszTw.Length == 0)))
                    {
                        m_twainlocalsession.SetSessionImageBlocksDrained(true);
                    }
                }
            }

            // Handle the functions...
            switch (a_szFunction)
            {
                // If we don't recognize the function, leave the state alone...
                default:
                    return (m_twainlocalsession.GetSessionState());

                // closeSession
                // If we have no pending images we go to noSession, otherwise
                // we go to closed.  It doesn't matter if this is issued in
                // a capturing or a draining state, the result is the same...
                case "closeSession":
                    if (m_twainlocalsession.GetSessionImageBlocksDrained())
                    {
                        return (SessionState.noSession);
                    }
                    return (SessionState.closed);

                // createSession
                // On success we always go to ready...
                case "createSession":
                    return (SessionState.ready);

                // getSession and waitForEvents
                // We make our decision based on the current state...
                case "getSession":
                case "waitForEvents":
                case "releaseImageBlocks":
                    switch (m_twainlocalsession.GetSessionState())
                    {
                        // If we don't handle it, just return the current state...
                        default:
                            return (m_twainlocalsession.GetSessionState());

                        // If we're capturing, see if we've drained all of the
                        // images, and if so, go to ready.  Otherwise stay in
                        // the current capturing state...
                        case SessionState.capturing:
                            if (m_twainlocalsession.GetSessionImageBlocksDrained())
                            {
                                //return (SessionState.ready);
                            }
                            return (SessionState.capturing);

                        // If we're draining, see if we've drained all of the
                        // images, and if so, go to ready.  Otherwise stay in
                        // the current draining state...
                        case SessionState.draining:
                            if (m_twainlocalsession.GetSessionImageBlocksDrained())
                            {
                                return (SessionState.ready);
                            }
                            return (SessionState.draining);

                        // If we're closed, see if we've drained all of the
                        // images, and if so, go to noSession.  Otherwise stay in
                        // the current closed state...
                        case SessionState.closed:
                            if (m_twainlocalsession.GetSessionImageBlocksDrained())
                            {
                                return (SessionState.noSession);
                            }
                            return (SessionState.closed);
                    }

                // readImageBlockMetadata
                // We always stay in the current state...
                case "readImageBlockMetadata":
                    return (m_twainlocalsession.GetSessionState());

                // readImageBlock
                // We always stay in the current state...
                case "readImageBlock":
                    return (m_twainlocalsession.GetSessionState());

                // sendTask
                // We always stay in the current state...
                case "sendTask":
                    return (m_twainlocalsession.GetSessionState());

                // startCapturing
                // On success we always go to capturing...
                case "startCapturing":
                    return (SessionState.capturing);

                // stopCapturing
                // If we have no pending images we go to ready, otherwise
                // we go to draining....
                case "stopCapturing":
                    if (m_twainlocalsession.GetSessionImageBlocksDrained())
                    {
                        return (SessionState.ready);
                    }
                    return (SessionState.draining);
            }
        }

        /// <summary>
        /// Remove files from our image folders...
        /// </summary>
        /// <returns>true on success</returns>
        private bool CleanImageFolders()
        {
            string[] aszFiles;

            // Scrub the TWAIN Direct folder that we use to send data
            // to the application...
            try
            {
                if (!Directory.Exists(m_szTdImagesFolder))
                {
                    Directory.CreateDirectory(m_szTdImagesFolder);
                }
                else
                {
                    aszFiles = Directory.GetFiles(m_szTdImagesFolder, "*.*");
                    foreach (string szFile in aszFiles)
                    {
                        File.Delete(szFile);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error("CleanImageFolders: CreateDirectory tdimages failed..." + exception.Message);
                return (false);
            }

            // Scrub the TWAIN Bridge folder that we use to receive
            // data from the TWAIN driver...
            try
            {
                if (!Directory.Exists(m_szTwImagesFolder))
                {
                    Directory.CreateDirectory(m_szTwImagesFolder);
                }
                else
                {
                    aszFiles = Directory.GetFiles(m_szTwImagesFolder, "*.*");
                    foreach (string szFile in aszFiles)
                    {
                        File.Delete(szFile);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error("CleanImageFolders: CreateDirectory twimages failed..." + exception.Message);
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Create an X-Privet-Token, we do this to generate a brand new value,
        /// and we do it to recreate a value that we want to validate...
        /// </summary>
        /// <param name="a_lTicks">0 to generate a new one, or the ticks from a previously created token</param>
        /// <returns>the token</returns>
        public string CreateXPrivetToken(long a_lTicks = 0)
        {
            long lTicks;
            string szXPrivetToken;

            // Use our ticks, this is for validation...
            if (a_lTicks > 0)
            {
                lTicks = a_lTicks;
            }

            // Otherwise use the clock, this is for generation...
            else
            {
                lTicks = DateTime.Now.Ticks;
            }

            // This is what's recommended...
            // XSRF_token = base64( SHA1(device_secret + DELIMITER + issue_timecounter) + DELIMITER + issue_timecounter )      
            szXPrivetToken = m_szDeviceSecret + ":" + lTicks;
            using (SHA1Managed sha1managed = new SHA1Managed())
            {
                byte[] abHash = sha1managed.ComputeHash(Encoding.UTF8.GetBytes(szXPrivetToken));
                szXPrivetToken = Convert.ToBase64String(abHash);
            }
            szXPrivetToken += ":" + lTicks;

            // All done...
            return (szXPrivetToken);
        }

        /// <summary>
        /// Return error information from a device function...
        /// </summary>
        /// <param name="a_szReason">our caller</param>
        /// <param name="a_apicmd">info about the command</param>
        /// <param name="a_szCode">the status code</param>
        /// <param name="a_szJsonKey">json key to point of error or null</param>
        /// <param name="a_lResponseCharacterOffset">character offset of json error or -1</param>
        /// <returns>true on success</returns>
        private bool DeviceReturnError(string a_szReason, ApiCmd a_apicmd, string a_szCode, string a_szJsonKey, long a_lResponseCharacterOffset)
        {
            bool blSuccess;
            string szResponse;

            // Log it...
            Log.Error
            (
                a_szReason + ": error code=" + a_szCode +
                (!string.IsNullOrEmpty(a_szJsonKey) ? " key=" + a_szJsonKey : "") +
                ((a_lResponseCharacterOffset >= 0) ? " offset=" + a_lResponseCharacterOffset : "")
            );

            // If we don't have an ApiCmd to respond to, we're done...
            if (a_apicmd == null)
            {
                return (true);
            }

            // Handle a JSON error...
            if (string.IsNullOrEmpty(a_szCode) || (a_szCode == "invalidJson"))
            {
                // Our base response...
                szResponse =
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + a_apicmd.GetCommandId() + "\"," +
                    "\"method\":\"" + a_apicmd.GetCommandName() + "\"," +
                    "\"results\":{" +
                    "\"success\":false," +
                    "\"code\":\"" + "invalidJson" + "\"," +
                    "\"characterOffset\":" + a_lResponseCharacterOffset +
                    "}" + // results
                    "}"; //root
            }

            // If it's an invalidTask, then include that data...
            else if (a_szCode == "invalidTask")
            {
                szResponse =
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + a_apicmd.GetCommandId() + "\"," +
                    "\"method\":\"" + a_apicmd.GetCommandName() + "\"," +
                    "\"results\":{" +
                    "\"success\":true," +
                    "\"session\":{" +
                    "\"sessionId\":\"" + m_twainlocalsession.GetSessionId() + "\"," +
                    "\"revision\":\"" + m_twainlocalsession.GetSessionRevision() + "\"," +
                    "\"state\":\"" + m_twainlocalsession.GetSessionState() + "\"," +
                    "\"status\":{" +
                    "\"success\":" + (m_twainlocalsession.GetSessionStatusSuccess() ? "true" : "false") + "," +
                    "\"detected\":\"" + m_twainlocalsession.GetSessionStatusDetected() + "\"" +
                    "}," + // status
                    "\"task\":" + a_szJsonKey +
                    "}" + // session
                    "}" + // results
                    "}"; //root
            }

            // Anything else...
            else
            {
                // Our base response...
                szResponse =
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + a_apicmd.GetCommandId() + "\"," +
                    "\"method\":\"" + a_apicmd.GetCommandName() + "\"," +
                    "\"results\":{" +
                    "\"success\":false," +
                    "\"code\":\"" + a_szCode + "\"" +
                    "}" + // results
                    "}"; //root
            }

            // Send the response...
            blSuccess = a_apicmd.HttpRespond(a_szCode, szResponse);
            if (!blSuccess)
            {
                Log.Error("Lost connection...");
                SetSessionState(SessionState.noSession, "Lost connection...");
                EndSession();
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Queue an event for waitForEvents to send...
        /// </summary>
        /// <param name="a_szEvent">the event to send</param>
        /// <param name="a_blAllowEventWithNoSession">needed for sessionTimeout and critical events</param>
        private void DeviceSendEvent
        (
            string a_szEvent,
            bool a_blAllowEventWithNoSession = false
        )
        {
            // Guard us...
            lock (m_objectLockDeviceApi)
            {
                // We only have an event if we have a session...
                if (m_twainlocalsession != null)
                {
                    ApiCmd apicmd = new ApiCmd(null);
                    m_twainlocalsession.SetSessionRevision(m_twainlocalsession.GetSessionRevision() + 1);
                    apicmd.SetEvent(a_szEvent, m_twainlocalsession.GetSessionState().ToString(), m_twainlocalsession.GetSessionRevision());
                    DeviceUpdateSession("DeviceSendEvent", m_apicmdEvent, true, apicmd, m_twainlocalsession.GetSessionRevision(), a_szEvent, a_blAllowEventWithNoSession);
                    m_apicmdEvent = null;
                }
            }
        }

        /// <summary>
        /// Our device event timeout callback...
        /// </summary>
        /// <param name="a_objectState"></param>
        internal void DeviceEventTimerCallback(object a_objectState)
        {
            // Get our scanner object...
            TwainLocalScannerDevice twainlocalscannerdevice = (TwainLocalScannerDevice)a_objectState;

            // We shouldn't be here...
            if ((m_timerEvent == null) || (m_twainlocalsession == null) || (m_twainlocalsession.GetSessionState() == SessionState.noSession))
            {
                if (m_timerEvent != null)
                {
                    m_timerEvent.Change(Timeout.Infinite, Timeout.Infinite);
                }
                return;
            }

            // Turn us off...
            m_timerEvent.Change(Timeout.Infinite, Timeout.Infinite);

            // Tell the application that this event has timed out, so it
            // needs to set up a new one...
            twainlocalscannerdevice.DeviceSendEvent("timeout", false);
        }

        /// <summary>
        /// Our device session timeout callback...
        /// </summary>
        /// <param name="a_objectState"></param>
        internal void DeviceSessionTimerCallback(object a_objectState)
        {
            // Our scanner object...
            TwainLocalScannerDevice twainlocalscannerdevice = (TwainLocalScannerDevice)a_objectState;

            // Set the state...
            twainlocalscannerdevice.SetSessionState(SessionState.noSession, "Session timeout...");

            // Send an event to let the app know that it's tooooooo late...
            twainlocalscannerdevice.DeviceSendEvent("sessionTimedOut", true);

            // Make a note of what we're doing...
            Log.Error("DeviceSessionTimerCallback: session timeout...");

            // Give the system time to deliver the message, otherwise
            // what will happen is the client will see that it's lost
            // communication.  Which it should interpret as the loss of the
            // session.  This is just a nicer way of getting there...
            Thread.Sleep(1000);

            // Scrag the session...
            EndSession();
        }

        /// <summary>
        /// Refresh our session timer...
        /// </summary>
        public void DeviceSessionRefreshTimer()
        {
            m_timerSession.Change(Timeout.Infinite, Timeout.Infinite);
            m_timerSession.Change(m_lSessionTimeout, Timeout.Infinite);
        }

        /// <summary>
        /// Try to shutdown TWAIN Direct on TWAIN...
        /// </summary>
        /// <param name="a_blForce">force the shutdown</param>
        private void DeviceShutdownTwainDirectOnTwain(bool a_blForce)
        {
            // Apparently we've already done this...
            if ((m_twainlocalsession == null) || (m_twainlocalsession.GetIpcTwainDirectOnTwain() == null))
            {
                return;
            }

            //
            // We'll only fully shutdown if we have no outstanding
            // images, so the close reply should tell us that, then
            // we can issue and exit to shut it down.  If we know
            // that the session is closed, then the releaseImageBlocks
            // function is the one that'll do the final shutdown when
            // the last block is released...
            if (!a_blForce && (m_twainlocalsession != null) && (m_twainlocalsession.GetSessionState() != SessionState.noSession))
            {
                return;
            }

            // Make sure we don't trigger our exit event handler...
            m_twainlocalsession.GetProcessTwainDirectOnTwain().Exited -= new EventHandler(TwainLocalScanner_Exited);

            // Exit the process, give it a second...
            m_twainlocalsession.GetIpcTwainDirectOnTwain().Close();
            Thread.Sleep(1000);

            // Shut down the process...
            if (m_twainlocalsession.GetIpcTwainDirectOnTwain() != null)
            {
                m_twainlocalsession.GetIpcTwainDirectOnTwain().Dispose();
                m_twainlocalsession.SetIpcTwainDirectOnTwain(null);
            }

            // Make sure the process is gone...
            if (m_twainlocalsession.GetProcessTwainDirectOnTwain() != null)
            {
                // Log what we're doing...
                Log.Info("kill>>> " + m_twainlocalsession.GetProcessTwainDirectOnTwain().StartInfo.FileName);
                Log.Info("kill>>> " + m_twainlocalsession.GetProcessTwainDirectOnTwain().StartInfo.Arguments);

                // Wait a bit for it...
                if (!m_twainlocalsession.GetProcessTwainDirectOnTwain().WaitForExit(5000))
                {
                    m_twainlocalsession.GetProcessTwainDirectOnTwain().Kill();
                }
                m_twainlocalsession.SetProcessTwainDirectOnTwain(null);
            }
        }

        /// <summary>
        /// Update the session object...
        /// </summary>
        /// <param name="a_szReason">something for logging</param>
        /// <param name="a_apicmd">the command object we're working on</param>
        /// <param name="a_apicmdEvent">the command object with event data</param>
        /// <param name="a_lSessionRevision">the current session revision</param>
        /// <param name="a_szEventName">name of an event (or null)</param>
        /// <param name="a_blAllowEventWithNoSession">pretty much what it says</param>
        /// <returns>true on success</returns>
        private bool DeviceUpdateSession
        (
            string a_szReason,
            ApiCmd a_apicmd,
            bool a_blWaitForEvents,
            ApiCmd a_apicmdEvent,
            long a_lSessionRevision,
            string a_szEventName,
            bool a_blAllowEventWithNoSession = false
        )
        {
            long ii;
            bool blSuccess;
            string szResponse;
            string szSessionObjects;
            string szEventsArray = "";
            ApiCmd apicmd;
            SessionState sessionstate;
            SessionState sessionstateOnEntry;

            // Get the session state we entered with...
            if (m_twainlocalsession == null)
            {
                sessionstateOnEntry = SessionState.noSession;
            }
            else
            {
                sessionstateOnEntry = m_twainlocalsession.GetSessionState();
            }

            //////////////////////////////////////////////////
            // We're responding to the /privet/info or the
            // /privet/infoex command...
            #region We're responding to the /privet/info command...
            if ((a_apicmd != null) && ((a_apicmd.GetUri() == "/privet/info") || (a_apicmd.GetUri() == "/privet/infoex")))
            {
                string szDeviceState;
                string szManufacturer;
                string szModel;
                string szSerialNumber;
                string szFirmware;
                long longUptime;
                Dnssd.DnssdDeviceInfo dnssddeviceinfo = GetDnssdDeviceInfo();

                // Our uptime is from when the process started...
                longUptime = (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;

                // Device state...
                if (m_twainlocalsession == null)
                {
                    szDeviceState = "idle";
                    szManufacturer = m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalManufacturer();
                    szModel = m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalProductName();
                    szSerialNumber = m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalSerialNumber();
                    szFirmware = m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalVersion();
                }
                else
                {
                    // The user has been warned in the Privet docs not to rely
                    // on this information.  However, it does have its uses, so
                    // we'll check it out during certification...
                    switch (m_twainlocalsession.GetSessionState())
                    {
                        default: szDeviceState = "stopped"; break;
                        case SessionState.noSession: szDeviceState = "idle"; break;
                        case SessionState.capturing: szDeviceState = "processing"; break;
                        case SessionState.closed: szDeviceState = "processing"; break;
                        case SessionState.draining: szDeviceState = "processing"; break;
                        case SessionState.ready: szDeviceState = "processing"; break;
                    }

                    // This is the best we can do for this info...
                    szManufacturer = m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalManufacturer();
                    szModel = m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalProductName();
                    szSerialNumber = m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalSerialNumber();
                    szFirmware = m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalVersion();

                    // Protection...
                    if (string.IsNullOrEmpty(szManufacturer))
                    {
                        szManufacturer = "(no manufacturer)";
                    }
                    if (string.IsNullOrEmpty(szModel))
                    {
                        szModel = "(no model)";
                    }
                    if (string.IsNullOrEmpty(szSerialNumber))
                    {
                        szSerialNumber = "(no serial number)";
                    }
                }

                // Add additional data for infoex...
                string szInfoex = "";
                if (a_apicmd.GetUri() == "/privet/infoex")
                {
                    szInfoex =
                        "," +
                        "\"clouds\":[" +
                        "]";
                }

                // Construct a response, always make a new X-Privet-Token...
                szResponse =
                    "{" +
                    "\"version\":\"1.0\"," +
                    "\"name\":\"" + dnssddeviceinfo.GetTxtTy() + "\"," +
                    "\"description\":\"" + dnssddeviceinfo.GetTxtNote() + "\"," +
                    "\"url\":\"\"," +
                    "\"type\":\"" + dnssddeviceinfo.GetTxtType() + "\"," +
                    "\"id\":\"\"," +
                    "\"device_state\":\"" + szDeviceState + "\"," +
                    "\"connection_state\":\"offline\"," +
                    "\"manufacturer\":\"" + szManufacturer + "\"," +
                    "\"model\":\"" + szModel + "\"," +
                    "\"serial_number\":\"" + szSerialNumber + "\"," +
                    "\"firmware\":\"" + szFirmware + "\"," +
                    "\"uptime\":\"" + longUptime + "\"," +
                    "\"setup_url\":\"" + "" + "\"," +
                    "\"support_url\":\"" + "" + "\"," +
                    "\"update_url\":\"" + "" + "\"," +
                    "\"x-privet-token\":\"" + CreateXPrivetToken(0) + "\"," +
                    "\"api\":[" +
                    "\"/privet/twaindirect/session\"" +
                    "]," +
                    "\"semantic_state\":\"" + "" + "\"" +
                    szInfoex +
                    "}";

                // Send the response...
                blSuccess = a_apicmd.HttpRespond("success", szResponse);
                if (!blSuccess)
                {
                    Log.Error("Lost connection...");
                    SetSessionState(SessionState.noSession, "Lost connection...");
                    EndSession();
                }

                // All done...
                return (true);
            }
            #endregion

            ////////////////////////////////////////////////////////////////
            // /privet/twaindirect/session command
            #region /privet/twaindirect/session command
            if (    (a_apicmd != null)
                &&  (a_apicmd.GetUri() == "/privet/twaindirect/session")
                &&  !a_blWaitForEvents)
            {
                string szMethod = a_apicmd.GetJsonReceived("method");

                // Get our current session state, we expect a_szReason to be
                // the name of the function.  The status is either succes or
                // the code given to us by the scanner...
                if (a_apicmd.GetSessionStatusSuccess())
                {
                    sessionstate = DeviceUpdateSessionState(szMethod, "success");
                }
                else
                {
                    sessionstate = DeviceUpdateSessionState(szMethod, a_apicmd.GetSessionStatusDetected());
                }

                // Show the session state...
                if (sessionstate != sessionstateOnEntry)
                {
                    if ((sessionstateOnEntry == SessionState.noSession) && (sessionstate == SessionState.ready))
                    {
                        Display("");
                        Display("Session started by <" + a_apicmd.HttpGetCallersHostName() + ">");
                    }
                    Display(a_apicmd.HttpGetCallersHostName() + ": " + sessionstateOnEntry + " --> " + sessionstate);
                }

                // If it's not noSession, apply it immediately.  noSession has to
                // be delayed to the end of the function...
                if (sessionstate != SessionState.noSession)
                {
                    SetSessionState(sessionstate);
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

                // Update the session's status, but only once.  Put another way, after
                // startCapturing is called, we'll record one boo-boo from a RESTful,
                // command, and skip any others, until the next startCapturing is called...
                if (m_twainlocalsession.GetSessionStatusSuccess() && !a_apicmd.GetSessionStatusSuccess())
                {
                    m_twainlocalsession.SetSessionStatusSuccess(a_apicmd.GetSessionStatusSuccess());
                    m_twainlocalsession.SetSessionStatusDetected(a_apicmd.GetSessionStatusDetected());
                }

                // Start building the session object...
                szSessionObjects =
                    "\"session\":{" +
                    "\"sessionId\":\"" + m_twainlocalsession.GetSessionId() + "\"," +
                    "\"revision\":" + m_twainlocalsession.GetSessionRevision() + "," +
                    "\"state\":\"" + sessionstate.ToString() + "\"," +
                    "\"status\":{" +
                    "\"success\":" + (m_twainlocalsession.GetSessionStatusSuccess() ? "true" : "false") + "," +
                    "\"detected\":\"" + m_twainlocalsession.GetSessionStatusDetected() + "\"" +
                    "}," + // status
                    a_apicmd.GetImageBlocksJson(sessionstate.ToString());

                // Add the TWAIN Direct options, if any...
                string szTaskReply = a_apicmd.GetTaskReply();
                if (!string.IsNullOrEmpty(szTaskReply))
                {
                    szSessionObjects += "\"task\":" + szTaskReply + ",";
                }

                // End the session object...
                if (szSessionObjects.EndsWith(","))
                {
                    szSessionObjects = szSessionObjects.Substring(0, szSessionObjects.Length - 1);
                }
                szSessionObjects += "}";

                // Check to see if we have to update our revision number...
                if (    string.IsNullOrEmpty(m_twainlocalsession.GetSessionSnapshot())
                    ||  (szSessionObjects != m_twainlocalsession.GetSessionSnapshot()))
                {
                    // Replace the old revision number with the new one...
                    szSessionObjects = szSessionObjects.Replace
                    (
                        "\"revision\":" + m_twainlocalsession.GetSessionRevision() + ",",
                        "\"revision\":" + (m_twainlocalsession.GetSessionRevision() + 1) + ","
                    );
                    m_twainlocalsession.SetSessionRevision(m_twainlocalsession.GetSessionRevision() + 1);
                    m_twainlocalsession.SetSessionSnapshot(szSessionObjects);
                }

                // Construct a response...
                szResponse =
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + a_apicmd.GetCommandId() + "\"," +
                    "\"method\":\"" + a_apicmd.GetCommandName() + "\"," +
                    "\"results\":{" +
                    "\"success\":true," +
                    a_apicmd.GetMetadata() +
                    szSessionObjects +
                    "}" + // results
                    "}";  // root

                // Send the response, note that any multipart construction work
                // takes place in this function...
                blSuccess = a_apicmd.HttpRespond("success", szResponse);
                if (!blSuccess)
                {
                    Log.Error("Lost connection...");
                    SetSessionState(SessionState.noSession, "Lost connection...");
                    EndSession();
                    return (true);
                }

                // If we determined that the session is ended, handle that now...
                if (sessionstate == SessionState.noSession)
                {
                    Log.Info("Session ended...");
                    SetSessionState(SessionState.noSession, "Session ended");
                    EndSession();
                    return (true);
                }

                // All done...
                return (true);
            }
            #endregion

            ////////////////////////////////////////////////////////////////
            // /privet/twaindirect/session event
            #region /privet/twaindirect/session event
            if (    ((a_apicmd == null) || (a_apicmd.GetUri() == "/privet/twaindirect/session"))
                &&  a_blWaitForEvents)
            {
                // This should never happen, but let's be sure...
                if ((sessionstateOnEntry == SessionState.noSession) && !a_blAllowEventWithNoSession)
                {
                    return (true);
                }

                // Get our current session state...
                sessionstate = DeviceUpdateSessionState("waitForEvents", "success");

                // Show the session state...
                if (sessionstate != sessionstateOnEntry)
                {
                    if ((sessionstateOnEntry == SessionState.noSession) && (sessionstate == SessionState.ready))
                    {
                        Display("");
                        Display("Session started by <" + a_apicmd.HttpGetCallersHostName() + ">");
                    }
                    Display(a_apicmd.HttpGetCallersHostName() + ": " + sessionstateOnEntry + " --> " + sessionstate);
                }

                // If it's not noSession, apply it immediately.  noSession has to
                // be delayed to the end of the function...
                if (sessionstate != SessionState.noSession)
                {
                    SetSessionState(sessionstate, "changed in waitForEvents");
                }

                // Do we have new event data?
                if (a_apicmdEvent != null)
                {
                    // Add it to the list...
                    for (ii = 0; ii < m_twainlocalsession.GetApicmdEvents().Length; ii++)
                    {
                        if (m_twainlocalsession.GetApicmdEvents()[ii] == null)
                        {
                            a_apicmdEvent.SetEvent(a_szEventName, sessionstate.ToString(), a_lSessionRevision);
                            m_twainlocalsession.SetApicmdEvent(ii, a_apicmdEvent);
                            break;
                        }
                    }
                }

                // Expire any events that are too old, or which have a
                // revision number less than or equal to the current
                // revision number from the last waitForEvents command.
                for (ii = 0; ii < m_twainlocalsession.GetApicmdEvents().Length; ii++)
                {
                    // Grab our apicmd...
                    apicmd = m_twainlocalsession.GetApicmdEvents()[ii];

                    // All done...
                    if (apicmd == null)
                    {
                        break;
                    }

                    // Is this older than the last revision sent to us in
                    // a waitForEvents call?  If so, discard it.
                    if (apicmd.DiscardEvent(m_twainlocalsession.GetWaitForEventsSessionRevision()))
                    {
                        // Delete the item by shifting the rest of the array over it...
                        for (long jj = ii; jj < (m_twainlocalsession.GetApicmdEvents().Length - 1); jj++)
                        {
                            m_twainlocalsession.SetApicmdEvent(jj, m_twainlocalsession.GetApicmdEvents()[jj + 1]);
                        }
                        m_twainlocalsession.SetApicmdEvent(m_twainlocalsession.GetApicmdEvents().Length - 1, null);
                    }
                }

                // Sort whatever is left, so that we give it to the caller
                // in order of increasing revision numbers.  Remove any
                // duplicates.

                // Generate the event array to send to the caller,
                // if we have a place to send it.  The data is already
                // filtered and sorted, so we send all of it.
                if (a_apicmd != null)
                {
                    // We have no events to report at this time...
                    if (m_twainlocalsession.GetApicmdEvents()[0] == null)
                    {
                        // Init our event timeout for HTTPS communication, this value
                        // needs to be less than whatever is being used by the application.
                        int iDefault = 30000; // 30 seconds
                        int iHttpTimeoutEvent = (int)Config.Get("httpTimeoutEvent", iDefault);
                        if (iHttpTimeoutEvent < 10000)
                        {
                            iHttpTimeoutEvent = iDefault;
                        }

                        // Start our event timer, the default is 30 seconds,
                        // we only run once, because the application is expected
                        // to send a new waitForEvents command...
                        m_timerEvent = new Timer(DeviceEventTimerCallback, this, iHttpTimeoutEvent, Timeout.Infinite);

                        // All done...
                        return (true);
                    }

                    // Start the array...
                    szEventsArray = "\"events\":[";

                    // Add each event object...
                    szSessionObjects = "";
                    for (ii = 0; ii < m_twainlocalsession.GetApicmdEvents().Length; ii++)
                    {
                        // Grab our apicmd...
                        apicmd = m_twainlocalsession.GetApicmdEvents()[ii];

                        // We're done...
                        if (apicmd == null)
                        {
                            break;
                        }

                        // Update the session, if needed...
                        if (m_twainlocalsession.GetSessionStatusSuccess() && !apicmd.GetSessionStatusSuccess())
                        {
                            m_twainlocalsession.SetSessionStatusSuccess(apicmd.GetSessionStatusSuccess());
                            m_twainlocalsession.SetSessionStatusDetected(apicmd.GetSessionStatusDetected());
                        }

                        // We're adding to existing stuff...
                        if (!string.IsNullOrEmpty(szSessionObjects))
                        {
                            szSessionObjects += ",";
                        }

                        // Build this event...
                        szSessionObjects +=
                            "{" +
                            "\"event\":\"" + apicmd.GetEventName() + "\"," +
                            "\"session\":{" +
                            "\"sessionId\":\"" + m_twainlocalsession.GetSessionId() + "\"," +
                            "\"revision\":" + apicmd.GetSessionRevision() + "," +
                            "\"state\":\"" + apicmd.GetSessionState() + "\"," +
                            "\"status\":{" +
                            "\"success\":" + (m_twainlocalsession.GetSessionStatusSuccess() ? "true" : "false") + "," +
                            "\"detected\":\"" + m_twainlocalsession.GetSessionStatusDetected() + "\"" +
                            "}," + // status
                            apicmd.GetImageBlocksJson(sessionstate.ToString());
                        if (szSessionObjects.EndsWith(","))
                        {
                            szSessionObjects = szSessionObjects.Substring(0, szSessionObjects.Length - 1);
                        }
                        szSessionObjects += "}";
                        szSessionObjects += "}";
                    }

                    // Add the events...
                    szEventsArray += szSessionObjects;

                    // End the array...
                    szEventsArray += "]";

                    // Construct a response...
                    szResponse =
                        "{" +
                        "\"kind\":\"twainlocalscanner\"," +
                        "\"commandId\":\"" + a_apicmd.GetCommandId() + "\"," +
                        "\"method\":\"waitForEvents\"," +
                        "\"results\":{" +
                        "\"success\":true," +
                        szEventsArray +
                        "}" + // results
                        "}";  // root

                    // Send the response, note that any multipart construction work
                    // takes place in this function...
                    blSuccess = a_apicmd.HttpRespond("success", szResponse);
                    if (!blSuccess)
                    {
                        Log.Error("Lost connection...");
                        SetSessionState(SessionState.noSession, "Lost connection...");
                        EndSession();
                    }

                    // If we determined that the session is ended, handle that now...
                    if (sessionstate == SessionState.noSession)
                    {
                        Log.Info("Session ended in waitForEvents...");
                        SetSessionState(SessionState.noSession, "Session ended in waitForEvents");
                        EndSession();
                    }

                    // All done...
                    return (true);
                }
            }
            #endregion

            // Getting this far is a bad thing.  We shouldn't be here
            // unless somebody upstream fell asleep at the switch...
            Log.Error("UpdateSession: bad uri..." + ((a_apicmd != null) ? a_apicmd.GetUri() : "no apicmd"));
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
            blSuccess = DeviceUpdateSession(szFunction, a_apicmd, false, null, -1, null);
            if (!blSuccess)
            {
                DeviceReturnError(szFunction, a_apicmd, "critical", null, -1);
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
            long lResponseCharacterOffset;
            string szIpc;
            string szFunction = "DeviceScannerCloseSession";

            // Protect our stuff...
            lock (m_objectLockDeviceApi)
            {
                // Refresh our timer...
                DeviceSessionRefreshTimer();

                // State check...
                switch (m_twainlocalsession.GetSessionState())
                {
                    // These are okay...
                    case SessionState.ready:
                    case SessionState.capturing:
                    case SessionState.draining:
                        break;

                    // These are not...
                    case SessionState.noSession:
                    case SessionState.closed:
                    default:
                        DeviceReturnError(szFunction, a_apicmd, "invalidState", null, -1);
                        return (false);
                }

                // Validate...
                if ((m_twainlocalsession == null)
                    || (m_twainlocalsession.GetIpcTwainDirectOnTwain() == null))
                {
                    DeviceReturnError(szFunction, a_apicmd, "invalidSessionId", null, -1);
                    return (false);
                }

                // Close the scanner...
                m_twainlocalsession.GetIpcTwainDirectOnTwain().Write
                (
                    "{" +
                    "\"method\":\"closeSession\"" +
                    "}"
                );

                // Get the result...
                JsonLookup jsonlookup = new JsonLookup();
                szIpc = m_twainlocalsession.GetIpcTwainDirectOnTwain().Read();
                if (!jsonlookup.Load(szIpc, out lResponseCharacterOffset))
                {
                    DeviceReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                    return (false);
                }

                // Update the ApiCmd command object...
                switch (m_twainlocalsession.GetSessionState())
                {
                    default:
                        a_apicmd.UpdateUsingIpcData(jsonlookup, false, m_szTdImagesFolder, m_szTwImagesFolder);
                        break;
                    case SessionState.capturing:
                    case SessionState.draining:
                        a_apicmd.UpdateUsingIpcData(jsonlookup, true, m_szTdImagesFolder, m_szTwImagesFolder);
                        break;
                }

                // Parse it...
                if (!string.IsNullOrEmpty(a_apicmd.GetHttpResponseData()))
                {
                    blSuccess = jsonlookup.Load(a_apicmd.GetHttpResponseData(), out lResponseCharacterOffset);
                    if (!blSuccess)
                    {
                        Log.Error(szFunction + ": error parsing the reply (but we're going to continue)...");
                        // keep going, we can't lock the user into this state
                    }
                }

                // Exit the process...
                m_twainlocalsession.GetIpcTwainDirectOnTwain().Write
                (
                    "{" +
                    "\"method\":\"exit\"" +
                    "}"
                );

                // Reply to the command with a session object...
                blSuccess = DeviceUpdateSession(szFunction, a_apicmd, false, null, -1, null);
                if (!blSuccess)
                {
                    DeviceReturnError(szFunction, a_apicmd, "critical", null, -1);
                    return (false);
                }

                // Shutdown TWAIN Direct on TWAIN, but only if we've run out of
                // images...
                DeviceShutdownTwainDirectOnTwain(false);

                // If we're done, cleanup...
                if ((m_twainlocalsession == null) || (m_twainlocalsession.GetSessionState() == SessionState.noSession))
                {
                    EndSession();
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Create a new scanning session...
        /// </summary>
        /// <param name="a_apicmd">the command the caller sent</param>
        /// <param name="a_szXPrivetToken">the X-Privet-Token for this session</param>
        /// <returns>true on success</returns>
        private bool DeviceScannerCreateSession(ref ApiCmd a_apicmd, string a_szXPrivetToken)
        {
            bool blSuccess;
            long lErrorErrorIndex;
            string szIpc;
            string szArguments;
            string szTwainDirectOnTwain;
            string szFunction = "DeviceScannerCreateSession";

            // Protect our stuff...
            lock (m_objectLockDeviceApi)
            {
                // Create it if we need it...
                if (m_twainlocalsession == null)
                {
                    m_twainlocalsession = new TwainLocalSession(a_szXPrivetToken);
                    m_twainlocalsession.DeviceRegisterLoad(Path.Combine(m_szWriteFolder, "register.txt"));
                }

                // Init stuff...
                szTwainDirectOnTwain = Config.Get("executablePath", "");
                szTwainDirectOnTwain = szTwainDirectOnTwain.Replace("TwainDirect.Scanner", "TwainDirect.OnTwain");

                // State check...
                if (m_twainlocalsession.GetSessionState() != SessionState.noSession)
                {
                    // We're running a session, and this is our current caller...
                    if (a_apicmd.HttpGetCallersHostName() == m_twainlocalsession.GetCallersHostName())
                    {
                        DeviceReturnError(szFunction, a_apicmd, "invalidState", null, -1);
                        return (false);
                    }

                    // Otherwise somebody else is trying to talk to us, and we
                    // need to tell them we're busy right now...
                    else
                    {
                        DeviceReturnError(szFunction, a_apicmd, "busy", null, -1);
                        return (false);
                    }
                }

                // Create an IPC...
                if (m_twainlocalsession.GetIpcTwainDirectOnTwain() == null)
                {
                    m_twainlocalsession.SetIpcTwainDirectOnTwain(new Ipc("socket|" + IPAddress.Loopback.ToString() + "|0", true, null, null));
                }

                // Arguments to the progream...
                szArguments = "ipc=\"" + m_twainlocalsession.GetIpcTwainDirectOnTwain().GetConnectionInfo() + "\"";
                szArguments += " images=\"" + m_szTwImagesFolder + "\"";
                szArguments += " twainlist=\"" + Path.Combine(m_szWriteFolder, "twainlist.txt") + "\"";

                // Get ready to start the child process...
                m_twainlocalsession.SetProcessTwainDirectOnTwain(new Process());
                m_twainlocalsession.GetProcessTwainDirectOnTwain().StartInfo.UseShellExecute = false;
                m_twainlocalsession.GetProcessTwainDirectOnTwain().StartInfo.WorkingDirectory = Path.GetDirectoryName(szTwainDirectOnTwain);
                m_twainlocalsession.GetProcessTwainDirectOnTwain().StartInfo.CreateNoWindow = true;
                m_twainlocalsession.GetProcessTwainDirectOnTwain().StartInfo.RedirectStandardOutput = false;
                if (TwainLocalScanner.GetPlatform() == Platform.WINDOWS)
                {
                    m_twainlocalsession.GetProcessTwainDirectOnTwain().StartInfo.FileName = szTwainDirectOnTwain;
                    m_twainlocalsession.GetProcessTwainDirectOnTwain().StartInfo.Arguments = szArguments;
                }
                else
                {
                    m_twainlocalsession.GetProcessTwainDirectOnTwain().StartInfo.FileName = "/usr/bin/mono";
                    m_twainlocalsession.GetProcessTwainDirectOnTwain().StartInfo.Arguments = "\"" + szTwainDirectOnTwain + "\"" + " " + szArguments;
                }
                m_twainlocalsession.GetProcessTwainDirectOnTwain().StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                m_twainlocalsession.GetProcessTwainDirectOnTwain().EnableRaisingEvents = true;
                m_twainlocalsession.GetProcessTwainDirectOnTwain().Exited += new EventHandler(TwainLocalScanner_Exited);

                // Log what we're doing...
                Log.Info("run>>> " + m_twainlocalsession.GetProcessTwainDirectOnTwain().StartInfo.FileName);
                Log.Info("run>>> " + m_twainlocalsession.GetProcessTwainDirectOnTwain().StartInfo.Arguments);

                // Start the child process.
                m_twainlocalsession.GetProcessTwainDirectOnTwain().Start();

                // Monitor our new process...
                m_twainlocalsession.GetIpcTwainDirectOnTwain().MonitorPid(m_twainlocalsession.GetProcessTwainDirectOnTwain().Id);
                m_twainlocalsession.GetIpcTwainDirectOnTwain().Accept();

                // Open the scanner...
                string szCommand =
                    "{" +
                    "\"method\":\"createSession\"," +
                    "\"scanner\":" + m_twainlocalsession.DeviceRegisterGetTwainLocalScanner() +
                    "}";
                m_twainlocalsession.GetIpcTwainDirectOnTwain().Write(szCommand);

                // Get the result...
                JsonLookup jsonlookup = new JsonLookup();
                szIpc = m_twainlocalsession.GetIpcTwainDirectOnTwain().Read();
                blSuccess = jsonlookup.Load(szIpc, out lErrorErrorIndex);
                if (!blSuccess)
                {
                    // Exit the process...
                    m_twainlocalsession.GetIpcTwainDirectOnTwain().Write
                    (
                        "{" +
                        "\"method\":\"exit\"" +
                        "}"
                    );
                    m_twainlocalsession.GetProcessTwainDirectOnTwain().WaitForExit(5000);
                    m_twainlocalsession.GetProcessTwainDirectOnTwain().Close();
                    m_twainlocalsession.SetProcessTwainDirectOnTwain(null);
                    DeviceReturnError(szFunction, a_apicmd, "invalidJson", null, lErrorErrorIndex);
                    if (m_twainlocalsession != null)
                    {
                        m_twainlocalsession.Dispose();
                        m_twainlocalsession = null;
                    }
                    return (false);
                }

                // Handle errors...
                if (jsonlookup.Get("status") != "success")
                {
                    // Exit the process...
                    m_twainlocalsession.GetIpcTwainDirectOnTwain().Write
                    (
                        "{" +
                        "\"method\":\"exit\"" +
                        "}"
                    );
                    m_twainlocalsession.GetProcessTwainDirectOnTwain().WaitForExit(5000);
                    m_twainlocalsession.GetProcessTwainDirectOnTwain().Close();
                    m_twainlocalsession.SetProcessTwainDirectOnTwain(null);
                    DeviceReturnError(szFunction, a_apicmd, jsonlookup.Get("status"), null, -1);
                    if (m_twainlocalsession != null)
                    {
                        m_twainlocalsession.Dispose();
                        m_twainlocalsession = null;
                    }
                    return (false);
                }

                // Update the ApiCmd command object...
                a_apicmd.UpdateUsingIpcData(jsonlookup, false, m_szTdImagesFolder, m_szTwImagesFolder);

                // Reply to the command with a session object, this is where we create our
                // session id, public session id and set the revision to 0 (we increment
                // before sending, so the first revision is always 1)...
                m_twainlocalsession.SetCallersHostName(a_apicmd.HttpGetCallersHostName());
                m_twainlocalsession.SetSessionId(Guid.NewGuid().ToString());
                m_twainlocalsession.ResetSessionRevision();
                blSuccess = DeviceUpdateSession(szFunction, a_apicmd, false, null, -1, null);
                if (!blSuccess)
                {
                    DeviceReturnError(szFunction, a_apicmd, "critical", null, -1);
                    if (m_twainlocalsession != null)
                    {
                        m_twainlocalsession.Dispose();
                        m_twainlocalsession = null;
                    }
                    return (false);
                }

                // Refresh our timer...
                DeviceSessionRefreshTimer();

                // Init stuff...
                m_lImageBlockNumber = 0;
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Display a message, if we have a callback for it...
        /// </summary>
        /// <param name="a_szMsg">the message to display</param>
        private void Display(string a_szMsg)
        {
            if (m_displaycallback != null)
            {
                m_displaycallback(a_szMsg);
            }
        }

        private void TwainLocalScanner_Exited(object sender, EventArgs e)
        {
            DeviceSessionExited(false);
        }

        /// <summary>
        /// Get the current info on a scanning session.  This can happen in one
        /// of two ways, either as a standalone call to getSession, or as a part
        /// of waiting for events with waitForEvents.
        ///
        /// In the latter case we check to see if we have ApiCmd data, if we
        /// don't, we squirrel the event away.  If we do, we drain all of the
        /// events we currently have that aren't older than a certain number
        /// of seconds, and with revision numbers newer than the one received
        /// from the last call to waitForEvents.
        /// 
        /// An explicit call to getSession will reset the session timer.  A
        /// call to waitForEvents will not.
        /// </summary>
        /// <param name="a_apicmd">our command object</param>
        /// <param name="a_blSendEvents">send as events</param>
        /// <param name="a_blGetSession">get session for an event</param>
        /// <param name="a_blGetSession">get session for an event</param>
        /// <param name="a_szEventName">name of the event (or null)</param>
        /// <returns>true on success</returns>
        private bool DeviceScannerGetSession(ref ApiCmd a_apicmd, bool a_blSendEvents, bool a_blGetSession, string a_szEventName)
        {
            bool blSuccess;
            long lResponseCharacterOffset;
            string szIpc;
            ApiCmd apicmdEvent;
            string szFunction = "DeviceScannerGetSession";

            // Protect our stuff...
            lock (m_objectLockDeviceApi)
            {
                // Sanity check, if we have no session, we shouldn't be here...
                if (m_twainlocalsession == null)
                {
                    DeviceReturnError(szFunction, a_apicmd, "invalidSessionId", null, -1);
                    return (false);
                }

                //////////////////////////////////////////////////////////////////////
                // This path is taken for getSession
                //////////////////////////////////////////////////////////////////////
                #region getSession

                // Handle getSession...
                if (!a_blSendEvents)
                {
                    // Refresh our timer...
                    DeviceSessionRefreshTimer();

                    // State check...
                    if (m_twainlocalsession.GetSessionState() == SessionState.noSession)
                    {
                        DeviceReturnError(szFunction, a_apicmd, "invalidState", null, -1);
                        return (false);
                    }

                    // Validate...
                    if (m_twainlocalsession.GetIpcTwainDirectOnTwain() == null)
                    {
                        DeviceReturnError(szFunction, a_apicmd, "invalidSessionId", null, -1);
                        return (false);
                    }

                    // Get the current session info...
                    m_twainlocalsession.GetIpcTwainDirectOnTwain().Write
                    (
                        "{" +
                        "\"method\":\"getSession\"" +
                        "}"
                    );

                    // Get the result...
                    JsonLookup jsonlookup = new JsonLookup();
                    szIpc = m_twainlocalsession.GetIpcTwainDirectOnTwain().Read();
                    if (!jsonlookup.Load(szIpc, out lResponseCharacterOffset))
                    {
                        DeviceReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                        return (false);
                    }

                    // Update the ApiCmd command object...
                    switch (m_twainlocalsession.GetSessionState())
                    {
                        default:
                            a_apicmd.UpdateUsingIpcData(jsonlookup, false, m_szTdImagesFolder, m_szTwImagesFolder);
                            break;
                        case SessionState.capturing:
                        case SessionState.draining:
                            a_apicmd.UpdateUsingIpcData(jsonlookup, true, m_szTdImagesFolder, m_szTwImagesFolder);
                            break;
                    }

                    // Reply to the command with a session object...
                    blSuccess = DeviceUpdateSession(szFunction, a_apicmd, false, null, -1, null);
                    if (!blSuccess)
                    {
                        DeviceReturnError(szFunction, a_apicmd, "critical", null, -1);
                        return (false);
                    }

                    // Parse it...
                    if (!string.IsNullOrEmpty(a_apicmd.GetHttpResponseData()))
                    {
                        blSuccess = jsonlookup.Load(a_apicmd.GetHttpResponseData(), out lResponseCharacterOffset);
                        if (!blSuccess)
                        {
                            Log.Error(szFunction + ": error parsing the reply...");
                            return (false);
                        }
                    }
                }

                #endregion

                //////////////////////////////////////////////////////////////////////
                // This path is taken for waitForEvents
                //////////////////////////////////////////////////////////////////////
                #region waitForEvents

                // Handle waitForEvents...
                else
                {
                    // Log a header...
                    if (a_blGetSession)
                    {
                        Log.Info("");
                        Log.Info("http>>> waitForEvents (response) sendevents=" + a_blSendEvents + " getsession=" + a_blGetSession + " eventname=" + a_szEventName);
                    }

                    // We've already been handled...
                    if (a_apicmd == null)
                    {
                        return (true);
                    }

                    // Create an event...
                    apicmdEvent = null;

                    // Update our session revision (always do this)...
                    m_twainlocalsession.SetWaitForEventsSessionRevision(a_apicmd.GetJsonReceived("params.sessionRevision"));

                    // Stock it, if asked to...
                    if (a_blGetSession)
                    {
                        // Create an event...
                        apicmdEvent = new ApiCmd(null);

                        // Get the current session info...
                        m_twainlocalsession.GetIpcTwainDirectOnTwain().Write
                        (
                            "{" +
                            "\"method\":\"getSession\"" +
                            "}"
                        );

                        // Get the result...
                        JsonLookup jsonlookup = new JsonLookup();
                        szIpc = m_twainlocalsession.GetIpcTwainDirectOnTwain().Read();
                        if (!jsonlookup.Load(szIpc, out lResponseCharacterOffset))
                        {
                            DeviceReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                            return (false);
                        }

                        // TBD: some kind of check to see if the session data
                        // is different from the last call...

                        // Update the ApiCmd command object...
                        switch (m_twainlocalsession.GetSessionState())
                        {
                            default:
                                apicmdEvent.UpdateUsingIpcData(jsonlookup, false, m_szTdImagesFolder, m_szTwImagesFolder);
                                break;
                            case SessionState.capturing:
                            case SessionState.draining:
                                apicmdEvent.UpdateUsingIpcData(jsonlookup, true, m_szTdImagesFolder, m_szTwImagesFolder);
                                break;
                        }

                        // Bump up the session number...
                        m_twainlocalsession.SetSessionRevision(m_twainlocalsession.GetSessionRevision() + 1);
                    }

                    // Reply to the command, but only if we have
                    // pending data...
                    blSuccess = DeviceUpdateSession(szFunction, a_apicmd, true, apicmdEvent, m_twainlocalsession.GetSessionRevision(), a_szEventName);
                    if (!blSuccess)
                    {
                        DeviceReturnError(szFunction, a_apicmd, "critical", null, -1);
                        a_apicmd = null;
                        return (false);
                    }
                }

                #endregion
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
            int iImageBlock;
            long lJsonErrorIndex;
            long lResponseCharacterOffset;
            string szIpc;
            string szPdf;
            string szWithMetadata;
            string szMetadataFile;
            JsonLookup jsonlookup;
            string szFunction = "DeviceScannerReadImageBlock";

            // Protect our stuff...
            lock (m_objectLockDeviceApi)
            {
                // Refresh our timer...
                DeviceSessionRefreshTimer();

                // State check...
                switch (m_twainlocalsession.GetSessionState())
                {
                    // These are okay...
                    case SessionState.capturing:
                    case SessionState.draining:
                    case SessionState.closed:
                        break;

                    // These are not...
                    case SessionState.ready:
                    case SessionState.noSession:
                    default:
                        DeviceReturnError(szFunction, a_apicmd, "invalidState", null, -1);
                        break;
                }

                // Get our imageblock number...
                if (!int.TryParse(a_apicmd.GetJsonReceived("params.imageBlockNum"), out iImageBlock))
                {
                    DeviceReturnError(szFunction, a_apicmd, "badValue", "params.imageBlockNum", -1);
                    return (false);
                }

                // Do we want a thumbnail?
                szWithMetadata = a_apicmd.GetJsonReceived("params.withMetadata");
                if (string.IsNullOrEmpty(szWithMetadata) || (szWithMetadata == "false"))
                {
                    blWithMetadata = false;
                }
                else if (szWithMetadata == "true")
                {
                    blWithMetadata = true;
                }
                else
                {
                    DeviceReturnError(szFunction, a_apicmd, "badValue", "params.withMetadata", -1);
                    return (false);
                }

                // The image file, make sure we have forward slashes
                // before passing it to the JSON parser...
                szPdf = Path.Combine(m_szTdImagesFolder, "img" + iImageBlock.ToString("D6") + ".pdf").Replace("\\", "/");

                // Build the metadata filename, if we don't have one, we have a problem...
                szMetadataFile = "";
                if (blWithMetadata)
                {
                    szMetadataFile = szPdf.Replace(".pdf", ".meta");
                }

                // Kinda stuck with this notation for now...
                szIpc =
                    "{" +
                    "\"status\":\"success\"," +
                    "\"imageFile\":\"" + szPdf + "\"," +
                    "\"meta\":\"" + szMetadataFile + "\"" +
                    "}";
                jsonlookup = new JsonLookup();
                jsonlookup.Load(szIpc, out lJsonErrorIndex);

                // Update the ApiCmd command object...
                switch (m_twainlocalsession.GetSessionState())
                {
                    default:
                        a_apicmd.UpdateUsingIpcData(jsonlookup, false, m_szTdImagesFolder, m_szTwImagesFolder);
                        break;
                    case SessionState.capturing:
                    case SessionState.draining:
                        a_apicmd.UpdateUsingIpcData(jsonlookup, true, m_szTdImagesFolder, m_szTwImagesFolder);
                        break;
                }

                // Reply to the command with a session object...
                blSuccess = DeviceUpdateSession(szFunction, a_apicmd, false, null, -1, null);
                if (!blSuccess)
                {
                    DeviceReturnError(szFunction, a_apicmd, "critical", null, -1);
                    return (false);
                }

                // Parse it...
                if (!string.IsNullOrEmpty(a_apicmd.GetHttpResponseData()))
                {
                    blSuccess = jsonlookup.Load(a_apicmd.GetHttpResponseData(), out lResponseCharacterOffset);
                    if (!blSuccess)
                    {
                        Log.Error(szFunction + ": error parsing the reply...");
                        return (false);
                    }
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
            int iImageBlock;
            long lJsonErrorIndex;
            bool blSuccess;
            bool blWithThumbnail = false;
            long lResponseCharacterOffset;
            string szIpc;
            string szPdf;
            string szThumbnailFile;
            string szWithThumbnail;
            JsonLookup jsonlookup;
            string szFunction = "DeviceScannerReadImageBlockMetadata";

            // Protect our stuff...
            lock (m_objectLockDeviceApi)
            {
                // Refresh our timer...
                DeviceSessionRefreshTimer();

                // State check...
                switch (m_twainlocalsession.GetSessionState())
                {
                    // These are okay...
                    case SessionState.capturing:
                    case SessionState.draining:
                    case SessionState.closed:
                        break;

                    // These are not...
                    case SessionState.ready:
                    case SessionState.noSession:
                    default:
                        DeviceReturnError(szFunction, a_apicmd, "invalidState", null, -1);
                        return (false);
                }

                // Get our imageblock number...
                if (!int.TryParse(a_apicmd.GetJsonReceived("params.imageBlockNum"), out iImageBlock))
                {
                    DeviceReturnError(szFunction, a_apicmd, "badValue", "params.imageBlockNum", -1);
                    return (false);
                }

                // Do we want a thumbnail?
                szWithThumbnail = a_apicmd.GetJsonReceived("params.withThumbnail");
                if (string.IsNullOrEmpty(szWithThumbnail) || (szWithThumbnail == "false"))
                {
                    blWithThumbnail = false;
                }
                else if (szWithThumbnail == "true")
                {
                    blWithThumbnail = true;
                }
                else
                {
                    DeviceReturnError(szFunction, a_apicmd, "badValue", "params.withThumbnail", -1);
                    return (false);
                }

                // The image file, make sure we have forward slashes
                // before passing it to the JSON parser...
                szPdf = Path.Combine(m_szTdImagesFolder, "img" + iImageBlock.ToString("D6") + ".pdf").Replace("\\", "/");

                // Generate a thumbnail...
                szThumbnailFile = "";
                if (blWithThumbnail)
                {
                    szThumbnailFile = szPdf.Replace(".pdf", "_thumbnail.pdf");
                    blSuccess = PdfRaster.CreatePdfRasterThumbnail
                    (
                        szPdf,
                        szThumbnailFile,
                        Config.Get("pfxFile", ""),
                        Config.Get("pfxFilePassword", "")
                    );
                }

                // Build the metadata filename, if we don't have one, we have a problem...
                string szMetadataFile = szPdf.Replace(".pdf", ".meta");

                // Kinda stuck with this notation for now...
                szIpc =
                    "{" +
                    "\"status\":\"success\"," +
                    "\"meta\":\"" + szMetadataFile + "\"," +
                    "\"thumbnailFile\":\"" + szThumbnailFile + "\"" +
                    "}";
                jsonlookup = new JsonLookup();
                jsonlookup.Load(szIpc, out lJsonErrorIndex);

                // Update the ApiCmd command object...
                switch (m_twainlocalsession.GetSessionState())
                {
                    default:
                        a_apicmd.UpdateUsingIpcData(jsonlookup, false, m_szTdImagesFolder, m_szTwImagesFolder);
                        break;
                    case SessionState.capturing:
                    case SessionState.draining:
                        a_apicmd.UpdateUsingIpcData(jsonlookup, true, m_szTdImagesFolder, m_szTwImagesFolder);
                        break;
                }

                // Reply to the command with a session object...
                blSuccess = DeviceUpdateSession(szFunction, a_apicmd, false, null, -1, null);
                if (!blSuccess)
                {
                    DeviceReturnError(szFunction, a_apicmd, "critical", null, -1);
                    return (false);
                }

                // Parse it...
                if (!string.IsNullOrEmpty(a_apicmd.GetHttpResponseData()))
                {
                    blSuccess = jsonlookup.Load(a_apicmd.GetHttpResponseData(), out lResponseCharacterOffset);
                    if (!blSuccess)
                    {
                        Log.Error(szFunction + ": error parsing the reply...");
                        return (false);
                    }
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
            int ii;
            int iImageBlockNum;
            int iLastImageBlockNum;
            long lJsonErrorIndex;
            long lResponseCharacterOffset;
            string szIpc;
            JsonLookup jsonlookup;
            string szFunction = "DeviceScannerReleaseImageBlocks";

            // Protect our stuff...
            lock (m_objectLockDeviceApi)
            {
                // Refresh our timer...
                DeviceSessionRefreshTimer();

                // State check...
                switch (m_twainlocalsession.GetSessionState())
                {
                    // These are okay...
                    case SessionState.capturing:
                    case SessionState.draining:
                    case SessionState.closed:
                        break;

                    // These are not...
                    case SessionState.ready:
                    case SessionState.noSession:
                    default:
                        DeviceReturnError(szFunction, a_apicmd, "invalidState", null, -1);
                        return (false);
                }

                // Get the values...
                if (!int.TryParse(a_apicmd.GetJsonReceived("params.imageBlockNum"), out iImageBlockNum))
                {
                    DeviceReturnError(szFunction, a_apicmd, "badValue", "params.imageBlockNum", -1);
                    return (false);
                }
                if (!int.TryParse(a_apicmd.GetJsonReceived("params.lastImageBlockNum"), out iLastImageBlockNum))
                {
                    DeviceReturnError(szFunction, a_apicmd, "badValue", "params.lastImageBlockNum", -1);
                    return (false);
                }
                if (iImageBlockNum <= 0)
                {
                    DeviceReturnError(szFunction, a_apicmd, "badValue", "params.imageBlockNum", -1);
                    return (false);
                }
                if ((iLastImageBlockNum <= 0) || (iLastImageBlockNum < iImageBlockNum))
                {
                    DeviceReturnError(szFunction, a_apicmd, "badValue", "params.lastImageBlockNum", -1);
                    return (false);
                }

                // Loopy...
                for (ii = iImageBlockNum; ii <= iLastImageBlockNum; ii++)
                {
                    // Build the filename...
                    string szFile = Path.Combine(m_szTdImagesFolder, "img" + ii.ToString("D6"));
                    if (File.Exists(szFile + ".meta"))
                    {
                        try
                        {
                            File.Delete(szFile + ".meta");
                        }
                        catch
                        {
                            // We don't care if this fails...
                        }
                    }
                    if (File.Exists(szFile + ".pdf"))
                    {
                        try
                        {
                            File.Delete(szFile + ".pdf");
                        }
                        catch
                        {
                            // We don't care if this fails...
                        }
                    }

                    // If we've run out of pdf files, then scoot
                    // (can't have a meta without a pdf)
                    string[] aszPdf = Directory.GetFiles(m_szTdImagesFolder, "*.pdf");
                    if ((aszPdf == null) || (aszPdf.Length == 0))
                    {
                        break;
                    }
                }

                // Kinda stuck with this notation for now...
                szIpc =
                    "{" +
                    "\"status\":\"success\"" +
                    "}";
                jsonlookup = new JsonLookup();
                jsonlookup.Load(szIpc, out lJsonErrorIndex);

                // Update the ApiCmd command object...
                switch (m_twainlocalsession.GetSessionState())
                {
                    default:
                        a_apicmd.UpdateUsingIpcData(jsonlookup, false, m_szTdImagesFolder, m_szTwImagesFolder);
                        break;
                    case SessionState.capturing:
                    case SessionState.draining:
                        a_apicmd.UpdateUsingIpcData(jsonlookup, true, m_szTdImagesFolder, m_szTwImagesFolder);
                        break;
                }

                // if the session has been closed and we have no more images,
                // then we need to close down twaindirect on twain...

                // Reply to the command with a session object...
                blSuccess = DeviceUpdateSession(szFunction, a_apicmd, false, null, -1, null);
                if (!blSuccess)
                {
                    DeviceReturnError(szFunction, a_apicmd, "critical", null, -1);
                    return (false);
                }

                // Parse it...
                if (    (m_twainlocalsession != null)
                    &&  (m_twainlocalsession.GetSessionState() != SessionState.noSession)
                    &&  !string.IsNullOrEmpty(a_apicmd.GetHttpResponseData()))
                {
                    blSuccess = jsonlookup.Load(a_apicmd.GetHttpResponseData(), out lResponseCharacterOffset);
                    if (!blSuccess)
                    {
                        Log.Error(szFunction + ": error parsing the reply...");
                        return (false);
                    }
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
        private bool DeviceScannerSendTask(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            long lResponseCharacterOffset;
            string szIpc;
            string szStatus;
            string szFunction = "DeviceScannerSendTask";

            // Protect our stuff...
            lock (m_objectLockDeviceApi)
            {
                // Refresh our timer...
                DeviceSessionRefreshTimer();

                // State check, we're allowing this to happen in more
                // than just the ready state to support custom vendor
                // actions.  The current TWAIN Direct actions can only
                // be used in the Ready state...
                switch (m_twainlocalsession.GetSessionState())
                {
                    // These are okay...
                    case SessionState.ready:
                        break;

                    // TBD
                    // These need to be checked to see if they are all vendor specific actions...
                    case SessionState.capturing:
                    case SessionState.draining:
                    case SessionState.closed:
                        break;

                    // These are not...
                    case SessionState.noSession:
                    default:
                        DeviceReturnError(szFunction, a_apicmd, "invalidState", null, -1);
                        return (false);
                }

                // Set the TWAIN Direct options...
                m_twainlocalsession.GetIpcTwainDirectOnTwain().Write
                (
                    "{" +
                    "\"method\":\"sendTask\"," +
                    "\"task\":" + a_apicmd.GetJsonReceived("params.task") +
                    "}"
                );

                // Get the result...
                JsonLookup jsonlookup = new JsonLookup();
                szIpc = m_twainlocalsession.GetIpcTwainDirectOnTwain().Read();
                blSuccess = jsonlookup.Load(szIpc, out lResponseCharacterOffset);
                if (!blSuccess)
                {
                    DeviceReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                    return (false);
                }

                // Check the status...
                szStatus = jsonlookup.Get("status");
                if (szStatus != "success")
                {
                    switch (szStatus)
                    {
                        default:
                            DeviceReturnError(szFunction, a_apicmd, szStatus, null, -1);
                            break;
                        case "invalidCapturingOptions":
                            DeviceReturnError(szFunction, a_apicmd, "invalidTask", jsonlookup.Get("taskReply"), -1);
                            break;
                    }
                    return (false);
                }

                // Update the ApiCmd command object...
                switch (m_twainlocalsession.GetSessionState())
                {
                    default:
                        a_apicmd.UpdateUsingIpcData(jsonlookup, false, m_szTdImagesFolder, m_szTwImagesFolder);
                        break;
                    case SessionState.capturing:
                    case SessionState.draining:
                        a_apicmd.UpdateUsingIpcData(jsonlookup, true, m_szTdImagesFolder, m_szTwImagesFolder);
                        break;
                }

                // Reply to the command with a session object...
                blSuccess = DeviceUpdateSession(szFunction, a_apicmd, false, null, -1, null);
                if (!blSuccess)
                {
                    DeviceReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                    return (false);
                }

                // Parse it...
                if (!string.IsNullOrEmpty(a_apicmd.GetHttpResponseData()))
                {
                    blSuccess = jsonlookup.Load(a_apicmd.GetHttpResponseData(), out lResponseCharacterOffset);
                    if (!blSuccess)
                    {
                        Log.Error(szFunction + ": error parsing the reply...");
                        return (false);
                    }
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        protected sealed override void Dispose(bool a_blDisposing)
        {
            // Cleanup the timeout event...
            if (m_timerEvent != null)
            {
                m_timerEvent.Change(Timeout.Infinite, Timeout.Infinite);
                m_timerEvent.Dispose();
                m_timerEvent = null;
            }

            // Don't timeout the session...
            if (m_timerSession != null)
            {
                m_timerSession.Change(Timeout.Infinite, Timeout.Infinite);
                m_timerSession.Dispose();
                m_timerSession = null;
            }

            // Zap our server...
            if (m_httpserver != null)
            {
                m_httpserver.Dispose();
                m_httpserver = null;
            }

            // Zap the rest of it...
            base.Dispose(a_blDisposing);
        }

        /// <summary>
        /// Handle changes to the imageBlocks folder for the TWAIN
        /// Bridge (ex: TWAIN Direct on TWAIN).  This is where we
        /// split images we got from the TWAIN Driver into imageBlocks
        /// that we'll send to the application...
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void OnChangedBridge(object source, FileSystemEventArgs e)
        {
            bool blSendImageBlocksEvent = false;
            long lImageBlocks;
            long lImageBlockSize;
            string szImageBlockName;
            string[] aszMetatmp;
            string[] aszBase;
            SessionState sessionstate;
            FileSystemWatcherHelper filesystemwatcherhelper = (FileSystemWatcherHelper)source;

            // We shouldn't be here...
            if (m_twainlocalsession == null)
            {
                filesystemwatcherhelper.EnableRaisingEvents = false;
                return;
            }

            // Get the session state...
            sessionstate = m_twainlocalsession.GetSessionState();

            // The scanner has no more data for us...
            if (!m_twainlocalsession.GetSessionDoneCapturing())
            {
                if (    (sessionstate == SessionState.capturing)
                    ||  (sessionstate == SessionState.draining)
                    ||  (sessionstate == SessionState.closed))
                {
                    if (    File.Exists(Path.Combine(m_szTdImagesFolder, "imageBlocksDrained.meta"))
                        ||  File.Exists(Path.Combine(m_szTwImagesFolder, "imageBlocksDrained.meta")))
                    {
                        m_twainlocalsession.SetSessionDoneCapturing(true);
                    }
                }
            }

            // It's important that we serialize the callbacks...
            lock (m_objectLockOnChangedBridge)
            {
                // Loop for as long as we find .twmeta files...
                while (true)
                {
                    // Get a reason for there being no more images...
                    if (!m_blReadImageBlocksDrainedMeta && (m_twainlocalsession != null) && File.Exists(m_szImageBlocksDrainedMeta))
                    {
                        JsonLookup jsonlookupDrained = new JsonLookup();
                        m_blReadImageBlocksDrainedMeta = true;
                        try
                        {
                            // Just using this to make the code nicer, not to loop...
                            while (true)
                            {
                                // Read the file...
                                long lJsonErrorIndex;
                                string szReason = File.ReadAllText(m_szImageBlocksDrainedMeta);
                                if (string.IsNullOrEmpty(szReason))
                                {
                                    Log.Error("empty imageBlocksDrained.meta (we'd like a reason)...");
                                    break;
                                }

                                // Load the JSON...
                                if (!jsonlookupDrained.Load(szReason, out lJsonErrorIndex))
                                {
                                    Log.Error("bad JSON in imageBlocksDrained.meta - <" + szReason + ">");
                                    break;
                                }

                                // Get the "detected" property...
                                Log.Info("imageBlocksDrained.meta: " + szReason);
                                string szDetected = jsonlookupDrained.Get("detected");
                                if (string.IsNullOrEmpty(szDetected))
                                {
                                    Log.Error("detected not found in imageBlocksDrained.meta (we'd like a reason) - " + szReason);
                                    break;
                                }

                                // Convert the TWAIN status to TWAIN Direct...
                                switch (szDetected.ToLowerInvariant())
                                {
                                    // If we don't recognize it, use the misfeed option...
                                    default:
                                        m_twainlocalsession.SetSessionStatusSuccess(false);
                                        m_twainlocalsession.SetSessionStatusDetected("misfeed");
                                        break;

                                    // Make no changes, we're initialized to true/nominal...
                                    case "success":
                                    case "cancel":
                                        break;

                                    // I wonder if anybody has actually implemented this...
                                    case "damagedcorner":
                                        m_twainlocalsession.SetSessionStatusSuccess(false);
                                        m_twainlocalsession.SetSessionStatusDetected("foldedCorner");
                                        break;

                                    // It's funny how the old names got into TWAIN Direct...
                                    case "interlock":
                                        m_twainlocalsession.SetSessionStatusSuccess(false);
                                        m_twainlocalsession.SetSessionStatusDetected("coverOpen");
                                        break;

                                    // We couldn't find the first sheet...
                                    case "nomedia":
                                        m_twainlocalsession.SetSessionStatusSuccess(false);
                                        m_twainlocalsession.SetSessionStatusDetected("noMedia");
                                        break;

                                    // We scan faster if we slug feed all the docs... :)
                                    case "paperdoublefeed":
                                        m_twainlocalsession.SetSessionStatusSuccess(false);
                                        m_twainlocalsession.SetSessionStatusDetected("doubleFeed");
                                        break;

                                    // The scanner is eating our docs...
                                    case "paperjam":
                                        m_twainlocalsession.SetSessionStatusSuccess(false);
                                        m_twainlocalsession.SetSessionStatusDetected("paperJam");
                                        break;
                                }

                                // Make a note to send an event...
                                blSendImageBlocksEvent = true;

                                // Bye-bye loop...
                                break;
                            }
                        }
                        catch (Exception exception)
                        {
                            Log.Error("trouble processing imageBlocksDrained.meta - " + exception.Message);
                        }
                    }

                    // Find all of the TWAIN *.twmeta files, scoot if we don't find any...
                    aszMetatmp = Directory.GetFiles(m_szTwImagesFolder, "*.twmeta"); // Metadata from the TWAIN Driver or the bridge
                    if ((aszMetatmp == null) || (aszMetatmp.Length == 0))
                    {
                        break;
                    }

                    // Figure out what our imageblock size is, a value <= 8192 causes
                    // us to send the entire image in one go without splitting it...
                    lImageBlockSize = Config.Get("imageBlockSize", 0);
                    if (lImageBlockSize < 8192)
                    {
                        lImageBlockSize = 0;
                    }

                    // Send the entire thing in one imageBlock...
                    #region Send the entire thing in one imageBlock...
                    if (lImageBlockSize == 0)
                    {
                        // Fix every .twmeta we find, and its associated files...
                        foreach (string szMetatmp in aszMetatmp)
                        {
                            // Get the files with this basename...
                            aszBase = Directory.GetFiles(m_szTwImagesFolder, Path.GetFileNameWithoutExtension(szMetatmp) + ".*");

                            // Walk all the files, except for .twmeta, which we must do last...
                            foreach (string szFile in aszBase)
                            {
                                // If it doesn't have a .tw in it, skip it...
                                if (!szFile.Contains(".tw"))
                                {
                                    continue;
                                }

                                // If it ends with .twmeta, skip it...
                                if (szFile.EndsWith(".twmeta"))
                                {
                                    continue;
                                }

                                // Rename it from .twxxx to .xxx...
                                try
                                {
                                    File.Move
                                    (
                                        szFile, // from twimages
                                        szFile.Replace("twimages", "tdimages").Replace(".tw", ".") // to tdimages
                                    );
                                }
                                catch (Exception exception)
                                {
                                    Log.Error("rename failed <" + szFile + "> <" + szFile.Substring(0, szFile.Length - 3) + "> - " + exception.Message);
                                }
                            }

                            // Now fix just the .twmeta files, this is the trigger
                            // that causes TwainDirect.Scanner to recognize that it
                            // has new data to send to the application.  Note that
                            // we are not refreshing aszBase, that's deliberate...
                            foreach (string szFile in aszBase)
                            {
                                // If it doesn't end with .twmeta, skip it...
                                if (!szFile.EndsWith(".twmeta"))
                                {
                                    continue;
                                }

                                // Rename it from .xxxtmp to .xxx...
                                try
                                {
                                    File.Move
                                    (
                                        szFile, // from twimages
                                        szFile.Replace(m_szTwImagesFolder, m_szTdImagesFolder).Replace(".tw", ".") // to tdimages
                                    );
                                }
                                catch (Exception exception)
                                {
                                    Log.Error("rename failed <" + szFile + "> <" + szFile.Substring(0, szFile.Length - 3) + "> - " + exception.Message);
                                }
                            }

                            // Make a note to send an event...
                            blSendImageBlocksEvent = true;
                        }
                    }
                    #endregion

                    // Split the thing into one or more imageBlocks...
                    #region Split the thing into one or more imageBlocks...
                    else
                    {
                        // Fix every .twmeta we find, and its associated files...
                        foreach (string szMetatmp in aszMetatmp)
                        {
                            // Read this data and load it into JSON...
                            long lJsonErrorIndex;
                            string szMetaLast = File.ReadAllText(szMetatmp);
                            JsonLookup jsonlookupLast = new JsonLookup();
                            jsonlookupLast.Load(szMetaLast, out lJsonErrorIndex);

                            // Get the .twpdf files with this basename, make sure
                            // it's sorted, because we're going to be messing
                            // with the imageBlock number...
                            aszBase = Directory.GetFiles(m_szTwImagesFolder, Path.GetFileNameWithoutExtension(szMetatmp) + ".twpdf");
                            Array.Sort(aszBase);

                            // Walk all the .twpdf files, skipping any thumbnails, we'll
                            // sort them further down in this loop...
                            foreach (string szFile in aszBase)
                            {
                                // Skip .twpdf thumbnails...
                                if (szFile.Contains("thumbnail"))
                                {
                                    continue;
                                }

                                // How many imageBlocks are we getting from this file?
                                // Be sure to pin to the next highest integer.
                                FileInfo fileinfo = new FileInfo(szFile);
                                lImageBlocks = (long)Math.Ceiling((double)fileinfo.Length / (double)lImageBlockSize);

                                // Split the .pdf file into smaller imageBlocks...
                                byte[] abData = new byte[lImageBlockSize];
                                FileStream filestreamRead = new FileStream(szFile, FileMode.Open);
                                for (long ll = 0; ll < lImageBlocks; ll++)
                                {
                                    int iBytesRead;
                                    szImageBlockName = Path.Combine(m_szTdImagesFolder, "img" + (m_lImageBlockNumber + 1 + ll).ToString("D6") + ".pdf");
                                    FileStream filestreamWrite = new FileStream(szImageBlockName, FileMode.Create);
                                    iBytesRead = filestreamRead.Read(abData, 0, (int)lImageBlockSize);
                                    filestreamWrite.Write(abData, 0, iBytesRead);
                                    filestreamWrite.Close();
                                }
                                filestreamRead.Close();

                                // We don't need this .twpdf file anymore...
                                File.Delete(szFile);

                                // Fix the .twmeta file, this involves updating both
                                // the imageNumber and the imagePart number.  We want
                                // to do this first to reduce the delay between when
                                // we create the other .meta files and this one...
                                string szMeta = File.ReadAllText(szMetatmp);

                                // We don't need this .twmeta file anymore...
                                File.Delete(szMetatmp);

                                // We only need to do this bit if we have more than
                                // one imageBlock...
                                if (lImageBlocks > 1)
                                {
                                    // Load the JSON from the .twmeta we got from the TWAIN driver...
                                    JsonLookup jsonlookup = new JsonLookup();
                                    jsonlookup.Load(szMeta, out lJsonErrorIndex);

                                    // Okay, let's create all the intermediate .meta files,
                                    // per the spec, these contain minimal information.
                                    // We're going to be sneaky about this, so that if changes
                                    // are made to the spec, we should still work.  Start by
                                    // grabbing the address block from the .twmeta file and
                                    // embedding it in a rooted metadata object...
                                    string szMetadataAddress =
                                        "{" +
                                        "\"metadata\":{" +
                                        "\"address\":" +
                                        jsonlookup.Get("metadata.address") + "," + // includes address' {}
                                        "\"status\":{" +
                                        "\"success\":true" +
                                        "}" + // status
                                        "}" + //metadata
                                        "}"; // root

                                    // Get an object for this data, so we can override the bits
                                    // we care about...
                                    JsonLookup jsonlookupMetadataAddress = new JsonLookup();
                                    jsonlookupMetadataAddress.Load(szMetadataAddress, out lJsonErrorIndex);

                                    // Now loop through the intermediate .meta files, making
                                    // each one with its correct imageBlock value...
                                    for (long ll = 0; ll < (lImageBlocks - 1); ll++)
                                    {
                                        //jsonlookupMetadataAddress.Override("metadata.address.imageNumber", (m_lImageBlockNumber + 1 + ll).ToString());
                                        //jsonlookupMetadataAddress.Override("metadata.address.imagePart", (ll + 1).ToString());
                                        jsonlookupMetadataAddress.Override("metadata.address.moreParts", "morePartsPending");
                                        szMetadataAddress = jsonlookupMetadataAddress.Dump();
                                        szImageBlockName = Path.Combine(m_szTdImagesFolder, "img" + (m_lImageBlockNumber + 1 + ll).ToString("D6") + ".meta");
                                        File.WriteAllText(szImageBlockName, szMetadataAddress);
                                    }

                                    // Override the imageNumber and imagePart, note that we
                                    // don't want the +1 on m_lImageBlockNumber, because that's
                                    // already accounted for in the lImageBlocks number.  Also
                                    // we don't have to touch moreParts, it should already have
                                    // the value we want...
                                    //jsonlookup.Override("metadata.address.imageNumber", (m_lImageBlockNumber + lImageBlocks).ToString());
                                    //jsonlookup.Override("metadata.address.imagePart", lImageBlocks.ToString());
                                    szMeta = jsonlookupMetadataAddress.Dump();
                                }

                                // If we have a thumbnail, rename it now...
                                szImageBlockName = Path.Combine(Path.GetDirectoryName(szMetatmp), Path.GetFileNameWithoutExtension(szMetatmp)) + "_thumbnail.twpdf";
                                if (File.Exists(szImageBlockName))
                                {
                                    File.Move
                                    (
                                        szImageBlockName,
                                        Path.Combine(m_szTdImagesFolder, "img" + (m_lImageBlockNumber + lImageBlocks).ToString("D6") + "_thumbnail.pdf")
                                    );
                                }

                                // Write out the .meta for the final image block, this
                                // triggers processing of the last block...
                                szImageBlockName = Path.Combine(m_szTdImagesFolder, "img" + (m_lImageBlockNumber + lImageBlocks).ToString("D6") + ".meta");
                                //jsonlookupLast.Override("metadata.address.imageNumber", (m_lImageBlockNumber + lImageBlocks).ToString());
                                //jsonlookupLast.Override("metadata.address.imagePart", lImageBlocks.ToString());
                                jsonlookupLast.Override("metadata.address.moreParts", "lastPartInFile");
                                szMeta = jsonlookupLast.Dump();
                                File.WriteAllText(szImageBlockName, szMeta);

                                // Update our block number (this is the last block, consistent
                                // with a starting value of 0)...
                                m_lImageBlockNumber += lImageBlocks;
                            }
                        }

                        // Make a note to send an event...
                        blSendImageBlocksEvent = true;
                    }
                    #endregion
                }

                // Check to see if we should move the imageBlocksDrained.meta file...
                if (File.Exists(m_szImageBlocksDrainedMeta))
                {
                    // Check for any pending files, if we have none, then we're
                    // done and it's safe to say-so, regardless of how many files
                    // have been transferred.  The reason being that we can
                    // guarantee that no new imageBlocks will be added...
                    aszMetatmp = Directory.GetFiles(m_szTwImagesFolder, "*.tw*");
                    if ((aszMetatmp == null) || (aszMetatmp.Length == 0))
                    {
                        try
                        {
                            // Move it...
                            File.Move
                            (
                                m_szImageBlocksDrainedMeta,
                                Path.Combine(m_szTdImagesFolder, "imageBlocksDrained.meta")
                            );

                            // Make a note to send an event...
                            blSendImageBlocksEvent = true;
                        }
                        catch (Exception exception)
                        {
                            Log.Error("moving imageBlocksDrained failed - " + exception.Message);
                        }
                    }
                }

                // Generate an event...
                if (blSendImageBlocksEvent && (m_apicmdEvent != null))
                {
                    DeviceScannerGetSession(ref m_apicmdEvent, true, true, "imageBlocks");
                    m_apicmdEvent = null;
                }
            }
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
            FileSystemWatcherHelper filesystemwatcherhelper;
            string szFunction = "DeviceScannerStartCapturing";

            // Protect our stuff...
            lock (m_objectLockDeviceApi)
            {
                // Refresh our timer...
                DeviceSessionRefreshTimer();

                // State check...
                if (m_twainlocalsession.GetSessionState() != SessionState.ready)
                {
                    DeviceReturnError(szFunction, a_apicmd, "invalidState", null, -1);
                    return (false);
                }

                // We start by assuming that any problems with the scanner have
                // been resoved by the user...
                CleanImageFolders();
                m_blReadImageBlocksDrainedMeta = false;
                m_twainlocalsession.SetSessionDoneCapturing(false);
                m_twainlocalsession.SetSessionImageBlocksDrained(false);
                m_twainlocalsession.SetSessionStatusSuccess(true);
                m_twainlocalsession.SetSessionStatusDetected("nominal");

                // Initialize our counters...
                m_lImageBlockNumber = 0;

                // Start capturing...
                m_twainlocalsession.GetIpcTwainDirectOnTwain().Write
                (
                    "{" +
                    "\"method\":\"startCapturing\"" +
                    "}"
                );

                // Get the result...
                JsonLookup jsonlookup = new JsonLookup();
                szIpc = m_twainlocalsession.GetIpcTwainDirectOnTwain().Read();
                blSuccess = jsonlookup.Load(szIpc, out lResponseCharacterOffset);
                if (!blSuccess)
                {
                    DeviceReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                    return (false);
                }

                // Update the ApiCmd command object...
                a_apicmd.UpdateUsingIpcData(jsonlookup, true, m_szTdImagesFolder, m_szTwImagesFolder);

                // Reply to the command with a session object...
                blSuccess = DeviceUpdateSession(szFunction, a_apicmd, false, null, -1, null);
                if (!blSuccess)
                {
                    DeviceReturnError(szFunction, a_apicmd, "critical", null, -1);
                    return (false);
                }

                // Parse it...
                if (!string.IsNullOrEmpty(a_apicmd.GetHttpResponseData()))
                {
                    blSuccess = jsonlookup.Load(a_apicmd.GetHttpResponseData(), out lResponseCharacterOffset);
                    if (!blSuccess)
                    {
                        Log.Error(szFunction + ": error parsing the reply...");
                        return (false);
                    }
                }

                // KEYWORD:imageBlock
                //
                // Watch for *.*meta files coming from the Bridge...
                filesystemwatcherhelper = new FileSystemWatcherHelper(this);
                filesystemwatcherhelper.Path = m_szTwImagesFolder;
                filesystemwatcherhelper.Filter = "*.*meta";
                filesystemwatcherhelper.IncludeSubdirectories = false;
                filesystemwatcherhelper.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                filesystemwatcherhelper.Changed += new FileSystemEventHandler(OnChangedBridge);
                filesystemwatcherhelper.EnableRaisingEvents = true;
                m_twainlocalsession.SetFileSystemWatcherHelperBridge(filesystemwatcherhelper);
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

            // Protect our stuff...
            lock (m_objectLockDeviceApi)
            {
                // Refresh our timer...
                DeviceSessionRefreshTimer();

                // State check...
                switch (m_twainlocalsession.GetSessionState())
                {
                    // These are okay...
                    case SessionState.capturing:
                    case SessionState.draining:
                        break;

                    // These are not...
                    case SessionState.closed:
                    case SessionState.noSession:
                    case SessionState.ready:
                    default:
                        DeviceReturnError(szFunction, a_apicmd, "invalidState", null, -1);
                        return (false);
                }

                // Stop capturing...
                m_twainlocalsession.GetIpcTwainDirectOnTwain().Write
                (
                    "{" +
                    "\"method\":\"stopCapturing\"" +
                    "}"
                );

                // Get the result...
                JsonLookup jsonlookup = new JsonLookup();
                szIpc = m_twainlocalsession.GetIpcTwainDirectOnTwain().Read();
                blSuccess = jsonlookup.Load(szIpc, out lResponseCharacterOffset);
                if (!blSuccess)
                {
                    DeviceReturnError(szFunction, a_apicmd, "invalidJson", null, lResponseCharacterOffset);
                    return (false);
                }

                // Update the ApiCmd command object...
                switch (m_twainlocalsession.GetSessionState())
                {
                    default:
                        a_apicmd.UpdateUsingIpcData(jsonlookup, false, m_szTdImagesFolder, m_szTdImagesFolder);
                        break;
                    case SessionState.capturing:
                    case SessionState.draining:
                        a_apicmd.UpdateUsingIpcData(jsonlookup, true, m_szTdImagesFolder, m_szTdImagesFolder);
                        break;
                }

                // Reply to the command with a session object...
                blSuccess = DeviceUpdateSession(szFunction, a_apicmd, false, null, -1, null);
                if (!blSuccess)
                {
                    DeviceReturnError(szFunction, a_apicmd, "critical", null, -1);
                    return (false);
                }

                // Parse it...
                if (    (m_twainlocalsession != null)
                    &&  (m_twainlocalsession.GetSessionState() != SessionState.noSession)
                    &&  !string.IsNullOrEmpty(a_apicmd.GetHttpResponseData()))
                {
                    blSuccess = jsonlookup.Load(a_apicmd.GetHttpResponseData(), out lResponseCharacterOffset);
                    if (!blSuccess)
                    {
                        Log.Error(szFunction + ": error parsing the reply...");
                        return (false);
                    }
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// We don't need a thread to report back events.  What we
        /// need is the ApiCmd for the outstanding request.  If we
        /// have pending event data, we respond immediately.  If
        /// there is no event data, we just need to remember the
        /// ApiCmd, so we can use it later when events do show up.
        /// 
        /// This works because we only remove events based on an
        /// expiration time, or when we get a waitForEvents command
        /// that tells us what the client has received.  We never
        /// clear an event after sending it.
        /// 
        /// We don't refresh the session time with waitForEvents,
        /// otherwise we'd never expire... :)
        /// 
        /// When we call this, we check to see if we need to respond
        /// immediately.  If so, we do that.  If not, then we set a
        /// timer that will expire after some period of time (for
        /// long polls the recommendation appears to be 30 seconds).
        /// </summary>
        /// <param name="a_apicmd">our command object</param>
        /// <returns>true on success</returns>
        private bool DeviceScannerWaitForEvents(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            string szFunction = "DeviceScannerWaitForEvents";

            // Squirrel this away...
            m_apicmdEvent = a_apicmd;

            // Update events...
            blSuccess = DeviceScannerGetSession(ref m_apicmdEvent, true, false, null);
            if (!blSuccess)
            {
                DeviceReturnError(szFunction, m_apicmdEvent, "critical", null, -1);
                m_apicmdEvent = null;
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Set the session state, and do additional cleanup work, if needed...
        /// </summary>
        /// <param name="a_sessionstate">new session state</param>
        /// <param name="a_szSessionEndedMessage">message to display when going to noSession</param>
        /// <param name="a_blUserShutdown">the user requested the close</param>
        /// <returns>the previous session state</returns>
        protected override SessionState SetSessionState
        (
            SessionState a_sessionstate,
            string a_szSessionEndedMessage = "Session ended...",
            bool a_blUserShutdown = true
        )
        {
            SessionState sessionstatePrevious;

            // Let the base cleanup...
            sessionstatePrevious = base.SetSessionState(a_sessionstate, a_szSessionEndedMessage, a_blUserShutdown);

            // Cleanup...
            if (a_sessionstate == SessionState.noSession)
            {
                // Display what happened...
                Display(a_szSessionEndedMessage);
            }

            // Return the previous state...
            return (sessionstatePrevious);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes
        // All of the members in this section must be specific to the device
        // and not to the session.  Session stuff goes into TwainLocalSession.
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes

        /// <summary>
        /// The long poll is on this guy, we'll respond to him when
        /// and if we have an event...
        /// </summary>
        private ApiCmd m_apicmdEvent;

        /// <summary>
        /// Use this to confirm a scan request...
        /// </summary>
        private ConfirmScan m_confirmscan;

        /// <summary>
        /// So we can have a bigger form...
        /// </summary>
        private float m_fConfirmScanScale;

        /// <summary>
        /// This value is generated whenever the TWAIN Local
        /// Scanner object is created, and it exists only for
        /// so long as the object exists.  We use this to
        /// generatre our X-Privet-Token.  Knowing this value
        /// allows us to validate it without having to keep
        /// a table around...
        /// </summary>
        private string m_szDeviceSecret;

        /// <summary>
        /// Optional callback for displaying text, this could
        /// be useful for debugging...
        /// </summary>
        private DisplayCallback m_displaycallback;

        /// <summary>
        /// Our HTTP server, all sessions must past through
        /// one server...
        /// </summary>
        private HttpServer m_httpserver;

        // If we're splitting up the TWAIN Bridge images into smaller
        // chunks, then we have to create a new sequence of image
        // block numbers.  In that case this counter keeps track of
        // those image block numbers.  If we're not splitting up the
        // images, this value doesn't get used (because we're just
        // renaming the *.xxxtmp files to *.xxx, which will preserve
        // the number we got from the TWAIN driver...
        private long m_lImageBlockNumber;

        /// <summary>
        /// Something we can lock...
        /// </summary>
        private object m_objectLockDeviceApi;
        private object m_objectLockDeviceHttpServerStop;
        private object m_objectLockOnChangedBridge;

        /// <summary>
        /// Idle time before a session times out (in milliseconds)...
        /// </summary>
        private long m_lSessionTimeout;

        /// <summary>
        /// This is where the TWAIN Direct imageBlocks and metadata
        /// are stored.  The ones we'll be sending to the app...
        /// </summary>
        private string m_szTdImagesFolder;

        /// <summary>
        /// Our event timer for m_apicmdEvent...
        /// </summary>
        private Timer m_timerEvent;

        /// <summary>
        /// Our session timer for /privet/twaindirect/session...
        /// </summary>
        private Timer m_timerSession;

        /// <summary>
        /// This is where the TWAIN Bridge imageBlocks and metadata
        /// are stored.  The ones're we're getting from the TWAIN
        /// driver...
        /// </summary>
        private string m_szTwImagesFolder;

        /// <summary>
        /// The file that tells us when TwainDirect.OnTwain is no longer
        /// capturing images from the scanner...
        /// </summary>
        private string m_szImageBlocksDrainedMeta;

        /// <summary>
        /// We've read the file once...
        /// </summary>
        private bool m_blReadImageBlocksDrainedMeta;

        #endregion
    }

    /// <summary>
    /// TWAIN Local support for the Client (Application and Certfication Tool)...
    /// </summary>
    public sealed class TwainLocalScannerClient : TwainLocalScanner
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods

        /// <summary>
        /// Init us...
        /// </summary>
        /// <param name="a_fConfirmScanScale">scale the confirmation dialog</param>
        /// <param name="a_eventcallback">event function</param>
        /// <param name="a_objectEventCallback">object that provided the event</param>
        /// <param name="a_blCreateTwainLocalSession">true for the server only</param>
        public TwainLocalScannerClient
        (
            EventCallback a_eventcallback,
            object a_objectEventCallback,
            bool a_blCreateTwainLocalSession
        ) : base
        (
            a_blCreateTwainLocalSession
        )
        {
            int iDefault;

            // We use this to get notification about events...
            m_autoreseteventWaitForEventsProcessing = new AutoResetEvent(false);

            // The callback we use to send messages about events,
            // such as critical and sessionTimedOut...
            m_eventcallback = a_eventcallback;

            // The payload for the event callback, probably the
            // caller's object...
            m_objectEventCallback = a_objectEventCallback;

            // Init our command timeout for HTTPS communication...
            iDefault = 30000; // 30 seconds
            m_iHttpTimeoutCommand = (int)Config.Get("httpTimeoutCommand", iDefault);
            if (m_iHttpTimeoutCommand < 5000)
            {
                m_iHttpTimeoutCommand = iDefault;
            }

            // Init our data timeout for HTTPS communication...
            iDefault = 30000; // 30 seconds
            m_iHttpTimeoutData = (int)Config.Get("httpTimeoutData", iDefault);
            if (m_iHttpTimeoutData < 10000)
            {
                m_iHttpTimeoutData = iDefault;
            }

            // This is our default location for storing imageblocks
            // and metadata...
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
                throw new Exception("Can't set up the images folder...");
            }

            // Our locks...
            m_objectLockClientApi = new object();
            m_objectLockClientFinishImage = new object();
            m_objectLockEndSession = new object();

            // The list of image blocks that we've received from
            // the scanner, and which we need to merge into finished
            // images...
            m_llPendingImageBlocks = new List<long>();
        }

        /// <summary>
        /// TWAIN Direct has four layers of error reporting: there's the HTTP status
        /// codes; a security layer defined by Privet 1.0; a TWAIN Local protocol
        /// layer, and a TWAIN Direct language layer.
        /// 
        /// This function checks out the health of the communication across all four
        /// layers, and updates the ApiCmd object with the results.  It returns true
        /// if no errors were found.
        /// </summary>
        /// <param name="a_szFunction">function we're reporting on</param>
        /// <param name="a_apicmd">object to update if we find problems</param>
        /// <returns>true on success, false if an error was detected</returns>
        public bool ClientCheckForApiErrors(string a_szFunction, ref ApiCmd a_apicmd)
        {
            int ii;
            bool blSuccess;
            long lJsonErrorIndex;
            string szReply;
            string szAction;
            string szSuccess;
            string szCode;
            string szJsonKey;
            string szCharacterOffset;
            string szDescription;
            JsonLookup jsonlookup;

            // First, let's check on the health of the HTTP communication.
            // If this fails, close the session...
            if (a_apicmd.HttpStatus() != System.Net.WebExceptionStatus.Success)
            {
                a_apicmd.SetApiErrorFacility(ApiCmd.ApiErrorFacility.httpstatus);
                a_apicmd.AddApiErrorCode("httpError");
                a_apicmd.AddApiErrorDescription(a_szFunction + ": httpError - " + a_apicmd.HttpStatus());
                return (false);
            }

            // Check the response data, every command must come back with a JSON
            // payload, so if we don't have one, that's a protocol error...
            szReply = a_apicmd.GetResponseData();
            if (string.IsNullOrEmpty(szReply))
            {
                a_apicmd.SetApiErrorFacility(ApiCmd.ApiErrorFacility.protocol);
                a_apicmd.AddApiErrorCode("protocolError");
                a_apicmd.AddApiErrorDescription(a_szFunction + ": protocolError - data is missing from the HTTP response");
                return (false);
            }

            // If the JSON format of the reply is damaged, we have a protocol
            // error...
            jsonlookup = new JsonLookup();
            blSuccess = jsonlookup.Load(szReply, out lJsonErrorIndex);
            if (!blSuccess)
            {
                a_apicmd.SetApiErrorFacility(ApiCmd.ApiErrorFacility.protocol);
                a_apicmd.AddApiErrorCode("invalidJson");
                a_apicmd.AddApiErrorDescription(a_szFunction + ": protocolError - JSON error in the HTTP response at character offset " + lJsonErrorIndex);
                return (false);
            }

            // Do we have a security error?
            szCode = jsonlookup.Get("error", false);
            if (!string.IsNullOrEmpty(szCode))
            {
                szDescription = jsonlookup.Get("description", false);
                if (string.IsNullOrEmpty(szDescription))
                {
                    szDescription = "(no description provided)";
                }
                a_apicmd.SetApiErrorFacility(ApiCmd.ApiErrorFacility.security);
                a_apicmd.AddApiErrorCode("invalidJson");
                a_apicmd.AddApiErrorDescription(a_szFunction + ": " + szCode + " - " + szDescription);
                return (false);
            }

            // How about a protocol error?
            szSuccess = jsonlookup.Get("results.success", false);
            if (string.IsNullOrEmpty(szSuccess) || ((szSuccess != "false") && (szSuccess != "true")))
            {
                a_apicmd.SetApiErrorFacility(ApiCmd.ApiErrorFacility.protocol);
                a_apicmd.AddApiErrorCode("protocolError");
                a_apicmd.AddApiErrorDescription(a_szFunction + ": protocolError - results.success is missing or invalid");
                return (false);
            }
            else if (szSuccess == "false")
            {
                // Collect the data...
                szCode = jsonlookup.Get("results.code", false);
                if (string.IsNullOrEmpty(szCode))
                {
                    szCode = "invalidTask";
                }
                szJsonKey = jsonlookup.Get("results.jsonKey", false);
                if (string.IsNullOrEmpty(szJsonKey))
                {
                    szJsonKey = "(n/a)";
                }
                szCharacterOffset = jsonlookup.Get("results.characterOffset", false);
                if (string.IsNullOrEmpty(szCharacterOffset))
                {
                    szCharacterOffset = "(n/a)";
                }

                // Squirrel it away...
                a_apicmd.SetApiErrorFacility(ApiCmd.ApiErrorFacility.protocol);
                a_apicmd.AddApiErrorCode("protocolError");
                a_apicmd.AddApiErrorDescription(a_szFunction + ": " + szCode + " - characterOffset=" + szCharacterOffset + " jsonKey=" + szJsonKey);
                return (false);
            }

            // All that's left are language errors, and those are
            // catagorized with each action.  Report all of them...
            bool blErrorDetected = false;
            for (ii = 0; true; ii++)
            {
                // If we run out of actions, scoot...
                szAction = "results.session.task.actions[" + ii + "]";
                szSuccess = jsonlookup.Get(szAction, false);
                if (string.IsNullOrEmpty(szSuccess))
                {
                    break;
                }

                // We'd better have one of these...
                szSuccess = jsonlookup.Get(szAction + ".results.success", false);

                // We're good, check the next one...
                if (!string.IsNullOrEmpty(szSuccess) && (szSuccess == "true"))
                {
                    continue;
                }

                // We've found a problem child...
                blErrorDetected = true;

                // The value better be false...
                if (string.IsNullOrEmpty(szSuccess) || (szSuccess != "false"))
                {
                    a_apicmd.SetApiErrorFacility(ApiCmd.ApiErrorFacility.language);
                    a_apicmd.AddApiErrorCode("invalidTask");
                    a_apicmd.AddApiErrorDescription(a_szFunction + ": invalidTask - " + szAction + ".results.success is missing or invalid");
                    continue;
                }

                // Collect the data on it...
                szCode = jsonlookup.Get(szAction + ".results.code", false);
                if (string.IsNullOrEmpty(szCode))
                {
                    szCode = "invalidTask";
                }
                szJsonKey = jsonlookup.Get(szAction + ".results.jsonKey", false);
                if (string.IsNullOrEmpty(szJsonKey))
                {
                    szJsonKey = "(n/a)";
                }

                // Add it to our list...
                a_apicmd.SetApiErrorFacility(ApiCmd.ApiErrorFacility.language);
                a_apicmd.AddApiErrorCode("invalidTask");
                a_apicmd.AddApiErrorDescription(a_szFunction + ": " + szCode + " - " + szAction + ", jsonKey=" + szJsonKey);
            }
            if (blErrorDetected)
            {
                return (false);
            }

            // Golly, it looks like we're in good shape...
            return (true);
        }

        /// <summary>
        /// End a session, and do all the cleanup work...
        /// </summary>
        public override void EndSession()
        {
            lock (m_objectLockEndSession)
            {
                // We're already clean...
                if (m_twainlocalsession == null)
                {
                    return;
                }

                // Take out the event communication and processing threads...
                if (m_waitforeventsinfo != null)
                {
                    m_blCancelWaitForEventsProcessing = true;
                    m_autoreseteventWaitForEventsProcessing.Set();
                    m_waitforeventsinfo.EndSession();
                    Thread.Sleep(100);
                }

                // Lose the eventing stuff on the client side...
                if (m_waitforeventsinfo != null)
                {
                    if (m_waitforeventsinfo.m_apicmd != null)
                    {
                        m_blCancelWaitForEventsProcessing = true;
                        m_waitforeventsinfo.m_apicmd.HttpAbortClientRequest(false);
                    }
                    m_waitforeventsinfo.Dispose();
                    m_waitforeventsinfo = null;
                }

                // Let the base cleanup...
                base.EndSession();
            }
        }

        /// <summary>
        /// Images are transferred using one or more imageBlocks.  This function
        /// recognizes when all of the imageBlocks for an image have been received.
        /// It creates the finished image, and ties the .meta and thumbnails to the
        /// basename.
        /// </summary>
        /// <param name="a_szImageBlockBasename">basename using this imageBlock</param>
        /// <param name="a_szFinishedImageBasename">the finished image basename</param>
        /// <returns></returns>
        public bool ClientFinishImage(string a_szImageBlockBasename, out string a_szFinishedImageBasename)
        {
            int ii;
            bool blSuccess;
            long lJsonErrorIndex;
            long iImageNumber;
            long iImagePart;
            long[] alImageBlocks;
            string szMetadata = "";
            string szMoreParts;
            string szThumbnail;
            string szTdMetadataFile = a_szImageBlockBasename + ".tdmeta";
            string szTdImageBlockFile = a_szImageBlockBasename + ".tdpdf";
            JsonLookup jsonlookup;

            // Init stuff...
            a_szFinishedImageBasename = "";

            // It's possible for this function to be called in response
            // to one or more readImageBlock calls completing.  We don't
            // want them fighting over the data.  So we serialize their
            // access...
            lock (m_objectLockClientFinishImage)
            {
                // If we were locked out, we may no longer have the imageBlock,
                // which means somebody else beat us to the punch, and we have
                // no work to do...
                if (!File.Exists(szTdImageBlockFile))
                {
                    return (false);
                }

                // We have no metadata...
                if (!File.Exists(szTdMetadataFile))
                {
                    return (false);
                }

                // Read the metadata, we're doing it this way because the data
                // could have been collected as a result of a call to readImageBlock
                // or readImageBlockMetadata.  We don't currently have a data
                // structure for images.  This kind of thing might push me over the edge...
                try
                {
                    szMetadata = File.ReadAllText(szTdMetadataFile);
                }
                catch (Exception exception)
                {
                    Log.Error("metadata error <" + szTdMetadataFile + "> - " + exception.Message);
                    return (false);
                }

                // Huh...maybe we got here before the file was ready...
                if (string.IsNullOrEmpty(szMetadata))
                {
                    return (false);
                }

                // Load the metadata...
                jsonlookup = new JsonLookup();
                blSuccess = jsonlookup.Load(szMetadata, out lJsonErrorIndex);
                if (!blSuccess)
                {
                    Log.Error("metadata error @" + lJsonErrorIndex + " <" + szMetadata + ">");
                    return (false);
                }

                // Collect the relevant address information...
                try
                {
                    iImageNumber = int.Parse(jsonlookup.Get("metadata.address.imageNumber"));
                    iImagePart = int.Parse(jsonlookup.Get("metadata.address.imagePart"));
                    szMoreParts = jsonlookup.Get("metadata.address.moreParts");
                    if (iImageNumber > m_iLastImageNumberSinceCurrentStartCapturing)
                    {
                        m_iLastImageNumberSinceCurrentStartCapturing = iImageNumber;
                    }
                }
                catch (Exception exception)
                {
                    Log.Error("metadata error <" + szMetadata + "> - " + exception.Message);
                    return (false);
                }

                // This is the basename of the finished .meta and .pdf...
                a_szFinishedImageBasename = Path.Combine(Path.GetDirectoryName(a_szImageBlockBasename), "image" + (m_iLastImageNumberFromPreviousStartCapturing + iImageNumber).ToString("D6"));

                // Get the list of imageBlocks, this should be the contents of the array
                // from the response to readImageBlock.  Having this allows us to better
                // figure out where we are in the imageBlock array...
                alImageBlocks = ClientGetImageBlocks();

                // Add any new imageBlocks to the list we're maintaining...
                if ((alImageBlocks != null) && (alImageBlocks.Length > 0))
                {
                    // Get the last imageBlock in our list...
                    long lLastImageBlock = 0;
                    if (m_llPendingImageBlocks.Count > 0)
                    {
                        lLastImageBlock = m_llPendingImageBlocks[m_llPendingImageBlocks.Count - 1];
                    }

                    // Append any imageBlocks greater than lLastImageBlock...
                    for (ii = 0; ii < alImageBlocks.Length; ii++)
                    {
                        if (alImageBlocks[ii] > lLastImageBlock)
                        {
                            m_llPendingImageBlocks.Add(alImageBlocks[ii]);
                        }
                    }
                }

                // If the imageNumber in the metadata matches the first imageBlock, if
                // its imagePart is 1, and moreParts is lastPartInFile, then we can
                // accomplish our task with simple renames.  I have this in place to allow
                // for a simplier code path when the config setting imageBlockSize is set
                // to 0, meaning that we don't want to split up images across multiple
                // imageBlocks.
                if ((m_llPendingImageBlocks.Count > 0) && (iImageNumber == m_llPendingImageBlocks[0]) && (iImagePart == 1) && (szMoreParts == "lastPartInFile"))
                {
                    // Rename the .tdpdf file...
                    try
                    {
                        File.Move(szTdImageBlockFile, a_szFinishedImageBasename + ".pdf");
                    }
                    catch (Exception exception)
                    {
                        Log.Error("move failed: <" + szTdImageBlockFile + "> --> <" + a_szFinishedImageBasename + ".pdf" + "> - " + exception.Message);
                        return (false);
                    }

                    // If we have a thumbnail, rename it...
                    szThumbnail = a_szImageBlockBasename + "_thumbnail.tdpdf";
                    if (File.Exists(szThumbnail))
                    {
                        try
                        {
                            File.Move(szThumbnail, a_szFinishedImageBasename + "_thumbnail.pdf");
                        }
                        catch (Exception exception)
                        {
                            Log.Error("move failed: <" + szThumbnail + "> --> <" + a_szFinishedImageBasename + "_thumbnail.pdf" + "> - " + exception.Message);
                            return (false);
                        }
                    }

                    // Always handle the .tdmeta last, because the creation
                    // of a .meta file indicates that all of the files associated
                    // with an image are ready for access...
                    try
                    {
                        File.Move(szTdMetadataFile, a_szFinishedImageBasename + ".meta");
                    }
                    catch (Exception exception)
                    {
                        Log.Error("move failed: <" + szTdMetadataFile + "> --> <" + a_szFinishedImageBasename + ".meta" + "> - " + exception.Message);
                        return (false);
                    }

                    // Remove this item from our list...
                    m_llPendingImageBlocks.RemoveAt(0);

                    // All done, we created our finished image...
                    return (true);
                }

                // Otherwise life is a bit more complicated.  We walk through the
                // list of imageBlocks we got from the session object to see if
                // we can build a complete image, and if so, we do that.  If there
                // are more imageBlocks after that, we tell the caller, so that
                // they can have us look for more complete images.  We do that until
                // we either exhaust the imageBlocks or we're unable to create a
                // finished image.
                //
                // Doing it this way allows us to generate finished images in order,
                // and with the caller controlling the basename of the finished
                // files...
                List<string> lszBasenames = new List<string>();
                string szDirectoryName = Path.GetDirectoryName(a_szImageBlockBasename);
                foreach (long lImageBlock in m_llPendingImageBlocks)
                {
                    // Check for a .tdmeta file, if we don't have
                    // it, we're done...
                    string szTdmetaFile = Path.Combine(szDirectoryName, "img" + lImageBlock.ToString("D6") + ".tdmeta");
                    if (!File.Exists(szTdmetaFile))
                    {
                        return (false);
                    }

                    // Read the beastie...
                    string szTdmeta = File.ReadAllText(szTdmetaFile);
                    jsonlookup.Load(szTdmeta, out lJsonErrorIndex);

                    // Collect the relevant address information...
                    try
                    {
                        iImageNumber = int.Parse(jsonlookup.Get("metadata.address.imageNumber"));
                        iImagePart = int.Parse(jsonlookup.Get("metadata.address.imagePart"));
                        szMoreParts = jsonlookup.Get("metadata.address.moreParts");
                    }
                    catch (Exception exception)
                    {
                        Log.Error("metadata error - " + exception.Message);
                        return (false);
                    }

                    // Add it to our list...
                    lszBasenames.Add(Path.Combine(szDirectoryName, Path.GetFileNameWithoutExtension(szTdmetaFile)));

                    // If this isn't the last bit, go up and look for more...
                    if (szMoreParts != "lastPartInFile")
                    {
                        continue;
                    }

                    // If we got this far, we've found all of the imageBlocks for an image...
                    break;
                }

                // Stitch all the .tdpdf's into a single .pdf...
                int iRead;
                byte[] abData = new byte[0x200000];
                string szLastBasename = lszBasenames[lszBasenames.Count - 1];
                FileStream filestreamWrite = new FileStream(a_szFinishedImageBasename + ".pdf", FileMode.Create);
                foreach (string szBasename in lszBasenames)
                {
                    // Copy the data...
                    try
                    {
                        FileStream filestreamRead = new FileStream(szBasename + ".tdpdf", FileMode.Open);
                        while (true)
                        {
                            iRead = filestreamRead.Read(abData, 0, abData.Length);
                            if (iRead == 0)
                            {
                                break;
                            }
                            filestreamWrite.Write(abData, 0, iRead);
                        }
                        filestreamRead.Close();
                    }
                    catch (Exception exception)
                    {
                        Log.Error("read or write error - " + exception.Message);
                        return (false);
                    }

                    // Blow away the .tdpdf file...
                    File.Delete(szBasename + ".tdpdf");

                    // Blow away the .tdmeta file, if it's not the last one...
                    if (szBasename != szLastBasename)
                    {
                        File.Delete(szBasename + ".tdmeta");
                    }

                    // Remove this imageBlock from our list...
                    m_llPendingImageBlocks.RemoveAt(0);
                }
                filestreamWrite.Close();

                // If we have a thumbnail, rename it...
                szThumbnail = szLastBasename + "_thumbnail.tdpdf";
                if (File.Exists(szThumbnail))
                {
                    try
                    {
                        File.Move(szThumbnail, a_szFinishedImageBasename + "_thumbnail.pdf");
                    }
                    catch (Exception exception)
                    {
                        Log.Error("move failed: " + szThumbnail + " -- > " + a_szFinishedImageBasename + "_thumbnail.pdf - " + exception.Message);
                    }
                }

                // Always handle the .tdmeta last, because the creation
                // of a .meta file indicates that all of the files associated
                // with an image are ready for access...
                try
                {
                    File.Move(szLastBasename + ".tdmeta", a_szFinishedImageBasename + ".meta");
                }
                catch (Exception exception)
                {
                    Log.Error("move failed: " + szLastBasename + ".tdmeta --> " + a_szFinishedImageBasename + ".meta - " + exception.Message);
                }

                // All done, we created our finished image...
                return (true);
            }
        }

        /// <summary>
        /// Return the doneCapturing flag...
        /// </summary>
        /// <returns>true if the scanner is no longer munching on paper</returns>
        public bool ClientGetDoneCapturing()
        {
            if (m_twainlocalsession != null)
            {
                return (m_twainlocalsession.GetSessionDoneCapturing());
            }
            return (true);
        }

        /// <summary>
        /// Return the current image blocks...
        /// </summary>
        /// <returns>array of image blocks</returns>
        public long[] ClientGetImageBlocks()
        {
            if (m_twainlocalsession != null)
            {
                return (m_twainlocalsession.m_alSessionImageBlocks);
            }
            return (null);
        }

        /// <summary>
        /// Return the imageBlocksDrained flag...
        /// </summary>
        /// <returns>true if the scanner has no more images</returns>
        public bool ClientGetImageBlocksDrained()
        {
            if (m_twainlocalsession != null)
            {
                return (m_twainlocalsession.GetSessionImageBlocksDrained());
            }
            return (true);
        }

        /// <summary>
        /// Return the current image blocks...
        /// </summary>
        /// <returns>array of image blocks</returns>
        public string ClientGetImageBlocks(ApiCmd a_apicmd)
        {
            return (a_apicmd.GetImageBlocks().Replace(" ", ""));
        }

        /// <summary>
        /// Return the current session state...
        /// </summary>
        /// <returns>session state</returns>
        public string ClientGetSessionState()
        {
            if (m_twainlocalsession == null)
            {
                return ("noSession");
            }
            return (m_twainlocalsession.GetSessionState().ToString());
        }

        /// <summary>
        /// Tell us about the health of the session...
        /// </summary>
        /// <param name="a_szDetected"></param>
        /// <returns></returns>
        public bool ClientGetSessionStatusSuccess(out string a_szDetected)
        {
            if (m_twainlocalsession == null)
            {
                a_szDetected = "nominal";
                return (true);
            }
            a_szDetected = m_twainlocalsession.GetSessionStatusDetected();
            return (m_twainlocalsession.GetSessionStatusSuccess());
        }

        /// <summary>
        /// Get info about the device...
        /// </summary>
        /// <param name="a_apicmd">info about the command</param>
        /// <param name="a_szOverride">used for certification testing</param>
        /// <returns>true on success</returns>
        public bool ClientInfo
        (
            ref ApiCmd a_apicmd,
            string a_szOverride = null
        )
        {
            bool blSuccess;
            string szCommand;
            string szFunction = "ClientInfo";

            // This command can be issued at any time, so we don't check state, we also
            // don't have to worry about locking anything...

            // Figure out what command we're sending...
            if (a_szOverride != null)
            {
                szCommand = "/privet/" + a_szOverride;
            }
            else
            {
                szCommand = (Config.Get("useInfoex", "yes") == "yes") ? "/privet/infoex" : "/privet/info";
            }

            // Send the RESTful API command, we'll support using either
            // privet/info or /privet/infoex, but we'll default to infoex...
            blSuccess = ClientHttpRequest
            (
                szFunction,
                ref a_apicmd,
                szCommand,
                "GET",
                ClientHttpBuildHeader(true),
                null,
                null,
                null,
                m_iHttpTimeoutCommand,
                ApiCmd.HttpReplyStyle.SimpleReplyWithSessionInfo
            );
            if (!blSuccess)
            {
                ClientReturnError(a_apicmd, false, "", 0, "");
                return (false);
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
            bool blSuccess;
            string szFunction = "ClientScannerCloseSession";

            // Lock this command to protect the session object...
            lock (m_objectLockClientApi)
            {
                string szClientCreateCommandId = "";
                string szSessionId = "";

                // Collect session data, if we have any...
                if (m_twainlocalsession != null)
                {
                    szClientCreateCommandId = m_twainlocalsession.ClientCreateCommandId();
                    szSessionId = m_twainlocalsession.GetSessionId();
                }

                // Send the RESTful API command...
                blSuccess = ClientHttpRequest
                (
                    szFunction,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    ClientHttpBuildHeader(),
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + szClientCreateCommandId + "\"," +
                    "\"method\":\"closeSession\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + szSessionId + "\"" +
                    "}" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    ApiCmd.HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (!blSuccess)
                {
                    ClientReturnError(a_apicmd, false, "", 0, "");
                    return (false);
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
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerCreateSession(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            bool blCreatedTwainLocalSession = false;
            string szFunction = "ClientScannerCreateSession";

            // Lock this command to protect the session object...
            lock (m_objectLockClientApi)
            {
                // Create it if we need it...
                if (m_twainlocalsession == null)
                {
                    // We got this X-Privet-Token from info or infoex, if we didn't
                    // get one yet, there will be sadness on the scanner side...
                    m_twainlocalsession = new TwainLocalSession(m_szXPrivetToken);
                    blCreatedTwainLocalSession = true;
                }

                // Send the RESTful API command...
                blSuccess = ClientHttpRequest
                (
                    szFunction,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    ClientHttpBuildHeader(),
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + m_twainlocalsession.ClientCreateCommandId() + "\"," +
                    "\"method\":\"createSession\"" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    ApiCmd.HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (!blSuccess)
                {
                    ClientReturnError(a_apicmd, false, "", 0, "");
                    if (blCreatedTwainLocalSession)
                    {
                        if (m_twainlocalsession != null)
                        {
                            m_twainlocalsession.SetUserShutdown(false);
                            m_twainlocalsession.Dispose();
                            m_twainlocalsession = null;
                        }
                    }
                    return (false);
                }
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
            bool blSuccess;
            string szFunction = "ClientScannerGetSession";

            // Lock this command to protect the session object...
            lock (m_objectLockClientApi)
            {
                string szClientCreateCommandId = "";
                string szSessionId = "";

                // Collection session data, if we have any...
                if (m_twainlocalsession != null)
                {
                    szClientCreateCommandId = m_twainlocalsession.ClientCreateCommandId();
                    szSessionId = m_twainlocalsession.GetSessionId();
                }

                // Send the RESTful API command...
                blSuccess = ClientHttpRequest
                (
                    szFunction,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    ClientHttpBuildHeader(),
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + szClientCreateCommandId + "\"," +
                    "\"method\":\"getSession\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + szSessionId + "\"" +
                    "}" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    ApiCmd.HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (!blSuccess)
                {
                    ClientReturnError(a_apicmd, false, "", 0, "");
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// This is an invalid command, it's only used to test certification, please
        /// don't go around adding this to your applications... >.<
        /// </summary>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerInvalidCommand(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            string szFunction = "ClientScannerInvalidCommand";

            // Lock this command to protect the session object...
            lock (m_objectLockClientApi)
            {
                // Send the RESTful API command...
                blSuccess = ClientHttpRequest
                (
                    szFunction,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    ClientHttpBuildHeader(),
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + Guid.NewGuid().ToString() + "\"," +
                    "\"method\":\"invalidCommand\"" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    ApiCmd.HttpReplyStyle.SimpleReply
                );
                if (!blSuccess)
                {
                    ClientReturnError(a_apicmd, false, "", 0, "");
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// This is an invalid uri, it's only used to test certification, please
        /// don't go around adding this to your applications... >.<
        /// </summary>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerInvalidUri(ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            string szFunction = "ClientScannerInvalidUri";

            // Lock this command to protect the session object...
            lock (m_objectLockClientApi)
            {
                // Send the RESTful API command...
                blSuccess = ClientHttpRequest
                (
                    szFunction,
                    ref a_apicmd,
                    "/privet/twaindirect/invaliduri",
                    "GET",
                    ClientHttpBuildHeader(true),
                    null,
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    ApiCmd.HttpReplyStyle.SimpleReply
                );
                if (!blSuccess)
                {
                    ClientReturnError(a_apicmd, false, "", 0, "");
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Read an image block from the scanner...
        /// </summary>
        /// <param name="a_lImageBlockNum">block number to read</param>
        /// <param name="a_blGetMetadataWithImage">ask for the metadata</param>
        /// <param name="a_scancallback">function to call</param>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerReadImageBlock
        (
            long a_lImageBlockNum,
            bool a_blGetMetadataWithImage,
            ScanCallback a_scancallback,
            ref ApiCmd a_apicmd
        )
        {
            bool blSuccess;
            string szImage;
            string szMetaFile;
            string szFunction = "ClientScannerReadImageBlock";

            // Lock this command to protect the session object...
            lock (m_objectLockClientApi)
            {
                string szClientCreateCommandId = "";
                string szSessionId = "";

                // Collection session data, if we have any...
                if (m_twainlocalsession != null)
                {
                    szClientCreateCommandId = m_twainlocalsession.ClientCreateCommandId();
                    szSessionId = m_twainlocalsession.GetSessionId();
                }

                // Build the full image path...
                szImage = Path.Combine(m_szImagesFolder, "img" + a_lImageBlockNum.ToString("D6") + ".tdpdf"); // TWAIN direct temporary pdf

                // Make sure it's clean...
                if (File.Exists(szImage))
                {
                    try
                    {
                        File.Delete(szImage);
                    }
                    catch
                    {
                        ClientReturnError(a_apicmd, false, "critical", -1, szFunction + ": access denied: " + szImage);
                        return (false);
                    }
                }

                // Send the RESTful API command...
                blSuccess = ClientHttpRequest
                (
                    szFunction,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    ClientHttpBuildHeader(),
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + szClientCreateCommandId + "\"," +
                    "\"method\":\"readImageBlock\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + szSessionId + "\"," +
                    (a_blGetMetadataWithImage ? "\"withMetadata\":true," : "") +
                    "\"imageBlockNum\":" + a_lImageBlockNum +
                    "}" +
                    "}",
                    null,
                    szImage,
                    m_iHttpTimeoutData,
                    ApiCmd.HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (!blSuccess)
                {
                    ClientReturnError(a_apicmd, false, "", 0, "");
                    return (false);
                }

                // We asked for metadata...
                string szMetadata = "";
                if (a_blGetMetadataWithImage && (m_twainlocalsession != null))
                {
                    // Try to get the meta data...
                    if (string.IsNullOrEmpty(m_twainlocalsession.GetMetadata()))
                    {
                        m_twainlocalsession.SetMetadata(null);
                        ClientReturnError(a_apicmd, false, "critical", -1, szFunction + ": 'results.metadata' missing for imageBlock=" + a_lImageBlockNum);
                        if (!string.IsNullOrEmpty(a_apicmd.GetHttpResponseData()))
                        {
                            Log.Error(a_apicmd.GetHttpResponseData());
                        }
                        return (false);
                    }

                    // Save the metadata to a file...
                    szMetadata = "{\"metadata\":" + m_twainlocalsession.GetMetadata() + "}";
                    szMetaFile = Path.Combine(m_szImagesFolder, "img" + a_lImageBlockNum.ToString("D6") + ".tdmeta");
                    try
                    {
                        File.WriteAllText(szMetaFile, szMetadata);
                    }
                    catch (Exception exception)
                    {
                        m_twainlocalsession.SetMetadata(null);
                        ClientReturnError(a_apicmd, false, "critical", -1, szFunction + " access denied: " + szMetaFile + " (" + exception.Message + ")");
                        return (false);
                    }
                    Log.Info("metadata: " + szMetaFile);
                }

                // If we have a scanner callback, hit it now...
                if (a_scancallback != null)
                {
                    a_scancallback(a_lImageBlockNum);
                }
            }

            // All done...
            Log.Info("image: " + szImage);
            return (true);
        }

        /// <summary>
        /// Read an image block's TWAIN Direct metadata from the scanner...
        /// </summary>
        /// <param name="a_lImageBlockNum">image block to read</param>
        /// <param name="a_blGetThumbnail">the caller would like a thumbnail</param>
        /// <param name="a_scancallback">function to call</param>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool ClientScannerReadImageBlockMetadata(long a_lImageBlockNum, bool a_blGetThumbnail, ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            string szThumbnail;
            string szMetaFile = "(no session)";
            string szFunction = "ClientScannerReadImageBlockMetadata";

            // Lock this command to protect the session object...
            lock (m_objectLockClientApi)
            {
                string szClientCreateCommandId = "";
                string szSessionId = "";

                // Collection session data, if we have any...
                if (m_twainlocalsession != null)
                {
                    szClientCreateCommandId = m_twainlocalsession.ClientCreateCommandId();
                    szSessionId = m_twainlocalsession.GetSessionId();
                }

                // We're asking for a thumbnail...
                szThumbnail = null;
                if (a_blGetThumbnail)
                {
                    // Build the full image thumbnail path...
                    szThumbnail = Path.Combine(m_szImagesFolder, "img" + a_lImageBlockNum.ToString("D6") + "_thumbnail.tdpdf"); // twain direct temporary pdf

                    // Make sure it's clean...
                    if (File.Exists(szThumbnail))
                    {
                        try
                        {
                            File.Delete(szThumbnail);
                        }
                        catch (Exception exception)
                        {
                            ClientReturnError(a_apicmd, false, "critical", -1, szFunction + ": access denied: " + szThumbnail + " (" + exception.Message + ")");
                            return (false);
                        }
                    }
                }

                // Send the RESTful API command...
                blSuccess = ClientHttpRequest
                (
                    szFunction,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    ClientHttpBuildHeader(),
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + szClientCreateCommandId + "\"," +
                    "\"method\":\"readImageBlockMetadata\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + szSessionId + "\"," +
                    "\"imageBlockNum\":" + a_lImageBlockNum +
                    (a_blGetThumbnail ? ",\"withThumbnail\":true" : "") +
                    "}" +
                    "}",
                    null,
                    a_blGetThumbnail ? szThumbnail : null,
                    m_iHttpTimeoutCommand,
                    ApiCmd.HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (!blSuccess)
                {
                    ClientReturnError(a_apicmd, false, "", 0, "");
                    return (false);
                }

                // Make sure we have a session for this...
                if (m_twainlocalsession != null)
                {
                    // Try to get the meta data...
                    if (string.IsNullOrEmpty(m_twainlocalsession.GetMetadata()))
                    {
                        m_twainlocalsession.SetMetadata(null);
                        ClientReturnError(a_apicmd, false, "critical", -1, szFunction + " 'results.metadata' missing for imageBlock=" + a_lImageBlockNum);
                        if (!string.IsNullOrEmpty(a_apicmd.GetHttpResponseData()))
                        {
                            Log.Error(a_apicmd.GetHttpResponseData());
                        }
                        return (false);
                    }

                    // Save the metadata to a file...
                    szMetaFile = Path.Combine(m_szImagesFolder, "img" + a_lImageBlockNum.ToString("D6") + ".tdmeta");
                    string szMetadata = "{\"metadata\":" + m_twainlocalsession.GetMetadata() + "}";
                    try
                    {
                        File.WriteAllText(szMetaFile, szMetadata);
                    }
                    catch (Exception exception)
                    {
                        m_twainlocalsession.SetMetadata(null);
                        ClientReturnError(a_apicmd, false, "critical", -1, szFunction + " access denied: " + szMetaFile + " (" + exception.Message + ")");
                        return (false);
                    }
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
        /// <param name="a_lImageBlockNum">first block to release</param>
        /// <param name="a_lLastImageBlockNum">last block in range (inclusive)</param>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns></returns>
        public bool ClientScannerReleaseImageBlocks(long a_lImageBlockNum, long a_lLastImageBlockNum, ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            string szFunction = "ClientScannerReleaseImageBlocks";

            // Lock this command to protect the session object...
            lock (m_objectLockClientApi)
            {
                string szClientCreateCommandId = "";
                string szSessionId = "";

                // Collection session data, if we have any...
                if (m_twainlocalsession != null)
                {
                    szClientCreateCommandId = m_twainlocalsession.ClientCreateCommandId();
                    szSessionId = m_twainlocalsession.GetSessionId();
                }

                // Send the RESTful API command...
                blSuccess = ClientHttpRequest
                (
                    szFunction,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    ClientHttpBuildHeader(),
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + szClientCreateCommandId + "\"," +
                    "\"method\":\"releaseImageBlocks\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + szSessionId + "\"," +
                    "\"imageBlockNum\":" + a_lImageBlockNum + "," +
                    "\"lastImageBlockNum\":" + a_lLastImageBlockNum +
                    "}" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    ApiCmd.HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (!blSuccess)
                {
                    ClientReturnError(a_apicmd, false, "", 0, "");
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
        public bool ClientScannerSendTask(string a_szTask, ref ApiCmd a_apicmd)
        {
            bool blSuccess;
            string szFunction = "ClientScannerSendTask";

            // Lock this command to protect the session object...
            lock (m_objectLockClientApi)
            {
                string szClientCreateCommandId = "";
                string szSessionId = "";

                // Collect session data, if we have any...
                if (m_twainlocalsession != null)
                {
                    szClientCreateCommandId = m_twainlocalsession.ClientCreateCommandId();
                    szSessionId = m_twainlocalsession.GetSessionId();
                }

                // Send the RESTful API command...
                blSuccess = ClientHttpRequest
                (
                    szFunction,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    ClientHttpBuildHeader(),
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + szClientCreateCommandId + "\"," +
                    "\"method\":\"sendTask\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + szSessionId + "\"," +
                    "\"task\":" + a_szTask +
                    "}" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    ApiCmd.HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (!blSuccess)
                {
                    ClientReturnError(a_apicmd, false, "", 0, "");
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
            bool blSuccess;
            string szFunction = "ClientScannerStartCapturing";

            // Lock this command to protect the session object...
            lock (m_objectLockClientApi)
            {
                string szClientCreateCommandId = "";
                string szSessionId = "";

                // Init stuff...
                m_iLastImageNumberFromPreviousStartCapturing += m_iLastImageNumberSinceCurrentStartCapturing;
                m_iLastImageNumberSinceCurrentStartCapturing = 0;

                // Collect session data, if we have any...
                if (m_twainlocalsession != null)
                {
                    szClientCreateCommandId = m_twainlocalsession.ClientCreateCommandId();
                    szSessionId = m_twainlocalsession.GetSessionId();
                    m_twainlocalsession.SetSessionStatusSuccess(true);
                    m_twainlocalsession.SetSessionStatusDetected("nominal");
                    m_twainlocalsession.SetSessionDoneCapturing(false);
                    m_twainlocalsession.SetSessionImageBlocksDrained(false);
                }

                // Send the RESTful API command...
                blSuccess = ClientHttpRequest
                (
                    szFunction,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    ClientHttpBuildHeader(),
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + szClientCreateCommandId + "\"," +
                    "\"method\":\"startCapturing\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + szSessionId + "\"" +
                    "}" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    ApiCmd.HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (!blSuccess)
                {
                    ClientReturnError(a_apicmd, false, "", 0, "");
                    if (m_twainlocalsession != null)
                    {
                        m_twainlocalsession.SetSessionDoneCapturing(true);
                        m_twainlocalsession.SetSessionImageBlocksDrained(true);
                    }
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
            bool blSuccess;
            string szFunction = "ClientScannerStopCapturing";

            // Lock this command to protect the session object...
            lock (m_objectLockClientApi)
            {
                string szClientCreateCommandId = "";
                string szSessionId = "";

                // Collection session data, if we have any...
                if (m_twainlocalsession != null)
                {
                    szClientCreateCommandId = m_twainlocalsession.ClientCreateCommandId();
                    szSessionId = m_twainlocalsession.GetSessionId();
                }

                // Send the RESTful API command...
                blSuccess = ClientHttpRequest
                (
                    szFunction,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    ClientHttpBuildHeader(),
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"" + szClientCreateCommandId + "\"," +
                    "\"method\":\"stopCapturing\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + szSessionId + "\"" +
                    "}" +
                    "}",
                    null,
                    null,
                    m_iHttpTimeoutCommand,
                    ApiCmd.HttpReplyStyle.SimpleReplyWithSessionInfo
                );
                if (!blSuccess)
                {
                    ClientReturnError(a_apicmd, false, "", 0, "");
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
            bool blSuccess;
            string szFunction = "ClientScannerWaitForEvents";

            // Lock this command to protect the session object...
            lock (m_objectLockClientApi)
            {
                string szSessionId = "";

                // Collection session data, if we have any...
                if (m_twainlocalsession != null)
                {
                    szSessionId = m_twainlocalsession.GetSessionId();
                }

                // Init our event timeout for HTTPS communication, this value
                // needs to be more than whatever is being used by the scanner.
                int iDefault = 60000; // 60 seconds
                int iHttpTimeoutEvent = (int)Config.Get("httpTimeoutEvent", iDefault);
                if (iHttpTimeoutEvent < 10000)
                {
                    iHttpTimeoutEvent = iDefault;
                }

                // Send the RESTful API command...
                // Both @@@COMMANDID@@@ and @@@SESSIONREVISION@@@ are resolved
                // inside of the ClientScannerWaitForEventsCommunicationHelper thread...
                blSuccess = ClientHttpRequest
                (
                    szFunction,
                    ref a_apicmd,
                    "/privet/twaindirect/session",
                    "POST",
                    ClientHttpBuildHeader(),
                    "{" +
                    "\"kind\":\"twainlocalscanner\"," +
                    "\"commandId\":\"@@@COMMANDID@@@\"," +
                    "\"method\":\"waitForEvents\"," +
                    "\"params\":{" +
                    "\"sessionId\":\"" + szSessionId + "\"," +
                    "\"sessionRevision\":@@@SESSIONREVISION@@@" +
                    "}" +
                    "}",
                    null,
                    null,
                    iHttpTimeoutEvent,
                    ApiCmd.HttpReplyStyle.Event
                );
                if (!blSuccess)
                {
                    ClientReturnError(a_apicmd, false, "", 0, "");
                    return (false);
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Wait for the session object to be updated, this is done
        /// by comparing the current session.revision number to the
        /// session.revision from the last command or event.
        /// </summary>
        /// <param name="a_lMilliseconds">milliseconds to wait for the update</param>
        /// <returns>true if an update was detected, false if the command timed out or was aborted</returns>
        public bool ClientWaitForSessionUpdate(long a_lMilliseconds)
        {
            bool blSignaled = false;

            // Wait for it...
            if (m_twainlocalsession != null)
            {
                blSignaled = m_twainlocalsession.ClientWaitForSessionUpdate(a_lMilliseconds);
            }

            // All done...
            Log.Info("ClientWaitForSessionUpdate - " + (blSignaled ? "true" : "false"));
            return (blSignaled);
        }

        /// <summary>
        /// Create a TWAIN Local Session object.  If this hasn't been done yet you
        /// can specify the X-Privet-Token for testing.  The following have special
        /// meaning:
        /// no_header - no X-Privet-Header
        /// no_token - X-Privet-Header with no data
        /// anything else is droppedin verbatim
        /// </summary>
        /// <param name="a_szXPrivetToken">token to use if we don't have one yet</param>
        public void ClientCertificationTwainLocalSessionCreate
        (
            string a_szXPrivetToken = "no_token"
        )
        {
            if (m_twainlocalsession == null)
            {
                // If we have a token from info or infoex, use it, otherwise make a bad token,
                // as the function indicates this is for the client only, and in fact it's just
                // for the certification tool...
                m_twainlocalsession = new TwainLocalSession(string.IsNullOrEmpty(m_szXPrivetToken) ? a_szXPrivetToken : m_szXPrivetToken);
            }
        }

        /// <summary>
        /// Destroy a TWAIN Local Session object
        /// </summary>
        public void ClientCertificationTwainLocalSessionDestroy(bool a_blSetNoSession = false)
        {
            // Take out the event handler...
            if (m_autoreseteventWaitForEventsProcessing != null)
            {
                m_blCancelWaitForEventsProcessing = true;
                m_autoreseteventWaitForEventsProcessing.Set();
            }

            // Make sure we've ended...
            if (a_blSetNoSession)
            {
                SetSessionState(SessionState.noSession, "Session critical...", false);
            }

            // Take out the session...
            EndSession();
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        protected sealed override void Dispose(bool a_blDisposing)
        {
            // Stop waiting for events...
            if (m_waitforeventsinfo != null)
            {
                m_blCancelWaitForEventsProcessing = true;
                m_autoreseteventWaitForEventsProcessing.Set();
                if (m_waitforeventsinfo.m_apicmd != null)
                {
                    m_waitforeventsinfo.m_apicmd.HttpAbortClientRequest(false);
                }
                m_waitforeventsinfo.Dispose();
                m_waitforeventsinfo = null;
            }

            // No more triggers...
            if (m_autoreseteventWaitForEventsProcessing != null)
            {
                m_autoreseteventWaitForEventsProcessing.Dispose();
                m_autoreseteventWaitForEventsProcessing = null;
            }

            // Zap the rest of it...
            base.Dispose(a_blDisposing);
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
        // Public Definitions
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions

        /// <summary>
        /// Delegate for event callback...
        /// </summary>
        /// <param name="a_object">caller's object</param>
        /// <param name="a_szEvent">event</param>
        public delegate void EventCallback(object a_object, string a_szEvent);

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods

        /// <summary>
        /// Build the HTTP headers needed by the client.  I didn't want to write this
        /// function, because it makes something simple too complex, but I needed it
        /// to help with the certification tool.  Application writers just need to
        /// make sure they set the headers for info/infoex, and the POST commands
        /// sent using the TWAIN Local RESTful API.
        ///
        /// I've commented the code with what apps do vs what the certification tool
        /// needs...
        /// </summary>
        /// <param name="a_blInfoInfoex">build header for info/infoex</param>
        /// <returns>the HTTP headers</returns>
        private string[] ClientHttpBuildHeader(bool a_blInfoInfoex = false)
        {
            string[] aszHeader;
            string szXPrivetToken = "";
            string szContentType = "Content-Type: application/json; charset=UTF-8";

            // Apps do this: collect session data, if we have any...
            if (m_twainlocalsession != null)
            {
                szXPrivetToken = m_twainlocalsession.GetXPrivetToken();

                // Certification only: test the scanner when we don't give it an
                // X-Privet-Token...
                if (szXPrivetToken == "no-token")
                {
                    szXPrivetToken = null;
                }
            }

            // Apps do this: if we're issuing info/infoex command, then we have
            // no content-type and the caller will set the argument to null
            // or an empty string.  All other TWAIN Local commands use POST,
            // and must specify a content type...
            if (a_blInfoInfoex)
            {
                // Certification only: we have no header...
                if (szXPrivetToken == null)
                {
                    return (null);
                }

                // Apps do this: send a privet token, the recommendation
                // for info/infoex is an empty string, indicated with two
                // double-quotes "".
                aszHeader = new string[] {
                    "X-Privet-Token: \"\""
                };
                return (aszHeader);
            }

            // Certification only: build the header, we're only allowing
            // this kind of flexibility to support the Certification tool.
            // Application writers shouldn't support this part of the code,
            // except to generate an error, if they've not first obtained
            // an X-Privet-Token...
            if (szXPrivetToken == null)
            {
                aszHeader = new string[] {
                    szContentType
                };
                return (aszHeader);
            }

            // Apps do this: for any POST commands...
            aszHeader = new string[] {
                szContentType,
                "X-Privet-Token: " + szXPrivetToken
            };
            return (aszHeader);
        }

        /// <summary>
        /// One stop shop for sending commands to the scanner.
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
        private bool ClientHttpRequest
        (
            string a_szReason,
            ref ApiCmd a_apicmd,
            string a_szUri,
            string a_szMethod,
            string[] a_aszHeader,
            string a_szData,
            string a_szUploadFile,
            string a_szOutputFile,
            int a_iTimeout,
            ApiCmd.HttpReplyStyle a_httpreplystyle
        )
        {
            bool blSuccess;
            string szCode;

            // Our normal path...
            if (a_httpreplystyle != ApiCmd.HttpReplyStyle.Event)
            {
                // Send the RESTful API command...
                blSuccess = a_apicmd.HttpRequest
                (
                    a_szReason,
                    a_szUri,
                    a_szMethod,
                    a_aszHeader,
                    a_szData,
                    a_szUploadFile,
                    a_szOutputFile,
                    a_iTimeout,
                    a_httpreplystyle
                );
                if (!blSuccess)
                {
                    return (false);
                }

                // Try to get any session data that may be in the payload...
                blSuccess = ParseSession(a_szReason, a_apicmd, out szCode);
                if (!blSuccess)
                {
                    ClientReturnError(a_apicmd, false, szCode, -1, a_szReason + ": ParseSession failed - " + szCode);
                    return (false);
                }
            }

            // Handle events, we only expect to come down this path
            // once per session...
            else
            {
                // We already have a thread, so shut it down...
                if (m_waitforeventsinfo != null)
                {
                    m_waitforeventsinfo.Dispose();
                    m_waitforeventsinfo = null;
                }

                // Make sure the cancel flag is off...
                m_blCancelWaitForEventsProcessing = false;

                // Squirrel the information away for the new thread...
                m_waitforeventsinfo = new WaitForEventsInfo();
                m_waitforeventsinfo.m_apicmd = a_apicmd;
                m_waitforeventsinfo.m_threadCommunication = new Thread(new ParameterizedThreadStart(ClientScannerWaitForEventsCommunicationLaunchpad));
                m_waitforeventsinfo.m_threadProcessing = new Thread(new ParameterizedThreadStart(ClientScannerWaitForEventsProcessingLaunchpad));
                m_waitforeventsinfo.m_szReason = a_szReason;
                m_waitforeventsinfo.m_dnssddeviceinfo = a_apicmd.GetDnssdDeviceInfo();
                m_waitforeventsinfo.m_szUri = a_szUri;
                m_waitforeventsinfo.m_szMethod = a_szMethod;
                m_waitforeventsinfo.m_aszHeader = a_aszHeader;
                m_waitforeventsinfo.m_szData = a_szData;
                m_waitforeventsinfo.m_szUploadFile = a_szUploadFile;
                m_waitforeventsinfo.m_szOutputFile = a_szOutputFile;
                m_waitforeventsinfo.m_iTimeout = a_iTimeout;
                m_waitforeventsinfo.m_httpreplystyle = a_httpreplystyle;

                // Start the threads...
                m_waitforeventsinfo.m_threadProcessing.Start(this);
                m_waitforeventsinfo.m_threadCommunication.Start(this);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Help with waiting for events (communication)...
        /// </summary>
        /// <param name="a_objectParameters">our object</param>
        private void ClientScannerWaitForEventsCommunicationLaunchpad
        (
            object a_objectParameters
        )
        {
            TwainLocalScannerClient twainlocalscannerclient;
            twainlocalscannerclient = (TwainLocalScannerClient)a_objectParameters;
            twainlocalscannerclient.ClientScannerWaitForEventsCommunicationHelper();
        }

        /// <summary>
        /// Help with waiting for events (communication)...
        /// </summary>
        private void ClientScannerWaitForEventsCommunicationHelper()
        {
            bool blSuccess;
            long lSessionRevision = 0;
            string szSessionState;
            ApiCmd apicmd = m_waitforeventsinfo.m_apicmd;

            // Loop until something stops us...
            for (;;)
            {
                string szClientCreateCommandId = "";
                string szSessionRevision = "0";
                SessionState sessionstate = SessionState.noSession;

                // Get data from our session...
                if (m_twainlocalsession != null)
                {
                    szClientCreateCommandId = m_twainlocalsession.ClientCreateCommandId();
                    szSessionRevision = m_twainlocalsession.GetSessionRevision().ToString();
                    sessionstate = m_twainlocalsession.GetSessionState();
                }

                // Update the data, first we need a new command id for this
                // instance of the long poll...
                string szData = m_waitforeventsinfo.m_szData;
                szData = szData.Replace("@@@COMMANDID@@@", szClientCreateCommandId);

                // Session data is protected...
                lock (m_waitforeventsinfo.m_objectlapicmdLock)
                {
                    // Report our current session id to the scanner, this
                    // will either be the revision from the session object
                    // or from the last event...
                    szData = szData.Replace("@@@SESSIONREVISION@@@", (lSessionRevision == 0) ? szSessionRevision : lSessionRevision.ToString());

                    // If we've gone to noSession, we should scoot, since
                    // it's no longer possible to receive events...
                    if (m_twainlocalsession == null/* || (sessionstate == SessionState.noSession)*/)
                    {
                        // Initialize the object, and that's it...
                        apicmd.HttpRequest
                        (
                            m_waitforeventsinfo.m_szReason,
                            m_waitforeventsinfo.m_szUri,
                            m_waitforeventsinfo.m_szMethod,
                            m_waitforeventsinfo.m_aszHeader,
                            szData,
                            m_waitforeventsinfo.m_szUploadFile,
                            m_waitforeventsinfo.m_szOutputFile,
                            m_waitforeventsinfo.m_iTimeout,
                            m_waitforeventsinfo.m_httpreplystyle,
                            true
                        );
                        apicmd.DeviceResponseSetStatus
                        (
                            false,
                            "invalidSessionId",
                            -1,
                            "{" +
                            "\"kind\":\"twainlocalscanner\"," +
                            "\"commandId\":\"" + szClientCreateCommandId + "\"," +
                            "\"method\":\"waitForEvents\"," +
                            "\"results\":{" +
                            "\"success\":false," +
                            "\"code\":\"invalidSessionId\"" +
                            "}" + // results
                            "}", //root
                            200
                        );
                        apicmd.WaitForEventsCallback();
                        return;
                    }
                }

                // We've been asked to scoot...
                if (m_blCancelWaitForEventsProcessing)
                {
                    return;
                }

                // Send the RESTful API command...
                blSuccess = apicmd.HttpRequest
                (
                    m_waitforeventsinfo.m_szReason,
                    m_waitforeventsinfo.m_szUri,
                    m_waitforeventsinfo.m_szMethod,
                    m_waitforeventsinfo.m_aszHeader,
                    szData,
                    m_waitforeventsinfo.m_szUploadFile,
                    m_waitforeventsinfo.m_szOutputFile,
                    m_waitforeventsinfo.m_iTimeout,
                    m_waitforeventsinfo.m_httpreplystyle
                );

                // We've been asked to scoot...
                if (m_blCancelWaitForEventsProcessing)
                {
                    return;
                }

                // Handle errors...
                if (!blSuccess)
                {
                    switch (apicmd.HttpStatus())
                    {
                        // Ruh-roh...
                        default:
                            Log.Error("ClientScannerWaitForEventsHelper: bad status..." + apicmd.HttpStatus());
                            continue;

                        // Issue a new command...
                        case WebExceptionStatus.ReceiveFailure:
                        case WebExceptionStatus.Timeout:
                            continue;
                    }
                }

                // Send this off for processing, we have to lock when adding
                // it to the list...
                lock (m_waitforeventsinfo.m_objectlapicmdLock)
                {
                    m_waitforeventsinfo.m_lapicmdEvents.Add(apicmd);
                }

                // We need a new one of these for the next long poll, the
                // code in this function collects the session state and
                // the session revision...
                apicmd = new ApiCmd(apicmd, out szSessionState, out lSessionRevision);

                // Wake up the processor...
                m_autoreseteventWaitForEventsProcessing.Set();

                // We're done, we can stop monitoring for events...
                if ((sessionstate == SessionState.noSession) || (szSessionState == "noSession"))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Help with waiting for events (processing)...
        /// </summary>
        /// <param name="a_objectParameters">our object</param>
        private void ClientScannerWaitForEventsProcessingLaunchpad
        (
            object a_objectParameters
        )
        {
            TwainLocalScannerClient twainlocalscannerclient;
            twainlocalscannerclient = (TwainLocalScannerClient)a_objectParameters;
            m_blCancelWaitForEventsProcessing = false;
            twainlocalscannerclient.ClientScannerWaitForEventsProcessingHelper();
        }

        /// <summary>
        /// Help with waiting for events (processing)...
        /// </summary>
        private void ClientScannerWaitForEventsProcessingHelper()
        {
            // Loop until something stops us...
            for (;;)
            {
                // We've been cancelled...
                if (m_blCancelWaitForEventsProcessing)
                {
                    // We've been asked to scoot...
                    if ((m_waitforeventsinfo != null) && (m_waitforeventsinfo.m_apicmd != null))
                    {
                        m_waitforeventsinfo.m_apicmd.HttpAbortClientRequest(false);
                    }
                    return;
                }

                // Wait for the communication thread to give us work...
                if (!m_autoreseteventWaitForEventsProcessing.WaitOne())
                {
                    // We've been asked to scoot...
                    if ((m_waitforeventsinfo != null) && (m_waitforeventsinfo.m_apicmd != null))
                    {
                        m_waitforeventsinfo.m_apicmd.HttpAbortClientRequest(false);
                    }
                    return;
                }

                // We've been cancelled...
                if (m_blCancelWaitForEventsProcessing)
                {
                    // We've been asked to scoot...
                    if ((m_waitforeventsinfo != null) && (m_waitforeventsinfo.m_apicmd != null))
                    {
                        m_waitforeventsinfo.m_apicmd.HttpAbortClientRequest(false);
                    }
                    return;
                }

                // Loop for as long as we find data in the list...
               while (m_waitforeventsinfo != null)
                {
                    ApiCmd apicmd;

                    // Pull the first item from the list, if the list is empty
                    // then drop out of this loop.  We have to protect this
                    // action, since the communication thread can add new content
                    // at any time...
                    lock (m_waitforeventsinfo.m_objectlapicmdLock)
                    {
                        // No more data...
                        if (m_waitforeventsinfo.m_lapicmdEvents.Count == 0)
                        {
                            break;
                        }

                        // Get the first item...
                        apicmd = m_waitforeventsinfo.m_lapicmdEvents[0];

                        // Delete the first item from the list...
                        m_waitforeventsinfo.m_lapicmdEvents.RemoveAt(0);
                    }

                    // Handle the event, at this point all we ever expect to see
                    // are updates for the session object...
                    lock (m_objectLockClientApi)
                    {
                        string szCode;

                        // Parse the data...
                        ParseSession(m_waitforeventsinfo.m_szReason, apicmd, out szCode);

                        // If we've gone to noSession, we should scoot, since
                        // it's no longer possible to receive events...
                        if ((m_twainlocalsession == null) || (m_twainlocalsession.GetSessionState() == SessionState.noSession))
                        {
                            // Do the callback, if we have one...
                            apicmd.WaitForEventsCallback();
                            break;
                        }
                    }

                    // Do the callback, if we have one...
                    apicmd.WaitForEventsCallback();
                }
            }
        }

        /// <summary>
        /// Parse data from the session object.  We do this in a number
        /// of places, so it makes sense to centralize...
        /// </summary>
        /// <param name="a_httpreplystyle">the reply style</param>
        /// <param name="a_apicmd">the command object</param>
        /// <param name="a_jsonlookup">data to check</param>
        /// <returns>true on success</returns>
        private bool ParseSession(string a_szReason, ApiCmd a_apicmd, out string a_szCode)
        {
            bool blSuccess;
            bool blIsInfoOrInfoex;
            int ii;
            int iSessionRevision;
            long lResponseCharacterOffset;
            string szImageBlocks;
            string szFunction = "ParseSession";
            JsonLookup jsonlookup;

            // Parse the JSON in the response, we always have to make sure
            // its valid...
            jsonlookup = new JsonLookup();
            string szHttpResponseData = a_apicmd.GetHttpResponseData();
            blSuccess = jsonlookup.Load(szHttpResponseData, out lResponseCharacterOffset);
            if (!blSuccess)
            {
                ClientReturnError(a_apicmd, false, "invalidJson", lResponseCharacterOffset, a_szReason + ": ClientHttpRequest JSON syntax error...");
                a_szCode = "critical";
                return (false);
            }

            // Are we info or infoex?
            blIsInfoOrInfoex = ((a_apicmd.GetUri() == "/privet/info") || (a_apicmd.GetUri() == "/privet/infoex"));

            // Run-roh...
            if (!blIsInfoOrInfoex && jsonlookup.Get("results.success") == "false")
            {
                // Get the code...
                a_szCode = jsonlookup.Get("results.code");
                if (string.IsNullOrEmpty(a_szCode))
                {
                    Log.Error("results.code is missing, so we're assuming 'critical'...");
                    a_szCode = "critical";
                }

                // If we've lost the session, we might as well zap things here...
                switch (a_szCode)
                {
                    default: break;
                    case "critical":
                        SetSessionState(SessionState.noSession);
                        EndSession();
                        break;
                    case "invalidSessionId":
                        SetSessionState(SessionState.noSession);
                        EndSession();
                        break;
                }

                // Bail...
                return (false);
            }

            // We expect success from this point on, unless set otherwise...
            a_szCode = "success";

            // If we don't have one of these styles, we can't have any session
            // data, so bail here...
            if (    (a_apicmd.GetHttpReplyStyle() != ApiCmd.HttpReplyStyle.SimpleReplyWithSessionInfo)
                &&  (a_apicmd.GetHttpReplyStyle() != ApiCmd.HttpReplyStyle.Event))
            {
                return (true);
            }


            // Handle /privet/info and /privet/infoex
            #region Handle /privet/info and /privet/infoex
            if (blIsInfoOrInfoex)
            {
                // Squirrel away the x-privet-token so that we'll have
                // it for when createSession is called.  Note that this
                // is only done for the client, the device never does
                // anything with this attribute...
                m_szXPrivetToken = jsonlookup.Get("x-privet-token");

                // All done...
                return (true);
            }
            #endregion


            // Handle events
            #region Handle Events
            if (a_apicmd.GetHttpReplyStyle() == ApiCmd.HttpReplyStyle.Event)
            {
                // Handle any and all of the event data...
                if (!string.IsNullOrEmpty(jsonlookup.Get("results.events", false)))
                {
                    // Loop through it...
                    for (ii = 0; ; ii++)
                    {
                        string szEvent = "results.events[" + ii + "]";

                        // We're out of events...
                        if (string.IsNullOrEmpty(jsonlookup.Get(szEvent, false)))
                        {
                            break;
                        }

                        // Check the session revision number...
                        if (!int.TryParse(jsonlookup.Get(szEvent + ".session.revision", false), out iSessionRevision))
                        {
                            Log.Error(szFunction + ": bad session revision number...");
                            continue;
                        }

                        // Only do this bit if the number is newer than what
                        // we already have...
                        if (!m_twainlocalsession.SetSessionRevision(iSessionRevision, true))
                        {
                            continue;
                        }

                        // Change our state...
                        bool blNoSession = false;
                        switch (jsonlookup.Get(szEvent + ".session.state"))
                        {
                            // Uh-oh...
                            default:
                                Log.Error(szFunction + ":Unrecognized session.state..." + jsonlookup.Get(szEvent + ".session.state"));
                                a_szCode = "critical";
                                return (false);

                            case "capturing":
                                SetSessionState(SessionState.capturing);
                                break;

                            case "closed":
                                // We can't truly close until all the imageblocks are resolved...
                                if ((m_twainlocalsession.m_alSessionImageBlocks == null)
                                    || (m_twainlocalsession.m_alSessionImageBlocks.Length == 0))
                                {
                                    SetSessionState(SessionState.noSession);
                                    EndSession();
                                }
                                else
                                {
                                    SetSessionState(SessionState.closed);
                                }
                                break;

                            case "draining":
                                SetSessionState(SessionState.draining);
                                break;

                            case "noSession":
                                blNoSession = true;
                                break;

                            case "ready":
                                SetSessionState(SessionState.ready);
                                break;
                        }

                        // Okay, now we can process this event...
                        switch (jsonlookup.Get(szEvent + ".event", false))
                        {
                            // Ignore unrecognized events...
                            default:
                                Log.Verbose(szFunction + ": unrecognized event..." + jsonlookup.Get(szEvent + ".event", false));
                                if (blNoSession)
                                {
                                    SetSessionState(SessionState.noSession);
                                    EndSession();
                                }
                                break;

                            // Our scanner session went bye-bye on us...
                            case "critical":
                                // Change state...
                                SetSessionState(SessionState.noSession);

                                // We'd like the events communication and processing threads
                                // to exit, however we might be in the processing thread when
                                // we get this message, so just set the flag...
                                m_blCancelWaitForEventsProcessing = true;
                                m_autoreseteventWaitForEventsProcessing.Set();

                                // Our reply...
                                Log.Info(szFunction + ": critical event...");

                                // Our callback...
                                if (m_eventcallback != null)
                                {
                                    m_eventcallback(m_objectEventCallback, "critical");
                                }

                                // Wake up anybody watching us...
                                ClientWaitForSessionUpdateForceSet();

                                // End session.
                                if (blNoSession)
                                {                                 
                                    EndSession();
                                }

                                // All done...
                                break;

                            // The session object has been updated, specifically we have a change
                            // to the imageBlocks array from stuff being added or removed...
                            case "imageBlocks":
                                Log.Verbose(szFunction + ": imageBlocks event...");
                                lock (m_objectLockClientApi)
                                {
                                    // Only set the imageBlocksDrained to true if we're told to,
                                    // don't set them to false.  That's done once during the state
                                    // change to capturing...
                                    if (jsonlookup.Get(szEvent + ".session.imageBlocksDrained", false) == "true")
                                    {
                                        m_twainlocalsession.SetSessionImageBlocksDrained(true);
                                    }

                                    // Get the image blocks...
                                    m_twainlocalsession.m_alSessionImageBlocks = null;
                                    szImageBlocks = jsonlookup.Get(szEvent + ".session.imageBlocks", false);
                                    if (!string.IsNullOrEmpty(szImageBlocks))
                                    {
                                        string[] aszImageBlocks = szImageBlocks.Split(new char[] { '[', ' ', ',', ']', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                        if (aszImageBlocks != null)
                                        {
                                            m_twainlocalsession.m_alSessionImageBlocks = new long[aszImageBlocks.Length];
                                            for (ii = 0; ii < aszImageBlocks.Length; ii++)
                                            {
                                                m_twainlocalsession.m_alSessionImageBlocks[ii] = long.Parse(aszImageBlocks[ii]);
                                            }
                                        }
                                    }

                                    // See if we've detected any new problems...
                                    if (m_twainlocalsession.GetSessionStatusSuccess())
                                    {
                                        string szSessionStatusSuccess = jsonlookup.Get(szEvent + ".session.status.success", false);
                                        if (!string.IsNullOrEmpty(szSessionStatusSuccess) && (szSessionStatusSuccess == "false"))
                                        {
                                            string szSessionStatusDetected = jsonlookup.Get(szEvent + ".session.status.detected", false);
                                            m_twainlocalsession.SetSessionStatusSuccess(false);
                                            m_twainlocalsession.SetSessionStatusDetected(string.IsNullOrEmpty(szSessionStatusDetected) ? "misfeed" : szSessionStatusDetected);
                                        }
                                    }

                                    // Our callback...
                                    if (m_eventcallback != null)
                                    {
                                        m_eventcallback(m_objectEventCallback, "imageBlocks");
                                    }

                                    // Wakeup anybody watching us...
                                    ClientWaitForSessionUpdateForceSet();

                                    // End session.
                                    if (blNoSession)
                                    {
                                        SetSessionState(SessionState.noSession);
                                    }
                                }
                                break;

                            // Our scanner session went bye-bye on us...
                            case "sessionTimedOut":
                                // Change state...
                                SetSessionState(SessionState.noSession);

                                // We'd like the events communication and processing threads
                                // to exit, however we might be in the processing thread when
                                // we get this message, so just set the flag...
                                m_blCancelWaitForEventsProcessing = true;
                                m_autoreseteventWaitForEventsProcessing.Set();

                                // Our reply...
                                Log.Info(szFunction + ": sessionTimedOut event...");

                                // Our callback...
                                if (m_eventcallback != null)
                                {
                                    m_eventcallback(m_objectEventCallback, "sessionTimedOut");
                                }

                                // End session.
                                if (blNoSession)
                                {                                  
                                    EndSession();
                                }

                                // Wake up anybody watching us...
                                ClientWaitForSessionUpdateForceSet();

                                // All done...
                                break;

                            // The event has timed out, we can ignore this one...
                            case "timeout":
                                // End session.
                                if (blNoSession)
                                {
                                    SetSessionState(SessionState.noSession);
                                    EndSession();
                                }
                                break;
                        }
                    }
                }

                // All done...
                return (true);
            }
            #endregion


            // Handle API responses
            #region Handle API responses

            // Init stuff...
            m_twainlocalsession.SetSessionId(jsonlookup.Get("results.session.sessionId"));

            // Set the metadata, if we have any, we don't care if we
            // succeed, the caller will worry about that...
            m_twainlocalsession.SetMetadata(jsonlookup.Get("results.metadata", false));

            // If we don't have a session id, then skip the rest of
            // this function...
            if (string.IsNullOrEmpty(m_twainlocalsession.GetSessionId()))
            {
                a_szCode = "invalidSessionId";
                return (false);
            }

            // Check the session revision number...
            if (!int.TryParse(jsonlookup.Get("results.session.revision", false), out iSessionRevision))
            {
                Log.Error(szFunction + ": bad session revision number...");
                a_szCode = "critical";
                return (false);
            }

            // If the session revision number we just received is less
            // than or equal to the one we already have, then skip the
            // rest of this function.  Otherwise, save the number...
            if (!m_twainlocalsession.SetSessionRevision(iSessionRevision))
            {
                return (true);
            }

            // We're going to refresh this...
            m_twainlocalsession.m_alSessionImageBlocks = null;

            // Protect ourselves from weirdness...
            try
            {
                // Only set the imageBlocksDrained to true if we're told to,
                // don't set them to false.  That's done once during the state
                // change to capturing...
                if (jsonlookup.Get("results.session.imageBlocksDrained", false) == "true")
                {
                    m_twainlocalsession.SetSessionImageBlocksDrained(true);
                }

                // Collect the image blocks data...
                m_twainlocalsession.m_alSessionImageBlocks = null;
                szImageBlocks = jsonlookup.Get("results.session.imageBlocks", false);
                if (!string.IsNullOrEmpty(szImageBlocks))
                {
                    string[] aszImageBlocks = szImageBlocks.Split(new char[] { '[', ' ', ',', ']', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (aszImageBlocks != null)
                    {
                        m_twainlocalsession.m_alSessionImageBlocks = new long[aszImageBlocks.Length];
                        for (ii = 0; ii < aszImageBlocks.Length; ii++)
                        {
                            m_twainlocalsession.m_alSessionImageBlocks[ii] = int.Parse(aszImageBlocks[ii]);
                        }
                    }
                }

                // Change our state...
                switch (jsonlookup.Get("results.session.state"))
                {
                    // Uh-oh...
                    default:
                        Log.Error(szFunction + ":Unrecognized results.session.state..." + jsonlookup.Get("results.session.state"));
                        a_szCode = "critical";
                        return (false);

                    case "capturing":
                        SetSessionState(SessionState.capturing);
                        break;

                    case "closed":
                        // We can't truly close until all the imageblocks are resolved...
                        if ((m_twainlocalsession.m_alSessionImageBlocks == null)
                            || (m_twainlocalsession.m_alSessionImageBlocks.Length == 0))
                        {
                            SetSessionState(SessionState.noSession);
                            EndSession();
                        }
                        else
                        {
                            SetSessionState(SessionState.closed);
                        }
                        break;

                    case "draining":
                        SetSessionState(SessionState.draining);
                        break;

                    case "noSession":
                        SetSessionState(SessionState.noSession);
                        EndSession();
                        break;

                    case "ready":
                        SetSessionState(SessionState.ready);
                        break;
                }
            }
            catch (Exception exception)
            {
                Log.Error(szFunction + ": exception..." + exception.Message);
                m_twainlocalsession.SetSessionId(null);
                m_twainlocalsession.SetCallersHostName(null);
                m_twainlocalsession.m_alSessionImageBlocks = null;
                m_twainlocalsession.SetSessionImageBlocksDrained(true);
                a_szCode = "critical";
                return (false);
            }

            // All done...
            return (true);

            #endregion
        }

        /// <summary>
        /// Set the session state, and do additional cleanup work, if needed...
        /// </summary>
        /// <param name="a_sessionstate">new session state</param>
        /// <param name="a_szSessionEndedMessage">message to display when going to noSession</param>
        /// <param name="a_blUserShutdown">the user requested the close</param>
        /// <returns>the previous session state</returns>
        protected override SessionState SetSessionState
        (
            SessionState a_sessionstate,
            string a_szSessionEndedMessage = "Session ended...",
            bool a_blUserShutdown = true
        )
        {
            SessionState sessionstatePrevious;

            // Let the base set it's session state...
            sessionstatePrevious = base.SetSessionState(a_sessionstate, a_szSessionEndedMessage, a_blUserShutdown);

            // Return the previous state...
            return (sessionstatePrevious);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes
        // All of the members in this section must be specific to the client
        // and not to the session.  Session stuff goes into TwainLocalSession.
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes

        /// <summary>
        /// Event callback function...
        /// </summary>
        private EventCallback m_eventcallback;

        /// <summary>
        /// Caller's object for the event callback function...
        /// </summary>
        private object m_objectEventCallback;

        /// <summary>
        /// Command timeout, this should be short (and in milliseconds)...
        /// </summary>
        private int m_iHttpTimeoutCommand;

        /// <summary>
        /// Data timeout, this should be long (and in milliseconds)...
        /// </summary>
        private int m_iHttpTimeoutData;

        /// <summary>
        /// Something we can lock...
        /// </summary>
        private object m_objectLockClientApi;
        private object m_objectLockClientFinishImage;
        private object m_objectLockEndSession;

        /// <summary>
        /// We maintain a list of the image blocks that we've not
        /// yet turned into finished images.
        /// </summary>
        private List<long> m_llPendingImageBlocks;

        /// <summary>
        /// We keep track of the last imageNumber from the current
        /// scan and from the previous scan, so that we can maintain
        /// a contiguous and growing set of image numbers for the
        /// files we output for a complete session.  We do this by
        /// adding the last image number from the current session
        /// to the previous session tally...
        /// </summary>
        private long m_iLastImageNumberSinceCurrentStartCapturing;
        private long m_iLastImageNumberFromPreviousStartCapturing;

        /// <summary>
        /// This is where the imageBlocks and metadata are stored.
        /// </summary>
        private string m_szImagesFolder;

        /// <summary>
        /// Event info...
        /// </summary>
        private WaitForEventsInfo m_waitforeventsinfo;

        /// <summary>
        /// We only need this value long enough to get it from
        /// info or infoex to createSession, and specifically
        /// into the TwainLocalSession object, which will maintain
        /// it for the life of the session...
        /// </summary>
        private string m_szXPrivetToken;

        /// <summary>
        /// Our signal to the client that an event has arrived...
        /// </summary>
        private AutoResetEvent m_autoreseteventWaitForEventsProcessing;
        private bool m_blCancelWaitForEventsProcessing;

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Wait For Events Info
        // A waitForEvents manager, it takes care of creating and running a
        // thread that receives and dispatches events, and issues a new
        // waitForEvents command.
        ///////////////////////////////////////////////////////////////////////////////
        #region Class: Wait For Events Info

        /// <summary>
        /// Information for waiting for events...
        /// </summary>
        private sealed class WaitForEventsInfo : IDisposable
        {
            /// <summary>
            /// Constructor...
            /// </summary>
            public WaitForEventsInfo()
            {
                m_lapicmdEvents = new List<ApiCmd>();
                m_objectlapicmdLock = new object();
            }

            /// <summary>
            /// Destructor...
            /// </summary>
            ~WaitForEventsInfo()
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
            /// Cleanup...
            /// </summary>
            /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
            internal void Dispose(bool a_blDisposing)
            {
                if (m_apicmd != null)
                {
                    try
                    {
                        m_apicmd.HttpAbortClientRequest(false);
                    }
                    catch
                    {
                    }
                    m_apicmd = null;
                }
                if (m_threadCommunication != null)
                {
                    try
                    {
                        m_threadCommunication.Abort();
                        m_threadCommunication.Join();
                    }
                    catch
                    {
                    }
                    m_threadCommunication = null;
                }
                if (m_threadProcessing != null)
                {
                    try
                    {
                        m_threadProcessing.Abort();
                        m_threadProcessing.Join();
                    }
                    catch
                    {
                    }
                    m_threadProcessing = null;
                }
            }

            /// <summary>
            /// Close down our threads...
            /// </summary>
            public void EndSession()
            {
                // Make sure the event api command knows to exit...
                if (m_apicmd != null)
                {
                    m_apicmd.HttpAbortClientRequest(false);
                }

                // Shutdown the communication thread...
                if (m_threadCommunication != null)
                {
                    try
                    {
                        m_threadCommunication.Abort();
                        //m_threadCommunication.Join();
                    }
                    catch
                    {
                    }
                    m_threadCommunication = null;
                }

                // Shutdown the processing thread...
                if (m_threadProcessing != null)
                {
                    try
                    {
                        //m_threadProcessing.Abort();
                        //m_threadProcessing.Join();
                    }
                    catch
                    {
                    }
                    m_threadProcessing = null;
                }
            }

            // The ApiCmd used to issue the waitForCommand...
            public ApiCmd m_apicmd;

            /// <summary>
            /// The communication thread issues the long poll HTTP
            /// request, which is sent to the processing thread...
            /// </summary>
            public Thread m_threadCommunication;

            /// <summary>
            /// The processing thread does the real work on whatever
            /// is received from the communication thread...
            /// </summary>
            public Thread m_threadProcessing;

            /// <summary>
            /// Our list of events passed from the communication thread
            /// to the processing thread.  If they come in fast, before
            /// we have a chance to process any of them, they'll bunch
            /// up here.  This list is protected by a lock...
            /// </summary>
            public List<ApiCmd> m_lapicmdEvents;
            public object m_objectlapicmdLock;

            // Arguments for the waitForEvents command...
            public string m_szReason;
            public Dnssd.DnssdDeviceInfo m_dnssddeviceinfo;
            public string m_szUri;
            public string m_szMethod;
            public string[] m_aszHeader;
            public string m_szData;
            public string m_szUploadFile;
            public string m_szOutputFile;
            public int m_iTimeout;
            public ApiCmd.HttpReplyStyle m_httpreplystyle;
        }

        #endregion
    }

    /// <summary>
    /// Stuff shared by TwainLocalScannerDevice and TwainLocalScannerClient...
    /// </summary>
    public abstract class TwainLocalScanner : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods

        /// <summary>
        /// Init us...
        /// </summary>
        /// <param name="a_blCreateTwainLocalSession">true for the server only</param>
        public TwainLocalScanner
        (
            bool a_blCreateTwainLocalSession
        )
        {
            // Init stuff...
            m_szWriteFolder = Config.Get("writeFolder", "");

            // Set up session specific content...
            if (a_blCreateTwainLocalSession)
            {
                m_twainlocalsessionInfo = new TwainLocalSession("");
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
        /// End the session, and do all necessary cleanup...
        /// </summary>
        public virtual void EndSession()
        {
            // Lose the session...
            if (m_twainlocalsession != null)
            {
                m_twainlocalsession.SetUserShutdown(false);
                m_twainlocalsession.EndSession();
                m_twainlocalsession.Dispose();
                m_twainlocalsession = null;
            }
        }

        /// <summary>
        /// Collect information for our device info...
        /// </summary>
        /// <returns></returns>
        public Dnssd.DnssdDeviceInfo GetDnssdDeviceInfo()
        {
            Dnssd.DnssdDeviceInfo dnssddeviceinfo;

            // Create it...
            dnssddeviceinfo = new Dnssd.DnssdDeviceInfo();

            // Stock it...
            dnssddeviceinfo.SetLinkLocal("");
            dnssddeviceinfo.SetServiceName(m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalInstanceName());
            dnssddeviceinfo.SetTxtCs("offline");
            dnssddeviceinfo.SetTxtHttps(true);
            dnssddeviceinfo.SetTxtId("");
            dnssddeviceinfo.SetTxtNote(m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalNote());
            dnssddeviceinfo.SetTxtTxtvers("1");
            dnssddeviceinfo.SetTxtTy(m_twainlocalsessionInfo.DeviceRegisterGetTwainLocalTy());
            dnssddeviceinfo.SetTxtType("twaindirect");

            // Return it...
            return (dnssddeviceinfo);
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
        /// Return the current state...
        /// </summary>
        /// <returns>the enum as a string</returns>
        public string GetState()
        {
            if (m_twainlocalsession == null)
            {
                return ("noSession");
            }
            return (m_twainlocalsession.GetSessionState().ToString());
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

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions

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
            invalidCapturingOptions = 7,
            busy = 8,
            noMedia = 9
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
        /// Delegate for the long poll event processor.  This function
        /// is called for every event received from the scanner.
        /// </summary>
        /// <param name="a_object">An object supplied by the caller when it registers the callback</param>
        public delegate void WaitForEventsProcessingCallback(ApiCmd a_apicmd, object a_object);

        /// <summary>
        /// Delegate for the scan callback...
        /// </summary>
        /// <param name="a_lImageBlock">the image block we're working on</param>
        /// <returns></returns>
        public delegate bool ScanCallback(long a_lImageBlock);

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Protected methods
        ///////////////////////////////////////////////////////////////////////////////
        #region Protected Common Methods

        /// <summary>
        /// Set the session state, and do additional cleanup work, if needed...
        /// </summary>
        /// <param name="a_sessionstate">new session state</param>
        /// <param name="a_szSessionEndedMessage">message to display when going to noSession</param>
        /// <param name="a_blUserShutdown">the user requested the close</param>
        /// <returns>the previous session state</returns>
        protected virtual SessionState SetSessionState
        (
            SessionState a_sessionstate,
            string a_szSessionEndedMessage = "Session ended...",
            bool a_blUserShutdown = true
        )
        {
            SessionState sessionstatePrevious = SessionState.noSession;

            // First set the session's state...
            if (m_twainlocalsession != null)
            {
                sessionstatePrevious = m_twainlocalsession.GetSessionState();
                m_twainlocalsession.SetSessionState(a_sessionstate);
            }

            // Return the previous state...
            return (sessionstatePrevious);
        }

        /// <summary>
        /// Set the error return for client functions...
        /// </summary>
        /// <param name="a_apicmd">the current command</param>
        /// <param name="a_blSuccess">our success status</param>
        /// <param name="a_szResponseCode">the error code</param>
        /// <param name="a_lResponseCharacterOffset">the offset of a JSON error, or -1</param>
        /// <param name="a_szResponseText">extra info about the error</param>
        protected void ClientReturnError(ApiCmd a_apicmd, bool a_blSuccess, string a_szResponseCode, long a_lResponseCharacterOffset, string a_szResponseText)
        {
            long lJsonErrorIndex;

            // Only log something if we have something...
            if (!string.IsNullOrEmpty(a_szResponseText))
            {
                Log.Error(a_szResponseText);
            }

            // Handle protocol errors...
            if (a_apicmd.GetResponseStatus() != 200)
            {
                string szResponseData = a_apicmd.GetResponseData();
                if (string.IsNullOrEmpty(szResponseData))
                {
                    a_apicmd.DeviceResponseSetStatus(false, "protocolError", 0, "unrecognized protocol error, sorry.");
                }
                else
                {
                    JsonLookup jsonlookup = new JsonLookup();
                    jsonlookup.Load(szResponseData, out lJsonErrorIndex);
                    string szError = jsonlookup.Get("error");
                    if (string.IsNullOrEmpty(szError))
                    {
                        szError = "protocolError";
                    }
                    a_apicmd.DeviceResponseSetStatus(false, szError, 0, szResponseData);
                }

                // All done...
                return;
            }

            // Set the command's error return...
            a_apicmd.DeviceResponseSetStatus(a_blSuccess, a_szResponseCode, a_lResponseCharacterOffset, a_szResponseText);
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
                if (m_twainlocalsession != null)
                {
                    m_twainlocalsession.SetUserShutdown(true);
                    m_twainlocalsession.Dispose();
                    m_twainlocalsession = null;
                }
                if (m_twainlocalsessionInfo != null)
                {
                    m_twainlocalsessionInfo.Dispose();
                    m_twainlocalsessionInfo = null;
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

        /// <summary>
        /// Allow us to kick ourselves out of a wait...
        /// </summary>
        /// <returns></returns>
        public void ClientWaitForSessionUpdateForceSet()
        {
            if (m_twainlocalsession != null)
            {
                m_twainlocalsession.ClientWaitForSessionUpdateForceSet();
            }
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

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Protected Definitions
        ///////////////////////////////////////////////////////////////////////////////
        #region Protected Definitions

        /// <summary>
        /// Ways of getting to the server...
        /// </summary>
        protected enum HttpMethod
        {
            Undefined,
            Curl,
            WebRequest
        }

        /// <summary>
        /// TWAIN Local Scanner API session states
        /// noSession - we don't have a session with a client
        /// ready - we have a session, but we're not scanning or transfering images
        /// capturing - we're capturing and transfering images
        /// draining - we're transfering images (go to ready when done)
        /// closed - we're transfering images (go to noSession when done)
        /// </summary>
        protected enum SessionState
        {
            noSession,
            ready,
            capturing,
            draining,
            closed
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Protected Attributes
        // All of the members in this section must be specific to the device
        // and not to the session.  Session stuff goes into TwainLocalSession.
        ///////////////////////////////////////////////////////////////////////////////
        #region Protected Attributes

        /// <summary>
        /// Use this with the /privet/info and /privet/infoex commands...
        /// </summary>
        protected TwainLocalSession m_twainlocalsessionInfo;

        /// <summary>
        /// All of the data we need for /privet/twaindirect/session...
        /// </summary>
        protected TwainLocalSession m_twainlocalsession;

        /// <summary>
        /// A place where we can write stuff...
        /// </summary>
        protected string m_szWriteFolder;

        /// <summary>
        /// Our current platform...
        /// </summary>
        protected static Platform ms_platform = Platform.UNKNOWN;

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Class: File System Watcher
        // We use this to detect when TwainDirectOnTwain has added new images and
        // metadata to the image folder maintained by TwainDirectScanner.
        ///////////////////////////////////////////////////////////////////////////////
        #region Class: File System Watcher

        // We need to associate some information with our file system
        // watcher.  Specifically, the TwainLocalSscanner and the
        // pending ApiCmd used for events...
        protected class FileSystemWatcherHelper : FileSystemWatcher
        {
            public FileSystemWatcherHelper(TwainLocalScannerDevice a_twainlocalscannerdevice)
            {
                m_twainlocalsscanner = a_twainlocalscannerdevice;
            }

            public TwainLocalScannerDevice GetTwainLocalScanner()
            {
                return (m_twainlocalsscanner);
            }

            private TwainLocalScannerDevice m_twainlocalsscanner;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Class: Twain Local Session
        // Information about a session.  In theory we should be able to have more
        // than one of these, with one of them owning the scanner transport, and
        // the others finishing up transferring images.
        ///////////////////////////////////////////////////////////////////////////////
        #region Class: Twain Local Session

        /// <summary>
        /// TWAIN Local session information that we need to keep track of...
        /// </summary>
        protected class TwainLocalSession : IDisposable
        {
            ///////////////////////////////////////////////////////////////////////////
            // Public Methods
            ///////////////////////////////////////////////////////////////////////////
            #region Public Methods

            /// <summary>
            /// Init stuff...
            /// </summary>
            /// <param name="a_szXPrivetToken">the privet token for this session</param>
            public TwainLocalSession
            (
                string a_szXPrivetToken
            )
            {
                // Our state...
                m_sessionstate = SessionState.noSession;

                // The session object...
                m_szSessionId = null;
                m_szCallersHostName = null;
                m_lSessionRevision = 0;
                m_lWaitForEventsSessionRevision = 0;
                m_szSessionSnapshot = "";
                m_alSessionImageBlocks = null;
                SetSessionDoneCapturing(true);  // we start empty and ready to scoot
                SetSessionImageBlocksDrained(true); // we start empty and ready to scoot
                m_szXPrivetToken = a_szXPrivetToken;

                // We assume all is well until told otherwise...
                m_blSessionStatusSuccess = true;
                m_szSessionStatusDetected = "nominal";

                // Metadata...
                m_szMetadata = null;

                // Events, we're going with a fixed size, we
                // can grow this dynamically, if needed, but
                // we'd like to avoid that.  Data is always
                // contiguous, starting at 0, with nulls for
                // unused entries...
                m_aapicmdEvents = new ApiCmd[32];

                // The place we'll keep our device information...
                m_deviceregister = new DeviceRegister();

                // Notification when the session revision changes...
                m_autoreseteventWaitForSessionUpdate = new AutoResetEvent(false);

                // Our lock object...
                m_objectLockSession = new object();
            }

            /// <summary>
            /// Destructor...
            /// </summary>
            ~TwainLocalSession()
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
            /// Create a unique command id...
            /// </summary>
            /// <returns>the device id</returns>
            public string ClientCreateCommandId()
            {
                return (Guid.NewGuid().ToString());
            }

            /// <summary>
            /// Allow the caller to kick us out of a wait...
            /// </summary>
            public void ClientWaitForSessionUpdateForceSet()
            {
                if (m_autoreseteventWaitForSessionUpdate != null)
                {
                    m_autoreseteventWaitForSessionUpdate.Set();
                }
            }

            /// <summary>
            /// Wait for the session object to be updated, this is done
            /// by comparing the current session.revision number to the
            /// session.revision from the last command or event.
            /// </summary>
            /// <param name="a_lMilliseconds">milliseconds to wait for the update</param>
            /// <returns>true if an update was detected, false if the command timed out</returns>
            public bool ClientWaitForSessionUpdate(long a_lMilliseconds)
            {
                bool blSignaled = false;

                // Wait for it...
                if (!m_blCancelWaitForSessionUpdate)
                {
                    blSignaled = m_autoreseteventWaitForSessionUpdate.WaitOne((int)a_lMilliseconds);
                    if (!m_blCancelWaitForSessionUpdate)
                    {
                        m_autoreseteventWaitForSessionUpdate.Reset();
                    }
                }

                // All done...
                return (blSignaled);
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
            /// <param name="a_szScanner">the complete scanner record</param>
            public void DeviceRegisterSet
            (
                string a_szTwainLocalTy,
                string a_szTwainLocalSerialNumber,
                string a_szTwainLocalNote,
                string a_szScanner
            )
            {
                m_deviceregister.Set(a_szTwainLocalTy, a_szTwainLocalSerialNumber, a_szTwainLocalNote, a_szScanner);
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
            /// Get the scanner's manufacturer...
            /// </summary>
            /// <returns>manufacturer</returns>
            public string DeviceRegisterGetTwainLocalManufacturer()
            {
                return (m_deviceregister.GetTwainInquiryData().GetManufacturer());
            }

            /// <summary>
            /// Get the scanner's product name...
            /// </summary>
            /// <returns>product name</returns>
            public string DeviceRegisterGetTwainLocalProductName()
            {
                return (m_deviceregister.GetTwainInquiryData().GetProductName());
            }

            /// <summary>
            /// Get the contents of the register.txt file...
            /// </summary>
            /// <returns>everything we know about the scanner</returns>
            public string DeviceRegisterGetTwainLocalScanner()
            {
                return (m_deviceregister.GetTwainLocalScanner());
            }

            /// <summary>
            /// Get the scanner's serial number...
            /// </summary>
            /// <returns>serial number</returns>
            public string DeviceRegisterGetTwainLocalSerialNumber()
            {
                return (m_deviceregister.GetTwainInquiryData().GetSerialNumber());
            }

            /// <summary>
            /// Get the scanner's version info...
            /// </summary>
            /// <returns>version info</returns>
            public string DeviceRegisterGetTwainLocalVersion()
            {
                return (m_deviceregister.GetTwainInquiryData().GetVersion());
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
            public bool DeviceRegisterLoad(string a_szFile)
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
            public bool DeviceRegisterSave(string a_szFile)
            {
                return (m_deviceregister.Save(a_szFile));
            }

            /// <summary>
            /// End the session and do all cleanup...
            /// </summary>
            public void EndSession()
            {
                // Don't set anything, unless we see a change...
                if (m_sessionstate != SessionState.noSession)
                {
                    // Log it...
                    Log.Info("SetSessionState: " + m_sessionstate + " --> noSession");

                    // Set it...
                    m_sessionstate = SessionState.noSession;
                }

                // Cleanup...
                ResetSessionRevision();
                SetSessionId(null);
                SetCallersHostName(null);
                ResetSessionRevision();
                SetSessionSnapshot("");
            }

            /// <summary>
            /// Get the whole event array...
            /// </summary>
            /// <returns></returns>
            public ApiCmd[] GetApicmdEvents()
            {
                return (m_aapicmdEvents);
            }

            /// <summary>
            /// Get the caller's host name
            /// </summary>
            /// <returns>the caller's host name</returns>
            public string GetCallersHostName()
            {
                return (m_szCallersHostName);
            }

            /// <summary>
            /// Get our file system watcher for the bridge...
            /// </summary>
            /// <returns>the filesystemwatcher object</returns>
            public FileSystemWatcherHelper GetFileSystemWatcherHelperBridge()
            {
                return (m_filesystemwatcherhelperBridge);
            }

            /// <summary>
            /// Get our file system watcher for the TWAIN Direct transfers...
            /// </summary>
            /// <returns>the filesystemwatcher object</returns>
            public FileSystemWatcherHelper GetFileSystemWatcherHelperImageBlocks()
            {
                return (m_filesystemwatcherhelperImageBlocks);
            }

            /// <summary>
            /// Our communication channel to TWAIN Direct on TWAIN...
            /// </summary>
            /// <returns>the ipc object</returns>
            public Ipc GetIpcTwainDirectOnTwain()
            {
                return (m_ipcTwainDirectOnTwain);
            }

            /// <summary>
            /// Get the metadata...
            /// </summary>
            /// <returns>the metadata</returns>
            public string GetMetadata()
            {
                return (m_szMetadata);
            }

            /// <summary>
            /// Our TWAIN Direct on TWAIN process...
            /// </summary>
            /// <returns>the process object</returns>
            public Process GetProcessTwainDirectOnTwain()
            {
                return (m_processTwainDirectOnTwain);
            }

            /// <summary>
            /// Get the done capturing flag...
            /// </summary>
            /// <returns>true if the scanner is done capturing images</returns>
            public bool GetSessionDoneCapturing()
            {
                return (m_blSessionDoneCapturing);
            }

            /// <summary>
            /// Get the session id...
            /// </summary>
            /// <returns>the session id</returns>
            public string GetSessionId()
            {
                return (m_szSessionId);
            }

            /// <summary>
            /// Get the session image blocks drained flag...
            /// </summary>
            /// <returns>true if we're drained</returns>
            public bool GetSessionImageBlocksDrained()
            {
                return (m_blSessionImageBlocksDrained);
            }

            /// <summary>
            /// Get the session revision number...
            /// </summary>
            /// <returns>the session revision number</returns>
            public long GetSessionRevision()
            {
                return (m_lSessionRevision);
            }

            /// <summary>
            /// Get the last session object snapshot...
            /// </summary>
            /// <returns>the session object JSON string</returns>
            public string GetSessionSnapshot()
            {
                return (m_szSessionSnapshot);
            }

            /// <summary>
            /// Get the session state...
            /// </summary>
            /// <returns>the session state</returns>
            public SessionState GetSessionState()
            {
                return (m_sessionstate);
            }

            /// <summary>
            /// The status of the session (really the device)...
            /// </summary>
            /// <returns>false if we need user help</returns>
            public bool GetSessionStatusSuccess()
            {
                return (m_blSessionStatusSuccess);
            }

            /// <summary>
            /// The last detected boo-boo...
            /// </summary>
            /// <returns>the reason m_blSessionStatusSuccess is false</returns>
            public string GetSessionStatusDetected()
            {
                return (m_szSessionStatusDetected);
            }

            /// <summary>
            /// We need to track the session revision that the client sends
            /// to use with waitForEvents, so that we can expire older events
            /// with a minimum of fuss.  This helps with that.
            /// </summary>
            /// <returns>revision from the last waitForEventsCall</returns>
            public long GetWaitForEventsSessionRevision()
            {
                return (m_lWaitForEventsSessionRevision);
            }

            /// <summary>
            /// Get the privet token...
            /// </summary>
            /// <returns>the privet token</returns>
            public string GetXPrivetToken()
            {
                return (m_szXPrivetToken);
            }

            /// <summary>
            /// Reset the session revision...
            /// </summary>
            public void ResetSessionRevision()
            {
                m_lSessionRevision = 0;
                m_lWaitForEventsSessionRevision = 0;
            }

            /// <summary>
            /// Set a item in the event array...
            /// </summary>
            /// <returns></returns>
            public void SetApicmdEvent(long a_lIndex, ApiCmd a_apicmd)
            {
                if ((a_lIndex < 0) || (a_lIndex >= m_aapicmdEvents.Length))
                {
                    Log.Error("SetApicmdEvents: bad index..." + a_lIndex);
                    return;
                }
                m_aapicmdEvents[a_lIndex] = a_apicmd;
            }

            /// <summary>
            /// Set the caller's host name...
            /// </summary>
            /// <param name="a_szCallersHostName">callers host name</param>
            public void SetCallersHostName(string a_szCallersHostName)
            {
                m_szCallersHostName = a_szCallersHostName;
            }

            /// <summary>
            /// Set our file system watcher for the bridge...
            /// </summary>
            public void SetFileSystemWatcherHelperBridge(FileSystemWatcherHelper a_filesystemwatcherhelperBridge)
            {
                m_filesystemwatcherhelperBridge = a_filesystemwatcherhelperBridge;
            }

            /// <summary>
            /// Set our file system watcher for the TWAIN Direct transfers...
            /// </summary>
            public void SetFileSystemWatcherHelperImageBlocks(FileSystemWatcherHelper a_filesystemwatcherhelperImageBlocks)
            {
                m_filesystemwatcherhelperImageBlocks = a_filesystemwatcherhelperImageBlocks;
            }

            /// <summary>
            /// Our communication channel to TWAIN Direct on TWAIN...
            /// </summary>
            public void SetIpcTwainDirectOnTwain(Ipc a_ipcTwainDirectOnTwain)
            {
                m_ipcTwainDirectOnTwain = a_ipcTwainDirectOnTwain;
            }

            /// <summary>
            /// Set the metadata...
            /// </summary>
            /// <param name="a_szMetadata">the metadata</param>
            public void SetMetadata(string a_szMetadata)
            {
                m_szMetadata = a_szMetadata;
            }

            /// <summary>
            /// Set our TWAIN Direct on TWAIN process...
            /// </summary>
            public void SetProcessTwainDirectOnTwain(Process a_processTwainDirectOnTwain)
            {
                m_processTwainDirectOnTwain = a_processTwainDirectOnTwain;
            }

            /// <summary>
            /// Set the session done capturing flag...
            /// </summary>
            /// <param name="a_blSessionImageBlocksDrained">true if drained</param>
            public void SetSessionDoneCapturing(bool a_blSessionDoneCapturing)
            {
                m_blSessionDoneCapturing = a_blSessionDoneCapturing;
            }

            /// <summary>
            /// Set the session id...
            /// </summary>
            /// <param name="a_szSessionId">the new session id</param>
            public void SetSessionId(string a_szSessionId)
            {
                m_szSessionId = a_szSessionId;
            }

            /// <summary>
            /// Set the session image blocks drained flag...
            /// </summary>
            /// <param name="a_blSessionImageBlocksDrained">true if drained</param>
            public void SetSessionImageBlocksDrained(bool a_blSessionImageBlocksDrained)
            {
                m_blSessionImageBlocksDrained = a_blSessionImageBlocksDrained;
            }

            /// <summary>
            /// Set the revision...
            /// </summary>
            /// <param name="a_lSessionRevision">new revision</param>
            /// <returns>true if the value is greater than what we had</returns>
            public bool SetSessionRevision(long a_lSessionRevision, bool a_blSetEvent = false)
            {
                // If the session sent to us is less than or equal to
                // what we already have, discard it and let the caller
                // know that we didn't take it.
                if (a_lSessionRevision <= m_lSessionRevision)
                {
                    return (false);
                }

                // Otherwise it's all sunshine and rainbows...
                m_lSessionRevision = a_lSessionRevision;

                // Set the event, if asked to...
                if (a_blSetEvent)
                {
                    m_autoreseteventWaitForSessionUpdate.Set();
                }

                // All done...
                return (true);
            }

            /// <summary>
            /// Sets the session snapshot...
            /// </summary>
            /// <param name="a_szSessionSnapshot">the new session snapshot</param>
            public void SetSessionSnapshot(string a_szSessionSnapshot)
            {
                m_szSessionSnapshot = a_szSessionSnapshot;
            }

            /// <summary>
            /// Set the session state...
            /// </summary>
            /// <param name="a_sessionstate"></param>
            public void SetSessionState(SessionState a_sessionstate)
            {
                // Don't set anything, unless we see a change...
                if (m_sessionstate != a_sessionstate)
                {
                    // Log it...
                    Log.Info("SetSessionState: " + m_sessionstate + " --> " + a_sessionstate);

                    // Set it...
                    m_sessionstate = a_sessionstate;
                }
            }

            /// <summary>
            /// Set the status of the session (really the device)...
            /// </summary>
            /// <param name="a_blSessionStatusSuccess">false if the scanner needs attention</param>
            public void SetSessionStatusSuccess(bool a_blSessionStatusSuccess)
            {
                lock (m_objectLockSession)
                {
                    m_blSessionStatusSuccess = a_blSessionStatusSuccess;
                }
            }

            /// <summary>
            /// Set the last detected boo-boo...
            /// </summary>
            /// <param name="a_szSessionStatusDetected">the reason the scanner needs attention</param>
            public void SetSessionStatusDetected(string a_szSessionStatusDetected)
            {
                lock (m_objectLockSession)
                {
                    m_szSessionStatusDetected = a_szSessionStatusDetected;
                }
            }

            /// <summary>
            /// Set to true if the user closed us.  It should be false if
            /// we're shutting down because the user is logging out, or if
            /// we're cleaning up from a problem.  This was added to take
            /// care a nasty exception in Process.Dispose() when trying to
            /// take down TwainDirect.OnTwain...
            /// </summary>
            /// <param name="a_blUserShutdown"></param>
            public void SetUserShutdown(bool a_blUserShutdown)
            {
                m_blUserShutdown = a_blUserShutdown;
            }

            /// <summary>
            /// We need to keep track of the session revision sent by the last
            /// waitForEvents, so that we can expire events old than that number.
            /// This allows us to keep our event list cleaner, without a lot of
            /// extra code.  We don't need to be in perfect sync, we just need
            /// to be in the ballpark.
            /// </summary>
            /// <param name="a_szWaitForEventsSessionRevision">sessionRevision in the waitForEvents command</param>
            public void SetWaitForEventsSessionRevision(string a_szWaitForEventsSessionRevision)
            {
                long lWaitForEventsSessionRevision;
                if (long.TryParse(a_szWaitForEventsSessionRevision, out lWaitForEventsSessionRevision))
                {
                    // No going backwards!
                    if (lWaitForEventsSessionRevision > m_lWaitForEventsSessionRevision)
                    {
                        m_lWaitForEventsSessionRevision = lWaitForEventsSessionRevision;
                    }
                }
            }

            #endregion


            ///////////////////////////////////////////////////////////////////////////
            // Internal Methods
            ///////////////////////////////////////////////////////////////////////////
            #region Internal Methods

            /// <summary>
            /// Cleanup...
            /// </summary>
            /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
            internal void Dispose(bool a_blDisposing)
            {
                // Free managed resources...
                if (a_blDisposing)
                {
                    // Wake up anybody checking this event...
                    if (m_autoreseteventWaitForSessionUpdate != null)
                    {
                        m_blCancelWaitForSessionUpdate = true;
                        m_autoreseteventWaitForSessionUpdate.Set();
                        Thread.Sleep(0);
                        m_autoreseteventWaitForSessionUpdate.Dispose();
                        m_filesystemwatcherhelperBridge = null;
                        m_filesystemwatcherhelperImageBlocks = null;
                    }
                    if (m_filesystemwatcherhelperBridge != null)
                    {
                        m_filesystemwatcherhelperBridge.EnableRaisingEvents = false;
                        m_filesystemwatcherhelperBridge.Dispose();
                        m_filesystemwatcherhelperBridge = null;
                    }
                    if (m_filesystemwatcherhelperImageBlocks != null)
                    {
                        m_filesystemwatcherhelperImageBlocks.EnableRaisingEvents = false;
                        m_filesystemwatcherhelperImageBlocks.Dispose();
                        m_filesystemwatcherhelperImageBlocks = null;
                    }
                    if (m_processTwainDirectOnTwain != null)
                    {
                        try
                        {
                            m_processTwainDirectOnTwain.Kill();
                        }
                        catch
                        {
                            // Not really interested in what we catch.
                            // Unless it's a goretrout... :)
                        }
                        try
                        {
                            // We're getting a crash with an unknown
                            // exception if logging off with an open
                            // session.  Presumably .NET is poo'ing
                            // itself because TwainDirect.OnTwain is
                            // exiting too.  The try/catch doesn't
                            // help (but I'm leaving it in).  Plan B
                            // is to punt...so I'm punting...
                            //
                            // A value of true means the user initiated
                            // this, so we're safe.
                            if (m_blUserShutdown)
                            {
                                m_processTwainDirectOnTwain.Dispose();
                            }
                        }
                        catch
                        {
                            // Not caring so much here, either.  We seem
                            // to hit it if logging out, which suggests
                            // that there's a bit of a struggle to see
                            // who gets to kill TwainDirect.OnTwain first...
                        }
                        m_processTwainDirectOnTwain = null;
                    }
                    if (m_ipcTwainDirectOnTwain != null)
                    {
                        m_ipcTwainDirectOnTwain.Dispose();
                        m_ipcTwainDirectOnTwain = null;
                    }
                    if (m_aapicmdEvents != null)
                    {
                        m_aapicmdEvents = null;
                    }
                }
            }

            #endregion


            ///////////////////////////////////////////////////////////////////////////
            // Private Attributes
            ///////////////////////////////////////////////////////////////////////////
            #region Private Attributes

            /// <summary>
            /// The object we use to lock access to the session object...
            /// </summary>
            private object m_objectLockSession;

            /// <summary>
            /// JSON IN:  params.sessionId
            /// JSON OUT: results.session.sessionId
            /// This is the unique "secret" id that the scanner provides in response
            /// to a CreateSession command.  The scanner uses it to make sure that
            /// commands that it receives belong to this session...
            /// </summary>
            private string m_szSessionId;

            /// <summary>
            /// JSON OUT:  results.session.revision
            /// Report when a change has been made to the session object.
            /// The most obvious use is when the state changes, or the
            /// imageBlocks data is being updated...
            /// </summary>
            private long m_lSessionRevision;

            /// <summary>
            /// False if the scanner needs some kind of user intervention...
            /// </summary>
            private bool m_blSessionStatusSuccess;

            /// <summary>
            /// If m_blSessionStatusSuccess is false, this value explains
            /// what happened.  In theory one can have a detected value
            /// without m_blSessionStatusSuccess being false, but we have
            /// no plans to do anything like that in the TWAIN Bridge...
            /// </summary>
            private string m_szSessionStatusDetected;

            /// <summary>
            /// True if we're shutting down because of some action from
            /// the user, like stopping or closing the bridge...
            /// </summary>
            private bool m_blUserShutdown;

            /// <summary>
            /// Triggered when the session object had been updated...
            /// </summary>
            private AutoResetEvent m_autoreseteventWaitForSessionUpdate;
            private bool m_blCancelWaitForSessionUpdate;

            /// <summary>
            /// This holds a JSON subset of the session object, so that we can
            /// detect changes and then update the revision number...
            /// </summary>
            private string m_szSessionSnapshot;

            /// <summary>
            /// JSON OUT:  results.session.imageBlocks
            /// Reports the index values of image blocks that are ready for transfer
            /// to the client...
            /// </summary>
            public long[] m_alSessionImageBlocks;

            /// <summary>
            /// The scanner is no longer capturing new stuff...
            /// </summary>
            private bool m_blSessionDoneCapturing;

            /// <summary>
            /// true if imageBlocksDrained has been set to true...
            /// </summary>
            private bool m_blSessionImageBlocksDrained;

            /// <summary>
            /// JSON OUT:  results.metadata
            /// Metadata...
            /// </summary>
            private string m_szMetadata;

            /// <summary>
            /// The hostname of our caller, captured during createSession,
            /// and used to check all other session calls...
            /// </summary>
            private string m_szCallersHostName;

            /// <summary>
            /// Persistant device information...
            /// </summary>
            private DeviceRegister m_deviceregister;

            /// <summary>
            /// Our current state...
            /// </summary>
            private SessionState m_sessionstate;

            /// <summary>
            /// The interprocess communication object we
            /// use to talk to the TwainDirect.OnTwain process...
            /// </summary>
            private Ipc m_ipcTwainDirectOnTwain;

            /// <summary>
            /// The session revision included with the last
            /// waitForEvents call...
            /// </summary>
            private long m_lWaitForEventsSessionRevision;

            /// <summary>
            /// The TwainDirect.OnTwain process...
            /// </summary>
            private Process m_processTwainDirectOnTwain;

            /// <summary>
            /// Privet requires this in the header for every
            /// command, except /privet/info and /privet/info (which
            /// return the value used by all other commands).  Google
            /// recommends that it be refreshed every 24 hours,
            /// but this can get weird with long lasting sessions,
            /// so instead we're going to refresh it if it's been
            /// more than two minutes since the last call to
            /// /privet/info or /privet/infoex.  Clients must call
            /// createSession immediately after info.  The token is
            /// stored in TwainLocalSession, so it will be valid for
            /// that session as long as it lasts...
            /// </summary>
            private string m_szXPrivetToken;

            /// <summary>
            /// The thread we use to monitor for changes to the contents
            /// of the imageBlocks folder.  This is for data that we're
            /// sending to a TWAIN Direct application...
            /// </summary>
            private FileSystemWatcherHelper m_filesystemwatcherhelperImageBlocks;

            /// <summary>
            /// The thread we use to monitor for changes to the contents
            /// of the imageBlocks folder coming in from the Bridge.  We
            /// have the option to modify this data, before sending it
            /// up to the TWAIN Direct application.  The result of this
            /// process will create the *.meta file that the other file
            /// system watcher is monitoring...
            /// </summary>
            private FileSystemWatcherHelper m_filesystemwatcherhelperBridge;

            /// <summary>
            /// Our list of events, maintained in revision order, from
            /// low to high numbers.
            /// </summary>
            private ApiCmd[] m_aapicmdEvents;

            #endregion
        }

        #endregion
    }
}
