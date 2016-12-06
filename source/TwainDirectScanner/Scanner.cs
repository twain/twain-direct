///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirectScanner.Scanner
//
// The actual interface to the scanner is in here, so that we can decouple it from
// the presentation layer.  This allows us to run in window mode, console mode or
// as a service.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    05-Dec-2014     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2014-2016 Kodak Alaris Inc.
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
using System.Threading;
using TwainDirectSupport;

namespace TwainDirectScanner
{
    public sealed class Scanner : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Initialize interesting stuff...
        /// </summary>
        /// <param name="a_blNeedBrowser">we need a browser for authentication</param>
        /// <param name="a_displaycallback">display callback function or null</param>
        /// <param name="a_stopnotification">notification that we've stopped monitoring</param>
        /// <param name="m_confirmscan">confirmation function or null</param>
        /// <param name="a_blNoDevices">true if we have no devices</param>
        public Scanner
        (
            bool a_blNeedBrowser,
            DisplayCallback a_displaycallback,
            StopNotification a_stopnotification,
            TwainLocalScanner.ConfirmScan a_confirmscan,
            float a_fConfirmScanScale,
            out bool a_blNoDevices
        )
        {
            bool blUseSane;
            string szExecutablePath = null;
            string szReadFolder = null;
            string szWriteFolder = null;

            // Init stuff...
            m_blUseXmpp = true;
            a_blNoDevices = true;
            m_displaycallback = a_displaycallback;
            m_stopnotification = a_stopnotification;
            m_confirmscan = a_confirmscan;
            m_fConfirmScanScale = a_fConfirmScanScale;
            m_iDiagnostics = 1;

            // Get the config and command line argument values...
            szExecutablePath = Config.Get("executablePath", "");
            szReadFolder = Config.Get("readFolder", "");
            szWriteFolder = Config.Get("writeFolder", "");
            blUseSane = (Config.Get("usesane", null) != null);

            // Init stuff...
            m_blUseXmpp = false;

            // Sanity check...
            if (!FindTwainDirectOnTwain(szExecutablePath, blUseSane))
            {
                if (blUseSane)
                {
                    Log.Error("Unable to locate TwainDirectOnSane.exe");
                    throw new Exception("Unable to locate TwainDirectOnSane.exe");
                }
                else
                {
                    Log.Error("Unable to locate TwainDirectOnTwain.exe");
                    throw new Exception("Unable to locate TwainDirectOnTwain.exe");
                }
            }

            // Get our TWAIN Local interface...
            m_twainlocalscanner = new TwainLocalScanner
            (
                m_blUseXmpp?MonitorTasks:(TimerCallback)null,
                a_confirmscan,
                a_fConfirmScanScale,
                m_blUseXmpp?this:(object)null
            );
            if (m_twainlocalscanner == null)
            {
                Log.Error("Failed to create TwainLocalScanner");
                throw new Exception("Failed to create TwainLocalScanner");
            }

            // Do we have any devices?
            a_blNoDevices = RefreshDeviceList();
        }

        /// <summary>
        /// Destructor...
        /// </summary>
        ~Scanner()
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
        /// Return an array of available scanners...
        /// </summary>
        /// <returns>an array of strings or null</returns>
        public string GetAvailableScanners()
        {
            string szList;
            string szListFile = m_twainlocalscanner.GetPath("twainlist.txt");

            // Get the list of available drivers...
            string aszDrivers = Run(m_szTwainDirectOn, "\"twainlist=" + szListFile + "\"");
            if (aszDrivers == null)
            {
                Log.Error("Unable to get drivers...");
                return (null);
            }

            // Read the list of scanners...
            try
            {
                szList = File.ReadAllText(szListFile);
            }
            catch
            {
                Log.Info("No drivers found...");
                return (null);
            }

            // All done...
            return (szList);
        }

        /// <summary>
        /// Start polling for tasks...
        /// </summary>
        /// <returns>true on success</returns>
        public bool MonitorTasksStart()
        {
            bool blSuccess;

            // Start monitoring for commands...
            blSuccess = m_twainlocalscanner.DeviceHttpServerStart();
            if (!blSuccess)
            {
                Log.Error("DeviceHttpServerStart failed...");
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Stop polling for tasks...
        /// </summary>
        public void MonitorTasksStop()
        {
            m_twainlocalscanner.DeviceHttpServerStop();
        }

        /// <summary>
        /// Get the TWAIN Local ty= field
        /// </summary>
        /// <returns>the vendors friendly name</returns>
        public string GetTwainLocalTy()
        {
            return (m_twainlocalscanner.GetTwainLocalTy());
        }

        /// <summary>
        /// Get the TWAIN Local note= field...
        /// </summary>
        /// <returns>the users preferred name</returns>
        public string GetTwainLocalNote()
        {
            return (m_twainlocalscanner.GetTwainLocalNote());
        }

        /// <summary>
        /// Register a scanner...
        /// </summary>
        /// <param name="a_jsonlookup">the TWAIN driver data</param>
        /// <param name="a_iScanner">the index of the driver we're interested in</param>
        /// <param name="a_szNote">a note from the user</param>
        /// <param name="a_apicmd">info about the command</param>
        /// <returns>true on success</returns>
        public bool RegisterScanner(JsonLookup a_jsonlookup, int a_iScanner, string a_szNote, ref ApiCmd a_apicmd)
        {
            // Register it...
            if (!m_twainlocalscanner.DeviceRegister(a_jsonlookup, a_iScanner, a_szNote, ref a_apicmd))
            {
                Log.Error("DeviceRegister failed...");
                return (false);
            }

            // Save the file...
            m_twainlocalscanner.DeviceRegisterSave();

            // All done...
            return (true);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions...

        /// <summary>
        /// Display callback...
        /// </summary>
        /// <param name="a_szText">text to display</param>
        public delegate void DisplayCallback(string a_szText);

        /// <summary>
        /// Notification that we've stopped monitoring...
        /// </summary>
        /// <param name="a_blNoDevices">true if we have no devices</param>
        public delegate void StopNotification(bool a_blNoDevices);

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
                if (m_twainlocalscanner != null)
                {
                    m_twainlocalscanner.Dispose();
                    m_twainlocalscanner = null;
                }
            }
        }

        /// <summary>
        /// Display something using the caller's function...
        /// </summary>
        /// <param name="a_szText">the text to display</param>
        internal void Display(string a_szText)
        {
            if (m_displaycallback != null)
            {
                m_displaycallback(a_szText);
            }
        }

        /// <summary>
        /// Monitor for work...
        /// </summary>
        /// <param name="sender"></param>
        internal void MonitorTasks(object sender)
        {
            bool blSuccess;
            string szJson;
            string szDeviceName;
            long lResponseCharacterOffset;
            XmppCallback xmppcallback = (XmppCallback)sender;
            JsonLookup jsonlookup = new JsonLookup();
            TwainLocalScanner.Command command;

            // Load the data...
            if (jsonlookup.Load(xmppcallback.m_szData, out lResponseCharacterOffset))
            {
                // Check out the event type...
                string szType = jsonlookup.Get("type");
                if (szType == null)
                {
                    Log.Error("XMPP event received has no type in it...");
                    Log.Error(xmppcallback.m_szData);
                    return;
                }

                // Our event types...
                switch (szType)
                {
                    //
                    // Nope, gots no clue...
                    //
                    default:
                        Display("");
                        Display("Unrecognized command: " + szType);
                        Log.Error("XMPP event has unrecognized type...");
                        Log.Error(xmppcallback.m_szData);
                        return;

                    //
                    // Our command has been canceled, we have to acknowledge that
                    // and either kill it or let it finish.  We opt to kill.  In
                    // the worst case scenerio we'll try to update a canceled
                    // command some place else in the code.  We need to be able
                    // to handle that pressure anyways...
                    //
                    case "COMMAND_CANCELED":
                    case "COMMAND_CANCELLED":
                        Display("");
                        Display("Command cancelled...");
                        szJson = jsonlookup.Get("method");
                        if (string.IsNullOrEmpty(szJson))
                        {
                            blSuccess = jsonlookup.Load(szJson, out lResponseCharacterOffset);
                            if (blSuccess)
                            {
                                ApiCmd apicmd = new ApiCmd(null);
                                //apicmd.SetState("canceled", null);
                            }
                        }
                        return;

                    //
                    // Our command has expired, we don't need to take any additional
                    // action, because to get this far we must never have seen or
                    // acknowledged the command...
                    //
                    case "COMMAND_EXPIRED":
                        Display("");
                        Display("Command expired...");
                        Log.Error("XMPP event COMMAND_EXPIRED...");
                        Log.Error(xmppcallback.m_szData);
                        return;

                    //
                    // Not supported yet...
                    //
                    case "DEVICE_ACL_UPDATED":
                        Display("");
                        Display("ACL updated...");
                        Log.Error("XMPP event DEVICE_ACL_UPDATED not supported yet...");
                        Log.Error(xmppcallback.m_szData);
                        return;

                    //
                    // Somebody doesn't like us anymore...
                    //
                    case "DEVICE_DELETED":
                        // Well, we'd better stop...
                        MonitorTasksStop();
                        Display("");
                        Display("Stop (a device has been deleted)...");

                        // Refresh our device list...
                        Thread.Sleep(1000);
                        bool blNoDevices = RefreshDeviceList();

                        // Notify the caller...
                        if (m_stopnotification != null)
                        {
                            m_stopnotification(blNoDevices);
                        }
                        return;

                    //
                    // We have something new to work on, drop down so that we're
                    // not doing all this work in the switch statement...
                    //
                    case "COMMAND_CREATED":
                        break;
                }

                // The command was included in the notification, so let's start
                // doing work with it.  We'll begin by collecting some info...
                string szState = jsonlookup.Get("command.state");
                string szDeviceId = jsonlookup.Get("deviceId");

                // Find the match in our list...
                szDeviceName = m_twainlocalscanner.GetTwainLocalTy();

                // Did we find it?
                if (!string.IsNullOrEmpty(szDeviceName))
                {
                    // Build the command...
                    command = new TwainLocalScanner.Command();
                    command.szDeviceName = szDeviceName;
                    command.szJson = jsonlookup.Get("method");

                    // Display it...
                    switch (m_iDiagnostics)
                    {
                        default:
                            break;
                        case 1:
                            if (command.szJson.Contains("readImageBlock") && command.szJson.Contains("Metadata"))
                            {
                                Display("Sending an image...");
                            }
                            else if (command.szJson.Contains("startCapturing"))
                            {
                                Display(" ");
                                Display("Scanning started...");
                            }
                            else if (command.szJson.Contains("stopCapturing"))
                            {
                                Display("Scanning stopped...");
                            }
                            else if (command.szJson.Contains("createSession"))
                            {
                                Display(" ");
                                Display("*** Scanner locked ***");
                            }
                            else if (command.szJson.Contains("closeSession"))
                            {
                                Display(" ");
                                Display("*** Scanner unlocked ***");
                            }
                            break;
                        case 2:
                            Display(" ");
                            Display("XMPP");
                            Display(command.szDeviceName + ": " + command.szJson);
                            break;
                    }

                    // Dispatch it...
                    //m_twainlocalscanner.DeviceDispatchCommand(command, ref httplistenercontext);
                }
            }
        }

        /// <summary>
        /// Refresh our list of devices...
        /// </summary>
        /// <returns>true if we have no devices</returns>
        internal bool RefreshDeviceList()
        {
            bool blNoDevices;
            bool blSuccess;

            // Load our device register, if we have one...
            blSuccess = m_twainlocalscanner.DeviceRegisterLoad();
            if (!blSuccess)
            {
                m_displaycallback("No scanners registered...");
                Log.Error("DeviceRegisterLoad failed...");
                return (true);
            }

            // Do we have any devices?
            blNoDevices = string.IsNullOrEmpty(m_twainlocalscanner.GetTwainLocalTy());

            // If yes, display the names...
            if (m_displaycallback != null)
            {
                m_displaycallback("Listing registered scanners...(please wait for the list)");
                m_displaycallback(m_twainlocalscanner.GetTwainLocalTy());
                m_displaycallback("Listing complete...");
            }

            // All done...
            return (blNoDevices);
        }

        // Run a program...get the stdout as a string, log the command and the result...
        internal string Run(string szProgram, string a_szArguments)
        {
            string szOutput = "";

            // Log what we're doing...
            Log.Info("run>>> " + szProgram);
            Log.Info("run>>> " + a_szArguments);

            // Start the child process.
            Process p = new Process();

            // Guard it...
            try
            {
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
                szOutput = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
            }
            catch (Exception exception)
            {
                Log.Error("Something bad happened..." + exception.Message);
            }
            finally
            {
                p.Dispose();
                p = null;
            }

            // Log any output...
            Log.Info("run>>> " + szOutput);

            // All done...
            return (szOutput);
        }

        /// <summary>
        /// Find TWAINDirect-on-TWAIN, we're assuming something about the
        /// construction of the kit to make this work.  Needless to
        /// say, this could be done better.
        /// 
        /// We're assuming something like this:
        /// C:\Users\user\Desktop\SWORD on TWAIN Kit\Device Proxy\bin\Debug\thing.exe
        /// 
        /// We want to get to this:
        ///  C:\Users\user\Desktop\SWORD on TWAIN Kit\...\TwainDirectOnTwain.exe
        /// 
        /// We'd also like to honor the debug/release, if possible.
        /// </summary>
        /// <returns>true if we find it</returns>
        internal bool FindTwainDirectOnTwain(string a_szExecutablePath, bool a_blUseSane)
        {
            // Just in case...
            try
            {
                // Find SWORD-on-TWAIN...
                string szTwainDirectOn;
                string szDataFolder = a_szExecutablePath;
                string szName = Path.GetFileNameWithoutExtension(szDataFolder);
                string szConfiguration;
                if (a_blUseSane)
                {
                    szTwainDirectOn = "TwainDirectOnSane";
                }
                else
                {
                    szTwainDirectOn = "TwainDirectOnTwain";
                }
                if (szDataFolder.Contains("Debug"))
                {
                    szConfiguration = "Debug";
                }
                else
                {
                    szConfiguration = "Release";
                }
                string szPlatform;
                if (szDataFolder.Contains("AnyCPU"))
                {
                    szPlatform = "AnyCPU";
                }
                else if (IntPtr.Size > 4) // szDataFolder.Contains("x64"))
                {
                    if (TwainLocalScanner.GetPlatform() == TwainLocalScanner.Platform.WINDOWS)
                    {
                        szPlatform = "x64";
                    }
                    else
                    {
                        szPlatform = "x86";
                    }
                }
                else
                {
                    szPlatform = "x86";
                }
                m_szTwainDirectOn = szDataFolder.Split(new string[] { szName }, StringSplitOptions.None)[0];
                m_szTwainDirectOn = Path.Combine(m_szTwainDirectOn, szTwainDirectOn);
                m_szTwainDirectOn = Path.Combine(m_szTwainDirectOn, "bin");
                m_szTwainDirectOn = Path.Combine(m_szTwainDirectOn, szPlatform);
                m_szTwainDirectOn = Path.Combine(m_szTwainDirectOn, szConfiguration);
                m_szTwainDirectOn = Path.Combine(m_szTwainDirectOn, szTwainDirectOn + ".exe");
                Log.Info("Using: " + m_szTwainDirectOn);

                // Validate...
                if (!File.Exists(m_szTwainDirectOn))
                {
                    Log.Error("Failed to find: " + m_szTwainDirectOn);
                    return (false);
                }

                // We're good...
                return (true);
            }
            catch
            {
                // Nothing to do, just bail...
            }

            // Uh-oh...
            m_szTwainDirectOn = null;
            return (false);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// Our scanner interface...
        /// </summary>
        private TwainLocalScanner m_twainlocalscanner;

        /// <summary>
        /// Use XMPP notifications...
        /// </summary>
        private bool m_blUseXmpp;

        /// <summary>
        /// Full path to TWAIN-Direct-on-TWAIN or TWAIN-Direct-
        /// on-SANE...
        /// </summary>
        private string m_szTwainDirectOn;

        /// <summary>
        /// Optional display callback...
        /// </summary>
        private DisplayCallback m_displaycallback;

        /// <summary>
        /// Notification that we've stopped monitoring...
        /// </summary>
        private StopNotification m_stopnotification;

        /// <summary>
        /// If not null, then use this to prompt the user to
        /// confirm a scan request...
        /// </summary>
        private TwainLocalScanner.ConfirmScan m_confirmscan;

        /// <summary>
        /// Beause sometimes forms are too darn small...
        /// </summary>
        private float m_fConfirmScanScale;

        /// <summary>
        /// If non-zero then we'll dump out some extra info...
        /// </summary>
        private int m_iDiagnostics;

        #endregion
    }
}
