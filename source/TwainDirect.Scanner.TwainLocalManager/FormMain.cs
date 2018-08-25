using Microsoft.Win32;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using System.Text;
using System.Windows.Forms;

namespace TwainDirect.Scanner.TwainLocalManager
{
    public partial class FormMain : Form
    {
        /// <summary>
        ///  Constructor for our main form...
        /// </summary>
        public FormMain()
        {
            // Init the form...
            InitializeComponent();

            // Are we running in reduced mode?
            string szTwainDirectApp = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            szTwainDirectApp = Path.Combine(szTwainDirectApp, "TwainDirect.App.exe");
            if (File.Exists(szTwainDirectApp))
            {
                m_groupboxSelfSignedCertificates.Enabled = false;
                m_labelRoot.Enabled = false;
                m_labelExchange.Enabled = false;
                m_labelUrlAcl.Enabled = false;
                m_richtextboxRoot.Enabled = false;
                m_richtextboxExchange.Enabled = false;
                m_richtextboxUrlAcl.Enabled = false;
                m_buttonDeleteCertificates.Enabled = false;
                m_buttonRefreshCertificates.Enabled = false;

                m_groupboxFirewall.Enabled = false;
                m_labelFirewall.Enabled = false;
                m_richtextboxFirewall.Enabled = false;
                m_buttonDeleteFirewall.Enabled = false;
                m_buttonCreateFirewall.Enabled = false;
            }

            // Init other stuff...
            m_szRootCertificateName = "TWAIN Direct Self-Signed Root Authority for " + Environment.MachineName; // Our root certificate
            m_szExchangeCertificateName = Environment.MachineName + ".local"; // The exchange name for this PC
            m_szTwainDirectScannerApp = "{aadc29dd-1d81-42f5-873d-5d89cf6e58ee}"; // TwainDirect.Scanner's GUID
            m_szPort = "34034"; // The port we'll be using

            // Init the certificate manager...
            m_managecertificates = new ManageCertificates(m_szRootCertificateName, m_szExchangeCertificateName, m_szTwainDirectScannerApp, m_szPort);

            // Update the form...
            UpdateBonjourInfoOnForm();
            UpdateCertificateInfoOnForm();
            UpdateFirewallInfoOnForm();
        }

        /// <summary>
        /// We don't want any button to have the focus when we come up...
        /// </summary>
        /// <param name="e"></param>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ActiveControl = null;
        }
        private void InstallBonjour_Click(object sender, EventArgs e)
        {
            // Prompt the user...
            DialogResult dialogresult = MessageBox.Show
            (
                this,
                "Clicking 'Yes' installs or updates your version of Apple's Bonjour" +
                "Service.  This is used by TWAIN Local to advertise and find scanners" + Environment.NewLine +
                "on your local area network.  It may be use by devices and programs" + Environment.NewLine +
                "on your PC." + Environment.NewLine +
                Environment.NewLine +
                "Would you like to continue?",
                "Install Bonjour",
                MessageBoxButtons.YesNo
            );

            // If yes, then do the refresh...
            if (dialogresult == DialogResult.Yes)
            {
                string szBonjourInstaller = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (Environment.Is64BitOperatingSystem)
                {
                    szBonjourInstaller = Path.Combine(Path.Combine(szBonjourInstaller, "data"), "Bonjour64.msi");
                    if (!File.Exists(szBonjourInstaller))
                    {
                        MessageBox.Show
                        (
                            "Could not find the 64-bit Bonjour Installer:" + Environment.NewLine +
                            szBonjourInstaller,
                            "Install Bonjour"
                        );
                        return;
                    }
                }
                else
                {
                    szBonjourInstaller = Path.Combine(Path.Combine(szBonjourInstaller, "data"), "Bonjour.msi");
                    if (!File.Exists(szBonjourInstaller))
                    {
                        MessageBox.Show
                        (
                            "Could not find the 32-bit Bonjour Installer:" + Environment.NewLine +
                            szBonjourInstaller,
                            "Install Bonjour"
                        );
                        return;
                    }
                }
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    Process process = new Process();
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.FileName = "msiexec.exe";
                    process.StartInfo.Arguments = "/i \"" + szBonjourInstaller + "\"";
                    process.Start();
                    process.WaitForExit();
                    process.Dispose();
                }
                catch
                {
                }
                UpdateBonjourInfoOnForm();
                Cursor.Current = Cursors.Default;
            }
        }

        private void UninstallBonjour_Click(object sender, EventArgs e)
        {
            // Prompt the user...
            DialogResult dialogresult = MessageBox.Show
            (
                this,
                "Clicking 'Yes' uninstalls Bonjour.  You should only do this if" + Environment.NewLine +
                "you installed Bonjour using this TWAIN Local Manager.  Do not" + Environment.NewLine +
                "uninstall Bonjour if you have other devices that depend on it." + Environment.NewLine +
                Environment.NewLine +
                "Would you like to continue?",
                "Uninstall Bonjour",
                MessageBoxButtons.YesNo
            );


            // If yes, then do the refresh...
            if (dialogresult == DialogResult.Yes)
            {
                string szBonjourInstaller = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (Environment.Is64BitOperatingSystem)
                {
                    szBonjourInstaller = Path.Combine(Path.Combine(szBonjourInstaller, "data"), "Bonjour64.msi");
                    if (!File.Exists(szBonjourInstaller))
                    {
                        MessageBox.Show
                        (
                            "Could not find the 64-bit Bonjour Installer:" + Environment.NewLine +
                            szBonjourInstaller,
                            "Install Bonjour"
                        );
                        return;
                    }
                }
                else
                {
                    szBonjourInstaller = Path.Combine(Path.Combine(szBonjourInstaller, "data"), "Bonjour.msi");
                    if (!File.Exists(szBonjourInstaller))
                    {
                        MessageBox.Show
                        (
                            "Could not find the 32-bit Bonjour Installer:" + Environment.NewLine +
                            szBonjourInstaller,
                            "Install Bonjour"
                        );
                        return;
                    }
                }
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    Process process = new Process();
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.FileName = "msiexec.exe";
                    process.StartInfo.Arguments = "/x \"" + szBonjourInstaller + "\"";
                    process.Start();
                    process.WaitForExit();
                    process.Dispose();
                    UpdateBonjourInfoOnForm();
                }
                catch
                {
                }
                Cursor.Current = Cursors.Default;
            }
        }

        /// <summary>
        /// Handle creating or refreshing our self-signed certificates...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CreateCertificates_Click(object sender, EventArgs e)
        {
            bool blSuccess;

            // Prompt the user...
            DialogResult dialogresult = MessageBox.Show
            (
                this,
                "Clicking 'Yes' replaces your TWAIN Direct self-signed certificates with" + Environment.NewLine +
                "new certificates.  You will have to install the new root certificate on" + Environment.NewLine +
                "any other computer accessing this scanner." + Environment.NewLine +
                Environment.NewLine +
                "Would you like to continue?",
                "Create Certificates",
                MessageBoxButtons.YesNo
            );

            // If yes, then do the refresh...
            if (dialogresult == DialogResult.Yes)
            {
                string szTwainDirectCertificates;
                SuspendLayout();
                m_managecertificates.CreateSelfSignedCertificates(out blSuccess, out szTwainDirectCertificates);
                UpdateCertificateInfoOnForm();
                ResumeLayout();
                if (blSuccess)
                {
                    MessageBox.Show
                    (
                        "Two copies of the self-signed root certificate have been placed in this folder: " + Environment.NewLine +
                        szTwainDirectCertificates + Environment.NewLine +
                        Environment.NewLine +
                        "The certificate ending in -pc.cer must be installed into the 'Trusted Root Certification Authorities', " +
                        "store in the 'Local Computer' location for each Windows PC needing access to this scanner." +
                        Environment.NewLine +
                        "The certificate ending in -android.der.crt must be copied to the Android's SD card, and installed using" +
                        "'Setup / Security / Install from device storage'.",
                        "Create Certificates"
                    );
                }
            }
        }

        /// <summary>
        /// Handle deleting our self-signed certificates...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteCertificates_Click(object sender, EventArgs e)
        {
            // Prompt the user...
            DialogResult dialogresult = MessageBox.Show
            (
                this,
                "Clicking 'Yes' deletes your TWAIN Direct self-signed certificates." + Environment.NewLine +
                "TWAIN Local scanning will no longer work." + Environment.NewLine +
                Environment.NewLine +
                "Would you like to continue?",
                "Delete Certificates",
                MessageBoxButtons.YesNo
            );

            // If yes, then do the refresh...
            if (dialogresult == DialogResult.Yes)
            {
                SuspendLayout();
                m_managecertificates.DeleteSelfSignedCertificates();
                UpdateCertificateInfoOnForm();
                ResumeLayout();
            }
        }

        private void CreateFirewall_Click(object sender, EventArgs e)
        {
            // Prompt the user...
            DialogResult dialogresult = MessageBox.Show
            (
                this,
                "Clicking 'Yes' creates an inbound firewall rule for the" + Environment.NewLine +
                "TwainDirect.Scanner program, so that TWAIN Local applications" + Environment.NewLine +
                "on other PCs can use your scanner." + Environment.NewLine +
                Environment.NewLine +
                "Would you like to continue?",
                "Create Firewall Rule",
                MessageBoxButtons.YesNo
            );

            // If yes, then do the refresh...
            if (dialogresult == DialogResult.Yes)
            {
                // Issue the command...
                string szFirewallBat = Path.Combine(Path.GetTempPath(), "twaindirectfirewall.bat");
                File.WriteAllText
                (
                    szFirewallBat,
                    "@echo off" + Environment.NewLine +
                    "netsh advfirewall firewall add rule name=TwainDirect.Scanner dir=in action=allow program=system enable=yes profile=any interfacetype=any protocol=tcp localport=" + m_szPort + " remoteport=any security=notrequired localip=any remoteip=any edge=yes"
                );
                ManageCertificates.RunBatchFile(szFirewallBat);
                File.Delete(szFirewallBat);

                // Update the form...
                SuspendLayout();
                UpdateFirewallInfoOnForm();
                ResumeLayout();
            }
        }

        private void DeleteFirewall_Click(object sender, EventArgs e)
        {
            // Prompt the user...
            DialogResult dialogresult = MessageBox.Show
            (
                this,
                "Clicking 'Yes' deletes the inbound firewall rule for the" + Environment.NewLine +
                "TwainDirect.Scanner program, preventing TWAIN Local applications" + Environment.NewLine +
                "on other PCs from using your scanner.  You will still be able" + Environment.NewLine +
                "to run TWAIN Local applications on this PC." + Environment.NewLine +
                Environment.NewLine +
                "Would you like to continue?",
                "Delete Firewall Rule",
                MessageBoxButtons.YesNo
            );

            // If yes, then do the refresh...
            if (dialogresult == DialogResult.Yes)
            {
                // Issue the command...
                string szFirewallBat = Path.Combine(Path.GetTempPath(), "twaindirectfirewall.bat");
                File.WriteAllText
                (
                    szFirewallBat,
                    "@echo off" + Environment.NewLine +
                    "netsh advfirewall firewall delete rule \"name=TwainDirect.Scanner\""
                );
                ManageCertificates.RunBatchFile(szFirewallBat);
                File.Delete(szFirewallBat);

                // Update the form...
                SuspendLayout();
                UpdateFirewallInfoOnForm();
                ResumeLayout();
            }
        }

        /// <summary>
        /// Update the Bonjour info displayed on the form...
        /// </summary>
        private void UpdateBonjourInfoOnForm()
        {
            string szStatus;
            string szVersion = "";
            string szImagePath;
            ServiceController servicecontroller;

            // Get the path to the executable from the registry...
            try
            {
                szImagePath = (string)Registry.GetValue
                (
                    "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Bonjour Service",
                    "ImagePath",
                    RegistryValueKind.String
                );
                szImagePath = szImagePath.Replace("\"", "");
            }
            catch
            {
                m_richtextboxStatus.Text = "Bonjour is not installed on this PC.";
                return;
            }

            // Grab the version info...
            try
            {
                FileVersionInfo fileversioninfo = FileVersionInfo.GetVersionInfo(szImagePath);
                szVersion = fileversioninfo.ProductVersion.Replace(",", ".");
            }
            catch
            {
                szVersion = "??.??";
            }

            // Can we find the service?
            try
            {
                servicecontroller = new ServiceController("Bonjour Service");
            }
            catch (Exception exception)
            {
                m_richtextboxStatus.Text = "Bonjour error: " + exception.Message;
                return;
            }
            if (servicecontroller == null)
            {
                m_richtextboxStatus.Text = "Bonjour is not installed on this PC.";
                return;
            }

            // So, what are we doing?
            try
            {
                switch (servicecontroller.Status)
                {
                    case ServiceControllerStatus.Running:
                        szStatus = "Running";
                        break;
                    case ServiceControllerStatus.Stopped:
                        szStatus = "Stopped";
                        break;
                    case ServiceControllerStatus.Paused:
                        szStatus = "Paused";
                        break;
                    case ServiceControllerStatus.StopPending:
                        szStatus = "Stopping";
                        break;
                    case ServiceControllerStatus.StartPending:
                        szStatus = "Starting";
                        break;
                    default:
                        szStatus = servicecontroller.Status.ToString();
                        break;
                }
            }
            catch
            {
                m_richtextboxStatus.Text = "Bonjour is not installed on this PC.";
                return;
            }

            // Report what we have...
            m_richtextboxStatus.Text = "Bonjour Service version " + szVersion + " is " + szStatus;
        }

        /// <summary>
        /// Update the firewall info displayed on the form...
        /// </summary>
        private void UpdateFirewallInfoOnForm()
        {
            string szNoAccess = "Other PCs on this local area network cannot access this PC's scanner.  If you'd like to change that, click on the 'Create Firewall Rule' button.";

            // Update the firewall info...
            try
            {
                // Issue the command...
                string szFirewallBat = Path.Combine(Path.GetTempPath(), "twaindirectfirewall.bat");
                File.WriteAllText
                (
                    szFirewallBat,
                    "@echo off" + Environment.NewLine +
                    "netsh advfirewall firewall show rule TwainDirect.Scanner"
                );
                string szOutput = ManageCertificates.RunBatchFile(szFirewallBat);
                File.Delete(szFirewallBat);

                // Tokenize...
                List<string> listSzTwainDirect = new List<string>();
                string[] aszLines = szOutput.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                // We have no rules...
                if (aszLines.Length <= 2)
                {
                    m_richtextboxFirewall.Text = szNoAccess;
                }

                // Show what we have...
                else
                {
                    m_richtextboxFirewall.Text = "Other PCs on this local area network can access this PC's scanner.";
                    foreach (string szLine in aszLines)
                    {
                        m_richtextboxFirewall.Text += Environment.NewLine + szLine;
                    }
                }
            }
            catch
            {
                m_richtextboxUrlAcl.Text = szNoAccess;
            }
        }

        /// <summary>
        /// Update the certificate info displayed on the form...
        /// </summary>
        private void UpdateCertificateInfoOnForm()
        {
            bool blSuccess;
            DateTime datetimeNotBefore;
            DateTime datetimeNotAfter;

            // Show the root certificate, if any...
            blSuccess = m_managecertificates.IsCertificateInstalled
            (
                StoreLocation.LocalMachine,
                StoreName.Root,
                m_szRootCertificateName,
                m_szRootCertificateName,
                out datetimeNotBefore,
                out datetimeNotAfter
            );
            if (!blSuccess)
            {
                m_richtextboxRoot.Text = "(no data)";
            }
            else
            {
                m_richtextboxRoot.Text = m_szRootCertificateName + ", " + datetimeNotBefore.ToShortDateString() + " to " + datetimeNotAfter.ToShortDateString();
            }

            // Show the exchange certificate, if any...
            blSuccess = m_managecertificates.IsCertificateInstalled
            (
                StoreLocation.LocalMachine,
                StoreName.My,
                m_szExchangeCertificateName,
                m_szRootCertificateName,
                out datetimeNotBefore,
                out datetimeNotAfter
            );
            if (!blSuccess)
            {
                m_richtextboxExchange.Text = "(no data)";
            }
            else
            {
                m_richtextboxExchange.Text = m_szExchangeCertificateName + ", " + datetimeNotBefore.ToShortDateString() + " to " + datetimeNotAfter.ToShortDateString();
            }

            // Update the URCACL info...
            try
            {
                m_richtextboxUrlAcl.Text = "(no data)";
                // Issue the command...
                string szUrlAclBat = Path.Combine(Path.GetTempPath(), "twaindirecturlacl.bat");
                File.WriteAllText(szUrlAclBat, "netsh http show urlacl");
                string szOutput = ManageCertificates.RunBatchFile(szUrlAclBat);
                File.Delete(szUrlAclBat);

                // Tokenize...
                List<string> listSzTwainDirect = new List<string>();
                string[] aszLines = szOutput.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                // Find the TWAIN Direct entry, if any, we shouldn't find more
                // than one, but let's be prepared for that possibility...
                foreach (string szLine in aszLines)
                {
                    if (szLine.ToLowerInvariant().Contains("/privet/twaindirect/session/") && szLine.ToLowerInvariant().Contains("https"))
                    {
                        listSzTwainDirect.Add(szLine.Substring(szLine.IndexOf("https")));
                    }
                }

                // Show what we found...
                m_richtextboxUrlAcl.Text = "";
                foreach (string szTwainDirect in listSzTwainDirect)
                {
                    int iIndex = szTwainDirect.IndexOf('/', 8);
                    if ((iIndex < 0) || ((iIndex + 1) >= szTwainDirect.Length))
                    {
                        continue;
                    }
                    string szPort = szTwainDirect.Remove(iIndex + 1).ToLowerInvariant();
                    foreach (string szLine in aszLines)
                    {
                        if (szLine.ToLowerInvariant().Contains(szPort))
                        {
                            if (!string.IsNullOrEmpty(m_richtextboxUrlAcl.Text))
                            {
                                m_richtextboxUrlAcl.Text += Environment.NewLine;
                            }
                            m_richtextboxUrlAcl.Text += szLine.ToLowerInvariant().Substring(szLine.IndexOf("https"));
                        }
                    }
                }

                // Ruh-roh...
                if (string.IsNullOrEmpty(m_richtextboxUrlAcl.Text))
                {
                    m_richtextboxUrlAcl.Text = "(no data)";
                }
            }
            catch
            {
                m_richtextboxUrlAcl.Text = "(no data)";
            }
        }

        /// <summary>
        /// Our helper object for managing certificates...
        /// </summary>
        private ManageCertificates m_managecertificates;

        /// <summary>
        /// The names of our root and exchange certificates...
        /// </summary>
        private string m_szRootCertificateName;
        private string m_szExchangeCertificateName;
        private string m_szTwainDirectScannerApp;
        private string m_szPort;
    }

    /// <summary>
    /// Manage the certificates, at this point this is self-signed only...
    /// </summary>
    internal class ManageCertificates
    {
        /// <summary>
        /// Squirrel away interesting stuff...
        /// </summary>
        /// <param name="a_szRootCertificateName">name of the root CA certificate</param>
        /// <param name="a_szExchangeCertificateName">.local name of the exchange certificate</param>
        public ManageCertificates(string a_szRootCertificateName, string a_szExchangeCertificateName, string a_szTwainDirectScannerApp, string a_szPort)
        {
            m_szRootCertificateName = a_szRootCertificateName;
            m_szExchangeCertificateName = a_szExchangeCertificateName;
            m_szTwainDirectScannerApp = a_szTwainDirectScannerApp;
            m_szPort = a_szPort;
        }

        /// <summary>
        /// Create our signed certificates.  We're going to make a root CA and an
        /// exchange certificate using that CA.  We delete any certificates with
        /// the same name and issuer, so we don't spam their store...
        /// </summary>
        public void CreateSelfSignedCertificates(out bool a_blSuccess, out string a_szTwainDirectCertificates)
        {
            AsymmetricKeyParameter caPrivateKey = null;
            string szPasswordRootCa = "1234";
            string szRootCN = "TWAIN Direct Self-Signed Root Authority for " + Environment.MachineName;
            a_blSuccess = true;

            // Make sure we have a place for the certificates...
            a_szTwainDirectCertificates = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "TWAIN Direct Certificates");
            if (!Directory.Exists(a_szTwainDirectCertificates))
            {
                try
                {
                    Directory.CreateDirectory(a_szTwainDirectCertificates);
                }
                catch
                {
                    a_blSuccess = false;
                    return;
                }
            }

            // Create the self-signed root certificate...
            byte[] Android;
            X509Certificate2 caCert = CreateCertificateAuthorityCertificate(szRootCN, out caPrivateKey, out Android);
            addCertToStore(caCert, StoreName.Root, StoreLocation.LocalMachine);
            File.WriteAllBytes(Path.Combine(a_szTwainDirectCertificates, szRootCN + "-pc.cer"), caCert.Export(X509ContentType.Cert, szPasswordRootCa));

            // Write the Android DER file...
            File.WriteAllBytes(Path.Combine(a_szTwainDirectCertificates, szRootCN + "-android.der.crt"), Android);

            // Create the self-signed exchange certificate...
            X509Certificate2 clientCert = CreateSelfSignedCertificateBasedOnCertificateAuthorityPrivateKey(Environment.MachineName + ".local", szRootCN, caPrivateKey);
            var p12 = clientCert.Export(X509ContentType.Pfx);
            addCertToStore(new X509Certificate2(p12, (string)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet), StoreName.My, StoreLocation.LocalMachine);

            // Update the URLACL using the new exchange thumbprint...
            UpdateUrlacl(clientCert.Thumbprint);
        }

        /// <summary>
        /// Delete our self-signed certificates...
        /// </summary>
        public void DeleteSelfSignedCertificates()
        {
            X509Store x509store;
            X509Certificate2Collection col;

            // Remove all occurrances of our root certificates with this name...
            x509store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            x509store.Open(OpenFlags.ReadWrite | OpenFlags.IncludeArchived);
            col = (X509Certificate2Collection)x509store.Certificates;
            foreach (var certTemp in col)
            {
                if (certTemp.Subject.Contains(m_szRootCertificateName) && (string.IsNullOrEmpty(certTemp.Issuer) || certTemp.Issuer.Contains(m_szRootCertificateName)))
                {
                    x509store.Remove(certTemp);
                }
            }
            x509store.Close();

            // Remove all occurrances of our exchange certificates with this name...
            x509store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            x509store.Open(OpenFlags.ReadWrite | OpenFlags.IncludeArchived);
            col = (X509Certificate2Collection)x509store.Certificates;
            foreach (var certTemp in col)
            {
                if (certTemp.Subject.Contains(m_szExchangeCertificateName) && certTemp.Issuer.Contains(m_szRootCertificateName))
                {
                    x509store.Remove(certTemp);
                }
            }
            x509store.Close();

            // Zap the URLACL...
            UpdateUrlacl(null);
        }

        /// <summary>
        /// Check if the certificate is installed...
        /// </summary>
        /// <param name="a_storelocation"></param>
        /// <param name="a_storename"></param>
        /// <param name="a_Name"></param>
        /// <param name="a_szIssuer"></param>
        /// <param name="a_datetimeNotBefore"></param>
        /// <param name="a_datetimeNotAfter"></param>
        /// <returns></returns>
        public bool IsCertificateInstalled
        (
            StoreLocation a_storelocation,
            StoreName a_storename,
            string a_Name,
            string a_szIssuer,
            out DateTime a_datetimeNotBefore,
            out DateTime a_datetimeNotAfter
        )
        {
            X509Store x509store;
            X509Certificate2Collection x509certificate2collection;

            // Find this certificate...
            x509store = new X509Store(a_storename, a_storelocation);
            x509store.Open(OpenFlags.ReadWrite | OpenFlags.IncludeArchived);
            x509certificate2collection = (X509Certificate2Collection)x509store.Certificates;
            foreach (var x509certificate2 in x509certificate2collection)
            {
                if (x509certificate2.Subject.Contains(a_Name))
                {
                    if (string.IsNullOrEmpty(x509certificate2.Issuer) || x509certificate2.Issuer.Contains(a_szIssuer))
                    {
                        a_datetimeNotBefore = x509certificate2.NotBefore;
                        a_datetimeNotAfter = x509certificate2.NotAfter;
                        return (true);
                    }
                }
            }

            // No joy...
            a_datetimeNotBefore = DateTime.MinValue;
            a_datetimeNotAfter = DateTime.MinValue;
            return (false);
        }

        public X509Certificate2 CreateSelfSignedCertificateBasedOnCertificateAuthorityPrivateKey(string subjectName, string issuerName, AsymmetricKeyParameter issuerPrivKey)
        {
            const int keyStrength = 2048;

            // Generating Random Numbers
            CryptoApiRandomGenerator randomGenerator = new CryptoApiRandomGenerator();
            SecureRandom random = new SecureRandom(randomGenerator);
            ISignatureFactory signatureFactory = new Asn1SignatureFactory("SHA256WITHRSA", issuerPrivKey, random);

            // The Certificate Generator
            X509V3CertificateGenerator certificateGenerator = new X509V3CertificateGenerator();
            certificateGenerator.AddExtension(X509Extensions.ExtendedKeyUsage.Id, true, new ExtendedKeyUsage(KeyPurposeID.IdKPServerAuth));

            // Serial Number
            BigInteger serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(Int64.MaxValue), random);
            certificateGenerator.SetSerialNumber(serialNumber);

            // Issuer and Subject Name
            X509Name subjectDN = new X509Name("CN=" + subjectName);
            X509Name issuerDN = new X509Name("CN=" + issuerName);
            certificateGenerator.SetIssuerDN(issuerDN);
            certificateGenerator.SetSubjectDN(subjectDN);

            // Valid For
            DateTime notBefore = DateTime.UtcNow.Date;
            DateTime notAfter = notBefore.AddYears(2);
            certificateGenerator.SetNotBefore(notBefore);
            certificateGenerator.SetNotAfter(notAfter);

            // Subject Public Key
            AsymmetricCipherKeyPair subjectKeyPair;
            var keyGenerationParameters = new KeyGenerationParameters(random, keyStrength);
            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(keyGenerationParameters);
            subjectKeyPair = keyPairGenerator.GenerateKeyPair();

            certificateGenerator.SetPublicKey(subjectKeyPair.Public);

            // selfsign certificate
            Org.BouncyCastle.X509.X509Certificate certificate = certificateGenerator.Generate(signatureFactory);
            var dotNetPrivateKey = ToDotNetKey((RsaPrivateCrtKeyParameters)subjectKeyPair.Private);

            // merge into X509Certificate2
            X509Certificate2 x509 = new X509Certificate2(DotNetUtilities.ToX509Certificate(certificate));
            x509.PrivateKey = dotNetPrivateKey;
            x509.FriendlyName = subjectName;

            return x509;
        }

        public X509Certificate2 CreateCertificateAuthorityCertificate(string subjectName, out AsymmetricKeyParameter CaPrivateKey, out byte[] Android)
        {
            const int keyStrength = 2048;

            // Generating Random Numbers
            CryptoApiRandomGenerator randomGenerator = new CryptoApiRandomGenerator();
            SecureRandom random = new SecureRandom(randomGenerator);

            // The Certificate Generator
            X509V3CertificateGenerator certificateGenerator = new X509V3CertificateGenerator();

            // Serial Number
            BigInteger serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(Int64.MaxValue), random);
            certificateGenerator.SetSerialNumber(serialNumber);

            // Issuer and Subject Name
            X509Name subjectDN = new X509Name("CN=" + subjectName);
            X509Name issuerDN = subjectDN;
            certificateGenerator.SetIssuerDN(issuerDN);
            certificateGenerator.SetSubjectDN(subjectDN);

            // Valid For
            DateTime notBefore = DateTime.UtcNow.Date;
            DateTime notAfter = notBefore.AddYears(2);

            certificateGenerator.SetNotBefore(notBefore);
            certificateGenerator.SetNotAfter(notAfter);

            // Subject Public Key
            AsymmetricCipherKeyPair subjectKeyPair;
            KeyGenerationParameters keyGenerationParameters = new KeyGenerationParameters(random, keyStrength);
            RsaKeyPairGenerator keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(keyGenerationParameters);
            subjectKeyPair = keyPairGenerator.GenerateKeyPair();

            certificateGenerator.SetPublicKey(subjectKeyPair.Public);

            // Generating the Certificate
            AsymmetricCipherKeyPair issuerKeyPair = subjectKeyPair;
            ISignatureFactory signatureFactory = new Asn1SignatureFactory("SHA256WITHRSA", issuerKeyPair.Private, random);

            // selfsign certificate
            Org.BouncyCastle.X509.X509Certificate certificate = certificateGenerator.Generate(signatureFactory);
            X509Certificate2 x509 = new X509Certificate2(certificate.GetEncoded());
            x509.FriendlyName = subjectName;
            CaPrivateKey = issuerKeyPair.Private;

            // Now do Android...
            certificateGenerator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(true));
            issuerKeyPair = subjectKeyPair;
            signatureFactory = new Asn1SignatureFactory("SHA256WITHRSA", issuerKeyPair.Private, random);
            Org.BouncyCastle.X509.X509Certificate certificateAndroid = certificateGenerator.Generate(signatureFactory);
            X509Certificate2 x509Android = new X509Certificate2(certificateAndroid.GetEncoded());
            Android = x509Android.Export(X509ContentType.Cert, "1234");

            return x509;
        }

        public AsymmetricAlgorithm ToDotNetKey(RsaPrivateCrtKeyParameters privateKey)
        {
            var cspParams = new CspParameters()
            {
                KeyContainerName = Guid.NewGuid().ToString(),
                KeyNumber = (int)KeyNumber.Exchange,
                Flags = CspProviderFlags.UseMachineKeyStore
            };

            var rsaProvider = new RSACryptoServiceProvider(cspParams);
            var parameters = new RSAParameters()
            {
                Modulus = privateKey.Modulus.ToByteArrayUnsigned(),
                P = privateKey.P.ToByteArrayUnsigned(),
                Q = privateKey.Q.ToByteArrayUnsigned(),
                DP = privateKey.DP.ToByteArrayUnsigned(),
                DQ = privateKey.DQ.ToByteArrayUnsigned(),
                InverseQ = privateKey.QInv.ToByteArrayUnsigned(),
                D = privateKey.Exponent.ToByteArrayUnsigned(),
                Exponent = privateKey.PublicExponent.ToByteArrayUnsigned()
            };

            rsaProvider.ImportParameters(parameters);
            return rsaProvider;
        }

        public bool addCertToStore(System.Security.Cryptography.X509Certificates.X509Certificate2 cert, System.Security.Cryptography.X509Certificates.StoreName st, System.Security.Cryptography.X509Certificates.StoreLocation sl)
        {
            X509Store x509store;

            try
            {
                x509store = new X509Store(st, sl);
                x509store.Open(OpenFlags.ReadWrite);
                x509store.Add(cert);
                x509store.Close();
            }
            catch
            {
                return (false);
            }

            return (true);
        }


        public void UpdateUrlacl
        (
            string a_szThumbprint
        )
        {
            string szUrlAclBat = "";

            // Delete prior stuff...
            szUrlAclBat +=
                "echo Setting up for HTTPS access" + Environment.NewLine +
                "netsh http delete urlacl \"url=http://+:" + m_szPort + "/twaindirect/v1/commands/\"" + Environment.NewLine +
                "netsh http delete urlacl \"url=https://+:" + m_szPort + "/twaindirect/v1/commands/\"" + Environment.NewLine +
                "netsh http delete urlacl \"url=http://+:" + m_szPort + "/privet/info/\"" + Environment.NewLine +
                "netsh http delete urlacl \"url=https://+:" + m_szPort + "/privet/info/\"" + Environment.NewLine +
                "netsh http delete urlacl \"url=http://+:" + m_szPort + "/privet/infoex/\"" + Environment.NewLine +
                "netsh http delete urlacl \"url=https://+:" + m_szPort + "/privet/infoex/\"" + Environment.NewLine +
                "netsh http delete urlacl \"url=http://+:" + m_szPort + "/privet/twaindirect/session/\"" + Environment.NewLine +
                "netsh http delete urlacl \"url=https://+:" + m_szPort + "/privet/twaindirect/session/\"" + Environment.NewLine +
                "netsh http delete sslcert ipport=0.0.0.0:" + m_szPort + " > NUL" + Environment.NewLine;

            // Add new stuff, but only if we have a thumbnail...
            if (!string.IsNullOrEmpty(a_szThumbprint))
            {
                szUrlAclBat +=
                    "netsh http add urlacl \"url=https://+:" + m_szPort + "/privet/info/\" \"sddl=D:(A;;GX;;;S-1-2-0)\"" + Environment.NewLine +
                    "netsh http add urlacl \"url=https://+:" + m_szPort + "/privet/infoex/\" \"sddl=D:(A;;GX;;;S-1-2-0)\"" + Environment.NewLine +
                    "netsh http add urlacl \"url=https://+:" + m_szPort + "/privet/twaindirect/session/\" \"sddl=D:(A;;GX;;;S-1-2-0)\"" + Environment.NewLine +
                    "netsh http add sslcert ipport=0.0.0.0:" + m_szPort + " certhash=" + a_szThumbprint + " appid=" + m_szTwainDirectScannerApp + " certstore=my" + Environment.NewLine;
            }

            // Do it...
            string szUrlAclFile = Path.Combine(Path.GetTempPath(), "twaindirecturlacl.bat");
            File.WriteAllText(szUrlAclFile, szUrlAclBat);
            RunBatchFile(szUrlAclFile);
            File.Delete(szUrlAclFile);
        }

        public static string RunBatchFile(string a_szBatchFile)
        {
            Process process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.FileName = a_szBatchFile;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            process.Start();
            string szOutput = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            process.Dispose();
            return (szOutput);
        }

        /// <summary>
        /// The names of our root and exchange certificates...
        /// </summary>
        private string m_szRootCertificateName;
        private string m_szExchangeCertificateName;
        private string m_szTwainDirectScannerApp;
        private string m_szPort;
    }
}
