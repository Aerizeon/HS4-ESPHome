using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HSPI_ESPHomeNative.ESPHome.Entities
{
    internal class LightEntity : EntityBase<ListEntitiesLightResponse>
    {
        internal LightEntity(ESPHomeDevice device, ListEntitiesLightResponse lightEntity) 
            : base(device, lightEntity, lightEntity.UniqueId, lightEntity.Key)
        {
        }

        public override Dictionary<int, Action<ControlEvent>> ProcessFeatures()
        {

            var controlsFactory = FeatureFactory.CreateGenericBinaryControl(Program._plugin.Id, $"{EntityData.Name} State", "On", "Off", 100, 0)
            .WithDisplayType(EFeatureDisplayType.Important)
                .AddSlider(new ValueRange(1, 99), controlUse: EControlUse.Dim)
                .AddGraphicForValue("images/HomeSeer/status/unknown.png", -1.0, "Offline")
                .AddGraphicForRange("images/HomeSeer/status/dim-00.gif", 01.00f, 10f)
                .AddGraphicForRange("images/HomeSeer/status/dim-10.gif", 10.01f, 20f)
                .AddGraphicForRange("images/HomeSeer/status/dim-20.gif", 20.01f, 30f)
                .AddGraphicForRange("images/HomeSeer/status/dim-30.gif", 30.01f, 40f)
                .AddGraphicForRange("images/HomeSeer/status/dim-40.gif", 40.01f, 50f)
                .AddGraphicForRange("images/HomeSeer/status/dim-50.gif", 50.01f, 60f)
                .AddGraphicForRange("images/HomeSeer/status/dim-60.gif", 60.01f, 70f)
                .AddGraphicForRange("images/HomeSeer/status/dim-70.gif", 70.01f, 80f)
                .AddGraphicForRange("images/HomeSeer/status/dim-80.gif", 80.01f, 90f)
                .AddGraphicForRange("images/HomeSeer/status/dim-90.gif", 90.01f, 99f);

            var controlsFeature = Device.GetOrCreateFeature("controls", controlsFactory);
               
                

            FeatureIds.Add("controls", controlsFeature.Ref);

            ControlEvents.Add(controlsFeature.Ref, (ControlEvent controlEvent) =>
            {
                if (controlEvent.ControlValue == 0)
                    SetState(false);
                else if (controlEvent.ControlValue < 100)
                    SetBrightness((float)((controlEvent.ControlValue - 1.0) / 98.0));
                else if (controlEvent.ControlValue == 100)
                    SetState(true);
            });
        

            if (EntityData.SupportedColorModes.Contains(ColorMode.ColorModeRgb))
            {
                
                var colorFeature = Device.GetOrCreateFeature("color", FeatureFactory.CreateFeature(Program._plugin.Id)
                    .WithName($"{EntityData.Name} Color")
                    .AddColorPicker(0, controlUse: EControlUse.ColorControl)
                    .AddGraphicForValue("images/HomeSeer/status/custom-color.png", 0));

                FeatureIds.Add("color", colorFeature.Ref);

                ControlEvents.Add(colorFeature.Ref, (ControlEvent controlEvent) =>
                {
                    int color = Int32.Parse(controlEvent.ControlString, NumberStyles.HexNumber);

                    float red = (color >> 16 & 0xFF);
                    float green = (color >> 8 & 0xFF);
                    float blue = (color >> 0 & 0xFF);

                    SetColor(red / 255.0f, green / 255.0f, blue / 255.0f);
                });
            }

            if(EntityData.Effects.Count > 0)
            {
                SortedDictionary<string, double> effects = new SortedDictionary<string, double>();
                foreach (var effect in EntityData.Effects)
                    effects.Add(effect, effects.Count);

                var effectFeature = Device.GetOrCreateFeature("effect", FeatureFactory.CreateFeature(Program._plugin.Id)
                    .WithName($"{EntityData.Name} Effect").
                    AddTextDropDown(effects, controlUse: EControlUse.OnAlternate));
                FeatureIds.Add("effect", effectFeature.Ref);

                ControlEvents.Add(effectFeature.Ref, (ControlEvent controlEvent) =>
                {
                    SetEffect(controlEvent.Label);
                });
            }
            return ControlEvents;
        }

        public void UpdateState(LightStateResponse state)
        {
            if (FeatureIds.TryGetValue("controls", out int controlsFeature))
            {
                if (state.State == false)
                    Device.UpdateFeature(controlsFeature, 0, "Off");
                else
                {
                    if (state.State == true && state.Brightness > 0.99)
                        Device.UpdateFeature(controlsFeature, 100, "On");
                    else
                        Device.UpdateFeature(controlsFeature, ((state.Brightness * 98.0) + 1.0), $"{(state.Brightness * 100.0):F0}%");
                }
            }

            if (FeatureIds.TryGetValue("color", out int colorFeature))
            {
                byte red = (byte)Math.Min(255, Math.Round(state.Red * 255.0f));
                byte green = (byte)Math.Min(255, Math.Round(state.Green * 255.0f));
                byte blue = (byte)Math.Min(255, Math.Round(state.Blue * 255.0f));
                Device.UpdateFeature(colorFeature, 0.0, $"{red:X2}{green:X2}{blue:X2}");
            }

            if (FeatureIds.TryGetValue("effect", out int effectFeature))
            {
                Device.UpdateFeature(effectFeature, EntityData.Effects.IndexOf(state.Effect), state.Effect);
            }
        }

        public override void RequestStatusUpdate()
        {
            LightCommandRequest lightCommand = new LightCommandRequest()
            {
                Key = Key
            };
            Device.SendMessage(lightCommand);
        }

        public override void HandleMessage(IExtensible message)
        {
            if(message is LightStateResponse state && state.Key == Key)
            {
                UpdateState(state);
            }
        }

        public void SetState(bool state)
        {
            LightCommandRequest lightCommand = new LightCommandRequest()
            {
                Key = this.Key,
                HasState = true,
                State = state,
                Brightness = 1.0f,
                HasBrightness = true
            };

            Device.SendMessage(lightCommand);
        }

        public void SetBrightness(float brightness)
        {
            LightCommandRequest lightCommand = new LightCommandRequest()
            {
                Key = this.Key,
                HasState = true,
                State = true,
                HasBrightness = true,
                Brightness = brightness,
            };
             Device.SendMessage(lightCommand);
        }

        public void SetColor(float red, float green, float blue)
        {
            LightCommandRequest lightCommand = new LightCommandRequest()
            {
                Key = this.Key,
                HasState = true,
                State = true,
                ColorMode = ColorMode.ColorModeRgb,
                HasColorMode = true,
                Effect = "None",
                HasEffect = true,
                Red = red,
                Green = green,
                Blue = blue,
                HasRgb = true
            };
            Device.SendMessage(lightCommand);
        }

        public void SetEffect(string effect)
        {
            LightCommandRequest lightCommand = new LightCommandRequest()
            {
                Key = this.Key,
                HasState = true,
                State = true,
                ColorMode = ColorMode.ColorModeRgb,
                Effect = effect,
                HasEffect= true
            };
            Device.SendMessage(lightCommand);
        }
    }
}
