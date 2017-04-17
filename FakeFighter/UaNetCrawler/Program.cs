using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UaNetCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
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
    }
}
