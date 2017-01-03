using Polly;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PollyDemos.OutputHelpers;

namespace PollyDemos.Async
{
    /// <summary>
    /// Demonstrates the Retry policy coming into action.
    /// Loops through a series of Http requests, keeping track of each requested
    /// item and reporting server failures when encountering exceptions.
    /// 
    /// Observations: There's no wait among these retries.  Can be appropriate sometimes.  
    /// In this case, no wait hasn't given underlying system time to recover, so calls still fail despite retries.
    /// </summary>
    public class AsyncDemo01_RetryNTimes
    {
        private static int totalRequests;
        private static int eventualSuccesses;
        private static int retries;
        private static int eventualFailures;

        public async Task ExecuteAsync(CancellationToken cancellationToken, IProgress<DemoProgress> progress)
        {
            if (cancellationToken == null) throw new ArgumentNullException(nameof(cancellationToken));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            eventualSuccesses = 0;
            retries = 0;
            eventualFailures = 0;

            progress.Report(ProgressWithMessage(typeof(AsyncDemo01_RetryNTimes).Name));
            progress.Report(ProgressWithMessage("======"));
            progress.Report(ProgressWithMessage(String.Empty));

            // Define our policy:
            var policy = Policy.Handle<Exception>().RetryAsync(3, (exception, attempt) =>
            {
                // This is your new exception handler! 
                // Tell the user what they've won!
                progress.Report(ProgressWithMessage("Policy logging: " + exception.Message, Color.Yellow));
                retries++;

            });

            using (var client = new HttpClient())
            {
                totalRequests = 0;
                // Do the following until a key is pressed
                while (!Console.KeyAvailable && !cancellationToken.IsCancellationRequested)
                {
                    totalRequests++;

                    try
                    {
                        // Retry the following call according to the policy - 3 times.
                        await policy.ExecuteAsync(async () =>
                        {
                            // This code is executed within the Policy 

                            // Make a request and get a response
                            string msg = await client.GetStringAsync(Configuration.WEB_API_ROOT + "/api/values/" + totalRequests);

                            // Display the response message on the console
                            progress.Report(ProgressWithMessage("Response : " + msg, Color.Green));
                            eventualSuccesses++;
                        });
                    }
                    catch (Exception e)
                    {
                        progress.Report(ProgressWithMessage("Request " + totalRequests + " eventually failed with: " + e.Message, Color.Red));
                        eventualFailures++;
                    }

                    // Wait half second
                    await Task.Delay(TimeSpan.FromSeconds(0.5), cancellationToken);
                }
            }
        }

        public static Statistic[] LatestStatistics => new[]
        {
            new Statistic("Total requests made", totalRequests),
            new Statistic("Requests which eventually succeeded", eventualSuccesses),
            new Statistic("Retries made to help achieve success", retries),
            new Statistic("Requests which eventually failed", eventualFailures),
        };

        public static DemoProgress ProgressWithMessage(string message)
        {
            return new DemoProgress(LatestStatistics, new ColoredMessage(message, Color.Default));
        }

        public static DemoProgress ProgressWithMessage(string message, Color color)
        {
            return new DemoProgress(LatestStatistics, new ColoredMessage(message, color));
        }
    }
}
