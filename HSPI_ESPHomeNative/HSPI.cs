using System.Collections.Generic;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using HSPI_ESPHomeNative.ESPHome;
using HomeSeer.PluginSdk.Logging;
using Newtonsoft.Json;
using System;
using System.Text;
using ProtoBuf.Meta;
using System.Linq;
using System.Threading.Tasks;

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
            HomeSeerSystem.RegisterDeviceIncPage(Id, "add-device.html", "Add Device");

            var devicesInfo = await deviceManager.SearchForDevices();

            // We reset all of the devices to showing an offline status, if possible.
            var deviceRefs = HomeSeerSystem.GetRefsByInterface(Id, true);
            foreach (var deviceRef in deviceRefs)
            {
                var device = HomeSeerSystem.GetDeviceWithFeaturesByRef(deviceRef);
                var existingDeviceInfo = devicesInfo.SingleOrDefault(d => device.Address == $"esp{d.Id}");
                if (existingDeviceInfo != null)
                {
                    var devicePSK = "";
                    if (device.PlugExtraData.ContainsNamed("DevicePSK"))
                        devicePSK = device.PlugExtraData.GetNamed<string>("DevicePSK");

                    SetupDevice(new ESPHomeDevice(existingDeviceInfo, devicePSK));
                }
                else
                {
                    if (device.Features.Count > 0)
                    {
                        OnFeatureUpdate(device.GetFeaturesInDisplayOrder()[0].Ref, -1, "Offline");
                    }
                }
            }
            
            Status = PluginStatus.Ok();
            deviceManager.ListenForAnnoucnements(new System.Threading.CancellationToken());
        }

        private async void SetupDevice(ESPHomeDevice device, string password = "")
        {
            device.OnFeatureUpdate += OnFeatureUpdate;
            device.OnDisconnected += OnDisconnected;
            await device.Connect(password);
            await device.QueryDeviceInformation();

            HsDevice hsDevice = HomeSeerSystem.GetDeviceByAddress($"esp{device.Info.Id}");
            if (hsDevice != null)
                hsDevice = HomeSeerSystem.GetDeviceWithFeaturesByRef(hsDevice.Ref);
            else
            {
                var df = DeviceFactory.CreateDevice(Program._plugin.Id).WithName(device.Name).WithAddress($"esp{device.Info.Id}");
                int deviceId = HomeSeerSystem.CreateDevice(df.PrepareForHs());
                hsDevice = HomeSeerSystem.GetDeviceWithFeaturesByRef(deviceId);
            }

            if (!hsDevice.PlugExtraData.ContainsNamed("UseEncryption"))
                hsDevice.PlugExtraData.AddNamed<bool>("UseEncryption", device.UseEncryption);
            if (!hsDevice.PlugExtraData.ContainsNamed("DevicePassword"))
                hsDevice.PlugExtraData.AddNamed<string>("DevicePassword", password);

            device.PrepareFeatures(hsDevice, HomeSeerSystem);
            deviceManager.ConfiguredDevices.Add(device);
        }

        private async void OnDisconnected(ESPHomeDevice sender, DisconnectReason reason)
        {
            HsDevice hsDevice = HomeSeerSystem.GetDeviceByAddress($"esp{sender.Info.Id}");
            if (hsDevice != null)
                hsDevice = HomeSeerSystem.GetDeviceWithFeaturesByRef(hsDevice.Ref);

            OnFeatureUpdate(hsDevice.GetFeaturesInDisplayOrder()[0].Ref, -1, "Offline");

            if(reason != DisconnectReason.DisconnectRequested)
            {
                //TODO: This is not robust. We should check if the IP changed, and not just sit in a loop.
                while (true)
                {
                    try
                    {
                        //TODO: This assumes no device password. This might not be
                        //the case, so we should change things around to make more sense.
                        await sender.Connect();
                    }
                    catch (Exception ex)
                    {
                        WriteLog(ELogType.Error, "Unable to reconnect to ESPHome device, ex: " + ex.Message);
                    }
                    await Task.Delay(10000);
                }
            }
        }

        private void OnFeatureUpdate(int refId, double value, string valueString)
        {
            HomeSeerSystem.UpdateFeatureValueByRef(refId, value);
            HomeSeerSystem.UpdateFeatureValueStringByRef(refId, valueString);
        }

        // This is where we handle updates from inside hs4
        public override void SetIOMulti(List<ControlEvent> colSend)
        {
            foreach (ControlEvent col in colSend)
            {
                foreach (var device in deviceManager.ConfiguredDevices)
                {
                    device.ProcessEvent(col);
                }
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

        public override string PostBackProc(string page, string data, string user, int userRights)
        {
            var response = "";
            switch(page)
            {
                case "add-device.html":

                    try
                    {
                        var postData = JsonConvert.DeserializeObject<AddDeviceInfo>(data);
                        if (LogDebug)
                        {
                            Console.WriteLine("Post back from add-sample-device page");
                        }
                        var devices = deviceManager.SearchForDevices().GetAwaiter().GetResult();
                        var deviceInfo = devices.SingleOrDefault(d => d.Id == postData.DeviceId);
                        if (deviceInfo != null)
                        {
                            var devicePSK = "";
                            var devicePassword = "";
                            if (postData.SecurityMode == "2")
                            {
                                devicePSK = postData.DevicePassword;
                                devicePassword = "";
                            }
                            else
                            {
                                devicePassword = postData.DevicePassword;
                            }

                            SetupDevice(new ESPHomeDevice(deviceInfo, devicePSK), devicePassword);
                            response = "ok";
                        }
                        else
                        {
                            response = "error";
                        }
                    }
                    catch (Exception exception)
                    {
                        if (LogDebug)
                        {
                            Console.WriteLine(exception.Message);
                        }
                        response = "error";
                    }
                    break;
            }
            return response;
        }

        public string GetUnpairedDevices()
        {
            var sb = new StringBuilder("<select class=\"mdb-select md-form\" id=\"unpairedDevices-sl\">");
            sb.Append(Environment.NewLine);
            sb.Append("<option value=\"\" disabled selected>Select an ESPHome device</option>");
            sb.Append(Environment.NewLine);

            foreach ( var device in deviceManager.KnownDevices)
            {
                sb.Append("<option value=\"");
                sb.Append(device.Value.Id);
                sb.Append("\">");
                sb.Append(device.Value.Name);
                sb.Append("</option>");
                sb.Append(Environment.NewLine);
            }


            sb.Append("</select>");
            return sb.ToString();
        }
    }
}
