using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSPI_ESPHomeNative.ESPHome.Entities
{
    internal class SwitchEntity : EntityBase<ListEntitiesSwitchResponse>
    {
        public SwitchEntity(ESPHomeDevice device, ListEntitiesSwitchResponse entityData) 
            : base(device, entityData, entityData.UniqueId, entityData.Key)
        {
        }

        public override Dictionary<int, Action<ControlEvent>> ProcessFeatures()
        {
            var controlsFeature = Device.GetOrCreateFeature($"{EntityData.UniqueId}-controls",
                FeatureFactory.CreateGenericBinaryControl(Program._plugin.Id, $"{EntityData.Name} State", "On", "Off", 1, 0));
            FeatureIds.Add("controls", controlsFeature.Ref);
            ControlEvents.Add(controlsFeature.Ref, (ControlEvent controlEvent) =>
            {
                SwitchCommandRequest request = new SwitchCommandRequest()
                {
                    Key = Key,
                    State = controlEvent.ControlValue == 1
                };
                Device.SendMessage(request);
            });

            return ControlEvents;
        }
        public override void HandleMessage(IExtensible message)
        {
            if (message is SwitchStateResponse state && state.Key == EntityData.Key)
            {
                if (FeatureIds.TryGetValue("controls", out int controlsFeature))
                {
                    if (state.State)
                        Device.UpdateFeature(controlsFeature, 1, "On");
                    else
                        Device.UpdateFeature(controlsFeature, 0, "Off");
                }
            }
        }

        public override void RequestStatusUpdate()
        {

        }

    }
}
