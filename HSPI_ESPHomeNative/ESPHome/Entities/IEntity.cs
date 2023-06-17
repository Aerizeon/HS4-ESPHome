using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using System;
using System.Collections.Generic;

namespace HSPI_ESPHomeNative.ESPHome.Entities
{
    internal interface IEntity
    {
        string Name { get;}
        string Id { get;}
        uint Key { get; }
        Dictionary<int, Action<ControlEvent>> ProcessFeatures(HsDevice device, IHsController homeSeer);
        void RequestStatusUpdate();
    }
}
