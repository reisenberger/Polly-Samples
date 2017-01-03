﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using PollyTestClient.Output;

namespace PollyTestClient.Samples.Async
{
    /// <summary>
    /// Imagine a microservice or web front end (the upstream caller) trying to call two endpoints on a downstream system.
    /// The 'good' endpoint responds quickly.  The 'faulting' endpoint faults, and responds slowly.
    /// Imagine the _caller_ has limited capacity (all single instances of services/webapps eventually hit some capacity limit).
    /// 
    /// This demo 00 has no bulkheads to protect the caller.  
    /// A random combination of calls to the 'good' and 'faulting' endpoint are made.
    /// 
    /// Observe: --
    /// Because no bulkheads isolate the separate streams of calls, 
    /// eventually all the caller's capacity is taken up waiting on the 'faulting' downstream calls.
    /// So the performance of 'good' calls is starved of resource, and starts suffering too.
    /// Watch the number of 'pending' calls to the good endpoint eventually start to climb,
    /// as the faulting calls saturate all resource in the caller.
    /// </summary>
    public static class BulkheadAsyncDemo00_NoBulkhead
    {

        // Track the number of 'good' and 'faulting' requests made, succeeded and failed.
        // At any time, requests pending = made - succeeded - failed.
        static int totalRequests = 0;
        static int goodRequestsMade = 0;
        static int goodRequestsSucceeded = 0;
        static int goodRequestsFailed = 0;
        static int faultingRequestsMade = 0;
        static int faultingRequestsSucceeded = 0;
        static int faultingRequestsFailed = 0;

        public static async Task ExecuteAsync(CancellationToken externalCancellationToken, IProgress<DemoProgress> progress)
        {
            if (externalCancellationToken == null) throw new ArgumentNullException(nameof(externalCancellationToken));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            progress.Report(ProgressWithMessage(typeof(BulkheadAsyncDemo00_NoBulkhead).Name));
            progress.Report(ProgressWithMessage("======"));
            progress.Report(ProgressWithMessage(String.Empty));

            // Let's imagine this caller has some theoretically limited capacity.
            const int callerParallelCapacity = 8; // (artificially low - but easier to follow, to illustrate principle)
            var limitedCapacityCaller = new LimitedConcurrencyLevelTaskScheduler(callerParallelCapacity);

            var client = new HttpClient();
            var rand = new Random();
            totalRequests = 0;

            IList<Task> tasks = new List<Task>();
            CancellationTokenSource internalCancellationTokenSource = new CancellationTokenSource();
            CancellationToken combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                externalCancellationToken, internalCancellationTokenSource.Token).Token;

            while (!Console.KeyAvailable && !externalCancellationToken.IsCancellationRequested)
            {
                totalRequests++;

                // Randomly make either 'good' or 'faulting' calls.
                if (rand.Next(0, 2) == 0)
                {
                    goodRequestsMade++;
                    // Call 'good' endpoint.
                    tasks.Add(Task.Factory.StartNew(async j =>
                    {

                        try
                        {
                            // Make a request and get a response, from the good endpoint
                            string msg = (await client.GetAsync(Configuration.WEB_API_ROOT + "/api/nonthrottledgood/" + j, combinedToken)).Content.ReadAsStringAsync().Result;
                            if (!combinedToken.IsCancellationRequested) progress.Report(ProgressWithMessage("Response : " + msg, Color.Green));

                            goodRequestsSucceeded++;
                        }
                        catch (Exception e)
                        {
                            if (!combinedToken.IsCancellationRequested) progress.Report(ProgressWithMessage("Request " + j + " eventually failed with: " + e.Message, Color.Red));

                            goodRequestsFailed++;
                        }
                    }, totalRequests, combinedToken, TaskCreationOptions.LongRunning, limitedCapacityCaller).Unwrap()
                    );

                }
                else
                {
                    faultingRequestsMade++;
                    // call 'faulting' endpoint.
                    tasks.Add(Task.Factory.StartNew(async j =>
                    {
                        try
                        {
                            // Make a request and get a response, from the faulting endpoint
                            string msg = (await client.GetAsync(Configuration.WEB_API_ROOT + "/api/nonthrottledfaulting/" + j, combinedToken)).Content.ReadAsStringAsync().Result;
                            if (!combinedToken.IsCancellationRequested) progress.Report(ProgressWithMessage("Response : " + msg, Color.Green));

                            faultingRequestsSucceeded++;
                        }
                        catch (Exception e)
                        {
                            if (!combinedToken.IsCancellationRequested) progress.Report(ProgressWithMessage("Request " + j + " eventually failed with: " + e.Message, Color.Red));

                            faultingRequestsFailed++;
                        }
                    }, totalRequests, combinedToken, TaskCreationOptions.LongRunning, limitedCapacityCaller).Unwrap()
                    );

                }

                progress.Report(ProgressWithMessage($"Total requests: requested {totalRequests:00}, ", Color.White)); progress.Report(ProgressWithMessage($"    Good endpoint: requested {goodRequestsMade:00}, ", Color.White));
                progress.Report(ProgressWithMessage($"Good endpoint:succeeded {goodRequestsSucceeded:00}, ", Color.Green));
                progress.Report(ProgressWithMessage($"Good endpoint:pending {goodRequestsMade - goodRequestsSucceeded - goodRequestsFailed:00}, ", Color.Yellow));
                progress.Report(ProgressWithMessage($"Good endpoint:failed {goodRequestsFailed:00}.", Color.Red));

                progress.Report(ProgressWithMessage(String.Empty));
                progress.Report(ProgressWithMessage($"Faulting endpoint: requested {faultingRequestsMade:00}, ", Color.White));
                progress.Report(ProgressWithMessage($"Faulting endpoint:succeeded {faultingRequestsSucceeded:00}, ", Color.Green));
                progress.Report(ProgressWithMessage($"Faulting endpoint:pending {faultingRequestsMade - faultingRequestsSucceeded - faultingRequestsFailed:00}, ", Color.Yellow));
                progress.Report(ProgressWithMessage($"Faulting endpoint:failed {faultingRequestsFailed:00}.", Color.Red));
                progress.Report(ProgressWithMessage(String.Empty));

                // Wait briefly
                await Task.Delay(TimeSpan.FromSeconds(0.1), externalCancellationToken);
            }   

            // Cancel any unstarted and running tasks.
            internalCancellationTokenSource.Cancel();
            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch
            {
                // Swallow any shutdown exceptions eg TaskCanceledException - we don't care - we are shutting down the demo.
            }
        }

        public static Statistic[] LatestStatistics => new[]
        {
            new Statistic("Total requests made", totalRequests, Color.White),
            new Statistic("Good endpoint: requested", goodRequestsMade, Color.White),
            new Statistic("Good endpoint: succeeded", goodRequestsSucceeded, Color.Green),
            new Statistic("Good endpoint: pending", goodRequestsMade-goodRequestsSucceeded-goodRequestsFailed, Color.Yellow),
            new Statistic("Good endpoint: failed", goodRequestsFailed, Color.Red),
            new Statistic("Faulting endpoint: requested", faultingRequestsMade, Color.White),
            new Statistic("Faulting endpoint: succeeded", faultingRequestsSucceeded, Color.Green),
            new Statistic("Faulting endpoint: pending", faultingRequestsMade-faultingRequestsSucceeded-faultingRequestsFailed, Color.Yellow),
            new Statistic("Faulting endpoint: failed", faultingRequestsFailed, Color.Red),
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
