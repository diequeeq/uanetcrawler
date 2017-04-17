using System;

namespace UaNetCrawler
{
    public class Job
    {
        public long Id { get; set; }
        public long ParentId { get; set; }
        public DateTimeOffset? RequestTime { get; set; }
        public string Domain { get; set; }
        public string Url { get; set; }
        public long? Size { get; set; }
        public int? Status { get; set; }
        public bool? IsProcessed { get; set; }
    }
}