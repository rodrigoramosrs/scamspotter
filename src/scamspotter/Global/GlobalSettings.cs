namespace ScamSpotter.Global
{
    internal static class GlobalSettings
    {
        public static int RequestTimeoutSeconds { get; internal set; } = 30;
        public static int MaximumPoolSize { get; set; }
        internal static bool SaveScreenshot { get; set; }
        internal static string TermsFullFilePath { get; set; }

        static GlobalSettings()
        {
            if (!Directory.Exists(OutputRootDirectory))
                Directory.CreateDirectory(OutputRootDirectory);

            if (!Directory.Exists(OutputScreenshotDirectory))
                Directory.CreateDirectory(OutputScreenshotDirectory);

        }

        internal static string OutputRootDirectory => Path.Combine(RootPath, "output");
        internal static string OutputScreenshotDirectory => Path.Combine(OutputRootDirectory, "screenshot");

        internal static string RootPath { get { return AppDomain.CurrentDomain.BaseDirectory; } }
    }
}
