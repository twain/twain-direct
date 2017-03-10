///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirectOnSane.TwainLocalToSane
//
//  Map TWAIN Local calls to SANE calls...
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
using System.Runtime.InteropServices;
using System.Text;
using TwainDirectSupport;

namespace TwainDirectOnSane
{
    /// <summary>
    /// Map TWAIN Local calls to SANE.  This seems like the best way to make sure
    /// we get all the needed data down to this level, however it means that
    /// we have knowledge of our caller at this level, so there will be some
    /// replication if we add support for another communication manager...
    /// </summary>
    public sealed class TwainLocalOnSane : IDisposable
    {
        // Public Methods: Run
        #region Public Methods: Run

        /// <summary>
        /// Init stuff...
        /// </summary>
        public TwainLocalOnSane(string a_szWriteFolder, string a_szIpc, int a_iPid)
        {
            // Remember this stuff...
            m_szWriteFolder = a_szWriteFolder;
            m_szImagesFolder = Path.Combine(m_szWriteFolder, "images");
            m_szIpc = a_szIpc;
            m_iPid = a_iPid;

            // Init stuff...
            m_iWidth = 0;
            m_iHeight = 0;
            m_iOffsetX = 0;
            m_iOffsetY = 0;
            m_szNumberOfSheets = "";
            m_szPixelFormat = "";
            m_szResolution = "";
            //m_blFlatbed = false;
            //m_blDuplex = false;

            // Make the compiler happy until we sort this out...
            m_szScanner = "";
        }

        /// <summary>
        /// Destructor...
        /// </summary>
        ~TwainLocalOnSane()
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
            string szMeta;
            string szSession;
            string szImageBlock;
            Ipc ipc;
            SwordTask swordtask;
            TwainLocalScanner.ApiStatus apistatus;

            // Pipe mode starting...
            TwainDirectSupport.Log.Info("IPC mode starting...");

            // Set up communication with our server process...
            ipc = new Ipc(m_szIpc, false);
            ipc.MonitorPid(m_iPid);
            ipc.Connect();

            // TBD (hack)
            string szCapabilities = Sword.SaneListDrivers();
            TwainDirectSupport.Log.Info("TwainListDrivers: " + szCapabilities);
            JsonLookup jsonlookupCapabilities = new JsonLookup();
            jsonlookupCapabilities.Load(szCapabilities, out lResponseCharacterOffset);
            m_szTwainDriverIdentity = jsonlookupCapabilities.Get("scanners[0].sane");
            m_szNumberOfSheets = jsonlookupCapabilities.Get("scanners[0].numberOfSheets[1]");
            m_szPixelFormat = jsonlookupCapabilities.Get("scanners[0].pixelFormat[0]");
            m_szResolution = jsonlookupCapabilities.Get("scanners[0].resolution[0]");

            m_iOffsetX = 0;
            m_iOffsetY = 0;
            m_iWidth = 0;
            m_iHeight = 0;
            int.TryParse(jsonlookupCapabilities.Get("scanners[0].offsetX[0]"), out m_iOffsetX);
            int.TryParse(jsonlookupCapabilities.Get("scanners[0].offsetY[0]"), out m_iOffsetY);
            int.TryParse(jsonlookupCapabilities.Get("scanners[0].width[1]"), out m_iWidth);
            int.TryParse(jsonlookupCapabilities.Get("scanners[0].height[1]"), out m_iHeight);

            // Loopy...
            while (blRunning)
            {
                // Read a command...
                szJson = ipc.Read();
                if (szJson == null)
                {
                    TwainDirectSupport.Log.Info("IPC channel disconnected...");
                    break;
                }

                // Log it...
                //TwainDirectSupport.Log.Info("");
                //TwainDirectSupport.Log.Info(szJson);

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
                                "{\n" +
                                "    \"status\": \"" + apistatus + "\",\n" +
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
                            TwainDirectSupport.Log.Info("IPC channel disconnected...");
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
                            TwainDirectSupport.Log.Info("IPC channel disconnected...");
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
                                "\"status\": \"" + apistatus + "\"," +
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
                            TwainDirectSupport.Log.Info("IPC channel disconnected...");
                            blRunning = false;
                        }
                        break;

                    case "readImageBlock":
                        apistatus = DeviceScannerReadImageBlock(jsonlookup, out szImageBlock);
                        if (apistatus == TwainLocalScanner.ApiStatus.success)
                        {
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\"," +
                                "\"imageBlock\":\"" + szImageBlock + "\"" +
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
                            TwainDirectSupport.Log.Info("IPC channel disconnected...");
                            blRunning = false;
                        }
                        break;

                    case "readImageBlockMetadata":
                        apistatus = DeviceScannerReadImageBlockMetadata(jsonlookup, out szMeta);
                        if (apistatus == TwainLocalScanner.ApiStatus.success)
                        {
                            apistatus = DeviceScannerGetSession(out szSession);
                            blSuccess = ipc.Write
                             (
                                 "{" +
                                 "\"status\":\"" + apistatus + "\"," +
                                 "\"meta\":\"" + szMeta + "\"" +
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
                            TwainDirectSupport.Log.Info("IPC channel disconnected...");
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
                            TwainDirectSupport.Log.Info("IPC channel disconnected...");
                            blRunning = false;
                        }
                        break;

                    case "sendTask":
                        apistatus = DeviceScannerSendTask(jsonlookup, out swordtask, ref blSetAppCapabilities);
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
                                string szTaskReply = swordtask.GetTaskReply();
                                blSuccess = ipc.Write
                                (
                                    "{" +
                                    "\"status\":\"" + apistatus + "\"," +
                                    "\"taskReply\":" + szTaskReply +
                                    "}"
                                );
                            }
                        }
                        else
                        {
                            blSuccess = ipc.Write
                            (
                                "{" +
                                "\"status\":\"" + apistatus + "\",\n" +
                                "\"exception\":\"" + swordtask.GetException() + "\"," +
                                "\"jsonKey\":\"" + swordtask.GetJsonExceptionKey() + "\"" +
                                "}"
                            );
                        }
                        if (!blSuccess)
                        {
                            TwainDirectSupport.Log.Info("IPC channel disconnected...");
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
                            TwainDirectSupport.Log.Info("IPC channel disconnected...");
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
                            TwainDirectSupport.Log.Info("IPC channel disconnected...");
                            blRunning = false;
                        }
                        break;
                }
            }

            // All done...
            TwainDirectSupport.Log.Info("IPC mode completed...");
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
            string a_szMode,
            UInt32 a_u32Resolution,
            UInt32 a_u32Width,
            UInt32 a_u32Height
        )
        {
            bool blSuccess = true;
            PdfRasterWriter.Writer.PdfRasterPixelFormat rasterpixelformat;
            PdfRasterWriter.Writer.PdfRasterCompression rastercompression;
            PdfRaster pdfraster = new PdfRaster();
            PdfRaster.t_OS os;

            // Convert the pixel type...
            switch (a_szMode)
            {
                default:
                    TwainDirectSupport.Log.Error("Unsupported mode: " + a_szMode);
                    return (false);
                case "P4": rasterpixelformat = PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_BITONAL; break;
                case "P5": rasterpixelformat = PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_GRAYSCALE; break;
                case "P6": rasterpixelformat = PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_RGB; break;
            }

            // We only support none...
            rastercompression = PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_UNCOMPRESSED;

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

                    // TODO: change this code to use .NET interface

                    object enc = pdfraster.pd_raster_encoder_create(PdfRasterWriter.Writer.PdfRasterConst.PDFRASWR_API_LEVEL, os);
                    PdfRaster.pd_raster_set_creator(enc, "TWAIN Direct on SANE v1.0");

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
                TwainDirectSupport.Log.Error("unable to open %s for writing: " + a_szPdfRasterFile);
                TwainDirectSupport.Log.Error(exception.Message);
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
        private void SetEndOfJob(string a_szStatus)
        {
            string szEndOfJob = Path.Combine(m_szImagesFolder, "endOfJob");
            if (!File.Exists(szEndOfJob))
            {
                File.WriteAllText
                (
                    szEndOfJob,
                    "{" +
                    "\"status\":\"" + a_szStatus + "\"" +
                    "}"
                );
            }
        }

        #endregion


        // Private Methods: TWAIN Direct Client-Scanner API
        #region Private Methods: TWAIN Direct Client-Scanner API

        /// <summary>
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        internal void Dispose(bool a_blDisposing)
        {
            // Free managed resources...
            if (a_blDisposing)
            {
                if (m_processScanImage != null)
                {
                    m_processScanImage.Kill();
                    m_processScanImage.Dispose();
                    m_processScanImage = null;
                }
            }
        }

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
            // Init stuff...
            a_szSession = "";

            // Validate...
            if (m_szTwainDriverIdentity == null)
            {
                return (TwainLocalScanner.ApiStatus.invalidSessionId);
            }

            // Close the driver...
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
            if (m_szTwainDriverIdentity == null)
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
            m_blImageBlocksDrained = false;
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
                m_blImageBlocksDrained = true;
            }

            // Start the reply...
            a_szSession =
                "\"endOfJob\":" + m_blImageBlocksDrained.ToString().ToLower() + "," +
                "\"session\":{";

            // If we're end of job (which is also set when we're not capturing)
            // then return the imageBlocks or nothing, where nothing will be
            // interpreted as end of job...
            if (m_blImageBlocksDrained)
            {
                a_szSession +=
                    (!string.IsNullOrEmpty(szImageBlocks) ? "\"imageBlocks\":[" + szImageBlocks + "]" : "");
            }
            // Otherwise, always return the imageBlocks, even if they are empty,
            // because this tells the caller that more images could be coming...
            else
            {
                a_szSession +=
                    "\"imageBlocks\":[" + szImageBlocks + "]";
            }

            // Finish the reply...
            a_szSession +=
                "}";

            // All done...
            return (TwainLocalScanner.ApiStatus.success);
        }

        /// <summary>
        /// Return the full path to the requested image block...
        /// </summary>
        /// <param name="a_jsonlookup">data for the open</param>
        /// <param name="a_szImageBlock">file containing the image data</param>
        /// <returns>status of the call</returns>
        private TwainLocalScanner.ApiStatus DeviceScannerReadImageBlock(JsonLookup a_jsonlookup, out string a_szImageBlock)
        {
            // Build the filename...
            int iImageBlock = int.Parse(a_jsonlookup.Get("imageBlockNum"));
            a_szImageBlock = Path.Combine(m_szImagesFolder, "img" + iImageBlock.ToString("D6"));
            a_szImageBlock = a_szImageBlock.Replace("\\", "/");
            if (File.Exists(a_szImageBlock + ".pdf"))
            {
                a_szImageBlock += ".pdf";
            }
            else
            {
                TwainDirectSupport.Log.Error("Image not found: " + a_szImageBlock);
                return (TwainLocalScanner.ApiStatus.invalidImageBlockNumber);
            }

            // All done...
            return (TwainLocalScanner.ApiStatus.success);
        }

        /// <summary>
        /// Return the TWAIN Direct metadata for this image block...
        /// </summary>
        /// <param name="a_jsonlookup">data for the open</param>
        /// <param name="a_jsonlookup">file containing the metadata</param>
        /// <returnsstatus of the call</returns>
        private TwainLocalScanner.ApiStatus DeviceScannerReadImageBlockMetadata(JsonLookup a_jsonlookup, out string a_szMetadata)
        {
            // Build the filename...
            int iImageBlock = int.Parse(a_jsonlookup.Get("imageBlockNum"));
            a_szMetadata = Path.Combine(m_szImagesFolder, "img" + iImageBlock.ToString("D6") + ".meta");
            a_szMetadata = a_szMetadata.Replace("\\", "/");
            if (!File.Exists(a_szMetadata))
            {
                TwainDirectSupport.Log.Error("Image metadata not found: " + a_szMetadata);
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
                if (File.Exists(szFile + ".pnm"))
                {
                    File.Delete(szFile + ".pnm");
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
        private TwainLocalScanner.ApiStatus DeviceScannerSendTask(JsonLookup a_jsonlookup, out SwordTask a_swordtask, ref bool a_blSetAppCapabilities)
        {
            bool blSuccess;
            string szTask;
            Sword sword;

            // Init stuff...
            a_swordtask = new SwordTask();
            m_szScanImageArguments = "";

            // Create our object...
            sword = new Sword();

            // Grab our task...
            szTask = a_jsonlookup.GetJson("task");

            // Run our task...
            blSuccess = sword.BatchMode(m_szScanner, szTask, true, ref a_swordtask, ref a_blSetAppCapabilities, out m_szScanImageArguments);
            if (!blSuccess)
            {
                return (TwainLocalScanner.ApiStatus.invalidCapturingOptions);
            }

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
            // Init stuff...
            //m_blCancel = false;
            m_iImageCount = 0;
            a_szSession = "";
            ClearEndOfJob();

            // Validate...
            if (m_szTwainDriverIdentity == null)
            {
                return (TwainLocalScanner.ApiStatus.invalidSessionId);
            }

            // TBD...
            ScanImageStart(m_szTwainDriverIdentity, m_szScanImageArguments);

            // Make a note of our success...
            //m_blProcessing = true;

            // Build the reply...
            DeviceScannerGetSession(out a_szSession);

            // All done...
            return (TwainLocalScanner.ApiStatus.success);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static Object objectScanImageReadLine = new Object();
        public void ScanImageReadLine
        (
            Object sender,
            DataReceivedEventArgs e
        )
        {
            lock (objectScanImageReadLine)
            {
                bool blSuccess;
                bool blEndOfJob = false;
                int iImageNumber;

                // Log it, note that we may not get everything because .NET can
                // fumble the standard output.  This isn't a problem, it's just
                // nice to show what we can get...
                if (e == null)
                {
                    TwainDirectSupport.Log.Info("scanimage>>> (program exited)");
                    blEndOfJob = true;
                }
                else if (string.IsNullOrEmpty(e.Data))
                {
                    TwainDirectSupport.Log.Info("scanimage>>> (no data)");
                    return;
                }
                else
                {
                    TwainDirectSupport.Log.Info("scanimage>>> " + e.Data);
                    if (!e.Data.StartsWith("Scanned page"))
                    {
                        return;
                    }
                }

                // Get the list of files...
                string[] aszPnm = Directory.GetFiles(m_szImagesFolder);
                if (aszPnm == null)
                {
                    if (blEndOfJob)
                    {
                        SetEndOfJob("SUCCESS");
                    }
                    return;
                }

                // Convert all .pnm files into .pdf and metadata...
                foreach (string szPnm in aszPnm)
                {
                    string szMeta;
                    byte[] abPnm;

                    // Skip stuff that isn't .pnm...
                    if (!szPnm.EndsWith(".pnm"))
                    {
                        continue;
                    }

                    // Load the .pnm data...
                    TwainDirectSupport.Log.Info("scanimage>>> load " + szPnm);
                    try
                    {
                        abPnm = File.ReadAllBytes(szPnm);
                    }
                    catch
                    {
                        // We'll assume that the file isn't ready for us yet,
                        // so bail...
                        TwainDirectSupport.Log.Info("scanimage>>> ReadAllBytes failed (file may be in use)...");
                        if (blEndOfJob)
                        {
                            SetEndOfJob("SUCCESS");
                        }
                        return;
                    }

                    // Convert the first 40 bytes to ANSI...
                    string szHeader = Encoding.ASCII.GetString(abPnm,0,40);
                    string[] aszHeader = szHeader.Split(new char[] { '\n', '\r' });

                    // Get the pixelformat and the stride...
                    int iStride = 0;
                    int iWidth = 0;
                    int iHeight = 0;
                    int iImageOffset = 0;
                    string szPixelFormat = aszHeader[0];
                    switch (szPixelFormat)
                    {
                        default:
                            TwainDirectSupport.Log.Info("scanimage>>> Not supported pixel format..." + szHeader);
                            continue;

                        case "P4":
                            iImageOffset = (aszHeader[0].Length + 1) + (aszHeader[1].Length + 1) + (aszHeader[2].Length + 1);
                            break;

                        case "P5":
                            iImageOffset = (aszHeader[0].Length + 1) + (aszHeader[1].Length + 1) + (aszHeader[2].Length + 1) + (aszHeader[3].Length + 1);
                            break;

                        case "P6":
                            iImageOffset = (aszHeader[0].Length + 1) + (aszHeader[1].Length + 1) + (aszHeader[2].Length + 1) + (aszHeader[3].Length + 1);
                            break;
                    }

                    // Get the width and the height...
                    string[] aszDim = aszHeader[2].Split(new char[] { ' ' });
                    int.TryParse(aszDim[0], out iWidth);
                    int.TryParse(aszDim[1], out iHeight);

                    // Get the image data...
                    byte[] abImage = new byte[abPnm.Length - iImageOffset];
                    Buffer.BlockCopy(abPnm, iImageOffset, abImage, 0, abPnm.Length - iImageOffset);

                    // Stupid bitonal data, you had a 50/50 shot!
                    if (szPixelFormat == "P4")
                    {
                        for (int bb = 0; bb < abImage.Length; bb++)
                        {
                            abImage[bb] = (byte)~abImage[bb];
                        }
                    }

                    // Infer the stride from the image size / by the height...
                    iStride = abImage.Length / iHeight;

                    // Get a byte array from the image...
                    TwainDirectSupport.Log.Info("scanimage>>> done: format=" + szPixelFormat + " " + iWidth + "x" + iHeight + " res=" + SaneTask.ms_szResolution + " stride=" + iStride);

                    // So far so good, let's extract the image number...
                    if (!int.TryParse(Path.GetFileNameWithoutExtension(szPnm).Replace("img",""), out iImageNumber))
                    {
                        TwainDirectSupport.Log.Info("scanimage>>> failed to get the image number...");
                        continue;
                    }

                    // Create the .pdf...
                    string szPdfFile = szPnm.Remove(szPnm.Length - 4, 4) + ".pdf";
                    TwainDirectSupport.Log.Info("scanimage>>> creating pdf " + szPdfFile);
                    blSuccess = CreatePdfRaster
                    (
                        szPdfFile,
                        abImage,
                        0,
                        szPixelFormat,
                        (UInt32)int.Parse(SaneTask.ms_szResolution),
                        (UInt32)iWidth,
                        (UInt32)iHeight
                    );
                    if (!blSuccess)
                    {
                        TwainDirectSupport.Log.Error("ReportImage: unable to save the image file..." + szPdfFile);
                        //m_blProcessing = false;
                        //m_blCancel = false;
                        SetEndOfJob("FILEWRITEERROR");
                        return;
                    }
                    TwainDirectSupport.Log.Info("scanimage>>> done...");

                    // TWAIN Direct metadata...
                    szMeta = "";

                    // TWAIN Direct metadata.address begin...
                    szMeta += "        \"metadata\": {\n";

                    // TWAIN Direct metadata.address begin...
                    szMeta += "            \"address\": {\n";

                    // Imagecount (counts images)...
                    szMeta += "                \"imageNumber\": " + m_iImageCount + ",\n";

                    // Sheetcount (counts sheets, including ones lost to blank image dropout)...
                    szMeta += "                \"sheetNumber\": " + "1" + ",\n";

                    // The image came from a flatbed or a feederFront or whatever...
                    if ((iImageNumber & 1) == 1)
                    {
                        szMeta += "                \"source\": \"" + "feederFront" + "\"\n";
                    }
                    else
                    {
                        szMeta += "                \"source\": \"" + "feederRear" + "\"\n";
                    }

                    // TWAIN Direct metadata.address end...
                    szMeta += "            },\n";

                    // TWAIN Direct metadata.image begin...
                    szMeta += "            \"image\": {\n";

                    // Add compression...
                    szMeta += "                \"compression\": \"" + "none" + "\",\n";

                    // Add pixel format...
                    switch (szPixelFormat)
                    {
                        default:
                        case "P4":
                            szMeta += "                \"pixelFormat\": \"" + "bw1" + "\",\n";
                            break;
                        case "P5":
                            szMeta += "                \"pixelFormat\": \"" + "gray8" + "\",\n";
                            break;
                        case "P6":
                            szMeta += "                \"pixelFormat\": \"" + "rgb24" + "\",\n";
                            break;
                    }

                    // Add height...
                    szMeta += "                \"pixelHeight\": " + iHeight + ",\n";

                    // X-offset...
                    szMeta += "                \"pixelOffsetX\": " + "0" + ",\n";

                    // Y-offset...
                    szMeta += "                \"pixelOffsetY\": " + "0" + ",\n";

                    // Add width...
                    szMeta += "                \"pixelWidth\": " + iWidth + ",\n";

                    // Add resolution...
                    szMeta += "                \"resolution\": " + SaneTask.ms_szResolution + ",\n";

                    // Add size...
                    FileInfo fileinfo = new FileInfo(szPnm);
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

                    // Get rid of the .pnm file...
                    TwainDirectSupport.Log.Info("scanimage>>> deleting the pnm file...");
                    abImage = null;
                    abPnm = null;
                    try
                    {
                        File.Delete(szPnm);
                    }
                    catch
                    {
                        // Don't really care...
                    }

                    // Save the metadata to disk...
                    try
                    {
                        string szMetaFile = szPnm.Remove(szPnm.Length-4,4) + ".meta";
                        File.WriteAllText(szMetaFile, szMeta);
                        TwainDirectSupport.Log.Info("ReportImage: saved " + szMetaFile);
                    }
                    catch
                    {
                        TwainDirectSupport.Log.Error("ReportImage: unable to save the metadata file...");
                        //m_blProcessing = false;
                        //m_blCancel = false;
                        SetEndOfJob("FILEWRITEERROR");
                        return;
                    }
                }

                // Looks like we're all done...
                if (blEndOfJob)
                {
                    SetEndOfJob("SUCCESS");
                    m_processScanImage = null;
                }
            }
        }

        /// <summary>
        /// Start scanning...
        /// </summary>
        /// <param name="a_szDevice"></param>
        /// <returns></returns>
        bool ScanImageStart(string a_szDevice, string a_szArguments)
        {
            // Validate...
            if (m_processScanImage != null)
            {
                Log.Error("Please don't do that, we're already scanning...");
                return (false);
            }

            // TBD -d doesn't work with Avision...
            // Time for launch...
            m_processScanImage = new Process();
            Program.ScanImage
            (
                "Scan",
                //"-d " + a_szDevice +
                " " + a_szArguments,
                ref m_processScanImage,
                ScanImageReadLine
            );

            // All done...
            return (true);
        }

        /// <summary>
        /// Stop scanning...
        /// </summary>
        /// <param name="a_szDevice"></param>
        /// <returns></returns>
        void ScanImageStop()
        {
            // Validate...
            if (m_processScanImage == null)
            {
                return;
            }

            // Stop monitoring the process output...
            m_processScanImage.CancelErrorRead();
            m_processScanImage.CancelOutputRead();

            // Kill the process...(might be nice to find a better way to do this)...
            m_processScanImage.Kill();

            // All done...
            m_processScanImage = null;
        }

        /// <summary>
        /// Stop the scanner.  We'll try it the nice way first.  If that doesn't fly, then
        /// we'll set a flag to reset the scanner next time we hit a msg_endxfer...
        /// </summary>
        /// <param name="a_szSession">the session data</param>
        /// <returns>a twain local status</returns>
        private TwainLocalScanner.ApiStatus DeviceScannerStopCapturing(out string a_szSession)
        {
            // TBD (not sure how to do this one...separate process and sigint?)

            // Build the reply...
            DeviceScannerGetSession(out a_szSession);

            // Oh well, we'll try to abort...
            //m_blCancel = true;
            return (TwainLocalScanner.ApiStatus.success);
        }

        #endregion


        // Private Attributes...
        #region Private Attributes...

        /// <summary>
        /// The SANE name of the scanner we're using...
        /// </summary>
        private string m_szScanner;

        /// <summary>
        /// The process object for scanimage...
        /// </summary>
        private Process m_processScanImage;

        /// <summary>
        /// TWAIN identity of the scanner we're using...
        /// </summary>
        private string m_szTwainDriverIdentity;

        /// <summary>
        /// The scanimage arguments...
        /// </summary>
        private string m_szScanImageArguments;

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
        //private bool m_blCancel;

        /// <summary>
        /// We're processing images...
        /// </summary>
        //private bool m_blProcessing;

        /// <summary>
        /// Count of images for each TwainStartCapturing call, the
        /// first image is always 1...
        /// </summary>
        private int m_iImageCount;

        /// <summary>
        /// End of job detected...
        /// </summary>
        private bool m_blImageBlocksDrained;

        /// <summary>
        /// We're scanning from a flatbed...
        /// </summary>
        //private bool m_blFlatbed;

        /// <summary>
        /// We're scanning duplex (front and rear) off an automatic document feeder (ADF)...
        /// </summary>
        //private bool m_blDuplex;

        /// <summary>
        /// The capturingOptions in the session object.  We need to persist these
        /// when we move from a ready state to anything else.  The way TWAIN Local is designed
        /// we're guaranteed to set these values before we get out of the ready state...
        /// </summary>
        private int m_iWidth;
        private int m_iHeight;
        private int m_iOffsetX;
        private int m_iOffsetY;
        private string m_szNumberOfSheets;
        private string m_szPixelFormat;
        private string m_szResolution;

        #endregion
    }
}
