using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using HazyBits.Twain.Cloud.Registration;
using HazyBits.Twain.Cloud.Telemetry;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TwainDirect.Support
{
    public class CloudManager
    {
        public class CloudInfo
        {
            public CloudInfo(string a_szName, string a_szUrl, string a_szManager, string a_szFolderName, string a_szUseHttps, string a_szTwainCloudExpressFolder)
            {
                szName = a_szName;
                szUrl = a_szUrl;
                szManager = a_szManager;
                szFolderName = a_szFolderName;
                szUseHttps = a_szUseHttps;
                szTwainCloudExpressFolder = a_szTwainCloudExpressFolder;
            }
            public string szName;
            public string szUrl;
            public string szManager;
            public string szFolderName;
            public string szUseHttps;
            public string szTwainCloudExpressFolder;
        }
        private static List<CloudInfo> m_lcloudinfo = null;
        private static CloudInfo m_cloudinfoCurrent = null;


        static CloudManager()
        {
            Logger.RegisteredLoggerAdapters.Add(new TwainDirectLoggerAdapter());
            m_lcloudinfo = new List<CloudInfo>();
        }

        public static void SetCloudApiRoot(string a_szCloudName)
        {
            int ii;

            // Nope...
            if (string.IsNullOrEmpty(a_szCloudName))
            {
                return;
            }

            // See if we have a match...
            for (ii = 0; ii < m_lcloudinfo.Count; ii++)
            {
                if (m_lcloudinfo[ii].szName == a_szCloudName)
                {
                    m_cloudinfoCurrent = new CloudInfo(m_lcloudinfo[ii].szName, m_lcloudinfo[ii].szUrl, m_lcloudinfo[ii].szManager, m_lcloudinfo[ii].szFolderName, m_lcloudinfo[ii].szUseHttps, m_lcloudinfo[ii].szTwainCloudExpressFolder);
                    return;
                }
            }

            // No joy, so make an entry...
            m_cloudinfoCurrent = new CloudInfo(a_szCloudName, a_szCloudName, "", "mycloud", "yes", "");
        }

        /// <summary>
        /// Get the current cloud info...
        /// </summary>
        /// <param name="a_iIndex"></param>
        /// <returns></returns>
        public static CloudInfo GetCurrentCloudInfo()
        {
            if (m_cloudinfoCurrent == null)
            {
                return (null);
            }
            return (new CloudInfo(m_cloudinfoCurrent.szName, m_cloudinfoCurrent.szUrl, m_cloudinfoCurrent.szManager, m_cloudinfoCurrent.szFolderName, m_cloudinfoCurrent.szUseHttps, m_cloudinfoCurrent.szTwainCloudExpressFolder));
        }

        /// <summary>
        /// Get cloud info for a given index...
        /// </summary>
        /// <param name="a_iIndex"></param>
        /// <returns></returns>
        public static CloudInfo GetCloudInfo(int a_iIndex)
        {
            // We have no data, see if we can load it...
            if (m_lcloudinfo.Count == 0)
            {
                GetCloudApiRoot();
            }
            // Out of range...
            if ((a_iIndex < 0) || (a_iIndex >= m_lcloudinfo.Count))
            {
                return (null);
            }
            return (new CloudInfo(m_lcloudinfo[a_iIndex].szName, m_lcloudinfo[a_iIndex].szUrl, m_lcloudinfo[a_iIndex].szManager, m_lcloudinfo[a_iIndex].szFolderName, m_lcloudinfo[a_iIndex].szUseHttps, m_lcloudinfo[a_iIndex].szTwainCloudExpressFolder));
        }

        /// <summary>
        /// Return whatever we currently have.  If we don't have anything,
        /// try to figure it out.
        /// </summary>
        /// <returns></returns>
        public static string GetCloudApiRoot()
        {
            // We have a value, use it...
            if (m_cloudinfoCurrent != null)
            {
                return (m_cloudinfoCurrent.szUrl);
            }

            // See if the user gave us anything...
            m_lcloudinfo.Clear();
            int ii = 0;
            string szCloudName;
            string szCloudUrl;
            string szCloudManager;
            string szCloudFolderName;
            string szCloudUseHttps;
            string szTwainCloudExpressFolder;
            for (szCloudUrl = Config.Get("cloudApiRoot[" + ii + "].url", "");
                 !string.IsNullOrEmpty(szCloudUrl);
                 szCloudUrl = Config.Get("cloudApiRoot[" + ++ii + "].url", ""))
            {
                szCloudName = Config.Get("cloudApiRoot[" + ii + "].name", szCloudUrl);
                szCloudManager = Config.Get("cloudApiRoot[" + ii + "].manager", "");
                szCloudFolderName = Config.Get("cloudApiRoot[" + ii + "].folderName", "mycloud");
                szCloudUseHttps = Config.Get("cloudApiRoot[" + ii + "].useHttps", Config.Get("useHttps", "yes"));
                szTwainCloudExpressFolder = Config.Get("cloudApiRoot[" + ii + "].twainCloudExpressFolder", "");
                if (!string.IsNullOrEmpty(szTwainCloudExpressFolder) && !Directory.Exists(szTwainCloudExpressFolder))
                {
                    Log.Error("Unable to locate twainCloudExpressFolder, so ignoring this setting: " + szTwainCloudExpressFolder);
                    szTwainCloudExpressFolder = "";
                }
                m_lcloudinfo.Add(new CloudInfo(szCloudName, szCloudUrl, szCloudManager, szCloudFolderName, szCloudUseHttps, szTwainCloudExpressFolder));
            }

            // Use whatever has been baked into the app...
            if (m_lcloudinfo.Count == 0)
            {
                m_lcloudinfo.Add(new CloudInfo(ConfigurationManager.AppSettings["CloudName"], ConfigurationManager.AppSettings["CloudApiRoot"], ConfigurationManager.AppSettings["CloudApiRoot"], ConfigurationManager.AppSettings["CloudFolderName"], ConfigurationManager.AppSettings["CloudUseHttps"], ""));
            }

            // Grab the first value, if the user's configured us...
            if ((m_lcloudinfo.Count > 0) && !string.IsNullOrEmpty(m_lcloudinfo[0].szUrl))
            {
                m_cloudinfoCurrent = new CloudInfo(m_lcloudinfo[0].szName, m_lcloudinfo[0].szUrl, m_lcloudinfo[0].szManager, m_lcloudinfo[0].szFolderName, m_lcloudinfo[0].szUseHttps, m_lcloudinfo[0].szTwainCloudExpressFolder);
                return (m_cloudinfoCurrent.szUrl);
            }

            // Ruh-roh...
            return ("we have a problem");
        }

        public static string GetScannerCloudUrl(ScannerInformation scanner)
        {
            var url = $"{GetCloudApiRoot()}/scanners/{scanner.Id}";
            return url;
        }

        public static JsonSerializerSettings SerializationSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private class TwainDirectLoggerAdapter : ILoggerAdapter
        {
            public void LogException(TelemetryContext context, LogLevel level, Exception ex, string message)
            {
                LogMessage(context, level, $"{message}: {ex}", null);
            }

            public void LogMessage(TelemetryContext context, LogLevel level, string message, Dictionary<string, string> props)
            {
                var logEntry = PrepareMessage(context, message, props);

                switch (level)
                {
                    case LogLevel.Critical:
                    case LogLevel.Error:
                        Log.Error(logEntry);
                        break;
                    case LogLevel.Warning:
                        Log.Warn(logEntry);
                        break;
                    case LogLevel.Info:
                        Log.Info(logEntry);
                        break;
                    case LogLevel.Debug:
                        Log.Verbose(logEntry);
                        break;
                    default:
                        break;
                }
            }

            public ActivityScope StartActivity(TelemetryContext context, string name)
            {
                LogMessage(context, LogLevel.Info, name, null);
                return ActivityScope.Empty;
            }

            private string PrepareMessage(TelemetryContext context, string message, Dictionary<string, string> props)
            {
                return $"cloud>>> [{context.TypeContext}] {message}";
            }
        }
    }

    public class CloudMessage
    {
        public string Body { get; set; }
        public string Method { get; set; }
        public string Url { get; set; }
        public Dictionary<string, string> Headers { get; set; }
    }

    public class CloudDeviceResponse
    {
        public string RequestId { get; set; }
        public int StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public string Body { get; set; }
        public Dictionary<string, string> Headers { get; set; }
    }
}
