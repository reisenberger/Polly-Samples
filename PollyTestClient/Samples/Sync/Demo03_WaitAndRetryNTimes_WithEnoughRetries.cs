
using Polly;
using System;
using System.Net;
using System.Reflection;
using System.Threading;
using PollyTestClient.Output;

namespace PollyTestClient.Samples.Sync
{
    /// <summary>
    /// Demonstrates the WaitAndRetry policy.
    /// Loops through a series of Http requests, keeping track of each requested
    /// item and reporting server failures when encountering exceptions.
    /// 
    /// Observations: We now have waits and enough retries: all calls now succeed!  Yay!
    /// But we kind-a had to guess how many retries would be enough before the server responded again ...
    /// (and we're hammering that server with retries)
    /// </summary>
    public static class Demo03_WaitAndRetryNTimes_WithEnoughRetries
    {
        private static int totalRequests;
        private static int eventualSuccesses;
        private static int retries;
        private static int eventualFailures;

        public static void Execute(CancellationToken cancellationToken, IProgress<DemoProgress> progress)
        {
            if (cancellationToken == null) throw new ArgumentNullException(nameof(cancellationToken));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            // Let's call a web api service to make repeated requests to a server. 
            // The service is programmed to fail after 3 requests in 5 seconds.

            eventualSuccesses = 0;
            retries = 0;
            eventualFailures = 0;

            progress.Report(ProgressWithMessage(typeof(Demo03_WaitAndRetryNTimes_WithEnoughRetries).Name));
            progress.Report(ProgressWithMessage("======"));
            progress.Report(ProgressWithMessage(String.Empty));

            // Let's call a web api service to make repeated requests to a server. 
            // The service is programmed to fail after 3 requests in 5 seconds.

            // Define our policy:
            var policy = Policy.Handle<Exception>().WaitAndRetry(
                retryCount: 20, // Retry up to 20 times! - should be enough that we eventually succeed.
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200), // Wait 200ms between each try.
                onRetry: (exception, calculatedWaitDuration) => // Capture some info for logging!
            {
                // This is your new exception handler! 
                // Tell the user what they've won!
                progress.Report(ProgressWithMessage("Policy logging: " + exception.Message, Color.Yellow));
                retries++;

            });

            var client = new WebClient();

            totalRequests = 0;

            // Do the following until a key is pressed
            while (!Console.KeyAvailable && !cancellationToken.IsCancellationRequested)
            {
                totalRequests++;

                try
                {
                    // Retry the following call according to the policy - 15 times.
                    policy.Execute(() =>
                    {
                        // This code is executed within the Policy 

                        // Make a request and get a response
                        var response = client.DownloadString(Configuration.WEB_API_ROOT + "/api/values/" + totalRequests.ToString());

                        // Display the response message on the console
                        progress.Report(ProgressWithMessage("Response : " + response, Color.Green));
                        eventualSuccesses++;
                    });
                }
                catch (Exception e)
                {
                    progress.Report(ProgressWithMessage("Request " + totalRequests + " eventually failed with: " + e.Message, Color.Red));
                    eventualFailures++;
                }

                // Wait half second before the next request.
                Thread.Sleep(500);
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
