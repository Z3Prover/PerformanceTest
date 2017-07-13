using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest
{

    public class ExperimentEntity
    {
        public int ID { get; set; }
        public DateTime Submitted { get; set; }
        public string Executable { get; set; }
        public string DomainName { get; set; }
        public string Parameters { get; set; }
        public string BenchmarkContainerUri { get; set; }
        public string BenchmarkDirectory { get; set; }
        public string Category { get; set; }
        public string BenchmarkFileExtension { get; set; }
        /// <summary>
        /// MegaBytes.
        /// </summary>
        public double MemoryLimitMB { get; set; }
        /// <summary>
        /// Seconds.
        /// </summary>
        public double BenchmarkTimeout { get; set; }
        /// <summary>
        /// Seconds.
        /// </summary>
        public double ExperimentTimeout { get; set; }
        public string Note { get; set; }
        public string Creator { get; set; }
        public bool Flag { get; set; }
        public string GroupName { get; set; }
    }
}
