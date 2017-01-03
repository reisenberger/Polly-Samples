﻿using Polly;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Wrap;
using PollyDemos.OutputHelpers;

namespace PollyDemos.Sync
{
    /// <summary>
    /// Demonstrates using the WaitAndRetry policy nesting CircuitBreaker.
    /// Same as Demo06 - but this time demonstrates combining the policies using PolicyWrap.
    /// 
    /// Loops through a series of Http requests, keeping track of each requested
    /// item and reporting server failures when encountering exceptions.
    /// 
    /// Obervations from this demo:
    /// The operation is identical to Demo06.  
    /// The code demonstrates how using the PolicyWrap makes your combined-Policy-strategy more concise, at the point of execution.
    /// </summary>
    public class Demo07_WaitAndRetryNestingCircuitBreakerUsingPolicyWrap
    {
        private static int totalRequests;
        private static int eventualSuccesses;
        private static int retries;
        private static int eventualFailuresDueToCircuitBreaking;
        private static int eventualFailuresForOtherReasons;

        public void Execute(CancellationToken cancellationToken, IProgress<DemoProgress> progress)
        {
            if (cancellationToken == null) throw new ArgumentNullException(nameof(cancellationToken));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            // Let's call a web api service to make repeated requests to a server. 
            // The service is programmed to fail after 3 requests in 5 seconds.

            eventualSuccesses = 0;
            retries = 0;
            eventualFailuresDueToCircuitBreaking = 0;
            eventualFailuresForOtherReasons = 0;

            progress.Report(ProgressWithMessage(typeof(Demo07_WaitAndRetryNestingCircuitBreakerUsingPolicyWrap).Name));
            progress.Report(ProgressWithMessage("======"));
            progress.Report(ProgressWithMessage(String.Empty));

            // Define our waitAndRetry policy: keep retrying with 200ms gaps.
            RetryPolicy waitAndRetryPolicy = Policy
                .Handle<Exception>(e => !(e is BrokenCircuitException)) // Exception filtering!  We don't retry if the inner circuit-breaker judges the underlying system is out of commission!
                .WaitAndRetryForever(
                attempt => TimeSpan.FromMilliseconds(200),
                (exception, calculatedWaitDuration) =>
                {
                    // This is your new exception handler! 
                    // Tell the user what they've won!
                    progress.Report(ProgressWithMessage(".Log,then retry: " + exception.Message, Color.Yellow));
                    retries++;
                });

            // Define our CircuitBreaker policy: Break if the action fails 4 times in a row.
            CircuitBreakerPolicy circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(
                    exceptionsAllowedBeforeBreaking: 4,
                    durationOfBreak: TimeSpan.FromSeconds(3),
                    onBreak: (ex, breakDelay) =>
                    {
                        progress.Report(ProgressWithMessage(".Breaker logging: Breaking the circuit for " + breakDelay.TotalMilliseconds + "ms!", Color.Magenta));
                        progress.Report(ProgressWithMessage("..due to: " + ex.Message, Color.Magenta));
                    },
                    onReset: () => progress.Report(ProgressWithMessage(".Breaker logging: Call ok! Closed the circuit again!", Color.Magenta)),
                    onHalfOpen: () => progress.Report(ProgressWithMessage(".Breaker logging: Half-open: Next call is a trial!", Color.Magenta))
                );

            // New for demo07: combine the waitAndRetryPolicy and circuitBreakerPolicy into a PolicyWrap.
            PolicyWrap policyWrap = Policy.Wrap(waitAndRetryPolicy, circuitBreakerPolicy);

            using (var client = new WebClient())
            {
                totalRequests = 0;
                // Do the following until a key is pressed
                while (!Console.KeyAvailable && !cancellationToken.IsCancellationRequested)
                {
                    totalRequests++;
                    Stopwatch watch = new Stopwatch();
                    watch.Start();

                    try
                    {
                        // Retry the following call according to the policy wrap
                        string response = policyWrap.Execute<String>(() =>
                        {
                            // This code is executed through both policies in the wrap: WaitAndRetry outer, then CircuitBreaker inner.  Demo 06 shows a broken-out version of what this is equivalent to.

                            return client.DownloadString(Configuration.WEB_API_ROOT + "/api/values/" + totalRequests);
                        });

                        // Without the extra comments in the anonymous method { } above, it could even be as concise as this:
                        // string msg = policyWrap.Execute(() => client.DownloadString(Configuration.WEB_API_ROOT + "/api/values/" + i));

                        watch.Stop();

                        // Display the response message on the console
                        progress.Report(ProgressWithMessage("Response : " + response
                                                            + " (after " + watch.ElapsedMilliseconds + "ms)", Color.Green));

                        eventualSuccesses++;
                    }
                    catch (BrokenCircuitException b)
                    {
                        watch.Stop();

                        progress.Report(ProgressWithMessage("Request " + totalRequests + " failed with: " + b.GetType().Name
                                                + " (after " + watch.ElapsedMilliseconds + "ms)", Color.Red));

                        eventualFailuresDueToCircuitBreaking++;
                    }
                    catch (Exception e)
                    {
                        watch.Stop();

                        progress.Report(ProgressWithMessage("Request " + totalRequests + " eventually failed with: " + e.Message
                                                + " (after " + watch.ElapsedMilliseconds + "ms)", Color.Red));

                        eventualFailuresForOtherReasons++;
                    }

                    // Wait half second
                    Thread.Sleep(500);
                }
            }
        }

        public static Statistic[] LatestStatistics => new[]
        {
            new Statistic("Total requests made", totalRequests),
            new Statistic("Requests which eventually succeeded", eventualSuccesses),
            new Statistic("Retries made to help achieve success", retries),
            new Statistic("Requests failed early by broken circuit", eventualFailuresDueToCircuitBreaking),
            new Statistic("Requests which failed after longer delay", eventualFailuresForOtherReasons),
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
