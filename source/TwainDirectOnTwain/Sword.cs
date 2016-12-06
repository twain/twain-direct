///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirectOnTwain.Sword
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
//  M.McLaughlin    21-Oct-2014     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2014-2016 Kodak Alaris Inc.
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
// Note that we are "cheating" by appealing directly to the TWAIN namespace.
// Most of the work is done through the toolkit.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using TwainDirectSupport;
using TWAINWorkingGroup;
using TWAINWorkingGroupToolkit;

namespace TwainDirectOnTwain
{
    /// <summary>
    /// Manage the SWORD interface.  This section shows how to make use of a
    /// SwordTask and a TwainTask to run a TWAIN scanning session.  It's also
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
        public Sword(TWAINCSToolkit a_twaincstoolkit)
        {
            // Remember this stuff...
            m_szWriteFolder = Config.Get("writeFolder", "");
            m_blTwainLocal = (Config.Get("twainlocal", null) != null);

            // The TWAIN version of the SWORD task...
            m_twaintask = null;

            // Our TWAIN toolkit and processing state...
            m_twaincstoolkitCaller = a_twaincstoolkit;
            m_twaincstoolkit = a_twaincstoolkit;
            m_blProcessing = false;
            m_blCancel = false;

            // Image tracking information...
            m_iImageCount = 0;
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
            ref bool a_blSetAppCapabilities
        )
        {
            bool blStatus;
            bool blError;
            string szStatus;
            TWAINCSToolkit.STS sts;
            TWAINWorkingGroup.Log.Info("Batch mode starting...");

            // Skip if it's already open...
            if (m_twaincstoolkitCaller == null)
            {
                // Create a toolkit for ourselves...
                try
                {
                    m_twaincstoolkit = new TWAINCSToolkit
                    (
                        IntPtr.Zero,
                        null,
                        ReportImage,
                        null,
                        "TWAIN Working Group",
                        "TWAIN Sharp",
                        "SWORD-on-TWAIN",
                        2,
                        3,
                        new string[] { "DF_APP2", "DG_CONTROL", "DG_IMAGE" },
                        "USA",
                        "testing...",
                        "ENGLISH_USA",
                        1,
                        0,
                        false,
                        true,
                        (TWAINCSToolkit.RunInUiThreadDelegate)null,
                        this
                    );
                }
                catch
                {
                    TWAINWorkingGroup.Log.Error("Process: couldn't create a toolkit object...");
                    m_twaincstoolkit = null;
                    m_twaintask = null;
                    return (false);
                }

                // If we've not been given a driver, then look for the default...
                if ((m_szTwainDriverIdentity == null) || (m_szTwainDriverIdentity == ""))
                {
                    // Get the list of drivers...
                    string[] aszTwainDriverIdentity = m_twaincstoolkit.GetDrivers(ref m_szTwainDriverIdentity);
                    if (aszTwainDriverIdentity == null)
                    {
                        TWAINWorkingGroup.Log.Error("Process: failed to enumerate the TWAIN drivers");
                        m_twaintask = null;
                        return (false);
                    }

                    // Find our match...
                    if (!string.IsNullOrEmpty(a_szScanner))
                    {
                        for (int ii = 0; ii < aszTwainDriverIdentity.Length; ii++)
                        {
                            if (aszTwainDriverIdentity[ii].Contains(a_szScanner))
                            {
                                m_szTwainDriverIdentity = aszTwainDriverIdentity[ii];
                                break;
                            }
                        }
                    }
                }

                // Open the scanner...
                szStatus = "";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_OPENDS", ref m_szTwainDriverIdentity, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error("Process: MSG_OPENDS failed");
                    m_twaincstoolkit.Cleanup();
                    m_twaincstoolkit = null;
                    return (false);
                }
            }

            // Collect information about the scanner...
            if (!TwainInquiry(true))
            {
                TWAINWorkingGroup.Log.Error("Process: TwainInquiry says we can't do this");
                //m_twaincstoolkit.Cleanup();
                //m_twaincstoolkit = null;
                //m_twaintask = null;
                return (false);
            }

            // Have the driver process the task...
            if (m_blNativeTwainDirectSupport)
            {
                string szMetadata;
                TWAIN.TW_TWAINDIRECT twtwaindirect = default(TWAIN.TW_TWAINDIRECT);

                // Convert the task to an array, and then copy it into
                // memory pointed to by a handle...
                string szTask = a_szTask.Replace("\r", "").Replace("\n", "");
                byte[] abTask = Encoding.UTF8.GetBytes(szTask);
                IntPtr intptrTask = Marshal.AllocHGlobal(abTask.Length);
                Marshal.Copy(abTask, 0, intptrTask, abTask.Length);

                // Build the command...
                szMetadata =
                    Marshal.SizeOf(twtwaindirect) + "," +   // SizeOf
                    "0" + "," +                             // CommunicationManager
                    intptrTask + "," +                      // Send
                    abTask.Length + "," +                   // SendSize
                    "0" + "," +                             // Receive
                    "0";                                    // ReceiveSize

                // Send the command...
                szStatus = "";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_TWAINDIRECT", "MSG_SETTASK", ref szMetadata, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error("Process: MSG_SENDTASK failed");
                    Marshal.FreeHGlobal(intptrTask);
                    intptrTask = IntPtr.Zero;
                    m_twaincstoolkit.Cleanup();
                    m_twaincstoolkit = null;
                    return (false);
                }

                // TBD: Open up the reply (we should probably get the CsvToTwaindirect
                // function to do this for us)...
                string[] asz = szMetadata.Split(new char[] { ',' });
                if ((asz == null) || (asz.Length < 6))
                {
                    TWAINWorkingGroup.Log.Error("Process: MSG_SENDTASK failed");
                    Marshal.FreeHGlobal(intptrTask);
                    intptrTask = IntPtr.Zero;
                    m_twaincstoolkit.Cleanup();
                    m_twaincstoolkit = null;
                    return (false);
                }

                // Get the reply data...
                long lReceive;
                if (!long.TryParse(asz[4], out lReceive) || (lReceive == 0))
                {
                    TWAINWorkingGroup.Log.Error("Process: MSG_SENDTASK failed");
                    Marshal.FreeHGlobal(intptrTask);
                    intptrTask = IntPtr.Zero;
                    m_twaincstoolkit.Cleanup();
                    m_twaincstoolkit = null;
                    return (false);
                }
                IntPtr intptrReceiveHandle = new IntPtr(lReceive);
                uint u32ReceiveBytes;
                if (!uint.TryParse(asz[5], out u32ReceiveBytes) || (u32ReceiveBytes == 0))
                {
                    TWAINWorkingGroup.Log.Error("Process: MSG_SENDTASK failed");
                    m_twaincstoolkit.DsmMemFree(ref intptrReceiveHandle);
                    Marshal.FreeHGlobal(intptrTask);
                    intptrTask = IntPtr.Zero;
                    m_twaincstoolkit.Cleanup();
                    m_twaincstoolkit = null;
                    return (false);
                }

                // Convert it to an array and then a string...
                IntPtr intptrReceive = m_twaincstoolkit.DsmMemLock(intptrReceiveHandle);
                byte[] abReceive = new byte[u32ReceiveBytes];
                Marshal.Copy(intptrReceive, abReceive, 0, (int)u32ReceiveBytes);
                string szReceive = Encoding.UTF8.GetString(abReceive);
                m_twaincstoolkit.DsmMemUnlock(intptrReceiveHandle);

                // Cleanup...
                m_twaincstoolkit.DsmMemFree(ref intptrReceiveHandle);
                Marshal.FreeHGlobal(intptrTask);
                intptrTask = IntPtr.Zero;

                // Squirrel the reply away...
                a_swordtask.SetTaskReply(szReceive);
                return (true);
            }

            // Collect information about the scanner...
            if (!TwainInquiry(false))
            {
                TWAINWorkingGroup.Log.Error("Process: TwainInquiry says we can't do this");
                //m_twaincstoolkit.Cleanup();
                //m_twaincstoolkit = null;
                //m_twaintask = null;
                return (false);
            }

            // Init stuff...
            blStatus = false;

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
                TWAINWorkingGroup.Log.Info("Analyzing the task...");
                blStatus = SwordDeserialize(szTask, m_guidScanner, ref a_swordtask);
                if (!blStatus)
                {
                    return (false);
                }

                // TWAIN task time...
                m_twaintask = new TwainTask(a_swordtask);

                // Process each action in turn.
                // TBD: some kind of event trigger would be nicer than polling.
                // and we'll need a way to process commands so that information
                // can be requested or cancels can be issued.
                TWAINWorkingGroup.Log.Info("Running the task...");
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
                TWAINWorkingGroup.Log.Error("Batch mode threw exception on error...");
                TWAINWorkingGroup.Log.Error("exception: " + a_swordtask.GetException());
                TWAINWorkingGroup.Log.Error("location: " + a_swordtask.GetJsonExceptionKey());
                TWAINWorkingGroup.Log.Error("SWORD value: " + a_swordtask.GetSwordValue());
                TWAINWorkingGroup.Log.Error("TWAIN value: " + a_swordtask.GetTwainValue());
                return (false);
            }
            if (blError)
            {
                TWAINWorkingGroup.Log.Error("Batch mode completed on error...");
                TWAINWorkingGroup.Log.Error("exception: " + a_swordtask.GetException());
                TWAINWorkingGroup.Log.Error("location: " + a_swordtask.GetJsonExceptionKey());
                TWAINWorkingGroup.Log.Error("SWORD value: " + a_swordtask.GetSwordValue());
                TWAINWorkingGroup.Log.Error("TWAIN value: " + a_swordtask.GetTwainValue());
                return (false);
            }

            // Cleanup and scoot...
            Close();
            TWAINWorkingGroup.Log.Info("Batch mode completed...");
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
            SwordTask swordtask = new SwordTask();

            // Parse the task into a SWORD object...
            TWAINWorkingGroup.Log.Info("JSON to SWORD...");
            blStatus = SwordDeserialize(a_szTask, m_guidScanner, ref swordtask);
            if (!blStatus)
            {
                TWAINWorkingGroup.Log.Error("SwordDeserialize failed...");
                return (false);
            }

            // Parse the SWORD object into a TWAIN object...
            TWAINWorkingGroup.Log.Info("SWORD to TWAIN...");
            m_twaintask = new TwainTask(swordtask);
            if (m_twaintask == null)
            {
                TWAINWorkingGroup.Log.Error("TwainTask failed...");
                return (false);
            }

            // Process each action in turn.
            // TBD: some kind of event trigger would be nicer than polling.
            // and we'll need a way to process commands so that information
            // can be requested or cancels can be issued.
            TWAINWorkingGroup.Log.Info("Task begins...");
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
            TWAINWorkingGroup.Log.Info("Task completed...");

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
            sword = new Sword(null);

            // Check for a TWAIN driver...
            szTwainDefaultDriver = sword.TwainGetDefaultDriver(a_szScanner);

            // Cleanup...
            sword.Close();
            sword = null;
            return (szTwainDefaultDriver);
        }

        /// <summary>
        /// Return both names and information about the TWAIN drivers installed on the
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
        /// TWAIN Direct on TWAIN, so focus any efforts there instead of here.
        /// </summary>
        /// <returns>The list of drivers</returns>
        public static string TwainListDrivers()
        {
            int iEnum;
            string szStatus;
            string szTwainDriverIdentity;
            string szList = "";
            string szCapability;
            string szValues;
            string[] aszContainer;
            IntPtr intptrHwnd;
            TWAINCSToolkit twaincstoolkit;
            TWAINCSToolkit.STS sts;

            // Get an hwnd...
            if (TWAIN.GetPlatform() == TWAIN.Platform.WINDOWS)
            {
                intptrHwnd = NativeMethods.GetDesktopWindow();
            }
            else
            {
                intptrHwnd = IntPtr.Zero;
            }

            // Create the toolkit...
            try
            {
                twaincstoolkit = new TWAINCSToolkit
                (
                    intptrHwnd,
                    null,
                    null,
                    null,
                    "TWAIN Working Group",
                    "TWAIN Sharp",
                    "SWORD-on-TWAIN",
                    2,
                    3,
                    new string[] { "DF_APP2", "DG_CONTROL", "DG_IMAGE" },
                    "USA",
                    "testing...",
                    "ENGLISH_USA",
                    1,
                    0,
                    false,
                    true,
                    (TWAINCSToolkit.RunInUiThreadDelegate)null,
                    null
                );
            }
            catch
            {
                twaincstoolkit = null;
                return (null);
            }

            // Cycle through the drivers and build up a list of identities...
            int iIndex = -1;
            string[] aszTwidentity = new string[256];
            szStatus = "";
            szTwainDriverIdentity = "";
            for (sts = twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_GETFIRST", ref szTwainDriverIdentity, ref szStatus);
                 sts == TWAINCSToolkit.STS.SUCCESS;
                 sts = twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_GETNEXT", ref szTwainDriverIdentity, ref szStatus))
            {
                // Save this identity...
                iIndex += 1;
                aszTwidentity[iIndex] = szTwainDriverIdentity;

                // Prep for the next entry...
                szStatus = "";
                szTwainDriverIdentity = "";
            }

            // Okay, we have a list of identities, so now let's try to open each one of
            // them up and ask some questions...
            szList =  "{\n";
            szList += "    \"scanners\": [\n";
            string szTwidentityLast = null;
            foreach (string szTwidentity in aszTwidentity)
            {
                // Closing the previous driver up here helps make the code in this section a
                // little cleaner, allowing us to continue instead of having to do a cleanup
                // run in each statement that deals with a problem...
                if (szTwidentityLast != null)
                {
                    szStatus = "";
                    sts = twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_CLOSEDS", ref szTwidentityLast, ref szStatus);
                    szTwidentityLast = null;
                    twaincstoolkit.ReopenDSM();
                }
                if (szTwidentity == null)
                {
                    break;
                }

                // Open the driver...
                szStatus = "";
                szTwidentityLast = szTwidentity;
                try
                {
                    sts = twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_OPENDS", ref szTwidentityLast, ref szStatus);
                }
                catch (Exception exception)
                {
                    TWAINWorkingGroup.Log.Info("Driver threw an exception on open: " + szTwidentity);
                    TWAINWorkingGroup.Log.Info(exception.Message);
                    szTwidentityLast = null;
                    continue;
                }
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("Unable to open driver: " + szTwidentity);
                    szTwidentityLast = null;
                    continue;
                }

                // Build an object to add to the list, this is a scratchpad, so if we
                // have to abandon it mid-way through, it's no problem...
                string szObject = (!szList.Contains("twidentity")) ? "        {\n" : ",\n        {\n";
                string[] szTwidentityBits = CSV.Parse(szTwidentity);
                if (szTwidentityBits == null)
                {
                    TWAINWorkingGroup.Log.Info("Unable to parse TW_IDENTITY: " + szTwidentity);
                    continue;
                }
                szObject += "            \"twidentity\": \"" + szTwidentityBits[11] + "\",\n";


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // Is the UI controllable?  This isn't a guarantee of good behavior, but without it
                // we can be confident that the driver won't behave well...
                #region Is the UI controllable?

                // Get the current value...
                szStatus = "";
                szCapability = "CAP_UICONTROLLABLE";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);

                // If we don't find it, that's bad...                
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("CAP_UICONTROLLABLE error: " + szTwidentity);
                    continue;
                }

                // Parse it...
                aszContainer = CSV.Parse(szCapability);

                // Oh dear...
                if (aszContainer[1] != "TWON_ONEVALUE")
                {
                    TWAINWorkingGroup.Log.Error("CAP_UICONTROLLABLE unsupported container: " + szTwidentity);
                    continue;
                }

                // If we can't keep the UI off, then we can't use this driver...
                if ((aszContainer[3] != "1") && (aszContainer[3] != "TRUE"))
                {
                    TWAINWorkingGroup.Log.Error("CAP_UICONTROLLABLE isn't TRUE: " + szTwidentity);
                    continue;
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // Make sure the units is set to inches, otherwise we're going to have a less than
                // plesant experience getting information about the cropping region...
                #region Is units set to inches?

                // Get the current value...
                szStatus = "";
                szCapability = "ICAP_UNITS";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);

                // If we don't find it, that's bad...                
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("ICAP_UNITS error: " + szTwidentity);
                    continue;
                }

                // Parse it...
                aszContainer = CSV.Parse(szCapability);

                // Oh dear...
                if (aszContainer[1] != "TWON_ONEVALUE")
                {
                    TWAINWorkingGroup.Log.Error("ICAP_UNITS unsupported container: " + szTwidentity);
                    continue;
                }

                // If we't not inches, then set us to inches...
                if ((aszContainer[3] != "0") && (aszContainer[3] != "TWUN_INCHES"))
                {
                    szStatus = "";
                    szCapability = aszContainer[0] + "," + aszContainer[1] + "," + aszContainer[2] + ",0"; 
                    sts = twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAINCSToolkit.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Info("ICAP_UNITS set failed: " + szTwidentity);
                        continue;
                    }
                }

                #endregion


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
                    TWAINWorkingGroup.Log.Info("Failed to get hostName: " + exception.Message);
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // serialNumber: this allows us to uniquely identify a scanner, it's debatable if we
                // need both the hostname and the serial number, but for now that's what we're doing...
                #region serialNumber...

                // Get the current value...
                szStatus = "";
                szCapability = "CAP_SERIALNUMBER";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);

                // It's an error, because we've lost the ability to handle more than one
                // model of this scanner, but it's not fatal, because we can stil handle
                // at least one scanner...                
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("CAP_SERIALNUMBER error: " + szTwidentity);
                    szObject += "            \"serialNumber\": \"(no serial number)\",\n";
                }

                // Keep on keeping on...
                else
                {
                    // Parse it...
                    aszContainer = CSV.Parse(szCapability);

                    // We've been weirded out...
                    if (aszContainer[1] != "TWON_ONEVALUE")
                    {
                        TWAINWorkingGroup.Log.Error("CAP_SERIALNUMBER unsupported container: " + szTwidentity);
                    }
                    
                    // This is enough to add the item...
                    else
                    {
                        szObject += "            \"serialNumber\": \"" + aszContainer[3] + "\",\n";
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // sources...
                #region sources...

                // Get the enumeration...
                szStatus = "";
                szCapability = "CAP_FEEDERENABLED";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GET", ref szCapability, ref szStatus);

                // Assume that we have a flatbed...
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    szObject += "            \"source\": [\"any\",\"flatbed\"],\n";
                }

                // It looks like we have something else...
                else
                {
                    // Parse it...
                    aszContainer = CSV.Parse(szCapability);

                    // Handle the container...
                    szValues = "\"any\"";
                    switch (aszContainer[1])
                    {
                        default:
                            TWAINWorkingGroup.Log.Info("CAP_FEEDERENABLED unsupported container: " + szTwidentity);
                            continue;

                        // These containers are just off by an index, so we can combine them.
                        // We should be checking the bitdepth, just to be sure, but this is a
                        // real edge case that shouldn't matter 99% of the time...
                        case "TWON_ONEVALUE":
                        case "TWON_ENUMERATION":
                            for (iEnum = (aszContainer[1] == "TWON_ONEVALUE") ? 3 : 6; iEnum < aszContainer.Length; iEnum++)
                            {
                                switch (aszContainer[iEnum])
                                {
                                    default:
                                        break;
                                    case "0": // FALSE
                                        if (!szValues.Contains("flatbed"))
                                        {
                                            szValues += ",\"flatbed\"";
                                        }
                                        break;
                                    case "1": // TRUE
                                        if (!szValues.Contains("feeder"))
                                        {
                                            szValues += ",\"feeder\"";
                                        }
                                        break;
                                }
                            }
                            break;
                    }

                    // Add to the list...
                    if (szValues != "")
                    {
                        szObject += "            \"source\": [" + szValues + "],\n";
                    }
                    else
                    {
                        TWAINWorkingGroup.Log.Info("ICAP_PIXELTYPE no recognized values: " + szTwidentity);
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // numberOfSheets...
                #region numberOfSheets...

                szObject += "            \"numberOfSheets\": [1,32767],\n";

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // resolutions...
                #region resolutions...

                // Get the enumeration...
                szStatus = "";
                szCapability = "ICAP_XRESOLUTION";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GET", ref szCapability, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("ICAP_XRESOLUTION error: " + szTwidentity);
                    continue;
                }

                // Parse it...
                aszContainer = CSV.Parse(szCapability);

                // Handle the container...
                szValues = "";
                switch (aszContainer[1])
                {
                    default:
                        TWAINWorkingGroup.Log.Info("ICAP_XRESOLUTION unsupported container: " + szTwidentity);
                        continue;

                    // These containers are just off by an index, so we can combine them.
                    // We should be checking the bitdepth, just to be sure, but this is a
                    // real edge case that shouldn't matter 99% of the time...
                    case "TWON_ONEVALUE":
                    case "TWON_ENUMERATION":
                        for (iEnum = (aszContainer[1] == "TWON_ONEVALUE") ? 3 : 6; iEnum < aszContainer.Length; iEnum++)
                        {
                            if (!szValues.Contains(aszContainer[iEnum]))
                            {
                                szValues += (szValues == "") ? aszContainer[iEnum] : ("," + aszContainer[iEnum]);
                            }
                        }
                        break;

                    // We're not going to support ranges at this time.  Instead we'll
                    // pare the range down to a set of commonly used resolutions...
                    case "TWON_RANGE":
                        // Get the min and the max, and add items in that range...
                        int iMin;
                        int iMax;
                        if (!int.TryParse(aszContainer[3], out iMin))
                        {
                            TWAINWorkingGroup.Log.Info("ICAP_XRESOLUTION error: " + szTwidentity);
                            continue;
                        }
                        if (!int.TryParse(aszContainer[4], out iMax))
                        {
                            TWAINWorkingGroup.Log.Info("ICAP_XRESOLUTION error: " + szTwidentity);
                            continue;
                        }
                        szValues += iMin;
                        foreach (int iRes in new int[] { 75, 100, 150, 200, 240, 250, 300, 400, 500, 600, 1200, 2400, 4800, 9600, 19200 })
                        {
                            if ((iMin < iRes) && (iRes < iMax))
                            {
                                szValues += "," + iRes;
                            }
                        }
                        szValues += "," + iMax;
                        break;
                }

                // Add to the list...
                if (szValues != "")
                {
                    szObject += "            \"resolution\": [" + szValues + "],\n";
                }
                else
                {
                    TWAINWorkingGroup.Log.Info("ICAP_XRESOLUTION no recognized values: " + szTwidentity);
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // height...
                #region height...

                // Get the physical height...
                szStatus = "";
                szCapability = "ICAP_PHYSICALHEIGHT";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("ICAP_PHYSICALHEIGHT error: " + szTwidentity);
                    continue;
                }
                aszContainer = CSV.Parse(szCapability);
                int iMaxHeightMicrons = (int)(double.Parse(aszContainer[3]) * 25400);

                // Get the physical width...
                szStatus = "";
                szCapability = "ICAP_PHYSICALWIDTH";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("ICAP_PHYSICALWIDTH error: " + szTwidentity);
                    continue;
                }
                aszContainer = CSV.Parse(szCapability);
                int iMaxWidthMicrons = (int)(double.Parse(aszContainer[3]) * 25400);

                // Get the minimum height...
                int iMinHeightMicrons = 0;
                szStatus = "";
                szCapability = "ICAP_MINIMUMHEIGHT";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("ICAP_MINIMUMHEIGHT not found, we'll use 2 inches: " + szTwidentity);
                    iMinHeightMicrons = (2 * 25400);
                }
                else
                {
                    aszContainer = CSV.Parse(szCapability);
                    iMinHeightMicrons = (int)(double.Parse(aszContainer[3]) * 25400);
                }

                // Get the minimum width...
                int iMinWidthMicrons = 0;
                szStatus = "";
                szCapability = "ICAP_MINIMUMWIDTH";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("ICAP_MINIMUMWIDTH error, we'll use 2 inches: " + szTwidentity);
                    iMinWidthMicrons = (2 * 25400);
                }
                else
                {
                    aszContainer = CSV.Parse(szCapability);
                    iMinWidthMicrons = (int)(double.Parse(aszContainer[3]) * 25400);
                }

                // Update the object
                szObject += "            \"height\": [" + iMinHeightMicrons + "," + iMaxHeightMicrons + "],\n";

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // width...
                #region width...

                // Update the object
                szObject += "            \"width\": [" + iMinWidthMicrons + "," + iMaxWidthMicrons + "],\n";

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // offsetX...
                #region offsetX...

                // Update the object
                szObject += "            \"offsetX\": [0," + (iMaxWidthMicrons - iMinWidthMicrons) + "],\n";

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // offsetY...
                #region offsetY...

                // Update the object
                szObject += "            \"offsetY\": [0," + (iMaxHeightMicrons - iMinHeightMicrons) + "],\n";

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // cropping...
                #region cropping...

                // Get the enumeration...
                szStatus = "";
                szCapability = "ICAP_AUTOMATICBORDERDETECTION";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GET", ref szCapability, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    szObject += "            \"cropping\": [\"fixed\"],\n";
                }
                else
                {
                    // Parse it...
                    aszContainer = CSV.Parse(szCapability);

                    // Handle the container...
                    szValues = "";
                    switch (aszContainer[1])
                    {
                        default:
                            TWAINWorkingGroup.Log.Info("ICAP_AUTOMATICBORDERDETECTION unsupported container: " + szTwidentity);
                            continue;

                        // These containers are just off by an index, so we can combine them.
                        // We should be checking the bitdepth, just to be sure, but this is a
                        // real edge case that shouldn't matter 99% of the time...
                        case "TWON_ONEVALUE":
                        case "TWON_ENUMERATION":
                            for (iEnum = (aszContainer[1] == "TWON_ONEVALUE") ? 3 : 6; iEnum < aszContainer.Length; iEnum++)
                            {
                                switch (aszContainer[iEnum])
                                {
                                    default:
                                        break;
                                    case "0": // FALSE
                                        if (!szValues.Contains("fixed"))
                                        {
                                            szValues += (szValues == "") ? "\"fixed\"" : ",\"fixed\"";
                                        }
                                        break;
                                    case "1": // TRUE
                                        if (!szValues.Contains("auto"))
                                        {
                                            szValues += (szValues == "") ? "\"auto\"" : ",\"auto\"";
                                        }
                                        break;
                                }
                            }
                            break;
                    }

                    // Add to the list...
                    if (szValues != "")
                    {
                        szObject += "            \"cropping\": [" + szValues + "],\n";
                    }
                    else
                    {
                        TWAINWorkingGroup.Log.Info("ICAP_AUTOMATICBORDERDETECTION no recognized values: " + szTwidentity);
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // pixelFormat...
                #region pixelFormat...

                // Get the enumeration...
                szStatus = "";
                szCapability = "ICAP_PIXELTYPE";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GET", ref szCapability, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("ICAP_PIXELTYPE error: " + szTwidentity);
                    continue;
                }

                // Parse it...
                aszContainer = CSV.Parse(szCapability);

                // Handle the container...
                szValues = "";
                switch (aszContainer[1])
                {
                    default:
                        TWAINWorkingGroup.Log.Info("ICAP_PIXELTYPE unsupported container: " + szTwidentity);
                        continue;

                    // These containers are just off by an index, so we can combine them.
                    // We should be checking the bitdepth, just to be sure, but this is a
                    // real edge case that shouldn't matter 99% of the time...
                    case "TWON_ONEVALUE":
                    case "TWON_ENUMERATION":
                        for (iEnum = (aszContainer[1] == "TWON_ONEVALUE") ? 3 : 6; iEnum < aszContainer.Length; iEnum++)
                        {
                            switch (aszContainer[iEnum])
                            {
                                default:
                                    break;
                                case "0": // TWPT_BW
                                    if (!szValues.Contains("bw1"))
                                    {
                                        szValues += (szValues == "") ? "\"bw1\"" : ",\"bw1\"";
                                    }
                                    break;
                                case "1": // TW_PT_GRAY
                                    if (!szValues.Contains("gray8"))
                                    {
                                        szValues += (szValues == "") ? "\"gray8\"" : ",\"gray8\"";
                                    }
                                    break;
                                case "2": // TWPT_RGB
                                    if (!szValues.Contains("rgb24"))
                                    {
                                        szValues += (szValues == "") ? "\"rgb24\"" : ",\"rgb24\"";
                                    }
                                    break;
                            }
                        }
                        break;
                }

                // Add to the list...
                if (szValues != "")
                {
                    szObject += "            \"pixelFormat\": [" + szValues + "],\n";
                }
                else
                {
                    TWAINWorkingGroup.Log.Info("ICAP_PIXELTYPE no recognized values: " + szTwidentity);
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // compression...
                #region compression...

                // Get the enumeration...
                szStatus = "";
                szCapability = "ICAP_COMPRESSION";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GET", ref szCapability, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("ICAP_COMPRESSION error: " + szTwidentity);
                    continue;
                }

                // Parse it...
                aszContainer = CSV.Parse(szCapability);

                // Handle the container...
                szValues = "";
                switch (aszContainer[1])
                {
                    default:
                        TWAINWorkingGroup.Log.Info("ICAP_COMPRESSION unsupported container: " + szTwidentity);
                        continue;

                    // These containers are just off by an index, so we can combine them...
                    case "TWON_ONEVALUE":
                    case "TWON_ENUMERATION":
                        for (iEnum = (aszContainer[1] == "TWON_ONEVALUE") ? 3 : 6; iEnum < aszContainer.Length; iEnum++)
                        {
                            switch (aszContainer[iEnum])
                            {
                                default:
                                    break;
                                case "0": // TWCP_NONE
                                    if (!szValues.Contains("none"))
                                    {
                                        szValues += (szValues == "") ? "\"none\"" : ",\"none\"";
                                    }
                                    break;
                                case "5": // TWCP_GROUP4
                                case "6": // TWCP_JPEG
                                    if (!szValues.Contains("autoVersion1"))
                                    {
                                        szValues += (szValues == "") ? "\"autoVersion1\"" : ",\"autoVersion1\"";
                                    }
                                    break;
                            }
                        }
                        break;
                }

                // Add to the list...
                if (szValues != "")
                {
                    szObject += "            \"compression\": [" + szValues + "]\n";
                }
                else
                {
                    TWAINWorkingGroup.Log.Info("ICAP_COMPRESSION no recognized values: " + szTwidentity);
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // We got this far, so add the object to the list we're building...
                szObject += "        }";
                szList += szObject;
            }
            szList += "\n    ]\n";
            szList += "}";

            // Take care of the last close, if we have one...
            if (szTwidentityLast != null)
            {
                szStatus = "";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_CLOSEDS", ref szTwidentityLast, ref szStatus);
                szTwidentityLast = null;
            }

            // We didn't find TWAIN or SANE content...
            if (!szList.Contains("twidentity") && !szList.Contains("sane"))
            {
                szList = "";
            }

            // Destroy the toolkit...
            twaincstoolkit.Cleanup();
            twaincstoolkit = null;

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
            string szStatus;
            string szTwainDriverIdentity;
            TWAINCSToolkit twaincstoolkit;
            TWAINCSToolkit.STS sts;

            // Create the toolkit...
            try
            {
                twaincstoolkit = new TWAINCSToolkit
                (
                    IntPtr.Zero,
                    null,
                    null,
                    null,
                    "TWAIN Working Group",
                    "TWAIN Sharp",
                    "SWORD-on-TWAIN",
                    2,
                    3,
                    new string[] { "DF_APP2", "DG_CONTROL", "DG_IMAGE" },
                    "USA",
                    "testing...",
                    "ENGLISH_USA",
                    1,
                    0,
                    false,
                    true,
                    (TWAINCSToolkit.RunInUiThreadDelegate)null,
                    null
                );
            }
            catch
            {
                twaincstoolkit = null;
                return (a_szTwainDriverIdentity);
            }

            // Ask the user to select a default...
            szStatus = "";
            szTwainDriverIdentity = "";
            sts = twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_USERSELECT", ref szTwainDriverIdentity, ref szStatus);
            if (sts != TWAINCSToolkit.STS.SUCCESS)
            {
                szTwainDriverIdentity = a_szTwainDriverIdentity;
            }

            // Destroy the toolkit...
            twaincstoolkit.Cleanup();
            twaincstoolkit = null;

            // All done...
            return (szTwainDriverIdentity);
        }

        /// <summary>
        /// Process a task...
        /// </summary>
        /// <param name="a_szWriteFolder">A place where we can write files</param>
        /// <param name="a_szDriver">The driver to use, or null if you want the function to figure it out</param>
        /// <param name="a_szFile">The full path and name of a file holding a SWORD task</param>
        /// <param name="m_blTwainLocal">true if TWAIN Local</param>
        /// <param name="a_twaincstoolkit">the TWAIN Toolkit</param>
        /// <param name="a_swordtask">result of the command</param>
        /// <returns>A sword object to monitor for end of scanning, or null, if the task failed</returns>
        public static Sword Task
        (
            string a_szWriteFolder,
            string a_szDriver,
            string a_szFile,
            bool m_blTwainLocal,
            TWAINCSToolkit a_twaincstoolkit,
            ref SwordTask a_swordtask
        )
        {
            bool blError;
            bool blSetAppCapabilities = false;
            Sword sword;

            // Create the SWORD object to manage all this stuff...
            sword = new Sword(a_twaincstoolkit);

            // Deserialize the JSON into a SWORD task...
            string szTask = File.ReadAllText(a_szFile);
            blError = sword.SwordDeserialize(szTask, new Guid("211a1e90-11e1-11e5-9493-1697f925ec7b"), ref a_swordtask);
            if (!blError)
            {
                TWAINWorkingGroup.Log.Error("Bad task..." + a_szFile);
                sword.Close();
                sword = null;
                return (null);
            }

            // Build a TWAIN task from the SWORD task...
            sword.m_twaintask = new TwainTask(a_swordtask);

            // Start processing the TWAIN task, if there is more
            // than one action they'll be each be dispatched in
            // turn from some other function made by the caller
            // using NextAction...
            if (!sword.Process(a_szDriver, true, false, out blError, ref a_swordtask, ref blSetAppCapabilities))
            {
                TWAINWorkingGroup.Log.Error("Task failed..." + a_szFile);
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
            m_blCancel = true;
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
            if ((m_twaincstoolkitCaller == null) && (m_twaincstoolkit != null))
            {
                m_twaincstoolkit.Cleanup();
                m_twaincstoolkit = null;
            }
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
            TWAINWorkingGroup.Log.Info("");
            TWAINWorkingGroup.Log.Info("sw> " + ((a_szTask != null) ? a_szTask : "(null)"));
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
        private bool TwainInquiry(bool a_blTestForTwainDirect)
        {
            string szStatus;

            // We've already done this function...
            if (m_blTwainInquiryCompleted)
            {
                return (true);
            }
            m_blTwainInquiryCompleted = true;

            // Give a clue where we are...
            TWAINWorkingGroup.Log.Info(" ");
            TWAINWorkingGroup.Log.Info("TwainInquiry begin...");

            // First pass, when we test for TWAIN Direct...
            if (a_blTestForTwainDirect)
            {
                // Is the device online?
                szStatus = TwainGetValue("CAP_DEVICEONLINE");
                m_blDeviceOnline = ((szStatus != null) && (szStatus == "1"));
                if (!m_blDeviceOnline)
                {
                    TWAINWorkingGroup.Log.Error("CAP_DEVICEONLINE if false...");
                    return (false);
                }

                // Can we turn the UI off...
                szStatus = TwainGetValue("CAP_UICONTROLLABLE");
                m_blUiControllable = ((szStatus != null) && (szStatus == "1"));
                if (!m_blUiControllable)
                {
                    TWAINWorkingGroup.Log.Error("CAP_UICONTROLLABLE isn't true...");
                    return (false);
                }

                // Can we detect paper?
                szStatus = TwainGetValue("CAP_PAPERDETECTABLE");
                m_blPaperDetectable = ((szStatus != null) && (szStatus == "1"));

                // Does the driver support DAT_TWAINDIRECT?
                m_blNativeTwainDirectSupport = false;
                szStatus = TwainGetContainer("CAP_SUPPORTEDDATS");
                if (    !string.IsNullOrEmpty(szStatus)
                    &&  (Config.Get("useDatTwaindirect", "yes") == "yes"))
                {
                    try
                    {
                        string[] asz = CSV.Parse(szStatus);
                        if (asz.Length < 5)
                        {
                            m_blNativeTwainDirectSupport = false;
                        }
                        else
                        {
                            int iNumItems;
                            if (int.TryParse(asz[3], out iNumItems))
                            {
                                string szTwainDirect = (((int)TWAIN.DG.CONTROL << 16) + (int)TWAIN.DAT.TWAINDIRECT).ToString();
                                for (int ii = 0; ii < iNumItems; ii++)
                                {
                                    if (asz[3 + ii] == szTwainDirect)
                                    {
                                        m_blNativeTwainDirectSupport = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        m_blNativeTwainDirectSupport = false;
                    }
                }

                // Does the driver support TWEI_TWAINDIRECTMETADATA?
                if (m_blNativeTwainDirectSupport)
                {
                    m_blNativeTwainDirectSupport = false;
                    szStatus = TwainGetContainer("ICAP_SUPPORTEDEXTIMAGEINFO");
                    if (string.IsNullOrEmpty(szStatus))
                    {
                        m_blNativeTwainDirectSupport = false;
                    }
                    else
                    {
                        try
                        {
                            string[] asz = CSV.Parse(szStatus);
                            if (asz.Length < 5)
                            {
                                m_blNativeTwainDirectSupport = false;
                            }
                            else
                            {
                                int iNumItems;
                                if (int.TryParse(asz[3], out iNumItems))
                                {
                                    string szMetadata = ((int)TWAIN.TWEI.TWAINDIRECTMETADATA).ToString();
                                    for (int ii = 0; ii < iNumItems; ii++)
                                    {
                                        if (asz[3 + ii] == szMetadata)
                                        {
                                            m_blNativeTwainDirectSupport = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            m_blNativeTwainDirectSupport = false;
                        }
                    }
                }

                // We'll be back with an a_blTestForTwainDirect of false, so
                // allow that to happen...
                if (!m_blNativeTwainDirectSupport)
                {
                    m_blTwainInquiryCompleted = false;
                }

                // All done...
                return (true);
            }

            // We only need this additional information, if the driver doesn't
            // support both DAT_TWAINDIRECT and TWEI_TWAINDIRECTMETADATA...
            if (!m_blNativeTwainDirectSupport)
            {
                string szCapability;
                TWAINCSToolkit.STS sts;

                // Reset the scanner.  This won't necessarily work for every device.
                // We're not going to treat it as a failure, though, because the user
                // should be able to get a factory default experience from their driver
                // in other ways.  Like from the driver's GUI.
                //
                // TBD: make sure the rest of the group is okay with this plan.
                szStatus = "";
                szCapability = ""; // don't need valid data for this call...
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_RESETALL", ref szCapability, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error("Process: MSG_RESETALL failed");
                }

                // Do we have a vendor ID?
                szStatus = TwainGetValue("CAP_CUSTOMINTERFACEGUID");
                try
                {
                    string[] asz = CSV.Parse(szStatus);
                    m_guidScanner = new Guid(asz[asz.Length - 1]);
                }
                catch
                {
                    m_guidScanner = new Guid("211a1e90-11e1-11e5-9493-1697f925ec7b");
                }

                // Can we automatically sense the medium?
                szStatus = TwainGetContainer("CAP_AUTOMATICSENSEMEDIUM");
                if (szStatus == null)
                {
                    m_blAutomaticSenseMedium = false;
                }
                else
                {
                    try
                    {
                        string[] asz = CSV.Parse(szStatus);
                        if (asz.Length < 7)
                        {
                            m_blAutomaticSenseMedium = false;
                        }
                        else
                        {
                            for (int ii = 0; ii < int.Parse(asz[3]); ii++)
                            {
                                if (asz[6 + ii] == "1")
                                {
                                    m_blAutomaticSenseMedium = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        m_blAutomaticSenseMedium = false;
                    }
                }

                // Can we detect the source?
                szStatus = TwainGetValue("CAP_FEEDERENABLED");
                m_blFeederEnabled = (szStatus != null);

                // Can we detect color?
                szStatus = TwainGetValue("ICAP_AUTOMATICCOLORENABLED");
                m_blAutomaticColorEnabled = (szStatus != null);

                // Can we get extended image information?
                szStatus = TwainGetValue("ICAP_EXTIMAGEINFO");
                m_blExtImageInfo = (szStatus != null);

                // All done...
                TWAINWorkingGroup.Log.Info(" ");
                TWAINWorkingGroup.Log.Info("TwainInquiry completed...");
            }

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
        private string TwainSelectStream(ref SwordTask a_swordtask)
        {
            string szStatus;
            string szSourceReply = "";

            // Give a clue where we are...
            TWAINWorkingGroup.Log.Info(" ");
            TWAINWorkingGroup.Log.Info("TwainSelectStream begin...");

            // We have no action, technically, we shouldn't be here...
            if (    (m_twaintask == null)
                ||  (m_twaintask.m_twainaction == null)
                ||  (m_twaintask.m_twainaction[m_iAction] == null))
            {
                TWAINWorkingGroup.Log.Info("TwainSelectStream: null task");
                a_swordtask.SetTaskReply
                (
                    "{\n" +
                    "            }\n"
                );
                return ("success");
            }

            // Let's make things more convenient...
            TwainAction twainaction = m_twaintask.m_twainaction[m_iAction];

            // We have no streams...
            if (twainaction.m_twainstream == null)
            {
                TWAINWorkingGroup.Log.Info("TwainSelectStream: default scanning mode (task has no streams)");
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
            for (iTwainStream = 0; (twainaction.m_twainstream != null) && (iTwainStream < twainaction.m_twainstream.Length); iTwainStream++)
            {
                TwainStream twainstream = twainaction.m_twainstream[iTwainStream];
                TWAINWorkingGroup.Log.Info("Stream #" + iTwainStream);

                // We can have more than one source in a stream, so do the reset up here...
                szSourceReply = "";

                // Analyze the sources...
                szStatus = "success";
                int iTwainSource;
                for (iTwainSource = 0; (twainstream.m_twainsource != null) && (iTwainSource < twainstream.m_twainsource.Length); iTwainSource++)
                {
                    TwainSource twainsource = twainstream.m_twainsource[iTwainSource];
                    TWAINWorkingGroup.Log.Info("TwainSelectStream: source #" + iTwainSource);

                    // Set the source...
                    string szSource;
                    szStatus = twainsource.SetSource(m_twaincstoolkit, m_guidScanner, m_blAutomaticSenseMedium, m_blFeederEnabled, out szSource, ref a_swordtask);
                    if (szStatus == "skip")
                    {
                        TWAINWorkingGroup.Log.Info("TwainSelectStream: source belongs to another vendor, so skipping it");
                        continue;
                    }
                    else if (szStatus != "success")
                    {
                        TWAINWorkingGroup.Log.Info("TwainSelectStream: source exception: " + szStatus);
                        break;
                    }

                    // Uh-oh, no pixelFormat...
                    if ((twainsource.m_twainformat == null) || (twainsource.m_twainformat.Length == 0))
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
                    for (iTwainFormat = 0; (twainsource.m_twainformat != null) && (iTwainFormat < twainsource.m_twainformat.Length); iTwainFormat++)
                    {
                        TwainPixelFormat twainpixelformat = twainsource.m_twainformat[iTwainFormat];
                        TWAINWorkingGroup.Log.Info("TwainSelectStream: pixelFormat #" + iTwainFormat);

                        // Pick a color...
                        szStatus = TwainSetValue(twainpixelformat.m_capabilityPixeltype, ref a_swordtask, ref szSourceReply);
                        if (szStatus != "success")
                        {
                            TWAINWorkingGroup.Log.Info("TwainSelectStream: pixelFormat exception: " + szStatus);
                            break;
                        }

                        // Resolution...
                        szStatus = TwainSetValue(twainpixelformat.m_capabilityResolution, ref a_swordtask, ref szSourceReply);
                        if (szStatus != "success")
                        {
                            TWAINWorkingGroup.Log.Info("TwainSelectStream: resolution exception: " + szStatus);
                            break;
                        }

                        // Compression...
                        szStatus = TwainSetValue(twainpixelformat.m_capabilityCompression, ref a_swordtask, ref szSourceReply);
                        if (szStatus != "success")
                        {
                            TWAINWorkingGroup.Log.Info("TwainSelectStream: compression exception: " + szStatus);
                            break;
                        }

                        // Xfercount...
                        szStatus = TwainSetValue(twainpixelformat.m_capabilityXfercount, ref a_swordtask, ref szSourceReply);
                        if (szStatus != "success")
                        {
                            TWAINWorkingGroup.Log.Info("TwainSelectStream: xfercount exception: " + szStatus);
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

                // Reset the driver so we start from a clean slate...
                szStatus = "";
                string szCapability = ""; // don't need valid data for this call...
                m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_RESETALL", ref szCapability, ref szStatus);
            }

            // Check the status from the stream, and if it's not success exit from the
            // function with the exception...
            switch (szStatus)
            {
                // Just drop down, we're okay...
                default:
                case "ignore":
                case "success":
                    TWAINWorkingGroup.Log.Info("TwainSelectStream: stream search ended in " + szStatus);
                    break;

                // The task has failed.  "nextStream" results in a failure if it was
                // explicitly set by the task for the last stream.  By default the
                // last stream is going to be "ignore"...
                case "fail":
                case "nextStream":
                    TWAINWorkingGroup.Log.Info("TwainSelectStream: stream search ended in error, " + szStatus);
                    return ("fail");
            }

            // Check the image source, so we can properly report it later in
            // the metadata.  If the feederenabled isn't supported, or if it
            // returns 0 (FALSE), then we're scanning from the flatbed...
            szStatus = TwainGetValue("CAP_FEEDERENABLED");
            if ((szStatus == null) || (szStatus == "0"))
            {
                m_blFlatbed = true;
            }

            // Otherwise...
            else
            {
                // Assume we're scanning from an ADF...
                m_blFlatbed = false;

                // Check the automatic sense medium...
                if (m_blAutomaticSenseMedium)
                {
                    szStatus = TwainGetValue("CAP_AUTOMATICSENSEMEDIUM");
                    if ((szStatus != null) && (szStatus == "1"))
                    {
                        // If we find it, check for paper...
                        szStatus = TwainGetValue("CAP_FEEDERLOADED");
                        if ((szStatus != null) && (szStatus == "0"))
                        {
                            // There's no paper, so it's going to be the flatbed...
                            m_blFlatbed = true;
                        }
                    }
                }
            }
            TWAINWorkingGroup.Log.Info("TwainSelectStream: source is a flatbed, " + m_blFlatbed);

            // Are we duplex?
            m_blDuplex = false;
            if (!m_blFlatbed)
            {
                szStatus = TwainGetValue("CAP_DUPLEXENABLED");
                m_blDuplex = ((szStatus != null) && (szStatus == "1"));
            }

            // We're good...
            TWAINWorkingGroup.Log.Info("TwainSelectStream completed...");
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
            TWAINWorkingGroup.Log.Info("");
            TWAINWorkingGroup.Log.Info("Process begin...");

            // Init stuff...
            a_blError = false;
            m_blIgnoreTaskScan = a_blIgnoreTaskScan;

            // Do this bit prior to executing the first action...
            if (a_blFirstAction)
            {
                // Set our action index...
                m_iAction = -1;
            }

            // Dispatch an action...
            blStatus = Action(out blError, ref a_swordtask, ref a_blSetAppCapabilities);
            if (!blStatus)
            {
                TWAINWorkingGroup.Log.Error("Process: action failed");
                a_blError = true;
                return (false);
            }

            // All done...
            TWAINWorkingGroup.Log.Info("Process completed...");
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
            string szStatus;
            string szCapability;
            string szUserInterface;
            TWAINCSToolkit.STS sts;
            TWAINWorkingGroup.Log.Info("");
            TWAINWorkingGroup.Log.Info("Action...");

            // Init stuff (just to be sure)...
            a_blError = false;
            m_blProcessing = false;

            // Go to the next action (this includes the first action)...
            m_iAction += 1;
            if (    (m_twaintask.m_twainaction == null)
                ||  (m_iAction >= m_twaintask.m_twainaction.Length)
                ||  (m_twaintask.m_twainaction[m_iAction] == null))
            {
                TWAINWorkingGroup.Log.Info("Action: end of actions...");
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
            TwainAction twainaction = m_twaintask.m_twainaction[m_iAction];
            TWAINWorkingGroup.Log.Info("Action: " + twainaction.m_szAction);
            switch (twainaction.m_szAction)
            {
                // We've got a command that's new to us.  Our default
                // behavior is to keep going.
                default:
                    if (twainaction.m_szException == "fail")
                    {
                        TWAINWorkingGroup.Log.Error("Action: unrecognized action...<" + twainaction.m_szAction + ">");
                        a_swordtask.SetTaskError("fail", twainaction.m_szJsonKey + ".action", twainaction.m_szAction, -1);
                        a_blError = true;
                        return (false);
                    }
                    TWAINWorkingGroup.Log.Info("Action: unrecognized action...<" + twainaction.m_szAction + ">");
                    return (true);

                // Scan...
                case "scan":
                    // Skip scanning (this is for TWAIN Local, which uses a separate command)...
                    if (m_blIgnoreTaskScan)
                    {
                        return (true);
                    }

                    // Memory transfer...
                    szStatus = "";
                    szCapability = "ICAP_XFERMECH,TWON_ONEVALUE,TWTY_UINT16,2";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAINCSToolkit.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Info("Action: we can't set ICAP_XFERMECH to TWSX_MEMORY");
                        a_blError = true;
                        return (false);
                    }

                    // No UI...
                    szStatus = "";
                    szCapability = "CAP_INDICATORS,TWON_ONEVALUE,TWTY_BOOL,0";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAINCSToolkit.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Error("Action: we can't set CAP_INDICATORS to FALSE");
                        a_blError = true;
                        return (false);
                    }

                    // Ask for extended image info...
                    if (m_blExtImageInfo)
                    {
                        szStatus = "";
                        szCapability = "ICAP_EXTIMAGEINFO,TWON_ONEVALUE,TWTY_BOOL,1";
                        sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                        if (sts != TWAINCSToolkit.STS.SUCCESS)
                        {
                            TWAINWorkingGroup.Log.Warn("Action: we can't set ICAP_EXTIMAGEINFO to TRUE");
                            m_blExtImageInfo = false;
                        }
                    }

                    // Make a note that we successfully set these capabilities, so that
                    // we won't have to do it again when scanning starts...
                    a_blSetAppCapabilities = true;

                    // Start scanning (no UI)...
                    szStatus = "";
                    szUserInterface = "0,0";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_USERINTERFACE", "MSG_ENABLEDS", ref szUserInterface, ref szStatus);
                    if (sts != TWAINCSToolkit.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Info("Action: MSG_ENABLEDS failed");
                        a_blError = true;
                        return (false);
                    }

                    // All done...
                    TWAINWorkingGroup.Log.Info("Action: Scanning started");
                    m_blProcessing = true;
                    return (true);

                // Configure...
                case "configure":
                    // Memory transfer...
                    szStatus = "";
                    szCapability = "ICAP_XFERMECH,TWON_ONEVALUE,TWTY_UINT16,2";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAINCSToolkit.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Info("Action: we can't set ICAP_XFERMECH to TWSX_MEMORY");
                        a_swordtask.SetTaskError("twainDirectError", twainaction.m_szJsonKey + ".action", "", -1);
                        a_blError = true;
                        return (false);
                    }

                    // No UI...
                    szStatus = "";
                    szCapability = "CAP_INDICATORS,TWON_ONEVALUE,TWTY_BOOL,0";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAINCSToolkit.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Error("Action: we can't set CAP_INDICATORS to FALSE");
                        a_swordtask.SetTaskError("twainDirectError", twainaction.m_szJsonKey + ".action", "", -1);
                        a_blError = true;
                        return (false);
                    }

                    // Ask for extended image info...
                    if (m_blExtImageInfo)
                    {
                        szStatus = "";
                        szCapability = "ICAP_EXTIMAGEINFO,TWON_ONEVALUE,TWTY_BOOL,1";
                        sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                        if (sts != TWAINCSToolkit.STS.SUCCESS)
                        {
                            TWAINWorkingGroup.Log.Warn("Action: we can't set ICAP_EXTIMAGEINFO to TRUE");
                            a_swordtask.SetTaskError("twainDirectError", twainaction.m_szJsonKey + ".action", "", -1);
                            m_blExtImageInfo = false;
                        }
                    }

                    // Make a note that we successfully set these capabilities, so that
                    // we won't have to do it again when scanning starts...
                    a_blSetAppCapabilities = true;

                    // Pick a stream...
                    if (TwainSelectStream(ref a_swordtask) != "success")
                    {
                        TWAINWorkingGroup.Log.Error("Action: TwainSelectStream failed");
                        a_blError = true;
                        return (false);
                    }

                    // We're all done with this command...
                    TWAINWorkingGroup.Log.Info("Action complete...");
                    return (true);
            }
        }

        /// <summary>
        /// Get a TWAIN capability value...
        /// </summary>
        /// <param name="a_szName">The name of the capabilty we want to get</param>
        /// <returns></returns>
        private string TwainGetValue(string a_szName)
        {
            string szStatus;
            string szCapability;
            TWAINCSToolkit.STS sts;

            // Get the value...
            szStatus = "";
            szCapability = a_szName;
            sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
            if (sts != TWAINCSToolkit.STS.SUCCESS)
            {
                return (null);
            }

            // Collect the value...
            string[] asz = CSV.Parse(szCapability);
            if ((asz == null) || (asz.Length != 4))
            {
                return (null);
            }

            // All done...
            return (asz[3]);
        }

        /// <summary>
        /// Get a TWAIN capability container...
        /// </summary>
        /// <param name="a_szName">The name of the capability we want to get</param>
        /// <returns></returns>
        private string TwainGetContainer(string a_szName)
        {
            string szStatus;
            string szCapability;
            TWAINCSToolkit.STS sts;

            // Get the value...
            szStatus = "";
            szCapability = a_szName;
            sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GET", ref szCapability, ref szStatus);
            if (sts != TWAINCSToolkit.STS.SUCCESS)
            {
                return (null);
            }

            // All done...
            return (szCapability);
        }

        /// <summary>
        /// Set a TWAIN capability value...
        /// </summary>
        /// <param name="a_capability">The stuff we want to set</param>
        /// <param name="a_szTwainValue">the TWAIN value we set</param>
        /// <param name="a_swordtask">result of the command</param>
        /// <returns></returns>
        private string TwainSetValue(Capability a_capability, ref SwordTask a_swordtask, ref string a_szSourceReply)
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
            szStatus = a_capability.SetScanner(m_twaincstoolkit, m_guidScanner, out szSwordName, out szSwordValue, out szTwainValue, ref a_swordtask);

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
            // Create the toolkit...
            try
            {
                m_twaincstoolkit = new TWAINCSToolkit
                (
                    IntPtr.Zero,
                    null,
                    null,
                    null,
                    "TWAIN Working Group",
                    "TWAIN Sharp",
                    "SWORD-on-TWAIN",
                    2,
                    3,
                    new string[] { "DF_APP2", "DG_CONTROL", "DG_IMAGE" },
                    "USA",
                    "testing...",
                    "ENGLISH_USA",
                    1,
                    0,
                    false,
                    true,
                    (TWAINCSToolkit.RunInUiThreadDelegate)null,
                    this
                );
            }
            catch
            {
                TWAINWorkingGroup.Log.Warn("Error creating toolkit...");
                m_twaincstoolkit = null;
                return (null);
            }

            // Get the default driver...
            if (a_szScanner == null)
            {
                m_szTwainDriverIdentity = "";
                if (m_twaincstoolkit.GetDrivers(ref m_szTwainDriverIdentity) == null)
                {
                    TWAINWorkingGroup.Log.Warn("No TWAIN drivers found...");
                    m_szTwainDriverIdentity = null;
                    return (null);
                }
            }

            // Otherwise, look for a match...
            else
            {
                string szStatus;
                string szMsg = "MSG_GETFIRST";
                TWAINCSToolkit.STS sts;
                while (true)
                {
                    szStatus = "";
                    m_szTwainDriverIdentity = "";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", szMsg, ref m_szTwainDriverIdentity, ref szStatus);
                    if (sts != TWAINCSToolkit.STS.SUCCESS)
                    {
                        m_szTwainDriverIdentity = "";
                        break;
                    }
                    if (m_szTwainDriverIdentity.EndsWith("," + a_szScanner))
                    {
                        break;
                    }
                    szMsg = "MSG_GETNEXT";
                }
            }

            // Destroy the toolkit...
            m_twaincstoolkit.Cleanup();
            m_twaincstoolkit = null;

            // All done...
            return (m_szTwainDriverIdentity);
        }

        /// <summary>
        /// Handle an image...
        /// </summary>
        /// <param name="a_szTag">Tag to locate a particular ReportImage call</param>
        /// <param name="a_szDg">Data group that preceeded this call</param>
        /// <param name="a_szDat">Data argument type that preceeded this call</param>
        /// <param name="a_szMsg">Message that preceeded this call</param>
        /// <param name="a_sts">Current status</param>
        /// <param name="a_bitmap">C# bitmap of the image</param>
        /// <param name="a_szFile">File name, if doing a file transfer</param>
        /// <param name="a_szTwimageinfo">Image info or null</param>
        /// <param name="a_abImage">raw image from transfer</param>
        /// <param name="a_iImageOffset">byte offset into the image</param>
        private TWAINCSToolkit.MSG ReportImage
        (
            string a_szTag,
            string a_szDg,
            string a_szDat,
            string a_szMsg,
            TWAINCSToolkit.STS a_sts,
            Bitmap a_bitmap,
            string a_szFile,
            string a_szTwimageinfo,
            byte[] a_abImage,
            int a_iImageOffset
        )
        {
            uint uu;
            string szFile;
            string szImageFile;
            string szValue;
            string szSeparator;
            TWAIN.STS sts;
            TWAIN twain;

            // We're processing end of scan...
            if (a_bitmap == null)
            {
                TWAINWorkingGroup.Log.Info("ReportImage: no more images: " + a_szDg + " " + a_szDat + " " + a_szMsg + " " + a_sts);
                m_blProcessing = false;
                m_blCancel = false;
                return (TWAINCSToolkit.MSG.RESET);
            }

            // Init stuff...
            twain = m_twaincstoolkit.Twain();

            // Get the metadata for TW_IMAGEINFO...
            TWAIN.TW_IMAGEINFO twimageinfo = default(TWAIN.TW_IMAGEINFO);
            if (a_szTwimageinfo != null)
            {
                twain.CsvToImageinfo(ref twimageinfo, a_szTwimageinfo);
            }
            else
            {
                sts = twain.DatImageinfo(TWAIN.DG.IMAGE, TWAIN.MSG.GET, ref twimageinfo);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error("ReportImage: DatImageinfo failed...");
                    m_blProcessing = false;
                    m_blCancel = false;
                    return (TWAINCSToolkit.MSG.RESET);
                }
            }

            // Get the metadata for TW_EXTIMAGEINFO...
            TWAIN.TW_EXTIMAGEINFO twextimageinfo = default(TWAIN.TW_EXTIMAGEINFO);
            TWAIN.TW_INFO twinfo = default(TWAIN.TW_INFO);
            if (m_blExtImageInfo)
            {
                twextimageinfo.NumInfos = 0;
                twinfo.InfoId = (ushort)TWAIN.TWEI.PAGESIDE; twextimageinfo.Set(twextimageinfo.NumInfos++, ref twinfo);
                sts = twain.DatExtimageinfo(TWAIN.DG.IMAGE, TWAIN.MSG.GET, ref twextimageinfo);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    m_blExtImageInfo = false;
                }
            }

            // Make sure we have a folder...
            string szFolder = Path.Combine(m_szWriteFolder, "images");
            if (!Directory.Exists(szFolder))
            {
                try
                {
                    Directory.CreateDirectory(szFolder);
                }
                catch
                {
                    TWAINWorkingGroup.Log.Error("ReportImage: unable to create the image destination directory...");
                    m_blProcessing = false;
                    m_blCancel = false;
                    return (TWAINCSToolkit.MSG.RESET);
                }
            }

            // Create a filename...
            m_iImageCount += 1;
            szFile = szFolder + Path.DirectorySeparatorChar + "img" + m_iImageCount.ToString("D6");

            // Cleanup...
            if (File.Exists(szFile + ".pdf"))
            {
                try
                {
                    File.Delete(szFile + ".pdf");
                }
                catch
                {
                    TWAINWorkingGroup.Log.Error("ReportImage: unable to delete the file...");
                    m_blProcessing = false;
                    m_blCancel = false;
                    return (TWAINCSToolkit.MSG.RESET);
                }
            }

            // Save the file to disk...
            try
            {
                if (twimageinfo.Compression == (ushort)TWAIN.TWCP.JPEG)
                {
                    szImageFile = szFile + ".jpg";
                    a_bitmap.SetResolution(twimageinfo.XResolution.Whole, twimageinfo.YResolution.Whole);
                    a_bitmap.Save(szImageFile, ImageFormat.Jpeg);
                    TWAINWorkingGroup.Log.Info("ReportImage: saved " + szImageFile);
                }
                else
                {
                    szImageFile = szFile + ".tif";
                    a_bitmap.SetResolution(twimageinfo.XResolution.Whole, twimageinfo.YResolution.Whole);
                    a_bitmap.Save(szImageFile, ImageFormat.Tiff);
                    TWAINWorkingGroup.Log.Info("ReportImage: saved " + szImageFile);
                }
            }
            catch
            {
                TWAINWorkingGroup.Log.Error("ReportImage: unable to save the image file...");
                m_blProcessing = false;
                m_blCancel = false;
                return (TWAINCSToolkit.MSG.RESET);
            }

            // Create the metadata...
            string szMeta = "";

            // Open SWORD...
            if (!m_blTwainLocal)
            {
                szMeta += "{\n";
            }

            // Open SWORD.metadata...
            szMeta += "    \"metadata\":{\n";

            // Open SWORD.metadata.status...
            szMeta += "        \"status\": [\n";

            // Add the status...
            szMeta += "            {\n";
            szMeta += "                \"success\": true\n";
            szMeta += "            }\n";

            // Close sword.metadata.status...
            szMeta += "        ],\n";

            // Open SWORD.metadata.address...
            szMeta += "        \"address\": [\n";
            szSeparator = "";

            // Imagecount (counts images)...
            szMeta += szSeparator;
            szMeta += "            {\n";
            szMeta += "                \"id\": \"imagecount\",\n";
            szMeta += "                \"value\": " + m_iImageCount + "\n";
            szMeta += "            }";
            szSeparator = ",\n";

            // The image came from a flatbed...
            if (m_blFlatbed)
            {
                szMeta += szSeparator;
                szMeta += "            {\n";
                szMeta += "                \"id\": \"imagesource\",\n";
                szMeta += "                \"value\": \"flatbed\"\n";
                szMeta += "            }";
                szSeparator = ",\n";
            }

            // The image came from a feeder...
            else
            {
                bool blFoundPageSide = false;

                // See if we can get the side from the extended image info...
                if (m_blExtImageInfo)
                {
                    for (uu = 0; uu < twextimageinfo.NumInfos; uu++)
                    {
                        twextimageinfo.Get(uu, ref twinfo);
                        if (twinfo.InfoId == (ushort)TWAIN.TWEI.PAGESIDE)
                        {
                            if (twinfo.ReturnCode == (ushort)TWAIN.STS.SUCCESS)
                            {
                                blFoundPageSide = true;
                                if (twinfo.Item == (UIntPtr)TWAIN.TWCS.TOP)
                                {
                                    szValue = "feederfront";
                                }
                                else
                                {
                                    szValue = "feederrear";
                                }
                                szMeta += szSeparator;
                                szMeta += "            {\n";
                                szMeta += "                \"id\": \"imagesource\",\n";
                                szMeta += "                \"value\": \"" + szValue + "\"\n";
                                szMeta += "            }";
                                szSeparator = ",\n";
                            }
                            break;
                        }
                    }
                }

                // We didn't get a pageside.  So we're going to make
                // the best guess we can.
                if (!blFoundPageSide)
                {
                    // We're just doing simplex front at the moment...
                    if (!m_blDuplex)
                    {
                        szMeta += szSeparator;
                        szMeta += "            {\n";
                        szMeta += "                \"id\": \"imagesource\",\n";
                        szMeta += "                \"value\": \"feederFront\"\n";
                        szMeta += "            }";
                        szSeparator = ",\n";
                    }

                    // We're duplex...
                    else
                    {
                        // Odd number images (we start at 1)...
                        if ((m_iImageCount & 1) == 1)
                        {
                            szValue = "feederFront";
                        }
                        // Even number images...
                        else
                        {
                            szValue = "feederRear";
                        }
                        szMeta += szSeparator;
                        szMeta += "            {\n";
                        szMeta += "                \"id\": \"imagesource\",\n";
                        szMeta += "                \"value\": \"" + szValue + "\"\n";
                        szMeta += "            }";
                        szSeparator = ",\n";
                    }
                }
            }

            // Segmentcount (long document or huge document)...
            szMeta += szSeparator;
            szMeta += "            {\n";
            szMeta += "                \"id\": \"segmentcount\",\n";
            szMeta += "                \"value\": " + "1" + "\n";
            szMeta += "            }";
            szSeparator = ",\n";

            // Segmentlast (long document or huge document)...
            szMeta += szSeparator;
            szMeta += "            {\n";
            szMeta += "                \"id\": \"segmentlast\",\n";
            szMeta += "                \"value\": \"" + "yes" + "\"\n";
            szMeta += "            }";
            szSeparator = ",\n";

            // Sheetcount (counts sheets, including ones lost to blank image dropout)...
            szMeta += szSeparator;
            szMeta += "            {\n";
            szMeta += "                \"id\": \"sheetcount\",\n";
            szMeta += "                \"value\": " + "1" + "\n";
            szMeta += "            }";
            szSeparator = ",\n";

            // Sheetimagecount (resets to 1 on every side of a sheet of paper)...
            szMeta += szSeparator;
            szMeta += "            {\n";
            szMeta += "                \"id\": \"sheetimagecount\",\n";
            szMeta += "                \"value\": " + "1" + "\n";
            szMeta += "            }";
            szSeparator = ",\n";

            // Final separator (no comma)...
            if (szSeparator != "")
            {
                szMeta += "\n";
            }

            // Close sword.metadata.address...
            szMeta += "        ],\n";

            // Open SWORD.metadata.image...
            szMeta += "        \"image\": [\n";
            szSeparator = "";

            // Add compression...
            switch (twimageinfo.Compression)
            {
                default:
                    m_blProcessing = false;
                    m_blCancel = false;
                    //if (m_twaincstoolkitCaller == null)
                    //{
                    //    m_twaincstoolkit.Cleanup();
                    //    m_twaincstoolkit = null;
                    //}
                    return (TWAINCSToolkit.MSG.RESET);
                case (ushort)TWAIN.TWCP.GROUP4:
                    szValue = "group4";
                    break;
                case (ushort)TWAIN.TWCP.JPEG:
                    szValue = "jpeg";
                    break;
                case (ushort)TWAIN.TWCP.NONE:
                    szValue = "none";
                    break;
            }
            szMeta += szSeparator;
            szMeta += "            {\n";
            szMeta += "                \"id\": \"compression\",\n";
            szMeta += "                \"value\": \"" + szValue + "\"\n";
            szMeta += "            },\n";

            // Add height...
            szMeta += szSeparator;
            szMeta += "            {\n";
            szMeta += "                \"id\": \"height\",\n";
            szMeta += "                \"value\": " + twimageinfo.ImageLength + "\n";
            szMeta += "            },\n";

            // Add imageformat...
            switch (twimageinfo.PixelType)
            {
                default:
                    m_blProcessing = false;
                    m_blCancel = false;
                    //if (m_twaincstoolkitCaller == null)
                    //{
                    //    m_twaincstoolkit.Cleanup();
                    //    m_twaincstoolkit = null;
                    //}
                    return (TWAINCSToolkit.MSG.RESET);
                case (short)TWAIN.TWPT.BW:
                    szValue = "bw1";
                    break;
                case (short)TWAIN.TWPT.GRAY:
                    szValue = "gray8";
                    break;
                case (short)TWAIN.TWPT.RGB:
                    szValue = "rgb24";
                    break;
            }
            szMeta += szSeparator;
            szMeta += "            {\n";
            szMeta += "                \"id\": \"pixelFormat\",\n";
            szMeta += "                \"value\": \"" + szValue + "\"\n";
            szMeta += "            },\n";

            // Add resolution...
            szMeta += szSeparator;
            szMeta += "            {\n";
            szMeta += "                \"id\": \"resolution\",\n";
            szMeta += "                \"value\": " + twimageinfo.XResolution.Whole + "\n";
            szMeta += "            },\n";

            // Add size...
            FileInfo fileinfo = new FileInfo(szImageFile);
            szMeta += szSeparator;
            szMeta += "            {\n";
            szMeta += "                \"id\": \"size\",\n";
            szMeta += "                \"value\": " + fileinfo.Length + "\n";
            szMeta += "            },\n";

            // Add width...
            szMeta += szSeparator;
            szMeta += "            {\n";
            szMeta += "                \"id\": \"width\",\n";
            szMeta += "                \"value\": " + twimageinfo.ImageWidth + "\n";
            szMeta += "            },\n";

            // X-offset...
            szMeta += szSeparator;
            szMeta += "            {\n";
            szMeta += "                \"id\": \"xoffset\",\n";
            szMeta += "                \"value\": " + "0" + "\n";
            szMeta += "            },\n";

            // Y-offset...
            szMeta += szSeparator;
            szMeta += "            {\n";
            szMeta += "                \"id\": \"yoffset\",\n";
            szMeta += "                \"value\": " + "0" + "\n";
            szMeta += "            }\n";

            // Close sword.metadata.image...
            szMeta += "        ]\n";

            // Close sword.metadata...
            szMeta += "    }\n";

            // Close SWORD...
            if (!m_blTwainLocal)
            {
                szMeta += "}";
            }

            // Save the metadata to disk...
            try
            {
                File.WriteAllText(szFile + ".txt", szMeta);
                TWAINWorkingGroup.Log.Info("ReportImage: saved " + szFile + ".txt");
            }
            catch
            {
                TWAINWorkingGroup.Log.Error("ReportImage: unable to save the metadata file...");
                m_blProcessing = false;
                m_blCancel = false;
                return (TWAINCSToolkit.MSG.RESET);
            }

            // We've been asked to cancel, so sneak that in...
            if (m_blCancel)
            {
                TWAIN.TW_PENDINGXFERS twpendingxfers = default(TWAIN.TW_PENDINGXFERS);
                sts = twain.DatPendingxfers(TWAIN.DG.CONTROL, TWAIN.MSG.STOPFEEDER, ref twpendingxfers);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error("ReportImage: DatPendingxfers failed...");
                    m_blProcessing = false;
                    m_blCancel = false;
                    return (TWAINCSToolkit.MSG.STOPFEEDER);
                }
            }

            // All done...
            return (TWAINCSToolkit.MSG.ENDXFER);
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
            /// m_twaintask - the list of actions to perform
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
            private TWAINCSToolkit m_twaincstoolkitCaller;
            private TWAINCSToolkit m_twaincstoolkit;
            private TwainTask m_twaintask;
            private int m_iAction;
            private bool m_blProcessing;
            private bool m_blCancel;
            private bool m_blFlatbed;
            private bool m_blDuplex;
            private bool m_blIgnoreTaskScan;

            /// <summary>
            /// Supported features...
            /// </summary>
            private bool m_blDeviceOnline;
            private bool m_blUiControllable;
            private bool m_blNativeTwainDirectSupport;
            private bool m_blTwainInquiryCompleted;
            private bool m_blPaperDetectable;
            private bool m_blAutomaticColorEnabled;
            private bool m_blAutomaticSenseMedium;
            private bool m_blFeederEnabled;
            private bool m_blExtImageInfo;
            private Guid m_guidScanner;

            /// <summary>
            /// Folder for our data...
            /// </summary>
            private string m_szWriteFolder;

            /// <summary>
            /// We're running in TWAIN Local...
            /// </summary>
            bool m_blTwainLocal;

            /// <summary>
            /// Image tracking information...
            /// </summary>
            private int m_iImageCount;

        #endregion
    }
}
