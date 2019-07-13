using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;

namespace Samples.IoTEdge.DeviceLab
{
    public class QueuedReceiverDeviceClient : IDeviceClient
    {
        private readonly string deviceId;
        private readonly IDeviceClient deviceClient;
        private SemaphoreSlim receiveAsyncLock;
        private TaskCompletionSource<Message> receiveAsyncTaskCompletionSource;
        private int receiveAsyncTaskWaitCount;

        public QueuedReceiverDeviceClient(string deviceId, IDeviceClient deviceClient)
        {
            this.deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
            this.deviceClient = deviceClient ?? throw new System.ArgumentNullException(nameof(deviceClient));
            this.receiveAsyncLock = new SemaphoreSlim(1, 1);
        }

        public Task CompleteAsync(Message c2dMsg) => this.deviceClient.CompleteAsync(c2dMsg);
        public Task AbandonAsync(Message c2dMsg) => this.deviceClient.AbandonAsync(c2dMsg);

        public Task OpenAsync() => this.deviceClient.OpenAsync();

        public async Task<Message> ReceiveAsync(TimeSpan timeout)
        {
            var isUsingPendingRequest = true;

            TaskCompletionSource<Message> localPendingReceiveAsync = null;

            await this.receiveAsyncLock.WaitAsync();

            try
            {
                if ((localPendingReceiveAsync = this.receiveAsyncTaskCompletionSource) == null)
                {
                    localPendingReceiveAsync = this.SetupNewReceiveAsyncTaskCompletionSource(timeout);
                    isUsingPendingRequest = false;
                }
            }
            finally
            {
                this.receiveAsyncTaskWaitCount++;
                this.receiveAsyncLock.Release();
            }

            if (isUsingPendingRequest)
            {
                Logger.Log($"{deviceId}: checking cloud to device message for {timeout}, reusing pending request");
            }

            using (var cts = new CancellationTokenSource())
            {
                var timer = Task.Delay(timeout, cts.Token);
                var winner = await Task.WhenAny(localPendingReceiveAsync.Task, timer);
                if (winner == localPendingReceiveAsync.Task)
                {
                    // Cancel the timer tasks so that it does not fire
                    cts.Cancel();

                    Task<Message> singleFinished;
                    await this.receiveAsyncLock.WaitAsync();
                    try
                    {
                        if (localPendingReceiveAsync == this.receiveAsyncTaskCompletionSource)
                        {
                            // Verbose log as long as this is a new feature
                            Logger.Log($"{deviceId}: task ReceiveAsync returned before timeout");
                            singleFinished = this.receiveAsyncTaskCompletionSource.Task;

                            this.receiveAsyncTaskCompletionSource = null;
                            this.receiveAsyncTaskWaitCount = 0;
                        }
                        else
                        {
                            singleFinished = null;
                        }
                    }
                    finally
                    {
                        this.receiveAsyncLock.Release();
                    }

                    // Finished can be null if two race for the value of pendingReceiveAsync
                    // In that case the winner will handle the message
                    if (singleFinished != null && singleFinished.IsFaulted)
                    {
                        Logger.Log($"{deviceId}: error in task checking cloud to device message: {singleFinished.Exception?.Message}");
                        return null;
                    }

                    return singleFinished?.Result;
                }
                else
                {
                    // Verbose log as long as this is a new feature
                    Logger.Log($"{deviceId}: task ReceiveAsync returned by timeout");

                    // Task.Delay won the race, we are not awaiting for ReceiveAsync anymore
                    await this.receiveAsyncLock.WaitAsync();
                    try
                    {
                        if (localPendingReceiveAsync == this.receiveAsyncTaskCompletionSource)
                        {
                            --this.receiveAsyncTaskWaitCount;
                        }
                    }
                    finally
                    {
                        this.receiveAsyncLock.Release();
                    }
                }
            }

            return null;
        }

        
        /// <summary>
        /// Setups a new <see cref="receiveAsyncTaskCompletionSource"/>. Must be called while the lock is owned
        /// </summary>
        private TaskCompletionSource<Message> SetupNewReceiveAsyncTaskCompletionSource(TimeSpan timeout)
        {
            var localPendingReceiveAsync = this.receiveAsyncTaskCompletionSource = new TaskCompletionSource<Message>();
            this.receiveAsyncTaskWaitCount = 0;

            // Verbose log as long as this is a new feature
            Logger.Log($"{deviceId}: starting new ReceiveAsync task");

            _ = this.deviceClient.ReceiveAsync(timeout).ContinueWith(async (t) =>
            {
                var hasReceivers = false;
                // if no one cares abandon message
                await this.receiveAsyncLock.WaitAsync();
                try
                {
                    hasReceivers = this.receiveAsyncTaskWaitCount > 0;

                    if (!hasReceivers && localPendingReceiveAsync == this.receiveAsyncTaskCompletionSource)
                    {
                        this.receiveAsyncTaskCompletionSource = null;
                        this.receiveAsyncTaskWaitCount = 0;
                    }
                }
                finally
                {
                    this.receiveAsyncLock.Release();
                }

                // Verbose log as long as this is a new feature
                Logger.Log($"{deviceId}: finished ReceiveAsync task (hasReceivers={hasReceivers})");

                if (hasReceivers)
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        localPendingReceiveAsync.SetResult(t.Result);
                    }
                    else
                    {
                        localPendingReceiveAsync.SetException(t.Exception);
                    }
                }
                else if (t.IsCompletedSuccessfully && t.Result != null)
                {
                    // Verbose log as long as this is a new feature
                    Logger.Log($"{deviceId}: task ReceiveAsync found message '{t.Result.GetBodyAsText()}' but not one is awaiting, abandoning");

                    try
                    {
                        await this.deviceClient.AbandonAsync(t.Result);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"{deviceId}: failed to abandon message '{t.Result.GetBodyAsText()}' from task without listener: {ex.Message}");
                    }
                }
            });

            return localPendingReceiveAsync;
        }

        public Task SendEventAsync(Message message) => this.deviceClient.SendEventAsync(message);

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) => this.deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        
        public void SetConnectionStatusChangesHandler(ConnectionStatusChangesHandler statusChangesHandler) => this.deviceClient.SetConnectionStatusChangesHandler(statusChangesHandler);

    }
}