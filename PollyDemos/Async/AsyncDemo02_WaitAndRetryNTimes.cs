﻿using Polly;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PollyDemos.OutputHelpers;

namespace PollyDemos.Async
{
    /// <summary>
    /// Demonstrates the WaitAndRetry policy.
    /// Loops through a series of Http requests, keeping track of each requested
    /// item and reporting server failures when encountering exceptions.
    /// 
    /// Observations: We now have waits among the retries.
    /// In this case, still not enough wait - or not enough retries - for the underlying system to have recovered.
    /// So we still fail some calls.
    /// </summary>
    public class AsyncDemo02_WaitAndRetryNTimes : AsyncDemo
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

            progress.Report(ProgressWithMessage(typeof(AsyncDemo02_WaitAndRetryNTimes).Name));
            progress.Report(ProgressWithMessage("======"));
            progress.Report(ProgressWithMessage(String.Empty));

            // Define our policy:
            var policy = Policy.Handle<Exception>().WaitAndRetryAsync(
                retryCount: 3, // Retry 3 times
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200), // Wait 200ms between each try.
                onRetry: (exception, calculatedWaitDuration) => // Capture some info for logging!
            {
                // This is your new exception handler! 
                // Tell the user what they've won!
                progress.Report(ProgressWithMessage("Policy logging: " + exception.Message, Color.Yellow));
                retries++;

            });

            using (var client = new HttpClient())
            {
                totalRequests = 0;
                bool internalCancel = false;

                // Do the following until a key is pressed
                while (!internalCancel && !cancellationToken.IsCancellationRequested)
                {
                    totalRequests++;

                    try
                    {
                        // Retry the following call according to the policy - 3 times.
                        await policy.ExecuteAsync(async token =>
                        {
                            // This code is executed within the Policy 

                            // Make a request and get a response
                            string msg = await client.GetStringAsync(Configuration.WEB_API_ROOT + "/api/values/" + totalRequests);

                            // Display the response message on the console
                            progress.Report(ProgressWithMessage("Response : " + msg, Color.Green));
                            eventualSuccesses++;
                        }, cancellationToken);
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
            new Statistic("Requests which eventually succeeded", eventualSuccesses, Color.Green),
            new Statistic("Retries made to help achieve success", retries, Color.Yellow),
            new Statistic("Requests which eventually failed", eventualFailures, Color.Red),
        };

    }
}
