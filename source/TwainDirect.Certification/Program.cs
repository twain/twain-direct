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
//  Copyright (C) 2014-2018 Kodak Alaris Inc.
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
using System.Threading;
using System.Windows.Forms;
using TwainDirect.Support;

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
            string szExecutableName;
            string szWriteFolder;

            // Load our configuration information and our arguments,
            // so that we can access them from anywhere in the code...
            if (!Config.Load(Application.ExecutablePath, a_aszArgs, "appdata.txt"))
            {
                MessageBox.Show("Error starting.  Try uninstalling and reinstalling this software.", "Error");
                Environment.Exit(1);
            }

            // Set up our data folders...
            szWriteFolder = Config.Get("writeFolder", "");
            szExecutableName = Config.Get("executableName", "");

            // Turn on logging...
            Log.Open(szExecutableName, szWriteFolder, (int)Config.Get("logLevel", 0));
            Log.Info(szExecutableName + " Log Started...");

            // Create our main form...
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            FormMain formmain = new FormMain();

            // Launch the terminal window, we're doing this in
            // a thread so we can have a console and a form...
            Terminal terminal = new TwainDirect.Certification.Terminal(formmain);
            Thread threadTerminal = new Thread(
                new ThreadStart(
                    delegate ()
                    {
                        terminal.Run();
                    }
                )
            );
            threadTerminal.Start();

            // Run our form (needed for the cloud stuff)...
            Application.Run(formmain);

            // All done...
            Log.Info(szExecutableName + " Log Ended...");
            Log.Close();
            Environment.Exit(0);
        }
    }
}
