using System.Text;
using Microsoft.Azure.Devices.Client;

namespace Samples.IoTEdge.DeviceLab
{

    public static class Extensions
    {
        public static string GetBodyAsText(this Message message)
        {
            if (message == null)
                return string.Empty;

            return Encoding.UTF8.GetString(message.GetBytes());
        }
    }
}