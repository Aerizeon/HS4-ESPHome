using System.Collections.Generic;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using HSPI_ESPHomeNative.ESPHome;
using HomeSeer.PluginSdk.Logging;

namespace HSPI_ESPHomeNative
{
    internal class HSPI : AbstractPlugin
    {
        public override string Id => "ESPHomeNative";

        public override string Name => "ESPHome Native";
        protected override string SettingsFileName { get; } = "ESPHomeNative.ini";

        DeviceManager deviceManager = new DeviceManager();
        protected override async void Initialize()
        {
            // We reset all of the devices to showing an offline status, if possible.
            var deviceRefs = HomeSeerSystem.GetRefsByInterface(Id, true);
            foreach (var deviceRef in deviceRefs)
            {
                var device = HomeSeerSystem.GetDeviceWithFeaturesByRef(deviceRef);
                if (device.Features.Count > 0)
                {
                    OnFeatureUpdate(device.GetFeaturesInDisplayOrder()[0].Ref, -1, "Offline");
                }
            }

            // Start the mDNS querying service to find nearby devices
            deviceManager.OnDeviceFound += OnDeviceFound;
            await deviceManager.FindDevices();
            Status = PluginStatus.Ok();
        }

        private async void OnDeviceFound(ESPHomeDevice device)
        {
            device.OnFeatureUpdate += OnFeatureUpdate;
            await device.Connect();
            await device.QueryDeviceInformation();

            HsDevice hsDevice = HomeSeerSystem.GetDeviceByAddress($"esp{device.Id}");
            if (hsDevice != null)
                hsDevice = HomeSeerSystem.GetDeviceWithFeaturesByRef(hsDevice.Ref);
            else
            {
                var df = DeviceFactory.CreateDevice(Program._plugin.Id).WithName("Test Device").WithAddress($"esp{device.Id}");
                int deviceId = HomeSeerSystem.CreateDevice(df.PrepareForHs());
                hsDevice = HomeSeerSystem.GetDeviceWithFeaturesByRef(deviceId);
            }

            device.PrepareFeatures(hsDevice, HomeSeerSystem);
            device.RequestStatusUpdate();
        }

        private void OnFeatureUpdate(int refId, double value, string valueString)
        {
            HomeSeerSystem.UpdateFeatureValueByRef(refId, value);
            HomeSeerSystem.UpdateFeatureValueStringByRef(refId, valueString);
        }

        // This is where we handle updates from inside hs4
        public override void SetIOMulti(List<ControlEvent> colSend)
        {
            //base.SetIOMulti(colSend);
            foreach (ControlEvent col in colSend)
            {
                deviceManager.ProcessControlEvent(col);
            }
        }

        protected override bool OnSettingChange(string pageId, AbstractView currentView, AbstractView changedView)
        {
            return true;
        }

        protected override void BeforeReturnStatus()
        {
        }

        public void WriteLog(ELogType logType, string message)
        {
            HomeSeerSystem.WriteLog(logType, message, Name);
        }
    }
}
