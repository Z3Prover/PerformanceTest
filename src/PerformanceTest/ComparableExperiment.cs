using System;


namespace PerformanceTest
{
    public class ComparableExperiment
    {
        public ComparableExperiment(int id, DateTime submitted, double maxTimeout, ComparableResult[] results)
        {
            Id = id;
            SubmissionTime = submitted;
            MaxTimeout = maxTimeout;
            Results = results;
        }

        public int Id { get; internal set; }

        public double MaxTimeout { get; internal set; }

        public ComparableResult[] Results { get; internal set; }

        public DateTime SubmissionTime { get; internal set; }
    }    
}
