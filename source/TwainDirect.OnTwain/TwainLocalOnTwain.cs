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
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using TwainDirect.Support;
using TWAINWorkingGroup;
using TWAINWorkingGroupToolkit;

namespace TwainDirect.OnTwain
{
    /// <summary>
    /// Map TWAIN Local calls to TWAIN.  This seems like the best way to make
    /// sure we get all the needed data down to this level, however it means
    /// that we have knowledge of our caller at this level, so there will be
    /// some replication if we add support for another communication manager...
    /// </summary>
    internal sealed class TwainLocalOnTwain
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
            TWAINCSToolkit.RunInUiThreadDelegate a_runinuithreaddelegate,
            object a_objectRunInUiThread,
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
            m_objectRunInUiThread = a_objectRunInUiThread;
            m_intptrHwnd = a_intptrHwnd;

            // Init stuff...
            m_blFlatbed = false;
            m_blDuplex = false;

            // Log stuff...
            TWAINWorkingGroup.Log.Info("TWAIN images folder: " + m_szImagesFolder);
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

            // All done...
            TWAINWorkingGroup.Log.Info("IPC mode completed...");
            return (true);
        }

        #endregion


        // Private Methods: ReportImage
        #region Private Methods: ReportImage

        /// <summary>
        /// Handle an image...
        /// </summary>
        /// <param name="a_szTag">tag to locate a particular ReportImage call</param>
        /// <param name="a_szDg">Data group that preceeded this call</param>
        /// <param name="a_szDat">Data argument type that preceeded this call</param>
        /// <param name="a_szMsg">Message that preceeded this call</param>
        /// <param name="a_sts">Current status</param>
        /// <param name="a_bitmap">C# bitmap of the image</param>
        /// <param name="a_szFile">File name, if doing a file transfer</param>
        /// <param name="a_szTwimageinfo">Image info or null</param>
        /// <param name="a_abImage">Raw image from transfer</param>
        /// <param name="a_iImageOffset">Byte offset into the raw image</param>
        private TWAINCSToolkit.MSG ReportImage
        (
            string a_szTag,
            string a_szDg,
            string a_szDat,
            string a_szMsg,
            TWAIN.STS a_sts,
            Bitmap a_bitmap,
            string a_szFile,
            string a_szTwimageinfo,
            byte[] a_abImage,
            int a_iImageOffset
        )
        {
            uint uu;
            bool blSuccess;
            string szFile;
            string szPdfFile;
            string szMetaFile;
            TWAIN.STS sts;
            TWAIN twain;

            // We're processing end of scan...
            if (a_bitmap == null)
            {
                TWAINWorkingGroup.Log.Info("ReportImage: no more images: " + a_szDg + " " + a_szDat + " " + a_szMsg + " " + a_sts);
                SetImageBlocksDrained(a_sts);
                return (TWAINCSToolkit.MSG.RESET);
            }

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
                    return (TWAINCSToolkit.MSG.RESET);
                }
            }

            // Init stuff...
            twain = m_twaincstoolkit.Twain();

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
                    return (TWAINCSToolkit.MSG.RESET);
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
                    sts = twain.DatExtimageinfo(TWAIN.DG.IMAGE, TWAIN.MSG.GET, ref twextimageinfo);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Error("DAT_EXTIMAGEINFO failed: " + sts);
                        SetImageBlocksDrained(TWAIN.STS.FILENOTFOUND);
                        return (TWAINCSToolkit.MSG.RESET);
                    }
                }

                // Write the image data...
                try
                {
                    File.WriteAllBytes(szPdfFile, a_abImage);
                }
                catch (Exception exception)
                {
                    TWAINWorkingGroup.Log.Error("ReportImage: unable to save the image file, " + szPdfFile + " - " + exception.Message);
                    SetImageBlocksDrained(TWAIN.STS.FILEWRITEERROR);
                    return (TWAINCSToolkit.MSG.RESET);
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
                                Item = m_twaincstoolkit.DsmMemLock(Handle);

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
                                m_twaincstoolkit.DsmMemUnlock(Handle);

                                // Okay, write it out and log it...
                                File.WriteAllText(szMetaFile, szMeta);
                                TWAINWorkingGroup.Log.Info("ReportImage: saved " + szMetaFile);
                                TWAINWorkingGroup.Log.Info(szMeta);
                            }
                            catch
                            {
                                TWAINWorkingGroup.Log.Error("ReportImage: unable to save the metadata file...");
                                SetImageBlocksDrained(TWAIN.STS.FILEWRITEERROR);
                                return (TWAINCSToolkit.MSG.RESET);
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
                        SetImageBlocksDrained(sts);
                        return (TWAINCSToolkit.MSG.RESET);
                    }
                }

                // Get the metadata for TW_EXTIMAGEINFO...
                TWAIN.TW_EXTIMAGEINFO twextimageinfo = default(TWAIN.TW_EXTIMAGEINFO);
                TWAIN.TW_INFO twinfo = default(TWAIN.TW_INFO);
                if (m_deviceregisterSession.GetTwainInquiryData().GetExtImageInfo())
                {
                    twextimageinfo.NumInfos = 0;
                    twinfo.InfoId = (ushort)TWAIN.TWEI.DOCUMENTNUMBER; twextimageinfo.Set(twextimageinfo.NumInfos++, ref twinfo);
                    twinfo.InfoId = (ushort)TWAIN.TWEI.PAGENUMBER; twextimageinfo.Set(twextimageinfo.NumInfos++, ref twinfo);
                    twinfo.InfoId = (ushort)TWAIN.TWEI.PAGESIDE; twextimageinfo.Set(twextimageinfo.NumInfos++, ref twinfo);
                    sts = twain.DatExtimageinfo(TWAIN.DG.IMAGE, TWAIN.MSG.GET, ref twextimageinfo);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        m_deviceregisterSession.GetTwainInquiryData().SetExtImageInfo(false);
                    }
                }

                // Get our pixelFormat...
                string szPixelFormat;
                switch ((TWAIN.TWPT)twimageinfo.PixelType)
                {
                    default:
                        TWAINWorkingGroup.Log.Error("ReportImage: bad pixeltype - " + twimageinfo.PixelType);
                        SetImageBlocksDrained(TWAIN.STS.FILEWRITEERROR);
                        return (TWAINCSToolkit.MSG.RESET);
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
                switch ((TWAIN.TWCP)twimageinfo.Compression)
                {
                    default:
                        TWAINWorkingGroup.Log.Error("ReportImage: bad compression - " + twimageinfo.Compression);
                        SetImageBlocksDrained(TWAIN.STS.FILEWRITEERROR);
                        return (TWAINCSToolkit.MSG.RESET);
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
                if (m_blFlatbed)
                {
                    szSource = "flatbed";
                }

                // The image came from a feeder...
                else
                {
                    // See if we can get the side from the extended image info...
                    if (m_deviceregisterSession.GetTwainInquiryData().GetExtImageInfo())
                    {
                        bool blUpdateSheetNumber = true;
                        for (uu = 0; uu < twextimageinfo.NumInfos; uu++)
                        {
                            twextimageinfo.Get(uu, ref twinfo);
                            if (twinfo.InfoId == (ushort)TWAIN.TWEI.DOCUMENTNUMBER)
                            {
                                if (twinfo.ReturnCode == (ushort)TWAIN.STS.SUCCESS)
                                {
                                    if (twinfo.Item != m_uintptrDocumentNumber)
                                    {
                                        if (blUpdateSheetNumber)
                                        {
                                            m_iSheetNumber += 1;
                                            blUpdateSheetNumber = false;
                                        }
                                        m_uintptrDocumentNumber = twinfo.Item;
                                    }
                                }
                            }
                            else if (twinfo.InfoId == (ushort)TWAIN.TWEI.PAGENUMBER)
                            {
                                if (twinfo.ReturnCode == (ushort)TWAIN.STS.SUCCESS)
                                {
                                    if (twinfo.Item != m_uintptrPageNumber)
                                    {
                                        if (blUpdateSheetNumber)
                                        {
                                            m_iSheetNumber += 1;
                                            blUpdateSheetNumber = false;
                                        }
                                        m_uintptrPageNumber = twinfo.Item;
                                    }
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
                        }

                        // We're duplex...
                        else
                        {
                            // Odd number images (we start at 1)...
                            if ((m_iImageCount & 1) == 1)
                            {
                                szSource = "feederFront";
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
                szMeta += "\"pixelHeight\":" + twimageinfo.ImageLength + ",";

                // X-offset...
                szMeta += "\"pixelOffsetX\":" + "0" + ",";

                // Y-offset...
                szMeta += "\"pixelOffsetY\":" + "0" + ",";

                // Add width...
                szMeta += "\"pixelWidth\":" + twimageinfo.ImageWidth + ",";

                // Add resolution...
                szMeta += "\"resolution\":" + twimageinfo.XResolution.Whole;

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

                // We have to do this ourselves, save as PDF/Raster...
                blSuccess = PdfRaster.CreatePdfRaster
                (
                    szPdfFile,
                    szMeta,
                    a_abImage,
                    a_iImageOffset,
                    szPixelFormat,
                    szCompression,
                    twimageinfo.XResolution.Whole,
                    twimageinfo.ImageWidth,
                    twimageinfo.ImageLength
                );
                if (!blSuccess)
                {
                    TWAINWorkingGroup.Log.Error("ReportImage: unable to save the image file, " + szPdfFile);
                    SetImageBlocksDrained(TWAIN.STS.FILEWRITEERROR);
                    return (TWAINCSToolkit.MSG.RESET);
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
                    return (TWAINCSToolkit.MSG.RESET);
                }
            }
            #endregion

            // We've been asked to stop the feeder, so sneak that in, but only do it once...
            if (    m_blStopFeeder
                &&  !m_blStopFeederSent
                &&  (m_twaincstoolkit.GetState() == 6))
            {
                m_blStopFeederSent = true;
                TWAIN.TW_PENDINGXFERS twpendingxfers = default(TWAIN.TW_PENDINGXFERS);
                sts = twain.DatPendingxfers(TWAIN.DG.CONTROL, TWAIN.MSG.STOPFEEDER, ref twpendingxfers);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error("ReportImage: DatPendingxfers failed...");
                }
            }

            // All done...
            return (TWAINCSToolkit.MSG.ENDXFER);
        }

        /// <summary>
        /// Remove the imageBlocksDrained file...
        /// </summary>
        private void ClearImageBlocksDrained()
        {
            m_iSheetNumber = 0;
            if (UIntPtr.Size == 4)
            {
                m_uintptrDocumentNumber = new UIntPtr(uint.MaxValue);
                m_uintptrPageNumber = new UIntPtr(uint.MaxValue);
            }
            else
            {
                m_uintptrDocumentNumber = new UIntPtr(ulong.MaxValue);
                m_uintptrPageNumber = new UIntPtr(ulong.MaxValue);
            }
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
            if (m_twaincstoolkit != null)
            {
                m_twaincstoolkit.Cleanup();
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
            string szPendingxfers;
            string szUserinterface;

            // Init stuff...
            a_szSession = "";

            // Validate...
            if ((m_twaincstoolkit == null) || (m_szTwainDriverIdentity == null))
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
                szStatus = "";
                m_twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_CLOSEDS", ref m_szTwainDriverIdentity, ref szStatus);
                m_twaincstoolkit.Cleanup();
                m_twaincstoolkit = null;
                m_szTwainDriverIdentity = null;
                return (TwainLocalScanner.ApiStatus.success);
            }

            // Otherwise, just make sure we've stopped scanning...
            switch (this.m_twaincstoolkit.GetState())
            {
                // DG_CONTROL / DAT_PENDINGXFERS / MSG_ENDXFER...
                case 7:
                    szStatus = "";
                    szPendingxfers = "0,0";
                    m_twaincstoolkit.Send("DG_CONTROL", "DAT_PENDINGXFERS", "MSG_ENDXFER", ref szPendingxfers, ref szStatus);
                    break;

                // DG_CONTROL / DAT_PENDINGXFERS / MSG_RESET...
                case 6:
                    szStatus = "";
                    szPendingxfers = "0,0";
                    m_twaincstoolkit.Send("DG_CONTROL", "DAT_PENDINGXFERS", "MSG_RESET", ref szPendingxfers, ref szStatus);
                    break;

                // DG_CONTROL / DAT_USERINTERFACE / MSG_DISABLEDS, but only if we have no images...
                case 5:
                    szStatus = "";
                    szUserinterface = "0,0";
                    m_twaincstoolkit.Send("DG_CONTROL", "DAT_USERINTERFACE", "MSG_DISABLEDS", ref szUserinterface, ref szStatus);
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
                m_twaincstoolkit = null;
                m_szTwainDriverIdentity = null;
                return (TwainLocalScanner.ApiStatus.newSessionNotAllowed);
            }

            // Create the toolkit...
            try
            {
                m_twaincstoolkit = new TWAINCSToolkit
                (
                    m_intptrHwnd,
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
                    m_runinuithreaddelegate,
                    m_objectRunInUiThread
                );
            }
            catch
            {
                m_twaincstoolkit = null;
                m_szTwainDriverIdentity = null;
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
            sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_OPENDS", ref m_szTwainDriverIdentity, ref szStatus);
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
            if ((m_twaincstoolkit == null) || (m_szTwainDriverIdentity == null))
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

            // Tack on the image blocks, if we have any...
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
            a_processswordtask = new ProcessSwordTask(m_szImagesFolder, m_twaincstoolkit, m_deviceregisterSession);

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
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_TWAINDIRECT", "MSG_SETTASK", ref szMetadata, ref szStatus);
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
                    m_twaincstoolkit.DsmMemFree(ref intptrReceiveHandle);
                    Marshal.FreeHGlobal(intptrTask);
                    intptrTask = IntPtr.Zero;
                    //m_swordtaskresponse.SetError("fail", null, "invalidJson", lResponseCharacterOffset);
                    return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
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
            blSuccess = a_processswordtask.ProcessAndRun(out m_configurenamelookup);
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
            m_iImageCount = 0;
            a_szSession = "";
            ClearImageBlocksDrained();

            // Validate...
            if (m_twaincstoolkit == null)
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
                    szCapability = "ICAP_XFERMECH,TWON_ONEVALUE,TWTY_UINT16,4"; // TWSX_MEMFILE
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Info("Action: we can't set ICAP_XFERMECH to TWSX_MEMFILE");
                        return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                    }

                    // No UI...
                    szStatus = "";
                    szCapability = "CAP_INDICATORS,TWON_ONEVALUE,TWTY_BOOL,0";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Error("Action: we can't set CAP_INDICATORS to FALSE");
                        return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                    }

                    // Ask for extended image info...
                    szStatus = "";
                    szCapability = "ICAP_EXTIMAGEINFO";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                    if ((sts == TWAIN.STS.SUCCESS) && szStatus.EndsWith("0"))
                    {
                        szStatus = "";
                        szCapability = "ICAP_EXTIMAGEINFO,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                        sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                        if (sts != TWAIN.STS.SUCCESS)
                        {
                            TWAINWorkingGroup.Log.Warn("Action: we can't set ICAP_EXTIMAGEINFO to TRUE");
                            return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                        }
                    }

                    // Ask for PDF/raster...
                    szStatus = "";
                    szCapability = "ICAP_IMAGEFILEFORMAT,TWON_ONEVALUE,TWTY_UINT16,17"; // TWFF_PDFRASTER
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
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
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_SETUPFILEXFER", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Warn("Action: we can't set DAT_SETUPFILEXFER to TWFF_PDFRASTER");
                        return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                    }
                }

                // Otherwise we're going to do most of the work ourselves...
                else
                {
                    // Memory transfer...
                    szStatus = "";
                    szCapability = "ICAP_XFERMECH,TWON_ONEVALUE,TWTY_UINT16,2";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Info("Action: we can't set ICAP_XFERMECH to TWSX_MEMORY");
                        return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                    }

                    // No UI...
                    szStatus = "";
                    szCapability = "CAP_INDICATORS,TWON_ONEVALUE,TWTY_BOOL,0";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Error("Action: we can't set CAP_INDICATORS to FALSE");
                        return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                    }

                    // Ask for extended image info...
                    if (m_deviceregisterSession.GetTwainInquiryData().GetExtImageInfo())
                    {
                        szStatus = "";
                        szCapability = "ICAP_EXTIMAGEINFO";
                        sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                        if ((sts == TWAIN.STS.SUCCESS) && szStatus.EndsWith("0"))
                        {
                            szStatus = "";
                            szCapability = "ICAP_EXTIMAGEINFO,TWON_ONEVALUE,TWTY_BOOL,1";
                            sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                            if (sts != TWAIN.STS.SUCCESS)
                            {
                                TWAINWorkingGroup.Log.Warn("Action: we can't set ICAP_EXTIMAGEINFO to TRUE");
                                //return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                            }
                        }
                    }
                }
            }

            // Start scanning (no UI)...
            szStatus = "";
            szUserInterface = "0,0";
            sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_USERINTERFACE", "MSG_ENABLEDS", ref szUserInterface, ref szStatus);
            if (sts != TWAIN.STS.SUCCESS)
            {
                TWAINWorkingGroup.Log.Info("Action: MSG_ENABLEDS failed");
                return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
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
            if (m_twaincstoolkit == null)
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
        private TWAINCSToolkit m_twaincstoolkit;

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
        /// Count of images for each TwainStartCapturing call, the
        /// first image is always 1...
        /// </summary>
        private int m_iImageCount;

        /// <summary>
        /// Count the sheets...
        /// </summary>
        private UIntPtr m_uintptrDocumentNumber;
        private UIntPtr m_uintptrPageNumber;
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
        private TWAINCSToolkit.RunInUiThreadDelegate m_runinuithreaddelegate;
        private object m_objectRunInUiThread;
        private IntPtr m_intptrHwnd;

        /// <summary>
        /// We'll use this to get the stream, source, and pixelFormat names for the
        /// metadata...
        /// </summary>
        ProcessSwordTask.ConfigureNameLookup m_configurenamelookup;

        #endregion
    }
}
