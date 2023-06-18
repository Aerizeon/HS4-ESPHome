using HSCF.Communication.Scs.Communication.Messages;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace HSPI_ESPHomeNative.ESPHome
{
    internal static class ProtocolTypeMap
    {
        public static OutgoingMessageType GetOutgoingMessageType(IExtensible message) =>
            message switch
            {
                HelloRequest => OutgoingMessageType.HelloRequest,
                ConnectRequest => OutgoingMessageType.ConnectRequest,
                DisconnectRequest => OutgoingMessageType.DisconnectRequest,
                PingRequest => OutgoingMessageType.PingRequest,
                PingResponse => OutgoingMessageType.PingResponse,
                DeviceInfoRequest => OutgoingMessageType.DeviceInfoRequest,
                ListEntitiesRequest => OutgoingMessageType.ListEntitiesRequest,
                SubscribeStatesRequest => OutgoingMessageType.SubscribeStatesRequest,
                CoverCommandRequest => OutgoingMessageType.CoverCommandRequest,
                FanCommandRequest => OutgoingMessageType.FanCommandRequest,
                LightCommandRequest => OutgoingMessageType.LightCommandRequest,
                SwitchCommandRequest => OutgoingMessageType.SwitchCommandRequest,
                SubscribeLogsRequest => OutgoingMessageType.SubscribeLogsRequest,
                SubscribeHomeassistantServicesRequest => OutgoingMessageType.SubscribeHomeassistantServicesRequest,
                SubscribeHomeAssistantStatesRequest => OutgoingMessageType.SubscribeHomeAssistantStatesRequest,
                GetTimeRequest => OutgoingMessageType.GetTimeRequest,
                ExecuteServiceRequest => OutgoingMessageType.ExecuteServiceRequest,
                CameraImageRequest => OutgoingMessageType.CameraImageRequest,
                ClimateCommandRequest => OutgoingMessageType.ClimateCommandRequest,
                NumberCommandRequest => OutgoingMessageType.NumberCommandRequest,
                SelectCommandRequest => OutgoingMessageType.SelectCommandRequest,
                LockCommandRequest => OutgoingMessageType.LockCommandRequest,
                ButtonCommandRequest => OutgoingMessageType.ButtonCommandRequest,
                MediaPlayerCommandRequest => OutgoingMessageType.MediaPlayerCommandRequest,
                SubscribeBluetoothLEAdvertisementsRequest => OutgoingMessageType.SubscribeBluetoothLEAdvertisementsRequest,
                BluetoothDeviceRequest => OutgoingMessageType.BluetoothDeviceRequest,
                BluetoothGATTGetServicesRequest => OutgoingMessageType.BluetoothGATTGetServicesRequest,
                BluetoothGATTReadRequest => OutgoingMessageType.BluetoothGATTReadRequest,
                BluetoothGATTWriteRequest => OutgoingMessageType.BluetoothGATTWriteRequest,
                BluetoothGATTReadDescriptorRequest => OutgoingMessageType.BluetoothGATTReadDescriptorRequest,
                BluetoothGATTWriteDescriptorRequest => OutgoingMessageType.BluetoothGATTWriteDescriptorRequest,
                BluetoothGATTNotifyRequest => OutgoingMessageType.BluetoothGATTNotifyRequest,
                SubscribeBluetoothConnectionsFreeRequest => OutgoingMessageType.SubscribeBluetoothConnectionsFreeRequest,
                UnsubscribeBluetoothLEAdvertisementsRequest => OutgoingMessageType.UnsubscribeBluetoothLEAdvertisementsRequest,
                SubscribeVoiceAssistantRequest => OutgoingMessageType.SubscribeVoiceAssistantRequest,
                VoiceAssistantRequest => OutgoingMessageType.VoiceAssistantRequest,
                AlarmControlPanelCommandRequest => OutgoingMessageType.AlarmControlPanelCommandRequest,
                _ => throw new NotImplementedException()
            };

        public static IExtensible GetIncomingMessage(NetworkStream stream, IncomingMessageType messageType, int messageLength) =>
            messageType switch
            {
                IncomingMessageType.HelloResponse => Serializer.Deserialize<HelloResponse>(stream, length:messageLength),
                IncomingMessageType.ConnectResponse => Serializer.Deserialize<ConnectResponse>(stream, length:messageLength),
                IncomingMessageType.PingRequest => Serializer.Deserialize<PingRequest>(stream, length:messageLength),
                IncomingMessageType.PingResponse => Serializer.Deserialize<PingResponse>(stream, length:messageLength),
                IncomingMessageType.DeviceInfoResponse => Serializer.Deserialize<DeviceInfoResponse>(stream, length:messageLength),
                IncomingMessageType.ListEntitiesDoneResponse => Serializer.Deserialize<ListEntitiesDoneResponse>(stream, length:messageLength),
                IncomingMessageType.ListEntitiesFanResponse => Serializer.Deserialize<ListEntitiesFanResponse>(stream, length: messageLength),
                IncomingMessageType.FanStateResponse => Serializer.Deserialize<FanStateResponse>(stream, length: messageLength),
                IncomingMessageType.ListEntitiesSwitchResponse => Serializer.Deserialize<ListEntitiesSwitchResponse>(stream, length:messageLength),
                IncomingMessageType.SwitchStateResponse => Serializer.Deserialize<SwitchStateResponse>(stream, length:messageLength),
                IncomingMessageType.ListEntitiesLightResponse => Serializer.Deserialize<ListEntitiesLightResponse>(stream, length:messageLength),
                IncomingMessageType.LightStateResponse => Serializer.Deserialize<LightStateResponse>(stream, length:messageLength),
                IncomingMessageType.ListEntitiesButtonResponse => Serializer.Deserialize<ListEntitiesButtonResponse>(stream, length: messageLength),
                _ => throw new NotImplementedException()

            };

        public enum OutgoingMessageType
        {
            HelloRequest = 1,
            ConnectRequest = 3,
            DisconnectRequest = 5,
            PingRequest = 7,
            PingResponse = 8,
            DeviceInfoRequest = 9,
            ListEntitiesRequest = 11,
            SubscribeStatesRequest = 20,
            SubscribeLogsRequest = 28,
            CoverCommandRequest = 30,
            FanCommandRequest = 31,
            LightCommandRequest = 32,
            SwitchCommandRequest = 33,
            SubscribeHomeassistantServicesRequest = 34,
            GetTimeRequest = 36,
            SubscribeHomeAssistantStatesRequest = 38,
            ExecuteServiceRequest = 42,
            CameraImageRequest = 45,
            ClimateCommandRequest = 48,
            NumberCommandRequest = 51,
            SelectCommandRequest = 54,
            LockCommandRequest = 60,
            ButtonCommandRequest = 62,
            MediaPlayerCommandRequest = 65,
            SubscribeBluetoothLEAdvertisementsRequest = 66,
            BluetoothDeviceRequest = 68,
            BluetoothGATTGetServicesRequest = 70,
            BluetoothGATTReadRequest = 73,
            BluetoothGATTWriteRequest = 75,
            BluetoothGATTReadDescriptorRequest = 76,
            BluetoothGATTWriteDescriptorRequest = 77,
            BluetoothGATTNotifyRequest = 78,
            SubscribeBluetoothConnectionsFreeRequest = 80,
            UnsubscribeBluetoothLEAdvertisementsRequest = 87,
            SubscribeVoiceAssistantRequest = 89,
            VoiceAssistantRequest = 90,
            AlarmControlPanelCommandRequest = 96
        };

        public enum IncomingMessageType
        {
            HelloResponse = 2,
            ConnectResponse = 4,
            DisconnectResponse = 6,
            PingRequest = 7,
            PingResponse = 8,
            DeviceInfoResponse = 10,
            ListEntitiesDoneResponse = 19,
            ListEntitiesBinarySensorResponse = 12,
            ListEntitiesCoverResponse = 13,
            CoverStateResponse = 22,
            ListEntitiesFanResponse = 14,
            FanStateResponse = 23,
            ListEntitiesLightResponse = 15,
            LightStateResponse = 24,
            ListEntitiesSensorResponse = 16,
            SensorStateResponse = 25,
            ListEntitiesSwitchResponse = 17,
            SwitchStateResponse = 26,
            ListEntitiesTextSensorResponse = 18,
            TextSensorStateResponse = 27,
            SubscribeLogsResponse = 29,
            HomeassistantServiceResponse = 35,
            SubscribeHomeAssistantStateResponse = 39,
            GetTimeResponse = 37,
            ListEntitiesServicesResponse = 41,
            ListEntitiesCameraResponse = 43,
            CameraImageResponse = 44,
            ListEntitiesClimateResponse = 46,
            ClimateStateResponse = 47,
            ListEntitiesNumberResponse = 49,
            NumberStateResponse = 50,
            ListEntitiesSelectResponse = 52,
            SelectStateResponse = 53,
            ListEntitiesLockResponse = 58,
            LockStateResponse = 59,
            ListEntitiesButtonResponse = 61,
            ListEntitiesMediaPlayerResponse = 63,
            MediaPlayerStateResponse = 64,
            BluetoothLEAdvertisementResponse = 67,
            BluetoothLERawAdvertisementsResponse = 93,
            BluetoothDeviceConnectionResponse = 69,
            BluetoothGATTGetServicesResponse = 71,
            BluetoothGATTGetServicesDoneResponse = 72,
            BluetoothGATTReadResponse = 74,
            BluetoothGATTNotifyDataResponse = 79,
            BluetoothConnectionsFreeResponse = 81,
            BluetoothGATTErrorResponse = 82,
            BluetoothGATTWriteResponse = 83,
            BluetoothGATTNotifyResponse = 84,
            BluetoothDevicePairingResponse = 85,
            BluetoothDeviceUnpairingResponse = 86,
            BluetoothDeviceClearCacheResponse = 88,
            VoiceAssistantResponse = 91,
            VoiceAssistantEventResponse = 92,
            ListEntitiesAlarmControlPanelResponse = 94,
            AlarmControlPanelStateResponse = 95
        };
    }
}
