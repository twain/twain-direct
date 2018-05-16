///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.Scanner.Service
//
// Run the scanner as a service.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    05-Dec-2014     Initial Release
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
using System.Resources;
using System.ServiceProcess;
using System.Threading;
using TwainDirect.Support;

namespace TwainDirect.Scanner
{
    internal sealed class Service : ServiceBase
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        public Service()
        {
            // Confirm scan...
            bool blConfirmScan = (Config.Get("confirmscan", null) != null);

            // Init service base stuff...
            ServiceName = "TwainDirectService";
            CanStop = true;
            CanPauseAndContinue = true;
            AutoLog = true;

            // Localize...
            string szCurrentUiCulture = "." + Thread.CurrentThread.CurrentUICulture.ToString();
            if (szCurrentUiCulture == ".en-US")
            {
                szCurrentUiCulture = "";
            }
            try
            {
                m_resourcemanager = new ResourceManager("TwainDirect.Scanner.WinFormStrings" + szCurrentUiCulture, typeof(Form1).Assembly);
            }
            catch
            {
                m_resourcemanager = new ResourceManager("TwainDirect.Scanner.WinFormStrings", typeof(Form1).Assembly);
            }

            // Instantiate our scanner object...
            m_scanner = new Scanner
            (
                m_resourcemanager,
                Display,
                blConfirmScan ? (TwainLocalScannerDevice.ConfirmScan)null : (TwainLocalScannerDevice.ConfirmScan)null,
                0,
                out m_blNoDevices
            );
            if (m_scanner == null)
            {
                Log.Error("Scanner failed...");
                throw new Exception("Scanner failed...");
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Protected Methods (ServiceBase overrides)...
        ///////////////////////////////////////////////////////////////////////////////
        #region Protected Methods (ServiceBase overrides)...

        /// <summary>
        /// Register...
        /// 
        /// TBD: Obviously this needs a way to communicate with
        /// the user other than console stuff, so fix that...
        /// </summary>
        /// <param name="a_iCommand"></param>
        protected override void OnCustomCommand(int a_iCommand)
        {
            int iScanner;
            long lResponseCharacterOffset;
            string szScanners;
            string szNumber;
            string szText;
            JsonLookup jsonlookup;
            ApiCmd apicmd;

            // Turn the buttons off...
            Display("Looking for Scanners...");

            // Get the list of scanners...
            szScanners = m_scanner.GetAvailableScanners("getproductnames", "");
            if (szScanners == null)
            {
                Display("No scanners found...");
                return;
            }
            try
            {
                jsonlookup = new JsonLookup();
                jsonlookup.Load(szScanners, out lResponseCharacterOffset);
            }
            catch
            {
                Display("No scanners found...");
                return;
            }

            // Show all the scanners, and then ask for the number of
            // the one to use as the new default...
            szText = "";
            for (iScanner = 0; ; iScanner++)
            {
                // Get the next scanner...
                string szScanner = jsonlookup.Get("scanners[" + iScanner + "].twidentityProductName");
                if (string.IsNullOrEmpty(szScanner))
                {
                    szScanner = jsonlookup.Get("scanners[" + iScanner + "].sane");
                }

                // We're out of stuff...
                if (string.IsNullOrEmpty(szScanner))
                {
                    break;
                }

                // If this is the current default, make a note of it...
                if (m_scanner.GetTwainLocalTy() == szScanner)
                {
                    szText = (iScanner + 1) + ": " + szScanner + " ***DEFAULT***";
                    Display(szText);
                }
                // Otherwise, just list it...
                else
                {
                    Display((iScanner + 1) + ": " + szScanner);
                }
            }

            // Finish the text for the prompt...
            if (string.IsNullOrEmpty(szText))
            {
                szText =
                    "Enter a number from 1 to " + iScanner + Environment.NewLine +
                    "(there is no current default)";
            }
            else
            {
                szText =
                    "Enter a number from 1 to " + iScanner + Environment.NewLine +
                    szText;
            }

            // Select the default...
            int iNumber = 0;
            for (; ; )
            {
                // Prompt the user...
                szNumber = "";
                bool blOk = InputBox
                (
                    "Select Default Scanner",
                    szText,
                    ref szNumber
                );

                // The user wants out...
                if (!blOk)
                {
                    Display("Canceled...");
                    break;
                }

                // Check the result...
                if (!int.TryParse(szNumber, out iNumber))
                {
                    Display("Please enter a number in the range 1 to " + iScanner);
                    continue;
                }

                // Check the range...
                if ((iNumber < 1) || (iNumber > iScanner))
                {
                    Display("Please enter a number in the range 1 to " + iScanner);
                    continue;
                }

                // We have what we want...
                break;
            }

            // Do a deep inquiry on the selected scanner...
            szScanners = m_scanner.GetAvailableScanners("getinquiry", jsonlookup.Get("scanners[" + (iScanner - 1) + "].twidentityProductName"));
            if (szScanners == null)
            {
                Display("We are unable to use the selected scanner...");
                return;
            }
            try
            {
                jsonlookup = new JsonLookup();
                jsonlookup.Load(szScanners, out lResponseCharacterOffset);
            }
            catch
            {
                Display("We are unable to use the selected scanner...");
                return;
            }

            // See if the user wants to update their note...
            string szNote = m_scanner.GetTwainLocalNote();
            if ((iNumber >= 1) && (iNumber <= iScanner))
            {
                bool blOk = InputBox
                (
                    "Enter Note",
                    "Your current note is: " + m_scanner.GetTwainLocalNote() + Environment.NewLine +
                    "Type a new note, or just press the Enter key to keep what you have.",
                    ref szNote
                );
                if (string.IsNullOrEmpty(szNote))
                {
                    szNote = m_scanner.GetTwainLocalNote();
                }
            }

            // Register it, make a note if it works by clearing the
            // no devices flag...
            apicmd = new ApiCmd();
            if (m_scanner.RegisterScanner(jsonlookup, 0, szNote, ref apicmd))
            {
                m_blNoDevices = false;
                Display("Done...");
            }
            else
            {
                Display("Registration failed for: " + iNumber);
            }

            // Prompt...
            Display("Press the enter key to finish...");
            Console.In.ReadLine();
        }

        /// <summary>
        /// Start polling for work...
        /// </summary>
        /// <param name="args"></param>
        protected override async void OnStart(string[] args)
        {
            bool blSuccess;

            // Start polling...
            Display("");
            Display("Starting, please wait...");
            blSuccess = await m_scanner.MonitorTasksStart();
            if (!blSuccess)
            {
                Log.Error("MonitorTasksStart failed...");
                return;
            }
            Display("Ready for use...");

            // Prompt...
            Display("Press the enter key to stop...");
            Console.In.ReadLine();

            // Stop...
            m_scanner.MonitorTasksStop(true);
        }

        /// <summary>
        /// Stop polling for work...
        /// </summary>
        protected override void OnStop()
        {
            // Staaaaaaahp...
            m_scanner.MonitorTasksStop(true);
            Display("Stop...");
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Input text...
        /// </summary>
        /// <param name="title">title of the box</param>
        /// <param name="promptText">prompt to the user</param>
        /// <param name="value">text typed by the user</param>
        /// <returns>true on success, false on cancel</returns>
        private bool InputBox(string a_szTitle, string a_szPrompt, ref string a_szValue)
        {
            // Prompt the user...
            Display(a_szTitle);
            Display(a_szPrompt);

            // Get the data...
            a_szValue = Console.In.ReadLine();

            // All done...
            return ((a_szValue != null) && (a_szValue != ""));
        }

        /// <summary>
        /// Display a message...
        /// </summary>
        /// <param name="a_szMsg">the thing to display</param>
        private void Display(string a_szMsg)
        {
            Console.Out.WriteLine(a_szMsg);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// Localized strings...
        /// </summary>
        private ResourceManager m_resourcemanager;

        /// <summary>
        /// Our scanner interface...
        /// </summary>
        private Scanner m_scanner;

        /// <summary>
        /// True if we have no devices...
        /// </summary>
        private bool m_blNoDevices;

        #endregion
    }
}
