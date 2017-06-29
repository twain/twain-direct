///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.Support.Interpreter
//
// A simple interpreter for console mode stuff...
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    12-Jun-2017     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2017-2017 Kodak Alaris Inc.
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
using System.IO;
using System.Text;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;

namespace TwainDirect.Support
{


    /// <summary>
    /// Interpret and dispatch user commands...
    /// </summary>
    public sealed class Interpreter
    {
        // Public Methods
        #region Public Methods

        /// <summary>
        /// Our constructor...
        /// </summary>
        /// <param name="a_szPrompt">initialize the prompt</param>
        public Interpreter(string a_szPrompt)
        {
            // Our prompt...
            m_szPrompt = (string.IsNullOrEmpty(a_szPrompt) ? ">>>" : a_szPrompt);
        }

        /// <summary>
        /// Create a console on Windows...
        /// </summary>
        public static StreamReader CreateConsole()
        {
            // Make sure we have a console...
            if (TwainLocalScanner.GetPlatform() == TwainLocalScanner.Platform.WINDOWS)
            {
                // Get our console...
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

            // And because life is hard, we need to up the size of standard input...
            StreamReader streamreaderConsole = new StreamReader(Console.OpenStandardInput(65536));
            return (streamreaderConsole);
        }

        /// <summary>
        /// Get the desktop windows for Windows systems...
        /// </summary>
        /// <returns></returns>
        public static IntPtr GetDesktopWindow()
        {
            // Get an hwnd...
            if (TwainLocalScanner.GetPlatform() == TwainLocalScanner.Platform.WINDOWS)
            {
                return (NativeMethods.GetDesktopWindow());
            }
            else
            {
                return (IntPtr.Zero);
            }
        }

        /// <summary>
        /// Prompt for input, returning a string, if there's any data...
        /// </summary>
        /// <param name="a_streamreaderConsole">the console to use</param>
        /// <returns>data captured</returns>
        public string Prompt(StreamReader a_streamreaderConsole)
        {
            string szCmd;

            // Read in a line...
            while (true)
            {
                // Write out the prompt...
                Console.Out.Write(m_szPrompt);

                // Read in a line...
                szCmd = (a_streamreaderConsole == null) ? Console.In.ReadLine() : a_streamreaderConsole.ReadLine();
                if (string.IsNullOrEmpty(szCmd))
                {
                    continue;
                }

                // Trim whitespace...
                szCmd = szCmd.Trim();
                if (string.IsNullOrEmpty(szCmd))
                {
                    continue;
                }

                // We must have data...
                break;
            }

            // All done...
            return (szCmd);
        }

        /// <summary>
        /// Change the prompt...
        /// </summary>
        /// <param name="a_szPrompt">new prompt</param>
        public void SetPrompt(string a_szPrompt)
        {
            m_szPrompt = a_szPrompt;
        }

        /// <summary>
        /// Tokenize a string, with support for single quotes and double quotes.
        /// Inside the body of a quote the only thing can can be (or needs to be)
        /// escaped is the current quote token.  The result is an array of strings...
        /// </summary>
        /// <param name="a_szCmd">command to tokenize</param>
        /// <returns>array of strings</returns>
        public string[] Tokenize(string a_szCmd)
        {
            int cc;
            int tt;
            char szQuote;
            string[] aszTokens;

            // We're coming out of this with at least one token...
            aszTokens = new string[1];
            tt = 0;

            // Validate...
            if (string.IsNullOrEmpty(a_szCmd))
            {
                aszTokens[tt] = "";
                return (aszTokens);
            }

            // Handle comments...
            if (a_szCmd[0] == ';')
            {
                aszTokens[tt] = "";
                return (aszTokens);
            }

            // Skip over goto labels...
            if (a_szCmd[0] == ':')
            {
                aszTokens[tt] = "";
                return (aszTokens);
            }

            // If we have no special characters, then we're done...
            if (a_szCmd.IndexOfAny(new char[] { ' ', '\t', '\'', '"' }) == -1)
            {
                aszTokens[tt] = a_szCmd;
                return (aszTokens);
            }

            // Devour leading whitespace...
            cc = 0;
            while ((cc < a_szCmd.Length) && ((a_szCmd[cc] == ' ') || (a_szCmd[cc] == '\t')))
            {
                cc += 1;
            }

            // Loopy...
            while (cc < a_szCmd.Length)
            {
                // Handle single and double quotes...
                if ((a_szCmd[cc] == '\'') || (a_szCmd[cc] == '"'))
                {
                    // Skip the quote...
                    szQuote = a_szCmd[cc];
                    cc += 1;

                    // Copy all of the string to the next unescaped single quote...
                    while (cc < a_szCmd.Length)
                    {
                        // We found our terminator (don't copy it)...
                        if (a_szCmd[cc] == szQuote)
                        {
                            cc += 1;
                            break;
                        }

                        // We're escaping the quote...
                        if ((cc + 1 < a_szCmd.Length) && (a_szCmd[cc] == '\\') && (a_szCmd[cc + 1] == szQuote))
                        {
                            aszTokens[tt] += szQuote;
                            cc += 1;
                        }

                        // Otherwise, just copy the character...
                        else
                        {
                            aszTokens[tt] += a_szCmd[cc];
                        }

                        // Next character...
                        cc += 1;
                    }
                }

                // Handle whitespace...
                else if ((a_szCmd[cc] == ' ') || (a_szCmd[cc] == '\t'))
                {
                    // Devour all of the whitespace...
                    while ((cc < a_szCmd.Length) && ((a_szCmd[cc] == ' ') || (a_szCmd[cc] == '\t')))
                    {
                        cc += 1;
                    }

                    // If we have more data, prep for it...
                    if (cc < a_szCmd.Length)
                    {
                        string[] asz = new string[aszTokens.Length + 1];
                        Array.Copy(aszTokens, asz, aszTokens.Length);
                        asz[aszTokens.Length] = "";
                        aszTokens = asz;
                        tt += 1;
                    }
                }

                // Anything else is data in the current token...
                else
                {
                    aszTokens[tt] += a_szCmd[cc];
                    cc += 1;
                }

                // Next character.,
            }

            // All done...
            return (aszTokens);
        }

        /// <summary>
        /// Dispatch a command...
        /// </summary>
        /// <param name="a_functionarguments">the arguments to the command</param>
        /// <param name="a_dispatchtable">dispatch table</param>
        /// <returns>true if the program should exit</returns>
        public bool Dispatch(ref FunctionArguments a_functionarguments, List<DispatchTable> a_ldispatchtable)
        {
            string szCmd;

            // Apparently we got nothing, it's a noop...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length == 0) || string.IsNullOrEmpty(a_functionarguments.aszCmd[0]))
            {
                return (false);
            }

            // Find the command...
            szCmd = a_functionarguments.aszCmd[0].ToLowerInvariant();
            foreach (DispatchTable dispatchtable in a_ldispatchtable)
            {
                foreach (string sz in dispatchtable.m_aszCmd)
                {
                    if (sz == szCmd)
                    {
                        return (dispatchtable.m_function(ref a_functionarguments));
                    }
                }
            }

            // No joy, make sure to lose the last transaction if the
            // user enters a bad command, so that we reduce the risk
            // of it be badlu interpreted later on...
            Console.Out.WriteLine("command not found: " + a_functionarguments.aszCmd[0]);
            a_functionarguments.transaction = null;
            return (false);
        }

        #endregion


        // Public Definitions
        #region Public Definitions

        public struct FunctionArguments
        {
            /// <summary>
            /// The tokenized command...
            /// </summary>
            public string[] aszCmd;

            /// <summary>
            /// The script we're running or null, used for
            /// commands like "goto"...
            /// </summary>
            public string[] aszScript;

            /// <summary>
            /// True if we've been asked to jump to a label,
            /// which includes the index to go to...
            /// </summary>
            public bool blGotoLabel;
            public int iLabelLine;

            /// <summary>
            /// The function value when returning from a call...
            /// </summary>
            public string szReturnValue;

            /// <summary>
            /// The current line in the script...
            /// </summary>
            public int iCurrentLine;

            /// <summary>
            /// Clears or records the last API transaction...
            /// </summary>
            public ApiCmd.Transaction transaction;
        }

        /// <summary>
        /// Function to call from the Dispatcher...
        /// </summary>
        /// <param name="a_aszCmd">arguments</param>
        /// <param name="a_aszScript">script or null</param>
        /// <returns>true if the program should exit</returns>
        public delegate bool Function(ref FunctionArguments a_functionarguments);

        /// <summary>
        /// Map commands to functions...
        /// </summary>
        public class DispatchTable
        {
            /// <summary>
            /// Stock our entries...
            /// </summary>
            /// <param name="a_function">the function</param>
            /// <param name="a_aszCmd">command variants for this function</param>
            public DispatchTable(Function a_function, string[] a_aszCmd)
            {
                m_aszCmd = a_aszCmd;
                m_function = a_function;
            }

            /// <summary>
            /// Variations for this command...
            /// </summary>
            public string[] m_aszCmd;

            /// <summary>
            /// Function to call...
            /// </summary>
            public Function m_function;
        }

        #endregion


        // Private Attributes
        #region Private Attributes

        /// <summary>
        /// Our prompt...
        /// </summary>
        private string m_szPrompt;

        #endregion
    }
}
