using HomeSeer.PluginSdk.Devices.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Zeroconf;

namespace HSPI_ESPHomeNative.ESPHome
{
    internal class DeviceManager
    {
        public ConcurrentDictionary<string, DeviceInfo> KnownDevices { get; private set; } = new();
        public List<ESPHomeDevice> ConfiguredDevices { get; set; } = new();

        public delegate void DeviceFoundHandlier(ESPHomeDevice device);

        public async Task<List<DeviceInfo>> SearchForDevices()
        {
            var hosts = await ZeroconfResolver.ResolveAsync("_esphomelib._tcp.local.");
            foreach (var host in hosts)
            {
                foreach (KeyValuePair<string, IService> service in host.Services)
                {
                    var serviceInfo = service.Value;
                    if (service.Value.Name != "_esphomelib._tcp.local.")
                        continue;
                    if (KnownDevices.ContainsKey(serviceInfo.Properties[0]["mac"]))
                        continue;

                    DeviceInfo deviceInfo = new()
                    {
                        Id = serviceInfo.Properties[0]["mac"],
                        Name = host.DisplayName,
                        Address = IPAddress.Parse(host.IPAddress),
                        Port = serviceInfo.Port
                    };

                    KnownDevices.TryAdd(deviceInfo.Id, deviceInfo);
                    break;
                }

            }
            return KnownDevices.Values.ToList();
        }

        public async void ListenForAnnoucnements(CancellationToken cancellationToken)
        {
            await ZeroconfResolver.ListenForAnnouncementsAsync((announcement) => { 
                foreach(KeyValuePair<string, IService> service in announcement.Host.Services)
                {
                    var serviceInfo = service.Value;
                    if (service.Value.Name != "_esphomelib._tcp.local.")
                        continue;
                    if (KnownDevices.ContainsKey(serviceInfo.Properties[0]["mac"]))
                        continue;

                    DeviceInfo deviceInfo = new()
                    {
                        Id = serviceInfo.Properties[0]["mac"],
                        Name = announcement.Host.DisplayName,
                        Address = IPAddress.Parse(announcement.Host.IPAddress),
                        Port = serviceInfo.Port
                    };

                    KnownDevices.TryAdd(deviceInfo.Id, deviceInfo);
                    break;
                }
            }, cancellationToken);
        }
    }
}
