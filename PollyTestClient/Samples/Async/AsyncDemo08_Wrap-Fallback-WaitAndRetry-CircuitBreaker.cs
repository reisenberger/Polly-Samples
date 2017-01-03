using Polly;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Wrap;
using PollyTestClient.Output;

namespace PollyTestClient.Samples.Async
{
    /// <summary>
    /// Demonstrates a PolicyWrap including two Fallback policies (for different exceptions), WaitAndRetry and CircuitBreaker.
    /// As Demo07 - but now uses Fallback policies to provide substitute values, when the call still fails overall.
    ///  
    /// Loops through a series of Http requests, keeping track of each requested
    /// item and reporting server failures when encountering exceptions.
    /// 
    /// Obervations from this demo:
    /// - operation identical to Demo06 and Demo07  
    /// - except fallback policies provide nice substitute messages, if still fails overall
    /// - onFallback delegate captures the stats that were captured in try/catches in demos 06 and 07
    /// - also demonstrates how you can use the same kind of policy (Fallback in this case) twice (or more) in a wrap.
    /// </summary>
    public static class AsyncDemo08_Wrap_Fallback_WaitAndRetry_CircuitBreaker
    {
        private static int totalRequests;
        private static int eventualSuccesses;
        private static int retries;
        private static int eventualFailuresDueToCircuitBreaking;
        private static int eventualFailuresForOtherReasons;

        public static async Task ExecuteAsync(CancellationToken cancellationToken, IProgress<DemoProgress> progress)
        {
            if (cancellationToken == null) throw new ArgumentNullException(nameof(cancellationToken));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            // Let's call a web api service to make repeated requests to a server. 
            // The service is programmed to fail after 3 requests in 5 seconds.

            eventualSuccesses = 0;
            retries = 0;
            eventualFailuresDueToCircuitBreaking = 0;
            eventualFailuresForOtherReasons = 0;

            progress.Report(ProgressWithMessage(typeof(AsyncDemo08_Wrap_Fallback_WaitAndRetry_CircuitBreaker).Name));
            progress.Report(ProgressWithMessage("======"));
            progress.Report(ProgressWithMessage(String.Empty));

            var client = new HttpClient();
            Stopwatch watch = null;

            // Define our waitAndRetry policy: keep retrying with 200ms gaps.
            var waitAndRetryPolicy = Policy
                .Handle<Exception>(e => !(e is BrokenCircuitException)) // Exception filtering!  We don't retry if the inner circuit-breaker judges the underlying system is out of commission!
                .WaitAndRetryForeverAsync(
                attempt => TimeSpan.FromMilliseconds(200),
                (exception, calculatedWaitDuration) =>
                {
                    progress.Report(ProgressWithMessage(".Log,then retry: " + exception.Message, Color.Yellow));
                    retries++;
                });

            // Define our CircuitBreaker policy: Break if the action fails 4 times in a row.
            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
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

            // Define a fallback policy: provide a nice substitute message to the user, if we found the circuit was broken.
            FallbackPolicy<String> fallbackForCircuitBreaker = Policy<String>
                .Handle<BrokenCircuitException>()
                .FallbackAsync(
                    fallbackValue: /* Demonstrates fallback value syntax */ "Please try again later [Fallback for broken circuit]",
                    onFallbackAsync: async b =>
                    {
                        watch.Stop();
                        progress.Report(ProgressWithMessage("Fallback catches failed with: " + b.Exception.Message
                            + " (after " + watch.ElapsedMilliseconds + "ms)", Color.Red));
                        eventualFailuresDueToCircuitBreaking++;
                    }
                );

            // Define a fallback policy: provide a substitute string to the user, for any exception.
            FallbackPolicy<String> fallbackForAnyException = Policy<String>
                .Handle<Exception>()
                .FallbackAsync(
                    fallbackAction: /* Demonstrates fallback action/func syntax */ async ct =>
                    {
                        /* do something else async if desired */
                        return "Please try again later [Fallback for any exception]";
                    },
                    onFallbackAsync: async e =>
                    {
                        watch.Stop();
                        progress.Report(ProgressWithMessage("Fallback catches eventually failed with: " + e.Exception.Message
                            + " (after " + watch.ElapsedMilliseconds + "ms)", Color.Red));
                        eventualFailuresForOtherReasons++;
                    }
                );

            // As demo07: we combine the waitAndRetryPolicy and circuitBreakerPolicy into a PolicyWrap, using the *static* Policy.Wrap syntax.
            PolicyWrap myResilienceStrategy = Policy.WrapAsync(waitAndRetryPolicy, circuitBreakerPolicy);

            // Added in demo08: we wrap the two fallback policies onto the front of the existing wrap too.  Demonstrates the *instance* wrap syntax. And the fact that the PolicyWrap myResilienceStrategy from above is just another Policy, which can be onward-wrapped too.  
            // With this pattern, you can build an overall resilience strategy programmatically, reusing some common parts (eg PolicyWrap myResilienceStrategy) but varying other parts (eg Fallback) individually for different calls.
            PolicyWrap<String> policyWrap = fallbackForAnyException.WrapAsync(fallbackForCircuitBreaker.WrapAsync(myResilienceStrategy));
            // For info: Equivalent to: PolicyWrap<String> policyWrap = Policy.Wrap(fallbackForAnyException, fallbackForCircuitBreaker, waitAndRetryPolicy, circuitBreakerPolicy);

            totalRequests = 0;
            // Do the following until a key is pressed
            while (!Console.KeyAvailable && !cancellationToken.IsCancellationRequested)
            {
                totalRequests++;
                watch = new Stopwatch();
                watch.Start();

                try
                {
                    // Manage the call according to the whole policy wrap
                    string response = await policyWrap.ExecuteAsync(ct => client.GetStringAsync(Configuration.WEB_API_ROOT + "/api/values/" + totalRequests), cancellationToken);

                    watch.Stop();

                    // Display the response message on the console
                    progress.Report(ProgressWithMessage("Response : " + response + " (after " + watch.ElapsedMilliseconds + "ms)", Color.Green));

                    eventualSuccesses++;
                }
                catch (Exception e) // try-catch not needed, now that we have a Fallback.Handle<Exception>.  It's only been left in to *demonstrate* it should never get hit.
                {
                    throw new InvalidOperationException("Should never arrive here.  Use of fallbackForAnyException should have provided nice fallback value for any exceptions.", e);
                }

                // Wait half second
                await Task.Delay(TimeSpan.FromSeconds(0.5), cancellationToken);
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
