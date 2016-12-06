// Helpers...
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using TwainDirectSupport;

namespace TwainDirectOnSane
{
    public sealed class TestTwainLocalOnSane : IDisposable
    {
        /// <summary>
        /// Init stuff...
        /// </summary>
        public TestTwainLocalOnSane(string a_szScanner, string a_szWriteFolder, int a_iPid, string a_szTask, string a_szIpc)
        {
            m_szScanner = a_szScanner;
            m_szWriteFolder = a_szWriteFolder;
            m_iPid = a_iPid;
            m_szIpc = a_szIpc; // Path.Combine(m_szWriteFolder, "ipc");
            m_szTask = Path.Combine(a_szWriteFolder, "tasks");
            m_szTask = Path.Combine(m_szTask, a_szTask);
        }

        /// <summary>
        /// Destructor...
        /// </summary>
        ~TestTwainLocalOnSane()
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
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        internal void Dispose(bool a_blDisposing)
        {
            // Free managed resources...
            if (a_blDisposing)
            {
                if (m_twainlocalonsane != null)
                {
                    m_twainlocalonsane.Dispose();
                    m_twainlocalonsane = null;
                }
            }
        }

        /// <summary>
        /// Test our ability to run the TWAIN driver using the TWAIN Direct Client-Scanner API
        /// as the controlling API.  This is easier to debug than running stuff
        /// across more than one process with the cloud involved...
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
            m_twainlocalonsane = new TwainLocalOnSane(m_szWriteFolder, m_szIpc, Process.GetCurrentProcess().Id);
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
                TwainDirectSupport.Log.Error("Lost our process...");
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
                    TwainDirectSupport.Log.Error("createSession failed: " + jsonlookup.Get("status"));
                    return (false);
                }
            }
            catch
            {
                TwainDirectSupport.Log.Error("createSession failed: JSON error");
                return (false);
            }

            #endregion


            // Set TWAIN Direct Options...
            #region Set TWAIN Direct Options...

            string szTwainDirectOptions = File.ReadAllText(m_szTask);

            blSts = ipc.Write
            (
                "{" +
                "\"method\":\"setTwainDirectOptions\"," +
                "\"task\":" + szTwainDirectOptions +
                "}"
            );
            if (!blSts)
            {
                TwainDirectSupport.Log.Error("Lost our process...");
                goto ABORT;
            }

            // Get the result...
            szJson = ipc.Read();
            if (szJson == null)
            {
                TwainDirectSupport.Log.Error("Lost our process...");
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
                TwainDirectSupport.Log.Error("Lost our process...");
                goto ABORT;
            }

            // Get the result...
            szJson = ipc.Read();
            if (szJson == null)
            {
                TwainDirectSupport.Log.Error("Lost our process...");
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
                        TwainDirectSupport.Log.Error("Lost our process...");
                        goto ABORT;
                    }

                    // Get the result...
                    szJson = ipc.Read();
                    if (szJson == null)
                    {
                        TwainDirectSupport.Log.Error("Lost our process...");
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
                    TwainDirectSupport.Log.Error("Lost our process...");
                    goto ABORT;
                }

                // Get the result...
                szJson = ipc.Read();
                if (szJson == null)
                {
                    TwainDirectSupport.Log.Error("Lost our process...");
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
                    TwainDirectSupport.Log.Error("Lost our process...");
                    goto ABORT;
                }

                // Get the result...
                szJson = ipc.Read();
                if (szJson == null)
                {
                    TwainDirectSupport.Log.Error("Lost our process...");
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
                    TwainDirectSupport.Log.Error("Lost our process...");
                    goto ABORT;
                }

                // Get the result...
                szJson = ipc.Read();
                if (szJson == null)
                {
                    TwainDirectSupport.Log.Error("Lost our process...");
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
                TwainDirectSupport.Log.Error("Lost our process...");
                goto ABORT;
            }

            // Get the result...
            szJson = ipc.Read();
            if (szJson == null)
            {
                TwainDirectSupport.Log.Error("Lost our process...");
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
                TwainDirectSupport.Log.Error("Lost our process...");
                goto ABORT;
            }

            // Get the result...
            szJson = ipc.Read();
            if (szJson == null)
            {
                TwainDirectSupport.Log.Error("Lost our process...");
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
                TwainDirectSupport.Log.Error("Lost our process...");
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
            m_twainlocalonsane.Run();
        }

        TwainLocalOnSane m_twainlocalonsane;
        private string m_szScanner;
        private string m_szWriteFolder;
        private int m_iPid;
        private string m_szIpc;
        private string m_szTask;
    }
}
