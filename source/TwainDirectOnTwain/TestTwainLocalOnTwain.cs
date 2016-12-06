// Helpers...
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using TwainDirectSupport;

namespace TwainDirectOnTwain
{
    public sealed class TestTwainLocalOnTwain
    {
        /// <summary>
        /// Initialize stuff...
        /// </summary>
        /// <param name="a_szScanner">our scanner name</param>
        /// <param name="a_szWriteFolder">the folder where images can go</param>
        /// <param name="a_iPid">our process id</param>
        /// <param name="a_szTaskFile">the task file to use</param>
        public TestTwainLocalOnTwain(string a_szScanner, string a_szWriteFolder, int a_iPid, string a_szTaskFile)
        {
            m_szScanner = a_szScanner;
            m_szWriteFolder = a_szWriteFolder;
            m_iPid = a_iPid;
            m_szIpc = Path.Combine(m_szWriteFolder, "ipc");
            m_szTaskFile = Path.Combine(a_szWriteFolder, "tasks");
            m_szTaskFile = Path.Combine(m_szTaskFile, a_szTaskFile);
        }

        /// <summary>
        /// Test our ability to run the TWAIN driver using the TWAIN Direct Client-Scanner
        /// API as the controlling API.  This is easier to debug than running stuff across
        /// more than one process with the network involved...
        /// </summary>
        /// <returns></returns>
        public bool Test()
        {
            int ii;
            int[] aiImageBlockNum;
            long lResponseCharacterOffset;
            bool blSts;
            bool blEndOfJob;
            string szJson;
            Thread thread;
            Ipc ipc;
            JsonLookup jsonlookup;

            // Create our objects...
            m_twainlocalontwain = new TwainLocalOnTwain(m_szWriteFolder, m_szIpc, Process.GetCurrentProcess().Id, null, null, IntPtr.Zero);
            jsonlookup = new JsonLookup();
            ipc = new Ipc(m_szIpc, true);

            // Run in a thread...
            thread = new Thread(RunTest);
            thread.Start();

            // Wait for a connection...
            ipc.Accept();

            // Create Session...
            #region Create Session...

            // Open the scanner...
            blSts = ipc.Write
            (
                "{" +
                "\"method\":\"createSession\"," +
                "\"scanner\":\"" + m_szScanner + "\"" +
                "}"
            );
            if (!blSts)
            {
                TWAINWorkingGroup.Log.Error("Lost our process...");
                goto ABORT;
            }

            // Get the result...
            szJson = ipc.Read();

            // Analyze the result...
            try
            {
                jsonlookup.Load(szJson, out lResponseCharacterOffset);
                if (jsonlookup.Get("status") != "success")
                {
                    TWAINWorkingGroup.Log.Error("createSession failed: " + jsonlookup.Get("status"));
                    return (false);
                }
            }
            catch
            {
                TWAINWorkingGroup.Log.Error("createSession failed: JSON error");
                return (false);
            }

            #endregion


            // Set TWAIN Direct Options...
            #region Set TWAIN Direct Options...

            // Read the file as a sequence of UTF-8 bytes, and convert the data
            // to base64 for transmission to the TwainDirectOnTwain process...
            string szTask = File.ReadAllText(m_szTaskFile);

            // Send the data...
            blSts = ipc.Write
            (
                "{" +
                "\"method\":\"setTwainDirectOptions\"," +
                "\"task\":" + szTask +
                "}"
            );
            if (!blSts)
            {
                TWAINWorkingGroup.Log.Error("Lost our process...");
                goto ABORT;
            }

            // Get the result...
            szJson = ipc.Read();
            if (szJson == null)
            {
                TWAINWorkingGroup.Log.Error("Lost our process...");
                goto ABORT;
            }

            #endregion


            // Start Capturing...
            #region Start Capturing...

            blSts = ipc.Write
            (
                "{" +
                "\"method\":\"startCapturing\"" +
                "}"
            );
            if (!blSts)
            {
                TWAINWorkingGroup.Log.Error("Lost our process...");
                goto ABORT;
            }

            // Get the result...
            szJson = ipc.Read();
            if (szJson == null)
            {
                TWAINWorkingGroup.Log.Error("Lost our process...");
                goto ABORT;
            }

            #endregion


            // Loop until we run out of images...
            blEndOfJob = false;
            aiImageBlockNum = null;
            while (true)
            {
                // Get Session (wait for image)...
                #region GetSession (wait for images)...

                // Stay in this loop unti we get an image or an error...
                while (true)
                {
                    // Get the current session info...
                    blSts = ipc.Write
                    (
                        "{" +
                        "\"method\":\"getSession\"" +
                        "}"
                    );
                    if (!blSts)
                    {
                        TWAINWorkingGroup.Log.Error("Lost our process...");
                        goto ABORT;
                    }

                    // Get the result...
                    szJson = ipc.Read();
                    if (szJson == null)
                    {
                        TWAINWorkingGroup.Log.Error("Lost our process...");
                        goto ABORT;
                    }

                    // Parse it...
                    jsonlookup = new JsonLookup();
                    jsonlookup.Load(szJson, out lResponseCharacterOffset);

                    // Bail if we're end of job...
                    if (jsonlookup.Get("endOfJob") == "true")
                    {
                        blEndOfJob = true;
                        break;
                    }

                    // Collect the data...
                    try
                    {
                        aiImageBlockNum = null;
                        for (ii = 0; ; ii++)
                        {
                            // Get the data...
                            string szNum = jsonlookup.Get("session.imageBlocks[" + ii + "]");
                            if ((szNum == null) || (szNum == ""))
                            {
                                break;
                            }

                            // Convert it...
                            int iTmp = int.Parse(szNum);

                            // Add it to the list...
                            if (aiImageBlockNum == null)
                            {
                                aiImageBlockNum = new int[1];
                                aiImageBlockNum[0] = iTmp;
                            }
                            else
                            {
                                int[] aiTmp = new int[aiImageBlockNum.Length + 1];
                                aiImageBlockNum.CopyTo(aiTmp,0);
                                aiTmp[aiTmp.Length - 1] = iTmp;
                                aiImageBlockNum = aiTmp;
                            }
                        }
                    }
                    catch
                    {
                        // don't need to do anything...
                    }

                    // We got one!
                    if (aiImageBlockNum != null)
                    {
                        break;
                    }

                    // Snooze a bit...
                    Thread.Sleep(100);
                }

                // Bail if we're end of job...
                if (blEndOfJob)
                {
                    break;
                }

                #endregion


                // Read Image Block Metadata...
                #region Read Image Block Metadata...

                // Get this image's metadata...
                blSts = ipc.Write
                (
                    "{" +
                    "\"method\":\"readImageBlockMetadata\"," +
                    "\"imageBlockNum\":" + aiImageBlockNum[0] +
                    "}"
                );
                if (!blSts)
                {
                    TWAINWorkingGroup.Log.Error("Lost our process...");
                    goto ABORT;
                }

                // Get the result...
                szJson = ipc.Read();
                if (szJson == null)
                {
                    TWAINWorkingGroup.Log.Error("Lost our process...");
                    goto ABORT;
                }

                #endregion


                // Read Image Block...
                #region Read Image Block...

                // Get this image...
                blSts = ipc.Write
                (
                    "{" +
                    "\"method\":\"readImageBlock\"," +
                    "\"imageBlockNum\":" + aiImageBlockNum[0] +
                    "}"
                );
                if (!blSts)
                {
                    TWAINWorkingGroup.Log.Error("Lost our process...");
                    goto ABORT;
                }

                // Get the result...
                szJson = ipc.Read();
                if (szJson == null)
                {
                    TWAINWorkingGroup.Log.Error("Lost our process...");
                    goto ABORT;
                }

                #endregion


                // Release Image Block...
                #region Release Image Block...

                // Release this image...
                blSts = ipc.Write
                (
                    "{" +
                    "\"method\":\"releaseImageBlocks\"," +
                    "\"imageBlockNum\":" + aiImageBlockNum[0] + "," +
                    "\"lastImageBlockNum\":" + aiImageBlockNum[0] +
                    "}"
                );
                if (!blSts)
                {
                    TWAINWorkingGroup.Log.Error("Lost our process...");
                    goto ABORT;
                }

                // Get the result...
                szJson = ipc.Read();
                if (szJson == null)
                {
                    TWAINWorkingGroup.Log.Error("Lost our process...");
                    goto ABORT;
                }

                #endregion
            }


            // Stop Capturing...
            #region Stop Capturing...

            blSts = ipc.Write
            (
                "{" +
                "\"method\":\"stopCapturing\"" +
                "}"
            );
            if (!blSts)
            {
                TWAINWorkingGroup.Log.Error("Lost our process...");
                goto ABORT;
            }

            // Get the result...
            szJson = ipc.Read();
            if (szJson == null)
            {
                TWAINWorkingGroup.Log.Error("Lost our process...");
                goto ABORT;
            }

            #endregion


            // Close Session...
            #region Close Session...

            // Close the scanner...
            blSts = ipc.Write
            (
                "{" +
                "\"method\":\"closeSession\"" +
                "}"
            );
            if (!blSts)
            {
                TWAINWorkingGroup.Log.Error("Lost our process...");
                goto ABORT;
            }

            // Get the result...
            szJson = ipc.Read();
            if (szJson == null)
            {
                TWAINWorkingGroup.Log.Error("Lost our process...");
                goto ABORT;
            }

            // Exit the process...
            blSts = ipc.Write
            (
                "{" +
                "\"method\":\"exit\"" +
                "}"
            );
            if (!blSts)
            {
                TWAINWorkingGroup.Log.Error("Lost our process...");
                goto ABORT;
            }

            #endregion


            // All done...
            ABORT:
            thread.Join();
            return (true);
        }

        /// <summary>
        /// Run Run in its own thread...
        /// </summary>
        private void RunTest()
        {
            m_twainlocalontwain.Run();
        }

        TwainLocalOnTwain m_twainlocalontwain;
        private string m_szScanner;
        private string m_szWriteFolder;
        private int m_iPid;
        private string m_szIpc;
        private string m_szTaskFile;
    }
}
