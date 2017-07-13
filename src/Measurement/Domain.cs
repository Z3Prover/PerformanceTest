using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Measurement
{
    public abstract class Domain
    {
        private static Domain defaultDomain = new DefaultDomain();
        public static Domain Default { get { return defaultDomain; } }

        private readonly string name;

        protected Domain(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentException("name is null or empty", "name");
            this.name = name;
        }

        /// <summary>
        /// Gets name of the domain.
        /// </summary>
        public string Name { get { return name; } }

        /// <summary>
        /// Returns a collection of extensions that are supported by the program.
        /// </summary>
        public virtual string[] BenchmarkExtensions { get { return new string[0]; } }

        /// <summary>
        /// Returns default command line parameters.
        /// </summary>
        public virtual string CommandLineParameters { get { return ""; } }

        /// <summary>
        /// Adds an input file name to the command line parameters in accordance with the 
        /// program command line syntax.
        /// </summary>
        public virtual string AddFileNameArgument(string parameters, string fileName)
        {
            return string.Format("{0} {1}", parameters, fileName);
        }

        /// <summary>
        /// Determines status of the process run result and its custom domain-specific properties.
        /// </summary>
        public abstract ProcessRunAnalysis Analyze(string inputFile, ProcessRunMeasure measure);

        /// <summary>
        /// Builds aggregated statistics for a collection of results of process runs.
        /// </summary>
        public AggregatedAnalysis Aggregate(IEnumerable<ProcessRunResults> runs)
        {
            var results = runs.ToArray();
            int bugs = 0, errors = 0, infrErrors = 0, timeouts = 0, memouts = 0;
            for (int i = 0; i < results.Length; i++)
            {
                var result = results[i].Analysis;
                switch (result.Status)
                {
                    case ResultStatus.OutOfMemory:
                        memouts++;
                        break;
                    case ResultStatus.Timeout:
                        timeouts++;
                        break;
                    case ResultStatus.Error:                        
                        errors++;
                        break;
                    case ResultStatus.InfrastructureError:
                        infrErrors++;
                        break;
                    case ResultStatus.Bug:
                        bugs++;
                        break;
                    default:
                        break;
                }
            }

            var props = AggregateProperties(results);
            return new AggregatedAnalysis(bugs, errors, infrErrors, timeouts, memouts, props, results.Length);
        }


        /// <summary>
        /// Allows to provide domain-specific properties for the aggregated statistics of multiple process runs.
        /// </summary>
        protected abstract IReadOnlyDictionary<string, string> AggregateProperties(IEnumerable<ProcessRunResults> results);

        /// <summary>
        /// Returns a boolean value that determines of the given process run result 
        /// can participate in the best result challenge.
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public virtual bool CanConsiderAsRecord(ProcessRunAnalysis result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return result.Status == ResultStatus.Success;
        }

        /// <summary>
        /// Returns tags that are applicable to the process run result which
        /// then can be used in UI application to distinguish this result.
        /// </summary>
        /// <param name="result">Result of a process run for an input file.</param>
        public virtual string[] GetTags(ProcessRunAnalysis result)
        {
            return new string[0];
        }
    }



    public sealed class DefaultDomain : Domain
    {
        public DefaultDomain() : base("default")
        {

        }

        public override ProcessRunAnalysis Analyze(string inputFile, ProcessRunMeasure measure)
        {
            ResultStatus status;
            switch (measure.Limits)
            {
                case Measure.LimitsStatus.WithinLimits:
                    status = measure.ExitCode == 0 ? ResultStatus.Success : ResultStatus.Error;
                    break;
                case Measure.LimitsStatus.MemoryOut:
                    status = ResultStatus.OutOfMemory;
                    break;
                case Measure.LimitsStatus.TimeOut:
                    status = ResultStatus.Timeout;
                    break;
                default:
                    throw new NotSupportedException("Unknown status");
            }

            return new ProcessRunAnalysis(status, new Dictionary<string, string>());
        }

        protected override IReadOnlyDictionary<string, string> AggregateProperties(IEnumerable<ProcessRunResults> results)
        {
            return new Dictionary<string, string>();
        }
    }


    public class ProcessRunResults
    {
        private readonly ProcessRunAnalysis analysis;
        private readonly double runtime;

        public ProcessRunResults(ProcessRunAnalysis analysis, double runtime)
        {
            this.analysis = analysis;
            this.runtime = runtime;
        }

        public ProcessRunAnalysis Analysis { get { return analysis; } }
        public double Runtime { get { return runtime; } }
    }

    public class ProcessRunAnalysis
    {
        private readonly ResultStatus status;
        private readonly IReadOnlyDictionary<string, string> outputProperties;

        public ProcessRunAnalysis(ResultStatus status, IReadOnlyDictionary<string, string> outputProperties)
        {
            this.status = status;
            this.outputProperties = outputProperties;
        }


        public ResultStatus Status { get { return status; } }
        public IReadOnlyDictionary<string, string> OutputProperties { get { return outputProperties; } }
    }

    public class AggregatedAnalysis
    {
        public AggregatedAnalysis(int bugs, int errors, int infrastructureErrors, int timeouts, int memouts, IReadOnlyDictionary<string, string> props, int files)
        {
            Bugs = bugs;
            Errors = errors;
            Timeouts = timeouts;
            MemoryOuts = memouts;
            Properties = props;
            InfrastructureErrors = infrastructureErrors;
            Files = files;
        }

        public int Bugs { get; private set; }

        public int Errors { get; private set; }

        public int InfrastructureErrors { get; private set; }

        public int Timeouts { get; private set; }

        public int MemoryOuts { get; private set; }

        public int Files { get; private set; }


        public IReadOnlyDictionary<string, string> Properties { get; private set; }
    }


    public enum ResultStatus
    {
        Success,
        OutOfMemory,
        Timeout,
        Error,
        InfrastructureError,
        Bug
    }
}
