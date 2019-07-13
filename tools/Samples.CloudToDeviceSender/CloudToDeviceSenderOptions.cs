using CommandLine;

namespace Samples.CloudToDeviceSender
{
    public class CloudToDeviceSenderOptions
    {
        [Option('c', "connection-string", Required = true, HelpText = "Sets the IoT Hub connection string (HostName=xzy.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=xyz)")]
        public string ConnectionString { get; set; }

        [Option('d', "device-id", Required = true, HelpText = "Sets the device identifier to send messages to")]
        public string DeviceId { get; set; }

        [Option('t', "time-between", Required = false, HelpText = "Sets the delay in seconds between each message sent")]
        public int TimeBetweenMessages { get; set; } = 30;

        [Option('s', "seed", Required = false, HelpText = "Message seed to start with (default=1, will be the messageId and body)")]
        public int MessageSeed { get; set; } = 1;
    }
}
