///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.Support.Log
//
// Log stuff.  Yeah, it's different from TWAIN.CS...oh well...
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    31-Oct-2014     Initial Release
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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading;

namespace TwainDirect.Support
{
    /// <summary>
    /// Our logger.  If we bump up to 4.5 (and if mono supports it at compile
    /// time), then we'll be able to add the following to our traces, which
    /// seems like it should be more than enough to locate log messages.  For
    /// now we'll leave the log messages undecorated:
    ///     [CallerFilePath] string file = "",
    ///     [CallerMemberName] string member = "",
    ///     [CallerLineNumber] int line = 0
    /// </summary>
    public static class Log
    {
        // Public Methods...
        #region Public Methods...

        /// <summary>
        /// Write an assert message, but only throw with a debug build...
        /// </summary>
        /// <param name="a_szMessage">message to log</param>
        public static void Assert(string a_szMessage)
        {
            WriteEntry("A", a_szMessage, true);
            #if DEBUG
                throw new Exception(a_szMessage);
            #endif
        }

        /// <summary>
        /// Close tracing...
        /// </summary>
        public static void Close()
        {
            if (!ms_blFirstPass)
            {
                Trace.Close();
                ms_filestream.Close();
                ms_filestream = null;
            }
            ms_blFirstPass = true;
            ms_blOpened = false;
            ms_blFlush = false;
            ms_iMessageNumber = 0;
        }

        /// <summary>
        /// Write an error message...
        /// </summary>
        /// <param name="a_szMessage">message to log</param>
        public static void Error(string a_szMessage)
        {
            WriteEntry("E", a_szMessage, true);
        }

        /// <summary>
        /// Get the debugging level...
        /// </summary>
        /// <returns>the level</returns>
        public static int GetLevel()
        {
            return (ms_iLevel);
        }

        /// <summary>
        /// Write an informational message...
        /// </summary>
        /// <param name="a_szMessage">message to log</param>
        public static void Info(string a_szMessage)
        {
            WriteEntry(".", a_szMessage, ms_blFlush);
        }

        /// <summary>
        /// Turn on the listener for our log file...
        /// </summary>
        /// <param name="a_szName">the name of our log</param>
        /// <param name="a_szPath">the path where we want our log to go</param>
        /// <param name="a_iLevel">debug level</param>
        public static void Open(string a_szName, string a_szPath, int a_iLevel)
        {
            // Init stuff...
            ms_blFirstPass = true;
            ms_blOpened = true;
            ms_blFlush = false;
            ms_iMessageNumber = 0;
            ms_iLevel = a_iLevel;

            // We're Windows...
            if (Environment.OSVersion.ToString().Contains("Microsoft Windows"))
            {
                ms_blIsWindows = true;
            }

            // Ask for a TWAINDSM log...
            if (a_iLevel > 0)
            {
                Environment.SetEnvironmentVariable("TWAINDSM_LOG", Path.Combine(a_szPath, "twaindsm.log"));
                Environment.SetEnvironmentVariable("TWAINDSM_MODE", "w");
            }

            // Turn on the listener...
            ms_filestream = File.Open(Path.Combine(a_szPath, a_szName + ".log"), FileMode.Append, FileAccess.Write, FileShare.Read);
            Trace.Listeners.Add(new TextWriterTraceListener(ms_filestream, a_szName + "Listener"));
        }

        /// <summary>
        /// Set the debugging level
        /// </summary>
        /// <param name="a_iLevel"></param>
        public static void SetLevel(int a_iLevel)
        {
            ms_iLevel = a_iLevel;
        }

        /// <summary>
        /// Flush data to the file...
        /// </summary>
        public static void SetFlush(bool a_blFlush)
        {
            ms_blFlush = a_blFlush;
            if (a_blFlush)
            {
                Trace.Flush();
            }
        }

        /// <summary>
        /// Set our state delegate...
        /// </summary>
        public static void SetStateDelegate(GetStateDelegate a_getstatedelegate)
        {
            GetState = a_getstatedelegate;
        }

        /// <summary>
        /// Write a verbose message...
        /// </summary>
        /// <param name="a_szMessage">message to log</param>
        public static void Verbose(string a_szMessage)
        {
            WriteEntry("V", a_szMessage, ms_blFlush);
        }

        /// <summary>
        /// Write an warning message...
        /// </summary>
        /// <param name="a_szMessage">message to log</param>
        public static void Warn(string a_szMessage)
        {
            WriteEntry("W", a_szMessage, ms_blFlush);
        }

        /// <summary>
        /// Do this for all of them...
        /// </summary>
        /// <param name="a_szMessage">The message</param>
        /// <param name="a_szSeverity">Message severity</param>
        /// <param name="a_blFlush">Flush it to disk</param>
        [EnvironmentPermissionAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        public static void WriteEntry(string a_szSeverity, string a_szMessage, bool a_blFlush)
        {
            long lThreadId;

            // Filter...
            switch (a_szSeverity)
            {
                // Always log these...
                case "A": break;
                case "E": break;
                case "W": break;

                // Log informationals when bit-0 is set...
                case ".":
                    if ((ms_iLevel & 0x0001) != 0)
                    {
                        break;
                    }
                    return;

                // Log verbose when bit-1 is set...
                case "V":
                    if ((ms_iLevel & 0x0002) != 0)
                    {
                        a_szSeverity = ".";
                        break;
                    }
                    return;
            }

            // Get our thread id...
            if (ms_blIsWindows)
            {
                lThreadId = NativeMethods.GetCurrentThreadId();
            }
            else
            {
                lThreadId = Thread.CurrentThread.ManagedThreadId; // AppDomain.GetCurrentThreadId();
            }

            // First pass...
            if (ms_blFirstPass)
            {
                string szPlatform;

                // We're Windows...
                if (Environment.OSVersion.ToString().Contains("Microsoft Windows"))
                {
                    szPlatform = "windows";
                }

                // We're Mac OS X (this has to come before LINUX!!!)...
                else if (Directory.Exists("/Library/Application Support"))
                {
                    szPlatform = "macosx";
                }

                // We're Linux...
                else if (Environment.OSVersion.ToString().Contains("Unix"))
                {
                    szPlatform = "linux";
                }

                // We have a problem, Log will throw for us...
                else
                {
                    szPlatform = "unknown";
                }

                if (!ms_blOpened)
                {
                    // We'll assume they want logging, since they didn't tell us...
                    Open("Twain", ".", 1);
                }
                Trace.UseGlobalLock = true;
                ms_blFirstPass = false;
                Trace.WriteLine
                (
                    string.Format
                    (
                        "{0:D6} {1} T{2:D8} {3} V{4} ts:{5} os:{6}",
                        ms_iMessageNumber++,
                        DateTime.Now.ToString("HHmmssffffff"),
                        lThreadId,
                        (GetState == null) ? "S0" : GetState(),
                        a_szSeverity.ToString(),
                        DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.ffffff"),
                        szPlatform
                    )
                );
            }

            // And log it...
            Trace.WriteLine
            (
                string.Format
                (
                    "{0:D6} {1} T{2:D8} {3} V{4} {5}",
                    ms_iMessageNumber++,
                    DateTime.Now.ToString("HHmmssffffff"),
                    lThreadId,
                    (GetState == null) ? "S0" : GetState(),
                    a_szSeverity.ToString(),
                    a_szMessage
                )
            );

            // Flush it...
            if (a_blFlush)
            {
                Trace.Flush();
            }
        }

        #endregion


        // Public Definitions...
        #region Public Definitions...

        /// <summary>
        /// Our severity levels...
        /// </summary>
        public enum Severity
        {
            Info,
            Warning,
            Error,
            Throw
        }

        /// <summary>
        /// We use this to get state info from other entities...
        /// </summary>
        /// <returns></returns>
        public delegate string GetStateDelegate();

        #endregion



        // Private Methods...
        #region Private Methods

        /// <summary>
        /// A place holder if we don't have a way to get state info...
        /// </summary>
        /// <returns>S0</returns>
        private static string GetStateLocal()
        {
            return ("S0");
        }

        #endregion


        // Private Attributes...
        #region Private Attributes

        private static bool ms_blFirstPass = true;
        private static bool ms_blOpened = false;
        private static bool ms_blFlush = false;
        private static int ms_iMessageNumber = 0;
        private static int ms_iLevel = 0;
        private static bool ms_blIsWindows = false;
        private static FileStream ms_filestream = null;

        /// <summary>
        /// We can override this with a function that will give us
        /// a state number whenever we record a line in the log...
        /// </summary>
        public static GetStateDelegate GetState = GetStateLocal;

        #endregion
    }
}
