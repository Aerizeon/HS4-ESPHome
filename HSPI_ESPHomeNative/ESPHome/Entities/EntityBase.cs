using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using System;
using System.Collections.Generic;

namespace HSPI_ESPHomeNative.ESPHome.Entities
{
    internal class EntityBase<T> : IEntity
    {
        protected ESPHomeDevice Device { get; }
        protected Dictionary<int, Action<ControlEvent>> ControlEvents { get; }
        protected Dictionary<string, int> FeatureIds { get; }
        protected T EntityData { get; set; }

        public EntityBase(ESPHomeDevice device, T entityData, string id, uint key)
        {
            Device = device;
            Id = id;
            Key = key;
            ControlEvents = new Dictionary<int, Action<ControlEvent>>();
            FeatureIds = new Dictionary<string, int>();
            EntityData = entityData;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public uint Key { get; private set; }

        public virtual Dictionary<int, Action<ControlEvent>> ProcessFeatures(HsDevice device, IHsController homeSeer) { throw new NotImplementedException(); }
        public virtual void RequestStatusUpdate() {  throw new NotImplementedException(); }
    }
}
