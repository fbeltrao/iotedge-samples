using System;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;

namespace Samples.IoTEdge.DeviceLab
{
    public interface IDeviceClient
    {
        Task OpenAsync();
        Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties);
        Task<Message> ReceiveAsync(TimeSpan timeSpan);
        Task CompleteAsync(Message c2dMsg);        
        Task SendEventAsync(Message message);
        Task AbandonAsync(Message result);
        void SetConnectionStatusChangesHandler(ConnectionStatusChangesHandler statusChangesHandler);
    }
}