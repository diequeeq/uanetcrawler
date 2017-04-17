using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace UaNetCrawler
{
    class SqliteDatabase : IDatabase
    {
        private SQLiteConnection connection;

        public string Path = @"d:\Slavik\work\kitsoft\FakeFighter\UaNetCrawler\UaNet.sqlite";

        private long _id = 1000;
        private object _lock = new object();

        public void Initialize()
        {
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
            }
        }

        public void InsertJob(Job job)
        {
            job.Id = GetNextId();
            this.connection.Execute("INSERT INTO job(Id, ParentId, Domain, URL, IsProcessed) VALUES (@Id, @ParentId, @Domain, @Url, @IsProcessed)",
                new { job.Id, job.ParentId, job.Domain, job.Url, IsProcessed = DBNull.Value });
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
