using System;
using NLog;
using NLog.Config;

namespace UaNetCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            InitNlog();

            IDatabase database = new SqliteDatabase();
            database.Initialize();
            var crawler = new Crawler(database);
            crawler.Start();
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey();
            } while (key.Key != ConsoleKey.Escape);

            crawler.Stop();
        }

        private static void InitNlog()
        {
            var nlogConfigFile = "NLog.config";
            LogManager.Configuration = new XmlLoggingConfiguration(nlogConfigFile, true);
            //LogManager.Configuration.Variables["appName"] = ?;
            LogManager.Configuration.Variables["fileSuffix"] = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss ");
            LogManager.ReconfigExistingLoggers();
        }
    }
}
