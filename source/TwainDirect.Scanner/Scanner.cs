///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.Scanner.Scanner
//
// The actual interface to the scanner is in here, so that we can decouple it from
// the presentation layer.  This allows us to run in window mode, console mode or
// as a service.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    05-Dec-2014     Initial Release
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
using System.Threading;
using TwainDirect.Support;

namespace TwainDirect.Scanner
{
    internal sealed class Scanner : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Initialize interesting stuff...
        /// </summary>
        /// <param name="a_displaycallback">display callback function or null</param>
        /// <param name="a_stopnotification">notification that we've stopped monitoring</param>
        /// <param name="m_confirmscan">confirmation function or null</param>
        /// <param name="a_blNoDevices">true if we have no devices</param>
        public Scanner
        (
            TwainLocalScanner.DisplayCallback a_displaycallback,
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
            a_blNoDevices = true;
            m_displaycallback = a_displaycallback;
            m_stopnotification = a_stopnotification;
            m_confirmscan = a_confirmscan;
            m_fConfirmScanScale = a_fConfirmScanScale;

            // Get the config and command line argument values...
            szExecutablePath = Config.Get("executablePath", "");
            szReadFolder = Config.Get("readFolder", "");
            szWriteFolder = Config.Get("writeFolder", "");
            blUseSane = (Config.Get("usesane", null) != null);

            // Sanity check...
            if (!FindTwainDirectOnTwain(szExecutablePath, blUseSane))
            {
                if (blUseSane)
                {
                    Log.Error("Unable to locate TwainDirect.OnSane.exe");
                    throw new Exception("Unable to locate TwainDirect.OnSane.exe");
                }
                else
                {
                    Log.Error("Unable to locate TwainDirect.OnTwain.exe");
                    throw new Exception("Unable to locate TwainDirect.OnTwain.exe");
                }
            }

            // Get our TWAIN Local interface...
            m_twainlocalscanner = new TwainLocalScanner(a_confirmscan,a_fConfirmScanScale, null, null, Display, true);
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

        // Get information about our session...
        public void GetInfo()
        {
            // Ain't got one...
            if (m_twainlocalscanner == null)
            {
                Console.Out.WriteLine("no TWAIN Local session...");
                return;
            }

            // Info...
            Console.Out.WriteLine("State: " + m_twainlocalscanner.GetState());
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
        ///  C:\Users\user\Desktop\SWORD on TWAIN Kit\...\TwainDirect.OnTwain.exe
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
                    szTwainDirectOn = "TwainDirect.OnSane";
                }
                else
                {
                    szTwainDirectOn = "TwainDirect.OnTwain";
                }

                // Try to load TwainDirect.OnTwain from the same execution folder first
                var localOnTwainApplication = Path.Combine(Path.GetDirectoryName(szDataFolder), szTwainDirectOn + ".exe");
                if (File.Exists(localOnTwainApplication))
                {
                    m_szTwainDirectOn = localOnTwainApplication;
                    return true;
                }

                // Otherwise, assume we are in DEV environment
                // TODO: simplify this, overcomplicated

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
        /// Full path to TWAIN-Direct-on-TWAIN or TWAIN-Direct-
        /// on-SANE...
        /// </summary>
        private string m_szTwainDirectOn;

        /// <summary>
        /// Optional display callback...
        /// </summary>
        private TwainLocalScanner.DisplayCallback m_displaycallback;

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

        #endregion
    }
}
