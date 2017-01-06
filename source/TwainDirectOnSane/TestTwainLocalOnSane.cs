// Helpers...
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using TwainDirectSupport;

namespace TwainDirectOnSane
{
    public sealed class TestTwainLocalOnSane : IDisposable
    {
        /// <summary>
        /// Init stuff...
        /// </summary>
        public TestTwainLocalOnSane(string a_szScanner, string a_szWriteFolder, int a_iPid, string a_szTask, string a_szIpc)
        {
            m_szScanner = a_szScanner;
            m_szWriteFolder = a_szWriteFolder;
            m_iPid = a_iPid;
            m_szIpc = a_szIpc; // Path.Combine(m_szWriteFolder, "ipc");
            m_szTask = Path.Combine(a_szWriteFolder, "tasks");
            m_szTask = Path.Combine(m_szTask, a_szTask);
        }

        /// <summary>
        /// Destructor...
        /// </summary>
        ~TestTwainLocalOnSane()
        {
            Dispose(false);
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        internal void Dispose(bool a_blDisposing)
        {
            // Free managed resources...
            if (a_blDisposing)
            {
                // nothing to free at this time...
            }
        }

        //TwainLocalOnSane m_twainlocalonsane;
        private string m_szScanner;
        private string m_szWriteFolder;
        private int m_iPid;
        private string m_szIpc;
        private string m_szTask;
    }
}
