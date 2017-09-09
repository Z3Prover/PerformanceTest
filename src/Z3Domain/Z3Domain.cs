using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using static Measurement.Measure;
using System.Globalization;

namespace Measurement
{
    [Export(typeof(Domain))]
    public class Z3Domain : Domain
    {
        public const string KeySat = "SAT";
        public const string KeyUnsat = "UNSAT";
        public const string KeyUnknown = "UNKNOWN";
        public const string KeyOverperformed = "OVERPERF";
        public const string KeyUnderperformed = "UNDERPERF";
        public const string KeyTimeSat = "SATTIME";
        public const string KeyTimeUnsat = "UNSATTIME";
        public const string KeyTargetSat = "TargetSAT";
        public const string KeyTargetUnsat = "TargetUNSAT";
        public const string KeyTargetUnknown = "TargetUNKNOWN";
        public const string TagUnderperformers = "UNDERPERFORMERS";

        public Z3Domain() : base("Z3")
        {
        }

        public override string[] BenchmarkExtensions
        {
            get
            {
                return new[] { "smt2", "smt" };
            }
        }

        public override string CommandLineParameters
        {
            get
            {
                return "-smt2 -file:{0}";
            }
        }

        public override string AddFileNameArgument(string parameters, string fileName)
        {
            return string.Format("{0} -file:{1}", parameters, fileName);
        }

        public override string Preprocess(string binary, string parameters)
        {
            string input_file = "";
            Regex input_rx = new Regex("-file:(([^\" ]+)|(\"([^\"]+)\"))");
            Match m = input_rx.Match(parameters);

            if (m.Success)
                input_file = m.Groups[1].Value;
            else
                return parameters;

            string new_cs = "";
            Regex rcs_rx = new Regex("replace-check-sat=\"([^\"]+)\"");

            m = rcs_rx.Match(parameters);
            if (m.Success)
            {
                string res = parameters;
                new_cs = m.Groups[1].Value;
                res = parameters.Replace("replace-check-sat=\"" + new_cs + "\"", "");

                string tmpf = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                using (FileStream f = new FileStream(input_file, FileMode.Open, FileAccess.Read))
                using (FileStream ft = new FileStream(tmpf, FileMode.Create, FileAccess.Write))
                using (StreamReader fr = new StreamReader(f))
                using (StreamWriter ftw = new StreamWriter(ft))
                    while (!fr.EndOfStream)
                    {
                        string s = fr.ReadLine();
                        ftw.WriteLine(s.Replace("(check-sat)", new_cs));
                    }

                File.Delete(input_file);
                File.Move(tmpf, input_file);
                return res;
            }
            else
                return parameters;
        }

        public override ProcessRunAnalysis Analyze(string inputFile, ProcessRunMeasure measure)
        {
            if (!measure.StdOut.CanSeek) throw new NotSupportedException("Standard output stream doesn't support seeking");
            if (!measure.StdErr.CanSeek) throw new NotSupportedException("Standard error stream doesn't support seeking");
            measure.StdOut.Position = 0L;
            measure.StdErr.Position = 0L;

            Counts countsTargets;
            try
            {
                using (Stream input = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
                {
                    countsTargets = CountInput(input);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to read input file: " + ex);
                countsTargets = new Counts();
            }
            Counts countsResults = CountResults(measure.StdOut);

            int? exitCode = measure.ExitCode;
            LimitsStatus limits = measure.Limits;
            ResultStatus status;

            if (limits == LimitsStatus.TimeOut)
                status = ResultStatus.Timeout;
            else if (limits == LimitsStatus.MemoryOut || exitCode == 101)
                status = ResultStatus.OutOfMemory;
            else if (exitCode == 0)
            {
                if (countsResults.sat == 0 && countsResults.unsat == 0 && countsResults.other == 0)
                    status = ResultStatus.Error;
                else
                    status = ResultStatus.Success;
            }
            else
            {
                status = GetBugCode(measure);
            }

            return new ProcessRunAnalysis(status,
                new Dictionary<string, string>
                {
                    { KeySat, countsResults.sat.ToString() },
                    { KeyUnsat, countsResults.unsat.ToString() },
                    { KeyUnknown, countsResults.other.ToString() },

                    { KeyTargetSat, countsTargets.sat.ToString() },
                    { KeyTargetUnsat, countsTargets.unsat.ToString() },
                    { KeyTargetUnknown, countsTargets.other.ToString() }
                });
        }

        protected override IReadOnlyDictionary<string, string> AggregateProperties(IEnumerable<ProcessRunResults> results)
        {
            int sat = 0, unsat = 0, unknown = 0, overPerf = 0, underPerf = 0, tsat = 0, tunsat = 0, tunk = 0;
            double timeSat = 0.0, timeUnsat = 0.0;

            foreach (ProcessRunResults result in results)
            {
                ProcessRunAnalysis analysis = result.Analysis;
                int _sat = int.Parse(analysis.OutputProperties[KeySat], CultureInfo.InvariantCulture);
                int _unsat = int.Parse(analysis.OutputProperties[KeyUnsat], CultureInfo.InvariantCulture);
                int _unk = int.Parse(analysis.OutputProperties[KeyUnknown], CultureInfo.InvariantCulture);
                int _tsat = int.Parse(analysis.OutputProperties[KeyTargetSat], CultureInfo.InvariantCulture);
                int _tunsat = int.Parse(analysis.OutputProperties[KeyTargetUnsat], CultureInfo.InvariantCulture);
                int _tunk = int.Parse(analysis.OutputProperties[KeyTargetUnknown], CultureInfo.InvariantCulture);

                if (analysis.Status != ResultStatus.Bug)
                {
                    sat += _sat;
                    unsat += _unsat;
                    unknown += _unk;

                    tsat += _tsat;
                    tunsat += _tunsat;
                    tunk += _tunk;

                    if (_sat > 0) timeSat += result.Runtime;
                    if (_unsat > 0) timeUnsat += result.Runtime;
                }

                if (analysis.Status == ResultStatus.Success && _sat + _unsat > _tsat + _tunsat && _unk < _tunk)
                    overPerf++;
                if (_sat + _unsat < _tsat + _tunsat || _unk > _tunk)
                    underPerf++;
            }
            return new Dictionary<string, string>
                {
                    { KeySat, sat.ToString() },
                    { KeyUnsat, unsat.ToString() },
                    { KeyUnknown, unknown.ToString() },
                    { KeyOverperformed, overPerf.ToString() },
                    { KeyUnderperformed, underPerf.ToString() },
                    { KeyTimeSat, timeSat.ToString() },
                    { KeyTimeUnsat, timeUnsat.ToString() },
                    { KeyTargetSat, tsat.ToString() },
                    { KeyTargetUnsat, tunsat.ToString() },
                    { KeyTargetUnknown, tunk.ToString() },
                };
        }

        public override bool CanConsiderAsRecord(ProcessRunAnalysis result)
        {
            int _sat = int.Parse(result.OutputProperties[KeySat], CultureInfo.InvariantCulture);
            int _unsat = int.Parse(result.OutputProperties[KeyUnsat], CultureInfo.InvariantCulture);
            int _unk = int.Parse(result.OutputProperties[KeyUnknown], CultureInfo.InvariantCulture);

            return result.Status == ResultStatus.Success &&
                _unk == 0 &&
                _sat + _unsat > 0;
        }

        public override string[] GetTags(ProcessRunAnalysis result)
        {
            int _sat = int.Parse(result.OutputProperties[KeySat], CultureInfo.InvariantCulture);
            int _unsat = int.Parse(result.OutputProperties[KeyUnsat], CultureInfo.InvariantCulture);
            int _unk = int.Parse(result.OutputProperties[KeyUnknown], CultureInfo.InvariantCulture);
            int _tsat = int.Parse(result.OutputProperties[KeyTargetSat], CultureInfo.InvariantCulture);
            int _tunsat = int.Parse(result.OutputProperties[KeyTargetUnsat], CultureInfo.InvariantCulture);
            int _tunk = int.Parse(result.OutputProperties[KeyTargetUnknown], CultureInfo.InvariantCulture);

            if (_sat + _unsat < _tsat + _tunsat || _unk > _tunk) return new string[] { TagUnderperformers };
            return new string[0];
        }

        public override string FailureFilter()
        {
            return "(state eq 'completed') and (executionInfo/exitCode ne 0)";
        }

        private ResultStatus GetBugCode(ProcessRunMeasure measure)
        {
            ResultStatus status = ResultStatus.Error; // no bug found means general error.

            StreamReader reader = new StreamReader(measure.StdErr);
            while (!reader.EndOfStream)
            {
                string l = reader.ReadLine();
                if (l.StartsWith("(error") && l.Contains("check annotation"))
                {
                    status = ResultStatus.Bug;
                    break;
                }
            }
            measure.StdErr.Position = 0L;

            if (status == ResultStatus.Error)
            {
                reader = new StreamReader(measure.StdOut);
                while (!reader.EndOfStream)
                {
                    string l = reader.ReadLine();
                    if (l.StartsWith("(error") && l.Contains("check annotation"))
                    {
                        status = ResultStatus.Bug;
                        break;
                    }
                    else if (l.StartsWith("(error \"out of memory\")"))
                    {
                        status = ResultStatus.OutOfMemory;
                        break;
                    }
                }
                measure.StdOut.Position = 0L;
            }

            return status;
        }

        private static Counts CountResults(Stream output)
        {
            Counts res = new Counts();
            StreamReader reader = new StreamReader(output);
            while (!reader.EndOfStream)
            {
                string l = reader.ReadLine(); // does not contain \r\n
                l.TrimEnd(' ');
                if (l == "sat" || l == "SAT" || l == "SATISFIABLE" || l == "s SATISFIABLE" || l == "SuccessfulRuns = 1") // || l == "VERIFICATION FAILED")
                    res.sat++;
                else if (l == "unsat" || l == "UNSAT" || l == "UNSATISFIABLE" || l == "s UNSATISFIABLE") // || l == "VERIFICATION SUCCESSFUL")
                    res.unsat++;
                else if (l == "unknown" || l == "UNKNOWN" || l == "INDETERMINATE")
                    res.other++;
            }
            output.Position = 0L;
            return res;
        }

        private static Counts CountInput(Stream input)
        {
            Counts res = new Counts();
            StreamReader r = new StreamReader(input);
            while (!r.EndOfStream)
            {
                string l = r.ReadLine(); // does not contain \r\n
                if (l.StartsWith("(set-info :status sat)"))
                    res.sat++;
                else if (l.StartsWith("(set-info :status unsat)"))
                    res.unsat++;
                else if (l.StartsWith("(set-info :status"))
                    res.other++;
            }
            return res;
        }

        struct Counts
        {
            public int sat;
            public int unsat;
            public int other;
        }
    }
}
