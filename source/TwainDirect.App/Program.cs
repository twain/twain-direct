///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirect.App.Program
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
using System.Windows.Forms;
using TwainDirect.Support;
[assembly: CLSCompliant(true)]

namespace TwainDirect.App
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
			// TODO: Handle this correctly - this is a temporary workaround.

	        //Trust all certificates
	        System.Net.ServicePointManager.ServerCertificateValidationCallback =
		        ((sender, certificate, chain, sslPolicyErrors) => true);

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
                Log.Error("Main exception - " + exception.Message);
            }
            finally
            {
                formscan.Dispose();
                formscan = null;
            }
        }
    }
}
