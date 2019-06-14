using System;
using System.Threading;
using System.Windows.Forms;
using HazyBits.Twain.Cloud.Client;
using HazyBits.Twain.Cloud.Forms;
using TwainDirect.Support;

namespace TwainDirect.Certification
{
    public partial class FormMain : Form
    {
        public FormMain()
        {
            InitializeComponent();
        }

        public void Shutdown()
        {
            // Let's make sure we do this in the right place...
            Invoke(new MethodInvoker(delegate {
                Close();
            }));
        }

        public TwainCloudTokens GetTwainCloudTokens()
        {
            return (m_twaincloudtokens);
        }

        public void SetTwainLocalScannerClient(TwainLocalScannerClient a_twainlocalscannerclient)
        {
            m_twainlocalscannerclient = a_twainlocalscannerclient;
        }

        /// <summary>
        /// Take us through the signin process...
        /// </summary>
        /// <param name="a_szApiRoot">our cloud url</param>
        /// <param name="a_szSigninUrl">our signin url</param>
        /// <returns></returns>
        public TwainLocalScannerClient Signin(string a_szApiRoot, string a_szSigninUrl)
        {
            // Cleanup...
            m_twaincloudclient = null;
            m_twaincloudtokens = null;
            if (m_twainlocalscannerclient != null)
            {
                m_twainlocalscannerclient.Dispose();
                m_twainlocalscannerclient = null;
            }

            // Remember stuff...
            m_szApiRoot = a_szApiRoot;
            m_szSigninUrl = a_szSigninUrl;

            // Signin...
            AutoResetEvent autoresetevent = new AutoResetEvent(false);
            Invoke(new MethodInvoker(delegate {
                FacebookLoginForm facebookloginform = new FacebookLoginForm(m_szSigninUrl);
                facebookloginform.Authorized += async (_, args) =>
                {
                    // Form goes bye-bye...
                    facebookloginform.Close();

                    // Squirrel this away...
                    m_twaincloudtokens = args.Tokens;

                    // Put the authorization bearer token where we can get it...
                    m_twainlocalscannerclient = new TwainLocalScannerClient(EventCallback, this, false);
                    m_twaincloudclient = new TwainCloudClient(m_szApiRoot, m_twaincloudtokens);
                    await m_twainlocalscannerclient.ConnectToCloud(m_twaincloudclient);
                    m_twainlocalscannerclient.m_dictionaryExtraHeaders.Add("Authorization", m_twaincloudtokens.AuthorizationToken);
                    autoresetevent.Set();
                };
            }));
            autoresetevent.WaitOne();

            // All done...
            return (m_twainlocalscannerclient);
        }

        /// <summary>
        /// Event callback...
        /// </summary>
        /// <param name="a_object"></param>
        /// <param name="a_szEvent"></param>
        private void EventCallback(object a_object, string a_szEvent)
        {
            switch (a_szEvent)
            {
                // Don't recognize it...
                default:
                    BeginInvoke(new MethodInvoker(UpdateSummary));
                    break;

                // Image blocks have been updated...
                case "imageBlocks":
                    BeginInvoke(new MethodInvoker(UpdateSummary));
                    break;

                // We've lost our session
                case "critical":
                    BeginInvoke(new MethodInvoker(Critical));
                    break;

                // We've lost our session
                case "sessionTimedOut":
                    BeginInvoke(new MethodInvoker(SessionTimedOut));
                    break;
            }
        }

        /// <summary>
        /// Update the summary text box...
        /// </summary>
        private void UpdateSummary()
        {
            if (m_twainlocalscannerclient != null)
            {
                // no action at this time
            }
        }

        /// <summary>
        /// Handle critical errors...
        /// </summary>
        private void Critical()
        {
            m_twainlocalscannerclient.ClientCertificationTwainLocalSessionDestroy(true);
        }

        /// <summary>
        /// Handle sessionTimedOut errors...
        /// </summary>
        private void SessionTimedOut()
        {
            m_twainlocalscannerclient.ClientCertificationTwainLocalSessionDestroy(true);
        }

        private string m_szApiRoot;
        private string m_szSigninUrl;
        private TwainCloudTokens m_twaincloudtokens;
        private TwainCloudClient m_twaincloudclient;
        private TwainLocalScannerClient m_twainlocalscannerclient;

    }
}
