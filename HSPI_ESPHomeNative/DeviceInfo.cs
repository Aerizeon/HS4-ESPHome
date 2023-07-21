using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace HSPI_ESPHomeNative
{
    internal class DeviceInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public IPAddress Address { get; set; }
        public int Port { get; set; }
    }
}
