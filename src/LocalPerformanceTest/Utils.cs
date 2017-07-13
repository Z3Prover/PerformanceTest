using Measurement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PerformanceTest
{
    public static class Utils
    {
        public static Stream StringToStream(string s)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(s == null ? string.Empty : s);
            MemoryStream stream = new MemoryStream(byteArray);
            stream.Position = 0;
            return stream;
        }

        public static string StreamToString(Stream s, bool resetPosition)
        {
            long pos = resetPosition ? s.Position : 0;
            StreamReader r = new StreamReader(s, Encoding.UTF8);
            string str = r.ReadToEnd();
            if (resetPosition)
                s.Position = pos;
            return str;
        }

        public static T Median<T>(T[] data, Func<T,T,T> mean)
        {
            int len = data.Length;
            Array.Sort(data);
            int im = len >> 1;
            T m;
            if (len % 2 == 1)
                m = data[im];
            else
                m = mean (data[im], data[im - 1]);
            return m;
        }

        public static double Median(double[] data)
        {
            return Median(data, (x,y) => 0.5*(x+y));
        }

        public static ProcessRunMeasure AggregateMeasures(ProcessRunMeasure[] measures)
        {
            if (measures.Length == 0) throw new ArgumentException("measures", "At least one element expected");
            if (measures.Length == 1) return measures[0];
            int? exitCode = measures[0].ExitCode;
            foreach(ProcessRunMeasure m in measures)
            {
                if (m.Limits != Measure.LimitsStatus.WithinLimits || m.ExitCode != exitCode) return m;
            }

            TimeSpan totalProcessorTime = Median(measures.Select(m => m.TotalProcessorTime).ToArray(), (t1, t2) => TimeSpan.FromTicks((t1 + t2).Ticks >> 1));
            TimeSpan wallClockTime = Median(measures.Select(m => m.WallClockTime).ToArray(), (t1, t2) => TimeSpan.FromTicks((t1 + t2).Ticks >> 1));
            double peakMemorySize = measures.Select(m => m.PeakMemorySizeMB).Max();

            return new ProcessRunMeasure(
                totalProcessorTime,
                wallClockTime,
                peakMemorySize,
                measures[0].Limits,
                exitCode,
                measures[0].StdOut,
                measures[0].StdErr);
        }



        public static String MakeRelativePath(String baseFolder, String toPath)
        {
            if (String.IsNullOrEmpty(baseFolder)) throw new ArgumentNullException("baseFolder");
            if (String.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");

            if (!baseFolder.EndsWith(Path.DirectorySeparatorChar.ToString()) && !baseFolder.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                baseFolder += Path.DirectorySeparatorChar;

            Uri fromUri = new Uri(EnsureAbsolutePath(baseFolder));
            Uri toUri = new Uri(EnsureAbsolutePath(toPath));

            if (fromUri.Scheme != toUri.Scheme) { return toPath; } // path can't be made relative.

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            String relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        private static string EnsureAbsolutePath(String path)
        {
            return Path.GetFullPath(path);
        }
    }
}
