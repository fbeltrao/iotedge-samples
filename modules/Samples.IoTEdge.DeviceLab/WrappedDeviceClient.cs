using System;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;

namespace Samples.IoTEdge.DeviceLab
{
    public class WrappedDeviceClient : IDeviceClient
    {
        private readonly DeviceClient deviceClient;

        public WrappedDeviceClient(DeviceClient deviceClient)
        {
            this.deviceClient = deviceClient ?? throw new System.ArgumentNullException(nameof(deviceClient));
        }

        public uint OperationTimeoutInMilliseconds
        { 
            get => deviceClient.OperationTimeoutInMilliseconds;
            set => deviceClient.OperationTimeoutInMilliseconds = value;
        }

        public Task CompleteAsync(Message c2dMsg) => deviceClient.CompleteAsync(c2dMsg);

        public Task OpenAsync() => deviceClient.OpenAsync();

        public Task<Message> ReceiveAsync(TimeSpan timeSpan) => deviceClient.ReceiveAsync(timeSpan);

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) => deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

        public Task SendEventAsync(Message message) => deviceClient.SendEventAsync(message);

        public Task AbandonAsync(Message message) => deviceClient.AbandonAsync(message);

        public void SetConnectionStatusChangesHandler(ConnectionStatusChangesHandler statusChangesHandler) => deviceClient.SetConnectionStatusChangesHandler(statusChangesHandler);

        public void SetRetryPolicy(IRetryPolicy retryPolicy) => deviceClient.SetRetryPolicy(retryPolicy);
    }
}