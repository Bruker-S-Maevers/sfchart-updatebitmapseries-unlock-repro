using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SfChartUpdateBitmapSeriesRepro;

public partial class App : Application
{
    public App()
    {
        // Optional: avoids the trial license dialog.
        var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
        if (!string.IsNullOrEmpty(licenseKey))
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);

        // Software rendering makes WriteableBitmap.Lock() wait for the render thread more often, so the issue
        // appears within seconds. With hardware rendering it also occurs, just less often.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
    }
}
