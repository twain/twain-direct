using System;
using System.Collections.Generic;
using System.Resources;
using System.Windows.Forms;
using TwainDirect.Support;

namespace TwainDirect.Scanner
{
    public partial class FormSelect : Form
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Init our form...
        /// </summary>
        /// <param name="a_resourcemanager"></param>
        /// <param name="a_aszDrivers"></param>
        /// <param name="a_szDefault"></param>
        public FormSelect(ResourceManager a_resourcemanager, List<string> a_lszDrivers, string a_szDefault, string a_szNote)
        {
            int iIndex;

            // Init stuff...
            InitializeComponent();
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            m_resourcemanager = a_resourcemanager;
            m_textboxNote.Text = a_szNote;

            // Localize...
            this.Text = Config.GetResource(m_resourcemanager, "strFormMainTitle"); // TWAIN Direct: TWAIN Bridge
            m_labelSelectScannerDriver.Text = Config.GetResource(m_resourcemanager, "strSelectScannerDriver"); // Select scanner driver...
            m_labelNote.Text = Config.GetResource(m_resourcemanager, "strSetNote"); // Register Cloud...

            // Fill the listbox...
            foreach (string szDriver in a_lszDrivers)
            {
                m_listboxSelectScannerDriver.Items.Add(szDriver);
            }

            // Set the default, and make sure the user sees it...
            iIndex = m_listboxSelectScannerDriver.FindStringExact(a_szDefault);
            if (iIndex >= 0)
            {
                m_listboxSelectScannerDriver.SelectedIndex = iIndex;
                m_listboxSelectScannerDriver.TopIndex = iIndex;
            }
        }

        /// <summary>
        /// Return the note...
        /// </summary>
        /// <returns></returns>
        public string GetNote()
        {
            return (m_textboxNote.Text);
        }

        /// <summary>
        /// Return the selected driver...
        /// </summary>
        /// <returns></returns>
        public int GetSelectedDriver()
        {
            return (m_listboxSelectScannerDriver.SelectedIndex);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// Our resource manager to help with localization...
        /// </summary>
        private ResourceManager m_resourcemanager;

        #endregion
    }
}
