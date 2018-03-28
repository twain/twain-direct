///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.Scanner.Terminal
//
// Run the scanner as a console app or a terminal.
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
using System.Threading;
using TwainDirect.Support;

namespace TwainDirect.Scanner
{
    internal sealed class Terminal : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Initialize stuff for our form...
        /// </summary>
        public Terminal()
        {
            // Confirm scan...
            bool blConfirmScan = (Config.Get("confirmscan", null) != null);

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
                blConfirmScan ? ConfirmScan : (TwainLocalScannerDevice.ConfirmScan)null,
                0,
                out m_blNoDevices
            );
            if (m_scanner == null)
            {
                Log.Error("Scanner failed...");
                throw new Exception("Scanner failed...");
            }
        }

        /// <summary>
        /// Destructor...
        /// </summary>
        ~Terminal()
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
        /// Register a device for use...
        /// </summary>
        public void Register()
        {
            int iScanner;
            long lResponseCharacterOffset;
            string szScanners;
            string szNumber;
            string szText;
            JsonLookup jsonlookup;
            ApiCmd apicmd;

            // Prompt the user to turn on their scanner, we don't care
            // about the result...
            szText = "";
            Display(Environment.NewLine);
            InputBox
            (
                "Power-On Scanner:",
                "Please turn your TWAIN scanner on, when it's ready press the ENTER key...",
                ref szText
            );

            // Turn the buttons off...
            Display(Environment.NewLine);
            Display("Looking for Scanners...");

            // Get the list of scanners...
            szScanners = m_scanner.GetAvailableScanners("getproductnames", "");
            if (string.IsNullOrEmpty(szScanners))
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
            int iNumber = 0;
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
                    Display((iScanner + 1) + ": " + szScanner + " ***DEFAULT***");
                    iNumber = iScanner + 1;
                }
                // Otherwise, just list it...
                else
                {
                    Display((iScanner + 1) + ": " + szScanner);
                }
            }

            // Build the text for the prompt...
            if (iScanner == 1)
            {
                szText =
                    "Enter 1 to select this scanner." + Environment.NewLine +
                    "Enter 0 to disable the system." + Environment.NewLine +
                    "Enter by itself (with no number) keeps the current setting.";
            }
            else if (iScanner == 2)
            {
                szText =
                    "Enter 1 or 2 to select a scanner." + Environment.NewLine +
                    "Enter 0 to disable the system." + Environment.NewLine +
                    "Enter by itself (with no number) keeps the current setting.";
            }
            else
            {
                szText =
                    "Enter a number from 1 to " + iScanner + " to select a scanner." + Environment.NewLine +
                    "Enter 0 to disable the system." + Environment.NewLine +
                    "Enter by itself (with no number) keeps the current setting.";
            }

            // Have the user select a scanner...
            for (;;)
            {
                // Prompt the user...
                szNumber = "";
                Display(Environment.NewLine);
                bool blOk = InputBox
                (
                    "Select Default Scanner:",
                    szText,
                    ref szNumber
                );

                // The user wants out...
                if (!blOk)
                {
                    break;
                }

                // Check the result...
                if (!int.TryParse(szNumber, out iNumber))
                {
                    Display("That wasn't a number...");
                    continue;
                }

                // Check the range...
                if ((iNumber < 0) || (iNumber > iScanner))
                {
                    Display("Please enter a valid number...");
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
                Display(Environment.NewLine);
                bool blOk = InputBox
                (
                    "Enter Note:",
                    (string.IsNullOrEmpty(m_scanner.GetTwainLocalNote()) ? "You have no note." : "Your current note is: " + m_scanner.GetTwainLocalNote()) + Environment.NewLine +
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
            Display(Environment.NewLine);
            Display("Press the enter key to finish...");
            Console.In.ReadLine();
        }

        /// <summary>
        /// Start polling for work...
        /// </summary>
        public void Start()
        {
            int iChar;
            bool blSuccess;

            // Start polling...
            Display("");
            Display("Starting, please wait...");
            blSuccess = m_scanner.MonitorTasksStart();
            if (!blSuccess)
            {
                Log.Error("MonitorTasksStart failed...");
                Display("Failed to start the device, check the logs for more information.");
                return;
            }
            Display("Ready for use...");

            // Prompt...
            Display("Press the enter key to stop...");

            // Handler...
            for (;;)
            {
                while (!Console.KeyAvailable && !m_blPauseConsole)
                {
                    System.Threading.Thread.Sleep(100);
                }

                // We've been paused...
                if (m_blPauseConsole)
                {
                    while (m_blPauseConsole)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    continue;
                }

                // Read the data...
                iChar = Console.In.Read();
                while (Console.In.Peek() != -1)
                {
                    Console.In.Read();
                }

                // We're done...
                if ((iChar == '\r') || (iChar == '\n'))
                {
                    break;
                }

                // Status...
                if (iChar == '?')
                {
                    m_scanner.GetInfo();
                }
            }

            // Stop...
            m_scanner.MonitorTasksStop(true);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        internal void Dispose(bool a_blDisposing)
        {
            // Free managed resources...
            if (a_blDisposing)
            {
                if (m_scanner != null)
                {
                    m_scanner.Dispose();
                    m_scanner = null;
                }
            }
        }

        /// <summary>
        /// Prompt the user prior to scanning...
        /// </summary>
        /// <returns>the button they pressed...</returns>
        private TwainLocalScanner.ButtonPress ConfirmScan(float a_fConfirmScanScale)
        {
            string szValue = "";

            // Pause the main...
            m_blPauseConsole = true;

            // Prompt the user...
            Display("");
            Display("********************************");
            InputBox("Scan Request", "Would you like to scan (YES/no)? ", ref szValue);

            // Resume the main...
            m_blPauseConsole = false;

            // Default action...
            if (string.IsNullOrEmpty(szValue))
            {
                return (TwainLocalScanner.ButtonPress.OK);
            }

            // Check for yes...
            szValue = szValue.ToLower();
            if ((szValue == "y") || (szValue == "ye") || (szValue == "yes"))
            {
                return (TwainLocalScanner.ButtonPress.OK);
            }

            // We're cancelling...
            return (TwainLocalScanner.ButtonPress.Cancel);
        }

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
            return (!string.IsNullOrEmpty(a_szValue));
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
        // Private Operations...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Operations...

        /// <summary>
        /// Stop polling for work...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Stop(object sender, EventArgs e)
        {
            // Staaaaaaahp...
            m_scanner.MonitorTasksStop(true);
            Display("Stop...");
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

        /// <summary>
        /// A slimey way to steal the console...
        /// </summary>
        private bool m_blPauseConsole;

        #endregion
    }
}
