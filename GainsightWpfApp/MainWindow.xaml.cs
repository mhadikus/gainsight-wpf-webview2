using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using GainsightPX;
using GainsightPX.Model;

namespace GainsightWpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartEngagement()
        {
            Logger.Handlers += (level, message, args) =>
            {
                Debug.WriteLine("Gainsight: " + message);
            };

            var config = new Config.Builder()
            {
                Enabled = true,
                DurableQueueDirectoryName = Path.GetTempPath() + "gainsight_cache",
                QueueLimit = 1000,
                SessionTimeout = new TimeSpan(0, 30, 0),
                EngagementsEnabled = true,
                PXVisitorId = GainsightConstants.UserId,
                LogLevel = Logger.Level.DEBUG,

            }.Build();

            Gainsight.Initialize(GainsightConstants.ProductKey, config, FailureHandler);

            AttachStatusEventHandler();

            Gainsight.Client.Identify(new User(GainsightConstants.UserId)
            {
                CustomAttributes = new Dictionary<string, object>()
                {
                    { "Internal", "Internal" }
                }
            });

            var provider = new EngagementRenderContainerProvider();
            GainsightPX.WPF.EngagementHandler.AttachEngagementsHandler(Gainsight.Client, provider);
            
            // TODO: Uncomment to unset reference control to fix an issue with the browser window not moving with the main window
            Gainsight.Client.SetEngagementReference(this.ReferenceControl);

            Gainsight.Client.Track(
                "TEST_KEY",
                new Dictionary<string, object>()
                {
                    { "WPF", true },
                    { ".NET", 6.0 }
                });
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // TODO: Make sure to close any active engagement

            Gainsight.Client?.Flush();
            Gainsight.Dispose();
        }

        private static void AttachStatusEventHandler()
        {
            Gainsight.Client.Succeeded += (action) =>
            {
                Debug.WriteLine($"Gainsight: Success Event: {action?.Type} - {action?.MessageId}");
            };
            Gainsight.Client.Failed += (action, error) =>
            {
                Debug.WriteLine($"Gainsight: Failure Event: {action?.Type} - {action?.MessageId}: {error?.Message}");
            };
        }

        private static void FailureHandler(BaseAction action, Exception e)
        {
            Debug.WriteLine("Gainsight: " + e.Message);
        }

        private void EngagementButton_Click(object sender, RoutedEventArgs e)
        {
            StartEngagement();
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateAsync("https://dotnet.microsoft.com");
        }

        private async void NavigateAsync(string url)
        {
            await TestWebView2Control.WebView2Browser.EnsureCoreWebView2Async();
            TestWebView2Control.WebView2Browser.CoreWebView2.Navigate(url);
        }
    }
}
