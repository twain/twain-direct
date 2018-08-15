///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirect.App.FormSelect
//
//  This class helps us select a TWAIN Direct scanner that we wish to open.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Commen6
//  M.McLaughlin    21-Oct-2013     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2013-2018 Kodak Alaris Inc.
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
using System.IO;
using System.Resources;
using System.Threading;
using System.Windows.Forms;
using HazyBits.Twain.Cloud.Application;
using HazyBits.Twain.Cloud.Client;
using HazyBits.Twain.Cloud.Registration;
using TwainDirect.Support;

namespace TwainDirect.App
{
    public partial class FormSelect : Form
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// 
        /// </summary>
        /// <param name="a_dnssd">The mDNS info</param>
        /// <param name="a_fScale">change the scale of the form</param>
        /// <param name="cloudTokens">TWAIN Cloud access tokens</param>
        /// <param name="a_blFoundOne">return how we did</param>
        /// <param name="a_resourcemanager">localization</param>
        public FormSelect(Dnssd a_dnssd, float a_fScale, TwainCloudTokens cloudTokens, out bool a_blFoundOne, ResourceManager a_resourcemanager)
        {
            // Init stuff...
            InitializeComponent();
            Config.ElevateButton(m_buttonManageTwainLocal.Handle);
            m_listviewSelect.MouseDoubleClick += new MouseEventHandler(m_listviewSelect_MouseDoubleClick);
            m_resourcemanager = a_resourcemanager;

            // Listview Headers...
            m_listviewSelect.Columns.Add("Name");
            m_listviewSelect.Columns.Add("Device");
            m_listviewSelect.Columns.Add("Note");
            m_listviewSelect.Columns.Add("Address");
            m_listviewSelect.Columns.Add("IPv4/IPv6");

            // Scaling...
            if ((a_fScale > 0) && (a_fScale != 1))
            {
                this.Font = new Font(this.Font.FontFamily, this.Font.Size * a_fScale, this.Font.Style);
            }

            // Localize...
            m_buttonOpen.Text = Config.GetResource(m_resourcemanager, "strButtonOpen");
            m_labelSelect.Text = Config.GetResource(m_resourcemanager, "strLabelSelectScanner");
            this.Text = Config.GetResource(m_resourcemanager, "strFormSelectTitle");

            // Hang onto this...
            m_dnssd = a_dnssd;

            // Load the list box...
            Thread.Sleep(1000);
            a_blFoundOne = LoadScannerNames(false);

            // Put the focus on the select box...
            ActiveControl = m_listviewSelect;

            // Start our timer...
            m_timerLoadScannerNames = new System.Windows.Forms.Timer();
            m_timerLoadScannerNames.Tick += new EventHandler(TimerEventProcessor);
            m_timerLoadScannerNames.Interval = 15000;
            m_timerLoadScannerNames.Tag = this;
            m_timerLoadScannerNames.Start();

            a_blFoundOne |= LoadCloudScanners(cloudTokens);
        }

        private bool LoadCloudScanners(TwainCloudTokens cloudTokens)
        {
            // The user hasn't identified themselves...
            if (cloudTokens == null)
            {
                return (false);
            }

            var apiRoot = CloudManager.GetCloudApiRoot();
            var client = new TwainCloudClient(apiRoot, cloudTokens);
            var applicationManager = new ApplicationManager(client);

            applicationManager.GetScanners().ContinueWith(task =>
            {
                var scanners = task.Result;
                foreach (var s in scanners)
                {
                    ListViewItem listviewitem = new ListViewItem
                    (
                        new[] {
                            s.Name,
                            s.Description,
                            s.Manufacturer,
                            CloudManager.GetScannerCloudUrl(s),
                            "(no ip)"
                        }
                    );

                    listviewitem.Tag = s;
                    AddCloudScanner(listviewitem);

                    // Fix our buttons...
                    SetButtons(ButtonState.Devices);
                }
            });

            // All done, there's no guarantee that we have any
            // cloud scanners, but since the user identified
            // themself, we can proceed...
            return (true);
        }

        private void AddCloudScanner(ListViewItem item)
        {
            if (InvokeRequired)
            {
                Action<ListViewItem> action = AddCloudScanner;
                Invoke(action, item);
            }
            else
            {
                item.Group = m_listviewSelect.Groups["cloudScannersGroup"];
                m_listviewSelect.Items.Add(item);
            }
        }


        /// <summary>
        /// Cleanup stuff...
        /// </summary>
        public void Cleanup()
        {
            // We don't own this, so don't free it, but it's
            // not a bad practice to clear it...
            m_dnssd = null;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Load the scanner names...
        /// </summary>
        /// <param name="a_blCompare">if true, compare to our last snapshot</param>
        /// <returns>true if we updated</returns>
        bool LoadScannerNames(bool a_blCompare)
            {
                int ii;
                bool blUpdated = false;
                bool blNoMonitor = false;
                Dnssd.DnssdDeviceInfo[] adnssddeviceinfo;

                // TWAIN Local isn't running...
                if (m_dnssd == null)
                {
                    return (false);
                }

                // Make a note of our current selection, if we have one, we expect our
                // snapshot to exactly match what we have in the list, including the
                // order of the data...
                m_dnssddeviceinfoSelected = null;
                if (m_adnssddeviceinfoCompare != null)
                {
                    for (ii = 0; ii < m_listviewSelect.Items.Count; ii++)
                    {
                        if (m_listviewSelect.Items[ii].Selected && m_listviewSelect.Items[ii].Group == m_listviewSelect.Groups["localScannersGroup"])
                        {
                            m_dnssddeviceinfoSelected = m_adnssddeviceinfoCompare[ii];
                            break;
                        }
                    }
                }

                // Take a snapshot...
                adnssddeviceinfo = m_dnssd.GetSnapshot(a_blCompare ? m_adnssddeviceinfoCompare : null, out blUpdated, out blNoMonitor);

                // If we've been asked to compare to the previous snapshot,
                // and if we detect that no change occurred, we can scoot...
                if (a_blCompare && !blUpdated)
                {
                    return (false);
                }

                // Suspend updating...
                m_listviewSelect.BeginUpdate();

                // Start with a clean slate...
                m_listviewSelect.Items.Clear();

                // Handle TWAIN Local: we've no data...
                if (adnssddeviceinfo == null)
                {
                    if (blNoMonitor)
                    {
                        // Don't display anything, TWAIN Local is disabled...
                    }
                    else
                    {
                        // TWAIN Local is supported, we just didn't find any scanners...
                        var item = new ListViewItem("*none*");
                        item.Group = m_listviewSelect.Groups["localScannersGroup"];
                        m_listviewSelect.Items.Add(item);
                    }
                    SetButtons(ButtonState.Nodevices);
                }
                // Handle TWAIN Local: show what we have...
                else
                {
                    // Populate our driver list...
                    foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in adnssddeviceinfo)
                    {
                        ListViewItem listviewitem = new ListViewItem
                        (
                            new string[] {
                                dnssddeviceinfo.GetTxtTy(),
                                dnssddeviceinfo.GetServiceName().Split(new string[] { ".", "\\." },StringSplitOptions.None)[0],
                                (dnssddeviceinfo.GetTxtNote() != null) ? dnssddeviceinfo.GetTxtNote() : "(no note)",
                                dnssddeviceinfo.GetLinkLocal(),
                                (dnssddeviceinfo.GetIpv4() != null) ? dnssddeviceinfo.GetIpv4() : (dnssddeviceinfo.GetIpv6() != null) ? dnssddeviceinfo.GetIpv6() : "(no ip)"
                            }
                        );
                        listviewitem.Group = m_listviewSelect.Groups["localScannersGroup"];
                        m_listviewSelect.Items.Add(listviewitem);
                    }

                    // Fix our columns...
                    m_listviewSelect.Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                    m_listviewSelect.Columns[1].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                    m_listviewSelect.Columns[2].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                    m_listviewSelect.Columns[3].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                    m_listviewSelect.Columns[4].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);

                    // Select the first column, and make sure it has the focus...
                    if (m_dnssddeviceinfoSelected == null)
                    {
                        if ((m_listviewSelect.Items != null) && (m_listviewSelect.Items.Count > 0))
                        {
                            m_listviewSelect.Items[0].Selected = true;
                        }
                    }

                    // Try to match the last item we had, if we can't, then go to the top
                    // of the list...
                    else
                    {
                        ii = 0;
                        bool blFound = false;
                        foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in adnssddeviceinfo)
                        {
                            if (   (dnssddeviceinfo.GetTxtTy() == m_dnssddeviceinfoSelected.GetTxtTy())
                                && (dnssddeviceinfo.GetServiceName() == m_dnssddeviceinfoSelected.GetServiceName())
                                && (dnssddeviceinfo.GetLinkLocal() == m_dnssddeviceinfoSelected.GetLinkLocal())
                                && (dnssddeviceinfo.GetIpv4() == m_dnssddeviceinfoSelected.GetIpv4())
                                && (dnssddeviceinfo.GetIpv6() == m_dnssddeviceinfoSelected.GetIpv6()))
                            {
                                m_listviewSelect.Items[ii].Selected = true;
                                blFound = true;
                                break;
                            }
                            ii += 1;
                        }
                        if (!blFound)
                        {
                            m_listviewSelect.Items[0].Selected = true;
                        }
                    }

                    // Fix our buttons...
                    SetButtons(ButtonState.Devices);
                }

                // Resume updating...
                m_listviewSelect.EndUpdate();

                // Rememeber this...
                m_adnssddeviceinfoCompare = adnssddeviceinfo;

                // All done...
                return (true);
            }

            /// <summary>
            /// See if we have a change in our device list...
            /// </summary>
            /// <param name="myObject"></param>
            /// <param name="myEventArgs"></param>
            private void TimerEventProcessor(Object a_object, EventArgs a_eventargs)
            {
                System.Windows.Forms.Timer timer = (System.Windows.Forms.Timer)a_object;
                FormSelect formselect = (FormSelect)timer.Tag;
                formselect.LoadScannerNames(true);
            }

            /// <summary>
            /// Launch the TWAIN Local Manager...
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="e"></param>
            private void m_buttonManageTwainLocal_Click(object sender, EventArgs e)
            {
                string szTwainLocalManager;
                Process process;
                bool blDevicesFound = m_buttonOpen.Enabled;

                // Get the path to the manager...
                szTwainLocalManager = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "TwainDirect.Scanner.TwainLocalManager.exe");
                if (!File.Exists(szTwainLocalManager))
                {
                    MessageBox.Show("TWAIN Local Manager is not installed on this system.", "Error");
                    return;
                }

                // We're busy...
                Cursor.Current = Cursors.WaitCursor;
                SetButtons(ButtonState.Undefined);
                this.Refresh();

                // Launch it as admin...
                process = new Process();
                process.StartInfo.FileName = szTwainLocalManager;
                process.StartInfo.UseShellExecute = true;
                if (System.Environment.OSVersion.Version.Major >= 6)
                {
                    process.StartInfo.Verb = "runas";
                }
                try
                {
                    process.Start();
                }
                catch
                {
                    Log.Error("User chose not to run TwainLocalManager in elevated mode...");
                    SetButtons(blDevicesFound ? ButtonState.Devices : ButtonState.Nodevices);
                    Cursor.Current = Cursors.Default;
                    return;
                }

                // Wait for it to finish...
                Thread.Sleep(1000);
                try
                {
                    process.WaitForInputIdle();
                    this.Refresh();
                    process.WaitForExit();
                    process.Dispose();
                }
                catch (Exception exception)
                {
                    Log.Error("Error waiting for TwainLocalManager - " + exception.Message);
                }
                SetButtons(blDevicesFound ? ButtonState.Devices : ButtonState.Nodevices);
                MessageBox.Show("Please exit from the Twain Direct Application, and restart it.", "Installation Complete");
                Cursor.Current = Cursors.Default;
            }

            /// <summary>
            /// Select this as our driver and close the dialog...
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="e"></param>
            private void m_buttonOpen_Click(object sender, EventArgs e)
            {
                m_timerLoadScannerNames.Stop();
                this.DialogResult = DialogResult.OK;
            }

            /// <summary>
            /// Open the clicked item...
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="e"></param>
            private void m_listviewSelect_MouseDoubleClick(object sender, MouseEventArgs e)
            {
                m_timerLoadScannerNames.Stop();
                this.DialogResult = DialogResult.OK;
            }

            /// <summary>
            /// Which device have we selected?
            /// </summary>
            /// <returns>the one we've selected</returns>
            public Dnssd.DnssdDeviceInfo GetSelectedDevice()
            {
                int ii;

                // Handle TWAIN Cloud...
                if (m_listviewSelect.SelectedIndices.Count > 0)
                {
                    var item = m_listviewSelect.SelectedItems[0];
                    var scanner = (ScannerInformation)item.Tag;
                    if (scanner != null)
                    {
                        string szUrl = CloudManager.GetScannerCloudUrl(scanner);

                        Dnssd.DnssdDeviceInfo dnssddeviceinfo = new Dnssd.DnssdDeviceInfo();
                        dnssddeviceinfo.SetTxtHttps(true);
                        dnssddeviceinfo.SetLinkLocal(szUrl);
                        dnssddeviceinfo.SetCloud(true);
                        return (dnssddeviceinfo);
                    }
                }

                // Handle TWAIN Local...
                // Make a note of our current selection, if we have one, we expect our
                // snapshot to exactly match what we have in the list, including the
                // order of the data...
                m_dnssddeviceinfoSelected = null;
                if (m_adnssddeviceinfoCompare != null)
                {
                    for (ii = 0; ii < m_listviewSelect.Items.Count; ii++)
                    {
                        if (m_listviewSelect.Items[ii].Selected)
                        {
                            m_dnssddeviceinfoSelected = m_adnssddeviceinfoCompare[ii];
                            break;
                        }
                    }
                }

                // Return what we found...
                return (m_dnssddeviceinfoSelected);
            }

            /// <summary>
            /// Select and accept...
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="e"></param>
            private void m_listboxSelect_MouseDoubleClick(object sender, MouseEventArgs e)
            {
                m_timerLoadScannerNames.Stop();
                this.DialogResult = DialogResult.OK;
            }

            /// <summary>
            /// Configure our buttons to match our current state...
            /// </summary>
            /// <param name="a_ebuttonstate"></param>
            private void SetButtons(ButtonState a_buttonstate)
            {
                if (InvokeRequired)
                {
                    Action<ButtonState> action = SetButtons;
                    Invoke(action, a_buttonstate);
                }
                else
                {
                    // Fix the buttons...
                    switch (a_buttonstate)
                    {
                        default:
                        case ButtonState.Undefined:
                            m_buttonManageTwainLocal.Enabled = false;
                            m_buttonOpen.Enabled = false;
                            break;

                        case ButtonState.Nodevices:
                            m_buttonManageTwainLocal.Enabled = true;
                            m_buttonOpen.Enabled = false;
                            break;

                        case ButtonState.Devices:
                            m_buttonManageTwainLocal.Enabled = true;
                            m_buttonOpen.Enabled = true;
                            break;
                    }
                }

            }

            #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Definitions...

        private enum ButtonState
        {
            Undefined,
            Nodevices,
            Devices,
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        private System.Windows.Forms.Timer m_timerLoadScannerNames;
        private Dnssd m_dnssd;
        private Dnssd.DnssdDeviceInfo m_dnssddeviceinfoSelected;
        private Dnssd.DnssdDeviceInfo[] m_adnssddeviceinfoCompare;
        private ResourceManager m_resourcemanager;

        #endregion
    }
}
