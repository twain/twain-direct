///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirectSupport.Config
//
// One stop shop for configuration and command line argument data...
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    11-Sep-2015     Initial Release
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

namespace TwainDirectSupport
{
    /// <summary>
    /// Our configuration object.  We must be able to access this
    /// from anywhere in the code...
    /// </summary>
    public static class Config
    {
        // Public Methods...
        #region Public Methods...

        /// <summary>
        /// Load the configuration object.  We want to read in the
        /// configuaration data (in JSON format) and a list of the
        /// command line arguments.
        /// </summary>
        /// <param name="a_szExecutablePath">the fill path to the program using us</param>
        /// <param name="a_szCommandLine">key[=value] groupings</param>
        /// <param name="a_szConfigFile">a JSON file</param>
        public static bool Load(string a_szExecutablePath, string[] a_aszCommandLine, string a_szConfigFile)
        {
            try
            {
                // Set up our folders...
                ms_szExecutablePath = a_szExecutablePath;
                ms_szExecutableName = Path.GetFileNameWithoutExtension(ms_szExecutablePath);
                ms_szWriteFolder = ms_szExecutablePath.Split(new string[] { ms_szExecutableName }, StringSplitOptions.None)[0];
                ms_szReadFolder = Path.Combine(ms_szWriteFolder, ms_szExecutableName);
                ms_szWriteFolder = Path.Combine(ms_szWriteFolder, "data");
                ms_szWriteFolder = Path.Combine(ms_szWriteFolder, ms_szExecutableName);
                if (!Directory.Exists(ms_szWriteFolder))
                {
                    Directory.CreateDirectory(ms_szWriteFolder);
                }

                // Store the command line...
                ms_aszCommandLine = a_aszCommandLine;

                // Load the config...
                string szConfigFile = Path.Combine(ms_szReadFolder, a_szConfigFile);
                if (File.Exists(szConfigFile))
                {
                    long a_lJsonErrorindex;
                    string szConfig = File.ReadAllText(szConfigFile);
                    ms_jsonlookup = new JsonLookup();
                    ms_jsonlookup.Load(szConfig, out a_lJsonErrorindex);
                }
            }
            catch
            {
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Get a value, if the value can't be found, return a default.  We look
        /// for the item first in the command line arguments, then in the data
        /// read from the config file, and finally based on a small collection of
        /// keywords accessing static information.  This way we can override the
        /// configuration at any point with a minimum of fuss.
        /// </summary>
        /// <param name="a_szKey">the item we're seeking</param>
        /// <param name="a_szDefault">the default if we don't find it</param>
        /// <returns>the result</returns>
        public static string Get(string a_szKey, string a_szDefault)
        {
            // Try the command line first...
            if (ms_aszCommandLine != null)
            {
                string szKey = a_szKey + "=";
                foreach (string sz in ms_aszCommandLine)
                {
                    if ((sz == a_szKey) || (sz == szKey))
                    {
                        return ("");
                    }
                    if (sz.StartsWith(szKey))
                    {
                        return (sz.Remove(0, szKey.Length));
                    }
                }
            }

            // Try the JSON...
            if (ms_jsonlookup != null)
            {
                string szValue;
                JsonLookup.EPROPERTYTYPE epropertytype;
                if (ms_jsonlookup.GetCheck(a_szKey, out szValue, out epropertytype, false))
                {
                    return (szValue);
                }
            }

            // Try the folders...
            if (a_szKey == "executablePath")
            {
                return (ms_szExecutablePath);
            }
            if (a_szKey == "executableName")
            {
                return (ms_szExecutableName);
            }
            if (a_szKey == "readFolder")
            {
                return (ms_szReadFolder);
            }
            if (a_szKey == "writeFolder")
            {
                return (ms_szWriteFolder);
            }

            // All done...
            return (a_szDefault);
        }

        /// <summary>
        /// Get a long value, if the value can't be found, return a default...
        /// </summary>
        /// <param name="a_szKey"></param>
        /// <param name="a_szDefault"></param>
        /// <returns></returns>
        public static long Get(string a_szKey, long a_lDefault)
        {
            // Get the value...
            string szValue = Get(a_szKey, "@@@NOTFOUND@@@@");

            // We didn't find it, use the default...
            if (szValue == "@@@NOTFOUND@@@@")
            {
                return (a_lDefault);
            }

            // Try to get the value...
            long lValue;
            if (long.TryParse(szValue, out lValue))
            {
                return (lValue);
            }

            // No joy, use the default...
            return (a_lDefault);
        }

        /// <summary>
        /// Get a double value, if the value can't be found, return a default...
        /// </summary>
        /// <param name="a_szKey"></param>
        /// <param name="a_szDefault"></param>
        /// <returns></returns>
        public static double Get(string a_szKey, double a_dfDefault)
        {
            // Get the value...
            string szValue = Get(a_szKey, "@@@NOTFOUND@@@@");

            // We didn't find it, use the default...
            if (szValue == "@@@NOTFOUND@@@@")
            {
                return (a_dfDefault);
            }

            // Try to get the value...
            double dfValue;
            if (double.TryParse(szValue, out dfValue))
            {
                return (dfValue);
            }

            // No joy, use the default...
            return (a_dfDefault);
        }

        #endregion


        // Private Attributes...
        #region Private Attributes

        /// <summary>
        /// The command line arguments...
        /// </summary>
        private static string[] ms_aszCommandLine = null;

        /// <summary>
        /// The JSON lookup object that contains any configuration data
        /// tht we want to access using a Get() command...
        /// </summary>
        private static JsonLookup ms_jsonlookup = null;

        /// <summary>
        /// The full path to our program...
        /// </summary>
        private static string ms_szExecutablePath;

        /// <summary>
        /// The base name, without extension of our program...
        /// </summary>
        private static string ms_szExecutableName;

        /// <summary>
        /// The readonly folder, which includes binaries and template
        /// files that are never updated by running code...
        /// </summary>
        private static string ms_szReadFolder;

        /// <summary>
        /// The writeable folder, which is where logs, temporary files,
        /// and configurable content is located...
        /// </summary>
        private static string ms_szWriteFolder;

        #endregion
    }
}
