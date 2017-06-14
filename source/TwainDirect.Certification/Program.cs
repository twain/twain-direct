///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirect.Certification.Program
//
//  Our entry point.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    21-Oct-2014     Initial Release
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
using System.Windows.Forms;
using TwainDirect.Support;
[assembly: CLSCompliant(true)]

namespace TwainDirect.Certification
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="a_aszArgs">interesting arguments</param>
        [STAThread]
        static void Main(string[] a_aszArgs)
        {
            /*
            FormScan formscan;

            // Basic initialization stuff...
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Load our configuration information and our arguments,
            // so that we can access them from anywhere in the code...
            if (!Config.Load(Application.ExecutablePath, a_aszArgs, "appdata.txt"))
            {
                MessageBox.Show("Error starting, is appdata.txt damaged?");
                Application.Exit();
            }

            // Run our form...
            formscan = new FormScan();
            try
            {
                if (!formscan.ExitRequested())
                {
                    Application.Run(formscan);
                }
            }
            catch (Exception exception)
            {
                Log.Error("exception - " + exception.Message);
            }
            finally
            {
                formscan.Dispose();
                formscan = null;
            }
            */

            string szExecutableName;
            string szWriteFolder;
            FormScan formscan;

            // Load our configuration information and our arguments,
            // so that we can access them from anywhere in the code...
            if (!Config.Load(Application.ExecutablePath, a_aszArgs, "appdata.txt"))
            {
                MessageBox.Show("Error starting, is appdata.txt damaged?");
                Environment.Exit(1);
            }

            // Set up our data folders...
            szWriteFolder = Config.Get("writeFolder", "");
            szExecutableName = Config.Get("executableName", "");

            // Turn on logging...
            Log.Open(szExecutableName, szWriteFolder, 1);
            Log.SetLevel((int)Config.Get("logLevel", 0));
            Log.Info(szExecutableName + " Log Started...");

            // Pick our command...
            switch (Config.Get("mode", "terminal"))
            {
                // Uh-oh...
                default:
                    Log.Error("Unrecognized mode: " + Config.Get("mode", "terminal"));
                    break;

                case "terminal":
                    Terminal terminal = new TwainDirect.Certification.Terminal();
                    terminal.Run();
                    break;

                // Fire up our application window...
                case "window":
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    formscan = new FormScan();
                    try
                    {
                        if (!formscan.ExitRequested())
                        {
                            Application.Run(formscan);
                        }
                    }
                    catch (Exception exception)
                    {
                        Log.Error("exception - " + exception.Message);
                    }
                    finally
                    {
                        formscan.Dispose();
                        formscan = null;
                    }
                    break;
            }


            // All done...
            Log.Info(szExecutableName + " Log Ended...");
            Log.Close();
            Environment.Exit(0);
        }
    }
}
