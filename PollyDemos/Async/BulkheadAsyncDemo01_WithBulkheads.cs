using Polly;
using Polly.Bulkhead;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using PollyDemos.OutputHelpers;

namespace PollyDemos.Async
{
    /// <summary>
    /// Same scenario as previous demo:
    /// Imagine a microservice or web front end (the upstream caller) trying to call two endpoints on a downstream system.
    /// The 'good' endpoint responds quickly.  The 'faulting' endpoint faults, and responds slowly.
    /// Imagine the _caller_ has limited capacity (all single instances of services/webapps eventually hit some capacity limit).
    /// 
    /// Compared to bulkhead demo 00, this demo 01 isolates the calls 
    /// to the 'good' and 'faulting' endpoints in separate bulkheads.
    /// A random combination of calls to the 'good' and 'faulting' endpoint are made.
    /// 
    /// Observe --
    /// Because the separate 'good' and 'faulting' streams are isolated in separate bulkheads, 
    /// the 'faulting' calls stil back up (high pending number), but 
    /// 'good' calls (in a separate bulkhead) are *unaffected* (all succeed; none pending).
    /// 
    /// Bulkheads: making sure one fault doesn't sink the whole ship!
    /// </summary>
    public class BulkheadAsyncDemo01_WithBulkheads
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

        public async Task ExecuteAsync(CancellationToken externalCancellationToken, IProgress<DemoProgress> progress)
        {
            if (externalCancellationToken == null) throw new ArgumentNullException(nameof(externalCancellationToken));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            progress.Report(ProgressWithMessage(typeof(BulkheadAsyncDemo01_WithBulkheads).Name));
            progress.Report(ProgressWithMessage("======"));
            progress.Report(ProgressWithMessage(String.Empty));

            // Let's imagine this caller has some theoretically limited capacity.
            const int callerParallelCapacity = 8; // (artificially low - but easier to follow, to illustrate principle)
            var limitedCapacityCaller = new LimitedConcurrencyLevelTaskScheduler(callerParallelCapacity);

            BulkheadPolicy bulkheadForGoodCalls = Policy.BulkheadAsync(callerParallelCapacity/2, int.MaxValue); 
            BulkheadPolicy bulkheadForFaultingCalls = Policy.BulkheadAsync(callerParallelCapacity - callerParallelCapacity/2, int.MaxValue); // In this demo we let any number (int.MaxValue) of calls _queue for an execution slot in the bulkhead (simulating a system still _trying to accept/process as many of the calls as possible).  A subsequent demo will look at using no queue (and bulkhead rejections) to simulate automated horizontal scaling.

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
                    tasks.Add(Task.Factory.StartNew(j =>

                        // Call 'good' endpoint: through the bulkhead.
                        bulkheadForGoodCalls.ExecuteAsync(async () =>
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
                        }), totalRequests, combinedToken, TaskCreationOptions.LongRunning, limitedCapacityCaller).Unwrap()
                    );

                }
                else
                {
                    faultingRequestsMade++;
                    
                    tasks.Add(Task.Factory.StartNew(j =>

                        // call 'faulting' endpoint: through the bulkhead.
                        bulkheadForFaultingCalls.ExecuteAsync(async () =>
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
                        }), totalRequests, combinedToken, TaskCreationOptions.LongRunning, limitedCapacityCaller).Unwrap()
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
