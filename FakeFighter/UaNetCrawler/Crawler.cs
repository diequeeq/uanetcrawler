using HtmlAgilityPack;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace UaNetCrawler
{
    public class Crawler
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly Regex _titleCheck = new Regex("[абвгдеєжзиіїйклмнопрстуфхцчшщэъьюя]");

        public volatile int _run;
        private IDatabase database;

        public Crawler(IDatabase database)
        {
            this.database = database;
            this.database.OpenDB();
        }

        public void Start()
        {
            Interlocked.Exchange(ref _run, 1);
            List<Job> jobs;
            var tasks = new List<Task>();
            var queue = new ConcurrentQueue<Job>();
            var urls = new HashSet<string>();
            do
            {
                jobs = ReadJobs();
                tasks.Clear();

                foreach (var job in jobs)
                {
                    //tasks.Add(Task.Run(() => ProcessJob(job)));
                    tasks.Add(ProcessJob(job, queue));
                }

                Task.WaitAll(tasks.ToArray());

                database.RunInTransaction(() =>
                {
                    foreach (var job in jobs)
                    {
                        database.UpdateJob(job);
                        urls.Add(job.Url);
                    }

                    foreach (var job in queue)
                    {
                        if (urls.Contains(job.Url))
                        {
                            Log.Debug($"Duplicated url found, skipping. [{job.Url}]");
                            continue;
                        }

                        database.InsertJob(job);
                    }
                });
            }
            while (jobs.Count > 0 && _run == 1);
        }

        private async Task ProcessJob(Job job, ConcurrentQueue<Job> queue)
        {
            try
            {
                job.RequestTime = DateTimeOffset.Now;

                var web = new HtmlWeb();
                web.OverrideEncoding = GetEncoding(job.Domain);
                web.PostResponse += (HttpWebRequest request, HttpWebResponse response) => job.Size = response.ContentLength;
                var doc = web.Load(job.Url);

                job.IsProcessed = web.StatusCode == System.Net.HttpStatusCode.OK;
                job.Status = (int)web.StatusCode;

                var title = doc.DocumentNode.Descendants("Title").FirstOrDefault()?.InnerText;

                if (IsGoodTitle(title))
                {
                    var linkedPages = doc.DocumentNode.Descendants("a")
                        .Select(a => a.GetAttributeValue("href", null))
                        .Where(u => IsGoodLink(u))
                        .Select(u => ToFullUrl(job.Domain, u));

                    var jobs = linkedPages.Select(p => new Job() {ParentId = job.Id, Domain = GetDomain(p), Url = p}).ToList();

                    Log.Info($"{jobs.Count} urls found inside [{job.Url}]");

                    foreach (var j in jobs)
                    {
                        queue.Enqueue(j);
                    }
                }
                else
                {
                    Log.Warn($"Title check failed: [{title}] for [{job.Url}]");
                }

                if (job.Size == -1)
                {
                    try
                    {
                        job.Size = doc.DocumentNode.InnerHtml.Length;
                    }
                    catch (NullReferenceException)
                    {
                        Log.Error($"Error calculating size of [{job.Url}]");
                        job.Size = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                job.IsProcessed = false;
                Log.Error(ex, $"Error processing job for [{job.Url}]");
            }
        }

        private string ToFullUrl(string url, string href)
        {
            if (href.StartsWith("//"))
            {
                href = "http:" + href;
            }
            if(!Uri.IsWellFormedUriString(href, UriKind.Absolute))
            {
                return "http://" + url + (href[0] == '/' ? href : "/" + href);
            }
            return href;
        }

        private static Encoding GetEncoding(string domain)
        {
            if (domain.Equals("pravda.com.ua") || domain.Equals("gazeta.ru") || domain.Equals("i.ua") || domain.Equals("tvgid.ua"))
            {
                return Encoding.GetEncoding("Windows-1251");
            }

            return Encoding.UTF8;
        }

        private bool IsGoodTitle(string title)
        {
            return !string.IsNullOrEmpty(title) && _titleCheck.IsMatch(title);
        }

        private static string GetDomain(string url)
        {
            return new Uri(url).DnsSafeHost.Replace("www.", string.Empty);
        }

        private static bool IsGoodLink(string url)
        {
            return !String.IsNullOrEmpty(url) && Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute) && url != "/" && !url.Contains("utm_") && !url.Contains("rnd=") && !url.Contains("password") && !url.Contains("javascript");
        }

        private List<Job> ReadJobs()
        {
            return database.GetNextJobs();
        }

        public void Stop()
        {
            Interlocked.Exchange(ref _run, 0);
        }
    }
}
