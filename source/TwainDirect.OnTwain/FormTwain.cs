// Helpers...
using System;
using System.Threading;
using System.Windows.Forms;
using TWAINWorkingGroupToolkit;

namespace TwainDirect.OnTwain
{
    public partial class FormTwain : Form
    {
        public FormTwain
        (
            string a_szWriteFolder,
            string a_szImagesFolder,
            string a_szIpc,
            int a_iPid,
            TWAINCSToolkit.RunInUiThreadDelegate a_runinuithreaddelegate
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
                a_runinuithreaddelegate,
                this,
                this.Handle
            );

            // The pain continues, we need to run the next bit in a thread
            // so that we don't block our window...
            m_threadTwainLocalOnTwain = new Thread(new ParameterizedThreadStart(TwainLocalOnTwainThread));
            m_threadTwainLocalOnTwain.Start(twainlocalontwainparameters);
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
                twainlocalontwainparameters.m_objectRunInUiThread,
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
                TWAINCSToolkit.RunInUiThreadDelegate a_runinuithreaddelegate,
                object a_objectRunInUiThread,
                IntPtr a_intptrHwnd
            )
            {
                m_szWriteFolder = a_szWriteFolder;
                m_szImagesFolder = a_szImagesFolder;
                m_szIpc = a_szIpc;
                m_iPid = a_iPid;
                m_runinuithreaddelegate = a_runinuithreaddelegate;
                m_objectRunInUiThread = a_objectRunInUiThread;
                m_intptrHwnd = a_intptrHwnd;
            }

            public string                                   m_szWriteFolder;
            public string                                   m_szImagesFolder;
            public string                                   m_szIpc;
            public int                                      m_iPid;
            public TWAINCSToolkit.RunInUiThreadDelegate     m_runinuithreaddelegate;
            public object                                   m_objectRunInUiThread;
            public IntPtr                                   m_intptrHwnd;
        };

        private Thread m_threadTwainLocalOnTwain;
    }
}
