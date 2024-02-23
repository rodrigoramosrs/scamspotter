using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using ScamSpotter.Utils;
using Spectre.Console;
using System.Text.RegularExpressions;
using WebDriverManager;
using WebDriverManager.DriverConfigs;
using WebDriverManager.DriverConfigs.Impl;

namespace ScamSpotter.Services
{
    public class ScreenshotService : IDisposable
    {
        public enum WebDriverTypes
        {
            EdgeDriver,
            ChromeDriver,
            FirefoxDriver
        }
        private readonly ILogger<ScreenshotService> _logger;
        private IWebDriver _webDriver;
        private DriverOptions _driverOptions;
        private DriverService _driverService;

        private DriverManager _manager;
        WebDriverTypes _webDriverType = WebDriverTypes.ChromeDriver;
        static SemaphoreSlim semaphore = new SemaphoreSlim(10); // Allow 3 threads to access the resource concurrently

        public ScreenshotService(ILogger<ScreenshotService> logger, WebDriverTypes webDriverType = WebDriverTypes.ChromeDriver)
        {
            _logger = logger;
            _manager = new DriverManager();


            _webDriverType = webDriverType;
            IDriverConfig driverConfig = new IDriverConfig[] { new EdgeConfig(), new ChromeConfig(), new FirefoxConfig() }[(int)_webDriverType];

            _manager.SetUpDriver(driverConfig);
            _driverOptions = BuildDriverOptions(_webDriverType);
            _driverService = BuildDriverService(_webDriverType);
            _webDriver = BuildDriver(_webDriverType, _driverOptions, _driverService);
        }

        private DriverService BuildDriverService(WebDriverTypes webDriverType)
        {
            var driverService = new DriverService[] {
                EdgeDriverService.CreateDefaultService(),
                ChromeDriverService.CreateDefaultService(),
                FirefoxDriverService.CreateDefaultService()
            }
            [(int)webDriverType];

            driverService.HideCommandPromptWindow = true;
            driverService.SuppressInitialDiagnosticInformation = true;
            return driverService;
        }

        private DriverOptions BuildDriverOptions(WebDriverTypes webDriverType)
        {
            DriverOptions driverOptions;

            string[] args = { "disable-infobars", "disable-logging", "--allow-insecure-localhost", "--incognito", "--disable-web-security",  "--disable-client-side-phishing-detection", "--disable-extensions",
                "--aggressive-cache-discard","--disable-cache","--disable-application-cache","--disable-offline-load-stale-cache", "--allow-running-insecure-content", "--disable-search-engine-choice-screen",
                "--disk-cache-size=0","--disable-background-networking","--disable-sync","--disable-translate","--hide-scrollbars", "--noerrdialogs", "--single-process",
                "--ignore-certificate-errors", "--ignore-ssl-errors", "--ignore-certificate-errors-spki-list", "--enable-automation",
                "--disable-logging", "--log-level=3", "--silent", "--no-sandbox", "--headless=new", "--disable-dev-shm-usage",
                "--disable-gpu", "--no-first-run","--no-zygote", "--window-size=1024,768"};
            switch (webDriverType)
            {
                case WebDriverTypes.ChromeDriver:
                    driverOptions = new ChromeOptions() { LeaveBrowserRunning = false };
                    (driverOptions as ChromeOptions).AddArguments(args);
                    break;
                case WebDriverTypes.FirefoxDriver:
                    driverOptions = new FirefoxOptions();
                    FirefoxProfile profile = new FirefoxProfile();
                    (driverOptions as FirefoxOptions).AddArguments(args);
                    break;
                case WebDriverTypes.EdgeDriver:
                    driverOptions = new EdgeOptions() { LeaveBrowserRunning = false };
                    (driverOptions as EdgeOptions).AddArguments(args);
                    break;

                default:
                    throw new NotSupportedException();
            }

            driverOptions.AcceptInsecureCertificates = true;
            driverOptions.SetLoggingPreference("driver", OpenQA.Selenium.LogLevel.Off);
            driverOptions.SetLoggingPreference("server", OpenQA.Selenium.LogLevel.Off);
            driverOptions.SetLoggingPreference("browser", OpenQA.Selenium.LogLevel.Off);

            return driverOptions;
        }

        private WebDriver BuildDriver(WebDriverTypes webDriverType, DriverOptions driverOptions, DriverService driverService)
        {
            WebDriver webDriver;

            switch (webDriverType)
            {
                case WebDriverTypes.EdgeDriver:
                    webDriver = new EdgeDriver(driverService as EdgeDriverService, driverOptions as EdgeOptions);
                    break;
                case WebDriverTypes.ChromeDriver:
                    webDriver = new ChromeDriver(driverService as ChromeDriverService, driverOptions as ChromeOptions);
                    break;
                case WebDriverTypes.FirefoxDriver:
                    webDriver = new FirefoxDriver(driverService as FirefoxDriverService, driverOptions as FirefoxOptions);
                    break;
                default:
                    throw new NotSupportedException();
            }
            webDriver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(Global.GlobalSettings.RequestTimeoutSeconds);

            return webDriver;
        }

        public async Task<byte[]> OpenNewWindowAndTakeScreenshot(string url, bool AutoSave, string OutputFilename)
        {
            semaphore.Wait();
            byte[] output;
            try
            {
                using (var driver = BuildDriver(_webDriverType, _driverOptions, BuildDriverService(_webDriverType)))
                {
                    output = await Task.FromResult(InternalTakeScreenshot(url, AutoSave, OutputFilename, driver));
                    driver.Close();
                    //_webDriver.Close();
                }
            }
            finally { semaphore.Release(); }



            return output;

        }

        public async Task<byte[]?> TakeScreenshot(string url, bool AutoSave = false, string OutputFilenam = "")
        {
            var result = new byte[0];

            try
            {
                result = InternalTakeScreenshot(url, AutoSave, OutputFilenam, _webDriver);
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
                _logger.LogError(ex, "Exception on ScrenshotService.TakeScreenshot");
            }
            return await Task.FromResult(result);

        }

        private object screenshotLocker = new object();
        private byte[] InternalTakeScreenshot(string url, bool AutoSave, string OutputFilename, IWebDriver webDriver)
        {
            byte[] result = new byte[0];

            //webDriver.Navigate().GoToUrl(url);
            try
            {
                webDriver.Navigate().GoToUrl(url);

                lock (screenshotLocker)
                {
                    Screenshot screenshot = ((ITakesScreenshot)webDriver).GetScreenshot();
                    if (AutoSave)
                    {
                        result = SaveScreenshot(url, screenshot, OutputFilename);
                    }
                }

                // Alterna de volta para a primeira aba
                //webDriver.SwitchTo().Window(webDriver.WindowHandles[0]);


                //var screenshot = (webDriver as ITakesScreenshot).GetScreenshot();

                //if (AutoSave)
                //{
                //    string OutputScreeenshotFilePath = string.Empty;
                //    lock (screenshotLogger)
                //    {
                //        result = SaveScreenshot(url, screenshot, out OutputScreeenshotFilePath);
                //    }

                //}/
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("ERR_NAME_NOT_RESOLVED"))
                {
                    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
                    _logger.LogError(ex, "Exception on ScrenshotService.TakeScreenshot");
                }

            }


            return result;
        }


        private byte[] SaveScreenshot(string url, Screenshot screenshot, string OutputFilename)
        {
            screenshot.SaveAsFile(OutputFilename);
            return screenshot.AsByteArray;
        }

        public void Dispose()
        {
            _webDriver.Close();
            _webDriver.Quit();
        }
    }
}


//--log-level=3
//--no-default-browser-check
//--disable-site-isolation-trials
//--no-experiments
//--ignore-gpu-blacklist
//--ignore-ssl-errors
//--ignore-certificate-errors
//--ignore-certificate-errors-spki-list
//--disable-gpu
//--disable-extensions
//--disable-default-apps
//--enable-features=NetworkService
//--disable-setuid-sandbox
//--no-sandbox
//--disable-webgl
//--disable-threaded-animation
//--disable-threaded-scrolling
//--disable-in-process-stack-traces
//--disable-histogram-customizer
//--disable-gl-extensions
//--disable-composited-antialiasing
//--disable-canvas-aa
//--disable-3d-apis
//--disable-accelerated-2d-canvas
//--disable-accelerated-jpeg-decoding
//--disable-accelerated-mjpeg-decode
//--disable-app-list-dismiss-on-blur
//--disable-accelerated-video-decode
//--disable-infobars
//--ignore-certifcate-errors
//--ignore-certifcate-errors-spki-list
//--disable-dev-shm-usage
//--disable-gl-drawing-for-tests
//--incognito
//--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.114 Safari/537.36
//--disable-web-security
//--aggressive-cache-discard
//--disable-cache
//--disable-application-cache
//--disable-offline-load-stale-cache
//--disk-cache-size=0
//--disable-background-networking
//--disable-sync
//--disable-translate
//--hide-scrollbars
//--metrics-recording-only
//--mute-audio
//--no-first-run
//--safebrowsing-disable-auto-update
//--no-zygote
//--window-size=800,600
//--start-maximized