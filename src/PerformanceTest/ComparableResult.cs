using System;
using System.Globalization;

using Measurement;

namespace PerformanceTest
{
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
        public double Runtime { get { return result.CPUTime.TotalSeconds; } }
        public int SAT { get { return sat; } }
        public int UNSAT { get { return unsat; } }
        public int UNKNOWN { get { return unknown; } }
    }
}
