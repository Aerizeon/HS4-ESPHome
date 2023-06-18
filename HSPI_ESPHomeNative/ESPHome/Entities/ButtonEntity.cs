using HomeSeer.PluginSdk.Devices.Controls;
using HomeSeer.PluginSdk.Devices.Identification;
using HomeSeer.PluginSdk.Devices;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSPI_ESPHomeNative.ESPHome.Entities
{
    internal class ButtonEntity : EntityBase<ListEntitiesButtonResponse>
    {
        public ButtonEntity(ESPHomeDevice device, ListEntitiesButtonResponse entityData)
            : base(device, entityData, entityData.UniqueId, entityData.Key)
        {
        }

        public override Dictionary<int, Action<ControlEvent>> ProcessFeatures()
        {

            var controlsFeature = Device.GetOrCreateFeature($"{EntityData.UniqueId}-controls",
            FeatureFactory.CreateFeature(Program._plugin.Id).WithName($"{EntityData.Name}")
            .AsType(EFeatureType.Generic, 2)
            .AddButton(0, $"{EntityData.Name}", new ControlLocation(1, 1), EControlUse.NotSpecified));

            ControlEvents.Add(controlsFeature.Ref, (ControlEvent controlEvent) =>
            {
                ButtonCommandRequest request = new()
                {
                    Key = Key
                };
                Device.SendMessage(request);
            });

            return ControlEvents;
        }
        public override void HandleMessage(IExtensible message)
        {
        }

        public override void RequestStatusUpdate()
        {

        }

    }
}
