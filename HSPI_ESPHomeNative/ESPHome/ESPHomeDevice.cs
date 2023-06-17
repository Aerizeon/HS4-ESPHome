using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using Google.Protobuf;
using System.Net.Configuration;
using HomeSeer.PluginSdk.Devices;
using HSPI_ESPHomeNative.ESPHome.Entities;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices.Controls;
using Esphome;
using System.Net;
using System.Collections.Concurrent;
using System.Threading;
using System.Security.Cryptography;
using HomeSeer.PluginSdk.Logging;

namespace HSPI_ESPHomeNative.ESPHome
{
    public enum MessageType : uint
    {
        HelloResponse = 2,
        ConnectResponse = 4,
        DisconnectResponse = 6,
        PingRequest = 7,
        PingResponse = 8,
        DeviceInfoResponse = 10,
        ListEntitiesDoneResponse = 19,

        ListEntitiesBinarySensorResponse = 12,
        BinarySensorStateResponse = 21,

        ListEntitiesCoverResponse = 13,
        CoverStateResponse = 22,

        ListEntitiesFanResponse = 14,
        FanStateResponse = 23,
        FanCommandRequest = 31,

        ListEntitiesLightResponse = 15,
        LightStateResponse = 24,
        LightCommandRequest = 32,

        ListEntitiesSensorResponse = 16,
        SensorStateResponse = 25,

        ListEntitiesSwitchResponse = 17,
        SwitchStateResponse = 26,
        SwitchCommandRequest = 33,

    }
    internal class ESPHomeDevice
    {
        public string Name { get; private set; }

        public string FriendlyName { get; private set; }

        public List<IEntity> Entities{ get; private set; } = new List<IEntity>();

        public string Id { get; private set; }

        public IPAddress Address { get; private set; }
        public int Port { get; private set; }

        Dictionary<int, Action<ControlEvent>> eventCallbacks = new Dictionary<int, Action<ControlEvent>>();

        ConcurrentQueue<IMessage> outgoingMessages = new ConcurrentQueue<IMessage>();
        SemaphoreSlim newMessage = new SemaphoreSlim(0);

        public delegate void FeatureUpdateHandler(int refId, double value, string valueString);
        public event FeatureUpdateHandler OnFeatureUpdate;
        private TcpClient _client;
        private TaskCompletionSource<ConnectResponse> _tcsConnected = new TaskCompletionSource<ConnectResponse>();
        private TaskCompletionSource<DeviceInfoResponse> _tcsDeviceInfo = new TaskCompletionSource<DeviceInfoResponse>();
        private TaskCompletionSource<object> _tcsListEntitiesDone = new TaskCompletionSource<object>();

        private HsDevice _device;
        private IHsController _homeSeer;

        public ESPHomeDevice(string name, string address, int port, string macAddress)
        {
            Name = name;
            Id = macAddress;
            Address = IPAddress.Parse(address);
            Port = port;
            _client = new TcpClient();
        }

        public async Task<ConnectResponse> Connect(string password = "")
        {
            await _client.ConnectAsync(Address, Port);
            RunBackgroundWorkers();
            // We can skip the hello portion of the exchange,
            // since we're not using the noise encryption yet.

            ConnectRequest request = new ConnectRequest
            {
                Password = password
            };
            SendMessage(request);
           
            return await _tcsConnected.Task;
        }

        async void RunBackgroundWorkers()
        {
            await Task.WhenAll(Task.Run(Reader), Task.Run(Writer));
        }

        public async Task QueryDeviceInformation()
        {
            SendMessage(new DeviceInfoRequest());
            SendMessage(new ListEntitiesRequest());
            await Task.WhenAll(_tcsDeviceInfo.Task, _tcsListEntitiesDone.Task);
            DeviceInfoResponse result = _tcsDeviceInfo.Task.Result;
            Name = string.IsNullOrEmpty(result.FriendlyName) ? result.Name : result.FriendlyName;
        }

        public void PrepareFeatures(HsDevice device, IHsController homeSeer)
        {
            _device = device;
            _homeSeer = homeSeer;
            foreach (var entity in Entities)
            {
                foreach(var devFeature in entity.ProcessFeatures(device, homeSeer))
                {
                    eventCallbacks.Add(devFeature.Key, devFeature.Value);
                }
            }
        }

        public HsFeature GetOrCreateFeature(string address, FeatureFactory featureFactory)
        {
            if (featureFactory == null)
                throw new ArgumentNullException(nameof(featureFactory));

            var searchFeature = _device.Features.SingleOrDefault(f => f.Address == address);

            if (searchFeature is null)
            {
                searchFeature = _homeSeer.GetFeatureByRef(
                    _homeSeer.CreateFeatureForDevice(
                        featureFactory.WithAddress(address)
                        .PrepareForHsDevice(_device.Ref)));
            }
            return searchFeature;
        }

        public void ProcessEvent(ControlEvent controlEvent)
        {
            if(eventCallbacks.TryGetValue(controlEvent.TargetRef, out Action<ControlEvent> callback))
            {
                callback(controlEvent);
            }
        }

        public void RequestStatusUpdate()
        {
            foreach(IEntity entity in Entities)
            {
                entity.RequestStatusUpdate();
            }
        }

        public void SendMessage<T>(T message) where T : IMessage
        {
            outgoingMessages.Enqueue(message);
            newMessage.Release();
        }

        private async void Writer()
        {
            NetworkStream netStream = _client.GetStream();
            while (true)
            {
                if (!outgoingMessages.IsEmpty && outgoingMessages.TryDequeue(out IMessage message))
                {
                    uint messageType = 0;
                    var options = message.Descriptor.GetOptions();
                    if (options != null && options.HasExtension(EsphomeApiOptionsExtensions.Id))
                    {
                       messageType = options.GetExtension(EsphomeApiOptionsExtensions.Id);
                    }

                    int messageSize = message.CalculateSize();
                    byte[] headerBytes = new byte[11];
                    headerBytes[0] = 0x00;
                    int offset = EncodeUInt32(ref headerBytes, 1, (uint)messageSize);
                    offset += EncodeUInt32(ref headerBytes, offset + 1, messageType);

                    await netStream.WriteAsync(headerBytes, 0, offset + 1);
                    await netStream.WriteAsync(message.ToByteArray(), 0, messageSize);
                }
                else
                    newMessage.Wait(100);
            }
        }
        private async void Reader()
        {
            byte[] readBuffer = new byte[2048];

            NetworkStream netstream = _client.GetStream();
            while (true)
            {
                int readOffset = 0;

                if (netstream.ReadByte() != 0)
                    throw new Exception("Invalid Indicator");

                int messageSize = (int)DecodeUInt32(netstream);
                MessageType messageType = (MessageType)DecodeUInt32(netstream);

                if (messageSize > readBuffer.Length)
                    throw new Exception("Incoming message is too large");

                while (readOffset < messageSize)
                    readOffset += await _client.GetStream().ReadAsync(readBuffer, readOffset, messageSize - readOffset);


                switch (messageType)
                {
                    case MessageType.ConnectResponse:
                        ConnectResponse response = ConnectResponse.Parser.ParseFrom(readBuffer, 0, messageSize);
                        _tcsConnected.SetResult(response);
                        if (response.InvalidPassword)
                        {
                            _client.Close();
                            return;
                        }
                        SendMessage(new SubscribeStatesRequest());
                        break;
                    case MessageType.PingRequest:
                        SendMessage(new PingResponse());
                        break;
                    case MessageType.DeviceInfoResponse:
                            _tcsDeviceInfo.SetResult(DeviceInfoResponse.Parser.ParseFrom(readBuffer, 0, messageSize));
                        break;
                    case MessageType.ListEntitiesDoneResponse:
                        _tcsListEntitiesDone.SetResult(null);
                        break;
                    case MessageType.ListEntitiesLightResponse:
                        {

                            ListEntitiesLightResponse entity = ListEntitiesLightResponse.Parser.ParseFrom(readBuffer, 0, messageSize);
                            Entities.Add(new LightEntity(this, entity));
                        }
                        break;
                    case MessageType.LightStateResponse:
                        {
                            LightStateResponse lsr = LightStateResponse.Parser.ParseFrom(readBuffer, 0, messageSize);
                            (Entities.Find(E => E.GetType() == typeof(LightEntity) && E.Key == lsr.Key) as LightEntity).UpdateState(lsr);
                        }
                        break;
                }
            }
        }

        internal void UpdateFeature(int featureId, double value, string valueString)
        {
            OnFeatureUpdate?.Invoke(featureId, value, valueString);
        }

        public static int EncodeUInt32(ref byte[] buffer, int offset, uint value)
        {
            int bytes = 0;
            do
            {
                byte lower7bits = (byte)(value & 0x7f);
                value >>= 7;
                if (value > 0)
                    lower7bits |= 128;
                buffer[offset + bytes] = lower7bits;
                bytes++;
            } while (value > 0);
            return bytes;
        }

        public static uint DecodeUInt32(NetworkStream netStream)
        {
            bool more = true;
            int shift = 0;
            uint value = 0;
            while (more)
            {
                byte lower7bits = (byte)netStream.ReadByte();
                more = (lower7bits & 128) != 0;
                value |= (lower7bits & (uint)0x7f) << (shift * 7);
                shift++;
            }
            return value;
        }
    }
}
