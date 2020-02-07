using System;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading;
using Ionic.Zip;

namespace Measurement
{
    public static class ProcessMeasurer
    {
        /// <summary>
        /// Starts and measures performance of an executable file or a script.
        /// </summary>
        /// <param name="fileName">An executable file name or cmd file or bat or zip file.</param>
        /// <param name="arguments"></param>
        /// <param name="timeout"></param>
        /// <param name="memoryLimit">Maximum allowed memory use for the process (megabytes). Zero means unlimited memory use.</param>
        /// <param name="outputLimit">Maximum length of the process standard output stream (characters).</param>
        /// <param name="errorLimit">Maximum length of the process standard error stream (characters).</param>
        /// <returns></returns>
        public static ProcessRunMeasure Measure(string fileName, string arguments, TimeSpan timeout, double? memoryLimit = 0, long? outputLimit = null, long? errorLimit = null, Domain domain = null)
        {
            var stdOut = new MemoryStream();
            var stdErr = new MemoryStream();
            StreamWriter out_writer = new StreamWriter(stdOut);
            StreamWriter err_writer = new StreamWriter(stdErr);
            long out_lim = outputLimit ?? long.MaxValue;
            long err_lim = errorLimit ?? long.MaxValue;

            bool isZip = ZipFile.IsZipFile(fileName);

            string localFileName = null, tempFolder = null;
            if (isZip)
            {
                tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempFolder);
                localFileName = ExecutablePackage.ExtractZip(fileName, tempFolder);
            }
            else
                localFileName = fileName;

            if (domain != null)
                arguments = domain.Preprocess(localFileName, arguments);

            Console.Error.WriteLine("Running {0} {1}", localFileName, arguments);

            Process p = StartProcess(localFileName, arguments,
                output => WriteToStream(output, out_writer, ref out_lim),
                error => WriteToStream(error, err_writer, ref err_lim));

            long maxmem = 0L;
            if (!memoryLimit.HasValue)
                memoryLimit = 0;
            long memLimitBytes = (long)Math.Floor(memoryLimit.Value * 1024 * 1024);
            bool exhausted_time = false, exhausted_memory = false;

            try
            {
                do
                {
                    p.Refresh();
                    if (!p.HasExited)
                    {
                        long m = Memory(p);
                        maxmem = Math.Max(maxmem, m);

                        TimeSpan wc = DateTime.Now - p.StartTime;
                        if (wc >= timeout)
                        {
                            Trace.WriteLine("Process timed out; killing.");
                            exhausted_time = true;
                            Kill(p);
                        }
                        else if (memLimitBytes > 0 && m > memLimitBytes)
                        {
                            Trace.WriteLine("Process uses too much memory (" + m + " bytes); killing.");
                            exhausted_memory = true;
                            Kill(p);
                        }
                        else if (out_lim <= 0 || err_lim <= 0)
                        {
                            Trace.WriteLine("Process produced too much output; killing.");
                            exhausted_memory = true;
                            out_writer.WriteLine("\n---OUTPUT TRUNCATED---");
                            err_writer.WriteLine("\n---OUTPUT TRUNCATED---");
                            Kill(p);
                        }
                    }
                }
                while (!p.WaitForExit(500));
            }
            catch (InvalidOperationException ex)
            {
                Trace.WriteLine("Invalid Operation: " + ex.Message);
                Trace.WriteLine("Assuming process has ended.");
            }

            p.WaitForExit();

            maxmem = Math.Max(maxmem, Memory(p));
            TimeSpan processorTime = exhausted_time ? timeout : p.TotalProcessorTime;
            TimeSpan wallClockTime = exhausted_time ? timeout : (DateTime.Now - p.StartTime);
            int processExitCode = p.ExitCode;

            p.Close();

            Thread.Sleep(500); // Give the asynch stdout/stderr events a chance to finish.
            out_writer.Flush();
            err_writer.Flush();

            stdOut.Seek(0, SeekOrigin.Begin);
            stdErr.Seek(0, SeekOrigin.Begin);

            if (isZip)
            {
                try
                {
                    Directory.Delete(tempFolder, true);
                }
                catch (IOException)
                {
                    /* OK */
                }
                catch (UnauthorizedAccessException)
                {
                    /* OK */
                }
            }

            var status =
                exhausted_time ?
                    Measurement.Measure.LimitsStatus.TimeOut :
                        (exhausted_memory || processExitCode == -1073741571) ?
                            Measurement.Measure.LimitsStatus.MemoryOut : // .NET StackOverflowException
                            Measurement.Measure.LimitsStatus.WithinLimits;

            int? exitCode;
            if (status == Measurement.Measure.LimitsStatus.MemoryOut || status == Measurement.Measure.LimitsStatus.TimeOut)
                exitCode = null;
            else
                exitCode = processExitCode;

            return new ProcessRunMeasure(
                processorTime,
                wallClockTime,
                (maxmem / 1024.0 / 1024.0),
                status,
                exitCode,
                stdOut,
                stdErr);
        }


        private static Process StartProcess(string fileName, string arguments, Action<string> stdOut, Action<string> stdErr)
        {
            Process p = new Process();
            p.StartInfo.FileName = fileName;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.UseShellExecute = false;
            p.OutputDataReceived += (sender, args) => { if (args != null && args.Data != null) stdOut(args.Data); };
            p.ErrorDataReceived += (sender, args) => { if (args != null && args.Data != null) stdErr(args.Data); };

            if (fileName.EndsWith(".cmd") || fileName.EndsWith(".bat"))
            {
                p.StartInfo.FileName = System.IO.Path.Combine(Environment.SystemDirectory, "cmd.exe");
                p.StartInfo.Arguments = "/c " + fileName + " " + p.StartInfo.Arguments;
            }

            bool retry;
            do
            {
                retry = false;
                try
                {
                    p.Start();
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    if (ex.Message == "The process cannot access the file because it is being used by another process")
                    {
                        Trace.WriteLine("Retrying to execute command...");
                        Thread.Sleep(500);
                        retry = true;
                    }
                    else
                        throw ex;
                }
            } while (retry);

            try
            {
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                int pn = Environment.ProcessorCount;
                if (pn > 1)
                {
                    p.ProcessorAffinity = (IntPtr)((1L << pn) - 1L);
                }
                p.PriorityClass = ProcessPriorityClass.RealTime;
            }
            catch (InvalidOperationException ex)
            {
                if (!(ex.Message.Contains("Cannot process request because the process") && ex.Message.Contains("has exited")))
                    throw;
            }

            return p;
        }

        private static void Kill(Process p)
        {
            bool retry;
            do
            {
                retry = false;
                try
                {
                    if (!p.HasExited) p.Kill();
                    //foreach (Process cp in Process.GetProcessesByName(p.ProcessName))
                    //    if(!cp.HasExited) cp.Kill();
                }
                catch
                {
                    Thread.Sleep(100);
                    retry = true; // could be access denied or similar
                }
            } while (retry);
        }

        private static void WriteToStream(string text, StreamWriter stream, ref long limit)
        {
            try
            {
                if (limit > 0 && text != null)
                {
                    stream.WriteLine(text);
                    limit -= text.Length;
                }
            }
            catch (System.NullReferenceException)
            {
                // That's okay, let's just discard the output.
            }
        }

        private static long Memory(Process p)
        {
            long r = 0;

            //foreach (Process cp in Process.GetProcessesByName(p.ProcessName))
            //    try { r += cp.PeakVirtualMemorySize64; } catch { /* OK */ }
            try
            {
                if (!p.HasExited)
                {
                    // p.PeakVirtualMemorySize64 seems to not work right or over-approximate on some systems?
                    r = p.PeakWorkingSet64;
                }
            }
            catch
            {
                // OK because the process has a chance to exit.
            }

            return r;
        }

        private static TimeSpan Time(Process p, bool wallclock)
        {
            if (wallclock)
                return DateTime.Now - p.StartTime;
            else
                return p.TotalProcessorTime;
        }

    }
}
