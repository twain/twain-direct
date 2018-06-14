using System.Collections.Generic;
using System.Configuration;
using HazyBits.Twain.Cloud.Registration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TwainDirect.Support
{
    public class CloudManager
    {
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
