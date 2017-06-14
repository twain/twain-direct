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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TwainDirect.Support;
using Microsoft.Win32.SafeHandles;

namespace TwainDirect.Certification
{
    /// <summary>
    /// The certification object that we'll use to test and exercise functions
    /// for TWAIN Direct.
    /// </summary>
    class Terminal
    {
        // Public Methods
        #region Public Methods

        /// <summary>
        /// Initialize stuff...
        /// </summary>
        public Terminal()
        {
            // Make sure we have a console...
            if (TwainLocalScanner.GetPlatform() == TwainLocalScanner.Platform.WINDOWS)
            {
                NativeMethods.AllocConsole();
                // We have to do some additional work to get out text in the console instead
                // of having it redirected to Visual Studio's output window...
                IntPtr stdHandle = NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE);
                SafeFileHandle safefilehandle = new SafeFileHandle(stdHandle, true);
                FileStream fileStream = new FileStream(safefilehandle, FileAccess.Write);
                Encoding encoding = System.Text.Encoding.GetEncoding(Encoding.Default.CodePage);
                StreamWriter streamwriterStdout = new StreamWriter(fileStream, encoding);
                streamwriterStdout.AutoFlush = true;
                Console.SetOut(streamwriterStdout);
            }

            // Init stuff...
            m_blSilent = false;
            m_adnssddeviceinfoSnapshot = null;
            m_dnssddeviceinfoSelected = null;
            m_twainlocalscanner = null;

            // Create the mdns monitor, and start it...
            m_dnssd = new Dnssd(Dnssd.Reason.Monitor);
            m_dnssd.MonitorStart(null, IntPtr.Zero);

            // Build our command table...
            m_ldispatchtable = new List<Interpreter.DispatchTable>();
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiClosesession,  new string[] { "cl", "closesession" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiCreatesession, new string[] { "cr", "createsession" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiInfoex,        new string[] { "in", "infoex" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdHelp,             new string[] { "h", "help", "?" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdList,             new string[] { "l", "list" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdQuit,             new string[] { "ex", "exit", "q", "quit" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdSelect,           new string[] { "s", "select" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdStatus,           new string[] { "status" }));

            // Say hi...
            Assembly assembly = typeof(Terminal).Assembly;
            AssemblyName assemblyname = assembly.GetName();
            Version version = assemblyname.Version;
            DateTime datetime = new DateTime(2000, 1, 1).AddDays(version.Build).AddSeconds(version.MinorRevision * 2);

            Console.Out.WriteLine("TWAIN Direct Certification v" + version.Major + "." + version.Minor + " " + datetime.ToShortDateString() + " " + ((IntPtr.Size == 4) ? " (32-bit)" : " (64-bit)"));
            Console.Out.WriteLine("Enter \"help\" for more info.");
        }

        /// <summary>
        /// Run the certification tool...
        /// </summary>
        public void Run()
        {
            Interpreter interpreter = new Interpreter("tdc>>> ");

            // Run until told to stop...
            while (true)
            {
                bool blDone;
                string szCmd;
                string[] aszCmd;

                // Prompt...
                szCmd = interpreter.Prompt();

                // Tokenize...
                aszCmd = interpreter.Tokenize(szCmd);

                // Dispatch...
                blDone = interpreter.Dispatch(aszCmd, m_ldispatchtable);
                if (blDone)
                {
                    return;
                }
            }
        }

        #endregion


        // Private Methods (commands)
        #region Private Methods (commands)

        /// <summary>
        /// Close a session...
        /// </summary>
        /// <param name="a_aszCmd">tokenized command</param>
        /// <returns>true to quit</returns>
        private bool CmdApiClosesession(string[] a_aszCmd)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscanner.ClientScannerCloseSession(ref apicmd);

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Create a session...
        /// </summary>
        /// <param name="a_aszCmd">tokenized command</param>
        /// <returns>true to quit</returns>
        private bool CmdApiCreatesession(string[] a_aszCmd)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscanner.ClientScannerCreateSession(m_dnssddeviceinfoSelected, ref apicmd);

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Send an infoex command to the selected scanner...
        /// </summary>
        /// <param name="a_aszCmd">tokenized command</param>
        /// <returns>true to quit</returns>
        private bool CmdApiInfoex(string[] a_aszCmd)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscanner.ClientInfo(m_dnssddeviceinfoSelected, ref apicmd);

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Help the user...
        /// </summary>
        /// <param name="a_aszCmd">tokenized command</param>
        /// <returns>true to quit</returns>
        private bool CmdHelp(string[] a_aszCmd)
        {
            Console.Out.WriteLine("help   - this text");
            Console.Out.WriteLine("list   - list scanners");
            Console.Out.WriteLine("quit   - exit the program");
            Console.Out.WriteLine("select - select 'scanner'");
            Console.Out.WriteLine("status - status of the program");
            return (false);
        }

        /// <summary>
        /// List scanners, both ones on the LAN and ones that are
        /// available in the cloud (when we get that far)...
        /// </summary>
        /// <param name="a_aszCmd">tokenized command</param>
        /// <returns>true to quit</returns>
        private bool CmdList(string[] a_aszCmd)
        {
            bool blUpdated;

            // Get a snapshot of the TWAIN Local scanners...
            m_adnssddeviceinfoSnapshot = m_dnssd.GetSnapshot(null, out blUpdated);

            // Display TWAIN Local...
            if (!m_blSilent)
            {
                if ((m_adnssddeviceinfoSnapshot == null) || (m_adnssddeviceinfoSnapshot.Length == 0))
                {
                    Console.Out.WriteLine("*** no TWAIN Local scanners ***");
                }
                else
                {
                    foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in m_adnssddeviceinfoSnapshot)
                    {
                        Console.Out.WriteLine(dnssddeviceinfo.szLinkLocal + " " + (!string.IsNullOrEmpty(dnssddeviceinfo.szIpv4) ? dnssddeviceinfo.szIpv4 : dnssddeviceinfo.szIpv6) + " " + dnssddeviceinfo.szTxtNote);
                    }
                }
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Quit...
        /// </summary>
        /// <param name="a_aszCmd">tokenized command</param>
        /// <returns>true to quit</returns>
        private bool CmdQuit(string[] a_aszCmd)
        {
            return (true);
        }

        /// <summary>
        /// Select a scanner, do a snapshot, if needed, if no selection
        /// is offered, then pick the first scanner found...
        /// </summary>
        /// <param name="a_aszCmd">tokenized command</param>
        /// <returns>true to quit</returns>
        private bool CmdSelect(string[] a_aszCmd)
        {
            bool blSilent;

            // Clear the last selected scanner...
            m_dnssddeviceinfoSelected = null;
            if (m_twainlocalscanner != null)
            {
                m_twainlocalscanner.Dispose();
                m_twainlocalscanner = null;
            }

            // If we don't have a snapshot, get one...
            if ((m_adnssddeviceinfoSnapshot == null) || (m_adnssddeviceinfoSnapshot.Length == 0))
            {
                blSilent = m_blSilent;
                m_blSilent = true;
                CmdList(null);
                m_blSilent = blSilent;
            }

            // No joy...
            if ((m_adnssddeviceinfoSnapshot == null) || (m_adnssddeviceinfoSnapshot.Length == 0))
            {
                Console.Out.WriteLine("*** no TWAIN Local scanners ***");
                return (false);
            }

            // We didn't get a selection, so grab the first item...
            if ((a_aszCmd == null) || (a_aszCmd.Length < 2) || string.IsNullOrEmpty(a_aszCmd[1]))
            {
                m_dnssddeviceinfoSelected = m_adnssddeviceinfoSnapshot[0];
                return (false);
            }

            // Look for a match...
            foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in m_adnssddeviceinfoSnapshot)
            {
                // Check the name...
                if (!string.IsNullOrEmpty(dnssddeviceinfo.szLinkLocal) && dnssddeviceinfo.szLinkLocal.Contains(a_aszCmd[1]))
                {
                    m_dnssddeviceinfoSelected = dnssddeviceinfo;
                    break;
                }

                // Check the IPv4...
                else if (!string.IsNullOrEmpty(dnssddeviceinfo.szIpv4) && dnssddeviceinfo.szIpv4.Contains(a_aszCmd[1]))
                {
                    m_dnssddeviceinfoSelected = dnssddeviceinfo;
                    break;
                }

                // Check the note...
                else if (!string.IsNullOrEmpty(dnssddeviceinfo.szTxtNote) && dnssddeviceinfo.szTxtNote.Contains(a_aszCmd[1]))
                {
                    m_dnssddeviceinfoSelected = dnssddeviceinfo;
                    break;
                }
            }

            // Report the result...
            if (m_dnssddeviceinfoSelected != null)
            {
                Console.Out.WriteLine(m_dnssddeviceinfoSelected.szLinkLocal + " " + (!string.IsNullOrEmpty(m_dnssddeviceinfoSelected.szIpv4) ? m_dnssddeviceinfoSelected.szIpv4 : m_dnssddeviceinfoSelected.szIpv6) + " " + m_dnssddeviceinfoSelected.szTxtNote);
                m_twainlocalscanner = new TwainLocalScanner(null, 1, null, null, null);
            }
            else
            {
                Console.Out.WriteLine("*** no selection matches ***");
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Status of the program...
        /// </summary>
        /// <param name="a_aszCmd">tokenized command</param>
        /// <returns>true to quit</returns>
        private bool CmdStatus(string[] a_aszCmd)
        {
            // Current scanner...
            Console.Out.WriteLine("SELECTED SCANNER");
            if (m_dnssddeviceinfoSelected == null)
            {
                Console.Out.WriteLine("*** no selected scanner ***");
            }
            else
            {
                Console.Out.WriteLine(m_dnssddeviceinfoSelected.szLinkLocal + " " + (!string.IsNullOrEmpty(m_dnssddeviceinfoSelected.szIpv4) ? m_dnssddeviceinfoSelected.szIpv4 : m_dnssddeviceinfoSelected.szIpv6) + " " + m_dnssddeviceinfoSelected.szTxtNote);
            }

            // Current snapshot of scanners...
            Console.Out.WriteLine("");
            Console.Out.WriteLine("LAST SCANNER LIST SNAPSHOT");
            if ((m_adnssddeviceinfoSnapshot == null) || (m_adnssddeviceinfoSnapshot.Length == 0))
            {
                Console.Out.WriteLine("*** no TWAIN Local scanners ***");
            }
            else
            {
                foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in m_adnssddeviceinfoSnapshot)
                {
                    Console.Out.WriteLine(dnssddeviceinfo.szLinkLocal + " " + (!string.IsNullOrEmpty(dnssddeviceinfo.szIpv4) ? dnssddeviceinfo.szIpv4 : dnssddeviceinfo.szIpv6) + " " + dnssddeviceinfo.szTxtNote);
                }
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Display information about this apicmd object...
        /// </summary>
        /// <param name="a_apicmd">the object we want to display</param>
        private void DisplayApicmd
        (
            ApiCmd a_apicmd
        )
        {
            // Display what we sent...
            Console.Out.WriteLine("REQURI: " + a_apicmd.GetUriFull());
            string[] aszRequestHeaders = a_apicmd.GetRequestHeaders();
            if (aszRequestHeaders != null)
            {
                foreach (string sz in aszRequestHeaders)
                {
                    Console.Out.WriteLine("REQHDR: " + sz);
                }
            }
            Console.Out.WriteLine("REQDAT: " + a_apicmd.GetSendCommand());

            // Report the result...
            Console.Out.WriteLine("RSPSTS: " + a_apicmd.HttpStatus());
            string[] aszResponseHeaders = a_apicmd.GetResponseHeaders();
            if (aszResponseHeaders != null)
            {
                foreach (string sz in aszResponseHeaders)
                {
                    Console.Out.WriteLine("RSPHDR: " + sz);
                }
            }
            string szResponseData = a_apicmd.GetResponseData();
            if (!string.IsNullOrEmpty(szResponseData))
            {
                Console.Out.WriteLine("RSPDAT: " + szResponseData);
            }
        }

        #endregion


        // Private Methods (certification)
        #region Private Methods (certification)

        /// <summary>
        /// Run the TWAIN Certification tests.  
        /// </summary>
        private void TwainDirectCertification()
        {
            int ii;
            int iPass = 0;
            int iFail = 0;
            int iSkip = 0;
            int iTotal = 0;
            bool blSuccess;
            long lJsonErrorIndex;
            long lTaskIndex;
            string szCertificationFolder;
            string[] aszCategories;
            string[] aszTestFiles;
            string szTestData;
            string[] aszTestData;
            JsonLookup jsonlookupTest;
            JsonLookup jsonlookupReply;
            ApiCmd apicmd;

            // Find our cert stuff...
            szCertificationFolder = Path.Combine(Config.Get("writeFolder", ""), "tasks");
            szCertificationFolder = Path.Combine(szCertificationFolder, "certification");

            // Whoops...nothing to work with...
            if (!Directory.Exists(szCertificationFolder))
            {
                Console.Out.WriteLine("Cannot find certification folder:\n" + szCertificationFolder);
                return;
            }

            // Get the categories...
            aszCategories = Directory.GetDirectories(szCertificationFolder);
            if (aszCategories == null)
            {
                Console.Out.WriteLine("Cannot find any certification categories:\n" + szCertificationFolder);
                return;
            }

            // Loop the catagories...
            foreach (string szCategory in aszCategories)
            {
                // Get the tests...
                aszTestFiles = Directory.GetFiles(Path.Combine(szCertificationFolder, szCategory));
                if (aszTestFiles == null)
                {
                    continue;
                }

                // Loop the tests...
                foreach (string szTestFile in aszTestFiles)
                {
                    string szSummary;
                    string szStatus;

                    // Log it...
                    Log.Info("");
                    Log.Info("certification>>> file........................." + szTestFile);

                    // The total...
                    iTotal += 1;

                    // Add a new item to show what we're doing...
                    jsonlookupTest = new JsonLookup();

                    // Init stuff...
                    szSummary = Path.GetFileNameWithoutExtension(szTestFile);
                    szStatus = "skip";

                    // Load the test...
                    szTestData = File.ReadAllText(szTestFile);
                    if (string.IsNullOrEmpty(szTestData))
                    {
                        Log.Info("certification>>> status.......................skip (empty file)");
                        iSkip += 1;
                        continue;
                    }

                    // Split the data...
                    if (!szTestData.Contains("***DATADATADATA***"))
                    {
                        Log.Info("certification>>> status.......................skip (data error)");
                        iSkip += 1;
                        continue;
                    }
                    aszTestData = szTestData.Split(new string[] { "***DATADATADATA***\r\n", "***DATADATADATA***\n" }, StringSplitOptions.RemoveEmptyEntries);
                    if (aszTestData.Length != 2)
                    {
                        Log.Info("certification>>> status.......................skip (data error)");
                        iSkip += 1;
                        continue;
                    }

                    // Always start this part with a clean slate...
                    apicmd = new ApiCmd(m_dnssddeviceinfoSelected);

                    // Get our instructions...
                    blSuccess = jsonlookupTest.Load(aszTestData[0], out lJsonErrorIndex);
                    if (!blSuccess)
                    {
                        Log.Info("certification>>> status.......................skip (json error)");
                        iSkip += 1;
                        continue;
                    }

                    // Validate the instructions...
                    if (string.IsNullOrEmpty(jsonlookupTest.Get("category")))
                    {
                        Log.Info("certification>>> status.......................ERROR (missing category)");
                        iSkip += 1;
                        continue;
                    }
                    if (string.IsNullOrEmpty(jsonlookupTest.Get("summary")))
                    {
                        Log.Info("certification>>> status.......................skip (missing summary)");
                        iSkip += 1;
                        continue;
                    }
                    if (string.IsNullOrEmpty(jsonlookupTest.Get("description")))
                    {
                        Log.Info("certification>>> status.......................skip (missing description)");
                        iSkip += 1;
                        continue;
                    }
                    if (string.IsNullOrEmpty(jsonlookupTest.Get("expects")))
                    {
                        Log.Info("certification>>> status.......................skip (missing expects)");
                        iSkip += 1;
                        continue;
                    }

                    // Log what we're doing...
                    Log.Info("certification>>> summary......................" + jsonlookupTest.Get("summary"));
                    Log.Info("certification>>> description.................." + jsonlookupTest.Get("description"));
                    for (ii = 0; ; ii++)
                    {
                        string szExpects = "expects[" + ii + "]";
                        if (string.IsNullOrEmpty(jsonlookupTest.Get(szExpects, false)))
                        {
                            break;
                        }
                        Log.Info("certification>>> " + szExpects + ".success..........." + jsonlookupTest.Get(szExpects + ".success"));
                        if (jsonlookupTest.Get(szExpects + ".success") == "false")
                        {
                            Log.Info("certification>>> " + szExpects + ".code.............." + jsonlookupTest.Get(szExpects + ".code"));
                            if (jsonlookupTest.Get(szExpects + ".code") == "invalidJson")
                            {
                                Log.Info("certification>>> " + szExpects + ".characterOffset..." + jsonlookupTest.Get(szExpects + ".characterOffset"));
                            }
                            if (jsonlookupTest.Get(szExpects + ".code") == "invalidValue")
                            {
                                Log.Info("certification>>> " + szExpects + ".jsonKey..........." + jsonlookupTest.Get(szExpects + ".jsonKey"));
                            }
                        }
                    }

                    // Make sure the last item is showing, and then show it...
                    szSummary = jsonlookupTest.Get("summary");
                    szStatus = "(running)";

                    // Perform the test...
                    blSuccess = m_twainlocalscanner.ClientScannerSendTask(aszTestData[1], ref apicmd);
                    if (!blSuccess)
                    {
                        //mlmtbd Add errror check...
                    }

                    // Figure out the index offset to the task, so that we don't
                    // have to dink with the certification tests if the API is
                    // changed for any reason.  Note that we're assuming that the
                    // API is packed...
                    string szSendCommand = apicmd.GetSendCommand();
                    lTaskIndex = (szSendCommand.IndexOf("\"task\":") + 7);

                    // Check out the reply...
                    string szHttpReplyData = apicmd.HttpResponseData();
                    jsonlookupReply = new JsonLookup();
                    blSuccess = jsonlookupReply.Load(szHttpReplyData, out lJsonErrorIndex);
                    if (!blSuccess)
                    {
                        Log.Info("certification>>> status.......................fail (json error)");
                        szStatus = "fail";
                        iFail += 1;
                        continue;
                    }

                    // Check for a task...
                    szHttpReplyData = jsonlookupReply.Get("results.session.task");
                    if (!string.IsNullOrEmpty(szHttpReplyData))
                    {
                        jsonlookupReply = new JsonLookup();
                        blSuccess = jsonlookupReply.Load(szHttpReplyData, out lJsonErrorIndex);
                        if (!blSuccess)
                        {
                            Log.Info("certification>>> status.......................fail (json error)");
                            szStatus = "fail";
                            iFail += 1;
                            continue;
                        }
                    }

                    // Loopy...
                    for (ii = 0; ; ii++)
                    {
                        // Make sure we have this entry...
                        string szExpects = "expects[" + ii + "]";
                        if (string.IsNullOrEmpty(jsonlookupTest.Get(szExpects, false)))
                        {
                            break;
                        }

                        // We need to bump the total for values of ii > 0, this handles
                        // tasks with multiple actions...
                        if (ii > 0)
                        {
                            iTotal += 1;
                        }

                        // We need the path to the results...
                        string szPath = jsonlookupTest.Get(szExpects + ".path");
                        if (string.IsNullOrEmpty(szPath))
                        {
                            szPath = "";
                        }
                        else
                        {
                            szPath += ".";
                        }

                        // The command is expected to succeed...
                        if (jsonlookupTest.Get(szExpects + ".success") == "true")
                        {
                            // Check success...
                            if (string.IsNullOrEmpty(jsonlookupReply.Get(szPath + "results.success")))
                            {
                                Log.Info("certification>>> status.......................fail (missing " + szPath + "results.success)");
                                szStatus = "fail (missing " + szPath + "results.success)";
                                iFail += 1;
                            }
                            else if (jsonlookupReply.Get(szPath + "results.success") != "true")
                            {
                                Log.Info("certification>>> status.......................fail (expected " + szPath + "results.success to be 'true')");
                                szStatus = "fail (expected " + szPath + "results.success to be 'true')";
                                iFail += 1;
                            }
                            else
                            {
                                Log.Info("certification>>> status.......................pass");
                                szStatus = "pass";
                                 iPass += 1;
                            }
                        }

                        // The command is expected to fail...
                        else if (jsonlookupTest.Get(szExpects + ".success") == "false")
                        {
                            // Check success...
                            if (string.IsNullOrEmpty(jsonlookupReply.Get(szPath + "results.success")))
                            {
                                Log.Info("certification>>> status.......................fail (missing " + szPath + "results.success)");
                                szStatus = "fail (missing " + szPath + "results.success)";
                                iFail += 1;
                            }
                            else if (jsonlookupReply.Get(szPath + "results.success") != "false")
                            {
                                Log.Info("certification>>> status.......................fail (expected " + szPath + "results.success to be 'false')");
                                szStatus = "fail (expected " + szPath + "results.success to be 'false')";
                                iFail += 1;
                            }

                            // Check the code...
                            else
                            {
                                switch (jsonlookupTest.Get(szExpects + ".code"))
                                {
                                    // Tell the programmer to fix their code or their tests...  :)
                                    default:
                                        Log.Info("certification>>> status.......................fail (no handler for this code '" + jsonlookupTest.Get(szExpects + ".code") + "')");
                                        iFail += 1;
                                        break;

                                    // JSON violations...
                                    case "invalidJson":
                                        if (string.IsNullOrEmpty(jsonlookupReply.Get(szPath + "results.code")))
                                        {
                                            Log.Info("certification>>> status.......................fail (missing " + szPath + "results.code)");
                                            szStatus = "fail (missing " + szPath + "results.code)";
                                            iFail += 1;
                                        }
                                        else if (jsonlookupReply.Get(szPath + "results.code") == "invalidJson")
                                        {
                                            if (string.IsNullOrEmpty(jsonlookupTest.Get(szExpects + ".characterOffset")))
                                            {
                                                Log.Info("certification>>> status.......................fail (missing " + szExpects + ".characterOffset)");
                                                szStatus = "fail (missing " + szExpects + ".characterOffset)";
                                                iFail += 1;
                                            }
                                            else if (int.Parse(jsonlookupTest.Get(szExpects + ".characterOffset")) == (int.Parse(jsonlookupReply.Get(szPath + "results.characterOffset")) - lTaskIndex))
                                            {
                                                Log.Info("certification>>> status.......................pass");
                                                szStatus = "pass";
                                                iPass += 1;
                                            }
                                            else
                                            {
                                                Log.Info("certification>>> status.......................fail (" + szExpects + ".characterOffset wanted:" + jsonlookupTest.Get(szExpects + ".characterOffset") + " got:" + (int.Parse(jsonlookupReply.Get(szPath + "results.characterOffset")) - lTaskIndex).ToString() + ")");
                                                szStatus = "fail (" + szExpects + ".characterOffset wanted:" + jsonlookupTest.Get(szExpects + ".characterOffset") + " got:" + (int.Parse(jsonlookupReply.Get(szPath + "results.characterOffset")) - lTaskIndex).ToString() + ")";
                                                iFail += 1;
                                            }
                                        }
                                        else
                                        {
                                            Log.Info("certification>>> status.......................fail (" + szExpects + ".code wanted:" + jsonlookupTest.Get(szExpects + ".code") + " got:" + jsonlookupReply.Get(szPath + "results.code") + ")");
                                            szStatus = "fail (" + szExpects + ".code wanted:" + jsonlookupTest.Get(szExpects + ".code") + " got:" + jsonlookupReply.Get(szPath + "results.code") + "')";
                                            iFail += 1;
                                        }
                                        break;

                                    // TWAIN Direct violations...
                                    case "invalidTask":
                                        if (string.IsNullOrEmpty(jsonlookupReply.Get(szPath + "results.code")))
                                        {
                                            Log.Info("certification>>> status.......................fail (missing " + szPath + "results.code)");
                                            szStatus = "fail (missing " + szPath + "results.code)";
                                            iFail += 1;
                                        }
                                        else if (jsonlookupReply.Get(szPath + "results.code") == "invalidTask")
                                        {
                                            if (string.IsNullOrEmpty(jsonlookupTest.Get(szExpects + ".jsonKey")))
                                            {
                                                Log.Info("certification>>> status.......................fail (missing " + szExpects + ".jsonKey)");
                                                szStatus = "fail (missing " + szExpects + "jsonKey)";
                                                iFail += 1;
                                            }
                                            else if (jsonlookupTest.Get(szExpects + ".jsonKey") == jsonlookupReply.Get(szPath + "results.jsonKey"))
                                            {
                                                Log.Info("certification>>> status.......................pass");
                                                szStatus = "pass";
                                                iPass += 1;
                                            }
                                            else
                                            {
                                                Log.Info("certification>>> status.......................fail (" + szExpects + ".jsonKey wanted:" + jsonlookupTest.Get(szExpects + ".jsonKey") + " got:" + jsonlookupReply.Get(szPath + "results.jsonKey"));
                                                szStatus = "fail (" + szExpects + ".jsonKey wanted:" + jsonlookupTest.Get(szExpects + ".jsonKey") + " got:" + jsonlookupReply.Get(szPath + "results.jsonKey");
                                                iFail += 1;
                                            }
                                        }
                                        else
                                        {
                                            Log.Info("certification>>> status.......................fail (" + szExpects + ".code wanted:" + jsonlookupTest.Get(szExpects + ".code") + " got:" + jsonlookupReply.Get(szPath + "results.code") + ")");
                                            szStatus = "fail (" + szExpects + ".code wanted:" + jsonlookupTest.Get(szExpects + ".code") + " got:" + jsonlookupReply.Get(szPath + "results.code") + "')";
                                            iFail += 1;
                                        }
                                        break;

                                    // invalidValue forced by exception...
                                    case "invalidValue":
                                        if (string.IsNullOrEmpty(jsonlookupReply.Get(szPath + "results.code")))
                                        {
                                            Log.Info("certification>>> status.......................fail (missing " + szPath + "results.code)");
                                            szStatus = "fail (missing " + szPath + "results.code)";
                                            iFail += 1;
                                        }
                                        else if (jsonlookupReply.Get(szPath + "results.code") == "invalidValue")
                                        {
                                            if (string.IsNullOrEmpty(jsonlookupTest.Get(szExpects + ".jsonKey")))
                                            {
                                                Log.Info("certification>>> status........................fail (missing " + szExpects + ".jsonKey)");
                                                szStatus = "fail (missing " + szExpects + ".jsonKey)";
                                                iFail += 1;
                                            }
                                            else if (jsonlookupTest.Get(szExpects + ".jsonKey") == jsonlookupReply.Get(szPath + "results.jsonKey"))
                                            {
                                                Log.Info("certification>>> status.......................pass");
                                                szStatus = "pass";
                                                iPass += 1;
                                            }
                                            else
                                            {
                                                Log.Info("certification>>> status.......................fail (" + szExpects + ".jsonKey wanted:" + jsonlookupTest.Get(szExpects + ".jsonKey") + " got:" + jsonlookupReply.Get(szPath + "results.jsonKey"));
                                                szStatus = "fail (" + szExpects + ".jsonKey wanted:" + jsonlookupTest.Get(szExpects + ".jsonKey") + " got:" + jsonlookupReply.Get(szPath + "results.jsonKey");
                                                iFail += 1;
                                            }
                                        }
                                        else
                                        {
                                            Log.Info("certification>>> status.......................fail (" + szExpects + ".code wanted:" + jsonlookupTest.Get(szExpects + ".code") + " got:" + jsonlookupReply.Get(szPath + "results.code") + ")");
                                            szStatus = "fail (" + szExpects + ".code wanted:" + jsonlookupTest.Get(szExpects + ".code") + " got:" + jsonlookupReply.Get(szPath + "results.code") + "')";
                                            iFail += 1;
                                        }
                                        break;
                                }
                            }
                        }

                        // Oops...
                        else
                        {
                            Log.Info("certification>>> status.......................fail (expectedSuccess must be 'true' or 'false')");
                            szStatus = "fail";
                            iFail += 1;
                        }
                    }
                }
            }

            // Pass count...
            Log.Info("certification>>> PASS: " + iPass);

            // Fail count...
            Log.Info("certification>>> FAIL: " + iFail);

            // Skip count...
            Log.Info("certification>>> SKIP: " + iSkip);

            // Total count...
            Log.Info("certification>>> TOTAL: " + iTotal);
        }

        #endregion


        // Private Attributes
        #region Private Attributes

        /// <summary>
        /// Map commands to functions...
        /// </summary>
        private List<Interpreter.DispatchTable> m_ldispatchtable;

        /// <summary>
        /// A snapshot of the current available devices...
        /// </summary>
        private Dnssd.DnssdDeviceInfo[] m_adnssddeviceinfoSnapshot;

        /// <summary>
        /// Information about our device...
        /// </summary>
        private Dnssd.DnssdDeviceInfo m_dnssddeviceinfoSelected;

        /// <summary>
        /// The connection to our device...
        /// </summary>
        private TwainLocalScanner m_twainlocalscanner;

        /// <summary>
        /// Our object for discovering TWAIN Local scanners...
        /// </summary>
        private Dnssd m_dnssd;

        // No output...
        private bool m_blSilent;

        #endregion
    }
}
