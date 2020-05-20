using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DSCPullServerWeb.Helpers {
    public class ResourceState {
        public string ConfigurationName { get; set; }
        public string DurationInSeconds { get; set; }
        public bool InDesiredState { get; set; }
        public string InstanceName { get; set; }
        public string ModuleName { get; set; }
        public string ModuleVersion { get; set; }
        public string RebootRequested { get; set; }
        public string ResourceId { get; set; }
        public string ResourceName { get; set; }
        public string SourceInfo { get; set; }
        public string StartDate { get; set; }
    }
}