using System;
using System.Globalization;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using CommandLine;

namespace Samples.CloudToDeviceSender
{
    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<CloudToDeviceSenderOptions>(args)
                  .WithParsed<CloudToDeviceSenderOptions>(o => {
                      
                      var cts = new CancellationTokenSource();
                      Init(o, cts.Token);

                      // Wait until the app unloads or is cancelled            
                      AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
                      Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
                      WhenCancelled(cts.Token).Wait();
                  });
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

        static void Init(CloudToDeviceSenderOptions options, CancellationToken cts)
        {
            Task.Run(() => SendCloudToDeviceMessagesAsync(options, cts));
        }

        private static async Task SendCloudToDeviceMessagesAsync(CloudToDeviceSenderOptions options, CancellationToken cts)
        {
            Logger.Log($"Connecting to {options.DeviceId}");
            var serviceClient = ServiceClient.CreateFromConnectionString(options.ConnectionString);
            Logger.Log("connected");

            var msgIdentifier = options.MessageSeed;
            while (!cts.IsCancellationRequested)
            {
                var msgIdentifierText = msgIdentifier.ToString(CultureInfo.InvariantCulture);
                var msg = new Message(Encoding.UTF8.GetBytes(msgIdentifierText))
                {
                    MessageId = msgIdentifierText,
                };

                await serviceClient.SendAsync(options.DeviceId, msg);

                Logger.Log($"Sent message {msgIdentifierText} to device {options.DeviceId}");

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.TimeBetweenMessages)));

                ++msgIdentifier;
            }
        }
    }
}
