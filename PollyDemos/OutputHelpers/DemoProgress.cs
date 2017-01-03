namespace PollyDemos.OutputHelpers
{
    public struct DemoProgress
    {
        public Statistic[] Statistics { get; private set; }
        public ColoredMessage ColoredMessage { get; private set; }

        public DemoProgress(Statistic[] statistics, ColoredMessage message)
        {
            Statistics = statistics;
            ColoredMessage = message;
        }
    }
}
