using Measurement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PerformanceTest
{

    public class ExperimentSummary
    {
        private AggregatedAnalysis overall;

        public ExperimentSummary(int id, DateTimeOffset date, IReadOnlyDictionary<string, AggregatedAnalysis> categorySummary)
        {
            if (categorySummary == null) throw new ArgumentNullException(nameof(categorySummary));
            Id = id;
            Date = date;
            CategorySummary = categorySummary;
        }

        public int Id { get; private set; }
        public DateTimeOffset Date { get; private set; }

        public IReadOnlyDictionary<string, AggregatedAnalysis> CategorySummary { get; private set; }

        public AggregatedAnalysis Overall
        {
            get
            {
                if (overall != null) return overall;

                int bugs = 0;
                int errors = 0;
                int infrastructureErrors = 0;
                int timeouts = 0;
                int memouts = 0;
                int files = 0;
                Dictionary<string, double> props = new Dictionary<string, double>();
                foreach (var item in CategorySummary)
                {
                    var sum = item.Value;

                    bugs += sum.Bugs;
                    errors += sum.Errors;
                    infrastructureErrors += sum.InfrastructureErrors;
                    timeouts += sum.Timeouts;
                    memouts += sum.MemoryOuts;
                    files += sum.Files;

                    foreach (var p in sum.Properties)
                    {
                        double v, w;
                        if (double.TryParse(p.Value, out w))
                        {
                            if (props.TryGetValue(p.Key, out v))
                                props[p.Key] = v + w;
                            else
                                props.Add(p.Key, w);
                        }
                    }
                }
                var stringProps = new Dictionary<string, string>(props.Count);
                foreach (var item in props)
                {
                    stringProps.Add(item.Key, item.Value.ToString());
                }
                overall = new AggregatedAnalysis(bugs, errors, infrastructureErrors, timeouts, memouts, stringProps, files);
                return overall;
            }
        }



        public static Dictionary<string, AggregatedAnalysis> Build(IEnumerable<BenchmarkResult> results, Domain domain, DuplicateResolution duplicateResolution = DuplicateResolution.Ignore)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (domain == null) throw new ArgumentNullException(nameof(domain));

            var categories = new Dictionary<string, List<BenchmarkResult>>();
            HashSet<string> seen = new HashSet<string>();
            foreach (var result in results)
            {
                if (duplicateResolution != DuplicateResolution.TakeAll)
                {
                    if (seen.Contains(result.BenchmarkFileName))
                    {
                        if (duplicateResolution == DuplicateResolution.Fail) throw new InvalidOperationException("Duplicate found");
                        continue;
                    }
                    seen.Add(result.BenchmarkFileName);
                }

                int i = result.BenchmarkFileName.IndexOf("/");
                string category = i >= 0 ? result.BenchmarkFileName.Substring(0, i) : string.Empty;

                List<BenchmarkResult> catResults;
                if (!categories.TryGetValue(category, out catResults))
                {
                    catResults = new List<BenchmarkResult>();
                    categories.Add(category, catResults);
                }
                catResults.Add(result);
            }

            var stats = new Dictionary<string, AggregatedAnalysis>(categories.Count);
            foreach (var cr in categories)
            {
                var catSummary = domain.Aggregate(cr.Value.Select(r => new ProcessRunResults(new ProcessRunAnalysis(r.Status, r.Properties), r.NormalizedRuntime)));
                stats.Add(cr.Key, catSummary);
            }
            return stats;
        }

        public enum DuplicateResolution
        {
            Fail,
            Ignore,
            TakeAll
        }
    }
}
