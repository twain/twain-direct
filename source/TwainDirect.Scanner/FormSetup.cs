using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Resources;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using TwainDirect.Scanner.Storage;
using TwainDirect.Support;

namespace TwainDirect.Scanner
{
    internal partial class FormSetup : Form
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Init stuff...
        /// </summary>
        /// <param name="a_resourcemanager"></param>
        public FormSetup
        (
            FormMain a_formmain,
            ResourceManager a_resourcemanager,
            Scanner a_scanner,
            TwainLocalScannerDevice.DisplayCallback a_displaycallback,
            bool a_blConfirmScan
        )
        {
            // Init the component...
            InitializeComponent();
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            Config.ElevateButton(m_buttonManageTwainLocal.Handle);
            m_checkboxStartNpm.Checked = (Config.Get("startNpm", "true") == "true");
            this.FormClosing += new FormClosingEventHandler(FormSetup_FormClosing);
            m_displaycallback = a_displaycallback;

            // Populate the cloud api root combobox...
            int ii = 0;
            CloudManager.CloudInfo cloudinfo;
            m_comboboxCloudApiRoot.Items.Clear();
            for (cloudinfo = CloudManager.GetCloudInfo(ii);
                 cloudinfo != null;
                 cloudinfo = CloudManager.GetCloudInfo(++ii))
            {
                m_comboboxCloudApiRoot.Items.Add(cloudinfo.szName);
            }
            if (m_comboboxCloudApiRoot.Items.Count > 0)
            {
                m_comboboxCloudApiRoot.Text = (string)m_comboboxCloudApiRoot.Items[0];
            }

            // Remember stuff...
            m_formmain = a_formmain;
            m_resourcemanager = a_resourcemanager;
            m_scanner = a_scanner;
            m_checkboxConfirmation.Checked = a_blConfirmScan;

            // Localize...
            this.Text = Config.GetResource(m_resourcemanager, "strFormMainTitle"); // TWAIN Direct: TWAIN Bridge
            m_buttonManageTwainLocal.Text = Config.GetResource(m_resourcemanager, "strButtonTwainLocalManagerEllipsis"); // Manage Local...
            m_buttonCloudRegister.Text = Config.GetResource(m_resourcemanager, "strButtonCloudRegisterEllipsis"); // Register Cloud...
            m_buttonManageCloud.Text = Config.GetResource(m_resourcemanager, "strButtonManageCloudEllipsis"); // Manage Cloud...
            m_buttonRegister.Text = Config.GetResource(m_resourcemanager, "strButtonRegisterEllipsis"); // Register...
            m_checkboxRunOnLogin.Text = Config.GetResource(m_resourcemanager, "strCheckboxRunOnLogin"); // Run on login
            m_labelCurrentDriver.Text = Config.GetResource(m_resourcemanager, "strLabelCurrentDriver"); // Current Driver:
            m_labelCurrentNote.Text = Config.GetResource(m_resourcemanager, "strLabelCurrentNote"); // Current Note:

            // Set stuff...
            m_textboxCurrentDriver.Text = m_formmain.GetTwainLocalTy();
            m_textboxCurrentNote.Text = m_formmain.GetTwainLocalNote();

            LoadRegisteredCloudDevices();

            // Are we registered for autorun?
            string szValueName = null;
            string szCommand = "";
            RegistryKey registrykeyRun;

            // Check the local machine (needed for legacy)...
            registrykeyRun = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
            foreach (string sz in registrykeyRun.GetValueNames())
            {
                string szValue = Convert.ToString(registrykeyRun.GetValue(sz));
                if (szValue.ToLowerInvariant().Contains("twaindirect.scanner.exe"))
                {
                    szValueName = sz;
                    szCommand = szValue;
                    break;
                }
            }

            // Check the local user...
            if (string.IsNullOrEmpty(szValueName))
            {
                registrykeyRun = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
                foreach (string sz in registrykeyRun.GetValueNames())
                {
                    string szValue = Convert.ToString(registrykeyRun.GetValue(sz));
                    if (szValue.ToLowerInvariant().Contains("twaindirect.scanner.exe"))
                    {
                        szValueName = sz;
                        szCommand = szValue;
                        break;
                    }
                }
            }

            // Looks like we're set...
            if (!string.IsNullOrEmpty(szCommand))
            {
                m_checkboxRunOnLogin.Checked = true;
                if (szCommand.Contains("startmonitoring=true"))
                {
                    m_checkboxAdvertise.Checked = true;
                }
                if (szCommand.Contains("confirmscan=true"))
                {
                    m_checkboxConfirmation.Checked = true;
                }
            }

            // Okay, we can let this happen now...
            m_blSkipUpdatingTheRegistry = false;
        }

        /// <summary>
        /// Are we automatically advertising on startup?
        /// </summary>
        /// <returns>true if we should start the monitor</returns>
        public bool GetAdvertise()
        {
            return (m_checkboxAdvertise.Checked);
        }

        /// <summary>
        /// Should we ask for confirmation when scanning?
        /// </summary>
        /// <returns>true if we should ask for confirmation</returns>
        public bool GetConfirmation()
        {
            return (m_checkboxConfirmation.Checked);
        }

        /// <summary>
        /// Get the currently selected cloud...
        /// </summary>
        /// <returns>cloud user wants to go with</returns>
        public string GetCloudApiRoot()
        {
            return (m_comboboxCloudApiRoot.Text);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Cleanup when we're going away...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void FormSetup_FormClosing(object sender, FormClosingEventArgs e)
        {
            // If we have an npm process, make it go away...
            if (m_processNpm != null)
            {
                try
                {
                    m_processNpm.CloseMainWindow();
                }
                catch
                {
                    // Just keep going...
                }
                m_processNpm.Dispose();
                m_processNpm = null;
            }
        }

        /// <summary>
        /// Loads list of registered cloud devices.
        /// </summary>
        private void LoadRegisteredCloudDevices()
        {
            // Handle a chicken and egg problem...
            if (m_scanner == null)
            {
                return;
            }

            // We're good from this point on...
            m_CloudDevicesComboBox.Items.Clear();
            using (var context = new CloudContext())
            {
                // load registered scanners
                var scanners = context.Scanners.ToArray();
                m_CloudDevicesComboBox.Items.AddRange(scanners);

                // select the current one
                var currentScanner = m_scanner.GetCurrentCloudScanner();
                if (currentScanner != null)
                {
                    foreach (var s in scanners)
                    {
                        if (s.Id == currentScanner.Id)
                        {
                            m_CloudDevicesComboBox.SelectedItem = s;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// If we need an 'npm start' process, start it up...
        /// </summary>
        /// <param name="a_blForceRestart">close the outstanding npm</param>
        /// <returns>true if things went well</returns>
        private bool StartNpmIfNeeded(bool a_blForceRestart)
        {
            // Remove anything from the past...
            if (a_blForceRestart)
            {
                if (m_processNpm != null)
                {
                    try
                    {
                        m_processNpm.CloseMainWindow();
                    }
                    catch
                    {
                        // Just keep going...
                    }
                    m_processNpm.Dispose();
                    m_processNpm = null;
                }
            }
            // We already have one, stick with it...
            else if (m_processNpm != null)
            {
                return (true);
            }

            // Get our local cloud info...
            CloudManager.CloudInfo cloudinfo = CloudManager.GetCurrentCloudInfo();
            if (cloudinfo == null)
            {
                return (true);
            }

            // If we don't have a working directory, we're done...
            if (string.IsNullOrEmpty(cloudinfo.szTwainCloudExpressFolder) || !Directory.Exists(cloudinfo.szTwainCloudExpressFolder))
            {
                return (true);
            }

            // Okay, fortune favors the bold (or the complete nutters)...
            try
            {
                if (m_displaycallback != null)
                {
                    m_displaycallback(Config.GetResource(m_resourcemanager, "strTextStartNpm"));
                }
                ProcessStartInfo processstartinfo = new ProcessStartInfo();
                processstartinfo.UseShellExecute = true;
                processstartinfo.FileName = "npm";
                processstartinfo.Arguments = "start";
                processstartinfo.WorkingDirectory = cloudinfo.szTwainCloudExpressFolder;
                processstartinfo.WindowStyle = ProcessWindowStyle.Minimized;
                m_processNpm = Process.Start(processstartinfo);
                Thread.Sleep(10000);
            }
            catch (Exception exception)
            {

                if (m_displaycallback != null)
                {
                    m_displaycallback(Config.GetResource(m_resourcemanager, "strTextNpmFailed") + " - " + exception.Message);
                }
                Log.Error("npi start launch failed - " + exception.Message);
                if (m_processNpm != null)
                {
                    m_processNpm.Dispose();
                    m_processNpm = null;
                    return (false);
                }
            }

            // All done...
            return (true);
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
            bool blDevicesFound = m_formmain.DevicesFound();

            // Get the path to the manager...
            szTwainLocalManager = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "TwainDirect.Scanner.TwainLocalManager.exe");
            if (!File.Exists(szTwainLocalManager))
            {
                MessageBox.Show("TWAIN Local Manager is not installed on this system.", "Error");
                return;
            }

            // We're busy...
            Cursor.Current = Cursors.WaitCursor;
            m_formmain.SetButtons(FormMain.ButtonState.Undefined);
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
                m_formmain.SetButtons(blDevicesFound ? FormMain.ButtonState.WaitingForStart : FormMain.ButtonState.NoDevices);
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
            m_formmain.SetButtons(blDevicesFound ? FormMain.ButtonState.WaitingForStart : FormMain.ButtonState.NoDevices);
            Cursor.Current = Cursors.Default;
        }

        /// <summary>
        /// Register a device for use...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonRegister_Click(object sender, EventArgs e)
        {
            m_formmain.SelectScanner();
            m_textboxCurrentDriver.Text = m_formmain.GetTwainLocalTy();
            m_textboxCurrentNote.Text = m_formmain.GetTwainLocalNote();
        }

        /// <summary>
        /// Register a device with cloud infrastructure.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private async void m_buttonCloudRegister_Click(object sender, EventArgs e)
        {
            StartNpmIfNeeded(true);
            await m_formmain.RegisterCloud();
            LoadRegisteredCloudDevices();
        }

        private void m_CloudDevicesComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            m_scanner.SetCurrentCloudScanner(m_CloudDevicesComboBox.SelectedItem as CloudScanner);
        }

        private void m_CloudDevicesComboBox_DropDown(object sender, EventArgs e)
        {
            var comboBox = (ComboBox)sender;
            var width = comboBox.DropDownWidth;
            var font = comboBox.Font;

            var vertScrollBarWidth = comboBox.Items.Count > comboBox.MaxDropDownItems ? SystemInformation.VerticalScrollBarWidth : 0;
            var itemsList = comboBox.Items.Cast<object>().Select(item => item.ToString());

            width = itemsList.Select(s => TextRenderer.MeasureText(s, font).Width + vertScrollBarWidth).Concat(new[] { width }).Max();

            comboBox.DropDownWidth = width;
        }

        /// <summary>
        /// Bring up the cloud console...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonManageCloud_Click(object sender, EventArgs e)
        {
            StartNpmIfNeeded(false);
            CloudManager.CloudInfo cloudinfo = CloudManager.GetCurrentCloudInfo();
            if ((cloudinfo != null) && !string.IsNullOrEmpty(cloudinfo.szManager))
            {
                Process.Start(cloudinfo.szManager);
            }
            else
            {
                MessageBox.Show(Config.GetResource(m_resourcemanager, "errNoCloudManager"), Config.GetResource(m_resourcemanager, "strFormScanTitle"));
            }
        }

        /// <summary>
        /// Control autorun...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_checkboxRunOnLogin_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;

            // Denied!
            if (m_blSkipUpdatingTheRegistry)
            {
                return;
            }

            // If we're checked, add us...
            if (checkbox.Checked)
            {
                string szValueName = null;
                RegistryKey registrykeyRun;

                // Check the local machine (needed for legacy)...
                registrykeyRun = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
                foreach (string sz in registrykeyRun.GetValueNames())
                {
                    string szValue = Convert.ToString(registrykeyRun.GetValue(sz));
                    if (szValue.ToLowerInvariant().Contains("twaindirect.scanner.exe"))
                    {
                        szValueName = sz;
                        break;
                    }
                }

                // Check the local user...
                if (string.IsNullOrEmpty(szValueName))
                {
                    registrykeyRun = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
                    foreach (string sz in registrykeyRun.GetValueNames())
                    {
                        string szValue = Convert.ToString(registrykeyRun.GetValue(sz));
                        if (szValue.ToLowerInvariant().Contains("twaindirect.scanner.exe"))
                        {
                            szValueName = sz;
                            break;
                        }
                    }
                }

                // Add or update us...
                try
                {
                    Registry.SetValue
                    (
                        "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Run",
                        "TWAIN Direct: TWAIN Bridge",
                        "\"" + System.Reflection.Assembly.GetEntryAssembly().Location + "\" background=true" + 
                        (m_checkboxAdvertise.Checked ? " startmonitoring=true" : "") +
                        (m_checkboxConfirmation.Checked ? " confirmscan=true" : "")
                    );
                }
                catch (Exception exception)
                {
                    MessageBox.Show("Sorry, we couldn't turn on autostart.  Run this program as admin, and try again.", "Error");
                    Log.Error("Failed to add registry value - " + exception.Message);
                    checkbox.Checked = true;
                }
            }

            // Otherwise, remove us...
            else
            {
                bool blAdmin = false;
                string szValueName = null;
                RegistryKey registrykeyRun;

                // Are we running as admin?
                using (WindowsIdentity windowsidentity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal windowsprincipal = new WindowsPrincipal(windowsidentity);
                    if (windowsprincipal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        blAdmin = true;
                    }
                }

                // Check the local machine (needed for legacy)...
                registrykeyRun = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", blAdmin);
                foreach (string sz in registrykeyRun.GetValueNames())
                {
                    string szValue = Convert.ToString(registrykeyRun.GetValue(sz));
                    if (szValue.ToLowerInvariant().Contains("twaindirect.scanner.exe"))
                    {
                        szValueName = sz;
                        break;
                    }
                }

                // Check the local user...
                if (string.IsNullOrEmpty(szValueName))
                {
                    registrykeyRun = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                    foreach (string sz in registrykeyRun.GetValueNames())
                    {
                        string szValue = Convert.ToString(registrykeyRun.GetValue(sz));
                        if (szValue.ToLowerInvariant().Contains("twaindirect.scanner.exe"))
                        {
                            szValueName = sz;
                            break;
                        }
                    }
                }

                // Remove it...
                if (!string.IsNullOrEmpty(szValueName))
                {
                    try
                    {
                        registrykeyRun.DeleteValue(szValueName);
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show("Sorry, we couldn't turn off autostart.  Run this program as admin, and try again.", "Error");
                        Log.Error("Failed to delete registry value - " + exception.Message);
                        checkbox.Checked = true;
                    }
                }
            }
        }

        /// <summary>
        /// Modify the RunOnLogin command with the advertising update...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_checkboxAdvertise_CheckedChanged(object sender, EventArgs e)
        {
            m_checkboxRunOnLogin_CheckedChanged(m_checkboxRunOnLogin, null);
        }

        /// <summary>
        /// Modify the RunOnLogin command with the confirm scanning update...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_checkboxConfirmation_CheckedChanged(object sender, EventArgs e)
        {
            m_checkboxRunOnLogin_CheckedChanged(m_checkboxRunOnLogin, null);
        }

        /// <summary>
        /// Make this our new current item...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_comboboxCloudApiRoot_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox combobox = (ComboBox)sender;
            CloudManager.SetCloudApiRoot(combobox.Text);
            LoadRegisteredCloudDevices();
            CloudManager.CloudInfo cloudinfo = CloudManager.GetCurrentCloudInfo();
            m_checkboxStartNpm.Enabled = ((cloudinfo != null) && !string.IsNullOrEmpty(cloudinfo.szTwainCloudExpressFolder) && Directory.Exists(cloudinfo.szTwainCloudExpressFolder));
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// Our main form...
        /// </summary>
        private FormMain m_formmain;

        /// <summary>
        /// Our resource manager to help with localization...
        /// </summary>
        private ResourceManager m_resourcemanager;

        /// <summary>
        /// Scanner object to work with.
        /// </summary>
        private Scanner m_scanner;

        /// <summary>
        /// This prevents us from hammering the registry at startup...
        /// </summary>
        private bool m_blSkipUpdatingTheRegistry = true;

        /// <summary>
        /// If we need twain-cloud-express, we use this to run 'npm start'...
        /// </summary>
        private Process m_processNpm;

        /// <summary>
        /// If we need to display any text...
        /// </summary>
        TwainLocalScannerDevice.DisplayCallback m_displaycallback;

        #endregion
    }
}
