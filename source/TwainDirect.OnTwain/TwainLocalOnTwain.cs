///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirect.OnTwain.TwainLocalToTwain
//
//  Map TWAIN Local calls to TWAIN calls...
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    17-Dec-2014     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2014-2020 Kodak Alaris Inc.
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
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using TwainDirect.Support;
using TWAINWorkingGroup;

namespace TwainDirect.OnTwain
{
    /// <summary>
    /// Map TWAIN Local calls to TWAIN.  This seems like the best way to make
    /// sure we get all the needed data down to this level, however it means
    /// that we have knowledge of our caller at this level, so there will be
    /// some replication if we add support for another communication manager...
    /// </summary>
    internal sealed class TwainLocalOnTwain : IDisposable
    {
        // Public Methods: Run
        #region Public Methods: Run

        /// <summary>
        /// Init stuff...
        /// </summary>
        public TwainLocalOnTwain
        (
            string a_szWriteFolder,
            string a_szImagesFolder,
            string a_szIpc,
            int a_iPid,
            TWAIN.RunInUiThreadDelegate a_runinuithreaddelegate,
            FormTwain a_formtwain,
            IntPtr a_intptrHwnd
        )
        {
            // Remember this stuff...
            m_szWriteFolder = a_szWriteFolder;
            if (!string.IsNullOrEmpty(a_szImagesFolder))
            {
                m_szImagesFolder = a_szImagesFolder;
            }
            else
            {
                m_szImagesFolder = Path.Combine(m_szWriteFolder, "images");
            }
            m_szIpc = a_szIpc;
            m_iPid = a_iPid;
            m_runinuithreaddelegate = a_runinuithreaddelegate;
            m_formtwain = a_formtwain;
            m_intptrHwnd = a_intptrHwnd;

            // Init stuff...
            m_blFlatbed = false;
            m_blDuplex = false;
            m_blDisableDsSent = false;
            m_blXferReadySent = false;

            // Log stuff...
            TWAINWorkingGroup.Log.Info("TWAIN images folder: " + m_szImagesFolder);
        }

        /// <summary>
        /// Finalizer...
        /// </summary>
        ~TwainLocalOnTwain()
        {
            Dispose(false);
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Run the driver...
        /// </summary>
        /// <returns>true on success</returns>
        public bool Run()
        {
            bool blSuccess;
            bool blRunning = true;
            bool blSetAppCapabilities = false;
            long lResponseCharacterOffset;
            string szJson;
            string szSession;
            Ipc ipc;
            ProcessSwordTask processswordtask;
            TwainLocalScanner.ApiStatus apistatus;

            // Pipe mode starting...
            TWAINWorkingGroup.Log.Info("IPC mode starting...");

            // Set up communication with our server process...
            ipc = new Ipc(m_szIpc, false, IpcDisconnectLaunchpad, this);
            ipc.MonitorPid(m_iPid);
            ipc.Connect();
            m_blIpcDisconnectCallbackArmed = true;

            // Loopy...
            while (blRunning)
            {
                // Read a command...
                szJson = ipc.Read();
                if (szJson == null)
                {
                    TWAINWorkingGroup.Log.Info("IPC channel disconnected...");
                    m_blIpcDisconnectCallbackArmed = false;
                    blRunning = false;
                    break;
                }

                // Parse the command...
                JsonLookup jsonlookup = new JsonLookup();
                if (!jsonlookup.Load(szJson, out lResponseCharacterOffset))
                {
                    continue;
                }

                // Dispatch the command...
                switch (jsonlookup.Get("method"))
                {
                    // Stuff we don't recognize.  Some commands never make it this
                    // far...
                    default:
                    case "readImageBlock":
                    case "readImageBlockMetadata":
                    case "releaseImageBlocks":
                        break;

                    case "closeSession":
                        apistatus = DeviceScannerCloseSession(out szSession);
                        if (apistatus == TwainLocalScanner.ApiStatus.success)
                        {
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\"," +
                                szSession +
                                "}"
                            );
                        }
                        else
                        {
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\"" +
                                "}"
                            );
                        }
                        if (!blSuccess)
                        {
                            TWAINWorkingGroup.Log.Info("IPC channel disconnected...");
                            blRunning = false;
                        }
                        break;

                    case "createSession":
                        apistatus = DeviceScannerCreateSession(jsonlookup, out szSession);
                        if (apistatus == TwainLocalScanner.ApiStatus.success)
                        {
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\"," +
                                szSession +
                                "}"
                            );
                        }
                        else
                        {
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\"" +
                                "}"
                            );
                        }
                        if (!blSuccess)
                        {
                            TWAINWorkingGroup.Log.Info("IPC channel disconnected...");
                            blRunning = false;
                        }
                        break;

                    case "exit":
                        m_blIpcDisconnectCallbackArmed = false;
                        blRunning = false;
                        break;

                    case "getSession":
                        apistatus = DeviceScannerGetSession(out szSession);
                        if (apistatus == TwainLocalScanner.ApiStatus.success)
                        {
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\"," +
                                szSession +
                                "}"
                            );
                        }
                        else
                        {
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\"" +
                                "}"
                            );
                        }
                        if (!blSuccess)
                        {
                            TWAINWorkingGroup.Log.Info("IPC channel disconnected...");
                            blRunning = false;
                        }
                        break;

                    case "sendTask":
                        apistatus = DeviceScannerSendTask(jsonlookup, out processswordtask, ref blSetAppCapabilities);
                        blSuccess = ipc.Write
                        (
                            "{" +
                            "\"status\":\"" + apistatus + "\"," +
                            "\"taskReply\":" + processswordtask.GetTaskReply() +
                            "}"
                        );
                        break;

                    case "startCapturing":
                        apistatus = DeviceScannerStartCapturing(ref blSetAppCapabilities, out szSession);
                        if (apistatus == TwainLocalScanner.ApiStatus.success)
                        {
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\"," +
                                szSession +
                                "}"
                            );
                        }
                        else
                        {
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\"" +
                                "}"
                            );
                        }
                        if (!blSuccess)
                        {
                            TWAINWorkingGroup.Log.Info("IPC channel disconnected...");
                            blRunning = false;
                        }
                        break;

                    case "stopCapturing":
                        apistatus = DeviceScannerStopCapturing(out szSession);
                        if (apistatus == TwainLocalScanner.ApiStatus.success)
                        {
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\"," +
                                szSession +
                                "}"
                            );
                        }
                        else
                        {
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\"" +
                                "}"
                            );
                        }
                        if (!blSuccess)
                        {
                            TWAINWorkingGroup.Log.Info("IPC channel disconnected...");
                            blRunning = false;
                        }
                        break;
                }
            }

            // Last chance cleanup...
            if (m_twain != null)
            {
                Rollback(TWAIN.STATE.S1);
            }

            // All done...
            TWAINWorkingGroup.Log.Info("IPC mode completed...");
            return (true);
        }

        /// <summary>
        /// Get our TWAIN object, whatever it is...
        /// </summary>
        /// <returns></returns>
        public TWAIN Twain()
        {
            return (m_twain);
        }

        /// <summary>
        /// Our scan callback event, used to drive the engine when scanning...
        /// </summary>
        public delegate void ScanCallbackEvent();

        /// <summary>
        /// Our event handler for the scan callback event.  This will be
        /// called once by ScanCallbackTrigger on receipt of an event
        /// like MSG_XFERREADY, and then will be reissued on every call
        /// into ScanCallback until we're done and get back to state 4.
        ///  
        /// This helps to make sure we're always running in the context
        /// of FormMain on Windows, which is critical if we want drivers
        /// to work properly.  It also gives a way to break up the calls
        /// so the message pump is still reponsive.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScanCallbackEventHandler(object sender, EventArgs e)
        {
            ScanCallback((m_twain == null) ? true : (m_twain.GetState() <= TWAIN.STATE.S3));
        }

        /// <summary>
        /// Rollback the TWAIN state to whatever is requested...
        /// </summary>
        /// <param name="a_state"></param>
        public void Rollback(TWAIN.STATE a_state)
        {
            string szTwmemref = "";
            string szStatus = "";
            TWAIN.STS sts;

            // Make sure we have something to work with...
            if (m_twain == null)
            {
                return;
            }

            // Walk the states, we don't care about the status returns.  Basically,
            // these need to work, or we're guaranteed to hang...

            // 7 --> 6
            if ((m_twain.GetState() == TWAIN.STATE.S7) && (a_state < TWAIN.STATE.S7))
            {
                szTwmemref = "0,0";
                szStatus = "";
                sts = Send("DG_CONTROL", "DAT_PENDINGXFERS", "MSG_ENDXFER", ref szTwmemref, ref szStatus);
            }

            // 6 --> 5
            if ((m_twain.GetState() == TWAIN.STATE.S6) && (a_state < TWAIN.STATE.S6))
            {
                szTwmemref = "0,0";
                szStatus = "";
                sts = Send("DG_CONTROL", "DAT_PENDINGXFERS", "MSG_RESET", ref szTwmemref, ref szStatus);
            }

            // 5 --> 4
            if ((m_twain.GetState() == TWAIN.STATE.S5) && (a_state < TWAIN.STATE.S5))
            {
                szTwmemref = "0,0," + m_intptrHwnd;
                szStatus = "";
                sts = Send("DG_CONTROL", "DAT_USERINTERFACE", "MSG_DISABLEDS", ref szTwmemref, ref szStatus);
                //ClearEvents();
            }

            // 4 --> 3
            if ((m_twain.GetState() == TWAIN.STATE.S4) && (a_state < TWAIN.STATE.S4))
            {
                //if (!m_checkboxUseCallbacks.Checked)
                //{
                //    Application.RemoveMessageFilter(this);
                //}
                szTwmemref = m_twain.GetDsIdentity();
                szStatus = "";
                sts = Send("DG_CONTROL", "DAT_IDENTITY", "MSG_CLOSEDS", ref szTwmemref, ref szStatus);
            }

            // 3 --> 2
            if ((m_twain.GetState() == TWAIN.STATE.S3) && (a_state < TWAIN.STATE.S3))
            {
                szTwmemref = m_intptrHwnd.ToString();
                szStatus = "";
                sts = Send("DG_CONTROL", "DAT_PARENT", "MSG_CLOSEDSM", ref szTwmemref, ref szStatus);
            }

            // 2 --> 1
            if ((m_twain.GetState() == TWAIN.STATE.S2) && (a_state < TWAIN.STATE.S2))
            {
                m_twain.Dispose();
                m_twain = null;
            }
        }

        /// <summary>
        /// Send a command to the currently loaded DSM...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        public TWAIN.STS Send(string a_szDg, string a_szDat, string a_szMsg, ref string a_szTwmemref, ref string a_szResult)
        {
            int iDg;
            int iDat;
            int iMsg;
            TWAIN.STS sts;
            TWAIN.DG dg = TWAIN.DG.MASK;
            TWAIN.DAT dat = TWAIN.DAT.NULL;
            TWAIN.MSG msg = TWAIN.MSG.NULL;

            // Init stuff...
            iDg = 0;
            iDat = 0;
            iMsg = 0;
            sts = TWAIN.STS.BADPROTOCOL;
            a_szResult = "";

            // Validate at the top level...
            if (m_twain == null)
            {
                TWAINWorkingGroup.Log.Error("***ERROR*** - dsmload wasn't run, so we is having no braims");
                return (TWAIN.STS.SEQERROR);
            }

            // Look for DG...
            if (!a_szDg.ToLowerInvariant().StartsWith("dg_"))
            {
                TWAINWorkingGroup.Log.Error("Unrecognized dg - <" + a_szDg + ">");
                return (TWAIN.STS.BADPROTOCOL);
            }
            else
            {
                // Look for hex number (take anything)...
                if (a_szDg.ToLowerInvariant().StartsWith("dg_0x"))
                {
                    if (!int.TryParse(a_szDg.ToLowerInvariant().Substring(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out iDg))
                    {
                        TWAINWorkingGroup.Log.Error("Badly constructed dg - <" + a_szDg + ">");
                        return (TWAIN.STS.BADPROTOCOL);
                    }
                }
                else
                {
                    if (!Enum.TryParse(a_szDg.ToUpperInvariant().Substring(3), out dg))
                    {
                        TWAINWorkingGroup.Log.Error("Unrecognized dg - <" + a_szDg + ">");
                        return (TWAIN.STS.BADPROTOCOL);
                    }
                    iDg = (int)dg;
                }
            }

            // Look for DAT...
            if (!a_szDat.ToLowerInvariant().StartsWith("dat_"))
            {
                TWAINWorkingGroup.Log.Error("Unrecognized dat - <" + a_szDat + ">");
                return (TWAIN.STS.BADPROTOCOL);
            }
            else
            {
                // Look for hex number (take anything)...
                if (a_szDat.ToLowerInvariant().StartsWith("dat_0x"))
                {
                    if (!int.TryParse(a_szDat.ToLowerInvariant().Substring(4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out iDat))
                    {
                        TWAINWorkingGroup.Log.Error("Badly constructed dat - <" + a_szDat + ">");
                        return (TWAIN.STS.BADPROTOCOL);
                    }
                }
                else
                {
                    if (!Enum.TryParse(a_szDat.ToUpperInvariant().Substring(4), out dat))
                    {
                        TWAINWorkingGroup.Log.Error("Unrecognized dat - <" + a_szDat + ">");
                        return (TWAIN.STS.BADPROTOCOL);
                    }
                    iDat = (int)dat;
                }
            }

            // Look for MSG...
            if (!a_szMsg.ToLowerInvariant().StartsWith("msg_"))
            {
                TWAINWorkingGroup.Log.Error("Unrecognized msg - <" + a_szMsg + ">");
                return (TWAIN.STS.BADPROTOCOL);
            }
            else
            {
                // Look for hex number (take anything)...
                if (a_szMsg.ToLowerInvariant().StartsWith("msg_0x"))
                {
                    if (!int.TryParse(a_szMsg.ToLowerInvariant().Substring(4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out iMsg))
                    {
                        TWAINWorkingGroup.Log.Error("Badly constructed dat - <" + a_szMsg + ">");
                        return (TWAIN.STS.BADPROTOCOL);
                    }
                }
                else
                {
                    if (!Enum.TryParse(a_szMsg.ToUpperInvariant().Substring(4), out msg))
                    {
                        TWAINWorkingGroup.Log.Error("Unrecognized msg - <" + a_szMsg + ">");
                        return (TWAIN.STS.BADPROTOCOL);
                    }
                    iMsg = (int)msg;
                }
            }

            // Send the command...
            switch (iDat)
            {
                // Ruh-roh, since we can't marshal it, we have to return an error,
                // it would be nice to have a solution for this, but that will need
                // a dynamic marshalling system...
                default:
                    sts = TWAIN.STS.BADPROTOCOL;
                    break;

                // DAT_AUDIOFILEXFER...
                case (int)TWAIN.DAT.AUDIOFILEXFER:
                    {
                        sts = m_twain.DatAudiofilexfer((TWAIN.DG)iDg, (TWAIN.MSG)iMsg);
                        a_szTwmemref = "";
                    }
                    break;

                // DAT_AUDIOINFO..
                case (int)TWAIN.DAT.AUDIOINFO:
                    {
                        TWAIN.TW_AUDIOINFO twaudioinfo = default(TWAIN.TW_AUDIOINFO);
                        sts = m_twain.DatAudioinfo((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twaudioinfo);
                        a_szTwmemref = m_twain.AudioinfoToCsv(twaudioinfo);
                    }
                    break;

                // DAT_AUDIONATIVEXFER..
                case (int)TWAIN.DAT.AUDIONATIVEXFER:
                    {
                        IntPtr intptr = IntPtr.Zero;
                        sts = m_twain.DatAudionativexfer((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref intptr);
                        a_szTwmemref = intptr.ToString();
                    }
                    break;

                // DAT_CALLBACK...
                case (int)TWAIN.DAT.CALLBACK:
                    {
                        TWAIN.TW_CALLBACK twcallback = default(TWAIN.TW_CALLBACK);
                        m_twain.CsvToCallback(ref twcallback, a_szTwmemref);
                        sts = m_twain.DatCallback((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twcallback);
                        a_szTwmemref = m_twain.CallbackToCsv(twcallback);
                    }
                    break;

                // DAT_CALLBACK2...
                case (int)TWAIN.DAT.CALLBACK2:
                    {
                        TWAIN.TW_CALLBACK2 twcallback2 = default(TWAIN.TW_CALLBACK2);
                        m_twain.CsvToCallback2(ref twcallback2, a_szTwmemref);
                        sts = m_twain.DatCallback2((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twcallback2);
                        a_szTwmemref = m_twain.Callback2ToCsv(twcallback2);
                    }
                    break;

                // DAT_CAPABILITY...
                case (int)TWAIN.DAT.CAPABILITY:
                    {
                        // Skip symbols for msg_querysupport, otherwise 0 gets turned into false, also
                        // if the command fails the return value is whatever was sent into us, which
                        // matches the experience one should get with C/C++...
                        string szStatus = "";
                        TWAIN.TW_CAPABILITY twcapability = default(TWAIN.TW_CAPABILITY);
                        m_twain.CsvToCapability(ref twcapability, ref szStatus, a_szTwmemref);
                        sts = m_twain.DatCapability((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twcapability);
                        if ((sts == TWAIN.STS.SUCCESS) || (sts == TWAIN.STS.CHECKSTATUS))
                        {
                            // Convert the data to CSV...
                            a_szTwmemref = m_twain.CapabilityToCsv(twcapability, ((TWAIN.MSG)iMsg != TWAIN.MSG.QUERYSUPPORT));
                            // Free the handle if the driver created it...
                            switch ((TWAIN.MSG)iMsg)
                            {
                                default: break;
                                case TWAIN.MSG.GET:
                                case TWAIN.MSG.GETCURRENT:
                                case TWAIN.MSG.GETDEFAULT:
                                case TWAIN.MSG.QUERYSUPPORT:
                                case TWAIN.MSG.RESET:
                                    m_twain.DsmMemFree(ref twcapability.hContainer);
                                    break;
                            }
                        }
                    }
                    break;

                // DAT_CIECOLOR..
                case (int)TWAIN.DAT.CIECOLOR:
                    {
                        //TWAIN.TW_CIECOLOR twciecolor = default(TWAIN.TW_CIECOLOR);
                        //sts = m_twain.DatCiecolor((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twciecolor);
                        //a_szTwmemref = m_twain.CiecolorToCsv(twciecolor);
                    }
                    break;

                // DAT_CUSTOMDSDATA...
                case (int)TWAIN.DAT.CUSTOMDSDATA:
                    {
                        TWAIN.TW_CUSTOMDSDATA twcustomdsdata = default(TWAIN.TW_CUSTOMDSDATA);
                        m_twain.CsvToCustomdsdata(ref twcustomdsdata, a_szTwmemref);
                        sts = m_twain.DatCustomdsdata((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twcustomdsdata);
                        a_szTwmemref = m_twain.CustomdsdataToCsv(twcustomdsdata);
                    }
                    break;

                // DAT_DEVICEEVENT...
                case (int)TWAIN.DAT.DEVICEEVENT:
                    {
                        TWAIN.TW_DEVICEEVENT twdeviceevent = default(TWAIN.TW_DEVICEEVENT);
                        sts = m_twain.DatDeviceevent((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twdeviceevent);
                        a_szTwmemref = m_twain.DeviceeventToCsv(twdeviceevent);
                    }
                    break;

                // DAT_ENTRYPOINT...
                case (int)TWAIN.DAT.ENTRYPOINT:
                    {
                        TWAIN.TW_ENTRYPOINT twentrypoint = default(TWAIN.TW_ENTRYPOINT);
                        twentrypoint.Size = (uint)Marshal.SizeOf(twentrypoint);
                        sts = m_twain.DatEntrypoint((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twentrypoint);
                        a_szTwmemref = m_twain.EntrypointToCsv(twentrypoint);
                    }
                    break;

                // DAT_EVENT...
                case (int)TWAIN.DAT.EVENT:
                    {
                        TWAIN.TW_EVENT twevent = default(TWAIN.TW_EVENT);
                        sts = m_twain.DatEvent((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twevent);
                        a_szTwmemref = m_twain.EventToCsv(twevent);
                    }
                    break;

                // DAT_EXTIMAGEINFO...
                case (int)TWAIN.DAT.EXTIMAGEINFO:
                    {
                        TWAIN.TW_EXTIMAGEINFO twextimageinfo = default(TWAIN.TW_EXTIMAGEINFO);
                        m_twain.CsvToExtimageinfo(ref twextimageinfo, a_szTwmemref);
                        sts = m_twain.DatExtimageinfo((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twextimageinfo);
                        a_szTwmemref = m_twain.ExtimageinfoToCsv(twextimageinfo);
                    }
                    break;

                // DAT_FILESYSTEM...
                case (int)TWAIN.DAT.FILESYSTEM:
                    {
                        TWAIN.TW_FILESYSTEM twfilesystem = default(TWAIN.TW_FILESYSTEM);
                        m_twain.CsvToFilesystem(ref twfilesystem, a_szTwmemref);
                        sts = m_twain.DatFilesystem((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twfilesystem);
                        a_szTwmemref = m_twain.FilesystemToCsv(twfilesystem);
                    }
                    break;

                // DAT_FILTER...
                case (int)TWAIN.DAT.FILTER:
                    {
                        //TWAIN.TW_FILTER twfilter = default(TWAIN.TW_FILTER);
                        //m_twain.CsvToFilter(ref twfilter, a_szTwmemref);
                        //sts = m_twain.DatFilter((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twfilter);
                        //a_szTwmemref = m_twain.FilterToCsv(twfilter);
                    }
                    break;

                // DAT_GRAYRESPONSE...
                case (int)TWAIN.DAT.GRAYRESPONSE:
                    {
                        //TWAIN.TW_GRAYRESPONSE twgrayresponse = default(TWAIN.TW_GRAYRESPONSE);
                        //m_twain.CsvToGrayresponse(ref twgrayresponse, a_szTwmemref);
                        //sts = m_twain.DatGrayresponse((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twgrayresponse);
                        //a_szTwmemref = m_twain.GrayresponseToCsv(twgrayresponse);
                    }
                    break;

                // DAT_ICCPROFILE...
                case (int)TWAIN.DAT.ICCPROFILE:
                    {
                        TWAIN.TW_MEMORY twmemory = default(TWAIN.TW_MEMORY);
                        sts = m_twain.DatIccprofile((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twmemory);
                        a_szTwmemref = m_twain.IccprofileToCsv(twmemory);
                    }
                    break;

                // DAT_IDENTITY...
                case (int)TWAIN.DAT.IDENTITY:
                    {
                        TWAIN.TW_IDENTITY twidentity = default(TWAIN.TW_IDENTITY);
                        switch (iMsg)
                        {
                            default:
                                break;
                            case (int)TWAIN.MSG.SET:
                            case (int)TWAIN.MSG.OPENDS:
                                m_twain.CsvToIdentity(ref twidentity, a_szTwmemref);
                                break;
                        }
                        sts = m_twain.DatIdentity((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twidentity);
                        a_szTwmemref = m_twain.IdentityToCsv(twidentity);
                    }
                    break;

                // DAT_IMAGEFILEXFER...
                case (int)TWAIN.DAT.IMAGEFILEXFER:
                    {
                        sts = m_twain.DatImagefilexfer((TWAIN.DG)iDg, (TWAIN.MSG)iMsg);
                        a_szTwmemref = "";
                    }
                    break;

                // DAT_IMAGEINFO...
                case (int)TWAIN.DAT.IMAGEINFO:
                    {
                        TWAIN.TW_IMAGEINFO twimageinfo = default(TWAIN.TW_IMAGEINFO);
                        m_twain.CsvToImageinfo(ref twimageinfo, a_szTwmemref);
                        sts = m_twain.DatImageinfo((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twimageinfo);
                        a_szTwmemref = m_twain.ImageinfoToCsv(twimageinfo);
                    }
                    break;

                // DAT_IMAGELAYOUT...
                case (int)TWAIN.DAT.IMAGELAYOUT:
                    {
                        TWAIN.TW_IMAGELAYOUT twimagelayout = default(TWAIN.TW_IMAGELAYOUT);
                        m_twain.CsvToImagelayout(ref twimagelayout, a_szTwmemref);
                        sts = m_twain.DatImagelayout((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twimagelayout);
                        a_szTwmemref = m_twain.ImagelayoutToCsv(twimagelayout);
                    }
                    break;

                // DAT_IMAGEMEMFILEXFER...
                case (int)TWAIN.DAT.IMAGEMEMFILEXFER:
                    {
                        TWAIN.TW_IMAGEMEMXFER twimagememxfer = default(TWAIN.TW_IMAGEMEMXFER);
                        m_twain.CsvToImagememxfer(ref twimagememxfer, a_szTwmemref);
                        sts = m_twain.DatImagememfilexfer((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twimagememxfer);
                        a_szTwmemref = m_twain.ImagememxferToCsv(twimagememxfer);
                    }
                    break;

                // DAT_IMAGEMEMXFER...
                case (int)TWAIN.DAT.IMAGEMEMXFER:
                    {
                        TWAIN.TW_IMAGEMEMXFER twimagememxfer = default(TWAIN.TW_IMAGEMEMXFER);
                        m_twain.CsvToImagememxfer(ref twimagememxfer, a_szTwmemref);
                        sts = m_twain.DatImagememxfer((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twimagememxfer);
                        a_szTwmemref = m_twain.ImagememxferToCsv(twimagememxfer);
                    }
                    break;

                // DAT_IMAGENATIVEXFER...
                case (int)TWAIN.DAT.IMAGENATIVEXFER:
                    {
                        IntPtr intptrBitmapHandle = IntPtr.Zero;
                        sts = m_twain.DatImagenativexferHandle((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref intptrBitmapHandle);
                        a_szTwmemref = intptrBitmapHandle.ToString();
                    }
                    break;

                // DAT_JPEGCOMPRESSION...
                case (int)TWAIN.DAT.JPEGCOMPRESSION:
                    {
                        //TWAIN.TW_JPEGCOMPRESSION twjpegcompression = default(TWAIN.TW_JPEGCOMPRESSION);
                        //m_twain.CsvToJpegcompression(ref twjpegcompression, a_szTwmemref);
                        //sts = m_twain.DatJpegcompression((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twjpegcompression);
                        //a_szTwmemref = m_twain.JpegcompressionToCsv(twjpegcompression);
                    }
                    break;

                // DAT_METRICS...
                case (int)TWAIN.DAT.METRICS:
                    {
                        TWAIN.TW_METRICS twmetrics = default(TWAIN.TW_METRICS);
                        twmetrics.SizeOf = (uint)Marshal.SizeOf(twmetrics);
                        sts = m_twain.DatMetrics((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twmetrics);
                        a_szTwmemref = m_twain.MetricsToCsv(twmetrics);
                    }
                    break;

                // DAT_PALETTE8...
                case (int)TWAIN.DAT.PALETTE8:
                    {
                        //TWAIN.TW_PALETTE8 twpalette8 = default(TWAIN.TW_PALETTE8);
                        //m_twain.CsvToPalette8(ref twpalette8, a_szTwmemref);
                        //sts = m_twain.DatPalette8((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twpalette8);
                        //a_szTwmemref = m_twain.Palette8ToCsv(twpalette8);
                    }
                    break;

                // DAT_PARENT...
                case (int)TWAIN.DAT.PARENT:
                    {
                        sts = m_twain.DatParent((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref m_intptrHwnd);
                        a_szTwmemref = "";
                    }
                    break;

                // DAT_PASSTHRU...
                case (int)TWAIN.DAT.PASSTHRU:
                    {
                        TWAIN.TW_PASSTHRU twpassthru = default(TWAIN.TW_PASSTHRU);
                        m_twain.CsvToPassthru(ref twpassthru, a_szTwmemref);
                        sts = m_twain.DatPassthru((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twpassthru);
                        a_szTwmemref = m_twain.PassthruToCsv(twpassthru);
                    }
                    break;

                // DAT_PENDINGXFERS...
                case (int)TWAIN.DAT.PENDINGXFERS:
                    {
                        TWAIN.TW_PENDINGXFERS twpendingxfers = default(TWAIN.TW_PENDINGXFERS);
                        sts = m_twain.DatPendingxfers((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twpendingxfers);
                        a_szTwmemref = m_twain.PendingxfersToCsv(twpendingxfers);
                    }
                    break;

                // DAT_RGBRESPONSE...
                case (int)TWAIN.DAT.RGBRESPONSE:
                    {
                        //TWAIN.TW_RGBRESPONSE twrgbresponse = default(TWAIN.TW_RGBRESPONSE);
                        //m_twain.CsvToRgbresponse(ref twrgbresponse, a_szTwmemref);
                        //sts = m_twain.DatRgbresponse((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twrgbresponse);
                        //a_szTwmemref = m_twain.RgbresponseToCsv(twrgbresponse);
                    }
                    break;

                // DAT_SETUPFILEXFER...
                case (int)TWAIN.DAT.SETUPFILEXFER:
                    {
                        TWAIN.TW_SETUPFILEXFER twsetupfilexfer = default(TWAIN.TW_SETUPFILEXFER);
                        m_twain.CsvToSetupfilexfer(ref twsetupfilexfer, a_szTwmemref);
                        sts = m_twain.DatSetupfilexfer((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twsetupfilexfer);
                        a_szTwmemref = m_twain.SetupfilexferToCsv(twsetupfilexfer);
                    }
                    break;

                // DAT_SETUPMEMXFER...
                case (int)TWAIN.DAT.SETUPMEMXFER:
                    {
                        TWAIN.TW_SETUPMEMXFER twsetupmemxfer = default(TWAIN.TW_SETUPMEMXFER);
                        sts = m_twain.DatSetupmemxfer((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twsetupmemxfer);
                        a_szTwmemref = m_twain.SetupmemxferToCsv(twsetupmemxfer);
                    }
                    break;

                // DAT_STATUS...
                case (int)TWAIN.DAT.STATUS:
                    {
                        TWAIN.TW_STATUS twstatus = default(TWAIN.TW_STATUS);
                        sts = m_twain.DatStatus((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twstatus);
                        a_szTwmemref = m_twain.StatusToCsv(twstatus);
                    }
                    break;

                // DAT_STATUSUTF8...
                case (int)TWAIN.DAT.STATUSUTF8:
                    {
                        TWAIN.TW_STATUSUTF8 twstatusutf8 = default(TWAIN.TW_STATUSUTF8);
                        sts = m_twain.DatStatusutf8((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twstatusutf8);
                        a_szTwmemref = m_twain.Statusutf8ToCsv(twstatusutf8);
                    }
                    break;

                // DAT_TWAINDIRECT...
                case (int)TWAIN.DAT.TWAINDIRECT:
                    {
                        TWAIN.TW_TWAINDIRECT twtwaindirect = default(TWAIN.TW_TWAINDIRECT);
                        m_twain.CsvToTwaindirect(ref twtwaindirect, a_szTwmemref);
                        sts = m_twain.DatTwaindirect((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twtwaindirect);
                        a_szTwmemref = m_twain.TwaindirectToCsv(twtwaindirect);
                    }
                    break;

                // DAT_USERINTERFACE...
                case (int)TWAIN.DAT.USERINTERFACE:
                    {
                        TWAIN.TW_USERINTERFACE twuserinterface = default(TWAIN.TW_USERINTERFACE);
                        m_twain.CsvToUserinterface(ref twuserinterface, a_szTwmemref);
                        sts = m_twain.DatUserinterface((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref twuserinterface);
                        a_szTwmemref = m_twain.UserinterfaceToCsv(twuserinterface);
                    }
                    break;

                // DAT_XFERGROUP...
                case (int)TWAIN.DAT.XFERGROUP:
                    {
                        uint uXferGroup = 0;
                        sts = m_twain.DatXferGroup((TWAIN.DG)iDg, (TWAIN.MSG)iMsg, ref uXferGroup);
                        a_szTwmemref = string.Format("0x{0:X}", uXferGroup);
                    }
                    break;
            }

            // All done...
            return (sts);
        }

        /// <summary>
        /// Our scanning callback function.  We appeal directly to the supporting
        /// TWAIN object.  This way we don't have to maintain some kind of a loop
        /// inside of the application, which is the source of most problems that
        /// developers run into.
        /// 
        /// While it looks scary at first, there's really not a lot going on in
        /// here.  We do some sanity checks, we watch for certain kinds of events,
        /// we support the four methods of transferring images, and we dump out
        /// some meta-data about the transferred image.  However, because it does
        /// look scary I dropped in some region pragmas to break things up...
        /// </summary>
        /// <param name="a_blClosing">We're shutting down</param>
        /// <returns>TWAIN status</returns>
        private TWAIN.STS ScanCallbackTrigger(bool a_blClosing)
        {
            m_formtwain.BeginInvoke(new MethodInvoker(delegate { ScanCallbackEventHandler(this, new EventArgs()); }));
            return (TWAIN.STS.SUCCESS);
        }
        private TWAIN.STS ScanCallback(bool a_blClosing)
        {
            string szTwmemref = "";
            string szStatus = "";
            TWAIN.STS sts;

            // Scoot...
            if (m_twain == null)
            {
                return (TWAIN.STS.FAILURE);
            }

            // We're superfluous...
            if (m_twain.GetState() <= TWAIN.STATE.S4)
            {
                return (TWAIN.STS.SUCCESS);
            }

            // We're leaving...
            if (a_blClosing)
            {
                return (TWAIN.STS.SUCCESS);
            }

            // Do this in the right thread, we'll usually be in the
            // right spot, save maybe on the first call...
            if (m_formtwain.InvokeRequired)
            {
                return
                (
                    (TWAIN.STS)m_formtwain.Invoke
                    (
                        (Func<TWAIN.STS>)delegate
                        {
                            return (ScanCallback(a_blClosing));
                        }
                    )
                );
            }

            // Handle DAT_NULL/MSG_XFERREADY...
            if (m_twain.IsMsgXferReady() && !m_blXferReadySent)
            {
                m_blXferReadySent = true;

                // What transfer mechanism are we using?
                szTwmemref = "ICAP_XFERMECH,0,0,0";
                szStatus = "";
                sts = Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szTwmemref, ref szStatus);
                if (szTwmemref.EndsWith("TWSX_NATIVE")) m_twsxXferMech = TWAIN.TWSX.NATIVE;
                else if (szTwmemref.EndsWith("TWSX_MEMORY")) m_twsxXferMech = TWAIN.TWSX.MEMORY;
                else if (szTwmemref.EndsWith("TWSX_FILE")) m_twsxXferMech = TWAIN.TWSX.FILE;
                else if (szTwmemref.EndsWith("TWSX_MEMFILE")) m_twsxXferMech = TWAIN.TWSX.MEMFILE;

                // Memory and memfile transfers need this...
                if ((m_twsxXferMech == TWAIN.TWSX.MEMORY) || (m_twsxXferMech == TWAIN.TWSX.MEMFILE))
                {
                    // Get the amount of memory needed...
                    szTwmemref = "0,0,0";
                    szStatus = "";
                    sts = Send("DG_CONTROL", "DAT_SETUPMEMXFER", "MSG_GET", ref szTwmemref, ref szStatus);
                    m_twain.CsvToSetupmemxfer(ref m_twsetupmemxfer, szTwmemref);
                    szStatus = (szStatus == "") ? sts.ToString() : (sts.ToString() + " - " + szStatus);
                    if ((sts != TWAIN.STS.SUCCESS) || (m_twsetupmemxfer.Preferred == 0))
                    {
                        m_blXferReadySent = false;
                        if (!m_blDisableDsSent)
                        {
                            m_blDisableDsSent = true;
                            Rollback(TWAIN.STATE.S4);
                        }
                    }

                    // Allocate the transfer memory (with a little extra to protect ourselves)...
                    m_intptrXfer = Marshal.AllocHGlobal((int)m_twsetupmemxfer.Preferred + 65536);
                    if (m_intptrXfer == IntPtr.Zero)
                    {
                        m_blDisableDsSent = true;
                        Rollback(TWAIN.STATE.S4);
                    }
                }

                // Memfile transfers need this...
                if ((m_twsxXferMech == TWAIN.TWSX.MEMORY) || (m_twsxXferMech == TWAIN.TWSX.MEMFILE))
                {
                    // Pick an image file format...
                    szTwmemref = "C:/image.pdf,TWFF_PDFRASTER,0";
                    szStatus = "";
                    sts = Send("DG_CONTROL", "DAT_SETUPFILEXFER", "MSG_SET", ref szTwmemref, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        m_blXferReadySent = false;
                        if (!m_blDisableDsSent)
                        {
                            m_blDisableDsSent = true;
                            Rollback(TWAIN.STATE.S4);
                        }
                    }
                }
            }

            // Handle DAT_NULL/MSG_CLOSEDSREQ...
            if (m_twain.IsMsgCloseDsReq() && !m_blDisableDsSent)
            {
                m_blDisableDsSent = true;
                Rollback(TWAIN.STATE.S4);
            }

            // Handle DAT_NULL/MSG_CLOSEDSOK...
            if (m_twain.IsMsgCloseDsOk() && !m_blDisableDsSent)
            {
                m_blDisableDsSent = true;
                Rollback(TWAIN.STATE.S4);
            }

            // This is where the statemachine runs that transfers and optionally
            // saves the images to disk (it also displays them).  It'll go back
            // and forth between states 6 and 7 until an error occurs, or until
            // we run out of images.
            //
            // Memory transfers are mandatory with TWAIN, so we'll support that
            // for drivers that don't natively support TWAIN Direct, so we can
            // turn the data into PDF/raster.
            //
            // Memfile transfers are mandatory for drivers that support TWAIN
            // direct, allowing us to get the image in its final form.
            //
            // Therefore there is no need to support native or file transfers.
            if (m_blXferReadySent && !m_blDisableDsSent)
            {
                // Transfer data...
                switch (m_twsxXferMech)
                {
                    default:
                    case TWAIN.TWSX.MEMORY:
                        sts = CaptureMemImages();
                        break;

                    case TWAIN.TWSX.MEMFILE:
                        sts = CaptureMemfileImages();
                        break;
                }

                // Catch the end of the batch, or any errors...
                if ((sts != TWAIN.STS.SUCCESS) || m_blResetSent)
                {
                    m_blDisableDsSent = false;
                    m_blXferReadySent = false;
                    SetImageBlocksDrained(sts);
                    return (sts);
                }
            }

            // Trigger the next event, this is where things all chain together.
            // We need begininvoke to prevent blockking, so that we don't get
            // backed up into a messy kind of recursion.  We need DoEvents,
            // because if things really start moving fast it's really hard for
            // application events, like button clicks to break through...
            Application.DoEvents();
            m_formtwain.BeginInvoke(new MethodInvoker(delegate { ScanCallbackEventHandler(this, new EventArgs()); }));

            // All done...
            return (TWAIN.STS.SUCCESS);
        }

        /// <summary>
        /// Go through the sequence needed to capture images using DAT_IMAGEMEMXFER...
        /// </summary>
        private TWAIN.STS CaptureMemImages()
        {
            string szTwmemref = "";
            string szStatus = "";
            TWAIN.STS sts;
            TWAIN.TW_IMAGEINFO twimageinfo = default(TWAIN.TW_IMAGEINFO);
            TWAIN.TW_IMAGEMEMXFER twimagememxfer = default(TWAIN.TW_IMAGEMEMXFER);
            TWAIN.TW_PENDINGXFERS twpendingxfers = default(TWAIN.TW_PENDINGXFERS);

            // We're exiting, get out of here...
            if (m_blReset && !m_blResetSent)
            {
                m_blResetSent = true;
                m_blDisableDsSent = true;
                Rollback(TWAIN.STATE.S4);
                return (TWAIN.STS.SUCCESS);
            }

            // Dispatch on the state...
            switch (m_twain.GetState())
            {
                // Not a good state, just scoot...
                default:
                    m_blDisableDsSent = true;
                    Rollback(TWAIN.STATE.S4);
                    return (TWAIN.STS.SEQERROR);

                // We're on our way out...
                case TWAIN.STATE.S5:
                    m_blResetSent = true;
                    m_blDisableDsSent = true;
                    Rollback(TWAIN.STATE.S4);
                    return (TWAIN.STS.SUCCESS);

                // Memory transfer...
                case TWAIN.STATE.S6:
                    // Gracefully end scanning, if that fails, go hard...
                    if (m_blStopFeeder && !m_blStopFeederSent)
                    {
                        m_blStopFeederSent = true;
                        szTwmemref = "0,0";
                        szStatus = "";
                        sts = Send("DG_CONTROL", "DAT_PENDINGXFERS", "MSG_STOPFEEDER", ref szTwmemref, ref szStatus);
                        if (sts != TWAIN.STS.SUCCESS)
                        {
                            m_blResetSent = true;
                            m_blDisableDsSent = true;
                            Rollback(TWAIN.STATE.S4);
                            return (sts);
                        }
                    }
                    // Transfer data...
                    szTwmemref = "0,0,0,0,0,0,0," + ((int)TWAIN.TWMF.APPOWNS | (int)TWAIN.TWMF.POINTER) + "," + m_twsetupmemxfer.Preferred + "," + m_intptrXfer;
                    szStatus = "";
                    sts = Send("DG_IMAGE", "DAT_IMAGEMEMXFER", "MSG_GET", ref szTwmemref, ref szStatus);
                    m_twain.CsvToImagememxfer(ref twimagememxfer, szTwmemref);
                    if ((sts != TWAIN.STS.SUCCESS) && (sts != TWAIN.STS.XFERDONE))
                    {
                        m_blDisableDsSent = true;
                        Rollback(TWAIN.STATE.S4);
                        return (sts);
                    }
                    break;

                // Memory transfer...
                case TWAIN.STATE.S7:
                    szTwmemref = "0,0,0,0,0,0,0," + ((int)TWAIN.TWMF.APPOWNS | (int)TWAIN.TWMF.POINTER) + "," + m_twsetupmemxfer.Preferred + "," + m_intptrXfer;
                    szStatus = "";
                    sts = Send("DG_IMAGE", "DAT_IMAGEMEMXFER", "MSG_GET", ref szTwmemref, ref szStatus);
                    m_twain.CsvToImagememxfer(ref twimagememxfer, szTwmemref);
                    if ((sts != TWAIN.STS.SUCCESS) && (sts != TWAIN.STS.XFERDONE))
                    {
                        m_blDisableDsSent = true;
                        Rollback(TWAIN.STATE.S4);
                        return (sts);
                    }
                    break;
            }

            // Allocate or grow the image memory...
            if (m_intptrImage == IntPtr.Zero)
            {
                m_intptrImage = Marshal.AllocHGlobal((int)twimagememxfer.BytesWritten);
            }
            else
            {
                m_intptrImage = Marshal.ReAllocHGlobal(m_intptrImage, (IntPtr)(m_iImageBytes + twimagememxfer.BytesWritten));
            }

            // Ruh-roh...
            if (m_intptrImage == IntPtr.Zero)
            {
                m_blDisableDsSent = true;
                Rollback(TWAIN.STATE.S4);
                return (TWAIN.STS.LOWMEMORY);
            }

            // Copy into the buffer, and bump up our byte tally...
            TWAIN.MemCpy(m_intptrImage + m_iImageBytes, m_intptrXfer, (int)twimagememxfer.BytesWritten);
            m_iImageBytes += (int)twimagememxfer.BytesWritten;

            // If we saw XFERDONE we can save the image, display it,
            // end the transfer, and see if we have more images...
            if (sts == TWAIN.STS.XFERDONE)
            {
                // Get the image info...
                szTwmemref = "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0";
                szStatus = "";
                sts = Send("DG_IMAGE", "DAT_IMAGEINFO", "MSG_GET", ref szTwmemref, ref szStatus);
                m_twain.CsvToImageinfo(ref twimageinfo, szTwmemref);

                // Save the image to disk, along with any metadata...
                sts = ReportImage(twimageinfo, m_intptrImage, m_iImageBytes);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error("ReportImage failed...");
                    Marshal.FreeHGlobal(m_intptrImage);
                    m_intptrImage = IntPtr.Zero;
                    m_iImageBytes = 0;
                    m_blDisableDsSent = true;
                    Rollback(TWAIN.STATE.S4);
                    return (TWAIN.STS.FILEWRITEERROR);
                }

                // Cleanup...
                Marshal.FreeHGlobal(m_intptrImage);
                m_intptrImage = IntPtr.Zero;
                m_iImageBytes = 0;

                // End the transfer...
                szTwmemref = "0,0";
                szStatus = "";
                sts = Send("DG_CONTROL", "DAT_PENDINGXFERS", "MSG_ENDXFER", ref szTwmemref, ref szStatus);
                m_twain.CsvToPendingXfers(ref twpendingxfers, szTwmemref);

                // Looks like we're done!
                if (twpendingxfers.Count == 0)
                {
                    m_blResetSent = true;
                    m_blDisableDsSent = true;
                    Rollback(TWAIN.STATE.S4);
                    return (TWAIN.STS.SUCCESS);
                }
            }

            // All done...
            return (TWAIN.STS.SUCCESS);
        }

        /// <summary>
        /// Go through the sequence needed to capture images using DAT_IMAGEMEMFILEXFER...
        /// </summary>
        private TWAIN.STS CaptureMemfileImages()
        {
            string szTwmemref = "";
            string szStatus = "";
            TWAIN.STS sts;
            TWAIN.TW_IMAGEINFO twimageinfo = default(TWAIN.TW_IMAGEINFO);
            TWAIN.TW_IMAGEMEMXFER twimagememxfer = default(TWAIN.TW_IMAGEMEMXFER);
            TWAIN.TW_PENDINGXFERS twpendingxfers = default(TWAIN.TW_PENDINGXFERS);

            // We're exiting, get out of here...
            if (m_blReset && !m_blResetSent)
            {
                m_blResetSent = true;
                m_blDisableDsSent = true;
                Rollback(TWAIN.STATE.S4);
                return (TWAIN.STS.SUCCESS);
            }

            // Dispatch on the state...
            switch (m_twain.GetState())
            {
                // Not a good state, just scoot...
                default:
                    m_blDisableDsSent = true;
                    Rollback(TWAIN.STATE.S4);
                    return (TWAIN.STS.SEQERROR);

                // We're on our way out...
                case TWAIN.STATE.S5:
                    m_blResetSent = true;
                    m_blDisableDsSent = true;
                    Rollback(TWAIN.STATE.S4);
                    return (TWAIN.STS.SUCCESS);

                // Memfile transfer...
                case TWAIN.STATE.S6:
                    // Gracefully end scanning, if that fails, go hard...
                    if (m_blStopFeeder && !m_blStopFeederSent)
                    {
                        m_blStopFeederSent = true;
                        szTwmemref = "0,0";
                        szStatus = "";
                        sts = Send("DG_CONTROL", "DAT_PENDINGXFERS", "MSG_STOPFEEDER", ref szTwmemref, ref szStatus);
                        if (sts != TWAIN.STS.SUCCESS)
                        {
                            m_blResetSent = true;
                            m_blDisableDsSent = true;
                            Rollback(TWAIN.STATE.S4);
                            return (sts);
                        }
                    }
                    // Transfer data...
                    szTwmemref = "0,0,0,0,0,0,0," + ((int)TWAIN.TWMF.APPOWNS | (int)TWAIN.TWMF.POINTER) + "," + m_twsetupmemxfer.Preferred + "," + m_intptrXfer;
                    szStatus = "";
                    sts = Send("DG_IMAGE", "DAT_IMAGEMEMFILEXFER", "MSG_GET", ref szTwmemref, ref szStatus);
                    m_twain.CsvToImagememxfer(ref twimagememxfer, szTwmemref);
                    if ((sts != TWAIN.STS.SUCCESS) && (sts != TWAIN.STS.XFERDONE))
                    {
                        m_blDisableDsSent = true;
                        Rollback(TWAIN.STATE.S4);
                        return (sts);
                    }
                    break;

                // Memfile transfer...
                case TWAIN.STATE.S7:
                    szTwmemref = "0,0,0,0,0,0,0," + ((int)TWAIN.TWMF.APPOWNS | (int)TWAIN.TWMF.POINTER) + "," + m_twsetupmemxfer.Preferred + "," + m_intptrXfer;
                    szStatus = "";
                    sts = Send("DG_IMAGE", "DAT_IMAGEMEMFILEXFER", "MSG_GET", ref szTwmemref, ref szStatus);
                    m_twain.CsvToImagememxfer(ref twimagememxfer, szTwmemref);
                    if ((sts != TWAIN.STS.SUCCESS) && (sts != TWAIN.STS.XFERDONE))
                    {
                        m_blDisableDsSent = true;
                        Rollback(TWAIN.STATE.S4);
                        return (sts);
                    }
                    break;
            }

            // Allocate or grow the image memory...
            if (m_intptrImage == IntPtr.Zero)
            {
                m_intptrImage = Marshal.AllocHGlobal((int)twimagememxfer.BytesWritten);
            }
            else
            {
                m_intptrImage = Marshal.ReAllocHGlobal(m_intptrImage, (IntPtr)(m_iImageBytes + twimagememxfer.BytesWritten));
            }

            // Ruh-roh...
            if (m_intptrImage == IntPtr.Zero)
            {
                m_blDisableDsSent = true;
                Rollback(TWAIN.STATE.S4);
                return (TWAIN.STS.LOWMEMORY);
            }

            // Copy into the buffer, and bump up our byte tally...
            TWAIN.MemCpy(m_intptrImage + m_iImageBytes, m_intptrXfer, (int)twimagememxfer.BytesWritten);
            m_iImageBytes += (int)twimagememxfer.BytesWritten;

            // If we saw XFERDONE we can save the image, display it,
            // end the transfer, and see if we have more images...
            if (sts == TWAIN.STS.XFERDONE)
            {
                // Get the image info...
                szTwmemref = "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0";
                szStatus = "";
                sts = Send("DG_IMAGE", "DAT_IMAGEINFO", "MSG_GET", ref szTwmemref, ref szStatus);
                m_twain.CsvToImageinfo(ref twimageinfo, szTwmemref);

                // Save the image to disk, along with any metadata...
                sts = ReportImage(twimageinfo, m_intptrImage, m_iImageBytes);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error("ReportImage failed...");
                    Marshal.FreeHGlobal(m_intptrImage);
                    m_intptrImage = IntPtr.Zero;
                    m_iImageBytes = 0;
                    m_blDisableDsSent = true;
                    Rollback(TWAIN.STATE.S4);
                    return (TWAIN.STS.FILEWRITEERROR);
                }

                // Cleanup...
                Marshal.FreeHGlobal(m_intptrImage);
                m_intptrImage = IntPtr.Zero;
                m_iImageBytes = 0;

                // End the transfer...
                szTwmemref = "0,0";
                szStatus = "";
                sts = Send("DG_CONTROL", "DAT_PENDINGXFERS", "MSG_ENDXFER", ref szTwmemref, ref szStatus);
                m_twain.CsvToPendingXfers(ref twpendingxfers, szTwmemref);

                // Looks like we're done!
                if (twpendingxfers.Count == 0)
                {
                    m_blResetSent = true;
                    m_blDisableDsSent = true;
                    Rollback(TWAIN.STATE.S4);
                    return (TWAIN.STS.SUCCESS);
                }
            }

            // All done...
            return (TWAIN.STS.SUCCESS);
        }

        #endregion


        // Private Methods: ReportImage
        #region Private Methods: ReportImage

        /// <summary>
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        internal void Dispose(bool a_blDisposing)
        {
            if (a_blDisposing)
            {
                if (m_twain != null)
                {
                    Rollback(TWAIN.STATE.S1);
                }
            }
        }

        /// <summary>
        /// Handle an image...
        /// </summary>
        /// <param name="a_szFile">File name, if doing a file transfer</param>
        /// <param name="a_abImage">Raw image from transfer</param>
        /// <param name="a_iImageOffset">Byte offset into the raw image</param>
        private TWAIN.STS ReportImage
        (
            TWAIN.TW_IMAGEINFO a_twimageinfo,
            IntPtr a_intptrImage,
            int a_iImageBytes
        )
        {
            uint uu;
            int iImageBytes;
            bool blSuccess;
            string szFile;
            string szPdfFile;
            string szMetaFile;
            TWAIN.STS sts;

            // Make sure we have a folder...
            if (!Directory.Exists(m_szImagesFolder))
            {
                try
                {
                    Directory.CreateDirectory(m_szImagesFolder);
                }
                catch
                {
                    TWAINWorkingGroup.Log.Error("ReportImage: unable to create the images folder: " + m_szImagesFolder);
                    SetImageBlocksDrained(TWAIN.STS.FILENOTFOUND);
                    return (TWAIN.STS.FILENOTFOUND);
                }
            }

            // KEYWORD:imageBlock
            //
            // Create our filenames.  The output from TWAIN Direct on TWAIN
            // represents the finished product from the TWAIN Driver, but
            // does not necessarily represent what we're going to send up to
            // the application.  We want the ability to split these images
            // into one or more imageBlocks.  We could try to do that with
            // the TWAIN Driver using DAT_SETUPMEMXFER, but I'm afraid that
            // the differences among TWAIN Drivers will create too many
            // problems.  So I've opted to decouple the TWAIN Driver transfer
            // from the transfer to the TWAIN Direct application.
            //
            // When TwainDirect.Scanner sees this content, it can choose to
            // send them up, as-is.  Or it can do things with the data, like
            // splitting it into multiple image blocks.
            //
            // Whatever happens, it's up to the application to stitch it all
            // back together again.
            m_iImageCount += 1;
            szFile = m_szImagesFolder + Path.DirectorySeparatorChar + "img" + m_iImageCount.ToString("D6");
            szPdfFile = szFile + ".twpdf"; // PDF file from the TWAIN Driver (or generated by the bridge)
            szMetaFile = szFile + ".twmeta"; // Metadata from the TWAIN Driver (or generated by the bridge)

            // Cleanup...
            if (File.Exists(szPdfFile))
            {
                try
                {
                    File.Delete(szPdfFile);
                }
                catch (Exception exception)
                {
                    TWAINWorkingGroup.Log.Error("unable to delete file: " + szPdfFile + " - " + exception.Message);
                    SetImageBlocksDrained(TWAIN.STS.FILENOTFOUND);
                    return (TWAIN.STS.FILENOTFOUND);
                }
            }

            // Driver support
            #region Driver Support
            if (m_deviceregisterSession.GetTwainInquiryData().GetTwainDirectSupport() == DeviceRegister.TwainDirectSupport.Driver)
            {
                // Get the metadata for TW_EXTIMAGEINFO...
                TWAIN.TW_EXTIMAGEINFO twextimageinfo = default(TWAIN.TW_EXTIMAGEINFO);
                TWAIN.TW_INFO twinfo = default(TWAIN.TW_INFO);
                if (m_deviceregisterSession.GetTwainInquiryData().GetExtImageInfo())
                {
                    twextimageinfo.NumInfos = 0;
                    twinfo.InfoId = (ushort)TWAIN.TWEI.TWAINDIRECTMETADATA;
                    twextimageinfo.Set(twextimageinfo.NumInfos++, ref twinfo);
                    sts = m_twain.DatExtimageinfo(TWAIN.DG.IMAGE, TWAIN.MSG.GET, ref twextimageinfo);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Error("DAT_EXTIMAGEINFO failed: " + sts);
                        SetImageBlocksDrained(TWAIN.STS.FILENOTFOUND);
                        return (TWAIN.STS.FILENOTFOUND);
                    }
                }

                // Write the image data...
                try
                {
                    string szFinalFilename;
                    iImageBytes = TWAIN.WriteImageFile(szPdfFile, a_intptrImage, a_iImageBytes, out szFinalFilename);
                    if (iImageBytes != a_iImageBytes)
                    {
                        TWAINWorkingGroup.Log.Error("ReportImage: unable to save the driver's image file, " + szPdfFile);
                        SetImageBlocksDrained(TWAIN.STS.FILEWRITEERROR);
                        return (TWAIN.STS.FILEWRITEERROR);
                    }
                }
                catch (Exception exception)
                {
                    TWAINWorkingGroup.Log.Error("ReportImage: unable to save the image file, " + szPdfFile + " - " + exception.Message);
                    SetImageBlocksDrained(TWAIN.STS.FILEWRITEERROR);
                    return (TWAIN.STS.FILEWRITEERROR);
                }

                // Save the metadata to disk, the arrival of metadata is
                // a trigger that announces the image is done...
                for (uu = 0; uu < twextimageinfo.NumInfos; uu++)
                {
                    twextimageinfo.Get(uu, ref twinfo);
                    if (twinfo.InfoId == (ushort)TWAIN.TWEI.TWAINDIRECTMETADATA)
                    {
                        if (twinfo.ReturnCode == (ushort)TWAIN.STS.SUCCESS)
                        {
                            try
                            {
                                IntPtr Item;
                                IntPtr Handle;

                                // Ugh...
                                if (Marshal.SizeOf(twinfo.Item) == 4)
                                {
                                    Handle = unchecked((IntPtr)(int)twinfo.Item);
                                }
                                else
                                {
                                    Handle = unchecked((IntPtr)(long)twinfo.Item);
                                }

                                // Lock the handle...
                                Item = m_twain.DsmMemLock(Handle);

                                // Get the data, watch out for a terminating NUL, we
                                // don't want that to end up in the file...
                                string szMeta;
                                byte[] abMeta = new byte[twinfo.NumItems];
                                Marshal.Copy(Item, abMeta, 0, (int)twinfo.NumItems);
                                if (abMeta[abMeta.Length - 1] == 0)
                                {
                                    szMeta = Encoding.UTF8.GetString(abMeta, 0, abMeta.Length - 1);
                                }
                                else
                                {
                                    szMeta = Encoding.UTF8.GetString(abMeta);
                                }

                                // Unlock the handle...
                                m_twain.DsmMemUnlock(Handle);

                                // Okay, write it out and log it...
                                File.WriteAllText(szMetaFile, szMeta);
                                TWAINWorkingGroup.Log.Info("ReportImage: saved " + szMetaFile);
                                TWAINWorkingGroup.Log.Info(szMeta);
                            }
                            catch
                            {
                                TWAINWorkingGroup.Log.Error("ReportImage: unable to save the metadata file...");
                                SetImageBlocksDrained(TWAIN.STS.FILEWRITEERROR);
                                return (TWAIN.STS.FILEWRITEERROR);
                            }
                        }
                        break;
                    }
                }
            }
            #endregion

            // Non-driver support
            #region Non-driver support
            else
            {
                // Get the metadata for TW_EXTIMAGEINFO...
                TWAIN.TW_EXTIMAGEINFO twextimageinfo = default(TWAIN.TW_EXTIMAGEINFO);
                TWAIN.TW_INFO twinfo = default(TWAIN.TW_INFO);
                if (m_deviceregisterSession.GetTwainInquiryData().GetExtImageInfo())
                {
                    twextimageinfo.NumInfos = 0;
                    twinfo.InfoId = (ushort)TWAIN.TWEI.PAPERCOUNT; twextimageinfo.Set(twextimageinfo.NumInfos++, ref twinfo);
                    twinfo.InfoId = (ushort)TWAIN.TWEI.PAGESIDE; twextimageinfo.Set(twextimageinfo.NumInfos++, ref twinfo);
                    sts = m_twain.DatExtimageinfo(TWAIN.DG.IMAGE, TWAIN.MSG.GET, ref twextimageinfo);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        m_deviceregisterSession.GetTwainInquiryData().SetExtImageInfo(false);
                    }
                }

                // Get our pixelFormat...
                string szPixelFormat;
                switch ((TWAIN.TWPT)a_twimageinfo.PixelType)
                {
                    default:
                        TWAINWorkingGroup.Log.Error("ReportImage: bad pixeltype - " + a_twimageinfo.PixelType);
                        SetImageBlocksDrained(TWAIN.STS.FILEWRITEERROR);
                        return (TWAIN.STS.FAILURE);
                    case TWAIN.TWPT.BW:
                        szPixelFormat = "bw1";
                        break;
                    case TWAIN.TWPT.GRAY:
                        szPixelFormat = "gray8";
                        break;
                    case TWAIN.TWPT.RGB:
                        szPixelFormat = "rgb24";
                        break;
                }

                // Get our compression...
                string szCompression;
                switch ((TWAIN.TWCP)a_twimageinfo.Compression)
                {
                    default:
                        TWAINWorkingGroup.Log.Error("ReportImage: bad compression - " + a_twimageinfo.Compression);
                        SetImageBlocksDrained(TWAIN.STS.FILEWRITEERROR);
                        return (TWAIN.STS.FAILURE);
                    case TWAIN.TWCP.NONE:
                        szCompression = "none";
                        break;
                    case TWAIN.TWCP.GROUP4:
                        szCompression = "group4";
                        break;
                    case TWAIN.TWCP.JPEG:
                        szCompression = "jpeg";
                        break;
                }

                // Work out the source...
                string szSource = "";
                bool blGotSheetNumber = false;
                if (m_blFlatbed)
                {
                    szSource = "flatbed";
                    m_iSheetNumber += 1;
                    blGotSheetNumber = true;
                }

                // The image came from a feeder...
                else
                {
                    // See if we can get the side from the extended image info...
                    if (m_deviceregisterSession.GetTwainInquiryData().GetExtImageInfo())
                    {
                        for (uu = 0; uu < twextimageinfo.NumInfos; uu++)
                        {
                            twextimageinfo.Get(uu, ref twinfo);
                            if (twinfo.InfoId == (ushort)TWAIN.TWEI.PAPERCOUNT)
                            {
                                if (twinfo.ReturnCode == (ushort)TWAIN.STS.SUCCESS)
                                {
                                    m_iSheetNumber = (int)twinfo.Item;
                                }
                            }
                            else if (twinfo.InfoId == (ushort)TWAIN.TWEI.PAGESIDE)
                            {
                                if (twinfo.ReturnCode == (ushort)TWAIN.STS.SUCCESS)
                                {
                                    if (twinfo.Item == (UIntPtr)TWAIN.TWCS.TOP)
                                    {
                                        szSource = "feederFront";
                                    }
                                    else
                                    {
                                        szSource = "feederRear";
                                    }
                                }
                                break;
                            }
                        }
                    }

                    // We didn't get a pageside.  So we're going to make
                    // the best guess we can.
                    if (szSource == "")
                    {
                        // We're just doing simplex front at the moment...
                        if (!m_blDuplex)
                        {
                            szSource = "feederFront";
                            if (!blGotSheetNumber)
                            {
                                m_iSheetNumber += 1;
                            }
                        }

                        // We're duplex...
                        else
                        {
                            // Odd number images (we start at 1)...
                            if ((m_iImageCount & 1) == 1)
                            {
                                szSource = "feederFront";
                                if (!blGotSheetNumber)
                                {
                                    m_iSheetNumber += 1;
                                }
                            }
                            // Even number images...
                            else
                            {
                                szSource = "feederRear";
                            }
                        }
                    }
                }

                // Try to sort out a lookup...
                ProcessSwordTask.ConfigureNameLookup configurenamelookup = null;
                if (m_configurenamelookup != null)
                {
                    configurenamelookup = m_configurenamelookup.Find(szSource, szPixelFormat);
                }
                else
                {
                    ProcessSwordTask.ConfigureNameLookup.Add(ref configurenamelookup, "stream0", "source0", "pixelFormat0", "", "");
                }

                // Create the TWAIN Direct metadata...
                string szMeta = "";

                // Root begins...
                szMeta += "{";

                // TWAIN Direct metadata.address begin...
                szMeta += "\"metadata\":{";

                // TWAIN Direct metadata.address begin...
                szMeta += "\"address\":{";

                // Imagecount (counts images)...
                szMeta += "\"imageNumber\":" + m_iImageCount + ",";

                // Segmentcount (long document or huge document)...
                szMeta += "\"imagePart\":" + "1" + ",";

                // Segmentlast (long document or huge document)...
                szMeta += "\"moreParts\":" + "\"lastPartInFile\",";

                // Sheetcount (counts sheets, including ones lost to blank image dropout)...
                szMeta += "\"sheetNumber\":" + m_iSheetNumber + ",";

                // The image came from a flatbed or a feederFront or whatever...
                szMeta += "\"source\":\"" + szSource + "\",";

                // Name of this stream...
                szMeta += "\"streamName\":\"" + configurenamelookup.GetStreamName() + "\",";

                // Name of this source...
                szMeta += "\"sourceName\":\"" + configurenamelookup.GetSourceName() + "\",";

                // Name of this pixelFormat...
                szMeta += "\"pixelFormatName\":\"" + configurenamelookup.GetPixelFormatName() + "\"";

                // TWAIN Direct metadata.address end...
                szMeta += "},";

                // TWAIN Direct metadata.image begin...
                szMeta += "\"image\":{";

                // Add compression...
                szMeta += "\"compression\":\"" + szCompression + "\",";

                // Add pixel format...
                szMeta += "\"pixelFormat\":\"" + szPixelFormat + "\",";

                // Add height...
                szMeta += "\"pixelHeight\":" + a_twimageinfo.ImageLength + ",";

                // X-offset...
                szMeta += "\"pixelOffsetX\":" + "0" + ",";

                // Y-offset...
                szMeta += "\"pixelOffsetY\":" + "0" + ",";

                // Add width...
                szMeta += "\"pixelWidth\":" + a_twimageinfo.ImageWidth + ",";

                // Add resolution...
                szMeta += "\"resolution\":" + a_twimageinfo.XResolution.Whole;

                // TWAIN Direct metadata.image end...
                szMeta += "},";

                // Open SWORD.metadata.status...
                szMeta += "\"status\":{";

                // Add the status...
                szMeta += "\"success\":true";

                // TWAIN Direct metadata.status end...
                szMeta += "}";

                // TWAIN Direct metadata end...
                szMeta += "}";

                // Root ends...
                szMeta += "}";

                // Unfortunately, we need a copy...
                byte[] abImage = new byte[m_iImageBytes];
                Marshal.Copy(m_intptrImage, abImage, 0, m_iImageBytes);

                // We have to do this ourselves, save as PDF/Raster...
                blSuccess = PdfRaster.CreatePdfRaster
                (
                    szPdfFile,
                    m_szEncryptionProfileName,
                    Config.Get("pfxFile", ""),
                    Config.Get("pfxFilePassword", ""),
                    szMeta,
                    abImage,
                    0,
                    szPixelFormat,
                    szCompression,
                    a_twimageinfo.XResolution.Whole,
                    a_twimageinfo.ImageWidth,
                    a_twimageinfo.ImageLength
                );
                if (!blSuccess)
                {
                    TWAINWorkingGroup.Log.Error("ReportImage: unable to save the image file, " + szPdfFile);
                    SetImageBlocksDrained(TWAIN.STS.FILEWRITEERROR);
                    return (TWAIN.STS.FAILURE);
                }

                // Save the metadata to disk, the arrival of metadata is
                // a trigger that announces the image is done...
                try
                {
                    File.WriteAllText(szMetaFile, szMeta);
                    TWAINWorkingGroup.Log.Info("ReportImage: saved " + szMetaFile);
                }
                catch
                {
                    TWAINWorkingGroup.Log.Error("ReportImage: unable to save the metadata file...");
                    SetImageBlocksDrained(TWAIN.STS.FILEWRITEERROR);
                    return (TWAIN.STS.FILEWRITEERROR);
                }
            }
            #endregion

            // All done...
            return (TWAIN.STS.SUCCESS);
        }

        /// <summary>
        /// Remove the imageBlocksDrained file...
        /// </summary>
        private void ClearImageBlocksDrained()
        {
            m_iSheetNumber = 0;
            m_blSessionImageBlocksDrained = false;
            string szSessionImageBlocksDrained = Path.Combine(m_szImagesFolder, "imageBlocksDrained.meta");
            if (File.Exists(szSessionImageBlocksDrained))
            {
                File.Delete(szSessionImageBlocksDrained);
            }
        }

        /// <summary>
        /// Things we'd like to do if we lose the IPC connection...
        /// </summary>
        /// <param name="a_objectContext">our object</param>
        private static void IpcDisconnectLaunchpad(object a_objectContext)
        {
            TwainLocalOnTwain twainlocalontwain = (TwainLocalOnTwain)a_objectContext;
            twainlocalontwain.IpcDisconnect(a_objectContext);
        }
        private void IpcDisconnect(object a_objectContext)
        {
            // If we've been disarmed, it means that we're shutting down nicely...
            if (!m_blIpcDisconnectCallbackArmed)
            {
                return;
            }

            // Make a note of it...
            TWAINWorkingGroup.Log.Error("IpcDisconnect called...");

            // Try to shut us down...
            if (m_twain != null)
            {
                Rollback(TWAIN.STATE.S1);
            }

            // All done...
            Environment.Exit(0);
        }

        /// <summary>
        /// Set imageblocks drained with a status...
        /// </summary>
        /// <param name="a_sts">status of end of job</param>
        private void SetImageBlocksDrained(TWAIN.STS a_sts)
        {
            // KEYWORD:DEVELOPER
            // Use this to help test for errors like PAPERJAM and DOUBLEFEED, note that
            // these are TWRC_ and TWCC_ names...
            string szOverride = Config.Get("developerForceSetImageBlocksDrained", "");
            if (!string.IsNullOrEmpty(szOverride))
            {
                TWAINWorkingGroup.Log.Error("Developer is forcing scanning to end with: " + szOverride);
            }

            // Update the file...
            string szSessionImageBlocksDrained = Path.Combine(m_szImagesFolder, "imageBlocksDrained.meta");
            if (!File.Exists(szSessionImageBlocksDrained))
            {
                TWAINWorkingGroup.Log.Info("SetImageBlocksDrained: " + a_sts);
                try
                {
                    File.WriteAllText
                    (
                        szSessionImageBlocksDrained,
                        "{" +
                        "\"detected\":\"" + (string.IsNullOrEmpty(szOverride) ? a_sts.ToString() : szOverride) + "\"" +
                        "}"
                    );
                }
                catch (Exception exception)
                { 
                    TWAINWorkingGroup.Log.Error("SetImageBlocksDrained: error writing <" + szSessionImageBlocksDrained + "> - " + exception.Message);
                }
            }
        }

        #endregion


        // Private Methods: TWAIN Direct Client-Scanner API
        #region Private Methods: TWAIN Direct Client-Scanner API

        // The naming convention for this bit is Executer / Package / Command.  So, since
        // this is the device (scanner) section, the executer is the Device.  The TWAIN Local
        // package is "scanner" and the commands are TWAIN Direct Client-Scanner API commands.  If you
        // want to find the corresponding function used by applications, just replace
        // "Device" with "Client"...

        /// <summary>
        /// Close the TWAIN driver...
        /// </summary>
        /// <param name="a_szSession">the session data</param>
        /// <returns>a twain local status</returns>
        private TwainLocalScanner.ApiStatus DeviceScannerCloseSession(out string a_szSession)
        {
            string szStatus;
            string szUserinterface;

            // Init stuff...
            a_szSession = "";

            // Validate...
            if ((m_twain == null) || (m_szTwainDriverIdentity == null))
            {
                return (TwainLocalScanner.ApiStatus.invalidSessionId);
            }

            // Build the reply (we need this before the close so that we can get
            // the image block info, if there is any)...
            DeviceScannerGetSession(out a_szSession);

            // If we're out of images, then bail...
            if (m_blSessionImageBlocksDrained)
            {
                // Close the driver...
                Rollback(TWAIN.STATE.S1);
                m_szTwainDriverIdentity = null;
                return (TwainLocalScanner.ApiStatus.success);
            }

            // Make sure we're going bye-bye...
            SetImageBlocksDrained(TWAIN.STS.SUCCESS);

            // Otherwise, just make sure we've stopped scanning...
            switch (this.m_twain.GetState())
            {
                // DG_CONTROL / DAT_PENDINGXFERS / MSG_ENDXFER...
                case TWAIN.STATE.S7:
                    // We can't end the session from here, because it can only be issued
                    // in state 6, and only the scan loop knows what state it's currently
                    // in.  So we set a flag, and let the loop sort out when to send it...
                    m_blReset = true;
                    break;

                // DG_CONTROL / DAT_PENDINGXFERS / MSG_RESET...
                case TWAIN.STATE.S6:
                    // We can't end the session from here, because it can only be issued
                    // in state 6, and only the scan loop knows what state it's currently
                    // in.  So we set a flag, and let the loop sort out when to send it...
                    m_blReset = true;
                    break;

                // DG_CONTROL / DAT_USERINTERFACE / MSG_DISABLEDS, but only if we have no images...
                case TWAIN.STATE.S5:
                    szStatus = "";
                    szUserinterface = "0,0";
                    Send("DG_CONTROL", "DAT_USERINTERFACE", "MSG_DISABLEDS", ref szUserinterface, ref szStatus);
                    break;
            }

            // All done...
            return (TwainLocalScanner.ApiStatus.success);
        }

        /// <summary>
        /// Open the TWAIN driver...
        /// </summary>
        /// <param name="a_jsonlookup">data for the open</param>
        /// <param name="a_szSession">the session data</param>
        /// <returns>a twain local status</returns>
        private TwainLocalScanner.ApiStatus DeviceScannerCreateSession(JsonLookup a_jsonlookup, out string a_szSession)
        {
            string szStatus;
            TWAIN.STS sts;
            string szIntprhwnd;

            // Init stuff...
            a_szSession = "";

            // Make sure we have an images folder.  This is just a failsafe,
            // if TwainDirect.Scanner isn't already doing this, we have a
            // problem...
            try
            {
                if (!Directory.Exists(m_szImagesFolder))
                {
                    Directory.CreateDirectory(m_szImagesFolder);
                }
            }
            catch (Exception exception)
            {
                TWAINWorkingGroup.Log.Error("Could not create <" + m_szImagesFolder + "> - " + exception.Message);
                m_twain = null;
                m_szTwainDriverIdentity = null;
                return (TwainLocalScanner.ApiStatus.newSessionNotAllowed);
            }

            // Create the toolkit...
            try
            {
                m_twain = new TWAIN
                (
                    "TWAIN Working Group",
                    "TWAIN Sharp",
                    "SWORD-on-TWAIN",
                    2,
                    4,
                    (uint)(TWAIN.DG.APP2 | TWAIN.DG.CONTROL | TWAIN.DG.IMAGE),
                    TWAIN.TWCY.USA,
                    "testing...",
                    TWAIN.TWLG.ENGLISH_USA,
                    1,
                    0,
                    false,
                    true,
                    null,
                    ScanCallbackTrigger,
                    m_runinuithreaddelegate,
                    m_intptrHwnd
                );
            }
            catch
            {
                m_twain = null;
                m_szTwainDriverIdentity = null;
                return (TwainLocalScanner.ApiStatus.newSessionNotAllowed);
            }

            // Open the DSM...
            szIntprhwnd = m_intptrHwnd.ToString();
            szStatus = "";
            sts = Send("DG_CONTROL", "DAT_PARENT", "MSG_OPENDSM", ref szIntprhwnd, ref szStatus);
            if (sts != TWAIN.STS.SUCCESS)
            {
                return (TwainLocalScanner.ApiStatus.newSessionNotAllowed);
            }

            // Load our deviceregister object...
            m_deviceregisterSession = new DeviceRegister();
            m_deviceregisterSession.Load("{\"scanner\":" + a_jsonlookup.Get("scanner") + "}");

            // Life sucks.
            // On a side note, the ty= field contains the TW_IDENTITY.ProductName
            // we need to find our scanner...
            if (TwainLocalScanner.GetPlatform() == TwainLocalScanner.Platform.WINDOWS)
            {
                m_szTwainDriverIdentity = "0,0,0,USA,USA, ,0,0,0xFFFFFFFF, , ," + m_deviceregisterSession.GetTwainLocalTy();
            }
            else if (TwainLocalScanner.GetPlatform() == TwainLocalScanner.Platform.MACOSX)
            {
                m_szTwainDriverIdentity = "0,0,0,USA,USA, ,0,0,0xFFFFFFFF, , ," + m_deviceregisterSession.GetTwainLocalTy();
            }
            else
            {
                m_szTwainDriverIdentity = "1,0,0,USA,USA, ,0,0,0xFFFFFFFF, , ," + m_deviceregisterSession.GetTwainLocalTy();
            }

            // Open the driver...
            szStatus = "";
            sts = Send("DG_CONTROL", "DAT_IDENTITY", "MSG_OPENDS", ref m_szTwainDriverIdentity, ref szStatus);
            if (sts != TWAIN.STS.SUCCESS)
            {
                return (TwainLocalScanner.ApiStatus.newSessionNotAllowed);
            }

            // Build the reply...
            DeviceScannerGetSession(out a_szSession);

            // All done...
            return (TwainLocalScanner.ApiStatus.success);
        }

        /// <summary>
        /// Get the session data...
        /// </summary>
        /// <param name="a_szSession">the session data</param>
        /// <returns>status of the call</returns>
        private TwainLocalScanner.ApiStatus DeviceScannerGetSession(out string a_szSession)
        {
            string[] aszFiles;
            string[] aszTw = null;

            // Init stuff...
            a_szSession = "";

            // Validate...
            if ((m_twain == null) || (m_szTwainDriverIdentity == null))
            {
                return (TwainLocalScanner.ApiStatus.invalidSessionId);
            }

            // Look for images, the nice thing about this is that we don't
            // have to worry about our scanning state.  If we are scanning
            // and we have images, then we'll report them...
            try
            {
                aszFiles = Directory.GetFiles(m_szImagesFolder, "img*.meta");
                aszTw = Directory.GetFiles(m_szImagesFolder, "*.tw*");
            }
            catch
            {
                aszFiles = null;
            }
            string szImageBlocks = "";
            if (aszFiles != null)
            {
                foreach (string szFile in aszFiles)
                {
                    // We write the meta after the pdf, so if we have this we have the other...
                    if (szFile.EndsWith(".meta"))
                    {
                        string sz = Path.GetFileNameWithoutExtension(szFile);
                        sz = sz.Replace("img", "");
                        int iNumber = int.Parse(sz);
                        szImageBlocks += ((szImageBlocks != "") ? "," : "") + iNumber.ToString();
                    }
                }
            }

            // If we have no images, then check if the the scanner says
            // that we're out of images...
            if (    string.IsNullOrEmpty(szImageBlocks) // image data ready to be sent
                &&  File.Exists(Path.Combine(m_szImagesFolder, "imageBlocksDrained.meta")) // scanner is done
                &&  ((aszTw == null) || (aszTw.Length == 0))) // image data that needs to be finished
            {
                string szReason = File.ReadAllText(Path.Combine(m_szImagesFolder, "imageBlocksDrained.meta"));
                TWAINWorkingGroup.Log.Info("imageBlocksDrained.meta: " + szReason);
                m_blSessionImageBlocksDrained = true;
            }

            // Build the reply.  Note that we have this kind of code in three places
            // in the solution.  This is the lowest "level", where we generate the
            // data that will be sent to TwainDirect.Scanner, so it's not really in
            // the final form, though it's close.
            a_szSession = "\"session\":{";

            // Tack on the image blocks, if we have any, note that we don't need to
            // have imageBlocksComplete here, since every block will always
            // represent a complete image or image segment...
            if (!string.IsNullOrEmpty(szImageBlocks))
            {
                a_szSession += "\"imageBlocks\":[" + szImageBlocks + "]";
            }

            // End of the session object...
            a_szSession += "}";

            // All done...
            return (TwainLocalScanner.ApiStatus.success);
        }

        /// <summary>
        /// Process a TWAIN Direct task...
        /// </summary>
        /// <param name="a_jsonlookup">data for the task</param>
        /// <param name="a_swordtask">the result of the task</param>
        /// <param name="a_blSetAppCapabilities">set the application capabilities (ex: ICAP_XFERMECH)</param>
        /// <returns>a twain local status</returns>
        private TwainLocalScanner.ApiStatus DeviceScannerSendTask(JsonLookup a_jsonlookup, out ProcessSwordTask a_processswordtask, ref bool a_blSetAppCapabilities)
        {
            bool blSuccess;
            string szTask;
            string szStatus;
            TWAIN.STS sts;

            // Init stuff...
            a_processswordtask = new ProcessSwordTask(m_twain, m_szImagesFolder, m_deviceregisterSession);

            // Get the task from the TWAIN Local command...
            szTask = a_jsonlookup.GetJson("task");

            // TWAIN Driver Support...
            #region TWAIN Driver Support

            // Have the driver process the task...
            if (m_deviceregisterSession.GetTwainLocalTwainDirectSupport() == DeviceRegister.TwainDirectSupport.Driver)
            {
                string szMetadata;
                TWAIN.TW_TWAINDIRECT twtwaindirect = default(TWAIN.TW_TWAINDIRECT);

                // Convert the task to an array, and then copy it into
                // memory pointed to by a handle.  I'm NUL terminating
                // the data because it feels safer that way...
                byte[] abTask = Encoding.UTF8.GetBytes(szTask);
                IntPtr intptrTask = Marshal.AllocHGlobal(abTask.Length + 1);
                Marshal.Copy(abTask, 0, intptrTask, abTask.Length);
                Marshal.WriteByte(intptrTask, abTask.Length, 0);

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
                sts = Send("DG_CONTROL", "DAT_TWAINDIRECT", "MSG_SETTASK", ref szMetadata, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error("Process: MSG_SENDTASK failed");
                    Marshal.FreeHGlobal(intptrTask);
                    intptrTask = IntPtr.Zero;
                    //m_swordtaskresponse.SetError("fail", null, "invalidJson", lResponseCharacterOffset);
                    return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                }

                // TBD: Open up the reply (we should probably get the CsvToTwaindirect
                // function to do this for us)...
                string[] asz = szMetadata.Split(new char[] { ',' });
                if ((asz == null) || (asz.Length < 6))
                {
                    TWAINWorkingGroup.Log.Error("Process: MSG_SENDTASK failed");
                    Marshal.FreeHGlobal(intptrTask);
                    intptrTask = IntPtr.Zero;
                    //m_swordtaskresponse.SetError("fail", null, "invalidJson", lResponseCharacterOffset);
                    return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                }

                // Get the reply data...
                long lReceive;
                if (!long.TryParse(asz[4], out lReceive) || (lReceive == 0))
                {
                    TWAINWorkingGroup.Log.Error("Process: MSG_SENDTASK failed");
                    Marshal.FreeHGlobal(intptrTask);
                    intptrTask = IntPtr.Zero;
                    return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                }
                IntPtr intptrReceiveHandle = new IntPtr(lReceive);
                uint u32ReceiveBytes;
                if (!uint.TryParse(asz[5], out u32ReceiveBytes) || (u32ReceiveBytes == 0))
                {
                    TWAINWorkingGroup.Log.Error("Process: MSG_SENDTASK failed");
                    m_twain.DsmMemFree(ref intptrReceiveHandle);
                    Marshal.FreeHGlobal(intptrTask);
                    intptrTask = IntPtr.Zero;
                    //m_swordtaskresponse.SetError("fail", null, "invalidJson", lResponseCharacterOffset);
                    return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                }

                // Convert it to an array and then a string...
                IntPtr intptrReceive = m_twain.DsmMemLock(intptrReceiveHandle);
                byte[] abReceive = new byte[u32ReceiveBytes];
                Marshal.Copy(intptrReceive, abReceive, 0, (int)u32ReceiveBytes);
                string szReceive = Encoding.UTF8.GetString(abReceive);
                m_twain.DsmMemUnlock(intptrReceiveHandle);

                // Cleanup...
                m_twain.DsmMemFree(ref intptrReceiveHandle);
                Marshal.FreeHGlobal(intptrTask);
                intptrTask = IntPtr.Zero;

                // Squirrel the reply away...
                a_processswordtask.SetTaskReply(szReceive);
                return (TwainLocalScanner.ApiStatus.success);
            }

            #endregion

            // TWAIN Bridge Support...
            #region TWAIN Bridge Support...

            // Deserialize our task...
            blSuccess = a_processswordtask.Deserialize(szTask, "211a1e90-11e1-11e5-9493-1697f925ec7b");
            if (!blSuccess)
            {
                return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
            }

            // Process our task...
            blSuccess = a_processswordtask.ProcessAndRun(out m_configurenamelookup, out m_szEncryptionProfileName);
            if (!blSuccess)
            {
                return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
            }

            #endregion

            // All done...
            return (TwainLocalScanner.ApiStatus.success);
        }

        /// <summary>
        /// Start scanning...
        /// </summary>
        /// <param name="a_szSession">the session data</param>
        /// <returns>a twain local status</returns>
        private TwainLocalScanner.ApiStatus DeviceScannerStartCapturing(ref bool a_blSetAppCapabilities, out string a_szSession)
        {
            string szStatus;
            string szCapability;
            string szUserInterface;
            TWAIN.STS sts;

            // Init stuff...
            m_blStopFeeder = false;
            m_blStopFeederSent = false;
            m_blReset = false;
            m_blResetSent = false;
            m_iImageCount = 0;
            a_szSession = "";
            ClearImageBlocksDrained();

            // Validate...
            if (m_twain == null)
            {
                return (TwainLocalScanner.ApiStatus.invalidSessionId);
            }

            // Only do this if we haven't done it already...
            if (!a_blSetAppCapabilities)
            {
                // We should only have to do it once...
                a_blSetAppCapabilities = true;

                // If the TWAIN driver is doing most of the work, then we
                // want to use TWSX_MEMFILE transfers, and specify that
                // it should send TWFF_PDFRASTER images...
                if (m_deviceregisterSession.GetTwainInquiryData().GetTwainDirectSupport() == DeviceRegister.TwainDirectSupport.Driver)
                {
                    // Memory file transfer...
                    szStatus = "";
                    szCapability = "ICAP_XFERMECH,TWON_ONEVALUE,TWTY_UINT16," + Config.Get("icapXfermech", "TWSX_MEMFILE");
                    sts = Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Info("Action: we can't set ICAP_XFERMECH to TWSX_MEMFILE");
                        return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                    }

                    // No UI, all drivers must support this, unless useCapIndicators is set
                    // to false.  If this is done then the driver's UI will be used, which
                    // is a bad experience, but may be necessary for some folks.  The line
                    // is written so that only 'false' will work...
                    if (Config.Get("useCapIndicators", "true") != "false")
                    {
                        szStatus = "";
                        szCapability = "CAP_INDICATORS,TWON_ONEVALUE,TWTY_BOOL,FALSE";
                        sts = Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                        if (sts != TWAIN.STS.SUCCESS)
                        {
                            TWAINWorkingGroup.Log.Error("Action: we can't set CAP_INDICATORS to FALSE");
                            return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                        }
                    }

                    // Ask for extended image info...
                    szStatus = "";
                    szCapability = "ICAP_EXTIMAGEINFO";
                    sts = Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                    if ((sts == TWAIN.STS.SUCCESS) && (szStatus.EndsWith("0") || szStatus.EndsWith("FALSE")))
                    {
                        szStatus = "";
                        szCapability = "ICAP_EXTIMAGEINFO,TWON_ONEVALUE,TWTY_BOOL,TRUE";
                        sts = Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                        if (sts != TWAIN.STS.SUCCESS)
                        {
                            TWAINWorkingGroup.Log.Warn("Action: we can't set ICAP_EXTIMAGEINFO to TRUE");
                            return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                        }
                    }

                    // Ask for PDF/raster...
                    szStatus = "";
                    szCapability = "ICAP_IMAGEFILEFORMAT,TWON_ONEVALUE,TWTY_UINT16,TWFF_PDFRASTER";
                    sts = Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Warn("Action: we can't set ICAP_IMAGEFILEFORMAT to TWFF_PDFRASTER");
                        return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                    }

                    // Ask for PDF/raster again (kinda), because interfaces are hard,
                    // we need the placeholder.tmp file to satisfy this call, which
                    // has to be able to create the file.  However, we're not going to
                    // be using it, since TWSX_MEMFILE transfers files in memory...
                    szStatus = "";
                    string szPlaceholder = Path.Combine(m_szImagesFolder, "placeholder.tmp");
                    szCapability = szPlaceholder + ",TWFF_PDFRASTER,0";
                    sts = Send("DG_CONTROL", "DAT_SETUPFILEXFER", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Warn("Action: we can't set DAT_SETUPFILEXFER to TWFF_PDFRASTER");
                        return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                    }
                }

                // Otherwise we're going to do most of the work ourselves...
                else
                {
                    // Memory transfer (extended only)...
                    if (m_deviceregisterSession.GetTwainInquiryData().GetTwainDirectSupport() == DeviceRegister.TwainDirectSupport.Extended)
                    {
                        string szIcapXfermach = Config.Get("icapXfermech", "TWSX_MEMORY");

                        // Request the transfer type...
                        szStatus = "";
                        szCapability = "ICAP_XFERMECH,TWON_ONEVALUE,TWTY_UINT16," + szIcapXfermach;
                        sts = Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                        if (sts != TWAIN.STS.SUCCESS)
                        {
                            TWAINWorkingGroup.Log.Info("Action: we can't set ICAP_XFERMECH to " + szIcapXfermach);
                            return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                        }

                        // If we're doing file transfers, pick the file...
                        if (szIcapXfermach == "TWSX_FILE")
                        {
                            // Pick the file...
                            szStatus = "";
                            string szPlaceholder = Path.Combine(Path.GetDirectoryName(m_szImagesFolder), "imagefilexfer");
                            if (Directory.Exists(szPlaceholder))
                            {
                                Directory.Delete(szPlaceholder, true);
                            }
                            Directory.CreateDirectory(szPlaceholder);
                            szCapability = szPlaceholder + ",TWFF_TIFF,0";
                            sts = Send("DG_CONTROL", "DAT_SETUPFILEXFER", "MSG_SET", ref szCapability, ref szStatus);
                            if (sts != TWAIN.STS.SUCCESS)
                            {
                                TWAINWorkingGroup.Log.Warn("Action: we can't set DAT_SETUPFILEXFER to TWFF_TIFF");
                                return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                            }

                            // And ask for this, because TIFF JPEG is ugly...
                            //m_twaincstoolkit.SetAutomaticJpegOrTiff(true);
                        }
                    }
                    // Native transfer (basic)...
                    else
                    {
                        szStatus = "";
                        szCapability = "ICAP_XFERMECH,TWON_ONEVALUE,TWTY_UINT16,TWSX_NATIVE";
                        sts = Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                        if (sts != TWAIN.STS.SUCCESS)
                        {
                            TWAINWorkingGroup.Log.Info("Action: we can't set ICAP_XFERMECH to TWSX_NATIVE");
                            return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                        }
                    }

                    // No UI, all drivers must support this, unless useCapIndicators is set
                    // to false.  If this is done then the driver's UI will be used, which
                    // is a bad experience, but may be necessary for some folks.  The line
                    // is written so that only 'false' will work...
                    if (Config.Get("useCapIndicators", "true") != "false")
                    {
                        szStatus = "";
                        szCapability = "CAP_INDICATORS,TWON_ONEVALUE,TWTY_BOOL,0";
                        sts = Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                        if (sts != TWAIN.STS.SUCCESS)
                        {
                            TWAINWorkingGroup.Log.Error("Action: we can't set CAP_INDICATORS to FALSE");
                            return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                        }
                    }

                    // Ask for extended image info...
                    if (m_deviceregisterSession.GetTwainInquiryData().GetExtImageInfo())
                    {
                        szStatus = "";
                        szCapability = "ICAP_EXTIMAGEINFO";
                        sts = Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                        if ((sts == TWAIN.STS.SUCCESS) && szStatus.EndsWith("0"))
                        {
                            szStatus = "";
                            szCapability = "ICAP_EXTIMAGEINFO,TWON_ONEVALUE,TWTY_BOOL,1";
                            sts = Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                            if (sts != TWAIN.STS.SUCCESS)
                            {
                                TWAINWorkingGroup.Log.Warn("Action: we can't set ICAP_EXTIMAGEINFO to TRUE");
                                //return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                            }
                        }
                    }
                }
            }

            // No UI, all drivers must support this, unless useCapIndicators is set
            // to false.  If this is done then the driver's UI will be used, which
            // is a bad experience, but may be necessary for some folks.  The line
            // is written so that only 'false' will work...
            if (Config.Get("useCapIndicators", "true") != "false")
            {
                szUserInterface = "0,0";
            }
            else
            {
                szUserInterface = "1,0";
            }
                
            // Start scanning (no UI)...
            szStatus = "";
            sts = Send("DG_CONTROL", "DAT_USERINTERFACE", "MSG_ENABLEDS", ref szUserInterface, ref szStatus);
            if (sts != TWAIN.STS.SUCCESS)
            {
                TWAINWorkingGroup.Log.Info("Action: MSG_ENABLEDS failed");
                switch (sts)
                {
                    default:
                    case TWAIN.STS.CANCEL:
                    case TWAIN.STS.NOMEDIA:
                        SetImageBlocksDrained(TWAIN.STS.NOMEDIA);
                        return (TwainLocalScanner.ApiStatus.noMedia);
                    case TWAIN.STS.BUSY:
                        SetImageBlocksDrained(TWAIN.STS.BUSY);
                        return (TwainLocalScanner.ApiStatus.busy);
                }
            }

            // Build the reply...
            DeviceScannerGetSession(out a_szSession);

            // All done...
            if (sts == TWAIN.STS.SUCCESS)
            {
                return (TwainLocalScanner.ApiStatus.success);
            }
            return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
        }

        /// <summary>
        /// Stop the scanner.  We'll try it the nice way first.  If that doesn't fly, then
        /// we'll set a flag to reset the scanner next time we hit a msg_endxfer...
        /// </summary>
        /// <param name="a_szSession">the session data</param>
        /// <returns>a twain local status</returns>
        private TwainLocalScanner.ApiStatus DeviceScannerStopCapturing(out string a_szSession)
        {
            // Init stuff...
            a_szSession = "";

            // Validate...
            if (m_twain == null)
            {
                return (TwainLocalScanner.ApiStatus.invalidSessionId);
            }

            // We can't stop the feeder from here, because it can only be issued
            // in state 6, and only the scan loop knows what state it's currently
            // in.  So we set a flag, and let the loop sort out when to send it...
            m_blStopFeeder = true;

            // Build the reply...
            DeviceScannerGetSession(out a_szSession);

            // Oh well, we'll try to abort...
            return (TwainLocalScanner.ApiStatus.success);
        }

        #endregion


        // Private Attributes...
        #region Private Attributes...

        /// <summary>
        /// The TWAIN Toolkit object that front ends TWAIN for us...
        /// </summary>
        private TWAIN m_twain;
        private IntPtr m_intptrXfer;
        private IntPtr m_intptrImage;
        private int m_iImageBytes;
        private bool m_blXferReadySent;
        private bool m_blDisableDsSent;
        private TWAIN.TWSX m_twsxXferMech;
        private TWAIN.TW_SETUPMEMXFER m_twsetupmemxfer;

        /// <summary>
        /// Information about the scanner sent to use by createSession...
        /// </summary>
        private DeviceRegister m_deviceregisterSession;

        /// <summary>
        /// TWAIN identity of the scanner we're using...
        /// </summary>
        private string m_szTwainDriverIdentity;

        /// <summary>
        /// The folder where we write stuff...
        /// </summary>
        private string m_szWriteFolder;

        /// <summary>
        /// The folder under the writer folder where we keep images...
        /// </summary>
        private string m_szImagesFolder;

        /// <summary>
        /// The path to our interprocess communication files...
        /// </summary>
        private string m_szIpc;

        /// <summary>
        /// Control whether or not our disconnect callback is allowed
        /// to fire...
        /// </summary>
        private bool m_blIpcDisconnectCallbackArmed;

        /// <summary>
        /// Process id we're communicating with...
        /// </summary>
        private int m_iPid;

        /// <summary>
        /// Send MSG_STOPFEEDER when true, but only send it once...
        /// </summary>
        private bool m_blStopFeeder;
        private bool m_blStopFeederSent;

        /// <summary>
        /// Send MSG_RESET when true, but only send it once...
        /// </summary>
        private bool m_blReset;
        private bool m_blResetSent;

        /// <summary>
        /// Count of images for each TwainStartCapturing call, the
        /// first image is always 1...
        /// </summary>
        private int m_iImageCount;

        /// <summary>
        /// Count the sheets...
        /// </summary>
        private int m_iSheetNumber;

        /// <summary>
        /// End of job detected...
        /// </summary>
        private bool m_blSessionImageBlocksDrained;

        /// <summary>
        /// We're scanning from a flatbed...
        /// </summary>
        private bool m_blFlatbed;

        /// <summary>
        /// We're scanning duplex (front and rear) off an automatic document feeder (ADF)...
        /// </summary>
        private bool m_blDuplex;

        /// <summary>
        /// The delegate that lets us run stuff in the main GUI thread on Windows,
        /// and some anonymous data that is sent along with it.  We're also holding
        /// onto the handle for the anonymous data...
        /// </summary>
        private TWAIN.RunInUiThreadDelegate m_runinuithreaddelegate;
        private FormTwain m_formtwain;
        private IntPtr m_intptrHwnd;

        /// <summary>
        /// We'll use this to get the stream, source, and pixelFormat names for the
        /// metadata...
        /// </summary>
        private ProcessSwordTask.ConfigureNameLookup m_configurenamelookup;

        /// <summary>
        /// The name of an encryptionProfile or null...
        /// </summary>
        private string m_szEncryptionProfileName;

        #endregion
    }
}
