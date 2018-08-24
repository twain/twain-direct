using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TwainDirect.App
{
    public partial class FormPassword : Form
    {
        /// <summary>
        /// Init stuff...
        /// </summary>
        public FormPassword(string a_szPassword, bool a_blShowPassword)
        {
            InitializeComponent();
            m_textboxPassword.Text = a_szPassword;
            m_checkboxShowPassword.Checked = a_blShowPassword;
            if (m_checkboxShowPassword.Checked)
            {
                m_textboxPassword.PasswordChar = '\0';
            }
            else
            {
                m_textboxPassword.PasswordChar = '*';
            }
        }

        /// <summary>
        ///  Get the password...
        /// </summary>
        /// <returns></returns>
        public string GetPassword()
        {
            return (m_textboxPassword.Text);
        }

        /// <summary>
        /// Get the show password flag...
        /// </summary>
        /// <returns></returns>
        public bool GetShowPassword()
        {
            return (m_checkboxShowPassword.Checked);
        }

        /// <summary>
        /// Show or don't show the password...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_checkboxShowPassword_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            if (checkbox.Checked)
            {
                m_textboxPassword.PasswordChar = '\0';
            }
            else
            {
                m_textboxPassword.PasswordChar = '*';
            }
        }
    }
}
