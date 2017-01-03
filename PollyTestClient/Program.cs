﻿using System;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Threading;
using System.Threading.Tasks;
using PollyTestClient.Output;
using PollyTestClient.Samples.Sync;
using PollyTestClient.Samples.Async;

namespace PollyTestClient
{
    class Program
    {
        private static readonly object lockObject = new object();

        static void Main(string[] args)
        {
            Statistic[] statistics = new Statistic[0];

            var progress = new Progress<DemoProgress>();
            progress.ProgressChanged += (sender, progressArgs) =>
            {
                lock (lockObject)
                {
                    WriteLineInColor(progressArgs.ColoredMessage.Message, progressArgs.ColoredMessage.Color.ToConsoleColor());
                    statistics = progressArgs.Statistics;
                }
            };

            CancellationTokenSource cancellationSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationSource.Token;

            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // Uncomment the samples you wish to run:
            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // Work through the demos in order, to discover features.
            // See <summary> at top of each demo class, for explanation.
            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // Synchronous demos
            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

            //Demo00_NoPolicy.Execute(cancellationToken, progress);
            //Demo01_RetryNTimes.Execute(cancellationToken, progress);
            //Demo02_WaitAndRetryNTimes.Execute(cancellationToken, progress);
            //Demo03_WaitAndRetryNTimes_WithEnoughRetries.Execute(cancellationToken, progress);
            //Demo04_WaitAndRetryForever.Execute(cancellationToken, progress);
            //Demo05_WaitAndRetryWithExponentialBackoff.Execute(cancellationToken, progress);
            //Demo06_WaitAndRetryNestingCircuitBreaker.Execute(cancellationToken, progress);
            //Demo07_WaitAndRetryNestingCircuitBreakerUsingPolicyWrap.Execute(cancellationToken, progress);
            //Demo08_Wrap_Fallback_WaitAndRetry_CircuitBreaker.Execute(cancellationToken, progress);
            //Demo09_Wrap_Fallback_Timeout_WaitAndRetry.Execute(cancellationToken, progress);

            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // Asynchronous demos
            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

            // These async demos use .Wait() (rather than await) with the async calls, only for the purposes of allowing the demos still to remain the primary execution thread and own the Console output.

            AsyncDemo00_NoPolicy.ExecuteAsync(cancellationToken, progress).Wait();
            //AsyncDemo01_RetryNTimes.ExecuteAsync(cancellationToken, progress).Wait();
            //AsyncDemo02_WaitAndRetryNTimes.ExecuteAsync(cancellationToken, progress).Wait();
            //AsyncDemo03_WaitAndRetryNTimes_WithEnoughRetries.ExecuteAsync(cancellationToken, progress).Wait();
            //AsyncDemo04_WaitAndRetryForever.ExecuteAsync(cancellationToken, progress).Wait();
            //AsyncDemo05_WaitAndRetryWithExponentialBackoff.ExecuteAsync(cancellationToken, progress).Wait();
            //AsyncDemo06_WaitAndRetryNestingCircuitBreaker.ExecuteAsync(cancellationToken, progress).Wait();
            //AsyncDemo07_WaitAndRetryNestingCircuitBreakerUsingPolicyWrap.ExecuteAsync(cancellationToken, progress).Wait();
            //AsyncDemo08_Wrap_Fallback_WaitAndRetry_CircuitBreaker.ExecuteAsync(cancellationToken, progress).Wait();
            //AsyncDemo09_Wrap_Fallback_Timeout_WaitAndRetry.ExecuteAsync(cancellationToken, progress).Wait();

            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // Bulkhead demos
            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

            //BulkheadAsyncDemo00_NoBulkhead.ExecuteAsync(cancellationToken, progress).Wait();
            //BulkheadAsyncDemo01_WithBulkheads.ExecuteAsync(cancellationToken, progress).Wait();


            // Keep the console open.
            Console.ReadKey();
            cancellationSource.Cancel();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();

            // Output statistics.
            int longestDescription = statistics.Max(s => s.Description.Length);
            foreach (Statistic stat in statistics)
            {
                WriteLineInColor(stat.Description.PadRight(longestDescription) + ": " + stat.Value, stat.Color.ToConsoleColor());
            }

            // Keep the console open.
            Console.ReadKey();
        }

        public static void WriteLineInColor(string msg, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
            Console.ResetColor();
        }

    }
}
