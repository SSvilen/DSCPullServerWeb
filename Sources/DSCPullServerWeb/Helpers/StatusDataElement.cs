using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DSCPullServerWeb.Helpers {
    public class StatusDataElement {
        public string CurrentChecksum { get; set; }
        public string DurationInSeconds { get; set; }
        public string Error { get; set; }
        public string HostName { get; set; }
        public string[] IPV4Addresses { get; set; }
        public string[] IPV6Addresses { get; set; }
        public string JobID { get; set; }
        public string LCMVersion { get; set; }
        public string Locale { get; set; }
        public string[] MACAddresses { get; set; }
        public string MetaData { get; set; }
        public string Mode { get; set; }
        public string NumberOfResources { get; set; }
        public string RebootRequested { get; set; }
        public List<ResourceState> ResourcesInDesiredState { get; set; }
        public List<ResourceState> ResourcesNotInDesiredState { get; set; }
        public DateTime StartDate { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
    }
}