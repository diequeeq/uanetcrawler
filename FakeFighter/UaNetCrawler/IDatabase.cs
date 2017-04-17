using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UaNetCrawler
{
    public interface IDatabase
    {
        void Initialize();
        IDisposable OpenDB();
        void DeleteDB();
        void RunInTransaction(Action action);
        void InsertObject<T>(T obj);
        void InsertJob(Job job);
        void UpdateJob(Job job);
        R ExecuteQuery<T, R>(Func<IQueryable<T>, R> query);
        List<Job> GetNextJobs();
    }
}
