///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirect.Certification.Program
//
//  Our entry point.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    01-Jun-2017     Initial Release
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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using TwainDirect.Support;

namespace TwainDirect.Certification
{
    /// <summary>
    /// The certification object that we'll use to test and exercise functions
    /// for TWAIN Direct.
    /// </summary>
    internal sealed class Terminal : IDisposable
    {
        // Public Methods
        #region Public Methods

        /// <summary>
        /// Initialize stuff...
        /// </summary>
        public Terminal()
        {
            // Make sure we have a console...
            m_streamreaderConsole = Interpreter.CreateConsole();

            // Init stuff...
            m_blSilent = false;
            m_blSilentEvents = false;
            m_adnssddeviceinfoSnapshot = null;
            m_dnssddeviceinfoSelected = null;
            m_twainlocalscannerclient = null;
            m_lkeyvalue = new List<KeyValue>();
            m_objectKeyValue = new object();
            m_transactionLast = null;
            m_lcallstack = new List<CallStack>();

            // Set up the base stack with the program arguments, we know
            // this is the base stack for two reasons: first, it has no
            // script, and second, it's first... :)
            CallStack callstack = default(CallStack);
            callstack.functionarguments.aszCmd = Config.GetCommandLine();
            m_lcallstack.Add(callstack);

            // Create the mdns monitor, and start it...
            m_dnssd = new Dnssd(Dnssd.Reason.Monitor);
            m_dnssd.MonitorStart(null, IntPtr.Zero);

            // Build our command table...
            m_ldispatchtable = new List<Interpreter.DispatchTable>();

            // Discovery and Selection...
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdHelp,                         new string[] { "help", "?" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdList,                         new string[] { "list" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdQuit,                         new string[] { "ex", "exit", "q", "quit" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdSelect,                       new string[] { "select" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdStatus,                       new string[] { "status" }));

            // Api commands...
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiClosesession,              new string[] { "close", "closesession", "closeSession" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiCreatesession,             new string[] { "create", "createsession", "createSession" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiGetsession,                new string[] { "get", "getsession", "getSession" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiInfo,                      new string[] { "info" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiInfoex,                    new string[] { "infoex" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiInvalidcommand,            new string[] { "invalidcommand", "invalidCommand" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiInvaliduri,                new string[] { "invaliduri", "invalidUri" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiReadimageblockmetadata,    new string[] { "readimageblockmetadata", "readImageBlockMetadata" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiReadimageblock,            new string[] { "readimageblock", "readImageBlock" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiReleaseimageblocks,        new string[] { "release", "releaseimageblocks", "releaseImageBlocks" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiSendtask,                  new string[] { "send", "sendtask", "sendTask" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiStartcapturing,            new string[] { "start", "startcapturing", "startCapturing" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiStopcapturing,             new string[] { "stop", "stopcapturing", "stopCapturing" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiWaitforevents,             new string[] { "wait", "waitforevents", "waitForEvents" }));

            // Scripting...
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdCall,                         new string[] { "call" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdCd,                           new string[] { "cd", "pwd" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdCheckpdfraster,               new string[] { "checkpdfraster" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdClean,                        new string[] { "clean" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdDir,                          new string[] { "dir", "ls" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdEcho,                         new string[] { "echo" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdEchoBlue,                     new string[] { "echo.blue" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdEchoGreen,                    new string[] { "echo.green" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdEchoRed,                      new string[] { "echo.red" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdEchoYellow,                   new string[] { "echo.yellow" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdEchopassfail,                 new string[] { "echopassfail" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdFinishImage,                  new string[] { "finishimage" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdGc,                           new string[] { "gc" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdGoto,                         new string[] { "goto" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdIf,                           new string[] { "if" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdIncrement,                    new string[] { "increment" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdInput,                        new string[] { "input" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdJson2Xml,                     new string[] { "json2xml" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdLog,                          new string[] { "log" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdReturn,                       new string[] { "return" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdRun,                          new string[] { "run" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdRunv,                         new string[] { "runv" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdSet,                          new string[] { "set" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdSleep,                        new string[] { "sleep" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdTwainlocalsession,            new string[] { "twainlocalsession" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdWaitForSessionUpdate,         new string[] { "waitforsessionupdate" }));

            // Say hi...
            Assembly assembly = typeof(Terminal).Assembly;
            AssemblyName assemblyname = assembly.GetName();
            Version version = assemblyname.Version;
            DateTime datetime = new DateTime(2000, 1, 1).AddDays(version.Build).AddSeconds(version.MinorRevision * 2);
            m_szBanner = "TWAIN Direct Certification v" + version.Major + "." + version.Minor + " " + datetime.Day + "-" + datetime.ToString("MMM") + "-" + datetime.Year + " " + ((IntPtr.Size == 4) ? "(32-bit)" : "(64-bit)");
            Display(m_szBanner);
            Display("Enter \"help\" for more info.");
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
        /// Run the certification tool...
        /// </summary>
        public void Run()
        {
            string szPrompt = "tdc";
            Interpreter interpreter = new Interpreter(szPrompt + ">>> ");

            // Run until told to stop...
            while (true)
            {
                bool blDone;
                string szCmd;
                string[] aszCmd;

                // Prompt...
                szCmd = interpreter.Prompt(m_streamreaderConsole);

                // Tokenize...
                aszCmd = interpreter.Tokenize(szCmd);

                // Expansion of symbols...
                Expansion(ref aszCmd);

                // Dispatch...
                Interpreter.FunctionArguments functionarguments = default(Interpreter.FunctionArguments);
                functionarguments.aszCmd = aszCmd;
                functionarguments.transaction = m_transactionLast;
                blDone = interpreter.Dispatch(ref functionarguments, m_ldispatchtable);
                if (blDone)
                {
                    return;
                }
                m_transactionLast = functionarguments.transaction;

                // Update the prompt with state information...
                if (m_twainlocalscannerclient == null)
                {
                    interpreter.SetPrompt(szPrompt + ">>> ");
                }
                else
                {
                    switch (m_twainlocalscannerclient.GetState())
                    {
                        default: interpreter.SetPrompt(szPrompt + "." + m_twainlocalscannerclient.GetState() + ">>> "); break;
                        case "noSession": interpreter.SetPrompt(szPrompt + ">>> "); break;
                        case "ready": interpreter.SetPrompt(szPrompt + ".rdy>>> "); break;
                        case "capturing": interpreter.SetPrompt(szPrompt + ".cap>>> "); break;
                        case "draining": interpreter.SetPrompt(szPrompt + ".drn>>> "); break;
                        case "closed": interpreter.SetPrompt(szPrompt + ".cls>>> "); break;
                    }
                }
            }
        }

        #endregion


        // Private Methods (api)
        #region Private Methods (api)

        /// <summary>
        /// Close a session...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiClosesession(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscannerclient.ClientScannerCloseSession(ref apicmd);

            // Squirrel away the transaction...
            a_functionarguments.transaction = apicmd.GetTransaction();

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Create a session...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiCreatesession(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscannerclient.ClientScannerCreateSession(ref apicmd);

            // Squirrel away the transaction...
            a_functionarguments.transaction = apicmd.GetTransaction();

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Get the current session object
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiGetsession(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscannerclient.ClientScannerGetSession(ref apicmd);

            // Squirrel away the transaction...
            a_functionarguments.transaction = apicmd.GetTransaction();

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Send an info command to the selected scanner...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiInfo(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscannerclient.ClientInfo(ref apicmd, "info");

            // Squirrel away the transaction...
            a_functionarguments.transaction = apicmd.GetTransaction();

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Send an infoex command to the selected scanner...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiInfoex(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscannerclient.ClientInfo(ref apicmd, "infoex");

            // Squirrel away the transaction...
            a_functionarguments.transaction = apicmd.GetTransaction();

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Send an invalid command to the selected scanner...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiInvalidcommand(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscannerclient.ClientScannerInvalidCommand(ref apicmd);

            // Squirrel away the transaction...
            a_functionarguments.transaction = apicmd.GetTransaction();

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Send an invalid uri to the selected scanner...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiInvaliduri(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscannerclient.ClientScannerInvalidUri(ref apicmd);

            // Squirrel away the transaction...
            a_functionarguments.transaction = apicmd.GetTransaction();

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Read an image data block's metadata and thumbnail...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiReadimageblockmetadata(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;
            long lImageBlock;
            bool blGetThumbnail;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }
            if (a_functionarguments.aszCmd.Length < 3)
            {
                DisplayError("please specify image block to read and thumbnail flag...");
                return (false);
            }

            // Get the image block number...
            if (!long.TryParse(a_functionarguments.aszCmd[1], out lImageBlock))
            {
                DisplayError("image block must be a number...");
                return (false);
            }
            if (a_functionarguments.aszCmd[2].ToLower() == "true")
            {
                blGetThumbnail = true;
            }
            else if (a_functionarguments.aszCmd[2].ToLower() == "false")
            {
                blGetThumbnail = false;
            }
            else
            {
                DisplayError("thumbnail flag must be true or false...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscannerclient.ClientScannerReadImageBlockMetadata(lImageBlock, blGetThumbnail, ref apicmd);

            // Squirrel away the transaction...
            a_functionarguments.transaction = apicmd.GetTransaction();

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Read an image data block and its metadata...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiReadimageblock(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;
            long lImageBlock;
            bool blGetMetadataWithImage;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }
            if (a_functionarguments.aszCmd.Length < 3)
            {
                DisplayError("please specify image block to read and thumbnail flag...");
                return (false);
            }

            // Get the image block number...
            if (!long.TryParse(a_functionarguments.aszCmd[1], out lImageBlock))
            {
                DisplayError("image block must be a number...");
                return (false);
            }
            if (a_functionarguments.aszCmd[2].ToLower() == "true")
            {
                blGetMetadataWithImage = true;
            }
            else if (a_functionarguments.aszCmd[2].ToLower() == "false")
            {
                blGetMetadataWithImage = false;
            }
            else
            {
                DisplayError("getmetdata flag must be true or false...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscannerclient.ClientScannerReadImageBlock(lImageBlock, blGetMetadataWithImage, null, ref apicmd);

            // Squirrel away the transaction...
            a_functionarguments.transaction = apicmd.GetTransaction();

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Release or or more image blocks, or all image blocks...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiReleaseimageblocks(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;
            long lFirstImageBlock;
            long lLastImageBlock;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }
            if (a_functionarguments.aszCmd.Length < 3)
            {
                DisplayError("please specify the first and last image block to release...");
                return (false);
            }

            // Get the values...
            if (!long.TryParse(a_functionarguments.aszCmd[1], out lFirstImageBlock))
            {
                DisplayError("first image block must be a number...");
                return (false);
            }
            if (!long.TryParse(a_functionarguments.aszCmd[2], out lLastImageBlock))
            {
                DisplayError("last image block must be a number...");
                return (false);
            }

            // Loop so we can handle the release-all scenerio...
            while (true)
            {
                // Make the call...
                apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
                m_twainlocalscannerclient.ClientScannerReleaseImageBlocks(lFirstImageBlock, lLastImageBlock, ref apicmd);

                // Squirrel away the transaction...
                a_functionarguments.transaction = apicmd.GetTransaction();

                // Scoot...
                if ((lFirstImageBlock != 1) || (lLastImageBlock != int.MaxValue))
                {
                    break;
                }

                // Otherwise, we'll only scoot if we're out of images, we
                // must be in a draining state for this to be allowed...
                if (apicmd.GetSessionState() != "draining")
                {
                    break;
                }

                // If the flag says we're done, then we're done...
                if (apicmd.GetImageBlocksDrained())
                {
                    break;
                }

                // Wait a little before beating up the scanner with another attempt...
                Thread.Sleep(1000);
            }

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Send task...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiSendtask(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;
            string szTask;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }

            // Must supply a task...
            if ((a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[1] == null))
            {
                DisplayError("must supply a task...");
                return (false);
            }

            // Is the argument a file?
            if (File.Exists(a_functionarguments.aszCmd[1]))
            {
                try
                {
                    szTask = File.ReadAllText(a_functionarguments.aszCmd[1]);
                }
                catch (Exception exception)
                {
                    DisplayError("failed to open file...<" + a_functionarguments.aszCmd[1] + "> - " + exception.Message);
                    return (false);
                }
            }
            else
            {
                szTask = a_functionarguments.aszCmd[1];
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscannerclient.ClientScannerSendTask(szTask, ref apicmd);

            // Squirrel away the transaction...
            a_functionarguments.transaction = apicmd.GetTransaction();

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Start capturing...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiStartcapturing(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscannerclient.ClientScannerStartCapturing(ref apicmd);

            // Squirrel away the transaction...
            a_functionarguments.transaction = apicmd.GetTransaction();

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Stop capturing...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiStopcapturing(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscannerclient.ClientScannerStopCapturing(ref apicmd);

            // Squirrel away the transaction...
            a_functionarguments.transaction = apicmd.GetTransaction();

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Wait for events, like changes to the session object...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiWaitforevents(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscannerclient == null))
            {
                DisplayError("must first select a scanner...");
                return (false);
            }

            // If we have arguments, it'll be the script to run, along with its arguments...
            m_aszWaitForEventsCallback = null;
            if ((a_functionarguments.aszCmd != null) && (a_functionarguments.aszCmd.Length > 1))
            {
                m_aszWaitForEventsCallback = a_functionarguments.aszCmd;
            }

            // Make the call, this is where we register the callback we
            // want to fire when events show up...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected, WaitForEventsCallbackLaunchpad, this);
            m_twainlocalscannerclient.ClientScannerWaitForEvents(ref apicmd);

            // Squirrel away the partial transaction (we usually won't have a reply)...
            a_functionarguments.transaction = apicmd.GetTransaction();

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        #endregion


        // Private Methods (commands)
        #region Private Methods (commands)

        /// <summary>
        /// Call a function...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdCall(ref Interpreter.FunctionArguments a_functionarguments)
        {
            int iLine;
            string szLabel;

            // Validate...
            if (    (a_functionarguments.aszScript == null)
                ||  (a_functionarguments.aszScript.Length < 2)
                ||  (a_functionarguments.aszScript[0] == null)
                ||  (a_functionarguments.aszCmd == null)
                ||  (a_functionarguments.aszCmd.Length < 2)
                ||  (a_functionarguments.aszCmd[1] == null))
            {
                return (false);
            }

            // Search for a match...
            szLabel = ":" + a_functionarguments.aszCmd[1];
            for (iLine = 0; iLine < a_functionarguments.aszScript.Length; iLine++)
            {
                if (a_functionarguments.aszScript[iLine].Trim() == szLabel)
                {
                    // We need this to go to the function...
                    a_functionarguments.blGotoLabel = true;
                    a_functionarguments.iLabelLine = iLine;

                    // We need this to get back...
                    CallStack callstack = default(CallStack);
                    callstack.functionarguments = a_functionarguments;
                    m_lcallstack.Add(callstack);
                    return (false);
                }
            }

            // Ugh...
            DisplayError("function label not found: <" + szLabel + ">");
            return (false);
        }

        /// <summary>
        /// Show or set the current directory...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdCd(ref Interpreter.FunctionArguments a_functionarguments)
        {
            // No data...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[1] == null) || (a_functionarguments.aszCmd[0].ToLowerInvariant() == "pwd"))
            {
                Display(Directory.GetCurrentDirectory(), true);
                return (false);
            }

            // Set the current directory...
            try
            {
                Directory.SetCurrentDirectory(a_functionarguments.aszCmd[1]);
            }
            catch (Exception exception)
            {
                DisplayError("cd failed - " + exception.Message);
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Check all of the PDF/raster files in the images folder to
        /// make sure they can be read (confirming that they're valid)...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private enum CHECKPDFRASTERRESULT { fail, skip, pass }
        private bool CmdCheckpdfraster(ref Interpreter.FunctionArguments a_functionarguments)
        {
            bool blSuccess;
            string szError = "";
            string szImagesFolder = "";
            CHECKPDFRASTERRESULT checkpdfrasterresult;

            // The default is to use our images folder...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[1] == null))
            {
                szImagesFolder = Path.Combine(Config.Get("writeFolder", null), "images");
            }
            // The user can overrride this...
            else
            {
                szImagesFolder = a_functionarguments.aszCmd[1];
            }

            // If we don't have an images folder, then we didn't pass
            // but we also didn't necessarily fail, so mark it as a
            // skip...
            if (!Directory.Exists(szImagesFolder))
            {
                SetReturnValue(CHECKPDFRASTERRESULT.skip.ToString());
                return (false);
            }

            // Give us some cover...
            checkpdfrasterresult = CHECKPDFRASTERRESULT.skip;
            try
            {
                // Get the PDF files, and walk through them...
                DirectoryInfo directoryinfo = new DirectoryInfo(szImagesFolder);
                foreach (System.IO.FileInfo file in directoryinfo.GetFiles("*.pdf"))
                {
                    blSuccess = PdfRaster.ValidPdfRaster(file.FullName, out szError);
                    if (blSuccess)
                    {
                        // Only keep marking as pass if we've not seen a fail...
                        if (checkpdfrasterresult > CHECKPDFRASTERRESULT.fail)
                        {
                            checkpdfrasterresult = CHECKPDFRASTERRESULT.pass;
                        }
                    }
                    else
                    {
                        // Oh well...
                        DisplayError("error in <" + file.FullName + "> - " + szError);
                        checkpdfrasterresult = CHECKPDFRASTERRESULT.fail;
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayError("error while examining <" + szImagesFolder + "> - " + exception.Message);
                SetReturnValue(CHECKPDFRASTERRESULT.fail.ToString());
                return (false);
            }

            // Report what happened, skip if we didn't find any PDF files,
            // fail if at least one of them failed, pass if they all passed...
            SetReturnValue(checkpdfrasterresult.ToString());
            return (false);
        }

        /// <summary>
        /// Clean the images folder...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdClean(ref Interpreter.FunctionArguments a_functionarguments)
        {
            // The images folder...
            string szImagesFolder = Path.Combine(Config.Get("writeFolder", null), "images");

            // Delete the images folder...
            if (Directory.Exists(szImagesFolder))
            {
                try
                {
                    DirectoryInfo directoryinfo = new DirectoryInfo(szImagesFolder);
                    foreach (System.IO.FileInfo file in directoryinfo.GetFiles()) file.Delete();
                    foreach (System.IO.DirectoryInfo subDirectory in directoryinfo.GetDirectories()) subDirectory.Delete(true);
                }
                catch (Exception exception)
                {
                    DisplayError("couldn't delete <" + szImagesFolder + "> - " + exception.Message);
                    return (false);
                }
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Lists the files and folders in the current directory...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdDir(ref Interpreter.FunctionArguments a_functionarguments)
        {
            // Get the folders...
            string[] aszFolders = Directory.GetDirectories(".");
            if ((aszFolders != null) && (aszFolders.Length > 0))
            {
                Array.Sort(aszFolders);
                foreach (string sz in aszFolders)
                {
                    Display(sz.Replace(".\\","").Replace("./","") + Path.DirectorySeparatorChar);
                }
            }

            // Get the files...
            string[] aszFiles = Directory.GetFiles(".");
            if ((aszFiles != null) && (aszFiles.Length > 0))
            {
                Array.Sort(aszFiles);
                foreach (string sz in aszFiles)
                {
                    Display(sz.Replace(".\\", "").Replace("./", ""));
                }
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Echo text...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdEcho(ref Interpreter.FunctionArguments a_functionarguments)
        {
            return (CmdEchoColor(ref a_functionarguments, ConsoleColor.White));
        }

        /// <summary>
        /// Echo text as blue...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdEchoBlue(ref Interpreter.FunctionArguments a_functionarguments)
        {
            return (CmdEchoColor(ref a_functionarguments, ConsoleColor.Blue));
        }

        /// <summary>
        /// Echo text as green...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdEchoGreen(ref Interpreter.FunctionArguments a_functionarguments)
        {
            return (CmdEchoColor(ref a_functionarguments, ConsoleColor.Green));
        }

        /// <summary>
        /// Echo text as red...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdEchoRed(ref Interpreter.FunctionArguments a_functionarguments)
        {
            return (CmdEchoColor(ref a_functionarguments, ConsoleColor.Red));
        }

        /// <summary>
        /// Echo text as yellow...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdEchoYellow(ref Interpreter.FunctionArguments a_functionarguments)
        {
            return (CmdEchoColor(ref a_functionarguments, ConsoleColor.Yellow));
        }

        /// <summary>
        /// Echo text as white...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdEchoColor(ref Interpreter.FunctionArguments a_functionarguments, ConsoleColor a_consolecolor)
        {
            int ii;
            string szLine = "";
            string[] aszCmd;

            // No data...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[0] == null))
            {
                Display("", true);
                return (false);
            }

            // Copy the array...
            aszCmd = new string[a_functionarguments.aszCmd.Length];
            Array.Copy(a_functionarguments.aszCmd, aszCmd, a_functionarguments.aszCmd.Length);

            // Expand the symbols...
            Expansion(ref aszCmd);

            // Turn it into a line...
            for (ii = 1; ii < aszCmd.Length; ii++)
            {
                szLine += ((szLine == "") ? "" : " ") + aszCmd[ii];
            }

            // Spit it out...
            switch (a_consolecolor)
            {
                default:
                    Display(szLine, true);
                    break;

                case ConsoleColor.Blue:
                    DisplayBlue(szLine, true);
                    break;

                case ConsoleColor.Green:
                    DisplayGreen(szLine, true);
                    break;

                case ConsoleColor.Red:
                    DisplayRed(szLine, true);
                    break;

                case ConsoleColor.Yellow:
                    DisplayYellow(szLine, true);
                    break;
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Display a pass/fail message...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdEchopassfail(ref Interpreter.FunctionArguments a_functionarguments)
        {
            string szLine;
            string szDots = "..........................................................................................................";

            // No data...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 3) || (a_functionarguments.aszCmd[0] == null))
            {
                DisplayError("echopassfail needs two arguments...");
                return (false);
            }

            // Build the string...
            szLine = a_functionarguments.aszCmd[1];
            if ((szDots.Length - szLine.Length) > 0)
            {
                szLine += szDots.Substring(0, szDots.Length - szLine.Length);
            }
            else
            {
                szLine += "...";
            }
            szLine += a_functionarguments.aszCmd[2];

            // Spit it out...
            if (a_functionarguments.aszCmd[2].Contains("fail"))
            {
                DisplayRed(szLine, true);
            }
            else
            {
                Display(szLine, true);
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Finishing an image involves examining the metadata we've
        /// collected looking for sequences of morePartsInFile followed
        /// by a lastPartInFile.  When we've identified one of these
        /// we stitch the corresponding .tdpdf files into a single .pdf
        /// that represents the complete captured image.  The intermediate
        /// .tdmeta and .tdpdf files are removed.  Note that this does not
        /// affect the way the imageBlocks work.  Those are tracked in
        /// the scanner.
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdFinishImage(ref Interpreter.FunctionArguments a_functionarguments)
        {
            int iImageBlock;
            string szBasename;
            string szImagesFolder;
            string szFinishedImageBasename;

            // Validate...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[1] == null))
            {
                Display("Please provide an imageBlock number", true);
                return (false);
            }
            if (!int.TryParse(a_functionarguments.aszCmd[1], out iImageBlock))
            {
                Display("Please provide an imageBlock number", true);
                return (false);
            }

            // The images folder...
            szImagesFolder = Path.Combine(Config.Get("writeFolder", null), "images");

            // The basename for this image block...
            szBasename = Path.Combine(szImagesFolder, "img" + iImageBlock.ToString("D6"));

            // This function creates a finished image, metadata, and thumbnail
            // from the imageBlocks, and gives us the basename to it...
            if (!m_twainlocalscannerclient.ClientFinishImage(szBasename, out szFinishedImageBasename))
            {
                // We don't have a complete image, so scoot...
                SetReturnValue("skip");
                return (false);
            }

            // Return the base path to the new image, adding a .meta, a
            // .pdf, or a _thumbnail.pdf will get the various files...
            SetReturnValue(szFinishedImageBasename);

            // All done...
            return (false);
        }

        /// <summary>
        /// Garbage collection, used to freak out the system and catch
        /// bugs that linger in places, like the bonjour interface...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdGc(ref Interpreter.FunctionArguments a_functionarguments)
        {
            // Let's see if we can break things...
            GC.Collect();

            // All done...
            return (false);
        }

        /// <summary>
        /// Goto the user...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdGoto(ref Interpreter.FunctionArguments a_functionarguments)
        {
            int iLine;
            string szLabel;

            // Validate...
            if (    (a_functionarguments.aszScript == null)
                ||  (a_functionarguments.aszScript.Length < 2)
                ||  (a_functionarguments.aszScript[0] == null)
                ||  (a_functionarguments.aszCmd == null)
                ||  (a_functionarguments.aszCmd.Length < 2)
                ||  (a_functionarguments.aszCmd[1] == null))
            {
                return (false);
            }

            // Search for a match...
            szLabel = ":" + a_functionarguments.aszCmd[1];
            for (iLine = 0; iLine < a_functionarguments.aszScript.Length; iLine++)
            {
                if (a_functionarguments.aszScript[iLine].Trim() == szLabel)
                {
                    a_functionarguments.blGotoLabel = true;
                    a_functionarguments.iLabelLine = iLine;
                    return (false);
                }
            }

            // Ugh...
            DisplayError("goto label not found: <" + szLabel + ">");
            return (false);
        }

        /// <summary>
        /// Help the user...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdHelp(ref Interpreter.FunctionArguments a_functionarguments)
        {
            string szCommand;

            // Summary...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[1] == null))
            {
                Display(m_szBanner);
                Display("");
                DisplayRed("Discovery and Selection");
                Display("help [command]...............................this text or info about a command");
                Display("list.........................................list scanners");
                Display("quit.........................................exit the program");
                Display("select {pattern}.............................select a scanner");
                Display("status.......................................status of the program");
                Display("");
                DisplayRed("Image Capture APIs (in order of use)");
                Display("info.........................................get baseline information about the scanner");
                Display("infoex.......................................get extended information about the scanner");
                Display("invalidCommand...............................see how scanner handles an invalid command");
                Display("invalidUri...................................see how scanner handles an invalid uri");
                Display("createSession................................create a new session");
                Display("getSession...................................show the current session object");
                Display("waitForEvents................................wait for events, like session object changes");
                Display("sendTask {task|file}.........................send task");
                Display("startCapturing...............................start capturing new images");
                Display("readImageBlockMetadata {block} {thumbnail}...read metadata for a block");
                Display("readImageBlock {block} {metadata}............read image data block");
                Display("releaseImageBlocks {first} {last}............release images blocks in the scanner");
                Display("stopCapturing................................stop capturing new images");
                Display("closeSession.................................close the current session");
                Display("");
                DisplayRed("Scripting");
                Display("help scripting...............................general discussion");
                Display("call {label}.................................call function");
                Display("cd [path]....................................shows or sets the current directory");
                Display("checkpdfraster [path]........................validate PDF/raster files");
                Display("clean........................................clean the images folder");
                Display("dir..........................................lists files and folders in the current directory");
                Display("echo [text]..................................echo text");
                Display("echopassfail {title} {result}................echo text in a tabular form");
                Display("goto {label}.................................jump to the :label in the script");
                Display("if {item1} {operator} {item2} goto {label}...if statement");
                Display("increment {dst} {src} [step].................increment src by step and store in dst");
                Display("json2xml {file|json}.........................convert json formatted data to xml");
                Display("log {info|warn|error,etc} text...............add a line to the log file");
                Display("return [status]..............................return from call function");
                Display("run [script].................................run a script");
                Display("runv [script]................................run a script verbosely");
                Display("set [key [value]]............................show, set, or delete keys");
                Display("sleep {milliseconds}.........................pause the current thread");
                Display("twainlocalsession {create|destroy}...........use to test calls without using createSession");
                Display("waitforsessionupdate {milliseconds}..........wait for the session object to update");
                return (false);
            }

            // Get the command...
            szCommand = a_functionarguments.aszCmd[1].ToLower();

            // Discovery and Selection
            #region Discovery and Selection

            // Help...
            if ((szCommand == "help"))
            {
                DisplayRed("HELP [COMMAND]");
                Display("Provides assistence with command and their arguments.  It does not");
                Display("go into detail on TWAIN Direct.  Please read the Specifications for");
                Display("more information.");
                Display("");
                Display("Curly brackets {} indicate mandatory arguments to a command.  Square");
                Display("brackets [] indicate optional arguments.");
                return (false);
            }

            // List...
            if ((szCommand == "list"))
            {
                DisplayRed("LIST");
                Display("List the scanners that are advertising themselves.  Note that the");
                Display("same scanner may be seen multiple times, if it's being advertised");
                Display("on more than one network interface card.");
                return (false);
            }

            // Quit...
            if ((szCommand == "quit"))
            {
                DisplayRed("QUIT");
                Display("Exit from this program.");
                return (false);
            }

            // Select...
            if ((szCommand == "select"))
            {
                DisplayRed("SELECT {PATTERN}");
                Display("Selects one of the scanners shown in the list command, which is");
                Display("the scanner that will be accessed by the API commands.  The pattern");
                Display("must match some or all of the name, the IP address, or the note.");
                Display("");
                Display("Note that with HTTPS we have to use the link local name, which");
                Display("means that you can't select which network interface is going to");
                Display("be used to talk to the scanner.  Put another way, we can't use the");
                Display("raw IP address.");
                return (false);
            }

            // Status...
            if ((szCommand == "status"))
            {
                DisplayRed("STATUS");
                Display("General information about the current operation of the program.");
                return (false);
            }

            #endregion

            // Image Capture APIs (in order of use)
            #region Image Capture APIs (in order of use)

            // infoex...
            if ((szCommand == "info"))
            {
                DisplayRed("INFO");
                Display("Issues an info command to the scanner that picked out using");
                Display("the SELECT command.  The command must be issued before making");
                Display("a call to CREATESESSION.");
                return (false);
            }

            // infoex...
            if ((szCommand == "infoex"))
            {
                DisplayRed("INFOEX");
                Display("Issues an infoex command to the scanner that picked out using");
                Display("the SELECT command.  The command must be issued before making");
                Display("a call to CREATESESSION.");
                return (false);
            }

            // invalidCommand...
            if ((szCommand == "invalidcommand"))
            {
                DisplayRed("INVALIDCOMMAND");
                Display("See how the scanner handles an invalid command.");
                return (false);
            }

            // invalidUri...
            if ((szCommand == "invaliduri"))
            {
                DisplayRed("INVALIDURI");
                Display("See how the scanner handles an invalid uri.");
                return (false);
            }

            // createSession...
            if ((szCommand == "createsession"))
            {
                DisplayRed("CREATESESSION");
                Display("Creates a session for the scanner picked out using the SELECT");
                Display("command.  To end the session use CLOSESESSION.");
                return (false);
            }

            // getSession...
            if ((szCommand == "getsession"))
            {
                DisplayRed("GETSESSION");
                Display("Gets infornation about the current session.");
                return (false);
            }

            // waitForEvents...
            if ((szCommand == "waitforevents"))
            {
                DisplayRed("WAITFOREVENTS [SCRIPT [argument1 [argument2[...]]]");
                Display("TWAIN Direct is event driven.  The command creates the event");
                Display("monitor used to detect updates to the session object.  It");
                Display("should be called once after CREATESESSION.  If a script name");
                Display("is specified, it'll be run when the event fires, and it will");
                Display("receive the arguments sent to it, if any.");
                return (false);
            }

            // sendTask...
            if ((szCommand == "sendtask"))
            {
                DisplayRed("SENDTASK {TASK|FILE}");
                Display("Sends a TWAIN Direct task.  The argument can either be the");
                Display("task itself, or a file containing the task.");
                return (false);
            }

            // startCapturing...
            if ((szCommand == "startcapturing"))
            {
                DisplayRed("STARTCAPTURING");
                Display("Start capturing images from the scanner.");
                return (false);
            }

            // readImageBlockMetadata...
            if ((szCommand == "readimageblockmetadata"))
            {
                DisplayRed("READIMAGEBLOCKMETADATA {BLOCK} {INCLUDETHUMBNAIL}");
                Display("Reads the metadata for the specified image BLOCK, and");
                Display("optionally includes a thumbnail for that image.  The");
                Display("value of BLOCK matches one of the numbers in the session");
                Display("object's imageBlocks array.  The INCLUDETHUMBNAIL value");
                Display("mustt be set to true to get a thumbnail.");
                return (false);
            }

            // readImageBlock...
            if ((szCommand == "readimageblock"))
            {
                DisplayRed("READIMAGEBLOCK {BLOCK} {INCLUDEMETADATA}");
                Display("Reads the image data for the specified image BLOCK, and");
                Display("optionally includes the metadata for that image.  The");
                Display("value of BLOCK matches one of the numbers in the session");
                Display("object's imageBlocks array.  The INCLUDEMETADATA value");
                Display("must be set to true to get metadata with the image.");
                return (false);
            }

            // releaseImageBlocks...
            if ((szCommand == "releaseimageblocks"))
            {
                DisplayRed("RELEASEIMAGEBLOCKS {FIRST} {LAST}");
                Display("Releases the image blocks from FIRST to LAST inclusive.");
                Display("The value of FIRST and LAST matches one of the numbers in");
                Display("the session object's imageBlocks array.");
                return (false);
            }

            // stopCapturing...
            if ((szCommand == "stopCapturing"))
            {
                DisplayRed("STOPCAPTURING");
                Display("Stop capturing images from the scanner, the scanner will");
                Display("complete scanning the current image.");
                return (false);
            }

            // closeSession...
            if ((szCommand == "closeSession"))
            {
                DisplayRed("CLOSESESSION");
                Display("Close the session, which unlocks the scanner.  The user");
                Display("is responsible for releasing any remaining images.  The");
                Display("scanner is not unlocked until all images are released.");
                return (false);
            }

            #endregion

            // Scripting
            #region Scripting

            // Scripting...
            if ((szCommand == "scripting"))
            {
                /////////0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789
                DisplayRed("GENERAL DISCUSSION OF SCRIPTING");
                Display("The TWAIN Direct Certification program is designed to test scanners and applications.  It looks");
                Display("at header information, JSONS payloads, and image data.  It's script based to make it easier to");
                Display("manage the tests.  Users can create and run their own tests, such as extracting key items from an");
                Display("existing test to make it easier to debug.");
                Display("");
                Display("The 'language' is not sophisticated.  It supports a goto, a conditional goto, and a call");
                Display("function.  The set and increment commands manage variables.  All of the TWAIN Direct calls are");
                Display("accessible, including some extras used to stress the system.  The semicolon ';' is the comment");
                Display("indicator.  At this time it must appear on a line by itself.");
                Display("");
                Display("The most interesting part of the scripting support is variable expansion.  Variables take the");
                Display("form ${source:target} with the following available sources:");
                Display("");
                Display("  '${arg:target}'");
                Display("  Expands an argument argument to run, runv, or call.  0 is the name of the script or label, and");
                Display("  1 - n access the rest of the arguments.");
                Display("");
                Display("  '${ej:target}'");
                Display("  Accesses the JSON contents of the last event.  For instance, ${ej:results.success} returns a");
                Display("  value of true or false for the last event, or an empty string if communication failed.  If");
                Display("  the target is #, then it expands to the number of UTF-8 bytes in the JSON payload.  If the");
                Display("  value can't be found it expands to an empty string.  Use this in the WAITFOREVENTS script.");
                Display("");
                Display("  '${ejx:target}'");
                Display("  Works like ${ej:target}, but if the target can't be found it expands to '(null)'.  Use this");
                Display("  in the WAITFOREVENTS script.");
                Display("");
                Display("  '${ests:}'");
                Display("  The HTTP status from the last waitForEvents command.  Use this in the WAITFOREVENTS script.");
                Display("");
                Display("  '${get:target}'");
                Display("  The value last assigned to the target using the set command.");
                Display("");
                Display("  '${hdrkey:target}'");
                Display("  Accesses the header keys in the response from the last command.  Target can be # for the number");
                Display("  of headers, or a value from 0 - (${hdrkey:#} - 1) to access a particular header.");
                Display("");
                Display("  '${hdrvalue:target}'");
                Display("  Accesses the header values in the response from the last command.  Target can be # for the number");
                Display("  of headers, or a value from 0 - (${hdrkey:#} - 1) to access a particular header.");
                Display("");
                Display("  '${hdrjsonkey:target}'");
                Display("  Accesses the header keys in the JSON multipart response from the last command.  Target can be #");
                Display("  for the number of headers, or a value from 0 - (${hdrkey:#} - 1) to access a particular header.");
                Display("");
                Display("  '${hdrjsonvalue:target}'");
                Display("  Accesses the header values in the JSON multipart response from the last command.  Target can be #");
                Display("  for the number of headers, or a value from 0 - (${hdrkey:#} - 1) to access a particular header.");
                Display("");
                Display("  '${hdrimagekey:target}'");
                Display("  Accesses the header keys in the image multipart response from the last command.  Target can be #");
                Display("  for the number of headers, or a value from 0 - (${hdrkey:#} - 1) to access a particular header.");
                Display("");
                Display("  '${hdrimagevalue:target}'");
                Display("  Accesses the header values in the image multipart response from the last command.  Target can be");
                Display("  # for the number of headers, or a value from 0 - (${hdrkey:#} - 1) to access a particular header.");
                Display("");
                Display("  '${hdrthumbnailkey:target}'");
                Display("  Accesses the header keys in the thumbnail multipart response from the last command.  Target can");
                Display("  be # for the number of headers, or a value from 0 - (${hdrkey:#} - 1) to access a particular");
                Display("  header.");
                Display("");
                Display("  '${hdrthumbnailvalue:target}'");
                Display("  Accesses the header values in the thumbnail multipart response from the last command.  Target");
                Display("  can be # for the number of headers, or a value from 0 - (${hdrkey:#} - 1) to access a particular");
                Display("  header.");
                Display("");
                Display("  '${localtime:[format]}'");
                Display("  Returns the current local time using the DateTime format.");
                Display("");
                Display("  '${ret:}'");
                Display("  The value supplied to the return command that ended the last run, runv, or call.  It's also");
                Display("  used by the WAITFORSESSIONUPDATE command.");
                Display("");
                Display("  '${rj:target}'");
                Display("  Accesses the JSON contents of the last command.  For instance, ${rj:results.success} returns a");
                Display("  value of true or false for the last command, or an empty string if communication failed.  If");
                Display("  the target is #, then it expands to the number of UTF-8 bytes in the JSON payload.  If the");
                Display("  value can't be found it expands to an empty string.");
                Display("");
                Display("  '${rjx:target}'");
                Display("  Works like ${rj:target}, but if the target can't be found it expands to '(null)'");
                Display("");
                Display("  '${rsts:}'");
                Display("  The HTTP status from the last command.");
                Display("");
                Display("  '${txt:target}'");
                Display("  Access the mDNS TXT fields.  If a target can't be found, it expands to an empty string.");
                Display("");
                Display("  '${txtx:target}'");
                Display("  Works like ${txt:target}, but if the target can't be found it expands to '(null)'");
                Display("");
                Display("Note that some tricks are allowed, one can do ${hdrkey:${get:index}}, using the set and increment");
                Display("increment commands to enumerate all of the header keys.  Or ${rj:${arg:1}} to pass a JSON key into");
                Display("a function.");
                return (false);
            }

            // Call...
            if ((szCommand == "call"))
            {
                DisplayRed("CALL {FUNCTION [argument1 [argument2 [...]]}");
                Display("Call a function with optional arguments.  Check '${ret:} to see what the");
                Display("function send back with its RETURN command.  The function must be prefixed");
                Display("with a colon.  For example...");
                Display("  call XYZ");
                Display("  ; the script will return here");
                Display("  ...");
                Display("  :XYZ");
                Display("  return");
                Display("");
                Display("Gotos are easy to implement, and easy to script, but they can get out of");
                Display("control fast.  Keep functions small.  And when doing a goto inside of a");
                Display("function, use the function name as a prefix to help avoid reusing the same");
                Display("label in more than one place.  For example...");
                Display("  call XYZ abc");
                Display("  ; the script will return here");
                Display("  ...");
                Display("  :XYZ");
                Display("  if '${arg:1}' == 'abc' goto XYZ.ABC");
                Display("  return 'is not abc'");
                Display("  :XYZ.ABC");
                Display("  return 'is abc'");
                return (false);
            }

            // Cd...
            if ((szCommand == "cd"))
            {
                DisplayRed("CD [PATH]");
                Display("Show the current directory.  If a path is specified, change to that path.");
                return (false);
            }

            // Checkpdfraster...
            if ((szCommand == "checkpdfraster"))
            {
                DisplayRed("CHECKPDFRASTER");
                Display("Validates that all of the PDF/raster files in the images folder are in");
                Display("compliance with the specification.  It also requires at least one digital");
                Display("signature.  All digital signatures are tested for validity.  The XMP data");
                Display("for the page is extracted, converted from base64, and compared to the");
                Display("metadata; it must match.  The valueof ${return:} is 'pass' on success.");
                return (false);
            }

            // Clean...
            if ((szCommand == "clean"))
            {
                DisplayRed("CLEAN");
                Display("Delete all files and folders in the images folder.");
                return (false);
            }

            // Dir...
            if ((szCommand == "dir"))
            {
                DisplayRed("DIR");
                Display("Directory command, lists files and folders in the current directory.");
                return (false);
            }

            // Echo...
            if ((szCommand == "echo"))
            {
                Display("ECHO [TEXT]");
                Display("Echoes the text.  If there is no text an empty line is echoed.");
                return (false);
            }

            // Echopassfail...
            if ((szCommand == "echopassfail"))
            {
                DisplayRed("ECHOPASSFAIL [TITLE] [RESULT]");
                Display("Echoes the title and result in a tabular format.");
                return (false);
            }

            // Goto...
            if ((szCommand == "goto"))
            {
                DisplayRed("GOTO {LABEL}");
                Display("Jump to the specified label in the script.  The label must be");
                Display("prefixed with a colon.  For example...");
                Display("  goto XYZ");
                Display("  :XYZ");
                return (false);
            }

            // If...
            if ((szCommand == "if"))
            {
                DisplayRed("IF {ITEM1} {OPERATOR} {ITEM2} GOTO {LABEL}");
                Display("If the operator for ITEM1 and ITEM2 is true, then goto the");
                Display("label.  For the best experience get in the habit of putting");
                Display("either single or double quotes around the items.");
                Display("");
                Display("Operators");
                Display("==...........values are equal (case sensitive)");
                Display("<............item1 is numerically less than item2");
                Display("<=...........item1 is numerically less than or equal to item2");
                Display(">............item1 is numerically greater than item2");
                Display(">=...........item1 is numerically greater than or equal to item2");
                Display("~~...........values are equal (case insensitive)");
                Display("contains.....item2 is contained in item1 (case sensitive)");
                Display("~contains....item2 is contained in item1 (case insensitive)");
                Display("!=...........values are not equal (case sensitive)");
                Display("!~...........values are not equal (case insensitive)");
                Display("!contains....item2 is not contained in item1 (case sensitive)");
                Display("!~contains...item2 is not contained in item1 (case sensitive)");
                Display("");
                Display("Items");
                Display("Items prefixed with 'rj:' indicate that the item is a JSON");
                Display("key in the last command's response payload.  For instance:");
                Display("  if '${rj:results.success}' != 'true' goto FAIL");
                Display("Items prefixed with 'get:' indicate that the item is the");
                Display("result of a prior set command.");
                Display("  if '${get:value}' != 'true' goto FAIL");
                Display("");
                Display("Enter HELP SCRIPTING for the complete list of symbols.");
                return (false);
            }

            // Increment...
            if ((szCommand == "increment"))
            {
                DisplayRed("INCREMENT {DST} {SRC} [STEP]");
                Display("Increments SRC by STEP and stores in DST.  STEP defaults to 1.");
                return (false);
            }

            // pwd...
            if ((szCommand == "pwd"))
            {
                DisplayRed("PWD");
                Display("Show the path to the current working directory.");
                return (false);
            }

            // Return...
            if ((szCommand == "return"))
            {
                DisplayRed("RETURN [STATUS]");
                Display("Return from a call function or a script invoked with RUN or RUNV.");
                Display("The caller can examine this value with the '${ret:}' symbol.");
                return (false);
            }

            // Run...
            if ((szCommand == "run"))
            {
                DisplayRed("RUN [SCRIPT]");
                Display("Runs the specified script.  SCRIPT is the full path to the script");
                Display("to be run.  If a SCRIPT is not specified, the scripts in the");
                Display("current folder are listed.");
                return (false);
            }

            // Run verbose...
            if ((szCommand == "runv"))
            {
                Display("RUNV [SCRIPT]");
                Display("Runs the specified script.  SCRIPT is the full path to the script");
                Display("to be run.  If a SCRIPT is not specified, the scripts in the");
                Display("current folder are listed.  The script commands are displayed.");
                return (false);
            }

            // Set...
            if ((szCommand == "set"))
            {
                DisplayRed("SET {KEY} {VALUE}");
                Display("Set a key to the specified value.  If a KEY is not specified");
                Display("all of the current keys are listed with their values.");
                Display("");
                Display("Values");
                Display("Values prefixed with 'rj:' indicate that the item is a JSON");
                Display("key in the last command's response payload.  For instance:");
                Display("  set success '${rj:results.success}'");
                return (false);
            }

            // Sleep...
            if ((szCommand == "sleep"))
            {
                DisplayRed("SLEEP {MILLISECONDS}");
                Display("Pause the thread for the specified number of milliseconds.");
                return (false);
            }

            // Twainlocalsession...
            if ((szCommand == "twainlocalsession"))
            {
                DisplayRed("TWAINLOCALSESSION {CREATE|DESTROY}");
                Display("Use this to test the behavior of commands called before");
                Display("createSession.  They should return 'invalidSessionId'.");
                return (false);
            }

            // Waitforsessionupdate...
            if ((szCommand == "waitforsessionupdate"))
            {
                DisplayRed("WAITFORSESSIONUPDATE {MILLISECONDS}");
                Display("Wait MILLISECONDS for the session object to be updated, which");
                Display("means that its revision number has been incremented.  The '${ret:}'");
                Display("symbol is set to true if the command was signaled.  A value of");
                Display("false means the command timed out.");
                return (false);
            }

            #endregion

            // Well, this ain't good...
            DisplayError("unrecognized command: " + a_functionarguments.aszCmd[1]);

            // All done...
            return (false);
        }

        /// <summary>
        /// Process an if-statement...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdIf(ref Interpreter.FunctionArguments a_functionarguments)
        {
            bool blDoAction = false;
            string szItem1;
            string szItem2;
            string szOperator;
            string szAction;

            // Validate...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 5) || (a_functionarguments.aszCmd[1] == null))
            {
                DisplayError("badly formed if-statement...");
                return (false);
            }

            // Get all of the stuff...
            szItem1 = a_functionarguments.aszCmd[1];
            szOperator = a_functionarguments.aszCmd[2];
            szItem2 = a_functionarguments.aszCmd[3];
            szAction = a_functionarguments.aszCmd[4];

            // Items must match (case sensitive)...
            if (szOperator == "==")
            {
                if (szItem1 == szItem2)
                {
                    blDoAction = true;
                }
            }

            // Items must match (case insensitive)...
            else if (szOperator == "~~")
            {
                if (szItem1.ToLowerInvariant() == szItem2.ToLowerInvariant())
                {
                    blDoAction = true;
                }
            }

            // Items must not match (case sensitive)...
            else if (szOperator == "!=")
            {
                if (szItem1 != szItem2)
                {
                    blDoAction = true;
                }
            }

            // Items must not match (case insensitive)...
            else if (szOperator == "!~")
            {
                if (szItem1.ToLowerInvariant() != szItem2.ToLowerInvariant())
                {
                    blDoAction = true;
                }
            }

            // Item1 > Item2...
            else if (szOperator == ">")
            {
                int iItem1;
                int iItem2;
                if (!int.TryParse(szItem1, out iItem1))
                {
                    DisplayError("<" + szItem1 + "> > <" + szItem2 + "> is invalid");
                }
                else
                {
                    if (!int.TryParse(szItem2, out iItem2))
                    {
                        DisplayError("<" + szItem1 + "> > <" + szItem2 + "> is invalid");
                    }
                    else
                    {
                        if (iItem1 > iItem2)
                        {
                            blDoAction = true;
                        }
                    }
                }
            }

            // Item1 >= Item2...
            else if (szOperator == ">=")
            {
                int iItem1;
                int iItem2;
                if (!int.TryParse(szItem1, out iItem1))
                {
                    DisplayError("<" + szItem1 + "> >= <" + szItem2 + "> is invalid");
                }
                else
                {
                    if (!int.TryParse(szItem2, out iItem2))
                    {
                        DisplayError("<" + szItem1 + "> >= <" + szItem2 + "> is invalid");
                    }
                    else
                    {
                        if (iItem1 >= iItem2)
                        {
                            blDoAction = true;
                        }
                    }
                }
            }

            // Item1 < Item2...
            else if (szOperator == "<")
            {
                int iItem1;
                int iItem2;
                if (!int.TryParse(szItem1, out iItem1))
                {
                    DisplayError("<" + szItem1 + "> < <" + szItem2 + "> is invalid");
                }
                else
                {
                    if (!int.TryParse(szItem2, out iItem2))
                    {
                        DisplayError("<" + szItem1 + "> < <" + szItem2 + "> is invalid");
                    }
                    else
                    {
                        if (iItem1 < iItem2)
                        {
                            blDoAction = true;
                        }
                    }
                }
            }

            // Item1 <= Item2...
            else if (szOperator == "<=")
            {
                int iItem1;
                int iItem2;
                if (!int.TryParse(szItem1, out iItem1))
                {
                    DisplayError("<" + szItem1 + "> <= <" + szItem2 + "> is invalid");
                }
                else
                {
                    if (!int.TryParse(szItem2, out iItem2))
                    {
                        DisplayError("<" + szItem1 + "> <= <" + szItem2 + "> is invalid");
                    }
                    else
                    {
                        if (iItem1 <= iItem2)
                        {
                            blDoAction = true;
                        }
                    }
                }
            }

            // Item1 must contain items2 (case sensitive)...
            else if (szOperator == "contains")
            {
                if (szItem1.Contains(szItem2))
                {
                    blDoAction = true;
                }
            }

            // Item1 must contain items2 (case insensitive)...
            else if (szOperator == "~contains")
            {
                if (szItem1.ToLowerInvariant().Contains(szItem2.ToLowerInvariant()))
                {
                    blDoAction = true;
                }
            }

            // Item1 must not contain items2 (case sensitive)...
            else if (szOperator == "!contains")
            {
                if (!szItem1.Contains(szItem2))
                {
                    blDoAction = true;
                }
            }

            // Item1 must not contain items2 (case insensitive)...
            else if (szOperator == "!~contains")
            {
                if (!szItem1.ToLowerInvariant().Contains(szItem2.ToLowerInvariant()))
                {
                    blDoAction = true;
                }
            }

            // Unrecognized operator...
            else
            {
                DisplayError("unrecognized operator: <" + szOperator + ">");
                return (false);
            }

            // We've been told to do the action...
            if (blDoAction)
            {
                // We're doing a goto...
                if (szAction.ToLowerInvariant() == "goto")
                {
                    int iLine;
                    string szLabel;

                    // Validate...
                    if ((a_functionarguments.aszCmd.Length < 5) || string.IsNullOrEmpty(a_functionarguments.aszCmd[4]))
                    {
                        DisplayError("goto label is missing...");
                        return (false);
                    }

                    // Find the label...
                    szLabel = ":" + a_functionarguments.aszCmd[5];
                    for (iLine = 0; iLine < a_functionarguments.aszScript.Length; iLine++)
                    {
                        if (a_functionarguments.aszScript[iLine].Trim() == szLabel)
                        {
                            a_functionarguments.blGotoLabel = true;
                            a_functionarguments.iLabelLine = iLine;
                            return (false);
                        }
                    }

                    // Ugh...
                    DisplayError("goto label not found: <" + szLabel + ">");
                    return (false);
                }

                // We have no idea what we're doing...
                else
                {
                    DisplayError("unrecognized action: <" + szAction + ">");
                    return (false);
                }
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Return from the current function...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdIncrement(ref Interpreter.FunctionArguments a_functionarguments)
        {
            int iSrc;
            int iDst;
            int iStep = 1;

            // Validate...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 3) || (a_functionarguments.aszCmd[1] == null))
            {
                DisplayError("badly formed increment...");
                return (false);
            }

            // Turn the source into a number...
            if (!int.TryParse(a_functionarguments.aszCmd[2], out iSrc))
            {
                DisplayError("source is not a number...");
                return (false);
            }

            // Get the step...
            if ((a_functionarguments.aszCmd.Length >= 4) || (a_functionarguments.aszCmd[3] != null))
            {
                if (!int.TryParse(a_functionarguments.aszCmd[3], out iStep))
                {
                    DisplayError("step is not a number...");
                    return (false);
                }
            }

            // Increment the value...
            iDst = iSrc + iStep;

            // Store the value...
            Interpreter.FunctionArguments functionarguments = default(Interpreter.FunctionArguments);
            functionarguments.aszCmd = new string[3];
            functionarguments.aszCmd[0] = "set";
            functionarguments.aszCmd[1] = a_functionarguments.aszCmd[1];
            functionarguments.aszCmd[2] = iDst.ToString();
            CmdSet(ref functionarguments);

            // All done...
            return (false);
        }

        /// <summary>
        /// Accept command input from the user.  The data is returned in the
        /// ${ret:} variable.  The first argument is a prompt, the rest of
        /// the arguments are optional, and indicate values that must be
        /// entered if the input command is going to return...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdInput(ref Interpreter.FunctionArguments a_functionarguments)
        {
            int ii;
            string szCmd = "";
            string szPrompt = "enter input: ";
            List<string> lszCommands = new List<string>();

            // Get the prompt...
            if ((a_functionarguments.aszCmd.Length >= 2) && (a_functionarguments.aszCmd[1] != null))
            {
                szPrompt = a_functionarguments.aszCmd[1];
            }

            // Get the commands...
            for (ii = 3; true; ii++)
            {
                if ((ii >= a_functionarguments.aszCmd.Length) || string.IsNullOrEmpty(a_functionarguments.aszCmd[ii - 1]))
                {
                    break;
                }
                lszCommands.Add(a_functionarguments.aszCmd[ii - 1]);
            }

            // Loopy...
            Interpreter interpreter = new Interpreter(szPrompt);
            while (true)
            {
                // Get the command...
                szCmd = interpreter.Prompt(m_streamreaderConsole);

                // If we have no commands to compare it against, we're done...
                if (lszCommands.Count == 0)
                {
                    break;
                }

                // Otherwise, we have to look for a match...
                bool blFound = false;
                foreach (string szCommand in lszCommands)
                {
                    if (szCmd.ToLowerInvariant() == szCommand.ToLowerInvariant())
                    {
                        blFound = true;
                        break;
                    }
                }

                // We got a match...
                if (blFound)
                {
                    break;
                }
            }

            // Update the return value...
            CallStack callstack = m_lcallstack[m_lcallstack.Count - 1];
            callstack.functionarguments.szReturnValue = szCmd;
            m_lcallstack[m_lcallstack.Count - 1] = callstack;

            // All done...
            return (false);
        }

        /// <summary>
        /// List scanners, both ones on the LAN and ones that are
        /// available in the cloud (when we get that far)...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdList(ref Interpreter.FunctionArguments a_functionarguments)
        {
            bool blUpdated;

            // Get a snapshot of the TWAIN Local scanners...
            m_adnssddeviceinfoSnapshot = m_dnssd.GetSnapshot(null, out blUpdated);

            // Display TWAIN Local...
            if (!m_blSilent)
            {
                if ((m_adnssddeviceinfoSnapshot == null) || (m_adnssddeviceinfoSnapshot.Length == 0))
                {
                    DisplayError("no TWAIN Local scanners");
                }
                else
                {
                    foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in m_adnssddeviceinfoSnapshot)
                    {
                        if (!string.IsNullOrEmpty(dnssddeviceinfo.GetIpv4()))
                        {
                            Display(dnssddeviceinfo.GetLinkLocal() + " " + dnssddeviceinfo.GetIpv4() + " " + dnssddeviceinfo.GetTxtNote());
                        }
                        else if (!string.IsNullOrEmpty(dnssddeviceinfo.GetIpv6()))
                        {
                            Display(dnssddeviceinfo.GetLinkLocal() + " " + dnssddeviceinfo.GetIpv6() + " " + dnssddeviceinfo.GetTxtNote());
                        }
                    }
                }
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Quit...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdQuit(ref Interpreter.FunctionArguments a_functionarguments)
        {
            // Bye-bye...
            return (true);
        }

        /// <summary>
        /// Convert JSON to XML...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdJson2Xml(ref Interpreter.FunctionArguments a_functionarguments)
        {
            bool blSuccess;
            string szJson;

            // Must supply a file or data...
            if ((a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[1] == null))
            {
                DisplayError("must supply a file or data...");
                return (false);
            }

            // Is the argument a file?
            if (File.Exists(a_functionarguments.aszCmd[1]))
            {
                try
                {
                    szJson = File.ReadAllText(a_functionarguments.aszCmd[1]);
                }
                catch (Exception exception)
                {
                    DisplayError("failed to open file...<" + a_functionarguments.aszCmd[1] + "> - " + exception.Message);
                    return (false);
                }
            }
            else
            {
                szJson = a_functionarguments.aszCmd[1];
            }

            // Load it...
            long lJsonErrorIndex;
            JsonLookup jsonlookup = new JsonLookup();
            blSuccess = jsonlookup.Load(szJson, out lJsonErrorIndex);
            if (!blSuccess)
            {
                DisplayError("json error at index: " + lJsonErrorIndex);
            }
            else
            {
                string szXml = jsonlookup.GetXml();
                if (szXml == null)
                {
                    DisplayError("unable to convert json to xml...");
                }
                else
                {
                    Display(szXml);
                }
            }   

            // Bye-bye...
            return (false);
        }

        /// <summary>
        /// Log text...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdLog(ref Interpreter.FunctionArguments a_functionarguments)
        {
            int ii;
            int iStart;
            string szSeverity;
            string szMessage;

            // If we have no arguments, then log a blank informational...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[1] == null))
            {
                Log.Info("");
                return (false);
            }

            // Pick a severity...
            switch (a_functionarguments.aszCmd[1])
            {
                default:
                    szSeverity = "info";
                    iStart = 1;
                    break;
                case "info":
                    szSeverity = "info";
                    iStart = 2;
                    break;
                case "warn":
                    szSeverity = "warn";
                    iStart = 2;
                    break;
                case "error":
                    szSeverity = "error";
                    iStart = 2;
                    break;
                case "verbose":
                    szSeverity = "verbose";
                    iStart = 2;
                    break;
                case "assert":
                    szSeverity = "assert";
                    iStart = 2;
                    break;
            }

            // Build the message...
            szMessage = "";
            for (ii = iStart; ii < a_functionarguments.aszCmd.Length; ii++)
            {
                szMessage += (szMessage == "") ? a_functionarguments.aszCmd[ii] : " " + a_functionarguments.aszCmd[ii];
            }

            // Log it...
            switch (szSeverity)
            {
                default:
                case "info":
                    Log.Info(szMessage);
                    break;
                case "warn":
                    Log.Warn(szMessage);
                    break;
                case "error":
                    Log.Error(szMessage);
                    break;
                case "verbose":
                    Log.Verbose(szMessage);
                    break;
                case "assert":
                    Log.Assert(szMessage);
                    break;
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Return from the current function...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdReturn(ref Interpreter.FunctionArguments a_functionarguments)
        {
            CallStack callstack;

            // If we don't have anything on the stack, then scoot...
            if ((m_lcallstack == null) || (m_lcallstack.Count == 0))
            {
                return (false);
            }

            // If this is the base of the stack, then return is a noop...
            if (m_lcallstack.Count == 1)
            {
                return (false);
            }

            // Make a copy of the last item (which we're about to delete)...
            callstack = m_lcallstack[m_lcallstack.Count - 1];

            // Remove the last item...
            m_lcallstack.RemoveAt(m_lcallstack.Count - 1);

            // Set the line we want to jump back to...
            a_functionarguments.blGotoLabel = true;
            a_functionarguments.iLabelLine = callstack.functionarguments.iCurrentLine + 1;

            // Make a note of the return value for "ret:"...
            if ((a_functionarguments.aszCmd != null) && (a_functionarguments.aszCmd.Length > 1))
            {
                callstack = m_lcallstack[m_lcallstack.Count - 1];
                callstack.functionarguments.szReturnValue = a_functionarguments.aszCmd[1];
                m_lcallstack[m_lcallstack.Count - 1] = callstack;
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// With no arguments, list the scripts.  With an argument,
        /// run the specified script.  This one runs silent.
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdRun(ref Interpreter.FunctionArguments a_functionarguments)
        {
            bool blSuccess;
            bool blSilent = m_blSilent;
            bool blSilentEvents = m_blSilentEvents;
            m_blSilent = true;
            m_blSilentEvents = true;
            blSuccess = CmdRunv(ref a_functionarguments);
            m_blSilent = blSilent;
            m_blSilentEvents = blSilentEvents;
            return (blSuccess);
        }

        /// <summary>
        /// With no arguments, list the scripts.  With an argument,
        /// run the specified script.  The one runs verbose.
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdRunv(ref Interpreter.FunctionArguments a_functionarguments)
        {
            string szPrompt = "tdc>>> ";
            string[] aszScript;
            string szScriptFile;
            int iCallStackCount;
            CallStack callstack;
            Interpreter interpreter;

            // List...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[1] == null))
            {
                // Get the script files...
                string[] aszScriptFiles = Directory.GetFiles(".", "*.tdc");
                if ((aszScriptFiles == null) || (aszScriptFiles.Length == 0))
                {
                    DisplayError("no script files found");
                }

                // List what we found...
                Display("SCRIPT FILES");
                foreach (string sz in aszScriptFiles)
                {
                    Display(Path.GetFileNameWithoutExtension(sz), true);
                }

                // All done...
                return (false);
            }

            // Make sure the file exists...
            szScriptFile = a_functionarguments.aszCmd[1];
            if (!File.Exists(szScriptFile))
            {
                szScriptFile = a_functionarguments.aszCmd[1] + ".tdc";
                if (!File.Exists(szScriptFile))
                {
                    DisplayError("script not found...<" + szScriptFile + ">");
                    return (false);
                }
            }

            // Read the file...
            try
            {
                aszScript = File.ReadAllLines(szScriptFile);
            }
            catch (Exception exception)
            {
                DisplayError("failed to read script...<" + szScriptFile + ">" + exception.Message);
                return (false);
            }

            // Give ourselves an interpreter...
            interpreter = new Interpreter("");

            // Bump ourself up on the call stack, because we're really
            // working like a call.  At this point we'll be running with
            // at least 2 items on the stack.  If we drop down to 1 item
            // that's a hint that the return command was used to get out
            // of the script...
            callstack = default(CallStack);
            callstack.functionarguments = a_functionarguments;
            callstack.functionarguments.aszScript = aszScript;
            m_lcallstack.Add(callstack);
            iCallStackCount = m_lcallstack.Count;

            // Run each line in the script...
            int iLine = 0;
            bool blReturn = false;
            while (iLine < aszScript.Length)
            {
                bool blDone;
                string szLine;
                string[] aszCmd;

                // Grab our line...
                szLine = aszScript[iLine];

                // Show the command...
                if (!m_blSilent)
                {
                    Display(szPrompt + szLine.Trim());
                }

                // Tokenize...
                aszCmd = interpreter.Tokenize(szLine.Trim());

                // Expansion of symbols...
                Expansion(ref aszCmd);

                // Dispatch...
                Interpreter.FunctionArguments functionarguments = default(Interpreter.FunctionArguments);
                functionarguments.aszCmd = aszCmd;
                functionarguments.aszScript = aszScript;
                functionarguments.iCurrentLine = iLine;
                functionarguments.transaction = m_transactionLast;
                blDone = interpreter.Dispatch(ref functionarguments, m_ldispatchtable);
                if (blDone)
                {
                    break;
                }
                m_transactionLast = functionarguments.transaction;

                // Handle gotos...
                if (functionarguments.blGotoLabel)
                {
                    iLine = functionarguments.iLabelLine;
                }
                // Otherwise, just increment...
                else
                {
                    iLine += 1;
                }

                // Update the prompt with state information...
                if (m_twainlocalscannerclient == null)
                {
                    szPrompt = "tdc>>> ";
                }
                else
                {
                    switch (m_twainlocalscannerclient.GetState())
                    {
                        default: szPrompt = "tdc." + m_twainlocalscannerclient.GetState() + ">>> "; break;
                        case "noSession": szPrompt = "tdc>>> "; break;
                        case "ready": szPrompt = "tdc.rdy>>> "; break;
                        case "capturing": szPrompt = "tdc.cap>>> "; break;
                        case "draining": szPrompt = "tdc.drn>>> "; break;
                        case "closed": szPrompt = "tdc.cls>>> "; break;
                    }
                }

                // If the count dropped, that's a sign we need to bail...
                if (m_lcallstack.Count < iCallStackCount)
                {
                    blReturn = true;
                    break;
                }
            }

            // Pop this item, and pass along the return value, but don't do it
            // if we detect that a return call was made in the script, because
            // it will have already popped the stack for us...
            if (!blReturn && (m_lcallstack.Count > 1))
            {
                string szReturnValue = m_lcallstack[m_lcallstack.Count - 1].functionarguments.szReturnValue;
                if (szReturnValue == null)
                {
                    szReturnValue = "";
                }
                m_lcallstack.RemoveAt(m_lcallstack.Count - 1);
                callstack = m_lcallstack[m_lcallstack.Count - 1];
                callstack.functionarguments.szReturnValue = szReturnValue;
                m_lcallstack[m_lcallstack.Count - 1] = callstack;
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Select a scanner, do a snapshot, if needed, if no selection
        /// is offered, then pick the first scanner found...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdSelect(ref Interpreter.FunctionArguments a_functionarguments)
        {
            bool blSilent;

            // Clear the last selected scanner...
            m_dnssddeviceinfoSelected = null;
            if (m_twainlocalscannerclient != null)
            {
                m_twainlocalscannerclient.Dispose();
                m_twainlocalscannerclient = null;
            }

            // If we don't have a snapshot, get one...
            if ((m_adnssddeviceinfoSnapshot == null) || (m_adnssddeviceinfoSnapshot.Length == 0))
            {
                blSilent = m_blSilent;
                m_blSilent = true;
                Interpreter.FunctionArguments functionarguments = default(Interpreter.FunctionArguments);
                CmdList(ref functionarguments);
                m_blSilent = blSilent;
            }

            // No joy...
            if ((m_adnssddeviceinfoSnapshot == null) || (m_adnssddeviceinfoSnapshot.Length == 0))
            {
                DisplayError("no TWAIN Local scanners");
                SetReturnValue("false");
                return (false);
            }

            // We didn't get a selection, so grab the first item...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || string.IsNullOrEmpty(a_functionarguments.aszCmd[1]))
            {
                m_dnssddeviceinfoSelected = m_adnssddeviceinfoSnapshot[0];
                SetReturnValue("true");
                return (false);
            }

            // Look for a match...
            foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in m_adnssddeviceinfoSnapshot)
            {
                // Check the name...
                if (!string.IsNullOrEmpty(dnssddeviceinfo.GetLinkLocal()) && dnssddeviceinfo.GetLinkLocal().Contains(a_functionarguments.aszCmd[1]))
                {
                    m_dnssddeviceinfoSelected = dnssddeviceinfo;
                    break;
                }

                // Check the IPv4...
                else if (!string.IsNullOrEmpty(dnssddeviceinfo.GetIpv4()) && dnssddeviceinfo.GetIpv4().Contains(a_functionarguments.aszCmd[1]))
                {
                    m_dnssddeviceinfoSelected = dnssddeviceinfo;
                    break;
                }

                // Check the note...
                else if (!string.IsNullOrEmpty(dnssddeviceinfo.GetTxtNote()) && dnssddeviceinfo.GetTxtNote().Contains(a_functionarguments.aszCmd[1]))
                {
                    m_dnssddeviceinfoSelected = dnssddeviceinfo;
                    break;
                }
            }

            // Report the result...
            if (m_dnssddeviceinfoSelected != null)
            {
                if (!string.IsNullOrEmpty(m_dnssddeviceinfoSelected.GetIpv4()))
                {
                    Display(m_dnssddeviceinfoSelected.GetLinkLocal() + " " + m_dnssddeviceinfoSelected.GetIpv4() + " " + m_dnssddeviceinfoSelected.GetTxtNote());
                }
                else if (!string.IsNullOrEmpty(m_dnssddeviceinfoSelected.GetIpv6()))
                {
                    Display(m_dnssddeviceinfoSelected.GetLinkLocal() + " " + m_dnssddeviceinfoSelected.GetIpv6() + " " + m_dnssddeviceinfoSelected.GetTxtNote());
                }
                m_twainlocalscannerclient = new TwainLocalScannerClient(null, null, false);
                SetReturnValue("true");
            }
            else
            {
                DisplayError("no selection matches...<" + a_functionarguments.aszCmd[1] + ">");
                SetReturnValue("false");
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// With no arguments, list the keys with their values.  With an argument,
        /// set the specified value.
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdSet(ref Interpreter.FunctionArguments a_functionarguments)
        {
            int iKey;

            // If we don't have any arguments, list what we have...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[1] == null))
            {
                lock (m_objectKeyValue)
                {
                        if (m_lkeyvalue.Count == 0)
                    {
                        DisplayError("no keys to list...");
                        return (false);
                    }

                    // Loopy...
                    Display("KEY/VALUE PAIRS");
                    foreach (KeyValue keyvalue in m_lkeyvalue)
                    {
                        Display(keyvalue.szKey + "=" + keyvalue.szValue);
                    }
                }

                // All done...
                return (false);
            }

            // We need protection...
            lock (m_objectKeyValue)
            {
                // Find the value for this key...
                for (iKey = 0; iKey < m_lkeyvalue.Count; iKey++)
                {
                    if (m_lkeyvalue[iKey].szKey == a_functionarguments.aszCmd[1])
                    {
                        break;
                    }
                }

                // If we have no value to set, then delete this item...
                if ((a_functionarguments.aszCmd.Length < 3) || (a_functionarguments.aszCmd[2] == null))
                {
                    if (iKey < m_lkeyvalue.Count)
                    {
                        m_lkeyvalue.Remove(m_lkeyvalue[iKey]);
                    }
                    return (false);
                }

                // Create a new keyvalue...
                KeyValue keyvalueNew = new KeyValue();
                keyvalueNew.szKey = a_functionarguments.aszCmd[1];
                keyvalueNew.szValue = a_functionarguments.aszCmd[2];

                // If the key already exists, update it's value...
                if (iKey < m_lkeyvalue.Count)
                {
                    m_lkeyvalue[iKey] = keyvalueNew;
                    return (false);
                }

                // Otherwise, add it, and sort...
                m_lkeyvalue.Add(keyvalueNew);
                m_lkeyvalue.Sort(SortByKeyAscending);
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Sleep some number of milliseconds...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdSleep(ref Interpreter.FunctionArguments a_functionarguments)
        {
            int iMilliseconds;

            // Get the milliseconds...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || !int.TryParse(a_functionarguments.aszCmd[1], out iMilliseconds))
            {
                iMilliseconds = 0;
            }
            if (iMilliseconds < 0)
            {
                iMilliseconds = 0;
            }

            // Wait...
            Thread.Sleep(iMilliseconds);

            // All done...
            return (false);
        }

        /// <summary>
        /// Status of the program...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdStatus(ref Interpreter.FunctionArguments a_functionarguments)
        {
            // Current scanner...
            DisplayRed("SELECTED SCANNER");
            if (m_dnssddeviceinfoSelected == null)
            {
                DisplayError("no selected scanner");
            }
            else
            {
                Display("Hostname...." + (!string.IsNullOrEmpty(m_dnssddeviceinfoSelected.GetLinkLocal()) ? m_dnssddeviceinfoSelected.GetLinkLocal() : "(none)"));
                Display("Service....."  + (!string.IsNullOrEmpty(m_dnssddeviceinfoSelected.GetServiceName()) ? m_dnssddeviceinfoSelected.GetServiceName() : "(none)"));
                Display("Interface..." + m_dnssddeviceinfoSelected.GetInterface());
                Display("IPv4........" + (!string.IsNullOrEmpty(m_dnssddeviceinfoSelected.GetIpv4()) ? m_dnssddeviceinfoSelected.GetIpv4() : "(none)"));
                Display("IPv6........" + (!string.IsNullOrEmpty(m_dnssddeviceinfoSelected.GetIpv6()) ? m_dnssddeviceinfoSelected.GetIpv6() : "(none)"));
                Display("Port........" + m_dnssddeviceinfoSelected.GetPort());
                Display("TTL........." + m_dnssddeviceinfoSelected.GetTtl());
                Display("TXT Fields");
                foreach (string sz in m_dnssddeviceinfoSelected.GetTxt())
                {
                    Display("  " + sz);
                }
            }

            // Current snapshot of scanners...
            Display("");
            DisplayRed("LAST SCANNER LIST SNAPSHOT");
            if ((m_adnssddeviceinfoSnapshot == null) || (m_adnssddeviceinfoSnapshot.Length == 0))
            {
                DisplayError("no TWAIN Local scanners");
            }
            else
            {
                foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in m_adnssddeviceinfoSnapshot)
                {
                    if (!string.IsNullOrEmpty(dnssddeviceinfo.GetIpv4()))
                    {
                        Display(dnssddeviceinfo.GetLinkLocal() + " " + dnssddeviceinfo.GetIpv4() + " " + dnssddeviceinfo.GetTxtNote());
                    }
                    else if (!string.IsNullOrEmpty(dnssddeviceinfo.GetIpv6()))
                    {
                        Display(dnssddeviceinfo.GetLinkLocal() + " " + dnssddeviceinfo.GetIpv6() + " " + dnssddeviceinfo.GetTxtNote());
                    }
                }
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Create the TwainLocalSession object without going through createSession, we need
        /// this to do some of the certification tests...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdTwainlocalsession(ref Interpreter.FunctionArguments a_functionarguments)
        {
            // Validate...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || string.IsNullOrEmpty(a_functionarguments.aszCmd[1]))
            {
                DisplayError("specify create or destroy");
                return (false);
            }

            // Create a session...
            if (a_functionarguments.aszCmd[1] == "create")
            {
                if ((a_functionarguments.aszCmd.Length < 3) || string.IsNullOrEmpty(a_functionarguments.aszCmd[2]))
                {
                    m_twainlocalscannerclient.ClientCertificationTwainLocalSessionCreate();
                }
                else
                {
                    m_twainlocalscannerclient.ClientCertificationTwainLocalSessionCreate(a_functionarguments.aszCmd[2]);
                }
                return (false);
            }

            // Destroy a session...
            if (a_functionarguments.aszCmd[1] == "destroy")
            {
                m_twainlocalscannerclient.ClientCertificationTwainLocalSessionDestroy();
                return (false);
            }

            // All done...
            DisplayError("specify create or destroy");
            return (false);
        }

        /// <summary>
        /// Return when the session is updated, or after the timeout expires.  Note
        /// that we're not handling the callback here.  This is because we want the
        /// processing of events to be independent of waiting to be told when they
        /// happen.  This will be the case for most applications, especially as we
        /// add more asynchronous events, such as low power.
        /// 
        /// Check out ClientScannerWaitForEventsCommunicationHelper() and
        /// ClientScannerWaitForEventsProcessingHelper() for the rest of the event
        /// processing implementation.
        /// </summary>
        /// <param name="a_functionarguments">>tokenized command and anything needed</param>
        /// <returns></returns>
        private bool CmdWaitForSessionUpdate(ref Interpreter.FunctionArguments a_functionarguments)
        {
            bool blSignaled;
            long lTimeout = long.MaxValue;
            CallStack callstack;

            // Validate...
            if ((a_functionarguments.aszCmd != null) || (a_functionarguments.aszCmd.Length > 1) || !string.IsNullOrEmpty(a_functionarguments.aszCmd[1]))
            {
                if (!long.TryParse(a_functionarguments.aszCmd[1], out lTimeout))
                {
                    DisplayError("bad value for timeout...");
                    return (false);
                }
            }

            // Wait...
            blSignaled = m_twainlocalscannerclient.ClientWaitForSessionUpdate(lTimeout);

            // Update the return value...
            callstack = m_lcallstack[m_lcallstack.Count - 1];
            if (blSignaled)
            {
                callstack.functionarguments.szReturnValue = "true";
            }
            else
            {
                callstack.functionarguments.szReturnValue = "false";
            }
            m_lcallstack[m_lcallstack.Count - 1] = callstack;

            // All done...
            return (false);
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
                DisplayError("cannot find certification folder:\n" + szCertificationFolder);
                return;
            }

            // Get the categories...
            aszCategories = Directory.GetDirectories(szCertificationFolder);
            if (aszCategories == null)
            {
                DisplayError("cannot find any certification categories:\n" + szCertificationFolder);
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
                    blSuccess = m_twainlocalscannerclient.ClientScannerSendTask(aszTestData[1], ref apicmd);
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
                    string szHttpReplyData = apicmd.GetHttpResponseData();
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


        // Private Methods (misc)
        #region Private Methods (misc)

        /// <summary>
        /// Display text (if allowed)...
        /// </summary>
        /// <param name="a_szText">the text to display</param>
        private void Display(string a_szText, bool a_blForce = false)
        {
            if (!m_blSilent || a_blForce)
            {
                Console.Out.WriteLine(a_szText);
            }
        }

        /// <summary>
        /// Display text (if allowed)...
        /// </summary>
        /// <param name="a_szText">the text to display</param>
        private void DisplayBlue(string a_szText, bool a_blForce = false)
        {
            if (!m_blSilent || a_blForce)
            {
                if (Console.BackgroundColor == ConsoleColor.Black)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Out.WriteLine(a_szText);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                else
                {
                    Console.Out.WriteLine(a_szText);
                }
            }
        }

        /// <summary>
        /// Display text (if allowed)...
        /// </summary>
        /// <param name="a_szText">the text to display</param>
        private void DisplayGreen(string a_szText, bool a_blForce = false)
        {
            if (!m_blSilent || a_blForce)
            {
                if (Console.BackgroundColor == ConsoleColor.Black)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Out.WriteLine(a_szText);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                else
                {
                    Console.Out.WriteLine(a_szText);
                }
            }
        }

        /// <summary>
        /// Display text (if allowed)...
        /// </summary>
        /// <param name="a_szText">the text to display</param>
        private void DisplayRed(string a_szText, bool a_blForce = false)
        {
            if (!m_blSilent || a_blForce)
            {
                if (Console.BackgroundColor == ConsoleColor.Black)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Out.WriteLine(a_szText);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                else
                {
                    Console.Out.WriteLine(a_szText);
                }
            }
        }

        /// <summary>
        /// Display text (if allowed)...
        /// </summary>
        /// <param name="a_szText">the text to display</param>
        private void DisplayYellow(string a_szText, bool a_blForce = false)
        {
            if (!m_blSilent || a_blForce)
            {
                if (Console.BackgroundColor == ConsoleColor.Black)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Out.WriteLine(a_szText);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                else
                {
                    Console.Out.WriteLine(a_szText);
                }
            }
        }

        /// <summary>
        /// Display an error message...
        /// </summary>
        /// <param name="a_szText">the text to display</param>
        private void DisplayError(string a_szText)
        {
            Console.Out.WriteLine("ERROR: " + a_szText);
        }

        /// <summary>
        /// Display information about this apicmd object...
        /// </summary>
        /// <param name="a_apicmd">the object we want to display</param>
        /// <param name="a_blForce">force output</param>
        /// <param name="a_szPrefix">prefix (meant for events)</param>
        private void DisplayApicmd
        (
            ApiCmd a_apicmd,
            bool a_blForce = false,
            string a_szPrefix = ""
        )
        {
            // Nope...
            if (m_blSilent && !a_blForce)
            {
                return;
            }

            // Do it...
            ApiCmd.Transaction transaction = new ApiCmd.Transaction(a_apicmd);
            List<string> lszTransation = transaction.GetAll();
            if (lszTransation != null)
            {
                foreach (string sz in lszTransation)
                {
                    Display(a_szPrefix + sz);
                }
            }
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        internal void Dispose(bool a_blDisposing)
        {
            // Free managed resources...
            if (a_blDisposing)
            {
                if (m_twainlocalscannerclient != null)
                {
                    m_twainlocalscannerclient.Dispose();
                    m_twainlocalscannerclient = null;
                }
                if (m_dnssd != null)
                {
                    m_dnssd.Dispose();
                    m_dnssd = null;
                }
            }
        }

        /// <summary>
        /// Attempt to get a key or a value at a specific index.  We also
        /// support getting the number of headers...
        /// </summary>
        /// <returns>value or (null)</returns>
        internal string GetTransactionAtIndex(string[] a_szHeaders, string a_szTarget, bool a_blKey)
        {
            // There's no chance of getting data...
            if ((a_szHeaders == null) || (m_transactionLast == null))
            {
                return ("(null)");
            }

            // We have a request for the number of headers...
            if (a_szTarget == "#")
            {
                return ((a_szHeaders == null) ? "0" : a_szHeaders.Length.ToString());
            }

            // Try to get data at a specific index, starting from 0...
            int iIndex;
            if (int.TryParse(a_szTarget, out iIndex))
            {
                if ((iIndex >= 0) && (iIndex < a_szHeaders.Length))
                {
                    string[] asz;
                    string szHeader = a_szHeaders[iIndex];

                    // Life sucks, we're sometimes getting "key=value" and other times we're
                    // getting "key: value", so we have to be a bit clever to figure out what's
                    // going on...
                    int iColon = szHeader.IndexOf(':');
                    int iEquals = szHeader.IndexOf('=');

                    // Nothing but losers, just return the silly thing...
                    if ((iColon == -1) && (iEquals == -1))
                    {
                        return (szHeader);
                    }

                    // Init stuff...
                    asz = new string[2];
                    asz[0] = "";
                    asz[1] = "";

                    // Colon wins...
                    if ((iEquals == -1) || ((iColon != -1) && (iColon < iEquals)))
                    {
                        asz[0] = szHeader.Substring(0, iColon);
                        if ((iColon + 1) >= szHeader.Length)
                        {
                            asz[1] = "";
                        }
                        else
                        {
                            asz[1] = szHeader.Substring(iColon + 1, szHeader.Length - (iColon + 1));
                        }
                    }

                    // Equals wins...
                    if ((iColon == -1) || ((iEquals != -1) && (iEquals < iColon)))
                    {
                        asz[0] = szHeader.Substring(0, iEquals);
                        if ((iEquals + 1) >= szHeader.Length)
                        {
                            asz[1] = "";
                        }
                        else
                        {
                            asz[1] = szHeader.Substring(iEquals + 1, szHeader.Length - (iEquals + 1));
                        }
                    }

                    // Woof, we finally made it...
                    return (a_blKey ? asz[0].Trim() : asz[1].Trim());
                }
            }

            // No joy...
            return ("(null)");
        }

        /// <summary>
        /// Expand symbols that we find in the tokenized strings.  Symbols take the form
        /// ${source:key} where source can be one of the following:
        ///     - the JSON text from the response to the last API command
        ///     - the list maintains by the set command
        ///     - the return value from the last run/runv or call in a script
        ///     - the arguments to the program, run/runv or call in a script
        /// 
        /// Symbols can be nests, for instance, if the first argument to a call
        /// is a JSON key, it can be expanded as:
        ///     - ${rj:${arg:1}}
        /// </summary>
        /// <param name="a_aszCmd">tokenized string array to expand</param>
        private void Expansion(ref string[] a_aszCmd)
        {
            int ii;
            int iReferenceCount;
            int iCmd;
            int iIndexLeft;
            int iIndexRight;
            CallStack callstack;

            // Expansion...
            for (iCmd = 0; iCmd < a_aszCmd.Length; iCmd++)
            {
                // If we don't find an occurrance of ${ in the string, then we're done...
                if (!a_aszCmd[iCmd].Contains("${"))
                {
                    continue;
                }

                // Find each outermost ${ in the string, meaning that if we have the
                // following ${rj:${arg:1}}${get:y} we only want to find the rj and
                // the get, the arg will be handled inside of the rj, so that means
                // we have to properly count our way to the closing } for rj...
                for (iIndexLeft = a_aszCmd[iCmd].IndexOf("${");
                        iIndexLeft >= 0;
                        iIndexLeft = a_aszCmd[iCmd].IndexOf("${"))
                {
                    string szSymbol;
                    string szValue;
                    string szKey = a_aszCmd[iCmd];

                    // Find the corresponding }...
                    iIndexRight = -1;
                    iReferenceCount = 0;
                    for (ii = iIndexLeft + 2; ii < szKey.Length; ii++)
                    {
                        // Either exit or decrement our reference count...
                        if (szKey[ii] == '}')
                        {
                            if (iReferenceCount == 0)
                            {
                                iIndexRight = ii;
                                break;
                            }
                            iReferenceCount -= 1;
                        }

                        // Bump up the reference count...
                        if ((szKey[ii] == '$') && ((ii + 1) < szKey.Length) && (szKey[ii + 1] == '{'))
                        {
                            iReferenceCount += 1;
                        }
                    }

                    // If we didn't find a closing }, we're done...
                    if (iIndexRight == -1)
                    {
                        break;
                    }

                    // This is our symbol...
                    // 0123456789
                    // aa${rj:x}a
                    // left index is 2, right index is 8, size is 7, so (r - l) + 1
                    szSymbol = szKey.Substring(iIndexLeft, (iIndexRight - iIndexLeft) + 1);

                    // Expand the stuff to the right of the source, so if we have
                    // ${rj:x} we'll get x back, but if we have ${rj:${arg:1}}, we'll
                    // get the value of ${arg:1} back...
                    if (szSymbol.StartsWith("${rdata:")
                        || szSymbol.StartsWith("${rj:")
                        || szSymbol.StartsWith("${rjx:")
                        || szSymbol.StartsWith("${rsts:")
                        || szSymbol.StartsWith("${edata:")
                        || szSymbol.StartsWith("${ej:")
                        || szSymbol.StartsWith("${ejx:")
                        || szSymbol.StartsWith("${ests:")
                        || szSymbol.StartsWith("${session:")
                        || szSymbol.StartsWith("${get:")
                        || szSymbol.StartsWith("${arg:")
                        || szSymbol.StartsWith("${ret:")
                        || szSymbol.StartsWith("${hdrkey:")
                        || szSymbol.StartsWith("${hdrvalue:")
                        || szSymbol.StartsWith("${hdrjsonkey:")
                        || szSymbol.StartsWith("${hdrjsonvalue:")
                        || szSymbol.StartsWith("${hdrimagekey:")
                        || szSymbol.StartsWith("${hdrimagevalue:")
                        || szSymbol.StartsWith("${hdrthumbnailkey:")
                        || szSymbol.StartsWith("${hdrthumbnailvalue:")
                        || szSymbol.StartsWith("${txt:")
                        || szSymbol.StartsWith("${txtx:")
                        || szSymbol.StartsWith("${localtime:"))
                    {
                        int iSymbolIndexLeft = szSymbol.IndexOf(":") + 1;
                        int iSymbolIndexLength;
                        string[] asz = new string[1];
                        asz[0] = szSymbol.Substring(0, szSymbol.Length - 1).Substring(iSymbolIndexLeft);
                        iSymbolIndexLength = asz[0].Length;
                        Expansion(ref asz);
                        szSymbol = szSymbol.Remove(iSymbolIndexLeft, iSymbolIndexLength);
                        szSymbol = szSymbol.Insert(iSymbolIndexLeft, asz[0]);
                    }

                    // Assume the worse...
                    szValue = "";

                    // Use the value as a JSON key to get data from the response data, if we
                    // don't find the value treat it as an empty string.  In most cases this
                    // will be good enough for testing purposes...
                    if (szSymbol.StartsWith("${rdata:") && szSymbol.StartsWith("${edata:"))
                    {
                        ApiCmd.Transaction transaction = szSymbol.StartsWith("${rdata:") ? m_transactionLast : m_transactionEvent;
                        if (transaction != null)
                        {
                            string szTarget = szSymbol.Substring(0, szSymbol.Length - 1).Substring(8);
                            // Report the number of bytes of data...
                            if (szTarget == "#")
                            {
                                szValue = transaction.GetResponseBytesXferred().ToString();
                            }
                        }
                    }

                    // Use the value as a JSON key to get data from the response data, if we
                    // don't find the value treat it as an empty string.  In most cases this
                    // will be good enough for testing purposes...
                    if (szSymbol.StartsWith("${rj:") || szSymbol.StartsWith("${ej:"))
                    {
                        ApiCmd.Transaction transaction = szSymbol.StartsWith("${rj:") ? m_transactionLast : m_transactionEvent;
                        if (transaction != null)
                        {
                            string szResponseData = transaction.GetResponseData();
                            string szTarget = szSymbol.Substring(0, szSymbol.Length - 1).Substring(5);
                            // Report the number of bytes of data...
                            if (szTarget == "#")
                            {
                                if (string.IsNullOrEmpty(szResponseData))
                                {
                                    szValue = "0";
                                }
                                else
                                {
                                    szValue = Encoding.UTF8.GetBytes(szResponseData).Length.ToString();
                                }
                            }
                            // Get the key...
                            else
                            {
                                // Return the whole thing...
                                if (!string.IsNullOrEmpty(szResponseData))
                                {
                                    bool blSuccess;
                                    long lJsonErrorIndex;
                                    JsonLookup jsonlookup = new JsonLookup();
                                    blSuccess = jsonlookup.Load(szResponseData, out lJsonErrorIndex);
                                    if (blSuccess)
                                    {
                                        szValue = jsonlookup.Get(szTarget);
                                        if (szValue == null)
                                        {
                                            szValue = "";
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Use the value as a JSON key to get data from the response data, add
                    // an existance check, and if we don't find data, return '(null)'...
                    else if (szSymbol.StartsWith("${rjx:") || szSymbol.StartsWith("${ejx:"))
                    {
                        ApiCmd.Transaction transaction = szSymbol.StartsWith("${rjx:") ? m_transactionLast : m_transactionEvent;
                        szValue = "(null)";
                        if (transaction != null)
                        {
                            string szResponseData = transaction.GetResponseData();
                            // Return the whole thing...
                            if (!string.IsNullOrEmpty(szResponseData))
                            {
                                bool blSuccess;
                                long lJsonErrorIndex;
                                JsonLookup jsonlookup = new JsonLookup();
                                blSuccess = jsonlookup.Load(szResponseData, out lJsonErrorIndex);
                                if (blSuccess)
                                {
                                    szValue = jsonlookup.Get(szSymbol.Substring(0, szSymbol.Length - 1).Substring(6));
                                    if (szValue == null)
                                    {
                                        szValue = "(null)";
                                    }
                                }
                            }
                        }
                    }

                    // We're getting the HTTP response status, which is an integer...
                    else if (szSymbol.StartsWith("${rsts:") || szSymbol.StartsWith("${ests:"))
                    {
                        ApiCmd.Transaction transaction = szSymbol.StartsWith("${rsts:") ? m_transactionLast : m_transactionEvent;
                        szValue = "(null)";
                        if (transaction != null)
                        {
                            szValue = transaction.GetResponseStatus().ToString();
                        }
                    }

                    // Get stuff from the session object...
                    else if (szSymbol.StartsWith("${session:"))
                    {
                        int iIndex;
                        szValue = "";
                        string szTarget = szSymbol.Substring(0, szSymbol.Length - 1).Substring(10);

                        // Image blocks value...
                        if (szTarget.StartsWith("imageBlocks[") && szTarget.EndsWith("]"))
                        {
                            if (m_twainlocalscannerclient != null)
                            {
                                string[] szIndex = szTarget.Split(new string[] { "[", "]" }, StringSplitOptions.None);
                                if ((szIndex == null) || (szIndex.Length < 2) || !int.TryParse(szIndex[1], out iIndex))
                                {
                                    DisplayError("badly constructed index for imageBlocks");
                                }
                                else
                                {
                                    long[] lImageBlocks = m_twainlocalscannerclient.ClientGetImageBlocks();
                                    if ((lImageBlocks != null) && (iIndex >= 0) && (iIndex < lImageBlocks.Length))
                                    {
                                        szValue = lImageBlocks[iIndex].ToString();
                                    }
                                }
                            }
                        }

                        // Done capturing...
                        else if (szTarget == "doneCapturing")
                        {
                            szValue = "true";
                            if (m_twainlocalscannerclient != null)
                            {
                                szValue = m_twainlocalscannerclient.ClientGetImageBlocksDrained() ? "true" : "false";
                            }
                        }

                        // Image blocks drained...
                        else if (szTarget == "imageBlocksDrained")
                        {
                            szValue = "true";
                            if (m_twainlocalscannerclient != null)
                            {
                                szValue = m_twainlocalscannerclient.ClientGetImageBlocksDrained() ? "true" : "false";
                            }
                        }

                        // State...
                        else if (szTarget == "state")
                        {
                            szValue = "noSession";
                            if (m_twainlocalscannerclient != null)
                            {
                                szValue = m_twainlocalscannerclient.ClientGetSessionState();
                            }
                        }
                    }

                    // Use value as a GET key to get a value, we don't allow a null in this
                    // case, it has to be an empty string...
                    else if (szSymbol.StartsWith("${get:"))
                    {
                        lock (m_objectKeyValue)
                        {
                            if (m_lkeyvalue.Count >= 0)
                            {
                                string szGet = szSymbol.Substring(0, szSymbol.Length - 1).Substring(6);
                                foreach (KeyValue keyvalue in m_lkeyvalue)
                                {
                                    if (keyvalue.szKey == szGet)
                                    {
                                        szValue = (keyvalue.szValue == null) ? "" : keyvalue.szValue;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    // Get data from the top of the call stack...
                    else if (szSymbol.StartsWith("${arg:"))
                    {
                        if ((m_lcallstack != null) && (m_lcallstack.Count > 0))
                        {
                            string szTarget = szSymbol.Substring(0, szSymbol.Length - 1).Substring(6);
                            callstack = m_lcallstack[m_lcallstack.Count - 1];
                            if (szTarget == "#")
                            {
                                // Needs to be -2 to remove "call xxx" or "run xxx" from count...
                                szValue = (callstack.functionarguments.aszCmd.Length - 2).ToString();
                            }
                            else
                            {
                                int iIndex;
                                if (int.TryParse(szSymbol.Substring(0, szSymbol.Length - 1).Substring(6), out iIndex))
                                {
                                    if ((callstack.functionarguments.aszCmd != null) && (iIndex >= 0) && ((iIndex + 1) < callstack.functionarguments.aszCmd.Length))
                                    {
                                        szValue = callstack.functionarguments.aszCmd[iIndex + 1];
                                    }
                                }
                            }
                        }
                    }

                    // Get data from the return value...
                    else if (szSymbol.StartsWith("${ret:"))
                    {
                        callstack = m_lcallstack[m_lcallstack.Count - 1];
                        if (callstack.functionarguments.szReturnValue != null)
                        {
                            szValue = callstack.functionarguments.szReturnValue;
                        }
                    }

                    // Get keys from the response headers for the last command...
                    else if (szSymbol.StartsWith("${hdrkey:"))
                    {
                        szValue = GetTransactionAtIndex(m_transactionLast.GetResponseHeaders(), szSymbol.Substring(0, szSymbol.Length - 1).Substring(9), true);
                    }

                    // Get values from the response headers for the last command...
                    else if (szSymbol.StartsWith("${hdrvalue:"))
                    {
                        szValue = GetTransactionAtIndex(m_transactionLast.GetResponseHeaders(), szSymbol.Substring(0, szSymbol.Length - 1).Substring(11), false);
                    }

                    // Get keys from the multipart JSON headers for the last command...
                    else if (szSymbol.StartsWith("${hdrjsonkey:"))
                    {
                        szValue = GetTransactionAtIndex(m_transactionLast.GetMultipartHeadersJson(), szSymbol.Substring(0, szSymbol.Length - 1).Substring(13), true);
                    }

                    // Get values from the multipart JSON headers for the last command...
                    else if (szSymbol.StartsWith("${hdrjsonvalue:"))
                    {
                        szValue = GetTransactionAtIndex(m_transactionLast.GetMultipartHeadersJson(), szSymbol.Substring(0, szSymbol.Length - 1).Substring(15), false);
                    }

                    // Get keys from the multipart image headers for the last command...
                    else if (szSymbol.StartsWith("${hdrimagekey:"))
                    {
                        szValue = GetTransactionAtIndex(m_transactionLast.GetMultipartHeadersImage(), szSymbol.Substring(0, szSymbol.Length - 1).Substring(14), true);
                    }

                    // Get values from the multipart image headers for the last command...
                    else if (szSymbol.StartsWith("${hdrimagevalue:"))
                    {
                        szValue = GetTransactionAtIndex(m_transactionLast.GetMultipartHeadersImage(), szSymbol.Substring(0, szSymbol.Length - 1).Substring(16), false);
                    }

                    // Get keys from the multipart thumbnail headers for the last command...
                    else if (szSymbol.StartsWith("${hdrthumbnailkey:"))
                    {
                        szValue = GetTransactionAtIndex(m_transactionLast.GetMultipartHeadersThumbnail(), szSymbol.Substring(0, szSymbol.Length - 1).Substring(18), true);
                    }

                    // Get values from the multipart thumbnail headers for the last command...
                    else if (szSymbol.StartsWith("${hdrthumbnailvalue:"))
                    {
                        szValue = GetTransactionAtIndex(m_transactionLast.GetMultipartHeadersThumbnail(), szSymbol.Substring(0, szSymbol.Length - 1).Substring(20), false);
                    }

                    // Check the mDNS text fields for the currently selected scanner...
                    else if (szSymbol.StartsWith("${txt:"))
                    {
                        string[] aszTxt = m_dnssddeviceinfoSelected.GetTxt();
                        if ((aszTxt != null) && (aszTxt.Length > 0))
                        {
                            string szTxt = szSymbol.Substring(0, szSymbol.Length - 1).Substring(6) + "=";
                            foreach (string sz in aszTxt)
                            {
                                if (sz.StartsWith(szTxt))
                                {
                                    szValue = sz.Substring(szTxt.Length);
                                    break;
                                }
                            }
                        }
                    }

                    // Check the mDNS text fields for the currently selected scanner...
                    else if (szSymbol.StartsWith("${txtx:"))
                    {
                        szValue = "(null)";
                        string[] aszTxt = m_dnssddeviceinfoSelected.GetTxt();
                        if ((aszTxt != null) && (aszTxt.Length > 0))
                        {
                            string szTxt = szSymbol.Substring(0, szSymbol.Length - 1).Substring(7) + "=";
                            foreach (string sz in aszTxt)
                            {
                                if (sz.StartsWith(szTxt))
                                {
                                    szValue = sz.Substring(szTxt.Length);
                                    break;
                                }
                            }
                        }
                    }

                    // Access to the local time...
                    else if (szSymbol.StartsWith("${localtime:"))
                    {
                        DateTime datetime = DateTime.Now;
                        string szFormat = szSymbol.Substring(0, szSymbol.Length - 1).Substring(12);
                        try
                        {
                            szValue = datetime.ToString(szFormat);
                        }
                        catch
                        {
                            szValue = datetime.ToString();
                        }
                    }

                    // Failsafe (we should catch all of these up above)...
                    if (szValue == null)
                    {
                        szValue = "";
                    }

                    // Replace the current contents with the expanded value...
                    a_aszCmd[iCmd] = a_aszCmd[iCmd].Remove(iIndexLeft, (iIndexRight - iIndexLeft) + 1).Insert(iIndexLeft, szValue);
                }
            }
        }

        /// <summary>
        /// A comparison operator for sorting keys in CmdSet...
        /// </summary>
        /// <param name="name1"></param>
        /// <param name="name2"></param>
        /// <returns></returns>
        private int SortByKeyAscending(KeyValue a_keyvalue1, KeyValue a_keyvalue2)
        {

            return (a_keyvalue1.szKey.CompareTo(a_keyvalue2.szKey));
        }

        /// <summary>
        /// Set the return value on the top callstack item...
        /// </summary>
        /// <param name="a_szReturn"></param>
        /// <returns></returns>
        private void SetReturnValue(string a_szReturnValue)
        {
            if (m_lcallstack.Count < 1) return;
            CallStack callstack = m_lcallstack[m_lcallstack.Count - 1];
            callstack.functionarguments.szReturnValue = a_szReturnValue;
            m_lcallstack[m_lcallstack.Count - 1] = callstack;
        }

        /// <summary>
        /// The function that will be called when events arrive...
        /// </summary>
        /// <param name="a_apicmd">the event information</param>
        /// <param name="a_object">our object</param>
        private void WaitForEventsCallbackLaunchpad(ApiCmd a_apicmd, object a_object)
        {
            Terminal terminal = (Terminal)a_object;
            if (terminal != null)
            {
                WaitForEventsCallback(a_apicmd);
            }
        }

        /// <summary>
        /// Process events...
        /// </summary>
        /// <param name="a_apicmd">the event information</param>
        private void WaitForEventsCallback(ApiCmd a_apicmd)
        {
            // Display what's going on, if we're displaying stuff...
            Display("EVENT", !m_blSilentEvents);
            DisplayApicmd(a_apicmd, !m_blSilentEvents, "EVENT - ");

            // If we have a script to call, then call it...
            if ((m_aszWaitForEventsCallback != null) && (m_aszWaitForEventsCallback.Length > 1))
            {
                // Create a transaction we can check out...
                m_transactionEvent = new ApiCmd.Transaction(a_apicmd);

                // Make the call...
                Interpreter.FunctionArguments functionarguments = default(Interpreter.FunctionArguments);
                if (m_blSilentEvents)
                {
                    functionarguments.aszCmd = new string[m_aszWaitForEventsCallback.Length];
                    functionarguments.aszCmd[0] = "run";
                    for (int ii = 1; ii < m_aszWaitForEventsCallback.Length; ii++)
                    {
                        functionarguments.aszCmd[ii] = m_aszWaitForEventsCallback[ii];
                    }
                    CmdRun(ref functionarguments);
                }
                else
                {
                    functionarguments.aszCmd = new string[m_aszWaitForEventsCallback.Length];
                    functionarguments.aszCmd[0] = "runv";
                    for (int ii = 1; ii < m_aszWaitForEventsCallback.Length; ii++)
                    {
                        functionarguments.aszCmd[ii] = m_aszWaitForEventsCallback[ii];
                    }
                    CmdRunv(ref functionarguments);
                }
            }
        }

        #endregion


        // Private Definitions
        #region Private Definitions

        /// <summary>
        /// A key/value pair...
        /// </summary>
        private struct KeyValue
        {
            /// <summary>
            /// Our key...
            /// </summary>
            public string szKey;

            /// <summary>
            /// The key's value...
            /// </summary>
            public string szValue;
        }

        /// <summary>
        /// Call stack info...
        /// </summary>
        private struct CallStack
        {
            /// <summary>
            /// The arguments to this call...
            /// </summary>
            public Interpreter.FunctionArguments functionarguments;
        }

        #endregion


        // Private Attributes
        #region Private Attributes

        /// <summary>
        /// Map commands to functions...
        /// </summary>
        private List<Interpreter.DispatchTable> m_ldispatchtable;

        /// <summary>
        /// Our console input...embiggened...
        /// </summary>
        private StreamReader m_streamreaderConsole;

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
        private TwainLocalScannerClient m_twainlocalscannerclient;

        /// <summary>
        /// Script to call when waitForEvents returns events...
        /// </summary>
        private string[] m_aszWaitForEventsCallback;

        /// <summary>
        /// Our object for discovering TWAIN Local scanners...
        /// </summary>
        private Dnssd m_dnssd;

        /// <summary>
        /// No output when this is true...
        /// </summary>
        private bool m_blSilent;
        private bool m_blSilentEvents;

        /// <summary>
        /// A record of the last transaction on the API, this
        /// doesn't include events...
        /// </summary>
        private ApiCmd.Transaction m_transactionLast;

        /// <summary>
        /// The event we're currently processing...
        /// </summary>
        private ApiCmd.Transaction m_transactionEvent;

        /// <summary>
        /// The list of key/value pairs created by the SET command...
        /// </summary>
        private List<KeyValue> m_lkeyvalue;
        private object m_objectKeyValue;

        /// <summary>
        /// A last in first off stack of function calls...
        /// </summary>
        private List<CallStack> m_lcallstack;

        /// <summary>
        /// The opening banner (program, version, etc)...
        /// </summary>
        private string m_szBanner;

        #endregion
    }
}
