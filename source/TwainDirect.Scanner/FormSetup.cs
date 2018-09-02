using Microsoft.Win32;
using System;
using System.Diagnostics;
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
        public FormSetup(FormMain a_formmain, ResourceManager a_resourcemanager, Scanner a_scanner, bool a_blConfirmScan)
        {
            // Init the component...
            InitializeComponent();
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            Config.ElevateButton(m_buttonManageTwainLocal.Handle);

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

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Loads list of registered cloud devices.
        /// </summary>
        private void LoadRegisteredCloudDevices()
        {
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
            await m_formmain.RegisterCloud();
            LoadRegisteredCloudDevices();
        }

        private void m_CloudDevicesComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            m_scanner.SetCurrentCloudScanner(m_CloudDevicesComboBox.SelectedItem as CloudScanner);
        }

        /// <summary>
        /// Bring up the cloud console...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonManageCloud_Click(object sender, EventArgs e)
        {
            Process.Start("https://twain.hazybits.com");
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

        #endregion
    }
}
