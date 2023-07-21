using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSPI_ESPHomeNative
{
    internal struct AddDeviceInfo
    {
        [JsonProperty("deviceId")]
        public string DeviceId { get; set; }
        [JsonProperty("securityMode")]
        public string SecurityMode { get; set; }
        [JsonProperty("devicePassword")]
        public string DevicePassword { get; set; }
    }
}
