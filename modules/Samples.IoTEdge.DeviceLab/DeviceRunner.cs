namespace Samples.IoTEdge.DeviceLab
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using System.Diagnostics;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Client.Exceptions;

    public class DeviceRunner
    {
        private readonly string deviceId;
        private readonly string connectionString;
        private readonly DeviceRunnerConfiguration deviceRunnerConfiguration;

        public DeviceRunner(string deviceId, string connectionString, DeviceRunnerConfiguration deviceRunnerConfiguration)
        {
            this.deviceId = deviceId;
            this.connectionString = connectionString;
            this.deviceRunnerConfiguration = deviceRunnerConfiguration;
        }

        IDeviceClient CreateDeviceClient()
        {
            var wrappedDeviceClient = new WrappedDeviceClient(DeviceClient.CreateFromConnectionString(
                this.connectionString,
                new[] { GetTransportSettings() }
                ));

            if (this.deviceRunnerConfiguration.QueuedAsyncReceived)
            {
                return new QueuedReceiverDeviceClient(deviceId, wrappedDeviceClient);
            }

            return wrappedDeviceClient;
        }

        private ITransportSettings GetTransportSettings()
        {
            ITransportSettings transportSettings;
            switch (this.deviceRunnerConfiguration.Protocol)
            {
                case "mqtt":
                    transportSettings = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
                    break;

                default:
                    {
                        var amqp = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);

                        if (this.deviceRunnerConfiguration.AmqpMultiplex)
                        {
                            amqp.AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                            {
                                Pooling = true,
                            };
                        }
                        transportSettings = amqp;
                        break;
                    }
            }

            return transportSettings;
        }

        public async Task Start(CancellationToken cts)
        {
            var correlationId = Guid.NewGuid().ToString(); 
            IDeviceClient deviceClient = null;
            var stopwatch = Stopwatch.StartNew();

            try
            {                               
                deviceClient = CreateDeviceClient();
                deviceClient.SetConnectionStatusChangesHandler(OnConnectionStatusChanged);
                deviceClient.OperationTimeoutInMilliseconds = deviceRunnerConfiguration.DeviceOperationTimeoutInMilliseconds;

                if (deviceRunnerConfiguration.DeviceRetryPolicy)
                {
                    deviceClient.SetRetryPolicy(new ExponentialBackoff(10, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(500)));
                }
                else
                {
                    deviceClient.SetRetryPolicy(new NoRetry());
                }

                await deviceClient.OpenAsync();
                stopwatch.Stop();
                Logger.Log($"{deviceId}: deviceClient created: {stopwatch.ElapsedMilliseconds}ms (retryPolicy={deviceRunnerConfiguration.DeviceRetryPolicy}, operationTimeoutInMilliseconds={deviceClient.OperationTimeoutInMilliseconds})");
            
            }
            catch (Exception ex)
            {
                Logger.Log($"{deviceId}: deviceClient creation failed: {ex.Message}");
                return;
            }

            var number = 0;
            var maxMsTwinUpdate = 0L;
            var numSlowTwin = 0;
            long maxMsC2D = 0;
            int numSlowC2D = 0;
            long maxMsSendEvent = 0;
            int numSlowSendEvent = 0;

            while (!cts.IsCancellationRequested)
            {
                number++;

                // Update Device Twin with increasing "Number"
                if (this.deviceRunnerConfiguration.UpdateTwin)
                {
                    try
                    {
                        var reportedProperties = new TwinCollection();
                        reportedProperties["Number"] = $"{correlationId}-{number}";
                        stopwatch.Restart();
                        Logger.Log($"{deviceId}: begin twins update {number}.");
                        await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
                        stopwatch.Stop();
                        if (maxMsTwinUpdate < stopwatch.ElapsedMilliseconds)
                            maxMsTwinUpdate = stopwatch.ElapsedMilliseconds;
                        if (stopwatch.ElapsedMilliseconds > 4000)
                            numSlowTwin++;
                        Logger.Log($"{deviceId}: twins updated {number}: {stopwatch.ElapsedMilliseconds}ms. Max: {maxMsTwinUpdate}ms. Delays: {numSlowTwin}");
                    }
                    catch (IotHubCommunicationException iotHubCommunicationException)
                    {
                        Logger.Log($"{deviceId}: iothub problem, waiting {deviceRunnerConfiguration.DelayAfterIotHubCommunicationError}ms. {iotHubCommunicationException.Message}");
                        await Task.Delay(deviceRunnerConfiguration.DelayAfterIotHubCommunicationError);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"{deviceId}: failed to get update twin. {ex.Message}");
                    }
                }


                if (this.deviceRunnerConfiguration.SendEvent)
                {      
                    try
                    {              
                        var message = new Message(Encoding.UTF8.GetBytes($"{correlationId}-{number}"));
                        message.CorrelationId = correlationId;
                        message.ContentType = "application/json";
                        stopwatch.Restart();
                        Logger.Log($"{deviceId}: begin send event {number}.");
                        await deviceClient.SendEventAsync(message);
                        stopwatch.Stop();                    
                        if (maxMsSendEvent < stopwatch.ElapsedMilliseconds)
                            maxMsSendEvent = stopwatch.ElapsedMilliseconds;
                        
                        if (stopwatch.ElapsedMilliseconds > 4000)
                            numSlowSendEvent++;
                        Logger.Log($"{deviceId}: send event {number}: {stopwatch.ElapsedMilliseconds}ms. Max: {maxMsSendEvent}ms. Delays: {numSlowSendEvent}");
                    }
                    catch (IotHubCommunicationException iotHubCommunicationException)
                    {
                        Logger.Log($"{deviceId}: iothub problem, waiting {deviceRunnerConfiguration.DelayAfterIotHubCommunicationError}ms. {iotHubCommunicationException.Message}");
                        await Task.Delay(deviceRunnerConfiguration.DelayAfterIotHubCommunicationError);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"{deviceId}: failed to send event. {ex.Message}");
                    }  
                }


                if (this.deviceRunnerConfiguration.CloudToDeviceMessage)
                {
                    try
                    {
                        // Check Cloud-to-Device Message
                        Logger.Log($"{deviceId}: checking c2d message");
                        stopwatch.Restart();
                        Message c2dMsg = await deviceClient.ReceiveAsync(TimeSpan.FromMilliseconds(500));
                        stopwatch.Stop();
                        
                        if (maxMsC2D < stopwatch.ElapsedMilliseconds)
                            maxMsC2D = stopwatch.ElapsedMilliseconds;
                        
                        if (stopwatch.ElapsedMilliseconds > 4000)
                            numSlowC2D++;

                        Logger.Log($"{deviceId}: done checking c2d message: {stopwatch.ElapsedMilliseconds}ms. Max: {maxMsC2D}ms. Delays: {numSlowC2D}");
                        
                        if (c2dMsg != null)
                        {
                            var c2dMsgBody = c2dMsg.GetBodyAsText();
                            Logger.Log($"{deviceId}: c2d message received '{c2dMsgBody}'");

                            if (deviceRunnerConfiguration.CheckPendingCloudToDeviceMessage)
                            {
                                stopwatch.Restart();
                                var pendingMessage = await deviceClient.ReceiveAsync(TimeSpan.FromMilliseconds(200));
                                stopwatch.Stop();
                                
                                if (maxMsC2D < stopwatch.ElapsedMilliseconds)
                                    maxMsC2D = stopwatch.ElapsedMilliseconds;
                                
                                if (stopwatch.ElapsedMilliseconds > 4000)
                                    numSlowC2D++;

                                Logger.Log($"{deviceId}: done checking pending c2d message: {stopwatch.ElapsedMilliseconds}ms. Max: {maxMsC2D}ms. Delays: {numSlowC2D}");                            

                                if (pendingMessage != null)
                                {
                                    var pendingMessageBody = pendingMessage.GetBodyAsText();
                                    Logger.Log($"{deviceId}: abandoning c2d message '{pendingMessageBody}'");
                                    await deviceClient.AbandonAsync(pendingMessage);
                                    Logger.Log($"{deviceId}: done abandoning c2d message '{pendingMessageBody}'");
                                }
                            }

                            if (deviceRunnerConfiguration.CompleteCloudToDeviceMessage)
                            {
                                Logger.Log($"{deviceId}: completing c2d message '{c2dMsgBody}'");
                                await deviceClient.CompleteAsync(c2dMsg);
                                Logger.Log($"{deviceId}: done completing c2d message '{c2dMsgBody}'");
                            }
                        }
                        else
                        {
                            Logger.Log($"{deviceId}: no c2d message received");
                        }                 
                    }         
                    catch (IotHubCommunicationException iotHubCommunicationException)
                    {
                        Logger.Log($"{deviceId}: iothub problem, waiting {deviceRunnerConfiguration.DelayAfterIotHubCommunicationError}ms. {iotHubCommunicationException.Message}");
                        await Task.Delay(deviceRunnerConfiguration.DelayAfterIotHubCommunicationError);
                    }           
                    catch (Exception ex)
                    {
                        Logger.Log($"{deviceId}: failed to handle cloud to device message. {ex.Message}");
                    }
                }

                await Task.Delay(this.deviceRunnerConfiguration.DeviceLoopDelay);
            }
        }

        private void OnConnectionStatusChanged(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            Logger.Log($"{deviceId}: status changed to {status} due to {reason}");
        }
    }
}