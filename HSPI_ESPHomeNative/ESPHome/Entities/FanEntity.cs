using HomeSeer.PluginSdk.Devices.Controls;
using HomeSeer.PluginSdk.Devices;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HomeSeer.PluginSdk.Devices.Identification;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;

namespace HSPI_ESPHomeNative.ESPHome.Entities
{
    internal class FanEntity : EntityBase<ListEntitiesFanResponse>
    {
        public FanEntity(ESPHomeDevice device, ListEntitiesFanResponse entityData)
            : base(device, entityData, entityData.UniqueId, entityData.Key)
        {
        }

        public override Dictionary<int, Action<ControlEvent>> ProcessFeatures()
        {
            var factory = FeatureFactory.CreateFeature(Program._plugin.Id).WithName($"{EntityData.Name} State")
            .AsType(EFeatureType.Generic, 2);

            if (EntityData.SupportsSpeed && EntityData.SupportedSpeedCount > 1)
            {
                if (EntityData.SupportedSpeedCount == 2)
                {
                    factory.AddButton(0, "Off", new ControlLocation(1, 1), EControlUse.Off)
                        .AddButton(1, "Low", new ControlLocation(1, 2), EControlUse.DimFan)
                        .AddButton(2, "High", new ControlLocation(1, 3), EControlUse.DimFan)
                        .AddGraphicForValue("/images/HomeSeer/status/fan-state-off.png", 0, "Off")
                        .AddGraphicForValue("/images/HomeSeer/status/fan-state-low.png", 1, "Low")
                        .AddGraphicForValue("/images/HomeSeer/status/fan-state-high.png", 2, "High");
                }
                else if (EntityData.SupportedSpeedCount == 3)
                {
                    factory.AddButton(0, "Off", new ControlLocation(1, 1), EControlUse.Off)
                        .AddButton(1, "Low", new ControlLocation(1, 2), EControlUse.DimFan)
                        .AddButton(2, "Medium", new ControlLocation(1, 3), EControlUse.DimFan)
                        .AddButton(3, "High", new ControlLocation(1, 4), EControlUse.DimFan)
                        .AddGraphicForValue("/images/HomeSeer/status/fan-state-off.png", 0, "Off")
                        .AddGraphicForValue("/images/HomeSeer/status/fan-state-low.png", 1, "Low")
                        .AddGraphicForValue("/images/HomeSeer/status/fan-state-on.png", 3, "Medium")
                        .AddGraphicForValue("/images/HomeSeer/status/fan-state-high.png", 3, "High");
                }
                else
                {
                    factory.AddButton(0, "Off", new ControlLocation(1, 1), EControlUse.Off)
                        .AddSlider(new ValueRange(1, 99), new ControlLocation(1, 2), EControlUse.DimFan)
                        .AddButton(100, "On", new ControlLocation(1, 3), EControlUse.On)
                        .AddGraphicForValue("/images/HomeSeer/status/fan-state-off.png", 0, "Off")
                        .AddGraphicForValue("/images/HomeSeer/status/fan-state-on.png", 1, "On");
                }
            }
            else
            {
                factory.AddButton(0, "Off", new ControlLocation(1, 1), EControlUse.Off)
                        .AddButton(1, "On", new ControlLocation(1, 2), EControlUse.On)
                        .AddGraphicForValue("/images/HomeSeer/status/fan-state-off.png", 0, "Off")
                        .AddGraphicForValue("/images/HomeSeer/status/fan-state-on.png", 1, "On");
            }
            var controlsFeature = Device.GetOrCreateFeature($"{EntityData.UniqueId}-controls", factory);

            FeatureIds.Add("controls", controlsFeature.Ref);
            ControlEvents.Add(controlsFeature.Ref, (ControlEvent controlEvent) =>
            {
                FanCommandRequest request = null;
                if (EntityData.SupportsSpeed)
                {
                    request = new FanCommandRequest
                    {
                        Key = Key,
                        State = controlEvent.ControlValue > 0,
                        HasState = true,
                        SpeedLevel = (int)controlEvent.ControlValue,
                        HasSpeedLevel = true
                    };
                }
                else
                {
                    request = new FanCommandRequest
                    {
                        Key = Key,
                        State = controlEvent.ControlValue == 1,
                        HasState = true
                    };
                }
                Device.SendMessage(request);
            });

            return ControlEvents;
        }
        public override void HandleMessage(IExtensible message)
        {
            if (message is FanStateResponse state && state.Key == EntityData.Key)
            {
                if (FeatureIds.TryGetValue("controls", out int controlsFeature))
                {
                    if (EntityData.SupportsSpeed && EntityData.SupportedSpeedCount > 1)
                    {
                        if (!state.State)
                        {
                            Device.UpdateFeature(controlsFeature, 0, "Off");
                        }
                        else if (EntityData.SupportedSpeedCount == 2)
                        {
                            Device.UpdateFeature(controlsFeature, state.SpeedLevel, state.SpeedLevel == 1 ? "Low" : "High");
                        }
                        else if (EntityData.SupportedSpeedCount == 3)
                        {
                            switch (state.SpeedLevel)
                            {
                                case 1:
                                    Device.UpdateFeature(controlsFeature, 1, "Low");
                                    break;
                                case 2:
                                    Device.UpdateFeature(controlsFeature, 1, "Medium");
                                    break;
                                case 3:
                                    Device.UpdateFeature(controlsFeature, 1, "High");
                                    break;
                            }
                        }
                        else
                        {
                            if (state.SpeedLevel > 99)
                                Device.UpdateFeature(controlsFeature, 100, "On");
                            else
                            {
                                float speed = ((float)state.SpeedLevel) / 100.0f;
                                Device.UpdateFeature(controlsFeature, ((speed * 98.0) + 1.0), $"{(state.SpeedLevel):F0}%");
                            }
                        }
                    }
                    else
                    {
                        if (state.State)
                            Device.UpdateFeature(controlsFeature, 1, "On");
                        else
                            Device.UpdateFeature(controlsFeature, 0, "Off");
                    }
                }
            }
        }

        public override void RequestStatusUpdate()
        {
            Device.SendMessage(new FanCommandRequest
            {
                Key = Key
            });
        }

    }
}
