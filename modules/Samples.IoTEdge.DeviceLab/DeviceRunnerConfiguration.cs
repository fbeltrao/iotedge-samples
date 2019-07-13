using Microsoft.Azure.Devices.Client;

namespace Samples.IoTEdge.DeviceLab
{
    public class DeviceRunnerConfiguration
    {
        public bool UpdateTwin { get; set; } = true;
        public bool CloudToDeviceMessage { get; set; } = true;        
        
        public bool CheckPendingCloudToDeviceMessage { get; set; } = true;
        public bool CompleteCloudToDeviceMessage { get; set; } = true;
        public int DeviceLoopDelay { get; set; } = 5_000;
        public int DeviceKickoffDelay { get; set; } = 5_000;
        public bool SendEvent { get; set; } = false;
        public bool QueuedAsyncReceived { get; set; } = true;
        public string Protocol { get; set; } = "amqp";
        public bool AmqpMultiplex { get; set; } = true;
        public string DeviceList { get; set; }
        public bool EdgeGateway { get; set; } = true;
        public string DeviceSharedAccessKey { get; set; }
        public int DelayAfterIotHubCommunicationError { get; set; } = 5_000;

        public uint DeviceOperationTimeoutInMilliseconds { get; set; } = DeviceClient.DefaultOperationTimeoutInMilliseconds;
        public bool DeviceRetryPolicy { get; set; } = true;
    }
}