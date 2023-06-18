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
using System.Net;
using System.Collections.Concurrent;
using System.Threading;
using System.Security.Cryptography;
using HomeSeer.PluginSdk.Logging;
using System.Timers;
using ProtoBuf;
using Google.Protobuf.Reflection;
using ProtoBuf.Reflection;
using Newtonsoft.Json.Linq;
using ProtoBuf.Meta;
using System.CodeDom;
using System.Reflection;
using System.IO;
using static HSPI_ESPHomeNative.ESPHome.ProtocolTypeMap;
using System.Runtime.InteropServices.ComTypes;

namespace HSPI_ESPHomeNative.ESPHome
{
    internal class ESPHomeDevice
    {
        public string Name { get; private set; }
        public string FriendlyName { get; private set; }
        public List<IEntity> Entities{ get; private set; } = new List<IEntity>();
        public string Id { get; private set; }
        public IPAddress Address { get; private set; }
        public int Port { get; private set; }

        public delegate void DisconnectedHandler(ESPHomeDevice sender);
        public event DisconnectedHandler OnDisconnected;

        Dictionary<int, Action<ControlEvent>> eventCallbacks = new Dictionary<int, Action<ControlEvent>>();

        public delegate void FeatureUpdateHandler(int refId, double value, string valueString);
        public event FeatureUpdateHandler OnFeatureUpdate;



        private int devFeatureCount = 0;
        private HsDevice _device;
        private IHsController _homeSeer;
        private bool _connected = false;
        private DateTime _lastMessage = DateTime.UtcNow;
        private TcpClient _client;
        private TaskCompletionSource<ConnectResponse> _tcsConnected = new TaskCompletionSource<ConnectResponse>();
        private TaskCompletionSource<DeviceInfoResponse> _tcsDeviceInfo = new TaskCompletionSource<DeviceInfoResponse>();
        private TaskCompletionSource<object> _tcsListEntitiesDone = new TaskCompletionSource<object>();
        private System.Timers.Timer pingTimeout = new System.Timers.Timer();

        public ESPHomeDevice(string name, string address, int port, string macAddress)
        {
            Name = name;
            Id = macAddress;
            Address = IPAddress.Parse(address);
            Port = port;
            _client = new TcpClient();
            pingTimeout.Interval = 10000;
            pingTimeout.AutoReset = true;
            pingTimeout.Elapsed += PingTimeout_Elapsed;
        }

        private void PingTimeout_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (DateTime.UtcNow - _lastMessage > TimeSpan.FromSeconds(90))
                Disconnect();
        }

        public async Task<ConnectResponse> Connect(string password = "")
        {
            await _client.ConnectAsync(Address, Port);
            _connected = true;
            _ = Task.Run(Reader);
            // We can skip the hello portion of the exchange,
            // since we're not using the noise encryption yet.

            ConnectRequest request = new ConnectRequest
            {
                Password = password
            };
            SendMessage(request);
            pingTimeout.Start();
            return await _tcsConnected.Task;
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
                foreach(var devFeature in entity.ProcessFeatures())
                {
                    eventCallbacks.Add(devFeature.Key, devFeature.Value);
                }
                entity.RequestStatusUpdate();
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
                        .WithDisplayType(devFeatureCount++ == 0 ? EFeatureDisplayType.Important : EFeatureDisplayType.Normal)
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

        public void SendMessage<T>(T message) where T : ProtoBuf.IExtensible
        {
            NetworkStream netStream = _client.GetStream();
            MeasureState<T> t = Serializer.Measure(message);
            netStream.WriteByte(0);
            EncodeUInt32(netStream, (uint)t.Length);
            EncodeUInt32(netStream, (uint)ProtocolTypeMap.GetOutgoingMessageType(message));
            t.Serialize(netStream);
        }

        private void Reader()
        {
            NetworkStream netstream = _client.GetStream();
            
            try
            {
                while (_connected)
                {

                    if (netstream.ReadByte() != 0)
                        throw new Exception("Invalid Indicator");

                    int messageLength = (int)DecodeUInt32(netstream);
                    IncomingMessageType messageType = (IncomingMessageType)DecodeUInt32(netstream);

                    if (messageLength > 8192)
                        throw new Exception("Incoming message is too large");
                    _lastMessage = DateTime.UtcNow;

                    IExtensible message = GetIncomingMessage(netstream, messageType, messageLength);
                    switch (message)
                    {
                        case HelloResponse:
                            break;
                        case ConnectResponse connectResponse:
                            _tcsConnected.SetResult(connectResponse);
                            if (connectResponse.InvalidPassword)
                            {
                                _client.Close();
                                return;
                            }
                            SendMessage(new SubscribeStatesRequest());
                            break;
                        case PingRequest:
                            SendMessage(new PingResponse());
                            break;
                        case DeviceInfoResponse deviceInfoResponse:
                            _tcsDeviceInfo.SetResult(deviceInfoResponse);
                            break;
                        case ListEntitiesDoneResponse:
                            _tcsListEntitiesDone.SetResult(null);
                            break;
                        case ListEntitiesFanResponse fanEntity:
                            Entities.Add(new FanEntity(this, fanEntity));
                            break;
                        case ListEntitiesSwitchResponse switchEntity:
                            Entities.Add(new SwitchEntity(this, switchEntity));
                            break;
                        case ListEntitiesLightResponse lightEntity:
                            Entities.Add(new LightEntity(this, lightEntity));
                            break;
                        case ListEntitiesButtonResponse buttonEntity:
                            Entities.Add(new ButtonEntity(this, buttonEntity));
                            break;
                        default:
                            Entities.ForEach(E => E.HandleMessage(message));
                            break;
                    };
                }
            }

            catch(IOException ex)
            {
                if(ex.InnerException is SocketException e)
                {
                    switch(e.SocketErrorCode)
                    {
                        case SocketError.Disconnecting:
                        case SocketError.ConnectionReset:
                            Disconnect();
                            break;
                    }
                }
            }
        }

        internal void Disconnect()
        {
            
            _client.Close();
            pingTimeout.Stop();
            OnDisconnected?.Invoke(this);
        }

        internal void UpdateFeature(int featureId, double value, string valueString)
        {
            OnFeatureUpdate?.Invoke(featureId, value, valueString);
        }

        private void EncodeUInt32(NetworkStream netStream, uint value)
        {
            do
            {
                byte lower7bits = (byte)(value & 0x7f);
                value >>= 7;
                if (value > 0)
                    lower7bits |= 128;
                netStream.WriteByte(lower7bits);
            } while (value > 0);
        }

        private uint DecodeUInt32(NetworkStream netStream)
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
