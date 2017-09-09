using Measurement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest
{
    /// <summary>
    /// Stores for which benchmark files there are issues in an experiment.
    /// </summary>
    public class ExperimentStatusSummary
    {
        private readonly Dictionary<string, List<string>> errorsByCategory;
        private readonly Dictionary<string, List<string>> bugsByCategory;
        private readonly Dictionary<string, Dictionary<string, List<string>>> tagsByCategory;
        private readonly Dictionary<string, List<string>> dippersByCategory;

        public ExperimentStatusSummary(int id, int? referenceId,
            Dictionary<string, List<string>> errorsByCategory,
            Dictionary<string, List<string>> bugsByCategory,
            Dictionary<string, Dictionary<string, List<string>>> tagsByCategory,
            Dictionary<string, List<string>> dippersByCategory)
        {
            if (errorsByCategory == null) throw new ArgumentNullException(nameof(errorsByCategory));
            if (bugsByCategory == null) throw new ArgumentNullException(nameof(bugsByCategory));
            if (tagsByCategory == null) throw new ArgumentNullException(nameof(tagsByCategory));
            if (dippersByCategory == null) throw new ArgumentNullException(nameof(dippersByCategory));

            Id = id;
            ReferenceId = referenceId;
            this.errorsByCategory = errorsByCategory;
            this.bugsByCategory = bugsByCategory;
            this.tagsByCategory = tagsByCategory;
            this.dippersByCategory = dippersByCategory;
        }

        public int Id { get; private set; }

        /// <summary>
        /// The experiment for which this experiment was compared when the dippers was built.
        /// </summary>
        public int? ReferenceId { get; private set; }

        public Dictionary<string, List<string>> ErrorsByCategory { get { return errorsByCategory; } }
        public Dictionary<string, List<string>> BugsByCategory { get { return bugsByCategory; } }

        /// <summary>
        /// Maps domain-specific tag to a map from category to file name which holds that property.
        /// </summary>
        public Dictionary<string, Dictionary<string, List<string>>> TagsByCategory { get { return tagsByCategory; } }
        public Dictionary<string, List<string>> DippersByCategory { get { return dippersByCategory; } }


        public static ExperimentStatusSummary Build(int expId, IEnumerable<BenchmarkResult> results, int? refExpId, IEnumerable<BenchmarkResult> refResults, Domain domain)
        {
            if (domain == null) throw new ArgumentNullException(nameof(domain));

            Dictionary<string, double> referenceTimes = null;
            if (refExpId != null)
            {
                referenceTimes = new Dictionary<string, double>();
                foreach (var r in refResults)
                {
                    if (r.Status == Measurement.ResultStatus.Success)
                        referenceTimes[r.BenchmarkFileName] = r.CPUTime.TotalSeconds;
                }
            }

            Dictionary<string, List<string>> errorsByCategory = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> bugsByCategory = new Dictionary<string, List<string>>();
            Dictionary<string, Dictionary<string, List<string>>> tagsByCategory = new Dictionary<string, Dictionary<string, List<string>>>();
            Dictionary<string, List<string>> dippersByCategory = new Dictionary<string, List<string>>();

            foreach (var r in results)
            {
                string fn = r.BenchmarkFileName;
                string cat = Records.RecordsTable.GetCategory(fn);

                switch (r.Status)
                {
                    case Measurement.ResultStatus.Success:
                        var tags = domain.GetTags(new ProcessRunAnalysis(r.Status, r.Properties));
                        foreach (string tag in tags) {
                            Dictionary<string, List<string>> byCat;
                            if (!tagsByCategory.TryGetValue(tag, out byCat))
                                tagsByCategory.Add(tag, byCat = new Dictionary<string, List<string>>());

                            AddToList(byCat, cat, fn);
                        }

                        if (referenceTimes != null && referenceTimes.ContainsKey(fn))
                        {
                            double new_time = r.CPUTime.TotalSeconds;
                            double old_time = referenceTimes[fn];

                            if (new_time > 1.0 && old_time > 1.0 &&
                                new_time >= 10.0 * old_time)
                            {
                                string msg = fn + " [" + (new_time - old_time) + " sec. slower]";
                                AddToList(dippersByCategory, cat, msg);
                            }
                        }
                        break;

                    case Measurement.ResultStatus.OutOfMemory:
                        if (referenceTimes != null && referenceTimes.ContainsKey(fn))
                        {
                            double old_time = referenceTimes[fn];
                            string msg = fn + " [went from " + old_time + " sec. to memory-out]";
                            AddToList(dippersByCategory, cat, msg);
                        }
                        break;
                    case Measurement.ResultStatus.Timeout:
                        if (referenceTimes != null && referenceTimes.ContainsKey(fn))
                        {
                            double old_time = referenceTimes[fn];
                            double new_time = r.CPUTime.TotalSeconds;

                            if (new_time - old_time > 10.0)
                            {
                                string msg = fn + " [more than " + (new_time - old_time) + " sec. slower]";
                                AddToList(dippersByCategory, cat, msg);
                            }
                        }
                        break;

                    case Measurement.ResultStatus.Error:
                    case Measurement.ResultStatus.InfrastructureError:
                        AddToList(errorsByCategory, cat, fn);
                        break;

                    case Measurement.ResultStatus.Bug:
                        AddToList(bugsByCategory, cat, fn);
                        break;
                }
            }

            return new ExperimentStatusSummary(expId, refExpId, errorsByCategory, bugsByCategory, tagsByCategory, dippersByCategory);
        }

        private static void AddToList(Dictionary<string, List<string>> dict, string cat, string value)
        {
            List<string> list;
            if (!dict.TryGetValue(cat, out list))
                dict.Add(cat, list = new List<string>());
            list.Add(value);

            if (!dict.TryGetValue("", out list))
                dict.Add("", list = new List<string>());
            list.Add(value);
        }
    }
}
