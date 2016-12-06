///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirectOnTwain.Program
//
//  Use a SWORD task to control a TWAIN driver.  This is a general solution that
//  represents standard SWORD on standard TWAIN.  Other schemes are needed to get
//  at the custom features that may be available for a device.
//
//  In here we're either processing a file passed in as a command line argument,
//  or we're going to an interactive mode.
//
//  One downside to this scheme is that there's no obviously graceful way of
//  cancelling a scan session.  There are some less than graceful ways, though,
//  which we'll probably add eventually.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    16-Jun-2014     Initial Release
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
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using TwainDirectSupport;
using TWAINWorkingGroup;
using TWAINWorkingGroupToolkit;
//[assembly: CLSCompliant(true)]

namespace TwainDirectOnTwain
{
    /// <summary>
    /// Our main program.  We're keeping it simple here...
    /// </summary>
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] a_aszArgs)
        {
            // Override the logging system...
            TWAINWorkingGroup.Log.Override
            (
                TwainDirectSupport.Log.Close,
                TwainDirectSupport.Log.GetLevel,
                TwainDirectSupport.Log.Open,
                null,
                TwainDirectSupport.Log.SetFlush,
                TwainDirectSupport.Log.SetLevel,
                TwainDirectSupport.Log.WriteEntry,
                out ms_getstatedelegate
            );
            TwainDirectSupport.Log.SetStateDelegate(GetState);

            // Load our configuration information and our arguments,
            // so that we can access them from anywhere in the code...
            if (!Config.Load(Application.ExecutablePath, a_aszArgs, "appdata.txt"))
            {
                MessageBox.Show("Error starting, is appdata.txt damaged?");
                Application.Exit();
            }

            // Sleep so we can attach and debug stuff...
            long lDelay = Config.Get("delayTwainDirectOnTwain", 0);
            if (lDelay > 0)
            {
                Thread.Sleep((int)lDelay);
            }

            // Check the arguments...
            string szWriteFolder = Config.Get("writeFolder", null);
            string szExecutableName = Config.Get("executableName", null);

            // Turn on logging...
            TWAINWorkingGroup.Log.Open(szExecutableName, szWriteFolder, 1);
            TWAINWorkingGroup.Log.SetLevel((int)Config.Get("logLevel", 0));
            TWAINWorkingGroup.Log.Info(szExecutableName + " Log Started...");

            // Let the manager figure out which mode we're in: batch or
            // interactive.  If batch it'll return true and it'll silently
            // try to run the task given to it.  If interactive we'll drop
            // down and raise the dialog...
            if (SelectMode())
            {
                TWAINWorkingGroup.Log.Info("Exiting...");
                TWAINWorkingGroup.Log.Close();
                Environment.Exit(0);
                return;
            }

            // Hellooooooo, user...
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());

            // Bye-bye...
            TWAINWorkingGroup.Log.Close();
        }

        /// <summary>
        /// TWAIN needs help, if we want it to run stuff in our main
        /// UI thread...
        /// </summary>
        /// <param name="control">the control to run in</param>
        /// <param name="code">the code to run</param>
        static public void RunInUiThread(Object a_object, Action a_action)
        {
            Control control = (Control)a_object;
            if (control.InvokeRequired)
            {
                control.Invoke(new TWAINCSToolkit.RunInUiThreadDelegate(RunInUiThread), new object[] { a_object, a_action });
                return;
            }
            a_action();
        }

        /// <summary>
        /// Select the mode for this session, batch or interactive,
        /// based on the arguments.  Don't be weirded out by this
        /// function calling TWAIN Local On Twain and then that function using
        /// Sword.  This is a static function, it's just a convenience
        /// to use the Sword object to hold it, it could go anywhere
        /// and later on probably will (like as a function inside of
        /// the Program module).
        /// </summary>
        /// <returns>true for batch mode, false for interactive mode</returns>
        public static bool SelectMode()
        {
            int iPid = 0;
            long lResponseCharacterOffset;
            string szIpc;
            string szTaskFile;
            bool blTestPdfRaster;
            bool blTestTwainLocalOnTwain;
            bool blTestJson;
            bool blTestDnssd;

            // Check the arguments...
            string szWriteFolder = Config.Get("writeFolder", null);
            string szExecutableName = Config.Get("executableName", null);
            szTaskFile = Config.Get("task", null);
            blTestPdfRaster = (Config.Get("testpdfraster", null) != null);
            blTestTwainLocalOnTwain = (Config.Get("testtwainlocalontwain", null) != null);
            blTestJson = (Config.Get("testjson", null) != null);
            blTestDnssd = (Config.Get("testdnssd", null) != null);
            szIpc = Config.Get("ipc", null);
            iPid = int.Parse(Config.Get("parentpid", "0"));

            // Run in IPC mode.  The caller has set up a 'pipe' for us, so we'll use
            // that to send commands back and forth.  This is the normal mode when
            // we're running with a scanner...
            if (szIpc != null)
            {
                // With Windows we need a window for the driver, but we can hide it...
                if (TWAINCSToolkit.GetPlatform() == "WINDOWS")
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new FormTwain(szWriteFolder, szIpc, iPid, RunInUiThread));
                    return (true);
                }

                // Linux and Mac OS X are okay without a window...
                else
                {
                    TwainLocalOnTwain twainlocalontwain;

                    // Create our object...
                    twainlocalontwain = new TwainLocalOnTwain(szWriteFolder, szIpc, iPid, null, null, IntPtr.Zero);

                    // Run our object...
                    twainlocalontwain.Run();

                    // All done...
                    return (true);
                }
            }

            // Handle the TWAIN list, we use this during registration to find out
            // what drivers we can use, and collect some info about them...
            string szTwainList = Config.Get("twainlist", null);
            if (szTwainList != null)
            {
                if (szTwainList == "")
                {
                    szTwainList = Path.Combine(Config.Get("writeFolder", ""), "twainlist.txt");
                }
                System.IO.File.WriteAllText(szTwainList, Sword.TwainListDrivers());
                return (true);
            }

            // Test PDF/Raster...
            if (blTestPdfRaster)
            {
                TestPdfRaster testpdfraster;

                // Create our object...
                testpdfraster = new TestPdfRaster();

                // Do the test...
                testpdfraster.Test();

                // All done...
                TWAINWorkingGroup.Log.Close();
                return (true);
            }

            // Test TWAIN Local on TWAIN...
            if (blTestTwainLocalOnTwain)
            {
                TestTwainLocalOnTwain testtwainlocalontwain;

                // Create our object...
                testtwainlocalontwain = new TestTwainLocalOnTwain(Config.Get("scanner", null), szWriteFolder, iPid, szTaskFile);

                // Do the test...
                testtwainlocalontwain.Test();

                // All done...
                return (true);
            }

            // Test JSON...
            if (blTestJson)
            {
                TwainDirectSupport.JsonLookup jsonlookup = new TwainDirectSupport.JsonLookup();

                string szTest =
		            "{\n" +
		            "    \"array\": [\n" +
	                "        {\n" +
		            "            \\\"aaa\\\": 0,\n" +
                    "            'bbb': 1\n" +
                    "        },\n" +
                    "        {\n" +
                    "            ccc : 2,\n" +
                    "            'ddd' : 3,\n" +
                    "            \"xxx\": [\n" +
                    "                {\n" +
                    "                    \"mmm\": 111\n" +
                    "                },\n" +
                    "                {\n" +
                    "                    \"nnn\": 222\n" +
                    "                },\n" +
                    "                {\n" +
                    "                    \"ooo\": 333\n" +
                    "                }\n" +
                    "            ]\n" +
                    "        },\n" +
                    "        {\n" +
                    "            \"eee\": 4,\n" +
                    "            \"fff\": 5\n" +
                    "        }\n" +
                    "    ],\n" +
                    "    \"string\": \"value\"\n" +
                    "}";

                // Load something interesting...
                bool blSuccess = jsonlookup.Load(szTest, out lResponseCharacterOffset);
                if (!blSuccess)
                {
                    string sz = szTest.Substring(0, (int)lResponseCharacterOffset);
                }

                NativeMethods.AllocConsole();
                jsonlookup.Dump();

                // Find something interesting...
                string szValue;
                TwainDirectSupport.JsonLookup.EPROPERTYTYPE epropertytype;
                blSuccess = jsonlookup.GetCheck("array[0].aaa", out szValue, out epropertytype, true);

                // Test...
                string[] aszFile = Directory.GetFiles("C:/Users/l252353/Desktop/TwainDirect/source/TwainDirectOnTwain/bin/x86/Debug/data");
                if (aszFile != null)
                {
                    foreach (string szFile in aszFile)
                    {
                        // Skip non-.json files...
                        if (!szFile.Contains("pass0.json"))
                        {
                            continue;
                        }

                        // Read it...
                        string szJson = File.ReadAllText(szFile);

                        // Load it...
                        blSuccess = jsonlookup.Load(szJson, out lResponseCharacterOffset);

                        // File we're working on...
                        Console.WriteLine("\r\n*******************************************************************************");
                        Console.WriteLine(szFile);

                        // Fail file results...
                        if (szFile.Contains("fail"))
                        {
                            if (blSuccess)
                            {
                                Console.WriteLine("FAILED ON THE FAIL...");
                            }
                            else
                            {
                                Console.WriteLine("Error at offset: " + lResponseCharacterOffset);
                                Console.WriteLine(szJson.Substring(0, (int)lResponseCharacterOffset) + "^ERROR^" + szJson.Substring((int)lResponseCharacterOffset));
                            }
                        }

                        // Pass file results...
                        else
                        {
                            if (blSuccess)
                            {
                                jsonlookup.Dump();
                                jsonlookup.GetCheck("devices[0].id", out szValue, out epropertytype, true);
                            }
                            else
                            {
                                Console.WriteLine("Error at offset: " + lResponseCharacterOffset);
                                Console.WriteLine(szJson.Substring(0, (int)lResponseCharacterOffset) + "^ERROR^" + szJson.Substring((int)lResponseCharacterOffset));
                            }
                        }
                    }
                }

                // All done...
                return (true);
            }

            /// Test DNS-SD...
            if (blTestDnssd)
            {
                // Do the test...
                int tt;
                int jj;
                TwainDirectSupport.Dnssd.DnssdDeviceInfo[] adnssddeviceinfo = null;
                NativeMethods.AllocConsole();
                TwainDirectSupport.Dnssd dnssd = new TwainDirectSupport.Dnssd(TwainDirectSupport.Dnssd.Reason.Monitor);
                dnssd.MonitorStart();
                for (tt = 0; tt < 6000000; tt++)
                {
                    bool blUpdated;
                    TwainDirectSupport.Dnssd.DnssdDeviceInfo[] adnssddeviceinfoNew;
                    TwainDirectSupport.Dnssd.DnssdDeviceInfo[] adnssddeviceinfoCompare = adnssddeviceinfo;
                    adnssddeviceinfoNew = dnssd.GetSnapshot(adnssddeviceinfoCompare, out blUpdated);
                    adnssddeviceinfo = adnssddeviceinfoNew;
                    adnssddeviceinfoNew = null;
                    if (adnssddeviceinfo == null)
                    {
                        if (blUpdated)
                        {
                            Console.Out.WriteLine("");
                            Console.Out.WriteLine("*** empty list ***");
                        }
                        System.Threading.Thread.Sleep(1000);
                        continue;
                    }
                    if (blUpdated)
                    {
                        Console.Out.WriteLine("");
                        for (jj = 0; jj < adnssddeviceinfo.Length; jj++)
                        {
                            Console.Out.WriteLine
                            (
                                adnssddeviceinfo[jj].szServiceName + Environment.NewLine +
                                "  " + adnssddeviceinfo[jj].szLinkLocal +
                                " " + adnssddeviceinfo[jj].lInterface +
                                ((adnssddeviceinfo[jj].szIpv4 == null) ? " NoIpv4" : (" " + adnssddeviceinfo[jj].szIpv4)) +
                                ((adnssddeviceinfo[jj].szIpv6 == null) ? " NoIpv6" : (" " + adnssddeviceinfo[jj].szIpv6)) +
                                " " + adnssddeviceinfo[jj].lPort +
                                ((adnssddeviceinfo[jj].aszText == null) ? "" : (Environment.NewLine + "  " + string.Join(" ", adnssddeviceinfo[jj].aszText)))
                            );
                        }
                    }
                    System.Threading.Thread.Sleep(1000);
                }
                dnssd.MonitorStop();

                // All done...
                return (true);
            }

            // Execute the specified task...
            if (File.Exists(szTaskFile))
            {
                bool blSetAppCapabilities = false;
                Sword sword;
                SwordTask swordtask;

                // Init stuff...
                swordtask = new SwordTask();

                // Create our object...
                sword = new Sword(null);

                // Run our task...
                sword.BatchMode(Config.Get("scanner", null), szTaskFile, false, ref swordtask, ref blSetAppCapabilities);

                // All done...
                return (true);
            }

            // Otherwise let the user interact with us...
            TWAINWorkingGroup.Log.Info("Interactive mode...");
            return (false);
        }

        /// <summary>
        /// Our state delegate helper...yeah, this is convoluted...
        /// </summary>
        /// <returns>either S0 or the TWAIN state</returns>
        private static string GetState()
        {
            return ((ms_getstatedelegate == null) ? "S0" : ms_getstatedelegate());
        }

        // Our helper for getting the state value...
        private static TWAINWorkingGroup.Log.GetStateDelegate ms_getstatedelegate;
    }

    /// <summary>
    /// P/Invokes
    /// </summary>
    internal static class NativeMethods
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Windows
        ///////////////////////////////////////////////////////////////////////////////
        #region Windows

        /// <summary>
        /// So we can get a console window on Windows...
        /// </summary>
        /// <returns></returns>
        [DllImport("kernel32")]
        internal static extern bool AllocConsole();

        /// <summary>
        /// Get the desktop window so we have a parent...
        /// </summary>
        /// <returns></returns>
        [DllImport("user32.dll", SetLastError = false)]
        internal static extern IntPtr GetDesktopWindow();

        #endregion
    }
}
