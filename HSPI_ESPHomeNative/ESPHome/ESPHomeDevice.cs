using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Sockets;
using HomeSeer.PluginSdk.Devices;
using HSPI_ESPHomeNative.ESPHome.Entities;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices.Controls;
using System.Timers;
using ProtoBuf;
using System.IO;
using static HSPI_ESPHomeNative.ESPHome.ProtocolTypeMap;
using System.Runtime.InteropServices.ComTypes;
using Noise;

namespace HSPI_ESPHomeNative.ESPHome
{
    public enum DisconnectReason
    {
        Unknown = 0,
        DisconnectRequested = 1,
        ConnectionReset = 2,
        Timeout = 3
    }

    internal class ESPHomeDevice
    {
        public string Name { get; private set; }
        public DeviceInfo Info { get; private set; }
        public bool UseEncryption { get; private set; }
        public string FriendlyName { get; private set; }
        public List<IEntity> Entities{ get; private set; } = new List<IEntity>();

        public delegate void DisconnectedHandler(ESPHomeDevice sender, DisconnectReason reason);
        public event DisconnectedHandler OnDisconnected;

        Dictionary<int, Action<ControlEvent>> eventCallbacks = new();

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
        private byte[] _devicePSK = null;
        private string devicePassword;

        public ESPHomeDevice(DeviceInfo deviceInfo, string devicePSK)
        {
            //devicePSK = "9qvHujCL96ldR10I59yIDN9IViQzSDb45Kc3QAHkm9E=";
            Info = deviceInfo;
            Name = deviceInfo.Name;
            UseEncryption = !String.IsNullOrEmpty(devicePSK);
            if (UseEncryption)
                _devicePSK = Convert.FromBase64String(devicePSK);
            _client = new TcpClient();
            pingTimeout.Interval = 10000;
            pingTimeout.AutoReset = true;
            pingTimeout.Elapsed += PingTimeout_Elapsed;
        }

        private void PingTimeout_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (DateTime.UtcNow - _lastMessage > TimeSpan.FromSeconds(90))
                Disconnect(DisconnectReason.Timeout);
        }

        byte[] writeBuf = new byte[4096];
        byte[] payloadBuf = new byte[4096];
        public async Task<ConnectResponse> Connect(string password = "", bool isReconnect = false)
        { 
            devicePassword = password;
            int retryCounter = 0;
            while (!_connected)
            {
                try
                {
                    _client.NoDelay = true;
                    await _client.ConnectAsync(Info.Address, Info.Port);
                    if(UseEncryption)
                    {
                       /* var protocol = Protocol.Parse("Noise_NNpsk0_25519_ChaChaPoly_SHA256".AsSpan());
                        byte[] prologue = Encoding.ASCII.GetBytes("NoiseAPIInit\0");
                        var initiator = protocol.Create(true, prologue, psks: new List<byte[]> { _devicePSK });


                        writeBuf[0] = 1;
                        writeBuf[1] = 0;
                        writeBuf[2] = 0;
                        _client.GetStream().WriteAsync(writeBuf, 0, 3);


                        // Read the hello response
                        int read = await _client.GetStream().ReadAsync(writeBuf, 0, 4096);
                        var indicator = writeBuf[0];
                        var msgLen = ((uint)writeBuf[1] << 8 | (uint)writeBuf[2]);
                        var protochoice = writeBuf[3]; // We only have one proto choice, so this should always be 1
                        var dat = Encoding.UTF8.GetString(writeBuf, 4, (int)msgLen-1); // we just get the name back here.

                        // now we can actually send data.
                        (var written, _, var transport) = initiator.WriteMessage(null, writeBuf.AsSpan(4));
                        written += 1;
                        writeBuf[0] = 1;
                        writeBuf[1] = (byte)((written >> 8) & 0xFF);
                        writeBuf[2] = (byte)((written >> 0) & 0xFF);
                        writeBuf[3] = 0x00;
                        await _client.GetStream().WriteAsync(writeBuf, 0, written + 4);

                        read = await _client.GetStream().ReadAsync(writeBuf, 0, 4096);
                        indicator = writeBuf[0];
                        msgLen = ((uint)writeBuf[1] << 8 | (uint)writeBuf[2]);
                        dat = Encoding.UTF8.GetString(writeBuf, 4, (int)msgLen - 1);
                        (int bytesRead, _, _) = initiator.ReadMessage(writeBuf.AsSpan(3, (int)msgLen), payloadBuf.AsSpan());*/
                    }

                    _connected = true;
                }
                catch (SocketException ex)
                {
                    // If we were previously connected, we should retry indefinitely.
                    if (retryCounter++ > 5 && !isReconnect)
                        throw ex;
                    retryCounter++;
                    await Task.Delay(2000);
                }
            }
            _ = Task.Run(Reader);
            // We can skip the hello portion of the exchange,
            // since we're not using the noise encryption yet.

            ConnectRequest request = new()
            {
                Password = devicePassword
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
            if(!_client.Connected)
            {
                Connect().GetAwaiter().GetResult(); ;
            }
            NetworkStream netStream = _client.GetStream();
            MeasureState<T> t = Serializer.Measure(message);
            netStream.WriteByte(0);
            EncodeVarUInt32(netStream, (uint)t.Length);
            EncodeVarUInt32(netStream, (uint)ProtocolTypeMap.GetOutgoingMessageType(message));
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
                        throw new Exception("Device is sending encrypted data.");

                    int messageLength = (int)DecodeVarUInt32(netstream);
                    IncomingMessageType messageType = (IncomingMessageType)DecodeVarUInt32(netstream);

                    if (messageLength > 8192)
                        throw new Exception("Incoming message is too large");
                    _lastMessage = DateTime.UtcNow;

                    IExtensible message = GetIncomingMessage(netstream, messageType, messageLength);
                    switch (message)
                    {
                        case HelloResponse helloResponse:
                            Console.WriteLine(helloResponse);
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
                        case DisconnectRequest:
                            Disconnect(DisconnectReason.DisconnectRequested);
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
                            Disconnect(DisconnectReason.DisconnectRequested);
                            break;
                        case SocketError.ConnectionReset:
                            Disconnect(DisconnectReason.ConnectionReset);
                            break;
                    }
                }
            }
        }

        internal void Disconnect(DisconnectReason reason = DisconnectReason.Unknown)
        {
            _connected = false;
            _client.Close();
            pingTimeout.Stop();
            OnDisconnected?.Invoke(this, reason);
        }

        internal void UpdateFeature(int featureId, double value, string valueString)
        {
            OnFeatureUpdate?.Invoke(featureId, value, valueString);
        }

        private void EncodeUInt16(NetworkStream netStream, ushort value)
        {
            netStream.WriteByte((byte)((value >> 8) & 0xFF));
            netStream.WriteByte((byte)((value >> 0) & 0xFF));
        }

        private void EncodeVarUInt32(NetworkStream netStream, uint value)
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

        private uint DecodeVarUInt32(NetworkStream netStream)
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
