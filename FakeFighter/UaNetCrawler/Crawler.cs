using HtmlAgilityPack;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace UaNetCrawler
{
    public class Crawler
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly Regex _titleCheck = new Regex("(абвгдеєжзиіїйклмнопрстуфхцчшщэъьюя)");

        public volatile int _run;
        private IDatabase database;

        public Crawler(IDatabase database)
        {
            this.database = database;
            this.database.OpenDB();
            _client = new HttpClient();
        }

        public void Start()
        {
            Interlocked.Exchange(ref _run, 1);
            List<Job> jobs;
            var tasks = new List<Task>();
            do
            {
                jobs = ReadJobs();
                tasks.Clear();

                foreach (var job in jobs)
                {
                    //tasks.Add(Task.Run(() => ProcessJob(job)));
                    tasks.Add(ProcessJob(job));
                }

                Task.WaitAll(tasks.ToArray());

                database.RunInTransaction(() =>
                {
                    foreach (var job in jobs)
                    {
                        database.UpdateJob(job);
                    }
                });
            }
            while (jobs.Count > 0 && _run == 1);
        }

        private async Task<IEnumerable<Job>> ProcessJob(Job job)
        {
            try
            {
                job.RequestTime = DateTimeOffset.Now;

                var web = new HtmlWeb();
                web.PostResponse += (HttpWebRequest request, HttpWebResponse response) => job.Size = response.ContentLength;
                var doc = web.Load(job.Url);

                job.IsProcessed = web.StatusCode == System.Net.HttpStatusCode.OK;
                job.Status = (int)web.StatusCode;

                var title = doc.DocumentNode.Descendants("Title").First().InnerText;

                if (IsGoodTitle(title))
                {
                    var linkedPages = doc.DocumentNode.Descendants("a")
                                  .Select(a => a.GetAttributeValue("href", null))
                                  .Where(u => IsGoodLink(u));

                    return linkedPages.Select(p => new Job() { ParentId = job.Id, Domain = GetDomain(job), Url = p }).ToList();
                }
                else
                {
                    return Enumerable.Empty<Job>();
                }
                
            }
            catch (Exception ex)
            {
                job.IsProcessed = false;
                return Enumerable.Empty<Job>();
            }
        }

        private bool IsGoodTitle(string title)
        {
            return _titleCheck.IsMatch(title);
        }

        private static string GetDomain(Job job)
        {
            return new Uri(job.Url).DnsSafeHost;
        }

        private static bool IsGoodLink(string url)
        {
            return !String.IsNullOrEmpty(url) && Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute) && !url.Contains("utm_") && !url.Contains("rnd=") && !url.Contains("password") && !url.Contains("javascript");
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
