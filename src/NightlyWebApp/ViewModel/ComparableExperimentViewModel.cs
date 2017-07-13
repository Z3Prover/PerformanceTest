using Measurement;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;

namespace Nightly
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

    public class ComparableResult
    {
        private readonly BenchmarkResult result;
        private int sat, unsat, unknown;

        public ComparableResult(BenchmarkResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            this.result = result;

            sat = int.Parse(result.Properties[Z3Domain.KeySat], CultureInfo.InvariantCulture);
            unsat = int.Parse(result.Properties[Z3Domain.KeyUnsat], CultureInfo.InvariantCulture);
            unknown = int.Parse(result.Properties[Z3Domain.KeyUnknown], CultureInfo.InvariantCulture);
        }

        public string Filename { get { return result.BenchmarkFileName; } }

        public ResultStatus Status { get { return result.Status; } }

        public double Runtime { get { return result.NormalizedRuntime; } }
        public int SAT { get { return sat; } }
        public int UNSAT { get { return unsat; } }
        public int UNKNOWN { get { return unknown; } }
    }
}
