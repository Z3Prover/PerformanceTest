using Measurement;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;

namespace Nightly
{
    public class ExperimentViewModel
    {
        private ExperimentSummary summary;

        public ExperimentViewModel(ExperimentSummary summary, bool isFinished)
        {
            this.summary = summary;
            this.IsFinished = isFinished;
        }

        public AggregatedAnalysis this[string category]
        {
            get
            {
                if (string.IsNullOrEmpty(category))
                {
                    return summary.Overall;
                }
                else
                {
                    return summary.CategorySummary[category];
                }
            }
        }

        public int Id { get { return summary.Id; } }

        public bool IsFinished { get; internal set; }

        public DateTime SubmissionTime { get { return summary.Date.Date; } }

        public TimeSpan Timeout { get; internal set; }

        public ExperimentSummary Summary { get { return summary; } }
    }

    public class Z3SummaryProperties
    {
        private readonly IReadOnlyDictionary<string, string> props;

        private Z3SummaryProperties(AggregatedAnalysis summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            props = summary.Properties;
        }

        public int Sat { get { return int.Parse(props[Z3Domain.KeySat], CultureInfo.InvariantCulture); } }
        public int Unsat { get { return int.Parse(props[Z3Domain.KeyUnsat], CultureInfo.InvariantCulture); } }

        public int TargetSat { get { return int.Parse(props[Z3Domain.KeyTargetSat], CultureInfo.InvariantCulture); } }
        public int TargetUnsat { get { return int.Parse(props[Z3Domain.KeyTargetUnsat], CultureInfo.InvariantCulture); } }
        public int TargetUnknown { get { return int.Parse(props[Z3Domain.KeyTargetUnknown], CultureInfo.InvariantCulture); } }

        public double TimeUnsat { get { return double.Parse(props[Z3Domain.KeyTimeUnsat], CultureInfo.InvariantCulture); } }
        public double TimeSat { get { return double.Parse(props[Z3Domain.KeyTimeSat], CultureInfo.InvariantCulture); } }

        public static Z3SummaryProperties TryWrap(AggregatedAnalysis summary)
        {
            if (summary == null || !summary.Properties.ContainsKey(Z3Domain.KeySat)) return null;
            return new Z3SummaryProperties(summary);
        }
    }
}