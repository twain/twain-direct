///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirectApplication.FormSelect
//
//  This class helps us select a TWAIN Direct scanner that we wish to open.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Commen6
//  M.McLaughlin    21-Oct-2013     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2013-2016 Kodak Alaris Inc.
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
using System.Resources;
using System.Threading;
using System.Windows.Forms;
using TwainDirectSupport;

namespace TwainDirectApplication
{
    public partial class FormSelect : Form
{
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        // Our constructor...
        public FormSelect(Dnssd a_dnssd, float a_fScale, out bool a_blResult)
        {
            // Init stuff...
            InitializeComponent();
            m_listviewSelect.MouseDoubleClick += new MouseEventHandler(m_listviewSelect_MouseDoubleClick);

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
            string szCurrentUiCulture = "." + Thread.CurrentThread.CurrentUICulture.ToString();
            if (szCurrentUiCulture == ".en-US")
            {
                szCurrentUiCulture = "";
            }
            try
            {
                m_resourcemanager = new ResourceManager("TwainDirectApplication.WinFormStrings" + szCurrentUiCulture, typeof(FormSelect).Assembly);
            }
            catch
            {
                m_resourcemanager = new ResourceManager("TwainDirectApplication.WinFormStrings", typeof(FormSelect).Assembly);
            }
            m_buttonOpen.Text = m_resourcemanager.GetString("strButtonOpen");
            m_labelSelect.Text = m_resourcemanager.GetString("strLabelSelectScanner");
            this.Text = m_resourcemanager.GetString("strFormSelectTitle");

            // Init more stuff...
            m_dnssd = a_dnssd;

            // Start the monitor...
            m_dnssd.MonitorStart();

            // Load the list box...
            Thread.Sleep(1000);
            a_blResult = LoadScannerNames(false);

            // Put the focus on the select box...
            ActiveControl = m_listviewSelect;

            // Start our timer...
            m_timerLoadScannerNames = new System.Windows.Forms.Timer();
            m_timerLoadScannerNames.Tick += new EventHandler(TimerEventProcessor);
            m_timerLoadScannerNames.Interval = 15000;
            m_timerLoadScannerNames.Tag = this;
            m_timerLoadScannerNames.Start();
        }

        /// <summary>
        /// Cleanup stuff...
        /// </summary>
        public void Cleanup()
        {
            if (m_dnssd != null)
            {
                m_dnssd.MonitorStop();
                m_dnssd.Dispose();
                m_dnssd = null;
            }
            GC.SuppressFinalize(this);
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
            Dnssd.DnssdDeviceInfo[] adnssddeviceinfo;

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

            // Take a snapshot...
            adnssddeviceinfo = m_dnssd.GetSnapshot(a_blCompare ? m_adnssddeviceinfoCompare : null, out blUpdated);

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

            // We've no data...
            if (adnssddeviceinfo == null)
            {
                m_listviewSelect.Items.Add("*none*");
                SetButtons(ButtonState.Nodevices);
            }
            else
            {
                // Populate our driver list...
                foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in adnssddeviceinfo)
                {
                    ListViewItem listviewitem = new ListViewItem
                    (
                        new string[] {
                            dnssddeviceinfo.szTxtTy,
                            dnssddeviceinfo.szServiceName.Split(new string[] { ".", "\\." },StringSplitOptions.None)[0],
                            (dnssddeviceinfo.szTxtNote != null) ? dnssddeviceinfo.szTxtNote : "(no note)",
                            dnssddeviceinfo.szLinkLocal,
                            (dnssddeviceinfo.szIpv4 != null) ? dnssddeviceinfo.szIpv4 : (dnssddeviceinfo.szIpv6 != null) ? dnssddeviceinfo.szIpv6 : "(no ip)"
                        }
                    );
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
                    m_listviewSelect.Items[0].Selected = true;
                }

                // Try to match the last item we had, if we can't, then go to the top
                // of the list...
                else
                {
                    ii = 0;
                    bool blFound = false;
                    foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in adnssddeviceinfo)
                    {
                        if (   (dnssddeviceinfo.szTxtTy == m_dnssddeviceinfoSelected.szTxtTy)
                            && (dnssddeviceinfo.szServiceName == m_dnssddeviceinfoSelected.szServiceName)
                            && (dnssddeviceinfo.szLinkLocal == m_dnssddeviceinfoSelected.szLinkLocal)
                            && (dnssddeviceinfo.szIpv4 == m_dnssddeviceinfoSelected.szIpv4)
                            && (dnssddeviceinfo.szIpv6 == m_dnssddeviceinfoSelected.szIpv6))
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
            // Fix the buttons...
            switch (a_buttonstate)
            {
                default:
                case ButtonState.Undefined:
                    m_buttonOpen.Enabled = false;
                    break;

                case ButtonState.Nodevices:
                    m_buttonOpen.Enabled = false;
                    break;

                case ButtonState.Devices:
                    m_buttonOpen.Enabled = true;
                    break;
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
