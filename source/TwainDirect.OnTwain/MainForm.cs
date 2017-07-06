///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirect.OnTwain.MainForm
//
//  This is our interactive dialog that allows users to select the TWAIN driver
//  they want to use and experiment with SWORD tasks.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    16-Jun-2014     Initial Release
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
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using TwainDirect.Support;

namespace TwainDirect.OnTwain
{
    /// <summary>
    /// Our main form...
    /// </summary>
    public partial class MainForm : Form
    {
        // Our constructor...
        public MainForm()
        {
            string szScanner;

            // Init the form...
            InitializeComponent();
            this.Load += new EventHandler(MainForm_Load);
            this.FormClosing += new FormClosingEventHandler(MainForm_FormClosing);

            // Init stuff...
            m_processswordtask = null;

            // Remember this stuff...
            m_szExecutablePath = Config.Get("executablePath","");
            m_szReadFolder = Config.Get("readFolder", "");
            m_szWriteFolder = Config.Get("writeFolder", "");
            m_blWriteFolderNotNull = (Config.Get("writeFolder", null) != null);
            szScanner = Config.Get("scanner", null);

            // Check for a TWAIN driver, yelp if we can't find one...
            m_szTwainDefaultDriver = ProcessSwordTask.GetCurrentDriver(m_szWriteFolder, szScanner);
            if (m_szTwainDefaultDriver == null)
            {
                MessageBox.Show("No TWAIN drivers installed...", "Warning");
                SetButtonMode(ButtonMode.Disabled);
            }
        }

        /// <summary>
        /// Load the main dialog...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Double click...
            m_listviewTasks.MouseDoubleClick += new MouseEventHandler(m_listviewTasks_MouseDoubleClick);

            // Populate the listview with tasks...
            try
            {
                m_listviewTasks.Items.Clear();
                string[] files = Directory.GetFiles(Path.Combine(m_szWriteFolder,"tasks"));
                foreach (string file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    ListViewItem item = new ListViewItem(fileName);
                    item.Tag = file;
                    m_listviewTasks.Items.Add(item);
                }
                m_listviewTasks.View = View.Details;
                m_listviewTasks.Refresh();
                if (m_listviewTasks.Items.Count > 0)
                {
                    m_listviewTasks.Items[0].Selected = true;
                    m_listviewTasks.Select();
                }
            }
            catch
            {
                // Sorry, couldn't do the list.  We're probably missing the task folder...
                MessageBox.Show("Couldn't access the task folder");
            }
        }

        /// <summary>
        /// The user is trying to exit...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Log.Close();
            if (m_timer != null)
            {
                m_timer.Stop();
                m_timer = null;
            }
            if (m_processswordtask != null)
            {
                m_processswordtask.Close();
                m_processswordtask = null;
            }
        }
 
        /// <summary>
        /// A double-click is like pressing the Scan button...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_listviewTasks_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            m_buttonRun_Click(sender, e);
        }

        /// <summary>
        /// Run a task...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonRun_Click(object sender, EventArgs e)
        {
            bool blSuccess;
            bool blSetAppCapabilities = true;

            // Protection...
            if (m_processswordtask != null)
            {
                return;
            }

            // Init stuff...
            m_processswordtask = new ProcessSwordTask(Path.Combine(m_szWriteFolder,"images"), null, null);

            // Buttons off, cancel on...
            SetButtonMode(ButtonMode.Scanning);

            // Start the batch...
            blSuccess = m_processswordtask.BatchMode("", (string)m_listviewTasks.SelectedItems[0].Tag, false, ref blSetAppCapabilities);

            // Set up a timer to wait for completion...
            Timer m_timer = new Timer();
            m_timer.Interval = 1000;
            m_timer.Start();
            m_timer.Tick += new EventHandler(m_timer_Tick);

            // Yike...            
            m_listviewTasks.Focus();
        }

        /// <summary>
        /// Cancel a scanning session...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonCancel_Click(object sender, EventArgs e)
        {
            if (m_processswordtask != null)
            {
                SetButtonMode(ButtonMode.Canceled);
                m_processswordtask.Cancel();
            }
        }

        /// <summary>
        /// Wait for a scanning session to end...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_timer_Tick(object sender, EventArgs e)
        {
            // We still have a manager...
            if (m_processswordtask != null)
            {
                // Processing this action is done...
                if (!m_processswordtask.IsProcessing())
                {
                    // Cleanup...
                    if (m_timer != null)
                    {
                        m_timer.Enabled = false;
                        m_timer = null;
                    }
                    m_processswordtask.Close();
                    m_processswordtask = null;

                    // Turn the buttons on...
                    SetButtonMode(ButtonMode.Ready);
                }
            }
        }

        /// <summary>
        /// Let the user edit a task...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonEdit_Click(object sender, EventArgs e)
        {
            // Run some editor...
            Process process = Process.Start((string)m_listviewTasks.SelectedItems[0].Tag);
            process.WaitForExit();
            m_listviewTasks.Focus();
        }

        /// <summary>
        /// Let the user pick a TWAIN driver (this is Windows only, we'll need our
        /// own dialog box to support Linux and Mac OS X)...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonSelect_Click(object sender, EventArgs e)
        {
            // Let the user select a default driver...
            m_szTwainDefaultDriver = ProcessSwordTask.SelectDriver(m_szTwainDefaultDriver);
        }

        /// <summary>
        /// Manage our buttons in one place to reduce insanity...
        /// </summary>
        /// <param name="a_buttonmode">The mode we'd like to show</param>
        private void SetButtonMode(ButtonMode a_buttonmode)
        {
            switch (a_buttonmode)
            {
                default:
                case ButtonMode.Disabled:
                    m_buttonSelect.Enabled = false;
                    m_buttonEdit.Enabled = false;
                    m_buttonScan.Enabled = false;
                    m_buttonCancel.Enabled = false;
                    m_buttonScan.Visible = true;
                    AcceptButton = null;
                    break;

                case ButtonMode.Ready:
                    m_buttonSelect.Enabled = true;
                    m_buttonEdit.Enabled = true;
                    m_buttonScan.Enabled = true;
                    m_buttonCancel.Enabled = false;
                    m_buttonScan.Visible = true;
                    AcceptButton = m_buttonScan;
                    break;

                case ButtonMode.Scanning:
                    m_buttonSelect.Enabled = false;
                    m_buttonEdit.Enabled = false;
                    m_buttonScan.Enabled = false;
                    m_buttonCancel.Enabled = true;
                    m_buttonScan.Visible = false;
                    AcceptButton = m_buttonCancel;
                    break;

                case ButtonMode.Canceled:
                    m_buttonSelect.Enabled = false;
                    m_buttonEdit.Enabled = false;
                    m_buttonScan.Enabled = false;
                    m_buttonCancel.Enabled = false;
                    m_buttonScan.Visible = false;
                    AcceptButton = m_buttonCancel;
                    break;
            }

            // Why the hell do I have this?
            Application.DoEvents();
        }

        /// <summary>
        /// Private attributes...
        /// </summary>
        private enum ButtonMode
        {
            Disabled,
            Ready,
            Scanning,
            Canceled
        }
        private string m_szExecutablePath;
        private string m_szReadFolder;
        private string m_szWriteFolder;
        private bool m_blWriteFolderNotNull;
        private Timer m_timer;
        private string m_szTwainDefaultDriver;
        private ProcessSwordTask m_processswordtask;
    }
}
