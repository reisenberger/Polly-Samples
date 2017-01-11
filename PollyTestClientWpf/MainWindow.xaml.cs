using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PollyDemos;
using PollyDemos.Async;
using PollyDemos.OutputHelpers;
using PollyDemos.Sync;
using Color = PollyDemos.OutputHelpers.Color;

namespace PollyTestClientWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        CancellationTokenSource cancellationSource;
        CancellationToken cancellationToken;

        readonly object lockObject = new object();

        Statistic[] statistics = new Statistic[0];
        private const int maxStatisticsToShow = 9;
        private const string StatisticBoxPrefix = "Statistic";
        private const string StatisticLabelPrefix = "StatisticLabel";

        private Progress<DemoProgress> progress;

        public MainWindow()
        {
            InitializeComponent();
            PlayButton.Click += (sender, args) => PlayButton_Click();
            StopButton.Click += (sender, args) => StopButton_Click();
            ClearButton.Click += (sender, args) => ClearButton_Click();

            progress = new Progress<DemoProgress>();
            progress.ProgressChanged += (sender, progressArgs) =>
            {
                lock (lockObject)
                {
                    WriteLineInColor(progressArgs.ColoredMessage.Message, progressArgs.ColoredMessage.Color);

                    statistics = progressArgs.Statistics;
                    UpdateStatistics(progressArgs.Statistics);
                }
            };

        }

        private void ClearButton_Click()
        {
            Output.Document = new FlowDocument();
            UpdateStatistics(new Statistic[0]);
        }

        private void PlayButton_Click()
        {
            StopButton.IsEnabled = true;
            PlayButton.IsEnabled = false;

            cancellationSource = new CancellationTokenSource();

            cancellationToken = cancellationSource.Token;

            ComboBoxItem selectedItem = Demo.SelectedItem as ComboBoxItem;
            if (selectedItem == null)
            {
                WriteLineInColor("No demo selected.", Color.Red);
                cancellationSource.Cancel();
                return;
            }

            Type demoType = Assembly.GetAssembly(typeof(DemoBase)).GetTypes().SingleOrDefault(t => t.Name == selectedItem.Name);
            if (demoType == null)
            {
                WriteLineInColor($"Unable to identify demo: {selectedItem.Name}", Color.Red);
                cancellationSource.Cancel();
            }
            else if (demoType.IsSubclassOf(typeof(SyncDemo)))
            {
                SyncDemo demoInstance;
                try
                {
                    demoInstance = Activator.CreateInstance(demoType) as SyncDemo;
                }
                catch (Exception)
                {
                    demoInstance = null;
                }
                if (demoInstance == null)
                {
                    WriteLineInColor($"Unable to instantiate demo: {selectedItem.Name}", Color.Red);
                    cancellationSource.Cancel();
                    return;
                }

                try
                {
                    Task.Run(() => demoInstance.Execute(cancellationToken, progress))
                        .ContinueWith(t =>
                        {
                            if (t.IsCanceled)
                            {
                                WriteLineInColor($"Demo was canceled: {selectedItem.Name}", Color.Red);
                            }
                            else if (t.IsFaulted)
                            {
                                WriteLineInColor($"Demo {selectedItem.Name} threw exception: {t.Exception.ToString()}", Color.Red);
                            }
                        }, TaskContinuationOptions.NotOnRanToCompletion);
                }
                catch (Exception e)
                {
                    WriteLineInColor($"Demo {selectedItem.Name} threw exception: {e}", Color.Red);
                }
            }
            else if (demoType.IsSubclassOf(typeof(AsyncDemo)))
            {
                AsyncDemo demoInstance;
                try
                {
                    demoInstance = Activator.CreateInstance(demoType) as AsyncDemo;
                }
                catch (Exception)
                {
                    demoInstance = null;
                }
                if (demoInstance == null)
                {
                    WriteLineInColor($"Unable to instantiate demo: {selectedItem.Name}", Color.Red);
                    cancellationSource.Cancel();
                    return;
                }

                demoInstance.ExecuteAsync(cancellationToken, progress)
                    .ContinueWith(t =>
                    {
                        if (t.IsCanceled)
                        {
                            WriteLineInColor($"Demo was canceled: {selectedItem.Name}", Color.Red);
                        }
                        else if (t.IsFaulted)
                        {
                            WriteLineInColor($"Demo {selectedItem.Name} threw exception: {t.Exception.ToString()}", Color.Red);
                        }
                    }, TaskContinuationOptions.NotOnRanToCompletion);
            }
            else
            {
                WriteLineInColor($"Unable to identify demo as either sync or async demo: {selectedItem.Name}", Color.Red);
                cancellationSource.Cancel();
            }
        }

        private void StopButton_Click()
        {
            StopButton.IsEnabled = false;
            PlayButton.IsEnabled = true;

            if (cancellationSource == null)
            {
                WriteLineInColor($"No demo currently running.", Color.Red);
                return;
            }

            try
            {
                cancellationSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
                WriteLineInColor($"Demo already stopped.", Color.Red);
                return;
            }

            // Output statistics.
            if (statistics.Any())
            {
                int longestDescription = statistics.Max(s => s.Description.Length);
                foreach (Statistic stat in statistics)
                {
                    WriteLineInColor(stat.Description.PadRight(longestDescription) + ": " + stat.Value, stat.Color);
                }
            }

            cancellationSource.Dispose();
        }

        public void WriteLineInColor(string msg, Color color)
        {
            TextRange newText = new TextRange(Output.Document.ContentEnd, Output.Document.ContentEnd)
            {
                Text = msg + "\n"
            };
            newText.ApplyPropertyValue(TextElement.ForegroundProperty, color.ToBrushColor());

            Output.ScrollToEnd();
        }

        private void UpdateStatistics(Statistic[] stats)
        {
            int statisticsToShow = stats.Length;
            for (int i = 0; i < maxStatisticsToShow; i++)
            {
                string statSuffix = $"{i:00}";
                Label label = (Label) this.FindName(StatisticLabelPrefix + statSuffix);
                TextBox statBox = (TextBox) this.FindName(StatisticBoxPrefix + statSuffix);

                if (i < statisticsToShow)
                {
                    Statistic statistic = stats[i];
                    label.Content = statistic.Description;
                    statBox.Foreground = statistic.Color.ToBrushColor();
                    statBox.Text = String.Format($"{statistic.Value:000}");
                    label.Visibility = Visibility.Visible;
                    statBox.Visibility = Visibility.Visible;
                }
                else
                {
                    label.Visibility = Visibility.Hidden;
                    statBox.Visibility = Visibility.Hidden;
                }
            }
        }
    }
}
