///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.App.FormScan
//
// This is the main class for the application.  We're showing how to select and
// load a device.  How to configure it for a scan session, and how to capture
// and display images.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Version     Comment
//  M.McLaughlin    31-Oct-2014     0.0.0.1     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2014-2018 Kodak Alaris Inc.
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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Threading;
using System.Windows.Forms;
using HazyBits.Twain.Cloud.Client;
using HazyBits.Twain.Cloud.Forms;
using TwainDirect.Support;

namespace TwainDirect.App
{
    /// <summary>
    /// Our mainform for this application...
    /// </summary>
    public partial class FormScan : Form
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Get things going...
        /// </summary>
        /// <param name="a_fScale">scale factor for our form</param>
        public FormScan()
        {
            // Set up a data folder, in this instance we're assuming the project
            // name matches the binary, so we can quickly locate it...
            string szExecutableName = Config.Get("executableName", "");
            string szWriteFolder = Config.Get("writeFolder", "");

            // Turn on logging...
            Log.Open(szExecutableName, szWriteFolder, 1);
            Log.SetLevel((int)Config.Get("logLevel", 0));
            Log.Info(szExecutableName + " Log Started...");

            // Localize, the user can override the system default...
            string szCurrentUiCulture = Config.Get("language", "");
            if (string.IsNullOrEmpty(szCurrentUiCulture))
            {
                szCurrentUiCulture = Thread.CurrentThread.CurrentUICulture.ToString();
            }
            switch (szCurrentUiCulture.ToLower())
            {
                default:
                    Log.Info("UiCulture: " + szCurrentUiCulture + " (not supported, so using en-US)");
                    m_resourcemanager = lang_en_US.ResourceManager;
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
                    break;
                case "en-us":
                    Log.Info("UiCulture: " + szCurrentUiCulture);
                    m_resourcemanager = lang_en_US.ResourceManager;
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
                    break;
                case "fr-fr":
                    Log.Info("UiCulture: " + szCurrentUiCulture);
                    m_resourcemanager = lang_fr_FR.ResourceManager;
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo("fr-FR");
                    break;
            }

            // Build our form...
            InitializeComponent();

            // Pick a place where we can write stuff...
            m_szWriteFolder = Config.Get("writeFolder", "");

            // Handle scaling...
            m_fScale = (float)Config.Get("scale", 1.0);
            if (m_fScale <= 1)
            {
                m_fScale = 1;
            }
            else if (m_fScale > 2)
            {
                m_fScale = 2;
            }
            if (m_fScale != 1)
            {
                this.Font = new Font(this.Font.FontFamily, this.Font.Size * m_fScale, this.Font.Style);
            }
            Log.Info("Scale: " + m_fScale);

            // Localize...
            this.Text = Config.GetResource(m_resourcemanager, "strFormScanTitle"); // TWAIN Direct: Application
            m_buttonClose.Text = Config.GetResource(m_resourcemanager, "strButtonClose");
            m_buttonOpen.Text = Config.GetResource(m_resourcemanager, "strButtonOpen");
            m_buttonSelect.Text = Config.GetResource(m_resourcemanager, "strButtonSelectEllipsis");
            m_buttonScan.Text = Config.GetResource(m_resourcemanager, "strButtonScan");
            m_buttonSetup.Text = Config.GetResource(m_resourcemanager, "strButtonSetupEllipsis");
            m_buttonStop.Text = Config.GetResource(m_resourcemanager, "strButtonStop");

            // Help with scaling...
            this.Resize += new EventHandler(FormScan_Resize);

            // Init other stuff...
            m_blExit = false;
            m_iUseBitmap = 0;
            this.MinimumSize = new Size(440, 331);
            this.FormClosing += new FormClosingEventHandler(FormScan_FormClosing);

            // Configure the listbox...
            this.m_listviewCertification.Hide();
            this.m_listviewCertification.Clear();
            this.m_listviewCertification.View = View.Details;
            this.m_listviewCertification.Sorting = SortOrder.None;
            this.m_listviewCertification.Columns.Add("Category", 130);
            this.m_listviewCertification.Columns.Add("Summary", 400);
            this.m_listviewCertification.Columns.Add("Status", 100);

            // Init our picture box...
            InitImage();

            // Init our buttons...
            SetButtons(EBUTTONSTATE.CLOSED);

            // Clear the picture boxes...
            LoadImage(ref m_pictureboxImage1, ref m_graphics1, ref m_bitmapGraphic1, null, "");
            LoadImage(ref m_pictureboxImage2, ref m_graphics2, ref m_bitmapGraphic2, null, "");

            // Get our TWAIN Local interface.
            m_twainlocalscannerclient = new TwainLocalScannerClient(EventCallback, this, false);

            // Is PDF/raster happy and healthy?
            PdfRaster pdfraster = new PdfRaster();
            if (!pdfraster.HealthCheck())
            {
                DialogResult dialogresult = MessageBox.Show
                (
                    "We need to install the Visual Studio 2017 Redistributables for PDF/raster.  May we continue?",
                    "Warning",
                    MessageBoxButtons.YesNo
                );
                if (dialogresult == DialogResult.Yes)
                {
                    pdfraster.InstallVisualStudioRedistributables();
                }
            }
        }

        /// <summary>
        /// Something horrible has happened and we need to abort...
        /// </summary>
        /// <returns></returns>
        public bool ExitRequested()
        {
            return (m_blExit);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// We run our client scan in a thread so the UI remains responsive...
        /// </summary>
        private void ClientScanThread()
        {
            bool blSuccess;
            ApiCmd apicmd;

            // Turn the buttons off while we're sending a task to the
            // scanner, and starting the capture of images...
            SetButtons(EBUTTONSTATE.UNDEFINED);

            // This does all the interesting bits.  The next change
            // to the buttons will occur in this function, if we
            // successfully transition to the capturing state...
            blSuccess = ClientScanSerial
            (
                m_formsetup.GetThumbnails(),
                m_formsetup.GetMetadataWithImage(),
                out apicmd
            );

            // Ruh-roh, something didn't work.  So let's figure out what happened
            // and what we're going to do about it...
            if (!blSuccess)
            {
                string[] aszCodes = apicmd.GetApiErrorCodes();
                string[] aszDescriptions = apicmd.GetApiErrorDescriptions();

                switch (apicmd.GetApiErrorFacility())
                {
                    // This should never happen, but if it does, end the session,
                    // because we don't know what's going on...
                    default:
                    case ApiCmd.ApiErrorFacility.undefined:
                        if ((aszDescriptions == null) || (aszDescriptions.Length == 0))
                        {
                            Log.Error("unknown error, please check the logs for more information (yeah this is meta, it means look up from this message)");
                            m_buttonClose_Click(null, null);
                            // Unknown error, please checks the logs for more information.
                            MessageBox.Show(Config.GetResource(m_resourcemanager, "errUnknownCheckLogs"), Config.GetResource(m_resourcemanager, "strFormScanTitle"));
                        }
                        else
                        {
                            Log.Error(aszDescriptions[0]);
                            m_buttonClose_Click(null, null);
                            MessageBox.Show(aszDescriptions[0], Config.GetResource(m_resourcemanager, "strFormScanTitle"));
                        }
                        break;

                    // All HTTP errors that get to this point end the session...
                    case ApiCmd.ApiErrorFacility.httpstatus:
                        Log.Error(aszDescriptions[0]);
                        m_buttonClose_Click(null, null);
                        MessageBox.Show(aszDescriptions[0], Config.GetResource(m_resourcemanager, "strFormScanTitle"));
                        break;

                    // Somebody didn't code stuff properly, end the session, because
                    // this kind of error could otherwise hang up the application...
                    case ApiCmd.ApiErrorFacility.security:
                        Log.Error(aszDescriptions[0]);
                        m_buttonClose_Click(null, null);
                        MessageBox.Show(aszDescriptions[0], Config.GetResource(m_resourcemanager, "strFormScanTitle"));
                        break;

                    // We have an error in the TWAIN Local procotol, it's up
                    // to the ClientScanSerial or ClientScanParallel function
                    // to make sure we got back to a ready state...
                    case ApiCmd.ApiErrorFacility.protocol:
                        Log.Error(aszDescriptions[0]);
                        MessageBox.Show(aszDescriptions[0], Config.GetResource(m_resourcemanager, "strFormScanTitle"));
                        break;

                    // We have an error in the TWAIN Direct language...
                    case ApiCmd.ApiErrorFacility.language:
                        // This error is the result of the task specifying an exception of 'fail' somewhere within it.  The scanner triggered this when it was unable to set a required value.  There are two possible solutions: find a scanner that supports the feature required by the task, or use a different task.
                        // The dotted notation, indicated above, points to the value that triggered the failure condition.
                        foreach (string sz in aszDescriptions)
                        {
                            Log.Error(sz);
                            MessageBox.Show
                            (
                                sz + "\n" +
                                "\n" +
                                Config.GetResource(m_resourcemanager, "errTaskFailed") + "\n" +
                                "\n" +
                                Config.GetResource(m_resourcemanager, "errDottedNotation"),
                                Config.GetResource(m_resourcemanager, "strFormScanTitle")
                            );
                        }
                        break;
                }
            }

            // A problem with scanning isn't an API error, which is why we're
            // not checking it with all the other stuff.  Inatead, we're going
            // to catch it at the very end to explain what's happened...
            string szDetected;
            if (!m_twainlocalscannerclient.ClientGetSessionStatusSuccess(out szDetected))
            {
                long lJsonErrorIndex;
                JsonLookup jsonlookup = new JsonLookup();
                jsonlookup.Load(apicmd.GetSendCommand(), out lJsonErrorIndex);
                string szCommand = jsonlookup.Get("method");
                if (string.IsNullOrEmpty(szCommand))
                {
                    szCommand = "scanning";
                }
                MessageBox.Show(ReportError(szCommand, apicmd.GetHttpResponseData()), Config.GetResource(m_resourcemanager, "strFormScanTitle"));
            }

            // We're in good shape, set the buttons to allow more scanning...
            SetButtons(EBUTTONSTATE.OPEN);

            // Use the UI thread to finish closing us...
            BeginInvoke(new MethodInvoker(ClientScanThreadClose));
        }

        /// <summary>
        /// Cleanup our scan thread...
        /// </summary>
        private void ClientScanThreadClose()
        {
            m_threadClientScan.Join();
            m_threadClientScan = null;
        }

        /// <summary>
        /// This is the serial version of the scan loop.  It has the benefit of being
        /// fairly easy to understand and debug.  The downside is a lot of dead time
        /// on the net, so it's slow compared to what can be achieved if one transfers
        /// multiple images at the same.
        /// 
        /// This program is intended as a template for application writers for any
        /// language.  This function is arguably the most important one in the entire
        /// program, so it's heavily commented.
        /// 
        /// If bad things happen, a_apicmd will return information on the first
        /// command that ran into issues.
        /// </summary>
        /// <param name="a_blStopCapturing">a flag if capturing has been stopped</param>
        /// <param name="a_blGetThumbnails">the caller would like thumbnails</param>
        /// <param name="a_blGetMetadataWithImage">skip the standalone metadata call</param>
        /// <param name="a_apicmd">if errors occur, this has information</param>
        /// <returns>true on success</returns>
        private bool ClientScanSerial
        (
            bool a_blGetThumbnails,
            bool a_blGetMetadataWithImage,
            out ApiCmd a_apicmd
        )
        {
            bool blSuccess;
            bool blSuccessClientScan;
            bool blStopCapturing;
            long[] alImageBlocks;
            ApiCmd apicmd;

            // Init stuff...
            alImageBlocks = null;
            blStopCapturing = true;

            // We want to return the first apicmd that has a problem, to do that we
            // need a bait-and-switch scheme that starts with making an object that
            // we'll return and then replace, if needed...
            a_apicmd = null; // it'll never be null, but the compiler needs comforting...
            apicmd = new ApiCmd(m_dnssddeviceinfo);
            blSuccessClientScan = true;

            // Clear the picture boxes, make sure we start with the left box...
            m_iUseBitmap = 0;
            LoadImage(ref m_pictureboxImage1, ref m_graphics1, ref m_bitmapGraphic1, null, "");
            LoadImage(ref m_pictureboxImage2, ref m_graphics2, ref m_bitmapGraphic2, null, "");

            // You won't find FormSetup.SetupMode.capturingOptions being handled
            // here, because its negotiation takes place entirely on the setup
            // dialog, so when we get here there's nothing left to do...

            // Send the task to the scanner... 
            m_twainlocalscannerclient.ClientScannerSendTask(m_formsetup.GetTask(), ref apicmd);
            blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerSendTask", ref apicmd);
            if (!blSuccess)
            {
                a_apicmd = apicmd;
                return (false);
            }

            // Last chance to bail before we start capturing...
            if (m_blStopCapturing || m_blAbortCapturing)
            {
                return (true);
            }

            // Start capturing...
            m_twainlocalscannerclient.ClientScannerStartCapturing(ref apicmd);
            blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerStartCapturing", ref apicmd);
            if (!blSuccess)
            {
                a_apicmd = apicmd;
                return (false);
            }

            // We're in a capturing state now, reflect that in the buttons...
            SetButtons(EBUTTONSTATE.SCANNING);

            // This is the outermost loop, inside we're handling thing in
            // two stages: first, we look for an event that tells us we
            // have work to do; second, we do work...
            blSuccess = true;
            while (true)
            {
                // Scoot if the scanner says it's done sending images, or if
                // we've received a failure status...
                if (m_blAbortCapturing || !blSuccess || m_twainlocalscannerclient.ClientGetImageBlocksDrained())
                {
                    Log.Info("ClientScanSerial: break on abort, not success, or drained...");
                    break;
                }

                // Wait for the session object to be updated, after that we'll
                // only need to wait for more events if we drain the scanner
                // of images...
                while (true)
                {
                    // Wait for the session object to be updated.  If this command
                    // returns false, it means that somebody wants us to stop
                    // scanning...
                    blSuccess = m_twainlocalscannerclient.ClientWaitForSessionUpdate(long.MaxValue);
                    if (m_blAbortCapturing)
                    {
                        Log.Info("ClientScanSerial: break on abort...");
                        break;
                    }
                    else if (m_blStopCapturing)
                    {
                        // Stop capturing...
                        if (blStopCapturing)
                        {
                            // If this doesn't work, then abort...
                            blStopCapturing = false;
                            m_twainlocalscannerclient.ClientScannerStopCapturing(ref apicmd);
                            blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerStopCapturing", ref apicmd);
                            if (!blSuccess)
                            {
                                m_blAbortCapturing = true;
                                if (blSuccessClientScan)
                                {
                                    a_apicmd = apicmd;
                                    apicmd = new ApiCmd(m_dnssddeviceinfo);
                                }
                                blSuccessClientScan = false;
                            }
                        }
                    }
                    else if (!blSuccess)
                    {
                        if (blSuccessClientScan)
                        {
                            a_apicmd = apicmd;
                            apicmd = new ApiCmd(m_dnssddeviceinfo);
                        }
                        blSuccessClientScan = false;
                        Log.Info("ClientScanSerial: break not success...");
                        break;
                    }

                    // If we have an imageBlock pop out, we're going to transfer it...
                    alImageBlocks = m_twainlocalscannerclient.ClientGetImageBlocks();
                    if (m_twainlocalscannerclient.ClientGetImageBlocksDrained() || ((alImageBlocks != null) && (alImageBlocks.Length > 0)))
                    {
                        break;
                    }
                }

                // Scoot if the scanner says it's done sending images, or if
                // we've received a failure status...
                if (m_blAbortCapturing || !blSuccess || m_twainlocalscannerclient.ClientGetImageBlocksDrained())
                {
                    Log.Info("ClientScan: break on abort, not success, or drained (2)...");
                    break;
                }

                // Loop on each imageBlock until we exhaust the imageBlocks array...
                while (true)
                {
                    // We have the option to skip getting the metadata with this
                    // call.  An application should get metadata if it wants to
                    // examine it before getting the image.  If it always wants
                    // the image, it really doesn't need this step to be separate,
                    // which will save us a round-trip on the network.
                    //
                    // If the caller doesn't get the metadata with this call,
                    // then they must get it with the image data.  This is because
                    // the metadata is the only place where address information is
                    // reported, such as the imageNumber, imagePart, and moreParts...
                    if (!a_blGetMetadataWithImage)
                    {
                        m_twainlocalscannerclient.ClientScannerReadImageBlockMetadata(alImageBlocks[0], a_blGetThumbnails, ref apicmd);
                        blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerReadImageBlockMetadata", ref apicmd);
                        if (m_blAbortCapturing)
                        {
                            break;
                        }
                        else if (m_blStopCapturing)
                        {
                            // Stop capturing...
                            if (blStopCapturing)
                            {
                                // If this doesn't work, then abort...
                                blStopCapturing = false;
                                m_twainlocalscannerclient.ClientScannerStopCapturing(ref apicmd);
                                blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerStopCapturing", ref apicmd);
                                if (!blSuccess)
                                {
                                    m_blAbortCapturing = true;
                                    if (blSuccessClientScan)
                                    {
                                        a_apicmd = apicmd;
                                        apicmd = new ApiCmd(m_dnssddeviceinfo);
                                    }
                                    blSuccessClientScan = false;
                                }
                            }
                        }
                        else if (!blSuccess)
                        {
                            if (blSuccessClientScan)
                            {
                                a_apicmd = apicmd;
                                apicmd = new ApiCmd(m_dnssddeviceinfo);
                            }
                            blSuccessClientScan = false;
                            break;
                        }
                    }

                    // Get the corresponding image block in the array...
                    m_twainlocalscannerclient.ClientScannerReadImageBlock(alImageBlocks[0], a_blGetMetadataWithImage, ImageBlockCallback, ref apicmd);
                    blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerReadImageBlock", ref apicmd);
                    if (m_blAbortCapturing)
                    {
                        break;
                    }
                    else if (m_blStopCapturing)
                    {
                        // Stop capturing...
                        if (blStopCapturing)
                        {
                            // If this doesn't work, then abort...
                            blStopCapturing = false;
                            m_twainlocalscannerclient.ClientScannerStopCapturing(ref apicmd);
                            blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerStopCapturing", ref apicmd);
                            if (!blSuccess)
                            {
                                m_blAbortCapturing = true;
                                if (blSuccessClientScan)
                                {
                                    a_apicmd = apicmd;
                                    apicmd = new ApiCmd(m_dnssddeviceinfo);
                                }
                                blSuccessClientScan = false;
                            }
                        }
                    }
                    else if (!blSuccess)
                    {
                        if (blSuccessClientScan)
                        {
                            a_apicmd = apicmd;
                            apicmd = new ApiCmd(m_dnssddeviceinfo);
                        }
                        blSuccessClientScan = false;
                        break;
                    }

                    // Release the image block...
                    m_twainlocalscannerclient.ClientScannerReleaseImageBlocks(alImageBlocks[0], alImageBlocks[0], ref apicmd);
                    blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerReleaseImageBlocks", ref apicmd);
                    if (m_blAbortCapturing)
                    {
                        break;
                    }
                    else if (m_blStopCapturing)
                    {
                        // Stop capturing...
                        if (blStopCapturing)
                        {
                            // If this doesn't work, then abort...
                            blStopCapturing = false;
                            m_twainlocalscannerclient.ClientScannerStopCapturing(ref apicmd);
                            blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerStopCapturing", ref apicmd);
                            if (!blSuccess)
                            {
                                m_blAbortCapturing = true;
                                if (blSuccessClientScan)
                                {
                                    a_apicmd = apicmd;
                                    apicmd = new ApiCmd(m_dnssddeviceinfo);
                                }
                                blSuccessClientScan = false;
                            }
                        }
                    }
                    else if (!blSuccess)
                    {
                        if (blSuccessClientScan)
                        {
                            a_apicmd = apicmd;
                            apicmd = new ApiCmd(m_dnssddeviceinfo);
                        }
                        blSuccessClientScan = false;
                        break;
                    }

                    // If we're out of imageBlocks, exit this loop...
                    alImageBlocks = m_twainlocalscannerclient.ClientGetImageBlocks();
                    if ((alImageBlocks == null) || (alImageBlocks.Length == 0))
                    {
                        break;
                    }
                }
            }

            // Stop capturing...
            if (blStopCapturing)
            {
                m_twainlocalscannerclient.ClientScannerStopCapturing(ref apicmd);
                blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerStopCapturing", ref apicmd);
                if (!blSuccess)
                {
                    if (blSuccessClientScan)
                    {
                        a_apicmd = apicmd;
                        apicmd = new ApiCmd(m_dnssddeviceinfo);
                    }
                    blSuccessClientScan = false;
                }
            }

            // As long as we're in the capturing or draining states, we need to
            // keep releasing images.
            while (   (m_twainlocalscannerclient.ClientGetSessionState() == "capturing")
                   || (m_twainlocalscannerclient.ClientGetSessionState() == "draining"))
            {
                // If we're drained, we can scoot, this handles stopCapturing,
                // which isn't going to close the session...
                if (m_twainlocalscannerclient.ClientGetImageBlocksDrained())
                {
                    break;
                }

                // Do we have anything to release?
                alImageBlocks = m_twainlocalscannerclient.ClientGetImageBlocks();
                if ((alImageBlocks == null) || (alImageBlocks.Length == 0))
                {
                    Thread.Sleep(100);
                    m_twainlocalscannerclient.ClientScannerGetSession(ref apicmd);
                    continue;
                }

                // Release them...
                m_twainlocalscannerclient.ClientScannerReleaseImageBlocks(alImageBlocks[0], alImageBlocks[alImageBlocks.Length - 1], ref apicmd);
                blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerReleaseImageBlocks", ref apicmd);
                if (!blSuccess)
                {
                    if (blSuccessClientScan)
                    {
                        a_apicmd = apicmd;
                        apicmd = new ApiCmd(m_dnssddeviceinfo);
                    }
                    blSuccessClientScan = false;
                    break;
                }
            }

            // Ideally, this is the point where we want to always be setting this...
            if (blSuccessClientScan)
            {
                a_apicmd = apicmd;
            }

            // All done...
            return (blSuccessClientScan);
        }

        private class ClientScanParallelHelper
        {
            public ClientScanParallelHelper(FormScan a_formscan, ApiCmd a_apicmd, long a_lImageBlock, bool a_blGetMetadataWithImage, bool a_blGetThumbnails)
            {
                m_formscan = a_formscan;
                m_apicmd = a_apicmd;
                m_lImageBlock = a_lImageBlock;
                m_blGetMetadataWithImage = a_blGetMetadataWithImage;
                m_blGetThumbnails = a_blGetThumbnails;
            }

            public void ClientScanParallelThreadLaunchpad(object a_object)
            {
                m_formscan.ClientScanParallelThread(this);
            }

            public FormScan m_formscan;
            public ApiCmd m_apicmd;
            public long m_lImageBlock;
            public bool m_blGetMetadataWithImage;
            public bool m_blGetThumbnails;
        }

        public void ClientScanParallelThread(object a_object)
        {
            bool blSuccess;
            ClientScanParallelHelper clientscanparallelhelper = (ClientScanParallelHelper)a_object;

            // We have the option to skip getting the metadata with this
            // call.  An application should get metadata if it wants to
            // examine it before getting the image.  If it always wants
            // the image, it really doesn't need this step to be separate,
            // which will save us a round-trip on the network.
            //
            // If the caller doesn't get the metadata with this call,
            // then they must get it with the image data.  This is because
            // the metadata is the only place where address information is
            // reported, such as the imageNumber, imagePart, and moreParts...
            if (!clientscanparallelhelper.m_blGetMetadataWithImage)
            {
                m_twainlocalscannerclient.ClientScannerReadImageBlockMetadata(clientscanparallelhelper.m_lImageBlock, clientscanparallelhelper.m_blGetThumbnails, ref clientscanparallelhelper.m_apicmd);
                blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerReadImageBlockMetadata", ref clientscanparallelhelper.m_apicmd);
            }

            // Get the corresponding image block in the array...
            m_twainlocalscannerclient.ClientScannerReadImageBlock(clientscanparallelhelper.m_lImageBlock, clientscanparallelhelper.m_blGetMetadataWithImage, ImageBlockCallback, ref clientscanparallelhelper.m_apicmd);
            blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerReadImageBlock", ref clientscanparallelhelper.m_apicmd);
            if (!blSuccess)
            {
                return;
            }

            // Release the image block...
            m_twainlocalscannerclient.ClientScannerReleaseImageBlocks(clientscanparallelhelper.m_lImageBlock, clientscanparallelhelper.m_lImageBlock, ref clientscanparallelhelper.m_apicmd);
            blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerReleaseImageBlocks", ref clientscanparallelhelper.m_apicmd);
        }

        /// <summary>
        /// This is the parallel version of the scan loop.  The idea is to push
        /// transfers to their maximum potential, by asking for multiple imageBlocks.
        /// 
        /// Since we're performance based, we're not getting metadata seperate
        /// from the image data.
        /// 
        /// If bad things happen, a_apicmd will return information on the first
        /// command that ran into issues.
        /// </summary>
        /// <param name="a_blStopCapturing">a flag if capturing has been stopped</param>
        /// <param name="a_blGetThumbnails">the caller would like thumbnails</param>
        /// <param name="a_blGetMetadataWithImage">skip the standalone metadata call</param>
        /// <param name="a_apicmd">if errors occur, this has information</param>
        /// <returns>true on success</returns>
        private bool ClientScanParallel
        (
            bool a_blGetThumbnails,
            bool a_blGetMetadataWithImage,
            out ApiCmd a_apicmd
        )
        {
            bool blSuccess;
            bool blSuccessClientScan;
            bool blStopCapturing;
            List<long> listlImageBlocksWithThreads;
            long[] alImageBlocks;
            ApiCmd apicmd;
            List<Thread> listThreadsToRemove;

            // Init stuff...
            alImageBlocks = null;
            blStopCapturing = true;
            m_listThreadClientScanParallel = new List<Thread>();
            listlImageBlocksWithThreads = new List<long>();

            // We want to return the first apicmd that has a problem, to do that we
            // need a bait-and-switch scheme that starts with making an object that
            // we'll return and then replace, if needed...
            a_apicmd = null; // it'll never be null, but the compiler needs comforting...
            apicmd = new ApiCmd(m_dnssddeviceinfo);
            blSuccessClientScan = true;

            // Clear the picture boxes, make sure we start with the left box...
            m_iUseBitmap = 0;
            LoadImage(ref m_pictureboxImage1, ref m_graphics1, ref m_bitmapGraphic1, null, "");
            LoadImage(ref m_pictureboxImage2, ref m_graphics2, ref m_bitmapGraphic2, null, "");

            // You won't find FormSetup.SetupMode.capturingOptions being handled
            // here, because its negotiation takes place entirely on the setup
            // dialog, so when we get here there's nothing left to do...

            // Send the task to the scanner... 
            m_twainlocalscannerclient.ClientScannerSendTask(m_formsetup.GetTask(), ref apicmd);
            blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerSendTask", ref apicmd);
            if (!blSuccess)
            {
                a_apicmd = apicmd;
                return (false);
            }

            // Last chance to bail before we start capturing...
            if (m_blStopCapturing || m_blAbortCapturing)
            {
                return (true);
            }

            // Start capturing...
            m_twainlocalscannerclient.ClientScannerStartCapturing(ref apicmd);
            blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerStartCapturing", ref apicmd);
            if (!blSuccess)
            {
                a_apicmd = apicmd;
                return (false);
            }

            // We're in a capturing state now, reflect that in the buttons...
            SetButtons(EBUTTONSTATE.SCANNING);

            // This is the outermost loop, inside we're handling thing in
            // two stages: first, we look for an event that tells us we
            // have work to do; second, we do work...
            blSuccess = true;
            while (true)
            {
                // Scoot if the scanner says it's done sending images, or if
                // we've received a failure status...
                if (m_blAbortCapturing || !blSuccess || m_twainlocalscannerclient.ClientGetImageBlocksDrained())
                {
                    Log.Info("ClientScanParallel: break on abort, not success, or drained...");
                    break;
                }

                // Wait for the session object to be updated, after that we'll
                // only need to wait for more events if we drain the scanner
                // of images...
                while (true)
                {
                    // Wait for the session object to be updated.  If this command
                    // returns false, it means that somebody wants us to stop
                    // scanning...
                    blSuccess = m_twainlocalscannerclient.ClientWaitForSessionUpdate(long.MaxValue);
                    if (m_blAbortCapturing)
                    {
                        Log.Info("ClientScanParallel: break on abort...");
                        break;
                    }
                    else if (m_blStopCapturing)
                    {
                        // Stop capturing...
                        if (blStopCapturing)
                        {
                            // If this doesn't work, then abort...
                            blStopCapturing = false;
                            m_twainlocalscannerclient.ClientScannerStopCapturing(ref apicmd);
                            blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerStopCapturing", ref apicmd);
                            if (!blSuccess)
                            {
                                m_blAbortCapturing = true;
                                if (blSuccessClientScan)
                                {
                                    a_apicmd = apicmd;
                                    apicmd = new ApiCmd(m_dnssddeviceinfo);
                                }
                                blSuccessClientScan = false;
                            }
                        }
                    }
                    else if (!blSuccess)
                    {
                        if (blSuccessClientScan)
                        {
                            a_apicmd = apicmd;
                            apicmd = new ApiCmd(m_dnssddeviceinfo);
                        }
                        blSuccessClientScan = false;
                        Log.Info("ClientScanParallel: break not success...");
                        break;
                    }

                    // If we have an imageBlock pop out, we're going to transfer it...
                    alImageBlocks = m_twainlocalscannerclient.ClientGetImageBlocks();
                    if (m_twainlocalscannerclient.ClientGetImageBlocksDrained() || ((alImageBlocks != null) && (alImageBlocks.Length > 0)))
                    {
                        break;
                    }
                }

                // Scoot if the scanner says it's done sending images, or if
                // we've received a failure status...
                if (m_blAbortCapturing || !blSuccess || m_twainlocalscannerclient.ClientGetImageBlocksDrained())
                {
                    Log.Info("ClientScanParallel: break on abort, not success, or drained (2)...");
                    break;
                }

                // Cleanup anything that's done...
                listThreadsToRemove = new List<Thread>();
                foreach (Thread thread in m_listThreadClientScanParallel)
                {
                    if (!thread.IsAlive)
                    {
                        listThreadsToRemove.Add(thread);
                    }
                }
                foreach (Thread thread in listThreadsToRemove)
                {
                    m_listThreadClientScanParallel.Remove(thread);
                }

                // Kick off a thread for each image block...
                foreach (long lImageBlock in alImageBlocks)
                {
                    // We've already done this one...
                    if (listlImageBlocksWithThreads.Contains(lImageBlock))
                    {
                        continue;
                    }

                    // We're doing it now!
                    listlImageBlocksWithThreads.Add(lImageBlock);
                    ClientScanParallelHelper clientscanparallelhelper = new ClientScanParallelHelper(this, apicmd, alImageBlocks[0], a_blGetMetadataWithImage, a_blGetThumbnails);
                    Thread thread = new Thread(clientscanparallelhelper.ClientScanParallelThreadLaunchpad);
                    thread.Start();
                    m_listThreadClientScanParallel.Add(thread);
                }
            }

            // Stop capturing...
            if (blStopCapturing)
            {
                m_twainlocalscannerclient.ClientScannerStopCapturing(ref apicmd);
                blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerStopCapturing", ref apicmd);
                if (!blSuccess)
                {
                    if (blSuccessClientScan)
                    {
                        a_apicmd = apicmd;
                        apicmd = new ApiCmd(m_dnssddeviceinfo);
                    }
                    blSuccessClientScan = false;
                }
            }

            // Cleanup any threads we ran...
            while (m_listThreadClientScanParallel.Count > 0)
            {
                listThreadsToRemove = new List<Thread>();
                foreach (Thread thread in m_listThreadClientScanParallel)
                {
                    if (!thread.IsAlive)
                    {
                        listThreadsToRemove.Add(thread);
                    }
                }
                foreach (Thread thread in listThreadsToRemove)
                {
                    m_listThreadClientScanParallel.Remove(thread);
                }
                Thread.Sleep(1000);
            }

            // As long as we're in the capturing or draining states, we need to
            // keep releasing images.
            while (     (m_twainlocalscannerclient.ClientGetSessionState() == "capturing")
                   ||   (m_twainlocalscannerclient.ClientGetSessionState() == "draining"))
            {
                // If we're drained, we can scoot, this handles stopCapturing,
                // which isn't going to close the session...
                if (m_twainlocalscannerclient.ClientGetImageBlocksDrained())
                {
                    break;
                }

                // Do we have anything to release?
                alImageBlocks = m_twainlocalscannerclient.ClientGetImageBlocks();
                if ((alImageBlocks == null) || (alImageBlocks.Length == 0))
                {
                    Thread.Sleep(100);
                    m_twainlocalscannerclient.ClientScannerGetSession(ref apicmd);
                    continue;
                }

                // Release them...
                m_twainlocalscannerclient.ClientScannerReleaseImageBlocks(alImageBlocks[0], alImageBlocks[alImageBlocks.Length - 1], ref apicmd);
                blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerReleaseImageBlocks", ref apicmd);
                if (!blSuccess)
                {
                    if (blSuccessClientScan)
                    {
                        a_apicmd = apicmd;
                        apicmd = new ApiCmd(m_dnssddeviceinfo);
                    }
                    blSuccessClientScan = false;
                    break;
                }
            }

            // Ideally, this is the point where we want to always be setting this...
            if (blSuccessClientScan)
            {
                a_apicmd = apicmd;
            }

            // All done...
            return (blSuccessClientScan);
        }

        /// <summary>
        /// Event callback...
        /// </summary>
        /// <param name="a_object"></param>
        /// <param name="a_szEvent"></param>
        private void EventCallback(object a_object, string a_szEvent)
        {
            FormScan formscan = (FormScan)a_object;
            if (formscan != null)
            {
                switch (a_szEvent)
                {
                    // Don't recognize it...
                    default:
                        BeginInvoke(new MethodInvoker(UpdateSummary));
                        break;

                    // Image blocks have been updated...
                    case "imageBlocks":
                        BeginInvoke(new MethodInvoker(UpdateSummary));
                        break;

                    // We've lost our session
                    case "critical":
                        BeginInvoke(new MethodInvoker(Critical));
                        break;

                    // We've lost our session
                    case "sessionTimedOut":
                        BeginInvoke(new MethodInvoker(SessionTimedOut));
                        break;
                }
            }
        }

        /// <summary>
        /// Handle critical errors...
        /// </summary>
        private void Critical()
        {
            SetButtons(EBUTTONSTATE.UNDEFINED);
            m_twainlocalscannerclient.ClientCertificationTwainLocalSessionDestroy(true);
            if (m_threadClientScan != null)
            {
                m_threadClientScan.Abort();
                m_threadClientScan = null;
            }
            // Your scanner session has aborted.  Check your scanner to make sure it's on and connected to the network.
            MessageBox.Show(Config.GetResource(m_resourcemanager, "errSessionAborted"), Config.GetResource(m_resourcemanager, "strFormScanTitle"));
            SetButtons(EBUTTONSTATE.CLOSED);
        }

        /// <summary>
        /// Write some text to an image so we can display it...
        /// </summary>
        /// <param name="a_szText"></param>
        /// <returns>the image to display</returns>
        private Image DrawText(String a_szText)
        {
            Font fontText = new Font(FontFamily.GenericSansSerif, 20.0F, FontStyle.Bold);

            // first, create a dummy bitmap just to get a graphics object
            Image image = new Bitmap(1, 1);
            Graphics graphics = Graphics.FromImage(image);

            // measure the string to see how big the image needs to be
            SizeF textSize = graphics.MeasureString(a_szText, fontText);

            // free up the dummy image and old graphics object
            image.Dispose();
            graphics.Dispose();

            // create a new image of the right size
            image = new Bitmap((int)textSize.Width, (int)textSize.Height);
            graphics = Graphics.FromImage(image);

            // paint the background
            graphics.Clear(Color.Black);

            // create a brush for the text
            Brush textBrush = new SolidBrush(Color.White);
            graphics.DrawString(a_szText, fontText, textBrush, 0, 0);
            graphics.Save();

            // Cleanup and done...
            textBrush.Dispose();
            graphics.Dispose();
            return (image);

        }
        /// <summary>
        /// Handle sessionTimedOut errors...
        /// </summary>
        private void SessionTimedOut()
        {
            SetButtons(EBUTTONSTATE.UNDEFINED);
            m_twainlocalscannerclient.ClientCertificationTwainLocalSessionDestroy(true);
            MessageBox.Show(Config.GetResource(m_resourcemanager, "errSessionTimeout"), Config.GetResource(m_resourcemanager, "strFormScanTitle"));
            SetButtons(EBUTTONSTATE.CLOSED);
        }

        /// <summary>
        /// Input text...
        /// </summary>
        /// <param name="title">title of the box</param>
        /// <param name="promptText">prompt to the user</param>
        /// <param name="value">text typed by the user</param>
        /// <returns>button pressed</returns>
        private static DialogResult InputBox(string a_szTitle, string a_szPrompt, ref string a_szValue)
        {
            DialogResult dialogResult = DialogResult.Cancel;
            Form form = null;
            Label label = null;
            TextBox textBox = null;
            Button buttonOk = null;
            Button buttonCancel = null;

            try
            {
                form = new Form();
                label = new Label();
                textBox = new TextBox();
                buttonOk = new Button();
                buttonCancel = new Button();

                form.Text = a_szTitle;
                label.Text = a_szPrompt;
                textBox.Text = a_szValue;

                buttonOk.Text = "OK";
                buttonCancel.Text = "Cancel";
                buttonOk.DialogResult = DialogResult.OK;
                buttonCancel.DialogResult = DialogResult.Cancel;

                label.SetBounds(9, 20, 372, 13);
                textBox.SetBounds(12, 36, 372, 20);
                buttonOk.SetBounds(228, 72, 75, 23);
                buttonCancel.SetBounds(309, 72, 75, 23);

                label.AutoSize = true;
                textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
                buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

                form.ClientSize = new Size(396, 107);
                form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
                form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.AcceptButton = buttonOk;
                form.CancelButton = buttonCancel;

                dialogResult = form.ShowDialog();
                a_szValue = textBox.Text;
            }
            catch (Exception exception)
            {
                Log.Error("Something bad happened..." + exception.Message);
            }
            finally
            {
                // On the advice of analyze...
                if (form != null)
                {
                    form.Dispose();
                    form = null;
                }
                if (label != null)
                {
                    label.Dispose();
                    label = null;
                }
                if (textBox != null)
                {
                    textBox.Dispose();
                    textBox = null;
                }
                if (buttonOk != null)
                {
                    buttonOk.Dispose();
                    buttonOk = null;
                }
                if (buttonCancel != null)
                {
                    buttonCancel.Dispose();
                    buttonCancel = null;
                }
            }

            // All done...
            return (dialogResult);
        }

        /// <summary>
        /// We're being closed, clean up nicely...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void FormScan_FormClosing(object sender, FormClosingEventArgs e)
        {
            // This will prevent ReportImage from doing anything stupid as we close...
            m_graphics1 = null;

            // Use our close function...
            m_buttonClose_Click(sender, e);

            // Kill the monitor, if we have one...
            if (m_dnssd != null)
            {
                m_dnssd.MonitorStop();
                m_dnssd.Dispose();
                m_dnssd = null;
            }

            // More cleanup...
            if (m_twainlocalscannerclient != null)
            {
                m_twainlocalscannerclient.Dispose();
                m_twainlocalscannerclient = null;
            }

            // Close the log...
            Log.Info(Path.GetFileNameWithoutExtension(Application.ExecutablePath) + " Log Ended...");
            Log.Close();
        }

        /// <summary>
        /// Handle resizing...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void FormScan_Resize(object sender, EventArgs e)
        {
            // This is fine...
            m_pictureboxImage1.Size = new Size
            (
                (int)(((double)Size.Width / 2.0) - (double)(m_pictureboxImage1.Location.X * 2)),
                m_pictureboxImage1.Size.Height
            );

            // This is fine...
            m_pictureboxImage2.Size = new Size
            (
                (int)(((double)Size.Width / 2.0) - (double)(m_pictureboxImage1.Location.X * 2)),
                m_pictureboxImage2.Size.Height
            );

            // This is a little weird...
            m_pictureboxImage2.Location = new Point
            (
                (int)(((double)Size.Width / 2.0) - ((double)m_pictureboxImage1.Location.X * (0.3 / m_fScale))),
                m_pictureboxImage2.Location.Y
            );
        }

        /// <summary>
        /// The user wants to select a destination folder or a TWAIN
        /// Direct task.  We have some other options in here too...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonSetup_Click(object sender, EventArgs e)
        {
            ApiCmd apicmd = new ApiCmd(m_dnssddeviceinfo);

            // Make sure the form is centered on our parent...
            m_formsetup.StartPosition = FormStartPosition.CenterParent;

            // Show the form and wait for the user to close it...
            m_formsetup.ShowDialog(this);

            // Select the controls to show based on the user selection...
            m_pictureboxImage1.Show();
            m_pictureboxImage2.Show();
            m_listviewCertification.Hide();

            // Update the summary...
            UpdateSummary();
        }

        /// <summary>
        /// Start a scan session...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonScan_Click(object sender, EventArgs e)
        {
            int ii;
            bool blSuccess;
            long lJsonErrorIndex;
            string szAction;
            string szName;

            // If we already have one of these, scoot...
            if (m_threadClientScan != null)
            {
                return;
            }

            // Init stuff...
            m_blStopCapturing = false;
            m_blAbortCapturing = false;

            // If encryption is turned on, ask for a password...
            string szTask = m_formsetup.GetTask();
            if (!string.IsNullOrEmpty(szTask))
            {
                JsonLookup jsonlookup = new JsonLookup();
                blSuccess = jsonlookup.Load(szTask, out lJsonErrorIndex);
                if (blSuccess)
                {
                    ii = 0;
                    for (szAction = jsonlookup.Get("actions[" + ii + "].action", false);
                         !string.IsNullOrEmpty(szAction);
                         szAction = jsonlookup.Get("actions[" + (++ii) + "].action", false))
                    {
                        if (szAction == "encryptionProfiles")
                        {
                            szName = jsonlookup.Get("actions[" + ii + "].encryptionProfiles[0].name", false);
                            if (!string.IsNullOrEmpty(szName))
                            {
                                FormPassword formpassword = new FormPassword(m_szPassword, m_blShowPassword);
                                formpassword.ShowDialog();
                                m_szPassword = formpassword.GetPassword();
                                m_blShowPassword = formpassword.GetShowPassword();
                            }
                            break;
                        }
                    }
                }
            }

            // Make a new one, and get it going...
            m_threadClientScan = new Thread(ClientScanThread);
            m_threadClientScan.Start();
        }

        /// <summary>
        /// Our scan callback, used to display images...
        /// </summary>
        /// <param name="a_szMetadata">metadata for this imageBlock</param>
        /// <param name="a_szImageBlock">file for this imageBlock</param>
        /// <returns>true on success</returns>
        public bool ImageBlockCallback(long a_lImageBlockNum)
        {
            string szBasename;
            string szPdf;
            string szImageText;
            bool blGotImage = false;
            string szFinishedImageBasename;
            byte[] abImage;

            // This function creates a finished image, metadata, and thumbnail
            // from the imageBlocks...
            szBasename = Path.Combine(Path.Combine(m_szWriteFolder, "images"), "img" + a_lImageBlockNum.ToString("D6"));
            if (!m_twainlocalscannerclient.ClientFinishImage(szBasename, out szFinishedImageBasename))
            {
                // We don't have a complete image, so scoot...
                return (blGotImage);
            }

            // This is our finished PDF file...
            szPdf = szFinishedImageBasename + ".pdf";

            // We might have an image...
            FileInfo fileinfo = null;
            try
            {
                fileinfo = new FileInfo(szPdf);
            }
            catch
            {
                return (blGotImage);
            }

            // Just for now...
            blGotImage = true;

            // Convert the beastie...
            abImage = PdfRaster.ConvertPdfToTiffOrJpeg(szPdf, m_szPassword);
            if (abImage == null)
            {
                Log.Error("failed to convert the image...");
                // Get the image data...
                using (var bitmap = new Bitmap(DrawText("...DISPLAY ERROR...")))
                {
                    // Display the image...
                    if (m_iUseBitmap == 0)
                    {
                        m_iUseBitmap = 1;
                        szImageText = Path.GetFileName(szPdf);
                        LoadImage(ref m_pictureboxImage1, ref m_graphics1, ref m_bitmapGraphic1, bitmap, szImageText);
                    }
                    else
                    {
                        m_iUseBitmap = 0;
                        szImageText = Path.GetFileName(szPdf);
                        LoadImage(ref m_pictureboxImage2, ref m_graphics2, ref m_bitmapGraphic2, bitmap, szImageText);
                    }
                }
                return (blGotImage);
            }

            // Get the image data...
            using (var bitmap = new Bitmap(new MemoryStream(abImage)))
            {
                // Display the image...
                if (m_iUseBitmap == 0)
                {
                    m_iUseBitmap = 1;
                    szImageText = Path.GetFileName(szPdf);
                    LoadImage(ref m_pictureboxImage1, ref m_graphics1, ref m_bitmapGraphic1, bitmap, szImageText);
                }
                else
                {
                    m_iUseBitmap = 0;
                    szImageText = Path.GetFileName(szPdf);
                    LoadImage(ref m_pictureboxImage2, ref m_graphics2, ref m_bitmapGraphic2, bitmap, szImageText);
                }
            }

            // All done...
            return (blGotImage);
        }

        /// <summary>
        /// Load an image into a picture box, maintain its aspect ratio...
        /// </summary>
        /// <param name="a_picturebox"></param>
        /// <param name="a_graphics"></param>
        /// <param name="a_bitmapGraphic"></param>
        /// <param name="a_bitmap">bitmap to display or null to clear it</param>
        private void LoadImage
        (
            ref PictureBox a_picturebox,
            ref Graphics a_graphics,
            ref Bitmap a_bitmapGraphic,
            Bitmap a_bitmap,
            string a_szImageText
        )
        {
            double fRatioWidth = 0;
            double fRatioHeight = 0;
            double fRatio = 0;
            int iWidth = 0;
            int iHeight = 0;

            // Let us be called from any thread...
            if (this.InvokeRequired)
            {
                // We need a copy of the bitmap, because we're not going to wait
                // for the thread to return.  Be careful when using EndInvoke.
                // It's possible to create a deadlock situation with the Stop
                // button press.
                PictureBox picturebox = a_picturebox;
                Graphics graphics = a_graphics;
                Bitmap bitmapGraphic = a_bitmapGraphic;
                Invoke(new MethodInvoker(delegate() {LoadImage(ref picturebox, ref graphics, ref bitmapGraphic, a_bitmap, a_szImageText);}));
                return;
            }

            // We want to maintain the aspect ratio...
            if (a_bitmap != null)
            {
                fRatioWidth = (double)a_bitmapGraphic.Size.Width / (double)a_bitmap.Width;
                fRatioHeight = (double)a_bitmapGraphic.Size.Height / (double)a_bitmap.Height;
                fRatio = (fRatioWidth < fRatioHeight) ? fRatioWidth : fRatioHeight;
                iWidth = (int)(a_bitmap.Width * fRatio);
                iHeight = (int)(a_bitmap.Height * fRatio);
            }

            // Update the text...
            if (a_picturebox == m_pictureboxImage1)
            {
                m_textbox1.Text = a_szImageText;
            }
            else
            {
                m_textbox2.Text = a_szImageText;
            }
            UpdateSummary();

            // Display the image...
            a_graphics.FillRectangle(m_brushBackground, m_rectangleBackground);
            if (a_bitmap != null)
            {
                a_graphics.DrawImage(a_bitmap, new Rectangle(((int)a_bitmapGraphic.Width - iWidth) / 2, ((int)a_bitmapGraphic.Height - iHeight) / 2, iWidth, iHeight));
            }
            a_picturebox.Image = a_bitmapGraphic;
            a_picturebox.Update();
            this.Refresh();
            // This is bad...
            Application.DoEvents();
        }

        /// <summary>
        /// Initialize the picture boxes and the graphics to support them, we're
        /// doing this to maximize performance during scanner...
        /// </summary>
        private void InitImage()
        {
            // Make sure our picture boxes don't do much work...
            m_pictureboxImage1.SizeMode = PictureBoxSizeMode.Normal;
            m_pictureboxImage2.SizeMode = PictureBoxSizeMode.Normal;

            m_bitmapGraphic1 = new Bitmap(m_pictureboxImage1.Width, m_pictureboxImage1.Height, PixelFormat.Format32bppPArgb);
            m_graphics1 = Graphics.FromImage(m_bitmapGraphic1);
            m_graphics1.CompositingMode = CompositingMode.SourceCopy;
            m_graphics1.CompositingQuality = CompositingQuality.HighSpeed;
            m_graphics1.InterpolationMode = InterpolationMode.Low;
            m_graphics1.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            m_graphics1.SmoothingMode = SmoothingMode.HighSpeed;

            m_bitmapGraphic2 = new Bitmap(m_pictureboxImage1.Width, m_pictureboxImage1.Height, PixelFormat.Format32bppPArgb);
            m_graphics2 = Graphics.FromImage(m_bitmapGraphic2);
            m_graphics2.CompositingMode = CompositingMode.SourceCopy;
            m_graphics2.CompositingQuality = CompositingQuality.HighSpeed;
            m_graphics2.InterpolationMode = InterpolationMode.Low;
            m_graphics2.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            m_graphics2.SmoothingMode = SmoothingMode.HighSpeed;

            m_brushBackground = new SolidBrush(Color.DarkGray);
            m_rectangleBackground = new Rectangle(0, 0, m_bitmapGraphic1.Width, m_bitmapGraphic1.Height);
        }

        /// <summary>
        /// Configure our buttons to match our current state...
        /// </summary>
        /// <param name="a_ebuttonstate"></param>
        private void SetButtons(EBUTTONSTATE a_ebuttonstate)
        {
            EBUTTONSTATE ebuttonstate;

            // Make sure we're running in the proper thread...
            if (this.InvokeRequired)
            {
                Invoke(new MethodInvoker(delegate() { SetButtons(a_ebuttonstate); }));
                return;
            }

            // Update the summary...
            UpdateSummary();

            // Override...
            ebuttonstate = a_ebuttonstate;
            if ((m_twainlocalscannerclient != null) && (m_twainlocalscannerclient.GetState() == "noSession"))
            {
                ebuttonstate = EBUTTONSTATE.CLOSED;
            }

            // Fix the buttons...
            switch (ebuttonstate)
            {
                default:
                case EBUTTONSTATE.UNDEFINED:
                    m_buttonOpen.Enabled = false;
                    m_buttonSelect.Enabled = false;
                    m_buttonClose.Enabled = false;
                    m_buttonSetup.Enabled = false;
                    m_buttonScan.Enabled = false;
                    m_buttonStop.Enabled = false;
                    break;

                case EBUTTONSTATE.CLOSED:
                    m_buttonOpen.Enabled = true;
                    m_buttonSelect.Enabled = true;
                    m_buttonClose.Enabled = false;
                    m_buttonSetup.Enabled = false;
                    m_buttonScan.Enabled = false;
                    m_buttonStop.Enabled = false;
                    break;

                case EBUTTONSTATE.OPEN:
                    m_buttonOpen.Enabled = false;
                    m_buttonSelect.Enabled = false;
                    m_buttonClose.Enabled = true;
                    m_buttonSetup.Enabled = true;
                    m_buttonScan.Enabled = true;
                    m_buttonStop.Enabled = false;
                    break;

                case EBUTTONSTATE.SCANNING:
                    m_buttonOpen.Enabled = false;
                    m_buttonSelect.Enabled = false;
                    m_buttonClose.Enabled = true;
                    m_buttonSetup.Enabled = false;
                    m_buttonScan.Enabled = false;
                    m_buttonStop.Enabled = true;
                    break;
            }
        }

        /// <summary>
        /// Open the last selected scanner...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonOpen_Click(object sender, EventArgs e)
        {
            bool blUpdated;
            bool blNoMonitor;
            bool blSuccess;
            long lJsonErrorIndex;
            string szSelected = null;
            string szSelectedFile;
            string szLinkLocal;
            string szIpv4;
            string szIpv6;
            JsonLookup jsonlookupSelected;
            Dnssd.DnssdDeviceInfo[] adnssddeviceinfo = null;

            // If we don't have a selected file, then run the selection
            // function to prompt the user to pick something...
            szSelectedFile = Path.Combine(m_szWriteFolder, "selected");
            if (!File.Exists(szSelectedFile))
            {
                m_buttonSelect_Click(sender, e);
                return;
            }

            // Init stuff...
            m_blStopCapturing = false;
            m_blAbortCapturing = false;
            m_dnssddeviceinfo = null;

            // Buttons off...
            SetButtons(EBUTTONSTATE.UNDEFINED);

            // Read the data...
            try
            {
                szSelected = File.ReadAllText(szSelectedFile);
            }
            catch (Exception exception)
            {
                Log.Error("m_buttonOpen_Click: failed to read - <" + szSelectedFile + "> " + exception.Message);
            }

            // No joy, open the selection form...
            if (szSelected == null)
            {
                m_buttonSelect_Click(sender, e);
                return;
            }

            // Parse the data...
            jsonlookupSelected = new JsonLookup();
            blSuccess = jsonlookupSelected.Load(szSelected, out lJsonErrorIndex);
            if (!blSuccess)
            {
                Log.Error("m_buttonOpen_Click: failed to parse - <" + szSelected + "> " + lJsonErrorIndex);
                m_buttonSelect_Click(sender, e);
                return;
            }

            // We need the link-local and an IPv4 or an IPv6 to proceed...
            szLinkLocal = jsonlookupSelected.Get("linkLocal");
            if (string.IsNullOrEmpty(szLinkLocal))
            {
                Log.Error("m_buttonOpen_Click: missing linklocal - <" + szSelected + ">");
                m_buttonSelect_Click(sender, e);
                return;
            }
            szIpv4 = jsonlookupSelected.Get("ipv4");
            szIpv6 = jsonlookupSelected.Get("ipv6");
            if (    string.IsNullOrEmpty(szIpv4)
                &&  string.IsNullOrEmpty(szIpv6))
            {
                Log.Error("m_buttonOpen_Click: missing ip - <" + szSelected + ">");
                m_buttonSelect_Click(sender, e);
                return;
            }

            // Create the mdns monitor, and start it...
            bool blServiceIsAvailable;
            m_dnssd = new Dnssd(Dnssd.Reason.Monitor, out blServiceIsAvailable);
            if (blServiceIsAvailable)
            {
                m_dnssd.MonitorStart(null, IntPtr.Zero);
            }
            else
            {
                m_dnssd.Dispose();
                m_dnssd = null;
                m_buttonSelect_Click(sender, e);
                return;
            }

            // Grab a snapshot of what's out there...
            if (m_dnssd != null)
            {
                adnssddeviceinfo = m_dnssd.GetSnapshot(null, out blUpdated, out blNoMonitor);
                if ((adnssddeviceinfo == null) || (adnssddeviceinfo.Length == 0))
                {
                    MessageBox.Show(Config.GetResource(m_resourcemanager, "errNoTwainScanners"), Config.GetResource(m_resourcemanager, "strFormScanTitle"));
                    SetButtons(EBUTTONSTATE.CLOSED);
                    m_dnssd.Dispose();
                    m_dnssd = null;
                    return;
                }
            }

            // Find our entry...
            if (adnssddeviceinfo != null)
            {
                foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in adnssddeviceinfo)
                {
                    if (dnssddeviceinfo.GetLinkLocal() == szLinkLocal)
                    {
                        // Try for a match on Ipv6...
                        if (!string.IsNullOrEmpty(szIpv6))
                        {
                            if (dnssddeviceinfo.GetIpv6() == szIpv6)
                            {
                                m_dnssddeviceinfo = dnssddeviceinfo;
                                break;
                            }
                        }

                        // If that fails, try various forms of Ipv4...
                        if (!string.IsNullOrEmpty(szIpv4))
                        {
                            string szIpv4Tmp = szIpv4;
                            // XXX.XXX.XXX.XXX...
                            if (dnssddeviceinfo.GetIpv4() == szIpv4Tmp)
                            {
                                m_dnssddeviceinfo = dnssddeviceinfo;
                                break;
                            }
                            // XXX.XXX.XXX.*
                            if (szIpv4Tmp.Contains("."))
                            {
                                szIpv4Tmp = szIpv4Tmp.Remove(szIpv4Tmp.LastIndexOf('.'));
                                if (dnssddeviceinfo.GetIpv4().StartsWith(szIpv4Tmp + "."))
                                {
                                    m_dnssddeviceinfo = dnssddeviceinfo;
                                    break;
                                }
                            }
                            // XXX.XXX.*.*
                            if (szIpv4Tmp.Contains("."))
                            {
                                szIpv4Tmp = szIpv4Tmp.Remove(szIpv4Tmp.LastIndexOf('.'));
                                if (dnssddeviceinfo.GetIpv4().StartsWith(szIpv4Tmp + "."))
                                {
                                    m_dnssddeviceinfo = dnssddeviceinfo;
                                    break;
                                }
                            }
                            // XXX.*.*.*
                            if (szIpv4Tmp.Contains("."))
                            {
                                szIpv4Tmp = szIpv4Tmp.Remove(szIpv4Tmp.LastIndexOf('.'));
                                if (dnssddeviceinfo.GetIpv4().StartsWith(szIpv4Tmp + "."))
                                {
                                    m_dnssddeviceinfo = dnssddeviceinfo;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // No joy...
            if (m_dnssddeviceinfo == null)
            {
                Log.Error("m_buttonOpen_Click: selected not found - <" + szSelected + ">");
                m_buttonSelect_Click(sender, e);
                if (m_dnssd != null)
                {
                    m_dnssd.Dispose();
                    m_dnssd = null;
                    return;
                }
            }

            // We got something, so open the scanner...
            blSuccess = OpenScanner();
            if (blSuccess)
            {
                if (m_dnssd != null)
                {
                    m_dnssd.Dispose();
                    m_dnssd = null;
                }
            }
        }

        /// <summary>
        /// Shutdown...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonClose_Click(object sender, EventArgs e)
        {
            bool blSuccess;
            ApiCmd apicmd = new ApiCmd(m_dnssddeviceinfo);

            // Buttons off...
            SetButtons(EBUTTONSTATE.UNDEFINED);

            // If we're scanning, try to shut it down nicely...
            if (m_threadClientScan != null)
            {
                // Ask it to stop...
                m_blAbortCapturing = true;
                if (m_twainlocalscannerclient != null)
                {
                    m_twainlocalscannerclient.ClientWaitForSessionUpdateForceSet();
                }

                // Give it ten seconds to comply...
                for (int iRetry = 0; iRetry < 10000; iRetry += 100)
                {
                    if (m_threadClientScan == null)
                    {
                        break;
                    }
                    Thread.Sleep(100);
                }

                // If it's still there, annihilate it, the user will
                // probably have to wait for the session to timeout
                // before they can use it again...
                m_blAbortCapturing = true;
                if (m_threadClientScan != null)
                {
                    m_threadClientScan.Abort();
                    m_threadClientScan = null;
                }
            }

            // Bye-bye to the setup form...
            if (m_formsetup != null)
            {
                m_formsetup.Dispose();
                m_formsetup = null;
            }

            // Close session, if needed...
            if ((m_twainlocalscannerclient != null) && (m_twainlocalscannerclient.ClientGetSessionState() != "noSession"))
            {
                blSuccess = m_twainlocalscannerclient.ClientScannerCloseSession(ref apicmd);
                if (blSuccess)
                {
                    blSuccess = m_twainlocalscannerclient.ClientCheckForApiErrors("ClientScannerCloseSession", ref apicmd);
                    if (!blSuccess)
                    {
                        string[] aszDescriptions = apicmd.GetApiErrorDescriptions();
                        if ((aszDescriptions != null) && (aszDescriptions.Length > 0))
                        {
                            Log.Error(aszDescriptions[0]);
                            MessageBox.Show(aszDescriptions[0], Config.GetResource(m_resourcemanager, "strFormScanTitle"));
                        }
                    }
                }
            }

            // Buttons off...
            SetButtons(EBUTTONSTATE.CLOSED);

            // Lose dnssd...
            if (m_dnssd != null)
            {
                m_dnssd.Dispose();
                m_dnssd = null;
            }

            // Update the title bar...
            Text = "TWAIN Direct: Application";
        }

        /// <summary>
        /// Select a scanner...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonSelect_Click(object sender, EventArgs e)
        {
            bool blSuccess;
            FormSelect formselect;
            DialogResult dialogresult;

            // Init stuff...
            m_blStopCapturing = false;
            m_blAbortCapturing = false;
            m_dnssddeviceinfo = null;

            // Buttons off...
            SetButtons(EBUTTONSTATE.UNDEFINED);

            // Create the mdns monitor, and start it...
            bool blServiceIsAvailable;
            m_dnssd = new Dnssd(Dnssd.Reason.Monitor, out blServiceIsAvailable);
            if (blServiceIsAvailable)
            {
                m_dnssd.MonitorStart(null, IntPtr.Zero);
            }
            else
            {
                Log.Error("Bonjour is not available, has it been installed?");
                m_dnssd.Dispose();
                m_dnssd = null;
            }

            // Instantiate our selection form with the list of devices, and
            // wait for the user to pick something...
            dialogresult = DialogResult.Cancel;
            formselect = new FormSelect(ref m_dnssd, m_fScale, m_twaincloudTokens, out blSuccess, m_resourcemanager);
            if (!blSuccess)
            {
                SetButtons(EBUTTONSTATE.CLOSED);
                if (m_dnssd != null)
                {
                    m_dnssd.Dispose();
                    m_dnssd = null;
                }
                return;
            }

            // Show the form...
            try
            {
                formselect.StartPosition = FormStartPosition.CenterParent;
                dialogresult = formselect.ShowDialog(this);
                if (dialogresult != DialogResult.OK)
                {
                    // They opted to cancel out...
                    SetButtons(EBUTTONSTATE.CLOSED);
                    formselect.Dispose();
                    formselect = null;
                    if (m_dnssd != null)
                    {
                        m_dnssd.Dispose();
                        m_dnssd = null;
                    }
                    return;
                }
            }
            catch (Exception exception)
            {
                Log.Error("Something bad happened..." + exception.Message);
            }
            finally
            {
                m_dnssddeviceinfo = null;
                if (formselect != null)
                {
                    m_dnssddeviceinfo = formselect.GetSelectedDevice();
                    formselect.Dispose();
                    formselect = null;
                }
            }

            // Validate...
            if (m_dnssddeviceinfo == null)
            {
                MessageBox.Show(Config.GetResource(m_resourcemanager, "errNoDeviceSelected"), Config.GetResource(m_resourcemanager, "strFormScanTitle"));
                SetButtons(EBUTTONSTATE.CLOSED);
                formselect = null;
                if (m_dnssd != null)
                {
                    m_dnssd.Dispose();
                    m_dnssd = null;
                }
                return;
            }

            // Open, open the beastie...
            blSuccess = OpenScanner();
            if (!blSuccess)
            {
                if (m_dnssd != null)
                {
                    m_dnssd.Dispose();
                    m_dnssd = null;
                }
            }
        }

        /// <summary>
        /// Request that scanning stop (gracefully)...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonStop_Click(object sender, EventArgs e)
        {
            m_blStopCapturing = true;
            if (m_twainlocalscannerclient != null)
            {
                m_twainlocalscannerclient.ClientWaitForSessionUpdateForceSet();
            }
        }

        /// <summary>
        /// Parse the response data, and try to provide a useful message
        /// to the user...
        /// </summary>
        /// <param name="a_szCommand">command that was issued</param>
        /// <param name="a_szHttpResponseData">response received</param>
        /// <returns>the message to display</returns>
        private string ReportError(string a_szCommand, string a_szHttpResponseData)
        {
            long lJsonErrorIndex;
            JsonLookup jsonlookup;

            // If we have no data we've probably timed out on the connection,
            // that can happen for a number of reasons.  We'll try to offer
            // the best explanation, based on the command...
            if (string.IsNullOrEmpty(a_szHttpResponseData))
            {
                switch (a_szCommand)
                {
                    default:
                        // timeout
                        // The scanner did not respond in a timely fashion.  Check that the scanner is still on, and that there is a good connection to the network.
                        return
                        (
                            a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errTimeout") + ".\n" +
                            "\n" +
                            Config.GetResource(m_resourcemanager, "errTimeoutFix")
                        );
                    case "CreateSession":
                        // timeout
                        // The scanner did not respond in a timely fashion. Check that the scanner is still on, and that there is a good connection to the network.  Since this happened for 'createSession' it's possible the scanner was in a sleep state, and took too long to wake up.  Trying a second time may be successful.
                        return
                        (
                            a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errTimeout") + ".\n" +
                            "\n" +
                            Config.GetResource(m_resourcemanager, "errTimeoutWakeupFix")
                        );
                }
            }

            // Load the JSON, if that blows up in our face, then give the user the raw data...
            jsonlookup = new JsonLookup();
            if (!jsonlookup.Load(a_szHttpResponseData, out lJsonErrorIndex))
            {
                // json error, index=
                // We have a response from the scanner, but it can't be parsed due to an error in its construction.  The index shows where we ran into trouble.  What follows is the raw data we received.
                return
                (
                    a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errJsonErrorIndex") + lJsonErrorIndex + ".\n" +
                    "\n" +
                    Config.GetResource(m_resourcemanager, "errJsonErrorIndexFix") + "\n" +
                    "\n" +
                    a_szHttpResponseData
                );
            }

            // We have two codes to check, there's the protocol and
            // the session.  Check if the protocol reports success,
            // if so, get the session data...
            string szCode;
            if (jsonlookup.Get("results.success") == "true")
            {
                szCode = jsonlookup.Get("results.session.status.detected");
            }
            // We seem to have a protocol problem, so sort that out...
            else
            {
                szCode = jsonlookup.Get("results.code");
            }

            // If we don't have an error code, tell them about it...
            if (string.IsNullOrEmpty(szCode))
            {
                // missing error code
                // When an error occurs we expect to receive a code.  The response does not have this code, so we can't tell what error happened What follows is the raw data we received.
                return
                (
                    a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errMissingErrorCode") + ".\n" +
                    "\n" +
                    Config.GetResource(m_resourcemanager, "errMissingErrorCodeFix") + "\n" +
                    "\n" +
                    a_szHttpResponseData
                );
            }

            // Handle the errors...
            switch (szCode)
            {
                default:
                    // unrecognized error code
                    // We don't have any advice to offer for this error, it may be custom to the scanner, or this application may not be recent enough to know about it.  What follows is the raw data we received.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errUnrecognizedErrorCode") + " - " + szCode + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errUnrecognizedErrorCodeFix") + "\n" +
                        "\n" +
                        a_szHttpResponseData
                    );

                case "newSessionNotAllowed":
                    // new session not allowed
                    // The scanner may be in use by another user.  Or it may be turned off or disconnected.  Check to to see which it is, and then retry.  If the problem persists, try turning the scanner off, wait a bit, and turn it back on. If you're using the TWAIN Bridge try exiting from it and restarting it.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errNewSessionNotAllowed") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errNewSessionNotAllowedFix")
                    );

                case "invalidSessionId":
                    // invalid session id
                    // Communication was lost or compromised.  Make sure this session is closed, and try to create a new session.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errInvalidSessionId") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errInvalidSessionIdFix")
                    );

                case "closedSession":
                    // closed session
                    // This session has been closed, and will not accept attempts to scan.  Drain all images to free the scanner.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errClosedSession") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errClosedSessionFix")
                    );

                case "notReady":
                    // not ready
                    // This session is not ready, and will not accept attempts to negotiate tasks.  Close this session and start a new one, if you want to continue.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errNotReady") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errNotReadyFix")
                    );

                case "notCapturing":
                    // not capturing
                    // This session is not capturing, and will not accept attempts to transfer images.  Close this session and start a new one, if you want to continue.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errNotCapturing") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errNotCapturingFix")
                    );

                case "invalidImageBlockNumber":
                    // invalid image block number
                    // The scanner was asked for image data that it can't provide.  Close this session and start a new one, if you want to continue.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errInvalidImageBlockNumber") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errInvalidImageBlockNumberFix")
                    );

                case "invalidCapturingOptions":
                    // invalid capturing options
                    // The scanner has rejected this task.  This only happens when the task requires specific capabilities from the scanner.  Either select a task that this scanner will accept, or change to a scanner that supports the features required by the current task.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errInvalidCapturingOptions") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errInvalidCapturingOptionsFix")
                    );

                case "busy":
                    // busy
                    // The scanner is busy working on another command.  Try again in a little bit.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errBusy") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errBusyFix")
                    );

                case "noMedia":
                    // no media
                    // There was no paper for the scanner to capture.  Please check that the paper is properly loaded, and try again.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errNoMedia") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errNoMediaFix")
                    );

                case "foldedCorner":
                    // folded corner
                    // The scanner was asked to detect problems with the paper being scanned, such as folded corners.  Please check the paper, and try again.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errFoldedCorner") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errFoldedCornerFix")
                    );

                case "coverOpen":
                    // cover open
                    // A cover or an interlock on the scanner is not properly secured.  Please check the scanner, and try again.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errCoverOpen") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errCoverOpenFix")
                    );

                case "doubleFeed":
                    // double feed
                    // The scanner was requested or defaulted to check for instances where two or more sheets pass through the automatic document feeder.  Check the paper to see if any are stuck together, and try again.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errDoubleFeed") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errDoubleFeedFix")
                    );

                case "paperJam":
                    // paper jam
                    // A paper jam has been reported.  Please clear the jam and try again.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errPaperJam") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errPaperJamFix")
                    );

                case "misfeed":
                    // misfeed
                    // A misfeed has occurred with the scanner.  This is a somewhat generic error, and could include communication errors.  Please resolve the problem, and try again.
                    return
                    (
                        a_szCommand + ": " + Config.GetResource(m_resourcemanager, "errMisfeed") + ".\n" +
                        "\n" +
                        Config.GetResource(m_resourcemanager, "errMisfeedFix")
                    );
            }
        }

        /// <summary>
        /// Open a scanner with the last selected DnssdDeviceInfo...
        /// </summary>
        private bool OpenScanner()
        {
            bool blSuccess;
            ApiCmd apicmd;

            // We need device info to proceed...
            if (m_dnssddeviceinfo == null)
            {
                return (false);
            }

            // Create a command context...
            apicmd = new ApiCmd(m_dnssddeviceinfo);

            // We need this to get the x-privet-token...
            blSuccess = m_twainlocalscannerclient.ClientInfo(ref apicmd);
            if (!blSuccess)
            {
                Log.Error("ClientInfo failed: " + apicmd.GetHttpResponseData());
                MessageBox.Show(ReportError("infoex", apicmd.GetHttpResponseData()), Config.GetResource(m_resourcemanager, "strFormScanTitle"));
                SetButtons(EBUTTONSTATE.CLOSED);
                return (false);
            }

            // Create session...
            blSuccess = m_twainlocalscannerclient.ClientScannerCreateSession(ref apicmd);
            if (!blSuccess)
            {
                Log.Error("ClientScannerCreateSession failed: " + apicmd.GetHttpResponseData());
                MessageBox.Show(ReportError("createSession", apicmd.GetHttpResponseData()), Config.GetResource(m_resourcemanager, "strFormScanTitle"));
                SetButtons(EBUTTONSTATE.CLOSED);
                return (false);
            }

            // Try to save this selection...
            try
            {
                File.WriteAllText
                (
                    Path.Combine(m_szWriteFolder, "selected"),
                    "{" +
                    "\"linkLocal\":\"" + m_dnssddeviceinfo.GetLinkLocal() + "\"," +
                    "\"ipv4\":\"" + m_dnssddeviceinfo.GetIpv4() + "\"," +
                    "\"ipv6\":\"" + m_dnssddeviceinfo.GetIpv6() + "\"" +
                    "}"
                );
            }
            catch (Exception exception)
            {
                Log.Error("OpenScanner failed to write <" + Path.Combine(m_szWriteFolder, "selected") + "> - " + exception.Message);
            }

            // Create a new command context...
            apicmd = new ApiCmd(m_dnssddeviceinfo);

            // Wait for events...
            blSuccess = m_twainlocalscannerclient.ClientScannerWaitForEvents(ref apicmd);
            if (!blSuccess)
            {
                // Log it, but stay open...
                Log.Error("ClientScannerWaitForEvents failed: " + apicmd.GetHttpResponseData());
                MessageBox.Show(ReportError("waitForEvents", apicmd.GetHttpResponseData()), Config.GetResource(m_resourcemanager, "strFormScanTitle"));
            }

            // Update the title bar...
            Text = "TWAIN Direct: Application (" + m_dnssddeviceinfo.GetLinkLocal() + ")";

            // Create the setup form...
            m_formsetup = new FormSetup(m_dnssddeviceinfo, m_twainlocalscannerclient, m_szWriteFolder, m_resourcemanager);

            // Clear the images folder...
            bool blWarnOnce = true;
            string szImagesFolder = Path.Combine(m_szWriteFolder, "images");
            string[] aszImages = Directory.GetFiles(szImagesFolder, "*.*");
            foreach (string szFile in aszImages)
            {
                try
                {
                    File.Delete(szFile);
                }
                catch (Exception exception)
                {
                    if (blWarnOnce)
                    {
                        blWarnOnce = false;
                        // We were unable to delete the following file.  It may be open in another program.  Please close any applications that are using these files, and then close and reopen the session before scanning.
                        MessageBox.Show
                        (
                            Config.GetResource(m_resourcemanager, "errCantDeleteFile") +
                            "\n" +
                            szFile + "\n" +
                            "\n" +
                            exception.Message,
                            Config.GetResource(m_resourcemanager, "strFormScanTitle")
                        );
                    }
                }
            }

            // New state...
            SetButtons(EBUTTONSTATE.OPEN);

            // All done...
            return (true);
        }

        /// <summary>
        /// Update the summary text box...
        /// </summary>
        void UpdateSummary()
        {
            if (m_twainlocalscannerclient != null)
            {
                if (m_formsetup != null)
                {
                    if (m_twainlocalscannerclient.GetState() != "noSession")
                    {
                        m_textboxSummary.Text =
                            "state=" + m_twainlocalscannerclient.GetState() +
                            "; task='" + m_formsetup.GetTaskName() + "'" +
                            "; getthumbnails=" + m_formsetup.GetThumbnails().ToString().ToLowerInvariant() +
                            "; getmetadatawithimage=" + m_formsetup.GetMetadataWithImage().ToString().ToLowerInvariant();
                    }
                    else
                    {
                        m_textboxSummary.Text = "state=" + m_twainlocalscannerclient.GetState();
                    }
                }
                else
                {
                    m_textboxSummary.Text = "state=" + m_twainlocalscannerclient.GetState();
                }
            }
            else
            {
                m_textboxSummary.Text = "state=noSession";
            }
        }

        /// <summary>
        /// Bring up the folder containing the images...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_pictureboxImage_DoubleClick(object sender, EventArgs e)
        {
            string szImagesFolder = Path.Combine(m_szWriteFolder, "images");
            if (Directory.Exists(szImagesFolder))
            {
                System.Diagnostics.Process.Start(szImagesFolder);
            }
        }

        /// <summary>
        /// Open the PDF file, if we have one...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_textbox_DoubleClick(object sender, EventArgs e)
        {
            TextBox textbox = (TextBox)sender;
            string szPdfFile = Path.Combine(m_szWriteFolder, Path.Combine("images", textbox.Text));
            if (File.Exists(szPdfFile))
            {
                System.Diagnostics.Process.Start(szPdfFile);
            }
        }

        /// <summary>
        /// Bring up the strip menu of authentication choices...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cloudButton_Click(object sender, EventArgs e)
        {
            int menuPosition = cloudButton.Height;
            Point screenPoint = PointToScreen(new Point(cloudButton.Left, cloudButton.Bottom));

            if (screenPoint.Y + cloudMenuStrip.Size.Height > Screen.PrimaryScreen.WorkingArea.Height)
                menuPosition = -cloudMenuStrip.Size.Height;

            cloudMenuStrip.Show(cloudButton, new Point(0, menuPosition));
        }

        /// <summary>
        /// Process the selected authentication method...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void loginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var loginProvider = (sender as ToolStripMenuItem).Tag as string;
            var apiRoot = CloudManager.GetCloudApiRoot();
            var loginUrl = $"{apiRoot}/authentication/signin/" + loginProvider;

            var loginForm = new FacebookLoginForm(loginUrl);
            loginForm.Authorized += async (_, args) =>
            {
                loginForm.Close();

                m_twaincloudTokens = args.Tokens;

                var client = new TwainCloudClient(apiRoot, m_twaincloudTokens);
                await m_twainlocalscannerclient.ConnectToCloud(client);
                m_twainlocalscannerclient.m_dictionaryExtraHeaders.Add("Authorization", args.Tokens.AuthorizationToken);

            };

            loginForm.ShowDialog(this);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Definitions...

        /// <summary>
        /// States for the buttons...
        /// </summary>
        private enum EBUTTONSTATE
        {
            UNDEFINED,
            CLOSED,
            OPEN,
            SCANNING
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// Our TWAIN Local interface to the scanning api...
        /// </summary>
        private TwainLocalScannerClient m_twainlocalscannerclient;

        /// <summary>
        /// The thread we'll scan in, so that our UI remains
        /// responsive...
        /// </summary>
        private Thread m_threadClientScan;

        /// <summary>
        /// A place where we can write stuff...
        /// </summary>
        private string m_szWriteFolder;

        /// <summary>
        /// Our selected device...
        /// </summary>
        private Dnssd.DnssdDeviceInfo m_dnssddeviceinfo;

        /// <summary>
        /// List devices on the local area network...
        /// </summary>
        private Dnssd m_dnssd;

        /// <summary>
        /// Use if something really bad happens...
        /// </summary>
        private bool m_blExit;

        /// <summary>
        /// Signal that we want to stop capturing, which will
        /// try to get all of the pending images, or abort
        /// capturing, which means we want to leave as quickly
        /// as possible...
        /// </summary>
        private bool m_blStopCapturing;
        private bool m_blAbortCapturing;

        /// <summary>
        /// Password values we'd like to remember...
        /// </summary>
        private string m_szPassword;
        private bool m_blShowPassword;

        /// <summary>
        /// Our list of threads for ClientScanParallel...
        /// </summary>
        private List<Thread> m_listThreadClientScanParallel;

        /// <summary>
        /// Setup information...
        /// </summary>
        private FormSetup m_formsetup;

        /// <summary>
        /// Help with the look of the form...
        /// </summary>
        private float m_fScale;

        /// <summary>
        /// Stuff used to display the images...
        /// </summary>
        private Bitmap m_bitmapGraphic1;
        private Bitmap m_bitmapGraphic2;
        private Graphics m_graphics1;
        private Graphics m_graphics2;
        private Brush m_brushBackground;
        private Rectangle m_rectangleBackground;
        private int m_iUseBitmap;
        private TwainCloudTokens m_twaincloudTokens;

        // Where we get our localized strings...
        private ResourceManager m_resourcemanager;

        #endregion
    }
}
