///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirectOnTwain.TwainLocalToTwain
//
//  Map TWAIN Local calls to TWAIN calls...
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    17-Dec-2014     Initial Release
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
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using TwainDirectSupport;
using TWAINWorkingGroup;
using TWAINWorkingGroupToolkit;

namespace TwainDirectOnTwain
{
    /// <summary>
    /// Map TWAIN Local calls to TWAIN.  This seems like the best way to make
    /// sure we get all the needed data down to this level, however it means
    /// that we have knowledge of our caller at this level, so there will be
    /// some replication if we add support for another communication manager...
    /// </summary>
    public sealed class TwainLocalOnTwain
    {
        // Public Methods: Run
        #region Public Methods: Run

        /// <summary>
        /// Init stuff...
        /// </summary>
        public TwainLocalOnTwain
        (
            string a_szWriteFolder,
            string a_szIpc,
            int a_iPid,
            TWAINCSToolkit.RunInUiThreadDelegate a_runinuithreaddelegate,
            object a_objectRunInUiThread,
            IntPtr a_intptrHwnd
        )
        {
            // Remember this stuff...
            m_szWriteFolder = a_szWriteFolder;
            m_szImagesFolder = Path.Combine(m_szWriteFolder, "images");
            m_szIpc = a_szIpc;
            m_iPid = a_iPid;
            m_runinuithreaddelegate = a_runinuithreaddelegate;
            m_objectRunInUiThread = a_objectRunInUiThread;
            m_intptrHwnd = a_intptrHwnd;

            // Init stuff...
            m_blFlatbed = false;
            m_blDuplex = false;
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
            string szMetadataFile;
            string szThumbnailFile;
            string szSession;
            string szImageFile;
            Ipc ipc;
            SwordTask swordtask;
            TwainLocalScanner.ApiStatus apistatus;

            // Pipe mode starting...
            TWAINWorkingGroup.Log.Info("IPC mode starting...");

            // Set up communication with our server process...
            ipc = new Ipc(m_szIpc, false);
            ipc.MonitorPid(m_iPid);
            ipc.Connect();

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
                    default:
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

                    case "readImageBlock":
                        apistatus = DeviceScannerReadImageBlock(jsonlookup, out szImageFile, out szMetadataFile);
                        if (apistatus == TwainLocalScanner.ApiStatus.success)
                        {
                            apistatus = DeviceScannerGetSession(out szSession);
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\"," +
                                ((jsonlookup.Get("withMetadata", false) == "true") ? "\"meta\":\"" + szMetadataFile + "\"," : "") +
                                "\"imageFile\":\"" + szImageFile + "\"" +
                                (!string.IsNullOrEmpty(szSession) ? "," + szSession : "") +
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

                    case "readImageBlockMetadata":
                        apistatus = DeviceScannerReadImageBlockMetadata(jsonlookup, out szMetadataFile, out szThumbnailFile);
                        if (apistatus == TwainLocalScanner.ApiStatus.success)
                        {
                            apistatus = DeviceScannerGetSession(out szSession);
                            blSuccess = ipc.Write
                             (
                                 "{" +
                                 "\"status\":\"" + apistatus + "\"," +
                                 "\"meta\":\"" + szMetadataFile + "\"," +
                                 "\"thumbnailFile\":\"" + szThumbnailFile + "\"" +
                                 (!string.IsNullOrEmpty(szSession) ? "," + szSession : "") +
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

                    case "releaseImageBlocks":
                        apistatus = DeviceScannerReleaseImageBlocks(jsonlookup, out szSession);
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

                    case "setTwainDirectOptions":
                        apistatus = DeviceScannerSetTwainDirectOptions(jsonlookup, out swordtask, ref blSetAppCapabilities);
                        if (apistatus == TwainLocalScanner.ApiStatus.success)
                        {
                            if (string.IsNullOrEmpty(swordtask.GetTaskReply()))
                            {
                                blSuccess = ipc.Write
                                (
                                    "{" +
                                    "\"status\":\"" + apistatus + "\"" +
                                    "}"
                                );
                            }
                            else
                            {
                                blSuccess = ipc.Write
                                (
                                    "{" +
                                    "\"status\":\"" + apistatus + "\"," +
                                    "\"taskReply\":" + swordtask.GetTaskReply() +
                                    "}"
                                );
                            }
                        }
                        else
                        {
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\"," +
                                "\"exception\":\"" + swordtask.GetException() + "\"," +
                                "\"jsonKey\":\"" + swordtask.GetJsonExceptionKey() + "\"" +
                                "}"
                            );
                        }
                        if (!blSuccess)
                        {
                            TWAINWorkingGroup.Log.Info("IPC channel disconnected...");
                            blRunning = false;
                        }
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


        // Private Methods: CreatePdfRaster
        #region Private Methods: CreatePdfRaster

        /// <summary>
        /// Create a PDF/raster from one of the following:
        /// - bitonal uncompressed
        /// - bitonal group4
        /// - grayscale uncompressed
        /// - grayscale jpeg
        /// - color uncompressed
        /// - color jpeg
        /// </summary>
        /// <param name="a_szPrdFile">file to store the pdf/raster</param>
        /// <param name="a_abImage">raw image data to wrap</param>
        /// <param name="a_iImageOffset">byte offset into the image</param>
        /// <param name="a_twpt">bitonal, grayscale or color</param>
        /// <param name="a_twcp">none, group4 or jpeg</param>
        /// <param name="a_u32Resolution">dots per inch</param>
        /// <param name="a_u32Width">width in pixels</param>
        /// <param name="a_u32Height">height in pixels</param>
        /// <returns></returns>
        private bool CreatePdfRaster
        (
            string a_szPdfRasterFile,
            byte[] a_abImage,
            int a_iImageOffset,
            TWAIN.TWPT a_twpt,
            TWAIN.TWCP a_twcp,
            UInt32 a_u32Resolution,
            UInt32 a_u32Width,
            UInt32 a_u32Height
        )
        {
            bool blSuccess = true;
            TwainDirectSupport.PdfRaster.RasterPixelFormat rasterpixelformat;
            TwainDirectSupport.PdfRaster.RasterCompression rastercompression;
            PdfRaster pdfraster = new PdfRaster();
            PdfRaster.t_OS os;

            // Convert the pixel type...
            switch (a_twpt)
            {
                default:
                    TWAINWorkingGroup.Log.Error("Unsupported pixel type: " + a_twpt);
                    return (false);
                case TWAIN.TWPT.BW: rasterpixelformat = TwainDirectSupport.PdfRaster.RasterPixelFormat.PDFRAS_BITONAL; break;
                case TWAIN.TWPT.GRAY: rasterpixelformat = TwainDirectSupport.PdfRaster.RasterPixelFormat.PDFRAS_GRAYSCALE; break;
                case TWAIN.TWPT.RGB: rasterpixelformat = TwainDirectSupport.PdfRaster.RasterPixelFormat.PDFRAS_RGB; break;
            }

            // Convert the compression...
            switch (a_twcp)
            {
                default:
                    TWAINWorkingGroup.Log.Error("Unsupported compression: " + a_twcp);
                    return (false);
                case TWAIN.TWCP.NONE: rastercompression = TwainDirectSupport.PdfRaster.RasterCompression.PDFRAS_UNCOMPRESSED; break;
                case TWAIN.TWCP.GROUP4: rastercompression = TwainDirectSupport.PdfRaster.RasterCompression.PDFRAS_CCITTG4; break;
                case TWAIN.TWCP.JPEG: rastercompression = TwainDirectSupport.PdfRaster.RasterCompression.PDFRAS_JPEG; break;
            }

            // Create the file...
            try
            {
                using (BinaryWriter binarywriter = new BinaryWriter(File.Create(a_szPdfRasterFile)))
                {
                    // Set up our worker functions...
                    os = new PdfRaster.t_OS();
                    os.allocsys = PdfRaster.pd_alloc_sys_new(os);
                    os.writeout = PdfRasterOutputWriter;
                    os.writeoutcookie = binarywriter;

                    // Construct a raster PDF encoder
                    object enc = pdfraster.pd_raster_encoder_create(TwainDirectSupport.PdfRaster.PdfRasterConst.PDFRAS_API_LEVEL, os);
                    PdfRaster.pd_raster_set_creator(enc, "TWAIN Direct on TWAIN v1.0");

                    // Create the page (we only ever have one)...
                    PdfRaster.pd_raster_set_resolution(enc, a_u32Resolution, a_u32Resolution);
                    pdfraster.pd_raster_encoder_start_page(enc, rasterpixelformat, rastercompression, (int)a_u32Width);
                    pdfraster.pd_raster_encoder_write_strip(enc, (int)a_u32Height, a_abImage, (UInt32)a_iImageOffset, (UInt32)(a_abImage.Length - a_iImageOffset));
                    pdfraster.pd_raster_encoder_end_page(enc);

                    // The document is complete
                    pdfraster.pd_raster_encoder_end_document(enc);

                    // clean up
                    pdfraster.pd_raster_encoder_destroy(enc);
                }
            }
            catch (Exception exception)
            {
                TWAINWorkingGroup.Log.Error("unable to open %s for writing: " + a_szPdfRasterFile);
                TWAINWorkingGroup.Log.Error(exception.Message);
                blSuccess = false;
            }

            // All done...
            return (blSuccess);
        }

        /// <summary>
        /// Write a blob of data to disk...
        /// </summary>
        /// <param name="a_abData">the data</param>
        /// <param name="a_u32Offset">the byte offset into the data</param>
        /// <param name="a_u32Length">the amount of data from the offset to write</param>
        /// <param name="a_object">our binary writer object</param>
        /// <returns>the number of bytes written</returns>
        private long PdfRasterOutputWriter(byte[] a_abData, long a_lOffset, long a_lLength, object a_object)
        {
            // Get our object...
            BinaryWriter binarywriter = (BinaryWriter)a_object;

            // Validate...
            if ((a_abData == null) || (a_lLength == 0))
            {
                return (0);
            }

            // Write...
            binarywriter.Write(a_abData, (int)a_lOffset, (int)a_lLength);

            // All done...
            return (a_lLength);
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
            TWAINCSToolkit.STS a_sts,
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
            string szImageFile;
            TWAIN.STS sts;
            TWAIN twain;

            // We're processing end of scan...
            if (a_bitmap == null)
            {
                TWAINWorkingGroup.Log.Info("ReportImage: no more images: " + a_szDg + " " + a_szDat + " " + a_szMsg + " " + a_sts);
                m_blCancel = false;
                SetEndOfJob(a_sts);
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
                    m_blCancel = false;
                    SetEndOfJob(sts);
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
            if (!Directory.Exists(m_szImagesFolder))
            {
                try
                {
                    Directory.CreateDirectory(m_szImagesFolder);
                }
                catch
                {
                    TWAINWorkingGroup.Log.Error("ReportImage: unable to create the image destination directory: " + m_szImagesFolder);
                    m_blCancel = false;
                    SetEndOfJob(TWAIN.STS.FILENOTFOUND);
                    return (TWAINCSToolkit.MSG.RESET);
                }
            }

            // Create a filename...
            m_iImageCount += 1;
            szFile = m_szImagesFolder + Path.DirectorySeparatorChar + "img" + m_iImageCount.ToString("D6");

            // Cleanup...
            if (File.Exists(szFile + ".pdf"))
            {
                try
                {
                    File.Delete(szFile + ".pdf");
                }
                catch
                {
                }
            }

            // Save as PDF/Raster...
            szImageFile = szFile + ".pdf";
            blSuccess = CreatePdfRaster
            (
                szImageFile,
                a_abImage,
                a_iImageOffset,
                (TWAIN.TWPT)twimageinfo.PixelType,
                (TWAIN.TWCP)twimageinfo.Compression,
                (UInt32)twimageinfo.XResolution.Whole,
                (UInt32)twimageinfo.ImageWidth,
                (UInt32)twimageinfo.ImageLength
            );
            if (!blSuccess)
            {
                TWAINWorkingGroup.Log.Error("ReportImage: unable to save the image file, " + szImageFile);
                m_blCancel = false;
                SetEndOfJob(TWAIN.STS.FILEWRITEERROR);
                return (TWAINCSToolkit.MSG.RESET);
            }

            // Compression...
            string szCompression;
            switch (twimageinfo.Compression)
            {
                default:
                    m_blCancel = false;
                    SetEndOfJob(TWAIN.STS.BADVALUE);
                    return (TWAINCSToolkit.MSG.RESET);
                case (ushort)TWAIN.TWCP.GROUP4:
                    szCompression = "group4";
                    break;
                case (ushort)TWAIN.TWCP.JPEG:
                    szCompression = "jpeg";
                    break;
                case (ushort)TWAIN.TWCP.NONE:
                    szCompression = "none";
                    break;
            }

            // Figure out the pixel format...
            string szPixelFormat;
            switch (twimageinfo.PixelType)
            {
                default:
                    m_blCancel = false;
                    SetEndOfJob(TWAIN.STS.BADVALUE);
                    return (TWAINCSToolkit.MSG.RESET);
                case (short)TWAIN.TWPT.BW:
                    szPixelFormat = "bw1";
                    break;
                case (short)TWAIN.TWPT.GRAY:
                    szPixelFormat = "gray8";
                    break;
                case (short)TWAIN.TWPT.RGB:
                    szPixelFormat = "rgb24";
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
                if (m_blExtImageInfo)
                {
                    for (uu = 0; uu < twextimageinfo.NumInfos; uu++)
                    {
                        twextimageinfo.Get(uu, ref twinfo);
                        if (twinfo.InfoId == (ushort)TWAIN.TWEI.PAGESIDE)
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

            // Create the TWAIN Diect metadata...
            string szMeta = "";

            // TWAIN Direct metadata.address begin...
            szMeta += "\"metadata\":{";

            // TWAIN Direct metadata.address begin...
            szMeta += "\"address\":{";

            // Imagecount (counts images)...
            szMeta += "\"imageNumber\":" + m_iImageCount + ",";

            // Sheetcount (counts sheets, including ones lost to blank image dropout)...
            szMeta += "\"sheetNumber\":" + "1" + ",";

            // The image came from a flatbed or a feederFront or whatever...
            szMeta += "                \"source\": \"" + szSource + "\"\n";

            // TWAIN Direct metadata.address end...
            szMeta += "            },\n";

            // TWAIN Direct metadata.image begin...
            szMeta += "            \"image\": {\n";

            // Add compression...
            szMeta += "                \"compression\": \"" + szCompression + "\",\n";

            // Add pixel format...
            szMeta += "                \"pixelFormat\": \"" + szPixelFormat + "\",\n";

            // Add height...
            szMeta += "                \"pixelHeight\": " + twimageinfo.ImageLength + ",\n";

            // X-offset...
            szMeta += "                \"pixelOffsetX\": " + "0" + ",\n";

            // Y-offset...
            szMeta += "                \"pixelOffsetY\": " + "0" + ",\n";

            // Add width...
            szMeta += "                \"pixelWidth\": " + twimageinfo.ImageWidth + ",\n";

            // Add resolution...
            szMeta += "                \"resolution\": " + twimageinfo.XResolution.Whole + ",\n";

            // Add size...
            FileInfo fileinfo = new FileInfo(szImageFile);
            szMeta += "                \"size\": " + fileinfo.Length + "\n";

            // TWAIN Direct metadata.image end...
            szMeta += "            },\n";

            // TWAIN Direct metadata.address begin...
            szMeta += "            \"imageBlock\": {\n";

            // Imagecount (counts images)...
            szMeta += "                \"imageNumber\": " + m_iImageCount + ",\n";

            // Segmentcount (long document or huge document)...
            szMeta += "                \"imagePart\": " + "1" + ",\n";

            // Segmentlast (long document or huge document)...
            szMeta += "                \"moreParts\": " + "false" + "\n";

            // TWAIN Direct metadata.address end...
            szMeta += "            },\n";

            // Open SWORD.metadata.status...
            szMeta += "            \"status\": {\n";

            // Add the status...
            szMeta += "                \"success\": true\n";

            // TWAIN Direct metadata.status end...
            szMeta += "            }\n";

            // TWAIN Direct metadata end...
            szMeta += "        }\n";

            // Save the metadata to disk...
            try
            {
                File.WriteAllText(szFile + ".meta", szMeta);
                TWAINWorkingGroup.Log.Info("ReportImage: saved " + szFile + ".meta");
            }
            catch
            {
                TWAINWorkingGroup.Log.Error("ReportImage: unable to save the metadata file...");
                m_blCancel = false;
                SetEndOfJob(TWAIN.STS.FILEWRITEERROR);
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
                    m_blCancel = false;
                    SetEndOfJob(sts);
                    return (TWAINCSToolkit.MSG.STOPFEEDER);
                }
            }

            // All done...
            return (TWAINCSToolkit.MSG.ENDXFER);
        }

        /// <summary>
        /// Remove the end of job file...
        /// </summary>
        private void ClearEndOfJob()
        {
            string szEndOfJob = Path.Combine(m_szImagesFolder, "endOfJob");
            if (File.Exists(szEndOfJob))
            {
                File.Delete(szEndOfJob);
            }
        }

        /// <summary>
        /// Set end of job file with a status...
        /// </summary>
        /// <param name="a_sts">status of end of job</param>
        private void SetEndOfJob(TWAINCSToolkit.STS a_sts)
        {
            string szEndOfJob = Path.Combine(m_szImagesFolder, "endOfJob");
            if (!File.Exists(szEndOfJob))
            {
                File.WriteAllText
                (
                    szEndOfJob,
                    "{" +
                    "\"status\":\"" + a_sts + "\"" +
                    "}"
                );
            }
        }

        /// <summary>
        /// Set end of job file with a status...
        /// </summary>
        /// <param name="a_sts">status of end of job</param>
        private void SetEndOfJob(TWAIN.STS a_sts)
        {
            string szEndOfJob = Path.Combine(m_szImagesFolder, "endOfJob");
            if (!File.Exists(szEndOfJob))
            {
                File.WriteAllText
                (
                    szEndOfJob,
                    "{" +
                    "\"status\":\"" + a_sts + "\"" +
                    "}"
                );
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

            // Init stuff...
            a_szSession = "";

            // Validate...
            if ((m_twaincstoolkit == null) || (m_szTwainDriverIdentity == null))
            {
                return (TwainLocalScanner.ApiStatus.invalidSessionId);
            }

            // Close the driver...
            szStatus = "";
            m_twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_CLOSEDS", ref m_szTwainDriverIdentity, ref szStatus);
            m_twaincstoolkit.Cleanup();
            m_twaincstoolkit = null;
            m_szTwainDriverIdentity = null;

            // Build the reply...
            DeviceScannerGetSession(out a_szSession);

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
            TWAINCSToolkit.STS sts;

            // Init stuff...
            a_szSession = "";

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

            // Build the identity...
            m_szScanner = a_jsonlookup.Get("scanner");
            if (m_szScanner.Contains(" | "))
            {
                m_szScanner = m_szScanner.Split(new string[] { " | " }, StringSplitOptions.RemoveEmptyEntries)[0];
            }
            if (TwainLocalScanner.GetPlatform() == TwainLocalScanner.Platform.WINDOWS)
            {
                m_szTwainDriverIdentity = "0,0,0,USA,USA, ,0,0,0xFFFFFFFF, , ," + m_szScanner;
            }
            else if (TwainLocalScanner.GetPlatform() == TwainLocalScanner.Platform.MACOSX)
            {
                m_szTwainDriverIdentity = "0,0,0,USA,USA, ,0,0,0xFFFFFFFF, , ," + m_szScanner;
            }
            else
            {
                m_szTwainDriverIdentity = "1,0,0,USA,USA, ,0,0,0xFFFFFFFF, , ," + m_szScanner;
            }

            // Open the driver...
            szStatus = "";
            sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_OPENDS", ref m_szTwainDriverIdentity, ref szStatus);
            if (sts != TWAINCSToolkit.STS.SUCCESS)
            {
                return (TwainLocalScanner.ApiStatus.newSessionNotAllowed);
            }

            // Make sure the images folder is empty...
            if (Directory.Exists(m_szImagesFolder))
            {
                Directory.Delete(m_szImagesFolder,true);
            }
            Directory.CreateDirectory(m_szImagesFolder);

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

            // If we have no images, then check for end of job...
            m_blEndOfJob = false;
            try
            {
                aszFiles = Directory.GetFiles(m_szImagesFolder, "endOfJob");
            }
            catch
            {
                aszFiles = null;
            }
            if ((szImageBlocks == "") && (aszFiles != null) && (aszFiles.Length > 0))
            {
                m_blEndOfJob = true;
            }

            // Build the reply.  Note that we have this kind of code in three places
            // in the solution.  This is the lowest "level", where we generate the
            // data that will be sent to TwainDirectScanner, so it's not really in
            // the final form, though it's close.  endOfJob, for instance, is our
            // own addition.
            a_szSession =
                "\"endOfJob\":" + m_blEndOfJob.ToString().ToLower() + "," +
                "\"session\":{";

            // Tack on the image blocks, if we have any...
            if (!string.IsNullOrEmpty(szImageBlocks))
            {
                a_szSession += "\"imageBlocks\":[" + szImageBlocks + "]" + (string.IsNullOrEmpty(m_szTwainDirectOptions) ? "" : ",");
            }

            // Tack on the task, if we have one...
            if (!string.IsNullOrEmpty(m_szTwainDirectOptions))
            {
                a_szSession += "\"taskReply\":" + m_szTwainDirectOptions;
            }

            // End of the session object...
            a_szSession += "}";

            // All done...
            return (TwainLocalScanner.ApiStatus.success);
        }

        /// <summary>
        /// Return the full path to the requested image block, we also get the
        /// file to the metadata, but the caller decides if we send this back
        /// or not based on the value of "withMetadata"...
        /// </summary>
        /// <param name="a_jsonlookup">data for the open</param>
        /// <param name="a_szImageFile">file containing the image data</param>
        /// <param name="a_szMetadataFile">file containing the metadata</param>
        /// <returns>status of the call</returns>
        private TwainLocalScanner.ApiStatus DeviceScannerReadImageBlock
        (
            JsonLookup a_jsonlookup,
            out string a_szImageFile,
            out string a_szMetadataFile
        )
        {
            // Build the filename...
            int iImageBlock = int.Parse(a_jsonlookup.Get("imageBlockNum"));
            a_szImageFile = Path.Combine(m_szImagesFolder, "img" + iImageBlock.ToString("D6"));
            a_szImageFile = a_szImageFile.Replace("\\", "/");
            if (File.Exists(a_szImageFile + ".pdf"))
            {
                a_szImageFile += ".pdf";
            }
            else
            {
                TWAINWorkingGroup.Log.Error("Image not found: " + a_szImageFile);
                a_szMetadataFile = "";
                return (TwainLocalScanner.ApiStatus.invalidImageBlockNumber);
            }

            // Build the metadata filename, if we don't have one, we have a problem...
            a_szMetadataFile = Path.Combine(m_szImagesFolder, "img" + iImageBlock.ToString("D6") + ".meta");
            a_szMetadataFile = a_szMetadataFile.Replace("\\", "/");
            if (!File.Exists(a_szMetadataFile))
            {
                TWAINWorkingGroup.Log.Error("Image metadata not found: " + a_szMetadataFile);
                a_szMetadataFile = "";
                return (TwainLocalScanner.ApiStatus.invalidImageBlockNumber);
            }

            // All done...
            return (TwainLocalScanner.ApiStatus.success);
        }

        /// <summary>
        /// Return the TWAIN Direct metadata for this image block, note that we
        /// generate the metadata file last, because it's the trigger that says
        /// that this image is complete...
        /// </summary>
        /// <param name="a_jsonlookup">data for the open</param>
        /// <param name="a_szMetadataFile">file containing the metadata</param>
        /// <param name="a_szThumbnailFile">optional file containing the thumbnail</param>
        /// <returnsstatus of the call</returns>
        private bool ThumbnailCallback()
        {
            return false;
        }
        private TwainLocalScanner.ApiStatus DeviceScannerReadImageBlockMetadata
        (
            JsonLookup a_jsonlookup,
            out string a_szMetadataFile,
            out string a_szThumbnailFile
        )
        {
            int iImageBlock;
            string szPdf;

            // Get our imageblock number...
            iImageBlock = int.Parse(a_jsonlookup.Get("imageBlockNum"));

            // Generate a thumbnail...
            a_szThumbnailFile = "";
            if (a_jsonlookup.Get("withThumbnail") == "true")
            {
                long ssww;
                long ddww;
                long hh;
                bool blSuccess;
                byte[] abImage;
                PdfRaster.RasterPixelFormat rasterpixelformat;
                PdfRaster.RasterCompression rastercompression;
                long lResolution;
                long lWidth;
                long lHeight;
                Bitmap bitmap = null;
                BitmapData bitmapdata;

                // This is the file we'll use...
                a_szThumbnailFile = Path.Combine(m_szImagesFolder, "img" + iImageBlock.ToString("D6") + "_thumbnail.pdf");
                a_szThumbnailFile = a_szThumbnailFile.Replace("\\", "/");

                // The name of our image file...
                szPdf = Path.Combine(m_szImagesFolder, "img" + iImageBlock.ToString("D6") + ".pdf");
                szPdf = szPdf.Replace("\\", "/");

                // Convert the image to a thumbnail...
                PdfRaster.GetImage(szPdf, out abImage, out rasterpixelformat, out rastercompression, out lResolution, out lWidth, out lHeight);
                using (var memorystream = new MemoryStream(abImage))
                {
                    // Get the thumbnail, fix so all thumbnails have the same height
                    // we want to preserve the aspect ratio...
                    bitmap = new Bitmap(memorystream);
                    Image.GetThumbnailImageAbort myCallback = new Image.GetThumbnailImageAbort(ThumbnailCallback);
                    int iWidth = (64 * (int)bitmap.HorizontalResolution) / (int)bitmap.VerticalResolution;
                    Image imageThumbnail = bitmap.GetThumbnailImage(iWidth, 64, myCallback, IntPtr.Zero);
                    bitmap = new Bitmap(imageThumbnail);

                    // Convert it from 32bit rgb to 24bit rgb...
                    bitmapdata = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),ImageLockMode.ReadOnly,bitmap.PixelFormat);
                    byte[] abImageBgr = new byte[bitmapdata.Stride * bitmap.Height];
                    System.Runtime.InteropServices.Marshal.Copy(bitmapdata.Scan0, abImageBgr, 0, abImageBgr.Length);
                    int iNewStride = (bitmapdata.Stride - (bitmapdata.Stride / 4)); // lose the A from BGRA
                    iNewStride = (iNewStride + 3) & ~3; // align the new stride on a 4-byte boundary
                    abImage = new byte[iNewStride * bitmapdata.Height];
                    for (hh = 0; hh < bitmapdata.Height; hh++)
                    {
                        long lSsRow = (hh * bitmapdata.Stride);
                        long lDdRow = (hh * iNewStride);
                        for (ssww = ddww = 0; ssww < bitmapdata.Stride; ddww += 3, ssww += 4)
                        {
                            abImage[lDdRow + ddww + 0] = (byte)abImageBgr[lSsRow + ssww + 2]; // R
                            abImage[lDdRow + ddww + 1] = (byte)abImageBgr[lSsRow + ssww + 1]; // G
                            abImage[lDdRow + ddww + 2] = (byte)abImageBgr[lSsRow + ssww + 0]; // B
                        }
                    }

                    // PDF/raster it...
                    blSuccess = CreatePdfRaster(a_szThumbnailFile, abImage, 0, TWAIN.TWPT.RGB, TWAIN.TWCP.NONE, (uint)bitmap.HorizontalResolution, (uint)bitmap.Width, (uint)bitmap.Height);
                }
            }

            // Build the metadata filename, if we don't have one, we have a problem...
            a_szMetadataFile = Path.Combine(m_szImagesFolder, "img" + iImageBlock.ToString("D6") + ".meta");
            a_szMetadataFile = a_szMetadataFile.Replace("\\", "/");
            if (!File.Exists(a_szMetadataFile))
            {
                TWAINWorkingGroup.Log.Error("Image metadata not found: " + a_szMetadataFile);
                a_szMetadataFile = "";
                a_szThumbnailFile = "";
                return (TwainLocalScanner.ApiStatus.invalidImageBlockNumber);
            }

            // All done...
            return (TwainLocalScanner.ApiStatus.success);
        }

        /// <summary>
        /// Return the requested file...
        /// </summary>
        /// <param name="a_jsonlookup">data for the open</param>
        /// <param name="a_szSession">the session data</param>
        /// <returns>a twain local status</returns>
        private TwainLocalScanner.ApiStatus DeviceScannerReleaseImageBlocks(JsonLookup a_jsonlookup, out string a_szSession)
        {
            string szFile;
            int ii;
            int iImageBlockNum;
            int iLastImageBlockNum;

            // Init stuff...
            a_szSession = "";

            // Get the endpoints (inclusive)...
            iImageBlockNum = int.Parse(a_jsonlookup.Get("imageBlockNum"));
            iLastImageBlockNum = int.Parse(a_jsonlookup.Get("lastImageBlockNum"));

            // Loopy...
            for (ii = iImageBlockNum; ii <= iLastImageBlockNum; ii++)
            {
                // Build the filename...
                szFile = Path.Combine(m_szImagesFolder, "img" + ii.ToString("D6"));
                if (File.Exists(szFile + ".meta"))
                {
                    File.Delete(szFile + ".meta");
                }
                if (File.Exists(szFile + ".txt"))
                {
                    File.Delete(szFile + ".txt");
                }
                if (File.Exists(szFile + ".pdf"))
                {
                    File.Delete(szFile + ".pdf");
                }
            }

            // Build the reply...
            DeviceScannerGetSession(out a_szSession);

            // All done...
            return (TwainLocalScanner.ApiStatus.success);
        }

        /// <summary>
        /// Process a TWAIN Direct task...
        /// </summary>
        /// <param name="a_jsonlookup">data for the task</param>
        /// <param name="a_swordtask">the result of the task</param>
        /// <returns>a twain local status</returns>
        private TwainLocalScanner.ApiStatus DeviceScannerSetTwainDirectOptions(JsonLookup a_jsonlookup, out SwordTask a_swordtask, ref bool a_blSetAppCapabilities)
        {
            bool blSuccess;
            string szTask;
            Sword sword;

            // Init stuff...
            a_swordtask = new SwordTask();

            // Create our object...
            sword = new Sword(m_twaincstoolkit);

            // Convert the base64 task into a string...
            szTask = a_jsonlookup.GetJson("task");

            // Run our task...
            blSuccess = sword.BatchMode(m_szScanner, szTask, true, ref a_swordtask, ref a_blSetAppCapabilities);
            if (!blSuccess)
            {
                return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
            }

            // Success, make a note of the twainDirectOptions...
            m_szTwainDirectOptions = a_swordtask.GetTaskReply();

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
            TWAINCSToolkit.STS sts;

            // Init stuff...
            m_blCancel = false;
            m_iImageCount = 0;
            a_szSession = "";
            ClearEndOfJob();

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

                // Memory transfer...
                szStatus = "";
                szCapability = "ICAP_XFERMECH,TWON_ONEVALUE,TWTY_UINT16,2";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("Action: we can't set ICAP_XFERMECH to TWSX_MEMORY");
                    return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                }

                // No UI...
                szStatus = "";
                szCapability = "CAP_INDICATORS,TWON_ONEVALUE,TWTY_BOOL,0";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error("Action: we can't set CAP_INDICATORS to FALSE");
                    return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
                }

                // Ask for extended image info...
                m_blExtImageInfo = true;
                szStatus = "";
                szCapability = "ICAP_EXTIMAGEINFO,TWON_ONEVALUE,TWTY_BOOL,1";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                if (sts != TWAINCSToolkit.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Warn("Action: we can't set ICAP_EXTIMAGEINFO to TRUE");
                    m_blExtImageInfo = false;
                }
            }

            // Start scanning (no UI)...
            szStatus = "";
            szUserInterface = "0,0";
            sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_USERINTERFACE", "MSG_ENABLEDS", ref szUserInterface, ref szStatus);
            if (sts != TWAINCSToolkit.STS.SUCCESS)
            {
                TWAINWorkingGroup.Log.Info("Action: MSG_ENABLEDS failed");
                return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
            }

            // Build the reply...
            DeviceScannerGetSession(out a_szSession);

            // All done...
            if (sts == TWAINCSToolkit.STS.SUCCESS)
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
            string szStatus;
            string szUserinterface;
            TWAINCSToolkit.STS sts;

            // Init stuff...
            a_szSession = "";

            // Validate...
            if (m_twaincstoolkit == null)
            {
                return (TwainLocalScanner.ApiStatus.invalidSessionId);
            }

            // We're done scanning, so bail...
            if (m_blEndOfJob)
            {
                sts = TWAINCSToolkit.STS.SUCCESS;
                if (m_twaincstoolkit.GetState() == 5)
                {
                    szStatus = "";
                    szUserinterface = "0,0";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_USERINTERFACE", "MSG_DISABLEDS", ref szUserinterface, ref szStatus);
                }
            }

            // We're still scanning, try to end gracefully...
            else
            {
                szStatus = "";
                szUserinterface = "0,0";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_USERINTERFACE", "MSG_STOPFEEDER", ref szUserinterface, ref szStatus);
            }

            // Build the reply...
            DeviceScannerGetSession(out a_szSession);

            // All done...
            if (sts == TWAINCSToolkit.STS.SUCCESS)
            {
                return (TwainLocalScanner.ApiStatus.success);
            }

            // Oh well, we'll try to abort...
            m_blCancel = true;
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
        /// The TWAIN TW_IDENTITY.ProductName of the scanner we're using...
        /// </summary>
        private string m_szScanner;

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
        /// Process id we're communicating with...
        /// </summary>
        private int m_iPid;

        /// <summary>
        /// A flag to help us abort a scan with MSG_RESET when
        /// MSG_STOPFEEDER fails to work...
        /// </summary>
        private bool m_blCancel;

        /// <summary>
        /// True if we have support for DAT_EXTIMAGEINFO...
        /// </summary>
        private bool m_blExtImageInfo;

        /// <summary>
        /// Count of images for each TwainStartCapturing call, the
        /// first image is always 1...
        /// </summary>
        private int m_iImageCount;

        /// <summary>
        /// End of job detected...
        /// </summary>
        private bool m_blEndOfJob;

        /// <summary>
        /// We're scanning from a flatbed...
        /// </summary>
        private bool m_blFlatbed;

        /// <summary>
        /// We're scanning duplex (front and rear) off an automatic document feeder (ADF)...
        /// </summary>
        private bool m_blDuplex;

        /// <summary>
        /// The capturingOptions in the session object.  We need to persist these
        /// when we move from a ready state to anything else.  The way TWAIN Local is designed
        /// we're guaranteed to set these values before we get out of the ready state.
        /// Technically, the twainDirectOptions are part of the session object and not
        /// the capturingOptions, but since they go hand-in-hand, this is as good a
        /// place as any to declare them.
        /// </summary>
        private string m_szTwainDirectOptions;

        /// <summary>
        /// The delegate that lets us run stuff in the main GUI thread on Windows,
        /// and some anonymous data that is sent along with it.  We're also holding
        /// onto the handle for the anonymous data...
        /// </summary>
        private TWAINCSToolkit.RunInUiThreadDelegate m_runinuithreaddelegate;
        private object m_objectRunInUiThread;
        private IntPtr m_intptrHwnd;

        #endregion
    }
}
