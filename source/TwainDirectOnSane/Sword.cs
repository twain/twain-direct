///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirectOnSane.Sword
//
//  Use a TWAIN Direct task to control a TWAIN driver.  This is a general solution
//  that represents standard TWAIN Direct on standard TWAIN.  Other schemes are
//  needed to support the custom features that may be available for a scanner.
//
//  We'd like to support as many TWAIN scanners as possible.  Right now the only
//  firm requirement is that the driver must report a value of TRUE for
//  CAP_UICONTROLLABLE.  It's possible that this code will run as a service, so
//  it can't allow access to a user interface.
//
//  We're currently assuming the use of 32-bit drivers.  Changing to 64-bit drivers
//  is as easy as changing the configuration for the project.  Avoid using AnyCPU
//  for now, since the number of native 64-bit TWAIN drivers is still small.
//
//  Yes, we're aware of the irony of "Scanning WithOut Requiring Drivers" using a
//  legacy TWAIN driver... :)
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    21-Aug-2015     Initial Release
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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using TwainDirectSupport;

namespace TwainDirectOnSane
{
    /// <summary>
    /// Manage the SWORD interface.  This section shows how to make use of a
    /// SwordTask and a SaneTask to run a TWAIN scanning session.  It's also
    /// the public interface to the caller.  Nothing else should be exposed
    /// other than the contents of the SwordManager.
    /// </summary>
    public sealed class Sword
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Init stuff...
        /// </summary>
        public Sword()
        {
            // Remember this stuff...
            m_szWriteFolder = Config.Get("writeFolder", "");
            m_blTwainLocal = (Config.Get("twainlocal", null) != null);

            // The TWAIN version of the SWORD task...
            m_sanetask = null;

            // Our TWAIN toolkit and processing state...
            m_blProcessing = false;
            //m_blCancel = false;

            // Make the analyzer happy until we fix this module...
            m_szTwainDriverIdentity = "";
            m_guidScanner = default(Guid);
        }

        /// <summary>
        /// Used with the _sword._task tests...
        /// </summary>
        /// <param name="a_szScanner">scanner's TWAIN product name</param>
        /// <param name="a_szTask">the task (JSON)</param>
        /// <param name="a_blIgnoreTaskScan">ingnore the scan action</param>
        /// <param name="a_swordtask">the object we'll be using for this task</param>
        /// <returns></returns>
        public bool BatchMode
        (
            string a_szScanner,
            string a_szTask,
            bool a_blIgnoreTaskScan,
            ref SwordTask a_swordtask,
            ref bool a_blSetAppCapabilities,
            out string a_szScanImageArguments
        )
        {
           bool blStatus;
           bool blError;
           TwainDirectSupport.Log.Info("Batch mode starting...");

            // Init stuff...
            blStatus = false;
            a_szScanImageArguments = "";

            // Cover our butts as best we can, because sometimes bad things
            // happen.
            try
            {
                string szTask;

                // If it starts with a '{', then it's raw data...
                if (a_szTask.StartsWith("{"))
                {
                    szTask = a_szTask;
                }

                // TBD
                // Else read the data from a file...this is old stuff for when
                // we were doing cURL, it can probably go away...
                else
                {
                    szTask = System.IO.File.ReadAllText(a_szTask);
                }

                // Do something with it...
                TwainDirectSupport.Log.Info("Analyzing the task...");
                blStatus = SwordDeserialize(szTask, m_guidScanner, ref a_swordtask);
                if (!blStatus)
                {
                    return (false);
                }

                // SANE task time...
                m_sanetask = new SaneTask(a_swordtask, m_szWriteFolder, ref a_szScanImageArguments);

                // Process each action in turn.
                // TBD: some kind of event trigger would be nicer than polling.
                // and we'll need a way to process commands so that information
                // can be requested or cancels can be issued.
                TwainDirectSupport.Log.Info("Running the task...");
                for (blStatus = Process(a_szScanner, true, a_blIgnoreTaskScan, out blError, ref a_swordtask, ref a_blSetAppCapabilities);
                     blStatus && !blError;
                     blStatus = NextAction(out blError, ref a_swordtask, ref a_blSetAppCapabilities))
                {
                    // Wait for the action to finish.
                    while (IsProcessing())
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            catch
            {
                TwainDirectSupport.Log.Error("Batch mode threw exception on error...");
                TwainDirectSupport.Log.Error("exception: " + a_swordtask.GetException());
                TwainDirectSupport.Log.Error("location: " + a_swordtask.GetJsonExceptionKey());
                TwainDirectSupport.Log.Error("SWORD value: " + a_swordtask.GetSwordValue());
                TwainDirectSupport.Log.Error("TWAIN value: " + a_swordtask.GetTwainValue());
                return (false);
            }
            if (blError)
            {
                TwainDirectSupport.Log.Error("Batch mode completed on error...");
                TwainDirectSupport.Log.Error("exception: " + a_swordtask.GetException());
                TwainDirectSupport.Log.Error("location: " + a_swordtask.GetJsonExceptionKey());
                TwainDirectSupport.Log.Error("SWORD value: " + a_swordtask.GetSwordValue());
                TwainDirectSupport.Log.Error("TWAIN value: " + a_swordtask.GetTwainValue());
                return (false);
            }

            // Cleanup and scoot...
            Close();
            TwainDirectSupport.Log.Info("Batch mode completed...");
            return (true);
        }

        /// <summary>
        /// Run the task passed into us...
        /// </summary>
        /// <param name="a_szTask">task to process</param>
        /// <returns>true on success</returns>
        public bool RunTask(string a_szTask)
        {
            bool blStatus;
            bool blError;
            bool blSetAppCapabilities = false;
            string szScanImageArguments = "";
            SwordTask swordtask = new SwordTask();

            // Parse the task into a SWORD object...
            TwainDirectSupport.Log.Info("JSON to SWORD...");
            blStatus = SwordDeserialize(a_szTask, m_guidScanner, ref swordtask);
            if (!blStatus)
            {
                TwainDirectSupport.Log.Error("SwordDeserialize failed...");
                return (false);
            }

            // Parse the SWORD object into a TWAIN object...
            TwainDirectSupport.Log.Info("SWORD to TWAIN...");
            m_sanetask = new SaneTask(swordtask, m_szWriteFolder, ref szScanImageArguments);
            if (m_sanetask == null)
            {
                TwainDirectSupport.Log.Error("SaneTask failed...");
                return (false);
            }

            // Process each action in turn.
            // TBD: some kind of event trigger would be nicer than polling.
            // and we'll need a way to process commands so that information
            // can be requested or cancels can be issued.
            TwainDirectSupport.Log.Info("Task begins...");
            for (blStatus = Process("", true, true, out blError, ref swordtask, ref blSetAppCapabilities);
                 blStatus;
                 blStatus = NextAction(out blError, ref swordtask, ref blSetAppCapabilities))
            {
                // Wait for the action to finish.
                while (IsProcessing())
                {
                    Thread.Sleep(100);
                }
            }
            TwainDirectSupport.Log.Info("Task completed...");

            // All done...
            return (true);
        }

        /// <summary>
        /// Get the driver that we'll be using (this also allows us to
        /// check that we have a driver that we can use).
        /// </summary>
        /// <returns>The currently selected driver or null, if there are no drivers</returns>
        public static string GetCurrentDriver(string a_szWriteFolder, string a_szScanner)
        {
            string szTwainDefaultDriver;
            Sword sword;

            // Create the SWORD manager...
            sword = new Sword();

            // Check for a TWAIN driver...
            szTwainDefaultDriver = sword.TwainGetDefaultDriver(a_szScanner);

            // Cleanup...
            sword.Close();
            sword = null;
            return (szTwainDefaultDriver);
        }

        /// <summary>
        /// Return both names and information about the SANE drivers installed on the
        /// system, which means that we have to open them to ask questions.  And that
        /// means that the scanner has to be turned on if we're going to be able to talk
        /// to it.
        /// 
        /// This is our one chance to filter out potential problem drivers.  We're not
        /// being too aggressive about this, since a user can always remove a driver
        /// that's not behaving well.
        /// 
        /// The worst scenerio we can run into is a driver that behaves badly (hangs or
        /// crashes) just talking to it, since that can prevent access to other better
        /// behaved drivers.  However, this is a problem for TWAIN CS to solve, not for
        /// TWAIN Direct on SANE, so focus any efforts there instead of here.
        /// </summary>
        /// <returns>The list of drivers</returns>
        public static string SaneListDrivers()
        {
            string szList = "";
            string[] aszScanners;
            Process process = null;

            // Ask for the list of scanners...
            aszScanners = Program.ScanImage("ScannerList", "-f \"scanner,%d,%v,%m,%t,%i,%n\"", ref process, null);
            if ((aszScanners == null) || (aszScanners.Length == 0) || !aszScanners[0].StartsWith("scanner"))
            {
                TwainDirectSupport.Log.Info("No scanners found...");
                return (szList);
            }

            // Okay, we have a list of scanners, so now let's get help on each
            // one of them to get the capabilities we can support...
            szList =  "{\n";
            szList += "    \"scanners\": [\n";
            foreach (string szScanner in aszScanners)
            {
                // Extract the devicename...
                string[] aszDevice = szScanner.Split(new char[] { ',' });
                if (aszDevice.Length < 2)
                {
                    continue;
                }

                // TBD -d doesn't work with avision!!!
                // Issue a help command...
                //string[] aszHelp = Program.ScanImage("ScannerHelp", "--help -d " + aszDevice[1], ref process, null);
                string[] aszHelp = Program.ScanImage("ScannerHelp", "--help", ref process, null);

                // Build an object to add to the list, this is a scratchpad, so if we
                // have to abandon it mid-way through, it's no problem...
                string szObject = (!szList.Contains("sane")) ? "        {\n" : ",\n        {\n";
                szObject += "            \"sane\": \"" + aszDevice[1] + "\",\n";


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // hostName: this allows us to pair a scanner with a PC, which is needed if the user
                // has access to more than one scanner of the same model...
                #region hostName...

                try
                {
                    szObject += "            \"hostName\": \"" + Dns.GetHostName() + "\",\n";
                }
                catch (Exception exception)
                {
                    TwainDirectSupport.Log.Info("Failed to get hostName: " + exception.Message);
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // serialNumber: this allows us to uniquely identify a scanner, it's debatable if we
                // need both the hostname and the serial number, but for now that's what we're doing...
                #region serialNumber...

                // We don't have one of these...
                szObject += "            \"serialNumber\": \"(no serial number)\",\n";

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // sources...
                #region sources...

                // Just handle any...
                szObject += "            \"source\": [\"any\", \"feeder\"],\n";

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // numberOfSheets...
                #region numberOfSheets...

                szObject += "            \"numberOfSheets\": [1,32767],\n";

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // resolutions...
                #region resolutions...

                // Find the line that has the resolutions...
                foreach (string sz in aszHelp)
                {
                    if (sz.StartsWith("    --resolution "))
                    {
                        string szResList = "";

                        // Enum...
                        if (sz.Contains("|"))
                        {
                            string[] aszNumbers = sz.Split(new char[] { ' ', '\t', '|' });
                            if ((aszNumbers == null) || (aszNumbers.Length < 3))
                            {
                                continue;
                            }
                            foreach (string szNumber in aszNumbers)
                            {
                                int iNumber;
                                if (int.TryParse(szNumber, out iNumber))
                                {
                                    szResList += (szResList == "") ? iNumber.ToString() : ("," + iNumber);
                                }
                            }
                            if (szResList == "")
                            {
                                continue;
                            }
                        }

                        // Range...
                        else if (sz.Contains(".."))
                        {
                            string[] aszNumbers = sz.Split(new string[] { "    --resolution ", "..", "dpi" }, StringSplitOptions.RemoveEmptyEntries);
                            int iMin = int.Parse(aszNumbers[0]);
                            int iMax = int.Parse(aszNumbers[1]);
                            foreach (int ii in new int[] {75, 100, 150, 200, 240, 250, 300, 600})
                            {
                                if ((iMin >= ii) && (ii <= iMax)) szResList += (szResList == "") ? ii.ToString() : ("," + ii);
                            }
                        }

                        szObject += "            \"resolution\": [" + szResList + "],\n";
                        break;
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // height...
                #region height...

                // Find the line that has the resolutions...
                double dfHeightMin = 0;
                double dfHeightMax = 0;
                foreach (string sz in aszHelp)
                {
                    if (sz.StartsWith("    -y "))
                    {
                        string[] aszNumbers = sz.Split(new string[] { "    -y ", "..", "mm" }, StringSplitOptions.RemoveEmptyEntries);
                        if (!double.TryParse(aszNumbers[0], out dfHeightMin))
                        {
                            continue;
                        }
                        if (!double.TryParse(aszNumbers[1], out dfHeightMax))
                        {
                            continue;
                        }
                        szObject += "            \"height\": [" + (dfHeightMin * 1000) + "," + (dfHeightMax * 1000) + "],\n";
                        break;
                    }
                }
                if (dfHeightMax == 0)
                {
                    continue;
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // width...
                #region width...

                // Find the line that has the resolutions...
                double dfWidthMin = 0;
                double dfWidthMax = 0;
                foreach (string sz in aszHelp)
                {
                    if (sz.StartsWith("    -x "))
                    {
                        string[] aszNumbers = sz.Split(new string[] { "    -x ", "..", "mm" }, StringSplitOptions.RemoveEmptyEntries);
                        if (!double.TryParse(aszNumbers[0], out dfWidthMin))
                        {
                            continue;
                        }
                        if (!double.TryParse(aszNumbers[1], out dfWidthMax))
                        {
                            continue;
                        }
                        szObject += "            \"width\": [" + (dfWidthMin * 1000) + "," + (dfWidthMax * 1000) + "],\n";
                        break;
                    }
                }
                if (dfWidthMax == 0)
                {
                    continue;
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // offsetX...
                #region offsetX...

                // Update the object
                szObject += "            \"offsetX\": [0," + (int)((dfWidthMax - dfWidthMin) * 1000) + "],\n";

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // offsetY...
                #region offsetY...

                // Update the object
                szObject += "            \"offsetY\": [0," + (int)((dfHeightMax - dfHeightMin) * 1000) + "],\n";

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // cropping...
                #region cropping...

                // Assume fixed...
                szObject += "            \"cropping\": [\"fixed\"],\n";

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // pixelFormat...
                #region pixelFormat...

                // Find the line that has the modes...
                foreach (string sz in aszHelp)
                {
                    if (sz.Contains("    --mode "))
                    {
                        string szModeList = "";
                        string[] aszModeList = sz.Split(new char[] { ' ', '\t', '|' });
                        if ((aszModeList == null) || (aszModeList.Length < 3))
                        {
                            continue;
                        }
                        foreach (string szMode in aszModeList)
                        {
                            switch (szMode)
                            {
                                default: break;
                                case "Lineart": szModeList += (szModeList == "") ? ("\"bw1\"") : (", \"bw1\""); break;
                                case "Gray": szModeList += (szModeList == "") ? ("\"gray8\"") : (", \"gray8\""); break;
                                case "Color": szModeList += (szModeList == "") ? ("\"rgb24\"") : (", \"rgb24\""); break;
                            }
                        }
                        if (szModeList == "")
                        {
                            continue;
                        }
                        szObject += "            \"pixelFormat\": [" + szModeList + "],\n";
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // compression...
                #region compression...

                // Just none...
                szObject += "            \"compression\": [" + "\"none\"" + "]\n";

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // We got this far, so add the object to the list we're building...
                szObject += "        }";
                szList += szObject;
            }
            szList += "\n    ]\n";
            szList += "}";

            // We didn't find anything...
            if (!szList.Contains("sane"))
            {
                szList = "";
            }

            // All done...
            return (szList);
        }

        /// <summary>
        /// Select a TWAIN driver to scan with...
        /// </summary>
        /// <param name="a_szTwainDriverIdentity">The current driver, in case the user cancels the selection</param>
        /// <returns>The currently selected driver or null, if there are no drivers</returns>
        public static string SelectDriver(string a_szTwainDriverIdentity)
        {
            return (a_szTwainDriverIdentity);
        }

        /// <summary>
        /// Process a task...
        /// </summary>
        /// <param name="a_szWriteFolder">A place where we can write files</param>
        /// <param name="a_szDriver">The driver to use, or null if you want the function to figure it out</param>
        /// <param name="a_szFile">The full path and name of a file holding a SWORD task</param>
        /// <param name="m_blTwainLocal">true if TWAIN Local</param>
        /// <param name="a_swordtask">result of the command</param>
        /// <returns>A sword object to monitor for end of scanning, or null, if the task failed</returns>
        public static Sword Task
        (
            string a_szWriteFolder,
            string a_szDriver,
            string a_szFile,
            bool m_blTwainLocal,
            ref SwordTask a_swordtask
        )
        {
            bool blError;
            bool blSetAppCapabilities = false;
            string szScanImageArguments = "";
            Sword sword;

            // Create the SWORD object to manage all this stuff...
            sword = new Sword();

            // Deserialize the JSON into a SWORD task...
            string szTask = File.ReadAllText(a_szFile);
            blError = sword.SwordDeserialize(szTask, new Guid("211a1e90-11e1-11e5-9493-1697f925ec7b"), ref a_swordtask);
            if (!blError)
            {
                TwainDirectSupport.Log.Error("Bad task..." + a_szFile);
                sword.Close();
                sword = null;
                return (null);
            }

            // Build a TWAIN task from the SWORD task...
            sword.m_sanetask = new SaneTask(a_swordtask, a_szWriteFolder, ref szScanImageArguments);

            // Start processing the TWAIN task, if there is more
            // than one action they'll be each be dispatched in
            // turn from some other function made by the caller
            // using NextAction...
            if (!sword.Process(a_szDriver, true, false, out blError, ref a_swordtask, ref blSetAppCapabilities))
            {
                TwainDirectSupport.Log.Error("Task failed..." + a_szFile);
                sword.Close();
                return (null);
            }

            // All done...
            return (sword);
        }

        /// <summary>
        /// Run the next action in a SWORD task...
        /// </summary>
        /// <param name="a_blError">true on an error</param>
        /// <param name="a_swordtask">result of the command</param>
        /// <returns></returns>
        public bool NextAction(out bool a_blError, ref SwordTask a_swordtask, ref bool a_blSetAppCapabilities)
        {
            return (Action(out a_blError, ref a_swordtask, ref a_blSetAppCapabilities));
        }

        /// <summary>
        /// Cancel a scanning session...
        /// </summary>
        public void Cancel()
        {
            //m_blCancel = true;
        }

        /// <summary>
        /// Are we processing an action?
        /// </summary>
        /// <returns>true if we're still processing an action</returns>
        public bool IsProcessing()
        {
            return (m_blProcessing);
        }

        /// <summary>
        /// Close sword, free all resources...
        /// </summary>
        public void Close()
        {
            // Nothing needed here at this time...
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Turn a JSON string into something we can deal with.  Specifically,
        /// the structure described in the SWORD documentation.  This structure
        /// takes on the topology of the request.  This means that the only
        /// elements we're going to find in it are the ones requested by the
        /// caller.
        /// 
        /// We have options at that point.  If the device is already set to
        /// the desired preset (such as factory default), then we only have
        /// to apply the task to it.  In this case the developer may opt to
        /// find the preset and send that down, then follow it with the rest
        /// of the contents within the task.
        /// 
        /// On the other paw it might be easier for some developers to merge
        /// the task with the relevant baseline settings and fire the whole
        /// thing over to the device.  Bearing in mind that they may need to
        /// some merging on the other side when they try to construct the
        /// metadata that goes with the image.
        /// </summary>
        /// <param name="a_szTask">task to process</param>
        /// <param name="a_guidScanner">this scanner's guid</param>
        /// <param name="a_swordtask">the object accompanying this task</param>
        /// <returns>true on success</returns>
        private bool SwordDeserialize(string a_szTask, Guid a_guidScanner, ref SwordTask a_swordtask)
        {
            bool blSuccess;

            // Log what's going on
            TwainDirectSupport.Log.Info("");
            TwainDirectSupport.Log.Info("sw> " + ((a_szTask != null) ? a_szTask : "(null)"));
            blSuccess = SwordTask.Deserialize(a_szTask, a_guidScanner, ref a_swordtask);

            // All done...
            return (blSuccess);
        }

        /// <summary>
        /// Collect information about the TWAIN Driver.  We need this to decide
        /// if we can safely use it, and to determine what features it can
        /// support.
        /// 
        /// TBD: We're starting simple.  The code will only support single
        /// stream from a feeder or a flatbed.  We'll add the other stuff
        /// later on.
        /// </summary>
        /// <returns></returns>
        private bool SaneInquiry()
        {
            // Give a clue where we are...
            TwainDirectSupport.Log.Info(" ");
            TwainDirectSupport.Log.Info("SaneInquiry begin...");

            // Issue a help command...
            Process process = null;
            string[] aszHelp = Program.ScanImage("ScannerHelp", "--help -d " + m_szTwainDriverIdentity, ref process, null);


            ////////////////////////////////////////////////////////////////////////////////////////////////
            // sources...
            #region sources...

            // Just handle any and feeder...
            m_aszSaneSources = new string[] { "any", "feeder" };

            #endregion


            ////////////////////////////////////////////////////////////////////////////////////////////////
            // resolutions...
            #region resolutions...

            // Find the line that has the resolutions...
            foreach (string szResolutionList in aszHelp)
            {
                if (szResolutionList.StartsWith("    --resolution "))
                {
                    string szResolutions = "";
                    string[] aszNumbers = szResolutionList.Split(new char[] { ' ', '\t', '|' });
                    if ((aszNumbers == null) || (aszNumbers.Length < 3))
                    {
                        continue;
                    }
                    foreach (string szNumber in aszNumbers)
                    {
                        int iNumber;
                        if (int.TryParse(szNumber,out iNumber))
                        {
                            szResolutions += (szResolutions == "") ? iNumber.ToString() : ("," + iNumber);
                        }
                    }
                    if (szResolutions != "")
                    {
                        m_aszSaneResolutions = szResolutions.Split(',');
                    }
                    break;
                }
            }

            #endregion


            ////////////////////////////////////////////////////////////////////////////////////////////////
            // height...
            #region height...

            // Find the line that has the resolutions...
            int iHeightMin = 0;
            int iHeightMax = 0;
            foreach (string szResolutions in aszHelp)
            {
                if (szResolutions.StartsWith("    -y "))
                {
                    string[] aszNumbers = szResolutions.Split(new string[] { "    -y ", "..", "mm" }, StringSplitOptions.None);
                    if (!int.TryParse(aszNumbers[1], out iHeightMin))
                    {
                        continue;
                    }
                    if (!int.TryParse(aszNumbers[2], out iHeightMax))
                    {
                        continue;
                    }
                    m_iSaneBottomMin = iHeightMin * 1000;
                    m_iSaneBottomMax = iHeightMax * 1000;
                    break;
                }
            }

            #endregion


            ////////////////////////////////////////////////////////////////////////////////////////////////
            // width...
            #region width...

            // Find the line that has the resolutions...
            int iWidthMin = 0;
            int iWidthMax = 0;
            foreach (string szResolutions in aszHelp)
            {
                if (szResolutions.StartsWith("    -x "))
                {
                    string[] aszNumbers = szResolutions.Split(new string[] { "    -x ", "..", "mm" }, StringSplitOptions.None);
                    if (!int.TryParse(aszNumbers[1], out iWidthMin))
                    {
                        continue;
                    }
                    if (!int.TryParse(aszNumbers[2], out iWidthMax))
                    {
                        continue;
                    }
                    m_iSaneWidthMin = (iWidthMin * 1000);
                    m_iSaneWidthMax = (iWidthMax * 1000);
                    break;
                }
            }

            #endregion


            ////////////////////////////////////////////////////////////////////////////////////////////////
            // offsetX...
            #region offsetX...

            //m_iSaneLeftMin = 0;
            //m_iSaneLeftMax = (iWidthMax - iWidthMin) * 1000;

            #endregion


            ////////////////////////////////////////////////////////////////////////////////////////////////
            // offsetY...
            #region offsetY...

            //m_iSaneTopMin = 0;
            //m_iSaneTopMax = (iHeightMax - iHeightMin) * 1000;

            #endregion


            ////////////////////////////////////////////////////////////////////////////////////////////////
            // cropping...
            #region cropping...

            // Assume fixed...
            m_aszSaneCropping = new string[] { "fixed" };

            #endregion


            ////////////////////////////////////////////////////////////////////////////////////////////////
            // pixelFormat...
            #region pixelFormat...

            // Find the line that has the modes...
            foreach (string szModes in aszHelp)
            {
                if (szModes.Contains("    --mode "))
                {
                    string szModeList = "";
                    string[] aszModeList = szModes.Split(new char[] { ' ', '\t', '|' });
                    if ((aszModeList == null) || (aszModeList.Length < 3))
                    {
                        continue;
                    }
                    foreach (string szMode in aszModeList)
                    {
                        switch (szMode)
                        {
                            default: break;
                            case "Lineart": szModeList += (szModeList == "") ? ("\"bw1\"") : (", \"bw1\""); break;
                            case "Gray": szModeList += (szModeList == "") ? ("\"gray8\"") : (", \"gray8\""); break;
                            case "Color": szModeList += (szModeList == "") ? ("\"rgb24\"") : (", \"rgb24\""); break;
                        }
                    }
                    m_aszPixelFormat = szModeList.Split(',');
                }
            }

            #endregion


            ////////////////////////////////////////////////////////////////////////////////////////////////
            // compression...
            #region compression...

            // Just none...
            m_aszCompression = new string[] { "none" };

            #endregion

            // All done...
            TwainDirectSupport.Log.Info(" ");
            TwainDirectSupport.Log.Info("TwainInquiry completed...");

            // All done...
            return (true);
        }

        /// <summary>
        /// Pick the stream that we're going to use based on the availability of
        /// the sources and their contingencies. It's easy to imagine this function
        /// getting fairly sophisticated, since there are several possible
        /// configurations and more than one way of identifying them.
        /// </summary>
        /// <param name="a_swordtask">result of the command</param>
        /// <returns></returns>
        private string SaneSelectStream(ref SwordTask a_swordtask)
        {
            string szStatus;
            string szSourceReply = "";

            // Give a clue where we are...
            TwainDirectSupport.Log.Info(" ");
            TwainDirectSupport.Log.Info("SaneSelectStream begin...");

            // We have no action, technically, we shouldn't be here...
            if (    (m_sanetask == null)
                ||  (m_sanetask.m_saneaction == null)
                ||  (m_sanetask.m_saneaction[m_iAction] == null))
            {
                TwainDirectSupport.Log.Info("SaneSelectStream: null task");
                a_swordtask.SetTaskReply
                (
                    "{\n" +
                    "            }\n"
                );
                return ("success");
            }

            // Let's make things more convenient...
            SaneAction saneaction = m_sanetask.m_saneaction[m_iAction];

            // We have no streams...
            if (saneaction.m_sanestream == null)
            {
                TwainDirectSupport.Log.Info("SaneSelectStream: default scanning mode (task has no streams)");
                a_swordtask.SetTaskReply
                (
                    "{\n" +
                    "                \"actions\": [\n" +
                    "                    {\n" +
                    "                        \"action\": \"configure\"\n" +
                    "                    }\n" +
                    "                ]\n" +
                    "            }\n"
                );
                return ("success");
            }

            // The first successful stream wins...
            szStatus = "success";
            int iTwainStream;
            for (iTwainStream = 0; (saneaction.m_sanestream != null) && (iTwainStream < saneaction.m_sanestream.Length); iTwainStream++)
            {
                SaneStream sanestream = saneaction.m_sanestream[iTwainStream];
                TwainDirectSupport.Log.Info("Stream #" + iTwainStream);

                // We can have more than one source in a stream, so do the reset up here...
                szSourceReply = "";

                // Analyze the sources...
                szStatus = "success";
                int iTwainSource;
                for (iTwainSource = 0; (sanestream.m_sanesource != null) && (iTwainSource < sanestream.m_sanesource.Length); iTwainSource++)
                {
                    SaneSource sanesource = sanestream.m_sanesource[iTwainSource];
                    TwainDirectSupport.Log.Info("SaneSelectStream: source #" + iTwainSource);

                    // Set the source...
                    string szSource;
                    szStatus = sanesource.SetSource(m_guidScanner, out szSource, ref a_swordtask);
                    if (szStatus == "skip")
                    {
                        TwainDirectSupport.Log.Info("SaneSelectStream: source belongs to another vendor, so skipping it");
                        continue;
                    }
                    else if (szStatus != "success")
                    {
                        TwainDirectSupport.Log.Info("SaneSelectStream: source exception: " + szStatus);
                        break;
                    }

                    // Uh-oh, no pixelFormat...
                    if ((sanesource.m_saneformat == null) || (sanesource.m_saneformat.Length == 0))
                    {
                        // Add this source...
                        if (szSourceReply == "")
                        {
                            szSourceReply +=
                                "                                    {\n" +
                                "                                        \"source\": \"" + szSource + "\"\n" +
                                "                                    },\n";
                        }
                        else
                        {
                            szSourceReply =
                                "                                    },\n" +
                                "                                    {\n" +
                                "                                        \"source\": \"" + szSource + "\"\n" +
                                "                                    },\n";
                        }

                        // Next source...
                        continue;
                    }

                    // Add this source...
                    if (szSourceReply == "")
                    {
                        szSourceReply +=
                            "                                    {\n" +
                            "                                        \"source\": \"" + szSource + "\",\n" +
                            "                                        \"pixelFormats\": [\n";
                    }
                    else
                    {
                        szSourceReply =
                            "                                    },\n" +
                            "                                    {\n" +
                            "                                        \"source\": \"" + szSource + "\",\n" +
                            "                                        \"pixelFormats\": [\n";
                    }

                    // We can have multiple formats in a source, in which case the scanner
                    // will automatically pick the best match.  This section needs to follow
                    // the Capability Ordering rules detailed in the TWAIN Specification.
                    szStatus = "success";
                    int iTwainFormat;
                    for (iTwainFormat = 0; (sanesource.m_saneformat != null) && (iTwainFormat < sanesource.m_saneformat.Length); iTwainFormat++)
                    {
                        SanePixelFormat sanepixelformat = sanesource.m_saneformat[iTwainFormat];
                        TwainDirectSupport.Log.Info("SaneSelectStream: pixelFormat #" + iTwainFormat);

                        // Pick a color...
                        szStatus = SaneSetValue(sanepixelformat.m_capabilityPixeltype, ref a_swordtask, ref szSourceReply);
                        if (szStatus != "success")
                        {
                            TwainDirectSupport.Log.Info("SaneSelectStream: pixelFormat exception: " + szStatus);
                            break;
                        }

                        // Resolution...
                        szStatus = SaneSetValue(sanepixelformat.m_capabilityResolution, ref a_swordtask, ref szSourceReply);
                        if (szStatus != "success")
                        {
                            TwainDirectSupport.Log.Info("SaneSelectStream: resolution exception: " + szStatus);
                            break;
                        }

                        // Compression...
                        szStatus = SaneSetValue(sanepixelformat.m_capabilityCompression, ref a_swordtask, ref szSourceReply);
                        if (szStatus != "success")
                        {
                            TwainDirectSupport.Log.Info("SaneSelectStream: compression exception: " + szStatus);
                            break;
                        }

                        // Xfercount...
                        szStatus = SaneSetValue(sanepixelformat.m_capabilityXfercount, ref a_swordtask, ref szSourceReply);
                        if (szStatus != "success")
                        {
                            TwainDirectSupport.Log.Info("SaneSelectStream: xfercount exception: " + szStatus);
                            break;
                        }
                    }

                    // Remove the trailing comma...
                    if (szSourceReply.EndsWith(",\n"))
                    {
                        szSourceReply = szSourceReply.Substring(0, szSourceReply.Length - 2) + "\n";
                    }

                    // End the attributes section...
                    szSourceReply +=
                        "                                                 ]\n" +
                        "                                             },\n";

                    // Check the status from the format, and if it's not success pass it on...
                    if (szStatus != "success")
                    {
                        break;
                    }
                }

                // Finish building the task reply...
                if (szSourceReply == "")
                {
                    a_swordtask.SetTaskReply
                    (
                        "{\n" +
                        "                \"actions\": [\n" +
                        "                    {\n" +
                        "                        \"action\": \"configure\",\n" +
                        "                        \"streams\": [\n" +
                        "                            {\n" +
                        "                                \"sources\": [\n" +
                        "                                    {\n" +
                        "                                    }\n" +
                        "                                ]\n" +
                        "                            }\n" +
                        "                        ]\n" +
                        "                    }\n" +
                        "                ]\n" +
                        "            }\n"
                    );
                }
                else
                {
                    // Remove the trailing ,\n...
                    if (szSourceReply.EndsWith(",\n"))
                    {
                        szSourceReply = szSourceReply.Substring(0, szSourceReply.Length - 2) + "\n";
                    }

                    // End the pixelFormats section...
                    if (szSourceReply.Contains("\"pixelFormat\":"))
                    {
                        szSourceReply +=
                            "                                         ]\n" +
                            "                                     }\n";
                    }

                    // Build the reply...
                    a_swordtask.SetTaskReply
                    (
                        "{\n" +
                        "                \"actions\": [\n" +
                        "                    {\n" +
                        "                        \"action\": \"configure\",\n" +
                        "                        \"streams\": [\n" +
                        "                            {\n" +
                        "                                \"sources\": [\n" +
                        szSourceReply +
                        "                                ]\n" +
                        "                            }\n" +
                        "                        ]\n" +
                        "                    }\n" +
                        "                ]\n" +
                        "            }\n"
                    );
                }

                // Check the status from the source, if it's not "nextStream", then
                // we'll break out...
                if (szStatus != "nextStream")
                {
                    break;
                }
            }

            // Check the status from the stream, and if it's not success exit from the
            // function with the exception...
            switch (szStatus)
            {
                // Just drop down, we're okay...
                default:
                case "ignore":
                case "success":
                    TwainDirectSupport.Log.Info("SaneSelectStream: stream search ended in " + szStatus);
                    break;

                // The task has failed.  "nextStream" results in a failure if it was
                // explicitly set by the task for the last stream.  By default the
                // last stream is going to be "ignore"...
                case "fail":
                case "nextStream":
                    TwainDirectSupport.Log.Info("SaneSelectStream: stream search ended in error, " + szStatus);
                    return ("fail");
            }

            // Check the image source, so we can properly report it later in
            // the metadata.  If the feederenabled isn't supported, or if it
            // returns 0 (FALSE), then we're scanning from the flatbed...
            // TBD (assume feeder for now)
            //m_blFlatbed = false;
            //m_blDuplex = true;

            // We're good...
            TwainDirectSupport.Log.Info("SaneSelectStream completed...");
            return ("success");
        }

        /// <summary>
        /// Process an action in a task.
        /// </summary>
        /// <param name="a_szTwainDefaultDriver">The scanner we're going to use</param>
        /// <param name="a_blFirstAction">Is this the first action in the task?</param>
        /// <param name="a_blIgnoreTaskScan">Skip any scan action (aka TWAIN Local)</param>
        /// <param name="a_blError">error flag</param>
        /// <param name="a_swordtask">result of the command</param>
        /// <returns></returns>
        private bool Process
        (
            string a_szTwainDefaultDriver,
            bool a_blFirstAction,
            bool a_blIgnoreTaskScan,
            out bool a_blError,
            ref SwordTask a_swordtask,
            ref bool a_blSetAppCapabilities
        )
        {
            bool blStatus;
            bool blError;
            TwainDirectSupport.Log.Info("");
            TwainDirectSupport.Log.Info("Process begin...");

            // Init stuff...
            a_blError = false;
            m_blIgnoreTaskScan = a_blIgnoreTaskScan;
            if (a_blFirstAction)
            {
                m_iAction = -1;
            }

            // Dispatch an action...
            blStatus = Action(out blError, ref a_swordtask, ref a_blSetAppCapabilities);
            if (!blStatus)
            {
                TwainDirectSupport.Log.Error("Process: action failed");
                a_blError = true;
                return (false);
            }

            // All done...
            TwainDirectSupport.Log.Info("Process completed...");
            a_blError = false;
            return (true);
        }

        /// <summary>
        /// Initiate an action...
        /// </summary>
        /// <param name="a_blError">error flag</param>
        /// <param name="a_swordtask">result of the command</param>
        /// <returns>true on success</returns>
        private bool Action(out bool a_blError, ref SwordTask a_swordtask, ref bool a_blSetAppCapabilities)
        {
            TwainDirectSupport.Log.Info("");
            TwainDirectSupport.Log.Info("Action...");

            // Init stuff (just to be sure)...
            a_blError = false;
            m_blProcessing = false;

            // Go to the next action (this includes the first action)...
            m_iAction += 1;
            if (    (m_sanetask.m_saneaction == null)
                ||  (m_iAction >= m_sanetask.m_saneaction.Length)
                ||  (m_sanetask.m_saneaction[m_iAction] == null))
            {
                TwainDirectSupport.Log.Info("Action: end of actions...");
                if (m_iAction == 0)
                {
                    a_swordtask.SetTaskReply
                    (
                        "{\n" +
                        "            }\n"
                    );
                }
                a_blError = false;
                return (false);
            }

            // Dispatch the action...
            SaneAction saneaction = m_sanetask.m_saneaction[m_iAction];
            TwainDirectSupport.Log.Info("Action: " + saneaction.m_szAction);
            switch (saneaction.m_szAction)
            {
                // We've got a command that's new to us.  Our default
                // behavior is to keep going.
                default:
                    if (saneaction.m_szException == "fail")
                    {
                        TwainDirectSupport.Log.Error("Action: unrecognized action...<" + saneaction.m_szAction + ">");
                        a_swordtask.SetTaskError("fail", saneaction.m_szJsonKey + ".action", saneaction.m_szAction, -1);
                        a_blError = true;
                        return (false);
                    }
                    TwainDirectSupport.Log.Info("Action: unrecognized action...<" + saneaction.m_szAction + ">");
                    return (true);

                // Configure...
                case "configure":
                    // Make a note that we successfully set these capabilities, so that
                    // we won't have to do it again when scanning starts...
                    a_blSetAppCapabilities = true;

                    // Pick a stream...
                    if (SaneSelectStream(ref a_swordtask) != "success")
                    {
                        TwainDirectSupport.Log.Error("Action: SaneSelectStream failed");
                        a_blError = true;
                        return (false);
                    }

                    // We're all done with this command...
                    TwainDirectSupport.Log.Info("Action complete...");
                    return (true);
            }
        }

        /// <summary>
        /// Set a SANE capability value...
        /// </summary>
        /// <param name="a_capability">The stuff we want to set</param>
        /// <param name="a_szTwainValue">the TWAIN value we set</param>
        /// <param name="a_swordtask">result of the command</param>
        /// <returns></returns>
        private string SaneSetValue(Capability a_capability, ref SwordTask a_swordtask, ref string a_szSourceReply)
        {
            string szStatus;
            string szSwordName;
            string szSwordValue;
            string szTwainValue;

            // We don't have this item...
            if (a_capability == null)
            {
                return ("success");
            }

            // Do the set...
            szStatus = a_capability.SetScanner(m_guidScanner, out szSwordName, out szSwordValue, out szTwainValue, ref a_swordtask);

            // Update the task reply for pixelFormat...
            if (szSwordName == "pixelFormat")
            {
                a_szSourceReply +=
                   "                                             {\n" +
                   "                                                 \"pixelFormat\": \"" + szSwordValue + "\",\n" +
                   "                                                 \"attributes\": [\n";
            }

            // Update the task reply for all the attributes...
            else
            {
                switch (szSwordName)
                {
                    // Handle strings...
                    default:
                        a_szSourceReply +=
                           "                                                     {\n" +
                           "                                                         \"attribute\": \"" + szSwordName + "\",\n" +
                           "                                                         \"values\": [\n" +
                           "                                                             {\n" +
                           "                                                                 \"value\": \"" + szSwordValue + "\"\n" +
                           "                                                             }\n" +
                           "                                                         ]\n" +
                           "                                                     },\n";
                        break;

                    // Handle integers...
                    case "imagecount":
                    case "resolution":
                        a_szSourceReply +=
                           "                                                     {\n" +
                           "                                                         \"attribute\": \"" + szSwordName + "\",\n" +
                           "                                                         \"values\": [\n" +
                           "                                                             {\n" +
                           "                                                                 \"value\": " + szSwordValue + "\n" +
                           "                                                             }\n" +
                           "                                                         ]\n" +
                           "                                                     },\n";
                        break;
                }
            }

            // All done...
            return (szStatus);
        }

        /// <summary>
        /// Get the TWAIN default driver...
        /// </summary>
        /// <param name="a_szScanner">An override that we'll get instead of the default driver</param>
        /// <returns></returns>
        private string TwainGetDefaultDriver(string a_szScanner)
        {
            return (a_szScanner);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Definitions...

        /// <summary>
        /// We use this to tag the kind of topology support we can expect
        /// to get from the TWAIN driver.
        /// 
        /// undefined - we haven't figured it out yet.
        ///
        /// simple - there's no way to set different capabilities for sides
        /// of a sheet of paper or images on one side of a sheet of paper.
        /// 
        /// image - we can set different values for color, grayscale and/or
        /// black-and-white; we'd expect this for scanners that support
        /// multistream, and it'll be used by tasks that have more than one
        /// imageformat in their formatlist.
        /// 
        /// side - we can set different values for front and rear; we don't
        /// have to worry about feeder and flatbed if the driver supports
        /// feeder loaded, since we'll download the appropriate settings at
        /// the point of detection.
        /// 
        /// imageandside - the scanner supports the full topology model.
        /// 
        /// </summary>
        enum TopologySupport
        {
            undefined,
            simple,
            image,
            side,
            imageandside
        };

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

            /// <summary>
            /// Command and control and state variables to manage our TWAIN session.
            /// 
            /// m_szTwainDriverIdentity - the scanner we'll be using
            /// 
            /// m_twaincstoolkit - our access to the scanner's TWAIN driver
            /// 
            /// m_sanetask - the list of actions to perform
            /// 
            /// m_iAction - the index of the current action in our task
            /// 
            /// m_blProcessing - state variable, true if we're processing an action
            /// 
            /// m_blCancel - state variable, true if a cancel was requested
            /// 
            /// m_blFlatbed - we're scanning from a flatbed
            /// 
            /// m_blDuplex - we're scanning duplex from a feeder
            /// 
            /// m_blIgnoreTaskScan - ignore the scan command in a task 
            /// 
            /// </summary>
            private string m_szTwainDriverIdentity;
            private SaneTask m_sanetask;
            private int m_iAction;
            private bool m_blProcessing;
            //private bool m_blCancel;
            //private bool m_blFlatbed;
            //private bool m_blDuplex;
            private bool m_blIgnoreTaskScan;

            /// <summary>
            /// Inquiry stuff...
            /// </summary>
            private string[] m_aszSaneSources;
            private string[] m_aszSaneResolutions;
            private int m_iSaneBottomMin;
            private int m_iSaneBottomMax;
            private int m_iSaneWidthMin;
            private int m_iSaneWidthMax;
            //private int m_iSaneLeftMin;
            //private int m_iSaneLeftMax;
            //private int m_iSaneTopMin;
            //private int m_iSaneTopMax;
            private string[] m_aszSaneCropping;
            private string[] m_aszPixelFormat;
            private string[] m_aszCompression;

            /// <summary>
            /// Supported features...
            /// </summary>
            private Guid m_guidScanner;

            /// <summary>
            /// Folder for our data...
            /// </summary>
            private string m_szWriteFolder;

            /// <summary>
            /// We're running in TWAIN Local...
            /// </summary>
            bool m_blTwainLocal;

        #endregion
    }
}
