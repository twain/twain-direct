// Helpers...
using System;
using System.Threading;
using System.Windows.Forms;
using TWAINWorkingGroup;

namespace TwainDirect.OnTwain
{
    public partial class FormTwain : Form
    {
        public FormTwain
        (
            string a_szWriteFolder,
            string a_szImagesFolder,
            string a_szIpc,
            int a_iPid
        )
        {
            TwainLocalOnTwainParameters twainlocalontwainparameters;

            // Init stuff (though we'll never show this form)...
            InitializeComponent();

            // Our parameters...
            twainlocalontwainparameters = new TwainLocalOnTwainParameters
            (
                a_szWriteFolder,
                a_szImagesFolder,
                a_szIpc,
                a_iPid,
                RunInUiThread,
                this,
                this.Handle
            );

            // The pain continues, we need to run the next bit in a thread
            // so that we don't block our window...
            m_threadTwainLocalOnTwain = new Thread(new ParameterizedThreadStart(TwainLocalOnTwainThread));
            m_threadTwainLocalOnTwain.Start(twainlocalontwainparameters);
        }

        /// <summary>
        /// TWAIN needs help, if we want it to run stuff in our main
        /// UI thread...
        /// </summary>
        /// <param name="control">the control to run in</param>
        /// <param name="code">the code to run</param>
        public void RunInUiThread(Action a_action)
        {
            RunInUiThread(this, a_action);
        }
        public void RunInUiThread(Object a_object, Action a_action)
        {
            Control control = (Control)a_object;
            if (control.InvokeRequired)
            {
                control.Invoke(new FormTwain.RunInUiThreadDelegate(RunInUiThread), new object[] { a_object, a_action });
                return;
            }
            a_action();
        }

        /// <summary>
        /// We don't want to steal the focus from anyone.  This
        /// override accomplishes that...
        /// </summary>
        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }
        /// <summary>
        /// Close our window...
        /// </summary>
        private void CloseFormTwain()
        {
            Close();
        }

        /// <summary>
        /// This is our main loop where we issue commands to the TWAIN
        /// object on behalf of the caller.  This function runs in its
        /// own thread...
        /// </summary>
        private void TwainLocalOnTwainThread
        (
            object a_objectParameters
        )
        {
            TwainLocalOnTwain twainlocalontwain;
            TwainLocalOnTwainParameters twainlocalontwainparameters = (TwainLocalOnTwainParameters)a_objectParameters;

            // Create our object...
            twainlocalontwain = new TwainLocalOnTwain
            (
                twainlocalontwainparameters.m_szWriteFolder,
                twainlocalontwainparameters.m_szImagesFolder,
                twainlocalontwainparameters.m_szIpc,
                twainlocalontwainparameters.m_iPid,
                twainlocalontwainparameters.m_runinuithreaddelegate,
                twainlocalontwainparameters.m_formtwain,
                twainlocalontwainparameters.m_intptrHwnd
            );

            // Run our object...
            twainlocalontwain.Run();

            // We're done...
            Invoke(new MethodInvoker(delegate() { CloseFormTwain(); }));
        }

        private class TwainLocalOnTwainParameters
        {
            public TwainLocalOnTwainParameters
            (
                string a_szWriteFolder,
                string a_szImagesFolder,
                string a_szIpc,
                int a_iPid,
                TWAIN.RunInUiThreadDelegate a_runinuithreaddelegate,
                FormTwain a_formtwain,
                IntPtr a_intptrHwnd
            )
            {
                m_szWriteFolder = a_szWriteFolder;
                m_szImagesFolder = a_szImagesFolder;
                m_szIpc = a_szIpc;
                m_iPid = a_iPid;
                m_runinuithreaddelegate = a_runinuithreaddelegate;
                m_formtwain = a_formtwain;
                m_intptrHwnd = a_intptrHwnd;
            }

            public string m_szWriteFolder;
            public string m_szImagesFolder;
            public string m_szIpc;
            public int m_iPid;
            public TWAIN.RunInUiThreadDelegate m_runinuithreaddelegate;
            public FormTwain m_formtwain;
            public IntPtr m_intptrHwnd;
        };

        private Thread m_threadTwainLocalOnTwain;

        /// <summary>
        /// We use this to run code in the context of the caller's UI thread...
        /// </summary>
        /// <param name="a_object">object (really a control)</param>
        /// <param name="a_action">code to run</param>
        public delegate void RunInUiThreadDelegate(Object a_object, Action a_action);

    }
}
