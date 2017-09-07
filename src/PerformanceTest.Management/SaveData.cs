using Measurement;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{

    class CSVDatum
    {
        public int? rv = null;
        public double normalized_time = 0.0, cpu_time = 0.0, wallclock_time = 0.0;
        public int sat = 0, unsat = 0, unknown = 0;
    }
    public class SaveData
    {
        SaveData () {}
        private static int[] computeUnique(ExperimentStatusViewModel[] experiments, ExperimentResults[] b)
        {
            int[] res = new int[experiments.Length];
            for (int i = 0; i < experiments.Length; i++) res[i] = 0;

            Dictionary<string, Dictionary<int, int?>> data =
                    new Dictionary<string, Dictionary<int, int?>>();
            //create dictionary for all benchmarks
            for (int i = 0; i < experiments.Length; i++)
            {
                int id = experiments[i].ID;
                var bi = b[i].Benchmarks;
                for (int j = 0; j < bi.Length; j++)
                {
                    BenchmarkResult bij = bi[j];
                    string filename = experiments[i].Category + "/" + bij.BenchmarkFileName;
                    if (!data.ContainsKey(filename))
                        data.Add(filename, new Dictionary<int, int?>());
                    if (!data[filename].ContainsKey(id))
                        data[filename].Add(id, bij.ExitCode);
                }
            }
            // find similar for all experiments benchmarks and check exitCode.
            foreach (KeyValuePair<string, Dictionary<int, int?>> d in data.OrderBy(x => x.Key))
            {
                int count = d.Value.Count();
                if (count == experiments.Length)
                {
                    for (int i = 0; i < count; i++)
                    {
                        int idi = experiments[i].ID;
                        bool condition = true;
                        for (int j = 0; j < count; j++)
                        {
                            int idj = experiments[j].ID;
                            if (i == j) condition = condition && d.Value[idj] == 0;
                            else condition = condition && d.Value[idj] != 0;
                        }
                        if (condition) ++res[i];
                    }
                }
            }
            return res;
        }
        public static async void SaveMetaCSV (string filename, ExperimentStatusViewModel[] experiments, ExperimentManager manager, IDomainResolver domainResolver, IUIService uiService)
        {
            if (filename == null) throw new ArgumentNullException("filename");
            if (experiments == null) throw new ArgumentNullException("experiments");
            if (manager == null) throw new ArgumentNullException("manager");
            if (domainResolver == null) throw new ArgumentNullException("domain");
            if (uiService == null) throw new ArgumentNullException("uiService");

            var handle = uiService.StartIndicateLongOperation("Save meta csv...");
            try
            {
                StreamWriter f = new StreamWriter(filename, false);
                f.WriteLine("\"ID\",\"# Total\",\"# SAT\",\"# UNSAT\",\"# UNKNOWN\",\"# Timeout\",\"# Memout\",\"# Bug\",\"# Error\",\"# Unique\",\"Parameters\",\"Note\"");
                var count = experiments.Length;
                var b = new ExperimentResults[count];
                b = await DownloadResultsAsync(experiments, manager);
                var unique = computeUnique(experiments, b);
                for (var i = 0; i < count; i++)
                {
                    var domain = domainResolver.GetDomain(experiments[i].Definition.DomainName ?? "Z3");
                    var aggr = domain.Aggregate(b[i].Benchmarks.Select(r => new ProcessRunResults(new ProcessRunAnalysis(r.Status, r.Properties), r.NormalizedRuntime)));
                    var statistics = new ExperimentStatistics(aggr);
                    var def = experiments[i].Definition;
                    string ps = def.Parameters.Trim(' ');
                    string note = experiments[i].Note.Trim(' ');
                    int? sat = statistics == null ? null : (int?)int.Parse(statistics.AggregatedResults.Properties[Z3Domain.KeySat], CultureInfo.InvariantCulture);
                    int? unsat = statistics == null ? null : (int?)int.Parse(statistics.AggregatedResults.Properties[Z3Domain.KeyUnsat], CultureInfo.InvariantCulture);
                    int? unknown = statistics == null ? null : (int?)int.Parse(statistics.AggregatedResults.Properties[Z3Domain.KeyUnknown], CultureInfo.InvariantCulture);
                    int? bugs = statistics == null ? null : (int?)statistics.AggregatedResults.Bugs;
                    int? errors = statistics == null ? null : (int?)statistics.AggregatedResults.Errors;
                    int? timeouts = statistics == null ? null : (int?)statistics.AggregatedResults.Timeouts;
                    int? memouts = statistics == null ? null : (int?)statistics.AggregatedResults.MemoryOuts;

                    f.WriteLine(experiments[i].ID + "," +
                                experiments[i].BenchmarksTotal + "," +
                                sat + "," +
                                unsat + "," +
                                unknown + "," +
                                timeouts + "," +
                                memouts + "," +
                                bugs + "," +
                                errors + "," +
                                unique[i] + "," +
                                "\"" + ps + "\"," +
                                "\"" + note + "\"");
                }
                f.WriteLine();
                f.Close();
        }
            catch (Exception ex)
            {
                uiService.ShowError(ex, "Failed to save meta CSV");
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
        }
        public static async void SaveCSV (string filename, ExperimentStatusViewModel[] experiments, ExperimentManager manager, IUIService uiService)
        {
            if (filename == null) throw new ArgumentNullException("filename");
            if (experiments == null) throw new ArgumentNullException("experiments");
            if (manager == null) throw new ArgumentNullException("manager");
            if (uiService == null) throw new ArgumentNullException("uiService");

            var handle = uiService.StartIndicateLongOperation("Save csv...");
            try
            {
                StreamWriter f = new StreamWriter(filename, false);
                var count = experiments.Length;
                Dictionary<string, Dictionary<int, CSVDatum>> data =
                    new Dictionary<string, Dictionary<int, CSVDatum>>();
                var benchs = await DownloadResultsAsync(experiments, manager);
                f.Write(",");
                for (var i = 0; i < count; i++)
                {
                    int id = experiments[i].ID;
                    var def = experiments[i].Definition;
                    string ps = def.Parameters == null ? "" : def.Parameters.Trim(' ');
                    string note = experiments[i].Note == null ? "" : experiments[i].Note.Trim(' ');
                    var ex_timeout = def.BenchmarkTimeout.TotalSeconds;

                    f.Write("\"" + note + "\",");
                    if (ps != "") f.Write("\"" + ps + "\"");
                    f.Write(",,,,,,");

                    double error_line = 10.0 * ex_timeout;
                    var benchmarks = benchs[i].Benchmarks;
                    bool HasDuplicates = false;
                    for (var j = 0; j < benchmarks.Length; j++)
                    {
                        BenchmarkResult b = benchmarks[j];
                        CSVDatum cur = new CSVDatum();
                        cur.rv = b.ExitCode.Equals(DBNull.Value) ? null : (int?)b.ExitCode;
                        cur.normalized_time = b.NormalizedRuntime.Equals(DBNull.Value) ? ex_timeout : b.NormalizedRuntime;
                        cur.cpu_time = b.TotalProcessorTime == null ? ex_timeout : b.TotalProcessorTime.TotalSeconds;
                        cur.wallclock_time = b.WallClockTime == null ? ex_timeout : b.WallClockTime.TotalSeconds;
                        cur.sat = Int32.Parse(b.Properties[Z3Domain.KeySat], CultureInfo.InvariantCulture);
                        cur.unsat = Int32.Parse(b.Properties[Z3Domain.KeyUnsat], CultureInfo.InvariantCulture);
                        cur.unknown = Int32.Parse(b.Properties[Z3Domain.KeyUnknown], CultureInfo.InvariantCulture);

                        bool rv_ok = b.Status != ResultStatus.Error && b.Status != ResultStatus.InfrastructureError &&
                                     (b.Status == ResultStatus.Timeout && cur.rv == null ||
                                     (b.Status == ResultStatus.Success && (cur.rv == 0 || cur.rv == 10 || cur.rv == 20)));

                        if (cur.sat == 0 && cur.unsat == 0 && !rv_ok) cur.normalized_time = error_line;
                        if (cur.normalized_time < 0.01) cur.normalized_time = 0.01;

                        string Benchmarkfilename = b.BenchmarkFileName.Contains("/") ? experiments[i].Category + "/" + b.BenchmarkFileName : experiments[i].Category + @"\" + b.BenchmarkFileName;
                        if (!data.ContainsKey(Benchmarkfilename)) data.Add(Benchmarkfilename, new Dictionary<int, CSVDatum>());
                        if (data[Benchmarkfilename].ContainsKey(id))
                            HasDuplicates = true;
                        else
                            data[Benchmarkfilename].Add(id, cur);
                    }
                    if (HasDuplicates)
                        uiService.ShowWarning(String.Format("Duplicates in experiment #{0} ignored", id), "Duplicate warning");
                }
                f.WriteLine();

                // Write headers
                f.Write(",");
                for (int i = 0; i < count; i++)
                {
                    int id = experiments[i].ID;
                    f.Write("\"RV " + id + "\",\"T_norm " + id + "\",\"T_cpu " + id + "\",\"T_wc " + id + "\",\"# SAT " + id + "\",\"# UNSAT " + id + "\",\"# UNKNOWN " + id + "\",");
                }
                f.WriteLine();

                // Write data.
                foreach (KeyValuePair<string, Dictionary<int, CSVDatum>> d in data.OrderBy(x => x.Key))
                {
                    bool skip = false;
                    for (int i = 0; i < count; i++)
                    {
                        int id = experiments[i].ID;
                        if (!d.Value.ContainsKey(id) || d.Value[id] == null)
                            skip = true;
                    }
                    if (skip)
                        continue;

                    f.Write("\"" + d.Key + "\",");

                    for (int i = 0; i < count; i++)
                    {
                        int id = experiments[i].ID;

                        if (!d.Value.ContainsKey(id) || d.Value[id] == null)
                            f.Write("MISSING,,,,");
                        else
                        {
                            CSVDatum c = d.Value[id];
                            f.Write(c.rv + ",");
                            f.Write(c.normalized_time + ",");
                            f.Write(c.cpu_time + ",");
                            f.Write(c.wallclock_time + ",");
                            f.Write(c.sat + ",");
                            f.Write(c.unsat + ",");
                            f.Write(c.unknown + ",");
                        }
                    }
                    f.WriteLine();
                }
                f.Close();
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex, "Failed to save CSV");
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
        }
        public static async void SaveOutput (string selectedPath, ExperimentStatusViewModel experiment, ExperimentManager manager, IUIService uiService)
        {
            if (experiment == null) throw new ArgumentNullException("experiment");
            if (manager == null) throw new ArgumentNullException("manager");
            if (uiService == null) throw new ArgumentNullException("uiService");

            var handle = uiService.StartIndicateLongOperation("Saving output...");
            try
            {
                string drctry = string.Format(@"{0}\{1}", selectedPath, experiment.ID.ToString());
                await Task.Run(async () =>
                {
                    double total = 0.0;
                    Directory.CreateDirectory(drctry);
                    var benchs = await manager.GetResults(experiment.ID);
                    var benchsVm = benchs.Benchmarks.Select(e => new BenchmarkResultViewModel(e, uiService)).ToArray();
                    total = benchsVm.Length;

                    for (int i = 0; i < total; i++)
                    {
                        UTF8Encoding enc = new UTF8Encoding();
                        string stdout = await benchsVm[i].GetStdOutAsync(false);
                        string stderr = await benchsVm[i].GetStdErrAsync(false);
                        string path = drctry + @"\" + experiment.Category + @"\" + benchsVm[i].Filename;
                        path = path.Replace("/", @"\");
                        Directory.CreateDirectory(path.Substring(0, path.LastIndexOf(@"\")));
                        if (stdout != null && stdout.Length > 0)
                        {
                            FileStream stdoutf = File.Open(path + ".out.txt", FileMode.OpenOrCreate);
                            stdoutf.Write(enc.GetBytes(stdout), 0, enc.GetByteCount(stdout));
                            stdoutf.Close();
                        }

                        if (stderr != null && stderr.Length > 0)
                        {
                            FileStream stderrf = File.Open(path + ".err.txt", FileMode.OpenOrCreate);
                            stderrf.Write(enc.GetBytes(stderr), 0, enc.GetByteCount(stderr));
                            stderrf.Close();
                        }
                    }
                });
                uiService.ShowInfo("Output saved to " + drctry);
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex, "Failed to save output");
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }


        }
        private static void MakeMatrix(ExperimentStatusViewModel[] experiments, ExperimentResults[] b, StreamWriter f, int condition, string name)
        {
            int numItems = experiments.Length;
            f.WriteLine(@"\begin{table}");
            f.WriteLine(@"  \centering");
            f.Write(@"  \begin{tabular}[h]{|p{.5\textwidth}|");
            for (int i = 0; i < numItems; i++)
                f.Write(@"c|");
            f.WriteLine(@"}\cline{2-" + (numItems + 1) + "}");

            // Header line
            f.Write(@"    \multicolumn{1}{c|}{}");
            for (int i = 0; i < numItems; i++)
            {
                string label = experiments[i].Note.Replace(@"\", @"\textbackslash ").Replace(@"_", @"\_");
                if (label.Length < 40) f.Write(@" & \multicolumn{1}{l|}{\rotatebox[origin=c]{90}{" + label + @"}}");
                else f.Write(@" & \multicolumn{1}{l|}{\rotatebox[origin=c]{90}{\parbox{.5\textwidth}{" + label + @"}}}");
            }
            f.WriteLine(@"\\\hline\hline");


            int example_value = 0;

            for (int i = 0; i < numItems; i++)
            {
                string label = experiments[i].Note.Replace(@"\", @"\textbackslash ").Replace(@"_", @"\_");
                f.Write(@"    " + label);
                for (int j = 0; j < numItems; j++)
                {
                    if (i == j)
                        f.Write(@" & $\pm 0$");
                    else
                    {
                        int q = FindSimilarBenchmarks(b[i].Benchmarks, b[j].Benchmarks, condition, false);
                        f.Write(@" & $" + (q > 0 ? @"+" : (q == 0) ? @"\pm" : @"") + q.ToString() + "$");
                        if (i == 1 && j == 0) example_value = q;
                    }
                }

                f.WriteLine(@"\\\hline");
            }

            f.WriteLine(@"  \end{tabular}");
            f.Write(@"  \caption{\label{tbl:mtrx} " + name + " Matrix. ");
            string label1 = experiments[1].Note.Replace(@"\", @"\textbackslash ").Replace(@"_", @"\_");
            string label0 = experiments[0].Note.Replace(@"\", @"\textbackslash ").Replace(@"_", @"\_");
            f.Write(@"For instance, '" + label1 + "' outperforms '" + label0 + "' on " + example_value + " benchmarks. ");
            f.WriteLine(@"}");
            f.WriteLine(@"\end{table}");
        }
        public static async void SaveMatrix(string filename, ExperimentStatusViewModel[] experiments, ExperimentManager manager, IUIService uiService)
        {
            if (filename == null) throw new ArgumentNullException("filename");
            if (experiments == null) throw new ArgumentNullException("experiments");
            if (manager == null) throw new ArgumentNullException("manager");
            if (uiService == null) throw new ArgumentNullException("uiService");
            var handle = uiService.StartIndicateLongOperation("Save tex...");
            try
            {
                using (StreamWriter f = new StreamWriter(filename, false))
                {
                    f.WriteLine("% -*- mode: latex; TeX-master: \"main.tex\"; -*-");
                    f.WriteLine();
                    f.WriteLine(@"\documentclass{article}");
                    f.WriteLine(@"\usepackage{multirow}");
                    f.WriteLine(@"\usepackage{rotating}");
                    f.WriteLine(@"\begin{document}");
                    int count = experiments.Length;
                    ExperimentResults[] b = new ExperimentResults[count];
                    b = await DownloadResultsAsync(experiments, manager);

                    MakeMatrix(experiments, b, f, 0, "SAT+UNSAT");
                    MakeMatrix(experiments, b, f, 1, "SAT");
                    MakeMatrix(experiments, b, f, 2, "UNSAT");

                    f.WriteLine(@"\end{document}");
                }
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex, "Failed to save matrix");
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
        }
        private static async Task<ExperimentResults[]> DownloadResultsAsync(ExperimentStatusViewModel[] experiments, ExperimentManager manager)
        {
            var count = experiments.Length;
            var t = new Task<ExperimentResults>[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                t[index] = Task.Run(() => manager.GetResults(experiments[index].ID));
            }
            var b = new ExperimentResults[count];
            for (int j = 0; j < count; j++)
            {
                int index = j;
                b[index] = await t[index];
            }
            return b;
        }
        private static bool ConditionTrue(int condition, BenchmarkResult elem1, BenchmarkResult elem2, bool isEqual_ij)
        {
            bool condition1 = elem1.BenchmarkFileName == elem2.BenchmarkFileName &&
                              (elem1.ExitCode == 0 && elem2.ExitCode != 0 ||
                               elem1.Status == ResultStatus.Success && elem2.Status == ResultStatus.Success && elem1.NormalizedRuntime < elem2.NormalizedRuntime);

            int sat1 = int.Parse(elem1.Properties[Z3Domain.KeySat], CultureInfo.InvariantCulture);
            int unsat1 = int.Parse(elem1.Properties[Z3Domain.KeyUnsat], CultureInfo.InvariantCulture);
            int unk1 = int.Parse(elem1.Properties[Z3Domain.KeyUnknown], CultureInfo.InvariantCulture);

            int sat2 = int.Parse(elem2.Properties[Z3Domain.KeySat], CultureInfo.InvariantCulture);
            int unsat2 = int.Parse(elem2.Properties[Z3Domain.KeyUnsat], CultureInfo.InvariantCulture);
            int unk2 = int.Parse(elem2.Properties[Z3Domain.KeyUnknown], CultureInfo.InvariantCulture);

            if (condition == 0) condition1 = condition1 && (unsat1 + unsat2 > 0 || sat1 + sat2 > 0);
            if (condition == 1) condition1 = condition1 && (sat1 + sat2 > 0);
            if (condition == 2) condition1 = condition1 && (unsat1 + unsat2 > 0);

            return condition1;
        }
        private static int FindSimilarBenchmarks (BenchmarkResult[] br1, BenchmarkResult[] br2, int condition, bool isEqual_ij)
        {
            int result = 0;

            for (int i1 = 0, i2 = 0; i1 < br1.Length && i2 < br2.Length;)
            {
                string filename1 = br1[i1].BenchmarkFileName;
                string filename2 = br2[i2].BenchmarkFileName;

                int cmp = string.Compare(filename1, filename2);
                if (cmp == 0)
                {
                    if (ConditionTrue(condition, br1[i1], br2[i2], isEqual_ij)) result++;
                    i1++; i2++;
                }
                else if (cmp < 0) // ~ r1 < r2
                {
                    i1++;
                }
                else // ~ r1 > r2
                {
                    i2++;
                }
            }
            return result;
        }

    }
}
