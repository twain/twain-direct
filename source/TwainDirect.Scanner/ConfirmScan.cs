// Helpers...
using System;
using System.Drawing;
using System.Media;
using System.Windows.Forms;

namespace TwainDirect.Scanner
{
    public partial class ConfirmScan : Form
    {
        /// <summary>
        /// Init us...
        /// </summary>
        public ConfirmScan(int a_iTimeout, bool a_blUseBeep, float a_fScale, string a_szUserDns)
        {
            // Init stuff...
            InitializeComponent();

            // Scaling...
            if ((a_fScale > 0) && (a_fScale != 1))
            {
                if (a_fScale > 4)
                {
                    a_fScale = 4;
                }
                this.Font = new Font(this.Font.FontFamily, this.Font.Size * a_fScale, this.Font.Style);
            }

            // Update the label...
            string[] aszUserDns = a_szUserDns.Split(':');
            m_szUserDns = aszUserDns[0];
            m_labelConfirmScan.Text = m_szUserDns + "\nWould you like to scan? (" + (a_iTimeout / 1000) + ")";

            // Try to make a noise...
            if (a_blUseBeep)
            {
                SystemSounds.Beep.Play();
            }

            // Start a timer...
            m_iTimeout = a_iTimeout;
            m_timer = new Timer();
            m_timer.Tick += m_timer_Tick;
            m_timer.Interval = 1000;
            m_timer.Start();
        }

        private void m_timer_Tick(object sender, EventArgs e)
        {
            // Decrement and show the result...
            m_iTimeout -= 1000;
            if (m_iTimeout < 0)
            {
                m_iTimeout = 0;
            }
            m_labelConfirmScan.Text = m_szUserDns + "\nWould you like to scan? (" + (m_iTimeout / 1000) + ")";

            // We're done...
            if (m_iTimeout <= 0)
            {
                m_timer.Stop();
                DialogResult = DialogResult.No;
            }
        }

        /// <summary>
        /// Sure, let's do it...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonYes_Click(object sender, EventArgs e)
        {
            m_timer.Stop();
            DialogResult = DialogResult.Yes;
        }

        /// <summary>
        /// Nope...nope...not gonna do it...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonNo_Click(object sender, EventArgs e)
        {
            m_timer.Stop();
            DialogResult = DialogResult.No;
        }

        /// <summary>
        /// Timeout for the confirmation message...
        /// </summary>
        private Timer m_timer;

        /// <summary>
        /// Timeout occurs when this value is <= 0...
        /// </summary>
        private int m_iTimeout;

        /// <summary>
        /// The user's DNS name...
        /// </summary>
        private string m_szUserDns;
    }
}
