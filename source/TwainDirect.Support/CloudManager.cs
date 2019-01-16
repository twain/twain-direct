using System;
using System.Collections.Generic;
using System.Configuration;
using HazyBits.Twain.Cloud.Registration;
using HazyBits.Twain.Cloud.Telemetry;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TwainDirect.Support
{
    public class CloudManager
    {
        static CloudManager()
        {
            Logger.RegisteredLoggerAdapters.Add(new TwainDirectLoggerAdapter());
        }

        public static string GetCloudApiRoot()
        {
            var apiRoot = ConfigurationManager.AppSettings["CloudApiRoot"];
            return apiRoot;
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
