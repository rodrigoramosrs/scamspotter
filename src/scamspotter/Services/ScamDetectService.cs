using Microsoft.Extensions.Logging;
using ScamSpotter.Models.OSINT;
using ScamSpotter.Services.OSINT;
using ScamSpotter.Utils;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Text;
using Whois.NET;

namespace ScamSpotter.Services
{
    public class ScamDetectService
    {
        private SemaphoreSlim _semaphore;

        public ScamDetectService(ILogger<ScamDetectService> logger, CrtShService crtService, ScreenshotService screnshotService, WhoIsService whoIsService)
        {
            _logger = logger;
            _logger.LogDebug("{0} initialized", nameof(ScamDetectService));
            _crtService = crtService;
            _screnshotService = screnshotService;
            _whoIsService = whoIsService;

        }

        public async Task StartDetection()
        {
            _semaphore = new SemaphoreSlim(Global.GlobalSettings.MaximumPoolSize);

            var terms = File.ReadAllLines(Global.GlobalSettings.TermsFullFilePath);
            int total = terms.Count();
            //int progress = 0;

            var progressInstance = AnsiConsole.Progress()
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),    // Task description
                    new ProgressBarColumn(),        // Progress bar
                    new PercentageColumn(),         // Percentage
                    new RemainingTimeColumn(),      // Remaining time
                                                    //new SpinnerColumn(),            // Spinner
                });
            progressInstance.RefreshRate = TimeSpan.FromMilliseconds(100);

            await progressInstance.StartAsync(async ctx =>
            {
                // Define tasks
                var poolSizeTask = ctx.AddTask("Calculating pool usage...", true, Global.GlobalSettings.MaximumPoolSize);

                var progressTask = ctx.AddTask("Calculating overal progress...", true, total);
                progressTask.Value = 0;

                var termsTask = ctx.AddTask("Calculating terms progress...", true, total);
                termsTask.Value = 0;

                var stepPoolSizeTask = new Action<int>(value =>
                {
                    lock (poolSizeTask)
                    {
                        poolSizeTask.Increment(value);
                        poolSizeTask.Description = $"[green]Pool usage: {poolSizeTask.Value} / {Global.GlobalSettings.MaximumPoolSize} [/]";
                    }

                });

                var stepProgressTask = new Action<int>(value =>
                {
                    lock (progressTask)
                    {
                        progressTask.Increment(value);
                        progressTask.Description = $"[green]Overal progress {progressTask.Value} / {total} [dim]({Math.Round(progressTask.Speed ?? 0, 0)} ops)[/][/]";
                    }
                });

                var stepTermsProgressTask = new Action<int, int, string>((value, termsTotal, term) =>
                {
                    lock (termsTask)
                    {
                        if (termsTask.MaxValue < termsTotal)
                            termsTask.MaxValue = termsTotal;

                        if (value > 0)
                            termsTask.Increment(value);
                        else
                            termsTask.Value = 0;

                        termsTask.Description = $"[green]Term '{term}' progress {termsTask.Value} / {termsTotal} [dim]({Math.Round(termsTask.Speed ?? 0, 0)} ops)[/][/]";
                    }
                });

                ConcurrentBag<Task> tasks = new ConcurrentBag<Task>();
                foreach (var term in terms)
                {
                    stepProgressTask.Invoke(1);
                    stepTermsProgressTask.Invoke(0, 0, term);
                    var searchTermsResult = await _crtService.DoSearch(term);
                    stepTermsProgressTask.Invoke(0, searchTermsResult.Count(), term);
                    foreach (var result in searchTermsResult)
                    {


                        await _semaphore.WaitAsync();
                        await Task.Delay(50);
                        var task = Task.Run(async () =>
                        {

                            try
                            {
                                stepPoolSizeTask.Invoke(1);

                                bool canContinue = !VerifyIfAlreadyScanned(result);
                                if (canContinue)
                                {
                                    var domainList = ExtractAllDomainFromCrtResult(result);
                                    foreach (var domain in domainList)
                                    {
                                        string url = $"https://{domain}";
                                        var whoisResult = await _whoIsService.QueryByDomain(result.common_name);

                                        AppendOutput(result, whoisResult);

                                        if (Global.GlobalSettings.SaveScreenshot)
                                        {
                                            string rootFilename = UrlUtils.RemoveInvalidUriCharsFromUrl(url.Replace("https", string.Empty).Replace("http", string.Empty));
                                            string filenamePrefix = $"{term}_{rootFilename}";
                                            string filenameSufix = $"{DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss")}.png";

                                            //TAKING ONLY ONE SCREENSHOT BY DOMAIN
                                            if(Directory.EnumerateFiles(Global.GlobalSettings.OutputScreenshotDirectory,$"*{rootFilename}*").Count() <= 0)
                                            {
                                                string OutputFilename = Path.Combine(Global.GlobalSettings.OutputScreenshotDirectory, $"{filenamePrefix}_{filenameSufix}");
                                                await _screnshotService.OpenNewWindowAndTakeScreenshot($"{url}", Global.GlobalSettings.SaveScreenshot, OutputFilename);
                                            }

                                            
                                        }

                                    }
                                }

                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Exception on ScamDetectService.Verify '{result.common_name}': {ex.Message} ", ex);
                            }
                            finally
                            {

                                stepPoolSizeTask.Invoke(-1);
                                stepTermsProgressTask.Invoke(1, searchTermsResult.Count(), term);
                                _semaphore.Release();
                            }



                        });
                        tasks.Add(task);

                        //Remove old tasks from list
                        task.ContinueWith(t =>
                        {
                            tasks.TryTake(out t);
                        });
                    }

                }

                await Task.WhenAll(tasks);

            });


        }

        private IEnumerable<string> ExtractAllDomainFromCrtResult(CrtShSearchResultModel cerModelResult)
        {
            var output = cerModelResult.name_value.Split("\n").ToList();

            if (!output.Contains(cerModelResult.common_name))
                output.Add(cerModelResult.common_name);

            return output.Distinct()
                .Where(x => !x.Contains("*"))
                .Order();
        }

        static object Locker = new object();
        private ILogger<ScamDetectService> _logger;
        private CrtShService _crtService;
        private ScreenshotService _screnshotService;
        private WhoIsService _whoIsService;

        private void AppendOutput(Models.OSINT.CrtShSearchResultModel param, WhoisResponse whoisResponse)
        {

            string OutputFilename = $"output.csv";
            string OutputFile = Path.Combine(Global.GlobalSettings.OutputRootDirectory, OutputFilename);

            lock (Locker)
            {
                if (!File.Exists(OutputFile))
                {
                    string Header = "id,issuer_ca_id,serial_number,common_name,name_value,result_count,entry_timestamp,not_before,not_after,org";
                    File.WriteAllText(OutputFile, Header);
                }
                string Content = $"\"{param.id}\",\"{param.issuer_ca_id}\",\"{param.serial_number}\",\"{param.common_name}\",\"{param.name_value}\",\"{param.result_count}\",\"{param.entry_timestamp}\",\"{param.not_before}\",\"{param.not_after}\",\"{whoisResponse.OrganizationName}\"";
                Content = Content.Replace(Environment.NewLine, " ").Replace("\n", " ");
                File.AppendAllText(OutputFile, $"{Content}\r\n");
            }
        }

        static string LastFileContent = string.Empty;
        private bool VerifyIfAlreadyScanned(Models.OSINT.CrtShSearchResultModel param)
        {
            if (!string.IsNullOrEmpty(LastFileContent))
                return LastFileContent.Contains(param.id.ToString());

            string OutputFilename = $"output.csv";
            string OutputFile = Path.Combine(Global.GlobalSettings.OutputRootDirectory, OutputFilename);
            if (!File.Exists(OutputFile)) return false;
            LastFileContent = File.ReadAllText(OutputFile, Encoding.UTF8);

            return LastFileContent.Contains(param.id.ToString());
        }
    }
}
