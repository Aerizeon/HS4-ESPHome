using HomeSeer.PluginSdk.Devices.Controls;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zeroconf;

namespace HSPI_ESPHomeNative.ESPHome
{
    internal class DeviceManager
    {
        private ConcurrentDictionary<string, ESPHomeDevice> _devices = new ConcurrentDictionary<string, ESPHomeDevice>();

        public delegate void DeviceFoundHandlier(ESPHomeDevice device);
        public event DeviceFoundHandlier OnDeviceFound;

        public async Task FindDevices()
        {
            var hosts = await ZeroconfResolver.ResolveAsync("_esphomelib._tcp.local.");
            foreach (var host in hosts)
            {
                foreach (KeyValuePair<string, IService> service in host.Services)
                {
                    if (service.Value.Name == "_esphomelib._tcp.local.")
                    {
                        DeviceDiscovered(host, service.Value);
                        break;
                    }
                }
               
            }
        }

        public async void ListenForAnnoucnements(CancellationToken cancellationToken)
        {
            await ZeroconfResolver.ListenForAnnouncementsAsync((announcement) => { 
                foreach(KeyValuePair<string, IService> service in announcement.Host.Services)
                {
                    if(service.Value.Name == "_esphomelib._tcp.local.")
                    {
                        DeviceDiscovered(announcement.Host, service.Value);
                        break;
                    }
                }
            }, cancellationToken);
        }

        public void ProcessControlEvent(ControlEvent controlEvent)
        {
            foreach(var device in _devices)
            {
                device.Value.ProcessEvent(controlEvent);
            }
        }

        private void DeviceDiscovered(IZeroconfHost host, IService service)
        {
            if(!_devices.ContainsKey(service.Properties[0]["mac"]))
            {
                var device = new ESPHomeDevice(host.DisplayName, host.IPAddress, service.Port, service.Properties[0]["mac"]);
                device.OnDisconnected += Device_OnDisconnected;
                _devices[service.Properties[0]["mac"]] = device;
                OnDeviceFound?.Invoke(device);
            }
        }

        private void Device_OnDisconnected(ESPHomeDevice sender)
        {
            _devices.TryRemove(sender.Id, out _);
        }
    }
}
