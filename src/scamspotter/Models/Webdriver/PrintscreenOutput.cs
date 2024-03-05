using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScamSpotter.Models.Webdriver
{
    internal enum ScreenshotSizeTypes
    {
        ScreenPageSize,
        FullPageSize

    }
    internal class PrintscreenOutput
    {
        internal ScreenshotSizeTypes screenshotSize { get; set; } = ScreenshotSizeTypes.ScreenPageSize;
        internal byte[] Screenshot { get; set; }
        internal IWebDriver SourceDriverRef { get; set; }

    }
}
