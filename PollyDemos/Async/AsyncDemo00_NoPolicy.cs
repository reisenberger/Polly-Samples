using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PollyDemos.OutputHelpers;

namespace PollyDemos.Async
{
    /// <summary>
    /// Uses no policy.  Demonstrates behaviour of 'faulting server' we are testing against.
    /// Loops through a series of Http requests, keeping track of each requested
    /// item and reporting server failures when encountering exceptions.
    /// </summary>
    public class AsyncDemo00_NoPolicy : AsyncDemo
    {
        private static int totalRequests;
        private static int eventualSuccesses;
        private static int retries;
        private static int eventualFailures;

        public override async Task ExecuteAsync(CancellationToken cancellationToken, IProgress<DemoProgress> progress)
        {
            if (cancellationToken == null) throw new ArgumentNullException(nameof(cancellationToken));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            eventualSuccesses = 0;
            retries = 0;
            eventualFailures = 0;

            progress.Report(ProgressWithMessage(typeof(AsyncDemo00_NoPolicy).Name));
            progress.Report(ProgressWithMessage("======"));
            progress.Report(ProgressWithMessage(String.Empty));

            using (var client = new HttpClient())
            {
                bool internalCancel = false;
                totalRequests = 0;
                // Do the following until a key is pressed
                while (!internalCancel && !cancellationToken.IsCancellationRequested)
                {
                    totalRequests++;

                    try
                    {
                        // Make a request and get a response
                        string msg = await client.GetStringAsync(Configuration.WEB_API_ROOT + "/api/values/" + totalRequests);

                        // Display the response message on the console
                        progress.Report(ProgressWithMessage("Response : " + msg, Color.Green));
                        eventualSuccesses++;
                    }
                    catch (Exception e)
                    {
                        progress.Report(ProgressWithMessage("Request " + totalRequests + " eventually failed with: " + e.Message, Color.Red));
                        eventualFailures++;
                    }

                    // Wait half second
                    await Task.Delay(TimeSpan.FromSeconds(0.5), cancellationToken);

                    internalCancel = TerminateDemosByKeyPress && Console.KeyAvailable;
                }
            }
        }

        public override Statistic[] LatestStatistics => new[]
        {
            new Statistic("Total requests made", totalRequests),
            new Statistic("Requests which eventually succeeded", eventualSuccesses),
            new Statistic("Retries made to help achieve success", retries),
            new Statistic("Requests which eventually failed", eventualFailures),
        };
        
    }
}
