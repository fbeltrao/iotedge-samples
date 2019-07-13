# Samples.IoTEdge.DeviceLab

Uses the C# IoT Hub Sdk to connect to IoT Edge sending messages, updating twin properties and receiving cloud to device message. The solution can also run as stand alone app, not running as an IoT Edge Module.

For quick interaction use the built-in docker images:
- SDK 1.18.1: fbeltrao/iotedgedevicelab:1.18.1
- SDK 1.20.3: fbeltrao/iotedgedevicelab:1.20.3

Running:

Set the following environment variables:

|Variable|Description|Default|
|-|-|-|
|UpdateTwin|Indicates if twin should be updated|true|
|CloudToDeviceMessage|Indicates if it should check for cloud to device messages|true
|CheckPendingCloudToDeviceMessage|Checks if a new check for cloud to device message should happen if the first succeeded. The second message will be abandoned|true
|CompleteCloudToDeviceMessage|Indicates if cloud to device messages should be completed|true
|DeviceLoopDelay|Defines the delay in ms for each device loop|5000
|DeviceKickoffDelay|Delay between each device loop creation|5000
|SendEvent|Indicates if device to cloud events should be sent|false
|QueuedAsyncReceived|Indicates if an the receive cloud uses a custom timeout implementation (due to problem with new SDK)|true
|Protocol|Defines protocol used (amqp and mqtt supported)|amqp
|AmqpMultiplex|Indicates if multiplex amqp should be used|true
|DeviceList|Comma separated list of devices. They all should have the same shared access key|""
|DeviceSharedAccessKey|Devices shared access key|""
|EdgeGateway|Indicates if the connection should done through and IoT Edge device|true
|DelayAfterIotHubCommunicationError|Delay in ms once a iot hub communication exception is caught|5000
|DeviceOperationTimeoutInMilliseconds|Device operation timeout in milliseconds|240000
|DeviceRetryPolicy|Enable/disable retry policy in device client|true