// Helpers...
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using TwainDirectSupport;

namespace TwainDirectOnTwain
{
    public sealed class TestTwainLocalOnTwain
    {
        /// <summary>
        /// Initialize stuff...
        /// </summary>
        /// <param name="a_szScanner">our scanner name</param>
        /// <param name="a_szWriteFolder">the folder where images can go</param>
        /// <param name="a_iPid">our process id</param>
        /// <param name="a_szTaskFile">the task file to use</param>
        public TestTwainLocalOnTwain(string a_szScanner, string a_szWriteFolder, int a_iPid, string a_szTaskFile)
        {
            m_szScanner = a_szScanner;
            m_szWriteFolder = a_szWriteFolder;
            m_iPid = a_iPid;
            m_szIpc = Path.Combine(m_szWriteFolder, "ipc");
            m_szTaskFile = Path.Combine(a_szWriteFolder, "tasks");
            m_szTaskFile = Path.Combine(m_szTaskFile, a_szTaskFile);
        }

        private string m_szScanner;
        private string m_szWriteFolder;
        private int m_iPid;
        private string m_szIpc;
        private string m_szTaskFile;
    }
}
