///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirect.OnSane.Program
//
//  Use a SWORD task to control a SANE driver.  This is a general solution that
//  represents standard SWORD on standard SANE.  Other schemes are needed to get
//  at the custom features that may be available for a device.
//
//  In here we're either processing a file passed in as a command line argument,
//  or we're going to an interactive mode.
//
//  This solution is diffent from TWAIN in that we're running scanimage to use
//  the SANE driver.  This has the benefit of being fairly easy to code, at the
//  cost of losing some programmatic control.
//
//  The breakdown remains the same, though.  The Sword class controls the basic
//  flow.  SwordTask parses the TWAIN Direct task.  SaneTask extracts the items
//  that SANE can handle.  SaneSelectStream analyzes the values and picks the
//  stream to use.  We then run scanimage and parse its standard output, building
//  the metadata and the PDF/raster and sending that across TWAIN Local.
//
//  It shouldn't be too hard to switch to programmatic mode, the real challenge
//  for that is creating C# versions of the SANE definitions and entry points,
//  doing it in a way that handles 32-bit and 64-bit, and offering whatever
//  toolkit is needed to protect the main code from the different image capture
//  behavior of the various SANE drivers.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    21-Aug-2015     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2015-2016 Kodak Alaris Inc.
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
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using TwainDirect.Support;
//[assembly: CLSCompliant(true)]

namespace TwainDirect.OnSane
{
    /// <summary>
    /// Our main program.  We're keeping it simple here...
    /// </summary>
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="a_aszArgs">interesting arguments</param>
        [STAThread]
        static void Main(string[] a_aszArgs)
        {
            // Load our configuration information and our arguments,
            // so that we can access them from anywhere in the code...
            if (!Config.Load(Application.ExecutablePath, a_aszArgs, "appdata.txt"))
            {
                MessageBox.Show("Error starting, is appdata.txt damaged?");
                Application.Exit();
            }

            // Turn on logging...
            string szExecutableName = Config.Get("executableName", "");
            Log.Open(szExecutableName, Config.Get("writeFolder", ""), 1);
            Log.SetLevel((int)Config.Get("logLevel", 0));
            TwainDirect.Support.Log.Info(szExecutableName + " Log Started...");

            // Let the manager figure out which mode we're in: batch or
            // interactive.  If batch it'll return true and it'll silently
            // try to run the task given to it.  If interactive we'll drop
            // down and raise the dialog...
            if (SelectMode())
            {
                TwainDirect.Support.Log.Info("Exiting...");
                Log.Close();
                Environment.Exit(0);
                return;
            }

            // Hellooooooo, user...
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());

            // Bye-bye...
            Log.Close();
        }

        /// <summary>
        /// Select the mode for this session, batch or interactive,
        /// based on the arguments.  Don't be weirded out by this
        /// function calling TWAIN Local On Sane and then that function using
        /// Sword.  This is a static function, it's just a convenience
        /// to use the Sword object to hold it, it could go anywhere
        /// and later on probably will (like as a function inside of
        /// the Program module).
        /// </summary>
        /// <returns>true for batch mode, false for interactive mode</returns>
        public static bool SelectMode()
        {
            int iPid = 0;
            string szIpc = null;
            string szTaskFile = "";
            string szWriteFolder;

            // Sleep so we can attach and debug stuff...
            int iDelay = (int)Config.Get("delayTwainDirectOnSane", 0);
            if (iDelay > 0)
            {
                Thread.Sleep(iDelay);
            }

            // Check the arguments...
            szWriteFolder   = Config.Get("writeFolder", null);
            szTaskFile      = Config.Get("task", null);
            szIpc           = Config.Get("ipc", null);
            iPid            = int.Parse(Config.Get("parentpid", "0"));

            // Handle the SANE list...
            string szSaneList = Config.Get("sanelist", null);
            if (szSaneList != null)
            {
                if (szSaneList == "")
                {
                    szSaneList = Path.Combine(Config.Get("writeFolder", ""), "sanelist.txt");
                }
                System.IO.File.WriteAllText(szSaneList,Sword.SaneListDrivers());
                return (true);
            }

            // Run in IPC mode.  The caller has set up a 'pipe' for us, so we'll use
            // that to send commands back and forth...
            if (szIpc != null)
            {
                TwainLocalOnSane twainlocalonsane;

                // Create our object...
                twainlocalonsane = new TwainLocalOnSane(szWriteFolder, szIpc, iPid);

                // Run our object...
                twainlocalonsane.Run();

                // All done...
                return (true);
            }

            // If we have a file, then take the shortcut, this is the path to use
            // when running under another program, like an HTTP service.
            if (File.Exists(szTaskFile))
            {
                bool blSetAppCapabilities = false;
                string szScanImageArguments = "";
                Sword sword;
                SwordTask swordtask;

                // Init stuff...
                swordtask = new SwordTask();

                // Create our object...
                sword = new Sword();

                // Run our task...
                sword.BatchMode(Config.Get("scanner", null), szTaskFile, false, ref swordtask, ref blSetAppCapabilities, out szScanImageArguments);

                // All done...
                return (true);
            }

            // Otherwise let the user interact with us...
            TwainDirect.Support.Log.Info("Interactive mode...");
            return (false);
        }

        /// <summary>
        /// Handle when the process exits...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void a_process_Exited(object sender, EventArgs e)
        {
            ms_datareceivedeventhandlerReadLine(null,null);
        }

        /// <summary>
        /// Run SANE's scanimage application...
        /// </summary>
        /// <param name="a_szReason">reason for the call</param>
        /// <param name="a_szArguments">arguments to the call</param>
        /// <returns>the standard output</returns>
        private static DataReceivedEventHandler ms_datareceivedeventhandlerReadLine = null;
        public static string[] ScanImage
        (
            string a_szReason,
            string a_szArguments,
            ref Process a_process,
            DataReceivedEventHandler a_datareceivedeventhandlerReadLine
        )
        {
            // Log what we're sending...
            TwainDirect.Support.Log.Info("");
            TwainDirect.Support.Log.Info("scanimage>>> " + a_szReason);
            TwainDirect.Support.Log.Info("scanimage>>> scanimage" + " " + a_szArguments);
            ms_datareceivedeventhandlerReadLine = null;

            // Just start the process and return...
            if (a_process != null)
            {
                // Redirect the output stream of the child process.
                a_process.StartInfo.UseShellExecute = false;
                a_process.StartInfo.CreateNoWindow = true;
                a_process.StartInfo.RedirectStandardOutput = (a_datareceivedeventhandlerReadLine != null);
                a_process.StartInfo.RedirectStandardError = (a_datareceivedeventhandlerReadLine != null);
                a_process.StartInfo.FileName = "scanimage";
                a_process.StartInfo.Arguments = a_szArguments;
                if (a_datareceivedeventhandlerReadLine != null)
                {
                    ms_datareceivedeventhandlerReadLine = a_datareceivedeventhandlerReadLine;
                    a_process.OutputDataReceived += a_datareceivedeventhandlerReadLine;
                    a_process.ErrorDataReceived += a_datareceivedeventhandlerReadLine;
                    a_process.EnableRaisingEvents = true;
                    a_process.Exited += new EventHandler(a_process_Exited);
                }
                a_process.Start();
                if (a_datareceivedeventhandlerReadLine != null)
                {
                    a_process.BeginOutputReadLine();
                    a_process.BeginErrorReadLine();
                }
                return (null);
            }

            // Run it all here...
            else
            {
                // Start the child process.
                Process p = new Process();

                // Redirect the output stream of the child process.
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.FileName = "scanimage";
                p.StartInfo.Arguments = a_szArguments;
                p.Start();

                // Grab the standard output, then wait for the
                // process to fully exit...
                string szReply = "";
                string szError = "";
                szReply = p.StandardOutput.ReadToEnd();
                szError = p.StandardError.ReadToEnd();
                p.WaitForExit();

                // Sort out the result...
                // TBD: we need a lot more smarts in this area to capture
                // errors, catagorize them, get a human readable bit of
                // text and work out a response.  In most cases we'll just
                // have to flat out fail, but the more data we can give to
                // the user, the better off they'll be...
                if ((szReply == "") && (szError != ""))
                {
                    szReply = "ERROR: " + szError;
                }

                // Log what we got back......
                TwainDirect.Support.Log.Info("scanimage>>> " + szReply);

                // Return the data as a string (it's usually json)...
                return (szReply.Split(new char[] { '\n' }));
            }
        }
    }
}
