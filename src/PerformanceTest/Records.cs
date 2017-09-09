using Measurement;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Records
{
    public class RecordsTable
    {
        private readonly Dictionary<string, Record> records;
        private readonly Dictionary<string, CategoryRecord> categoryRecords;

        public RecordsTable(Dictionary<string, Record> records, Dictionary<string, CategoryRecord> categoryRecords)
        {
            this.records = records;
            this.categoryRecords = categoryRecords;
        }

        /// <summary>Record for each benchmark (maps from benchmark filename).</summary>
        public Dictionary<string, Record> BenchmarkRecords { get { return records; } }

        /// <summary>Returns the benchmark records aggregated by categories (maps from category name).</summary>
        public Dictionary<string, CategoryRecord> CategoryRecords { get { return categoryRecords; } }

        public CategoryRecord Overall
        {
            get
            {
                CategoryRecord res = new CategoryRecord(0.0, 0);
                foreach (KeyValuePair<string, CategoryRecord> kvp in categoryRecords)
                    res = res.Add(kvp.Value.Runtime, kvp.Value.Files);
                return res;
            }
        }

        public void UpdateWith(IEnumerable<BenchmarkResult> results, Domain domain)
        {
            foreach (var r in results)
            {
                if (domain.CanConsiderAsRecord(new ProcessRunAnalysis(r.Status, r.Properties)))
                {
                    Record record;
                    if (!records.TryGetValue(r.BenchmarkFileName, out record) || record.Runtime > r.CPUTime.TotalSeconds)
                    {
                        // New record found
                        records[r.BenchmarkFileName] = new Record(r.ExperimentID, r.CPUTime.TotalSeconds);                        string category = GetCategory(r.BenchmarkFileName);
                    }
                }
            }
            RebuildSummary();
        }

        private void RebuildSummary()
        {
            categoryRecords.Clear();
            foreach (var item in records)
            {
                string category = GetCategory(item.Key);

                CategoryRecord catRecord;
                if (!categoryRecords.TryGetValue(category, out catRecord))
                    catRecord = new CategoryRecord(0, 0);

                categoryRecords[category] = catRecord.Add(item.Value.Runtime, 1);
            }
        }



        public static string GetCategory(string filename)
        {
            int i = filename.IndexOf("/");
            string category = i >= 0 ? filename.Substring(0, i) : string.Empty;
            return category;
        }
    }

    public struct Record
    {
        public Record(int expId, double runtime)
        {
            this.ExperimentId = expId;
            this.Runtime = runtime;
        }

        public int ExperimentId { get; private set; }

        public double Runtime { get; private set; }
    }

    public struct CategoryRecord
    {
        public CategoryRecord(double runtime, int files)
        {
            this.Runtime = runtime;
            this.Files = files;
        }

        public int Files { get; private set; }

        public double Runtime { get; private set; }

        public CategoryRecord Add(double runtime, int files)
        {
            return new CategoryRecord(Runtime + runtime, Files + files);
        }
    }

}
