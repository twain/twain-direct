///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirect.OnTwain.Program
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
//  Copyright (C) 2014-2020 Kodak Alaris Inc.
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
using System.IO;
using System.Threading;
using System.Windows.Forms;
using TwainDirect.Support;
using TWAINWorkingGroup;

namespace TwainDirect.OnTwain
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
            // Override TWAIN's logging system.  We're doing this so
            // that we can integrate our logging with TWAIN to get a
            // nice continous log between the two systems.  we don't
            // directly use TwainDirect's logging anywhere in this
            // product, except in this call...
            TWAINWorkingGroup.Log.Override
            (
                TwainDirect.Support.Log.Close,
                TwainDirect.Support.Log.GetLevel,
                TwainDirect.Support.Log.Open,
                null,
                TwainDirect.Support.Log.SetFlush,
                TwainDirect.Support.Log.SetLevel,
                TwainDirect.Support.Log.WriteEntry,
                out ms_getstatedelegate
            );
            TwainDirect.Support.Log.SetStateDelegate(GetState);

            // Load our configuration information and our arguments,
            // so that we can access them from anywhere in the code...
            if (!Config.Load(Application.ExecutablePath, a_aszArgs, "appdata.txt"))
            {
                MessageBox.Show("Error starting.  Try uninstalling and reinstalling this software.", "Error");
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
            string szIpc;
            string szTaskFile;
            string szImagesFolder;
            bool blTestPdfRaster;
            string szTestDnssd;

            // Check the arguments...
            string szWriteFolder = Config.Get("writeFolder", null);
            string szExecutableName = Config.Get("executableName", null);
            szTaskFile = Config.Get("task", null);
            blTestPdfRaster = (Config.Get("testpdfraster", null) != null);
            szTestDnssd = Config.Get("testdnssd", null);
            szIpc = Config.Get("ipc", null);
            szImagesFolder = Config.Get("images", null);
            if (string.IsNullOrEmpty(szImagesFolder))
            {
                szImagesFolder = Path.Combine(szWriteFolder, "images");
            }
            iPid = int.Parse(Config.Get("parentpid", "0"));

            // Run in IPC mode.  The caller has set up a 'pipe' for us, so we'll use
            // that to send commands back and forth.  This is the normal mode when
            // we're running with a scanner...
            if (szIpc != null)
            {
                // With Windows we need a window for the driver, but we can hide it...
                if (TWAIN.GetPlatform() == TWAIN.Platform.WINDOWS)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new FormTwain(szWriteFolder, szImagesFolder, szIpc, iPid));
                    return (true);
                }

                // Linux and Mac OS X are okay without a window...
                else
                {
                    TwainLocalOnTwain twainlocalontwain;

                    // Create our object...
                    twainlocalontwain = new TwainLocalOnTwain(szWriteFolder, szImagesFolder, szIpc, iPid, null, null, IntPtr.Zero);

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
                string szTwainListAction = Config.Get("twainlistaction", null);
                string szTwainListData = Config.Get("twainlistdata", null);
                if (szTwainList == "")
                {
                    szTwainList = Path.Combine(Config.Get("writeFolder", ""), "twainlist.txt");
                }
                System.IO.File.WriteAllText(szTwainList, ProcessSwordTask.TwainListDrivers(szTwainListAction, szTwainListData));
                return (true);
            }

            /// Test DNS-SD...
            if (!string.IsNullOrEmpty(szTestDnssd))
            {
                if (szTestDnssd == "monitor")
                {
                    int ii;
                    int jj;
                    Dnssd dnssd;
                    bool blServiceIsAvailable;
                    Interpreter.CreateConsole();
                    List<Dnssd.DnssdDeviceInfo> ldnssddeviceinfo = new List<Dnssd.DnssdDeviceInfo>();
                    dnssd = new Dnssd(Dnssd.Reason.Monitor, out blServiceIsAvailable);
                    if (blServiceIsAvailable)
                    {
                        dnssd.MonitorStart(null, IntPtr.Zero);
                        for (ii = 0; ii < 60; ii++)
                        {
                            bool blUpdated = false;
                            bool blNoMonitor = false;
                            Thread.Sleep(1000);
                            ldnssddeviceinfo = dnssd.GetSnapshot(ldnssddeviceinfo, out blUpdated, out blNoMonitor);
                            if (blUpdated)
                            {
                                Console.Out.WriteLine("");
                                if ((ldnssddeviceinfo == null) || (ldnssddeviceinfo.Count == 0))
                                {
                                    Console.Out.WriteLine("***empty***");
                                }
                                else
                                {
                                    for (jj = 0; jj < ldnssddeviceinfo.Count; jj++)
                                    {
                                        Console.Out.WriteLine(ldnssddeviceinfo[jj].GetInterface() + " " + ldnssddeviceinfo[jj].GetServiceName());
                                    }
                                }
                            }
                        }
                        dnssd.MonitorStop();
                    }
                    dnssd.Dispose();
                    dnssd = null;
                }
                else if (szTestDnssd == "register")
                {
                    Dnssd dnssd;
                    bool blServiceIsAvailable;
                    Interpreter.CreateConsole();
                    dnssd = new Dnssd(Dnssd.Reason.Register, out blServiceIsAvailable);
                    if (blServiceIsAvailable)
                    {
                        dnssd.RegisterStart("Instance", 55556, "Ty", "", "", "", "Note");
                        Thread.Sleep(60000);
                        dnssd.RegisterStop();
                    }
                    dnssd.Dispose();
                    dnssd = null;
                }

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
}
