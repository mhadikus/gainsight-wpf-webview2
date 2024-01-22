# How to show Gainsight PX engagements using the WebView2 control in .NET applications

.NET version 6.0

Download Gainsight PX 1.3.6 (or later version) C# SDK from https://support.gainsight.com/PX/API_for_Developers/SDKs/Gainsight_PX_C_SDK#Engagements
- Copy `GainsightPX.dll` to `GainsightWpfApp\GainsightPX.1.3.6\GainsightPX\net6.0` directory
- Copy `GainsightPX.WPF` to `GainsightWpfApp\GainsightPX.1.3.6\GainsightPX.WPF\net6.0-windows` directory

Open `GainsightWpfApp.sln` with Visual Studio 2022
 - Set your product key and your user ID in `GainsightConstants.cs`
 - Build and run the application

Required packages
- Microsoft.WebWebView2 - https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.2210.55
- Newtonsoft.Json - https://www.nuget.org/packages/Newtonsoft.Json/13.0.1
