using System;

namespace Samples.IoTEdge.DeviceLab
{

    public static class Logger
    {
        public static void Log(string message)
        {
            Console.WriteLine(string.Concat(System.DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss:ff] "), message));
        }
    }
}