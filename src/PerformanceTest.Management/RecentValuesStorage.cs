using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public sealed class RecentValuesStorage
    {
        const string userRoot = "HKEY_CURRENT_USER";
        const string subkey = "PerformanceTest.Management";
        const string keyName = userRoot + "\\" + subkey;

        public RecentValuesStorage()
        {
        }

        public bool ShowProgress
        {
            get { return ReadBool("ShowProgress"); }
            set { WriteBool("ShowProgress", value); }
        }
        public bool ResolveTimeoutDupes
        {
            get { return ReadBool("ResolveTimeoutDupes"); }
            set { WriteBool("ResolveTimeoutDupes", value); }
        }
        public bool ResolveSameTimeDupes
        {
            get { return ReadBool("ResolveSameTimeDupes"); }
            set { WriteBool("ResolveSameTimeDupes", value); }
        }
        public bool ResolveSlowestDupes
        {
            get { return ReadBool("ResolveSlowestDupes"); }
            set { WriteBool("ResolveSlowestDupes", value); }
        }
        public bool ResolveInErrorsDupes
        {
            get { return ReadBool("ResolveInErrorsDupes"); }
            set { WriteBool("ResolveInErrorsDupes", value); }
        }
        public string ConnectionString
        {
            get { return ReadString("ConnectionString"); }
            set { WriteString("ConnectionString", value); }
        }

        public string BenchmarkDirectory
        {
            get { return ReadString("BenchmarkDirectory"); }
            set { WriteString("BenchmarkDirectory", value); }
        }

        public string BenchmarkCategories
        {
            get { return ReadString("BenchmarkCategories"); }
            set { WriteString("BenchmarkCategories", value); }
        }

        public string BenchmarkExtension
        {
            get { return ReadString("BenchmarkExtension"); }
            set { WriteString("BenchmarkExtension", value); }
        }

        public string ExperimentExecutableParameters
        {
            get { return ReadString("ExperimentExecutableParameters"); }
            set { WriteString("ExperimentExecutableParameters", value); }
        }
        public bool AllowAdaptiveRuns
        {
            get { return ReadBool("AllowAdaptiveRuns"); }
            set { WriteBool("AllowAdaptiveRuns", value); }
        }

        public int MaxRepetitions
        {
            get
            {
                var reps = ReadInt("MaxRepetitions");
                return reps <= 0 ? 10 : reps;
            }
            set { WriteInt("MaxRepetitions", value); }
        }

        public double MaxTimeForAdaptiveRuns
        {
            get
            {
                double time = ReadDouble("MaxTimeForAdaptiveRuns");
                return Double.IsNaN(time) ? 10.0 : time;
            }
            set
            {
                WriteDouble("MaxTimeForAdaptiveRuns", value);
            }
        }

        public double BenchmarkMemoryLimit
        {
            get
            {
                double mem = ReadDouble("BenchmarkMemoryLimit");
                return Double.IsNaN(mem) ? 0 : mem;
            }
            set
            {
                WriteDouble("BenchmarkMemoryLimit", value);
            }
        }

        public TimeSpan BenchmarkTimeLimit
        {
            get
            {
                double sec = ReadDouble("BenchmarkTimeLimit");
                return Double.IsNaN(sec) ? TimeSpan.FromSeconds(0) : TimeSpan.FromSeconds(sec);
            }
            set
            {
                WriteDouble("BenchmarkTimeLimit", value.TotalSeconds);
            }
        }
        public TimeSpan ExperimentTimeLimit
        {
            get
            {
                double sec = ReadDouble("ExperimentTimeLimit");
                return Double.IsNaN(sec) ? TimeSpan.FromSeconds(0) : TimeSpan.FromSeconds(sec);
            }
            set
            {
                WriteDouble("ExperimentTimeLimit", value.TotalSeconds);
            }
        }
        public string ExperimentNote
        {
            get { return ReadString("ExperimentNote"); }
            set { WriteString("ExperimentNote", value); }
        }

        public string BatchPool
        {
            get { return ReadString("BatchPool"); }
            set { WriteString("BatchPool", value); }
        }



        private void WriteBool(string key, bool value)
        {
            Registry.SetValue(keyName, key, value ? 1 : 0, RegistryValueKind.DWord);
        }

        private bool ReadBool(string key)
        {
            var val = Registry.GetValue(keyName, key, 0);
            return val is int && (int)val == 1;
        }

        private string ReadString(string key)
        {
            return Registry.GetValue(keyName, key, "") as string;
        }

        private void WriteString(string key, string value)
        {
            if (value == null) value = "";
            Registry.SetValue(keyName, key, value, RegistryValueKind.String);
        }



        private int ReadInt(string key)
        {
            object obj = Registry.GetValue(keyName, key, "");
            if (obj is int) return (int)obj;
            return 0;
        }

        private void WriteInt(string key, int value)
        {
            Registry.SetValue(keyName, key, value, RegistryValueKind.DWord);
        }


        private double ReadDouble(string key)
        {
            string s = ReadString(key);
            if (s == null) return double.NaN;
            double d;
            if (!double.TryParse(s, out d)) return double.NaN;
            return d;
        }

        private void WriteDouble(string key, double value)
        {
            WriteString(key, value.ToString());
        }
    }
}
