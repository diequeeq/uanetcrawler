using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using NLog;

namespace UaNetCrawler
{
    class SqliteDatabase : IDatabase
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private SQLiteConnection connection;

        public string Path = @"..\..\UaNet.sqlite";

        private long _id;
        private object _lock = new object();

        public void Initialize()
        {
            this.OpenDB();
            var maxId = this.connection.QuerySingle<int>("SELECT Max(Id) FROM job");
            _id = Math.Max(1000, maxId + 1);
        }

        public IDisposable OpenDB()
        {
            var connectionString = $"Data Source={this.Path};Version=3;";
            this.connection = new SQLiteConnection(connectionString);
            return this.connection;
        }

        private long GetNextId()
        {
            lock (_lock)
            {
                return _id++;
            }
        }

        public void DeleteDB()
        {
            throw new NotImplementedException();

            System.IO.File.Delete(this.Path);
        }

        public R ExecuteQuery<T, R>(Func<IQueryable<T>, R> query)
        {
            throw new NotImplementedException();
        }

        public void InsertObject<T>(T obj)
        {
            throw new NotImplementedException();
        }

        public void RunInTransaction(Action action)
        {
            connection.Open();
            using(var tran = this.connection.BeginTransaction())
            {
                try
                {
                    action();
                    tran.Commit();
                }
                catch
                {
                    tran.Rollback();
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        public void InsertJob(Job job)
        {
            var urlExists = this.connection.QueryFirstOrDefault<int?>("SELECT Id FROM job WHERE URL = @Url LIMIT 1", new {job.Url}).HasValue;

            if (urlExists)
            {
                Log.Debug($"Url already exists in database. [{job.Url}]");
                return;
            }

            job.Id = GetNextId();
            this.connection.Execute("INSERT INTO job(Id, ParentId, Domain, URL) VALUES (@Id, @ParentId, @Domain, @Url)",
                new { job.Id, job.ParentId, job.Domain, job.Url });
        }

        public void UpdateJob(Job job)
        {
            this.connection.Execute("Update job SET RequestTime = @RequestTime, Size = @Size, Status = @Status, IsProcessed = @IsProcessed WHERE Id = @Id",
                new { job.Id, job.RequestTime, job.Size, job.Status, job.IsProcessed });
        }

        public List<Job> GetNextJobs()
        {
            return this.connection.Query<Job>("SELECT * FROM Job WHERE IsProcessed IS NULL LIMIT 10").ToList();
        }
    }
}
