using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public class ShowOutputViewModel
    {
        private readonly int id;
        private readonly string filename, stdOut, stdErr;
        public ShowOutputViewModel(int id, string filename, string stdOut, string stdErr)
        {
            this.id = id;
            this.filename = filename;
            this.stdOut = stdOut;
            this.stdErr = stdErr;
        }

        public string Title
        {
            get { return "Experiment" + id.ToString(); }
        }
        public string Filename
        {
            get { return filename; }
        }
        public string StdOut
        {
            get { return stdOut; }
        }
        public string StdErr
        {
            get { return stdErr; }
        }
    }
}
