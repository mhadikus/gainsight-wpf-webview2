using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using GainsightPX.WPF;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace GainsightWpfApp
{
    /// <summary>
    /// Implementation of <see cref="GainsightPX.WPF.IRenderContainerProvider"/>
    /// </summary>
    internal class EngagementRenderContainerProvider : IRenderContainerProvider
    {
        private bool _browserInitialized;
        private bool _javascriptBindingRegistered;
        private string _hostName;
        private object _javascriptCallback;
        private string _webControlDataDirectoryPath;

        private string _htmlContent = null;
        private string _htmlPath = null;

        public WebView2WrapperControl EngagementControl { get; private set; }

        public WebView2 Browser => EngagementControl?.WebView2Browser;

        private uint? BrowserProcessId => Browser?.CoreWebView2?.BrowserProcessId;

        private string WebControlDataDirectoryPath => _webControlDataDirectoryPath
            ?? (_webControlDataDirectoryPath = CreateWebControlDataDirectoryPath());

        /// <inheritdoc cref="IRenderContainerProvider.OnPageLoaded" />
        public event PageLoaded OnPageLoaded;

        public EngagementRenderContainerProvider()
        {
        }

        /// <inheritdoc cref="IRenderContainerProvider.GetWebControl" />
        public Visual GetWebControl()
        {
            // TODO: If there is any active engagement, we should either dismiss it before showing a new one
            // TODO: or ignore the new incoming engagement

            EngagementControl = new WebView2WrapperControl();

            Browser.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            Browser.CoreWebView2InitializationCompleted += OnCoreWebView2InitializationCompleted;
            Browser.NavigationCompleted += OnNavigationCompleted;
            Browser.Loaded += OnBrowserLoaded;
            Browser.Unloaded += OnBrowserUnloaded;

            return EngagementControl;
        }

        /// <inheritdoc cref="IRenderContainerProvider.RegisterJavaScriptBinding" />
        public void RegisterJavaScriptBinding(string callbackName, object javascriptCallback)
        {
            if (_javascriptBindingRegistered)
            {
                return;
            }

            _hostName = callbackName;
            _javascriptCallback = javascriptCallback;

            if (_browserInitialized)
            {
                Browser.CoreWebView2.AddHostObjectToScript(
                    _hostName,
                    new EngagementJavaScriptHost(_javascriptCallback, EngagementControl));

                _javascriptBindingRegistered = true;
            }
        }

        /// <inheritdoc cref="IRenderContainerProvider.ExecuteJavaScript" />
        public void ExecuteJavaScript(string surveyScript)
        {
            Browser?.CoreWebView2?.ExecuteScriptAsync(surveyScript);
        }

        /// <inheritdoc cref="IRenderContainerProvider.LoadHtmlContent" />
        public void LoadHtmlContent(string htmlContent, string path)
        {
            _htmlContent = htmlContent;
            _htmlPath = path;
            LoadHtml();
        }

        /// <summary>
        /// Create an environment to set the data folder for the <see cref="WebView2"/> control,
        /// otherwise the control will try to create its data folder in the <see cref="EngagementServer"/> EXE folder
        /// and throw <see cref="UnauthorizedAccessException"/> because it has no access to "C:\Program Files\National Instruments"
        /// </summary>
        /// <returns><see cref="CoreWebView2Environment"/></returns>
        /// <remarks>
        /// See https://github.com/MicrosoftEdge/WebView2Feedback/issues/904
        /// </remarks>
        private Task<CoreWebView2Environment> CreateWebViewEnvironmentAsync()
        {
            return CoreWebView2Environment.CreateAsync(userDataFolder: WebControlDataDirectoryPath);
        }

        private void LoadHtml()
        {
            if (_htmlContent != null && _htmlPath != null && Browser.CoreWebView2 != null && _browserInitialized)
            {
                _htmlContent = ConvertResultsToString(_hostName, _htmlContent);

                // Trying to achive: https://stackoverflow.com/a/27684558/11687890
                // Without that, we won't have permissions for cookies and it will fail
                // Based on: https://github.com/MicrosoftEdge/WebView2Feedback/issues/219
                // Using: https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/webresourcerequested?tabs=dotnet
                Browser.CoreWebView2.AddWebResourceRequestedFilter(_htmlPath, CoreWebView2WebResourceContext.All);
                Browser.CoreWebView2.WebResourceRequested += OnCoreWebView2WebResourceRequested;
                Browser.CoreWebView2.Navigate(_htmlPath);
            }
        }

        private void OnCoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs args)
        {
            _browserInitialized = true;
            DisableBrowserSettings();
            LoadHtml();
            RegisterJavaScriptBinding(_hostName, _javascriptCallback);

            // TODO: Uncomment to show the MS Edge DevTools
            // Browser?.CoreWebView2?.OpenDevToolsWindow();
        }

        private void DisableBrowserSettings()
        {
            var settings = Browser?.CoreWebView2?.Settings;
            if (settings != null)
            {
                // Disable keyboard shortcuts
                settings.AreBrowserAcceleratorKeysEnabled = false;

                // Disable context menu
                settings.AreDefaultContextMenusEnabled = false;

                // Disable zoom
                settings.IsZoomControlEnabled = false;
                settings.IsPinchZoomEnabled = false;
            }
        }

        private void OnCoreWebView2WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            if (sender is CoreWebView2 coreWeb)
            {
                var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_htmlContent ?? string.Empty));
                var response = coreWeb.Environment.CreateWebResourceResponse(stream, 200, "OK", "Content-Type: text/html; charset=utf-8");
                args.Response = response;
            }

            Browser.CoreWebView2.RemoveWebResourceRequestedFilter(_htmlPath, CoreWebView2WebResourceContext.All);
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                // Set the host object as part of the window
                if (!string.IsNullOrEmpty(_hostName))
                {
                    ExecuteJavaScript($"window.{_hostName} = chrome.webview.hostObjects.{_hostName};");
                }

                OnPageLoaded?.Invoke(this);
            }
        }

        private void OnBrowserLoaded(object sender, RoutedEventArgs e)
        {
            //// TODO: Create a custom environment to specify the Data directory for the browser
            ////var environment = await CreateWebViewEnvironmentAsync();
            ////Browser?.EnsureCoreWebView2Async(environment);

            Browser.EnsureCoreWebView2Async();
        }

        private void OnBrowserUnloaded(object sender, RoutedEventArgs e)
        {
            var deleteTask = DeleteWebControlDataDirectoryAsync(BrowserProcessId, _webControlDataDirectoryPath);

            _webControlDataDirectoryPath = null;
        }

        private static string CreateWebControlDataDirectoryPath()
        {
            var id = Guid.NewGuid().ToString();
            return Path.Combine(Path.GetTempPath(), $"{nameof(EngagementControl)}-{id}");
        }

        private static async Task DeleteWebControlDataDirectoryAsync(uint? browserProcessId, string dataDirectory)
        {
            if (string.IsNullOrEmpty(dataDirectory) || !Directory.Exists(dataDirectory))
            {
                return;
            }

            try
            {
                // Wait until the browser process exits before deleting its data directory
                var pid = Convert.ToInt32(browserProcessId, CultureInfo.InvariantCulture);
                var processWatcher = new ProcessWatcher(pid);
                await processWatcher.WaitForExitAsync();
                Directory.Delete(dataDirectory, recursive: true);
            }
            catch (Exception)
            {
            }
        }

        private static string ConvertResultsToString(string hostName, string htmlContent)
        {
            // Make sure to JSON.stringify(obj) the response object on the JS side
            // If not, the return type will be of System._com type and we can't process it
            if (hostName != null && htmlContent.Contains(hostName))
            {
                var split = htmlContent.Split(';');
                for (int i = 0; i < split.Length; i++)
                {
                    // Make sure that if the object was stringified, not to do that again
                    if (split[i].Contains(hostName) && !split[i].Contains("stringify"))
                    {
                        split[i] = split[i].Replace("result", "JSON.stringify(result)");
                    }
                }

                htmlContent = string.Join(";", split);
            }

            return htmlContent;
        }

        private class ProcessWatcher
        {
            public ProcessWatcher(int pid)
            {
                ProcessId = pid;
            }

            public int ProcessId { get; }

            public bool IsProcessRunning => Process.GetProcesses().Any(p => p.Id == ProcessId);

            public async Task WaitForExitAsync()
            {
                try
                {
                    var process = Process.GetProcessById(ProcessId);
                    if (process != null)
                    {
                        var retries = 10;
                        for (var i = 0; i < retries && !process.HasExited; i++)
                        {
                            await Task.Delay(500);
                        }
                    }
                }
                catch (Exception)
                {
                    // Process is not running
                }
            }
        }
    }
}
