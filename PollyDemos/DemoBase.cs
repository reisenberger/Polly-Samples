using System;
using System.Configuration;
using PollyDemos.OutputHelpers;

namespace PollyDemos
{
    public abstract class DemoBase
    {
        private readonly bool terminateDemosByKeyPress = ( ConfigurationManager.AppSettings["TerminateDemosByKeyPress"] ?? String.Empty).Equals(Boolean.TrueString, StringComparison.InvariantCultureIgnoreCase);

        protected bool TerminateDemosByKeyPress => terminateDemosByKeyPress;

        public abstract Statistic[] LatestStatistics { get; }

        public DemoProgress ProgressWithMessage(string message)
        {
            return new DemoProgress(LatestStatistics, new ColoredMessage(message, Color.Default));
        }

        public DemoProgress ProgressWithMessage(string message, Color color)
        {
            return new DemoProgress(LatestStatistics, new ColoredMessage(message, color));
        }
    }
}
