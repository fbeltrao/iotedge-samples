namespace Samples.IoTEdge.DeviceLab
{
    using System;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Extensions.Configuration;

    class Program
    {
        static DeviceRunnerConfiguration deviceRunnerConfiguration;

        static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            deviceRunnerConfiguration = new DeviceRunnerConfiguration();
            configuration.Bind(deviceRunnerConfiguration);


            var cts = new CancellationTokenSource();
            Init(cts.Token, deviceRunnerConfiguration).Wait();

            // Wait until the app unloads or is cancelled            
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init(CancellationToken cts, DeviceRunnerConfiguration deviceRunnerConfiguration)
        {
            var iotEdgeApiVersion = Environment.GetEnvironmentVariable("IOTEDGE_APIVERSION");
            var iotHubHostName = Environment.GetEnvironmentVariable("IOTEDGE_IOTHUBHOSTNAME");
            string gatewayHostName = null;
            if (!string.IsNullOrWhiteSpace(iotEdgeApiVersion))
            {            
                var transportSettings = string.Equals(deviceRunnerConfiguration.Protocol, "mqtt", StringComparison.OrdinalIgnoreCase) ? 
                    (ITransportSettings)new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) : new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
                
                ITransportSettings[] settings = { transportSettings };
                Logger.Log($"Using protocol type {transportSettings.GetTransportType().ToString()}");

                // Open a connection to the Edge runtime
                ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
                await ioTHubModuleClient.OpenAsync();
                Logger.Log("IoT Hub module client initialized");       

                if (deviceRunnerConfiguration.EdgeGateway)
                {
                    gatewayHostName = Environment.GetEnvironmentVariable("IOTEDGE_GATEWAYHOSTNAME");
                }
            }
            else
            {
                Logger.Log("Starting as non IoT Edge module");
                deviceRunnerConfiguration.EdgeGateway = false;
            }            

            var devices = deviceRunnerConfiguration.DeviceList.Split(new[] { ';', ','}, StringSplitOptions.RemoveEmptyEntries);
            if (devices.Length == 0)
            {
                Logger.Log($"No leaf devices configured in {deviceRunnerConfiguration.DeviceList}");
            }

            var deviceConnectionString = new StringBuilder();
            foreach (var deviceId in devices)
            {
                deviceId.Trim();
                if (string.IsNullOrWhiteSpace(deviceId))
                    continue;

                deviceConnectionString.Clear();
                if (!string.IsNullOrWhiteSpace(gatewayHostName))
                {
                    deviceConnectionString.Append("GatewayHostName=").Append(gatewayHostName).Append(';');
                }
                
                deviceConnectionString.Append("HostName=").Append(iotHubHostName).Append(';');
                deviceConnectionString.Append("DeviceId=").Append(deviceId).Append(';');
                deviceConnectionString.Append("SharedAccessKey=").Append(deviceRunnerConfiguration.DeviceSharedAccessKey).Append(';');

                var testRunner = new DeviceRunner(deviceId, deviceConnectionString.ToString(), deviceRunnerConfiguration);
                _ = Task.Run(() => testRunner.Start(cts));
                Logger.Log($"Started runner for device '{deviceId}'");

                await Task.Delay(deviceRunnerConfiguration.DeviceKickoffDelay);
            }            
        }
    }
}