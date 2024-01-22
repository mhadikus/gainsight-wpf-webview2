using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace GainsightWpfApp
{
    /// <summary>
    /// JavaScript host for creating and closing <see cref="GainsightPX.Gainsight"/> engagement
    /// </summary>
    /// <remarks>
    /// Class has to be public and COM visible to expose to the JavaScript
    /// </remarks>
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class EngagementJavaScriptHost
    {
        /// <summary>
        /// Event name for when engagement is drawn
        /// </summary>
        public const string EngagementDrawnEventName = "EngagementDrawn";

        /// <summary>
        /// Event name for when engagement is resized
        /// </summary>
        public const string EngagementResizeEventName = "EngagementResize";

        /// <summary>
        /// Event name for when a link on engagement is clicked
        /// </summary>
        public const string EngagementLinkEventName = "EngagementLink";

        /// <summary>
        /// Event name for when engagement is closed
        /// </summary>
        public const string EngagementClosedEventName = "EngagementClosed";

        /// <summary>
        /// Default width of engagement UI
        /// </summary>
        public const int DefaultEngagementWidth = 680;

        /// <summary>
        /// Default height of engagement UI
        /// </summary>
        public const int DefaultEngagementHeight = 350;

        private readonly object engagementHandler;

        private readonly MethodInfo cefQueryMethod;

        private readonly WebView2WrapperControl engagementControl;

        public EngagementJavaScriptHost(object handler, WebView2WrapperControl control)
        {
            this.engagementHandler = handler;
            var objType = this.engagementHandler.GetType();
            cefQueryMethod = objType.GetMethod("cefQuery", new Type[] { typeof(string), typeof(object) });

            engagementControl = control;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element must begin with upper-case letter", Justification = "Has to match JavaScript callback name sent by Gainsight")]
        public void cefQuery(string message, object args = null)
        {
            IDictionary<string, object> jsonEventArgs = null;
            if (args is string jsonString)
            {
                try
                {
                    jsonEventArgs = JsonConvert.DeserializeObject<IDictionary<string, object>>(jsonString);
                }
                catch
                {
                    jsonEventArgs = null;
                }
            }

            switch (message)
            {
                // If an incoming engagement is a single-step engagement, Gainsight will send us a single Resize event
                // If the incoming engagement is a multi-step engagement,
                //   Gainsight will send a Drawn event for the first step, and the Resize events for next steps
                // For a Drawn or Resize event, make sure we have the form visible with the correct sizes
                case EngagementDrawnEventName:
                case EngagementResizeEventName:
                    HandleEngagementDrawnOrResize(message, jsonEventArgs);
                    break;
                case EngagementLinkEventName:
                case EngagementClosedEventName:
                    ////engagementControl.OnEngagementClosed();
                    Debug.WriteLine($"{nameof(GainsightPX.Gainsight)}: Closing engagement {nameof(cefQuery)} event: {message}");
                    break;
                default:
                    Debug.WriteLine($"{nameof(GainsightPX.Gainsight)}: Received {nameof(cefQuery)} event: {message}");
                    break;
            }

            if (IsMultiStepEngagement(message))
            {
                // This is a work-around suggested by Gainsight for Bug 2426291: Multi-Step Engagements do not resize correctly
                //  Add a tiny delay so Gainsight can compute the html content size after we resize our engagement control
                Debug.WriteLine($"{nameof(GainsightPX.Gainsight)}: Delay processing multi-step engagement");
                System.Threading.Tasks.Task.Delay(TimeSpan.FromTicks(1)).ContinueWith(
                    task => InvokeEngagementHandler(message, args, jsonEventArgs));
            }
            else
            {
                InvokeEngagementHandler(message, args, jsonEventArgs);
            }
        }

        private void InvokeEngagementHandler(string message, object args, IDictionary<string, object> jsonEventArgs)
        {
            Debug.WriteLine($"{nameof(GainsightPX.Gainsight)}: Invoke engagement handler for event: {message}");
            cefQueryMethod.Invoke(engagementHandler, new object[] { message, jsonEventArgs ?? args });
        }

        private bool IsMultiStepEngagement(string eventName)
        {
            // We are within a multi-step engagement if we get a resize event when the engagement control is already visible
            return eventName == EngagementResizeEventName && engagementControl.IsVisible;
        }

        private void HandleEngagementDrawnOrResize(string message, IDictionary<string, object> eventArgs)
        {
            try
            {
                // Get the actual engagement size
                var width = Convert.ToInt32(eventArgs["fixedWidth"]);
                var height = Convert.ToInt32(eventArgs["fixedHeight"]);
                Debug.WriteLine($"{nameof(GainsightPX.Gainsight)}: Received {nameof(cefQuery)} event: {message} fixedWidth: {width} fixedHeight: {height}");

                ////engagementControl.OnEngagementAvailable(width, height);

                // TODO: Refresh the Gainsight PX browser window.
                // TODO: This is a work-around to ensure engagement is displayed on the web control.
                if (engagementControl.Parent is System.Windows.Window browserWindow)
                {
                    browserWindow.Hide();
                    browserWindow.Show();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"{nameof(GainsightPX.Gainsight)}: ERROR {nameof(cefQuery)} event: {message} exception: {e}");

                // If we get an exception, try to display the engagement with the default size
                ////engagementControl.OnEngagementAvailable(
                ////    EngagementConstants.DefaultEngagementWidth,
                ////    EngagementConstants.DefaultEngagementHeight);
            }
        }
    }
}
