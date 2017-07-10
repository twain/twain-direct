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
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Resources;
using System.Threading;
using System.Windows.Forms;
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

            // Localize...
            string szCurrentUiCulture = "." + Thread.CurrentThread.CurrentUICulture.ToString();
            if (szCurrentUiCulture == ".en-US")
            {
                szCurrentUiCulture = "";
            }
            try
            {
                m_resourcemanager = new ResourceManager("TwainDirect.App.WinFormStrings" + szCurrentUiCulture, typeof(FormSelect).Assembly);
            }
            catch
            {
                m_resourcemanager = new ResourceManager("TwainDirect.App.WinFormStrings", typeof(FormSelect).Assembly);
            }
            m_buttonClose.Text = m_resourcemanager.GetString("strButtonClose");
            m_buttonOpen.Text = m_resourcemanager.GetString("strButtonOpen");
            m_buttonSelect.Text = m_resourcemanager.GetString("strButtonSelectEllipsis");
            m_buttonScan.Text = m_resourcemanager.GetString("strButtonScan");
            m_buttonSetup.Text = m_resourcemanager.GetString("strButtonSetupEllipsis");
            m_buttonStop.Text = m_resourcemanager.GetString("strButtonStop");
            this.Text = m_resourcemanager.GetString("strFormScanTitle");

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

            // Set up a data folder, in this instance we're assuming the project
            // name matches the binary, so we can quickly locate it...
            string szExecutableName = Config.Get("executableName", "");
            string szWriteFolder = Config.Get("writeFolder", "");

            // Turn on logging...
            Log.Open(szExecutableName, szWriteFolder, 1);
            Log.SetLevel((int)Config.Get("logLevel", 0));
            Log.Info(szExecutableName + " Log Started...");

            // Init our picture box...
            InitImage();

            // Init our buttons...
            SetButtons(EBUTTONSTATE.CLOSED);

            // Clear the picture boxes...
            LoadImage(ref m_pictureboxImage1, ref m_graphics1, ref m_bitmapGraphic1, null);
            LoadImage(ref m_pictureboxImage2, ref m_graphics2, ref m_bitmapGraphic2, null);

            // Create the mdns monitor, and start it...
            m_dnssd = new Dnssd(Dnssd.Reason.Monitor);
            m_dnssd.MonitorStart(null,IntPtr.Zero);

            // Get our TWAIN Local interface.
            m_twainlocalscanner = new TwainLocalScanner(null, 0, EventCallback, this, null, false);
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
        public bool ClientScan
        (
            ref bool a_blStopCapturing,
            bool a_blGetThumbnails,
            bool a_blGetMetadataWithImage,
            out ApiCmd a_apicmd
        )
        {
            bool blSuccess;
            bool blSuccessClientScan;
            long[] alImageBlocks = null;
            ApiCmd apicmd;

            // Capturing can be stopped by pressing the "stop" button, in which case
            // we shouldn't do it a second time.  This variable helps with that.
            m_blStopCapturing = false;

            // We want to return the first apicmd that has a problem, to do that we
            // need a bait-and-switch scheme that starts with making an object that
            // we'll return and then replace, if needed...
            a_apicmd = null; // it'll never be null, but the compiler needs comforting...
            apicmd = new ApiCmd(m_dnssddeviceinfo);
            blSuccessClientScan = true;

            // Clear the picture boxes, make sure we start with the left box...
            m_iUseBitmap = 0;
            LoadImage(ref m_pictureboxImage1, ref m_graphics1, ref m_bitmapGraphic1, null);
            LoadImage(ref m_pictureboxImage2, ref m_graphics2, ref m_bitmapGraphic2, null);

            // You won't find FormSetup.SetupMode.capturingOptions being handled
            // here, because its negotiation takes place entirely on the setup
            // dialog, so when we get here there's nothing left to do...

            // Send the task to the scanner... 
            m_twainlocalscanner.ClientScannerSendTask(m_formsetup.GetTask(), ref apicmd);
            blSuccess = m_twainlocalscanner.ClientCheckForApiErrors("ClientScannerSendTask", ref apicmd);
            if (!blSuccess)
            {
                a_apicmd = apicmd;
                return (false);
            }

            // Start capturing...
            m_twainlocalscanner.ClientScannerStartCapturing(ref apicmd);
            blSuccess = m_twainlocalscanner.ClientCheckForApiErrors("ClientScannerStartCapturing", ref apicmd);
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
                if (!blSuccess || m_twainlocalscanner.ClientGetImageBlocksDrained())
                {
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
                    blSuccess = m_twainlocalscanner.ClientWaitForSessionUpdate(long.MaxValue);
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

                    // If we have an imageBlock pop out, we're going to transfer it...
                    alImageBlocks = m_twainlocalscanner.ClientGetImageBlocks();
                    if ((alImageBlocks != null) && (alImageBlocks.Length > 0))
                    {
                        break;
                    }
                }

                // Scoot if the scanner says it's done sending images, or if
                // we've received a failure status...
                if (!blSuccess || m_twainlocalscanner.ClientGetImageBlocksDrained())
                {
                    break;
                }

                // Loop on each image until we exhaust the imageBlocks array...
                while (true)
                {
                    // We have the option to skip getting the metadata with this
                    // call.  An application should get metadata if it wants to
                    // examine it before getting the image.  If it always wants
                    // the image, it really doesn't need this step to be separate,
                    // which will save us a round-trip on the network...
                    if (!a_blGetMetadataWithImage)
                    {
                        m_twainlocalscanner.ClientScannerReadImageBlockMetadata(alImageBlocks[0], a_blGetThumbnails, ImageBlockMetadataCallback, ref apicmd);
                        blSuccess = m_twainlocalscanner.ClientCheckForApiErrors("ClientScannerReadImageBlockMetadata", ref apicmd);
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

                    // Get the first image block in the array...
                    m_twainlocalscanner.ClientScannerReadImageBlock(alImageBlocks[0], a_blGetMetadataWithImage, ImageBlockCallback, ref apicmd);
                    blSuccess = m_twainlocalscanner.ClientCheckForApiErrors("ClientScannerReadImageBlock", ref apicmd);
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

                    // Release the image...
                    m_twainlocalscanner.ClientScannerReleaseImageBlocks(alImageBlocks[0], alImageBlocks[0], ref apicmd);
                    blSuccess = m_twainlocalscanner.ClientCheckForApiErrors("ClientScannerReleaseImageBlocks", ref apicmd);
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

                    // If we're out of imageBlocks, exit this loop...
                    alImageBlocks = m_twainlocalscanner.ClientGetImageBlocks();
                    if ((alImageBlocks == null) || (alImageBlocks.Length == 0))
                    {
                        break;
                    }
                }
            }

            // Stop capturing...
            if (!a_blStopCapturing)
            {
                a_blStopCapturing = true;
                m_twainlocalscanner.ClientScannerStopCapturing(ref apicmd);
                blSuccess = m_twainlocalscanner.ClientCheckForApiErrors("ClientScannerStopCapturing", ref apicmd);
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
            while (   (m_twainlocalscanner.ClientGetSessionState() == "capturing")
                   || (m_twainlocalscanner.ClientGetSessionState() == "draining"))
            {
                m_twainlocalscanner.ClientScannerReleaseImageBlocks(1, long.MaxValue, ref apicmd);
                blSuccess = m_twainlocalscanner.ClientCheckForApiErrors("ClientScannerReleaseImageBlocks", ref apicmd);
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
                        break;

                    // We've lost our session
                    case "sessionTimedOut":
                        formscan.SetButtons(EBUTTONSTATE.CLOSED);
                        MessageBox.Show("Your scanner session has timed out.", "Notification");
                        break;
                }
            }
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
            ApiCmd apicmd;

            // This will prevent ReportImage from doing anything stupid as we close...
            m_graphics1 = null;

            // Cleanup...
            SetButtons(EBUTTONSTATE.CLOSED);
            if (m_formsetup != null)
            {
                m_formsetup.Dispose();
                m_formsetup = null;
            }

            // Gracefully end our session with the scanner...
            if (m_twainlocalscanner != null)
            {
                // Close the session...
                apicmd = new ApiCmd(m_dnssddeviceinfo);
                m_twainlocalscanner.ClientScannerCloseSession(ref apicmd);

                // If we didn't go to nosession, blast the image blocks...
                if (    !string.IsNullOrEmpty(apicmd.GetSessionState())
                    &&  (apicmd.GetSessionState() == "draining"))
                {
                    apicmd = new ApiCmd(m_dnssddeviceinfo);
                    m_twainlocalscanner.ClientScannerReleaseImageBlocks(0, 999999999, ref apicmd);
                }
            }

            // Kill the monitor, if we have one...
            if (m_dnssd != null)
            {
                m_dnssd.MonitorStop();
                m_dnssd.Dispose();
                m_dnssd = null;
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
        }

        /// <summary>
        /// Start a scan session...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonScan_Click(object sender, EventArgs e)
        {
            bool blSuccess;
            ApiCmd apicmd;

            // Turn the buttons off while we're sending a task to the
            // scanner, and starting the capture of images...
            SetButtons(EBUTTONSTATE.UNDEFINED);

            // This does all the interesting bits.  The next change
            // to the buttons will occur in this function, if we
            // successfully transition to the capturing state...
            blSuccess = ClientScan
            (
                ref m_blStopCapturing,
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
                        Log.Error(aszDescriptions[0]);
                        m_buttonClose_Click(null, null);
                        MessageBox.Show(aszDescriptions[0]);
                        break;
                    
                    // All HTTP errors that get to this point end the session...
                    case ApiCmd.ApiErrorFacility.httpstatus:
                        Log.Error(aszDescriptions[0]);
                        m_buttonClose_Click(null, null);
                        MessageBox.Show(aszDescriptions[0]);
                        break;

                    // Somebody didn't code stuff properly, end the session, because
                    // this kind of error could otherwise hang up the application...
                    case ApiCmd.ApiErrorFacility.security:
                        Log.Error(aszDescriptions[0]);
                        m_buttonClose_Click(null, null);
                        MessageBox.Show(aszDescriptions[0]);
                        break;

                    // We have an error in the TWAIN Local procotol, it's up
                    // to the ClientScan function to make sure we got back to
                    // a ready state...
                    case ApiCmd.ApiErrorFacility.protocol:
                        Log.Error(aszDescriptions[0]);
                        MessageBox.Show(aszDescriptions[0]);
                        break;

                    // We have an error in the TWAIN Direct language...
                    case ApiCmd.ApiErrorFacility.language:
                        foreach (string sz in aszDescriptions)
                        {
                            Log.Error(sz);
                            MessageBox.Show(sz);
                        }
                        break;
                }
            }

            // We're in good shape, set the buttons to allow more scanning...
            SetButtons(EBUTTONSTATE.OPEN);
        }

        /// <summary>
        /// Our scan callback, used to access the metadata...
        /// </summary>
        /// <param name="a_szMetadata">metadata file</param>
        /// <returns>true on success</returns>
        public bool ImageBlockMetadataCallback(string a_szMetadata)
        {
            // All done...
            return (true);
        }

        /// <summary>
        /// Our scan callback, used to display images...
        /// </summary>
        /// <param name="a_szImage">image file</param>
        /// <returns>true on success</returns>
        public bool ImageBlockCallback(string a_szImage)
        {
            bool blGotImage = false;

            // We have no image...
            if (!File.Exists(a_szImage))
            {
                return (blGotImage);
            }

            // We might have an image...
            FileInfo fileinfo = null;
            try
            {
                fileinfo = new FileInfo(a_szImage);
            }
            catch
            {
                return (blGotImage);
            }

            // Prep for the next image...
            if (fileinfo.Length > 200)
            {
                byte[] abImage;

                // Just for now...
                blGotImage = true;

                // Convert the beastie...
                abImage = PdfRaster.ConvertPdfToTiffOrJpeg(a_szImage);
                if (abImage == null)
                {
                    // Ew...
                    return (blGotImage);
                }

                // Get the image data...
                using (var bitmap = new Bitmap(new MemoryStream(abImage)))
                {
                    // Display the image...
                    if (m_iUseBitmap == 0)
                    {
                        m_iUseBitmap = 1;
                        LoadImage(ref m_pictureboxImage1, ref m_graphics1, ref m_bitmapGraphic1, bitmap);
                    }
                    else
                    {
                        m_iUseBitmap = 0;
                        LoadImage(ref m_pictureboxImage2, ref m_graphics2, ref m_bitmapGraphic2, bitmap);
                    }
                }
            }

            // Delete it...
            else
            {
                try
                {
                    File.Delete(a_szImage);
                }
                catch
                {
                    // Delete failed, but we don't care...
                }
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Load an image into a picture box, maintain its aspect ratio...
        /// </summary>
        /// <param name="a_picturebox"></param>
        /// <param name="a_graphics"></param>
        /// <param name="a_bitmapGraphic"></param>
        /// <param name="a_bitmap">bitmap to display or null to clear it</param>
        private void LoadImage(ref PictureBox a_picturebox, ref Graphics a_graphics, ref Bitmap a_bitmapGraphic, Bitmap a_bitmap)
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
                Invoke(new MethodInvoker(delegate() {LoadImage(ref picturebox, ref graphics, ref bitmapGraphic, a_bitmap);}));
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
            // Make sure we're running in the proper thread...
            if (this.InvokeRequired)
            {
                Invoke(new MethodInvoker(delegate() { SetButtons(a_ebuttonstate); }));
                return;
            }

            // Fix the buttons...
            switch (a_ebuttonstate)
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
                    m_buttonClose.Enabled = false;
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
            bool blSuccess;
            long lJsonErrorIndex;
            string szSelected = null;
            string szSelectedFile;
            string szLinkLocal;
            string szIpv4;
            string szIpv6;
            JsonLookup jsonlookupSelected;
            Dnssd.DnssdDeviceInfo[] adnssddeviceinfo;

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

            // Grab a snapshot of what's out there...
            adnssddeviceinfo = m_dnssd.GetSnapshot(null, out blUpdated);
            if ((adnssddeviceinfo == null) || (adnssddeviceinfo.Length == 0))
            {
                MessageBox.Show("There are no TWAIN Direct scanners available at this time.");
                SetButtons(EBUTTONSTATE.CLOSED);
                return;
            }

            // Find our entry...
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

            // No joy...
            if (m_dnssddeviceinfo == null)
            {
                Log.Error("m_buttonOpen_Click: selected not found - <" + szSelected + ">");
                m_buttonSelect_Click(sender, e);
                return;
            }

            // We got something, so open the scanner...
            OpenScanner();
        }

        /// <summary>
        /// Shutdown...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonClose_Click(object sender, EventArgs e)
        {
            ApiCmd apicmd = new ApiCmd(m_dnssddeviceinfo);

            // Init stuff...
            m_blStopCapturing = false;

            // Buttons off...
            SetButtons(EBUTTONSTATE.UNDEFINED);

            // Bye-bye to the form...
            if (m_formsetup != null)
            {
                m_formsetup.Dispose();
                m_formsetup = null;
            }

            // Close session...
            if (!m_twainlocalscanner.ClientScannerCloseSession(ref apicmd))
            {
                Log.Error("ClientScannerCloseSession failed: " + apicmd.HttpResponseData());
                MessageBox.Show("ClientScannerCloseSession failed, the reason follows:\n\n" + apicmd.HttpResponseData(), "Error");
                // We're going to close anyways...
            }

            // Buttons off...
            SetButtons(EBUTTONSTATE.CLOSED);

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
            m_dnssddeviceinfo = null;

            // Buttons off...
            SetButtons(EBUTTONSTATE.UNDEFINED);

            // Instantiate our selection form with the list of devices, and
            // wait for the user to pick something...
            dialogresult = DialogResult.Cancel;
            formselect = new FormSelect(m_dnssd, m_fScale, out blSuccess);
            if (!blSuccess)
            {
                SetButtons(EBUTTONSTATE.CLOSED);
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
                MessageBox.Show("No device selected...");
                SetButtons(EBUTTONSTATE.CLOSED);
                formselect = null;
                return;
            }

            // Open, open the beastie...
            OpenScanner();
        }

        /// <summary>
        /// Request that scanning stop (gracefully)...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonStop_Click(object sender, EventArgs e)
        {
            ApiCmd apicmd;

            // Only do this once...
            if (m_blStopCapturing)
            {
                return;
            }
            m_blStopCapturing = true;

            // Stop capturing...
            apicmd = new ApiCmd(m_dnssddeviceinfo);
            if (!m_twainlocalscanner.ClientScannerStopCapturing(ref apicmd))
            {
                Log.Error("ClientScannerStopCapturing failed: " + apicmd.HttpResponseData());
                MessageBox.Show("ClientScannerStopCapturing failed, the reason follows:\n\n" + apicmd.HttpResponseData(), "Error");
                return;
            }

            // Buttons off...
            SetButtons(EBUTTONSTATE.UNDEFINED);
        }

        /// <summary>
        /// Open a scanner with the last selected DnssdDeviceInfo...
        /// </summary>
        private void OpenScanner()
        {
            bool blSuccess;
            ApiCmd apicmd;

            // Create a command context...
            apicmd = new ApiCmd(m_dnssddeviceinfo);

            // We need this to get the x-privet-token...
            blSuccess = m_twainlocalscanner.ClientInfo(ref apicmd);
            if (!blSuccess)
            {
                Log.Error("ClientInfo failed: " + apicmd.HttpResponseData());
                MessageBox.Show("ClientInfo failed, the reason follows:\n\n" + apicmd.HttpResponseData(), "Error");
                SetButtons(EBUTTONSTATE.CLOSED);
                return;
            }

            // Create session...
            blSuccess = m_twainlocalscanner.ClientScannerCreateSession(ref apicmd);
            if (!blSuccess)
            {
                Log.Error("ClientScannerCreateSession failed: " + apicmd.HttpResponseData());
                MessageBox.Show("ClientScannerCreateSession failed, the reason follows:\n\n" + apicmd.HttpResponseData(), "Error");
                SetButtons(EBUTTONSTATE.CLOSED);
                return;
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
            blSuccess = m_twainlocalscanner.ClientScannerWaitForEvents(ref apicmd);
            if (!blSuccess)
            {
                // Log it, but stay open...
                Log.Error("ClientScannerWaitForEvents failed: " + apicmd.HttpResponseData());
                MessageBox.Show("ClientScannerWaitForEvents failed, the reason follows:\n\n" + apicmd.HttpResponseData(), "Error");
            }

            // New state...
            SetButtons(EBUTTONSTATE.OPEN);

            // Update the title bar...
            Text = "TWAIN Direct: Application (" + m_dnssddeviceinfo.GetLinkLocal() + ")";

            // Create the setup form...
            m_formsetup = new FormSetup(m_dnssddeviceinfo, m_twainlocalscanner);
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
        private TwainLocalScanner m_twainlocalscanner;

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
        /// Localized text...
        /// </summary>
        private ResourceManager m_resourcemanager;

        /// <summary>
        /// Use if something really bad happens...
        /// </summary>
        private bool m_blExit;

        /// <summary>
        /// Signal that we want to stop capturing...
        /// </summary>
        private bool m_blStopCapturing;

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

        #endregion
    }
}
