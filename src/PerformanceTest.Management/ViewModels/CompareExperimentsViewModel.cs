using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using Measurement;
using System.Diagnostics;

namespace PerformanceTest.Management
{
    public class CompareExperimentsViewModel : INotifyPropertyChanged
    {
        private readonly int id1, id2;
        private readonly ExperimentManager manager;
        private readonly IUIService uiService;

        private BenchmarkResult[] allResults1, allResults2;
        private CompareBenchmarksViewModel[] experiments, allResults;
        private bool isFiltering;

        private bool checkIgnorePostfix, checkIgnoreCategory, checkIgnorePrefix;
        private string extension1, extension2, category1, category2, sharedDirectory1, sharedDirectory2;

        public event PropertyChangedEventHandler PropertyChanged;

        public CompareExperimentsViewModel(int id1, int id2, ExperimentDefinition def1, ExperimentDefinition def2, ExperimentManager manager, IUIService message)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.manager = manager;
            this.uiService = message;
            this.id1 = id1;
            this.id2 = id2;
            this.checkIgnoreCategory = false;
            this.checkIgnorePostfix = false;
            this.checkIgnorePrefix = false;
            this.extension1 = "." + def1.BenchmarkFileExtension.Split('|')[0];
            this.extension2 = "." + def2.BenchmarkFileExtension.Split('|')[0];
            this.category1 = def1.Category;
            this.category2 = def2.Category;
            this.sharedDirectory1 = def1.BenchmarkDirectory;
            this.sharedDirectory2 = def2.BenchmarkDirectory;
            this.isFiltering = false;
            DownloadResultsAsync();
        }

        public bool IsFiltering
        {
            get { return isFiltering; }
            private set
            {
                isFiltering = value;
                NotifyPropertyChanged();
            }
        }

        public CompareBenchmarksViewModel[] CompareItems
        {
            get { return experiments; }
            private set
            {
                experiments = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("Title");
            }
        }
        public string Category1
        {
            get { return category1; }
        }
        public string Category2
        {
            get { return category2; }
        }
        public string Title
        {
            get
            {
                string title = string.Format("Comparison: {0} vs. {1}", id1, id2);
                if (CompareItems != null && allResults != null)
                {
                    title += String.Format(" ({0} of {1} items shown)", CompareItems.Length, allResults.Length);
                }
                return title;
            }

        }
        public bool CheckIgnorePostfix
        {
            get { return checkIgnorePostfix; }
            set
            {
                if (checkIgnorePostfix == value) return;
                checkIgnorePostfix = value;
                JoinResults();
                NotifyPropertyChanged();
                NotifyPropertyChanged("EnableFirstExtension");
                NotifyPropertyChanged("EnableSecondExtension");
            }
        }
        public bool CheckIgnorePrefix
        {
            get { return checkIgnorePrefix; }
            set
            {
                if (checkIgnorePrefix == value) return;
                checkIgnorePrefix = value;
                JoinResults();
                NotifyPropertyChanged();
            }
        }
        public bool CheckIgnoreCategory
        {
            get { return checkIgnoreCategory; }
            set
            {
                if (checkIgnoreCategory == value) return;
                checkIgnoreCategory = value;
                JoinResults();
                NotifyPropertyChanged();
            }
        }
        public bool EnableFirstExtension
        {
            get { return checkIgnorePostfix; }
        }
        public bool EnableSecondExtension
        {
            get { return checkIgnorePostfix; }
        }
        public string Extension1
        {
            get { return extension1; }
            set
            {
                extension1 = value;
                JoinResults();
            }
        }
        public string Extension2
        {
            get { return extension2; }
            set
            {
                extension2 = value;
                JoinResults();
            }
        }

        public string Runtime1Title { get { return "Runtime (" + id1.ToString() + ")"; } }
        public string Runtime2Title { get { return "Runtime (" + id2.ToString() + ")"; } }
        public string Status1Title { get { return "Status (" + id1.ToString() + ")"; } }
        public string Status2Title { get { return "Status (" + id2.ToString() + ")"; } }
        public string Exitcode1Title { get { return "ExitCode (" + id1.ToString() + ")"; } }
        public string Exitcode2Title { get { return "ExitCode (" + id2.ToString() + ")"; } }
        public string Sat1Title { get { return "SAT (" + id1.ToString() + ")"; } }
        public string Sat2Title { get { return "SAT (" + id2.ToString() + ")"; } }
        public string Unsat1Title { get { return "UNSAT (" + id1.ToString() + ")"; } }
        public string Unsat2Title { get { return "UNSAT (" + id2.ToString() + ")"; } }
        public string Unknown1Title { get { return "UNKNOWN (" + id1.ToString() + ")"; } }
        public string Unknown2Title { get { return "UNKNOWN (" + id2.ToString() + ")"; } }


        public void FilterResultsByError(int code)
        {
            if (IsFiltering || allResults == null) return;
            IsFiltering = true;
            try
            {
                if (code == 0) CompareItems = allResults.Where(e => e.Results1.Sat > 0 && e.Results2.Sat > 0).ToArray(); //both sat
                else if (code == 1) CompareItems = allResults.Where(e => e.Results1.Unsat > 0 && e.Results2.Unsat > 0).ToArray(); //both unsat
                else if (code == 2) CompareItems = allResults.Where(e => e.Results1.Unknown > 0 && e.Results2.Unknown > 0).ToArray(); //both unknown
                else if (code == 3) CompareItems = allResults.Where(e => e.Results1.Sat > 0 || e.Results2.Sat > 0).ToArray(); //one sat
                else if (code == 4) CompareItems = allResults.Where(e => e.Results1.Unsat > 0 || e.Results2.Unsat > 0).ToArray(); //one unsat
                else if (code == 5) CompareItems = allResults.Where(e => e.Results1.Unknown > 0 || e.Results2.Unknown > 0).ToArray(); //one unknown
                else if (code == 6) CompareItems = allResults.Where(e => e.Results1.Status == ResultStatus.Bug || e.Results2.Status == ResultStatus.Bug).ToArray();
                else if (code == 7) CompareItems = allResults.Where(e => e.Results1.Status == ResultStatus.Error || e.Results2.Status == ResultStatus.Error ||
                                                                          e.Results1.Status == ResultStatus.InfrastructureError || e.Results2.Status == ResultStatus.InfrastructureError).ToArray();
                else if (code == 8) CompareItems = allResults.Where(e => e.Results1.Status == ResultStatus.Timeout || e.Results2.Status == ResultStatus.Timeout).ToArray();
                else if (code == 9) CompareItems = allResults.Where(e => e.Results1.Status == ResultStatus.OutOfMemory || e.Results2.Status == ResultStatus.OutOfMemory).ToArray();
                else if (code == 10) CompareItems = allResults.Where(e => e.Results1.Sat > 0 && e.Results2.Sat == 0 || e.Results1.Sat == 0 && e.Results2.Sat > 0).ToArray(); //sat star
                else if (code == 11) CompareItems = allResults.Where(e => e.Results1.Unsat > 0 && e.Results2.Unsat == 0 || e.Results1.Unsat == 0 && e.Results2.Unsat > 0).ToArray(); //unsat star
                else if (code == 12) CompareItems = allResults.Where(e => e.Results1.Sat > 0 && e.Results2.Sat == 0 || e.Results1.Sat == 0 && e.Results2.Sat > 0 || e.Results1.Unsat > 0 && e.Results2.Unsat == 0 || e.Results1.Unsat == 0 && e.Results2.Unsat > 0).ToArray(); //ok star
                else if (code == 13) CompareItems = allResults.Where(e => e.Results1.Sat > 0 && e.Results2.Unsat > 0 || e.Results1.Unsat > 0 && e.Results2.Sat > 0).ToArray(); //sat/unsat
                else CompareItems = allResults;
            }
            finally
            {
                IsFiltering = false;
            }
        }

        public void FilterResultsByText(string filter)
        {
            if (IsFiltering || allResults == null) return;
            IsFiltering = true;
            try
            {
                if (filter != "")
                {
                    var resVm = allResults;
                    if (filter == "sat")
                    {
                        resVm = resVm.Where(e => Regex.IsMatch(e.Filename, "/^(?:(?!unsat).)*$/")).ToArray();
                    }
                    CompareItems = resVm.Where(e => e.Filename.Contains(filter)).ToArray();
                }
                else CompareItems = allResults;
            }
            finally
            {
                IsFiltering = false;
            }
        }

        private async void DownloadResultsAsync()
        {
            if (IsFiltering) return;
            IsFiltering = true;

            var handle = uiService.StartIndicateLongOperation("Comparing the experiments...");
            try
            {
                allResults = CompareItems = null;

                var t1 = Task.Run(() => manager.GetResults(id1));
                var t2 = Task.Run(() => manager.GetResults(id2));
                allResults1 = (await t1).Benchmarks;
                allResults2 = (await t2).Benchmarks;

                JoinResults();
                IsFiltering = false;
            }
            catch(Exception ex)
            {
                uiService.ShowError(ex, "Failed to get results for the experiments");
            }
            finally
            {
                IsFiltering = false;
                uiService.StopIndicateLongOperation(handle);
            }
        }

        private void JoinResults()
        {
            if (allResults1 == null || allResults2 == null) return;

            var param = new CheckboxParameters(checkIgnoreCategory, checkIgnorePrefix, checkIgnorePostfix, category1, category2, extension1, extension2, sharedDirectory1, sharedDirectory2);
            var join = InnerJoinOrderedResults(allResults1, allResults2, param, uiService);
            Array.Sort<CompareBenchmarksViewModel>(join, new VMComparer());
            CompareItems = allResults = join;
        }

        private sealed class VMComparer : IComparer<CompareBenchmarksViewModel>
        {
            public int Compare(CompareBenchmarksViewModel x, CompareBenchmarksViewModel y)
            {
                if (x == null)
                    if (y == null) return 0;
                    else return 1;
                if (y == null) return -1;

                double diff = Math.Abs(x.Diff) - Math.Abs(y.Diff);
                if (diff < 0) return 1;
                else if (diff > 0) return -1;
                else return 0;
            }
        }

        private static CompareBenchmarksViewModel[] InnerJoinOrderedResults(BenchmarkResult[] r1, BenchmarkResult[] r2, CheckboxParameters param, IUIService uiService)
        {
            int n1 = r1.Length;
            int n2 = r2.Length;
            var join = new CompareBenchmarksViewModel[Math.Min(n1, n2)];
            if (!param.IsCategoryChecked && param.Category1 != param.Category2) return new CompareBenchmarksViewModel[0];
            if (!param.IsPrefixChecked && param.Dir1 != param.Dir2) return new CompareBenchmarksViewModel[0];
            int i = 0;
            for (int i1 = 0, i2 = 0; i1 < n1 && i2 < n2;)
            {
                string filename1 = r1[i1].BenchmarkFileName;
                string filename2 = r2[i2].BenchmarkFileName;
                if (!param.IsCategoryChecked)
                {
                    filename1 = param.Category1 + "/" + filename1;
                    filename2 = param.Category2 + "/" + filename2;
                }
                if (param.IsPostfixChecked)
                {
                    filename1 = filename1.Substring(0, filename1.Length - param.Ext1.Length);
                    filename2 = filename2.Substring(0, filename2.Length - param.Ext2.Length);
                }
                int cmp = string.Compare(filename1, filename2);
                if (cmp == 0)
                {
                    join[i++] = new CompareBenchmarksViewModel(filename1,
                        new BenchmarkResultViewModel(r1[i1], uiService),
                        new BenchmarkResultViewModel(r2[i2], uiService),
                        uiService);
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
            var join2 = new CompareBenchmarksViewModel[i];
            for (; --i >= 0;)
            {
                join2[i] = join[i];
            }
            return join2;
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class CheckboxParameters
    {
        private readonly bool isCategoryChecked, isPrefixChecked, isPostfixChecked;
        private readonly string category1, category2, ext1, ext2, dir1, dir2;
        public CheckboxParameters(bool isCategoryChecked, bool isPrefixChecked, bool isPostfixChecked, string category1, string category2, string ext1, string ext2, string dir1, string dir2)
        {
            this.isCategoryChecked = isCategoryChecked;
            this.isPrefixChecked = isPrefixChecked;
            this.isPostfixChecked = isPostfixChecked;
            this.category1 = category1;
            this.category2 = category2;
            this.ext1 = ext1;
            this.ext2 = ext2;
            this.dir1 = dir1;
            this.dir2 = dir2;
        }
        public bool IsCategoryChecked { get { return isCategoryChecked; } }
        public bool IsPrefixChecked { get { return isPrefixChecked; } }
        public bool IsPostfixChecked { get { return isPostfixChecked; } }
        public string Category1 { get { return category1; } }
        public string Category2 { get { return category2; } }
        public string Ext1 { get { return ext1; } }
        public string Ext2 { get { return ext2; } }
        public string Dir1 { get { return dir1; } }
        public string Dir2 { get { return dir2; } }
    }


    public class CompareBenchmarksViewModel : INotifyPropertyChanged
    {
        private readonly BenchmarkResultViewModel result1;
        private readonly BenchmarkResultViewModel result2;
        private readonly string filename;
        private readonly IUIService message;

        public event PropertyChangedEventHandler PropertyChanged;


        public CompareBenchmarksViewModel(string filename, BenchmarkResultViewModel res1, BenchmarkResultViewModel res2, IUIService message)
        {
            if (res1 == null || res2 == null) throw new ArgumentNullException("results");
            if (message == null) throw new ArgumentNullException("message");
            this.result1 = res1;
            this.result2 = res2;
            this.filename = filename;
            this.message = message;
        }

        public string Filename
        {
            get { return filename; }
        }

        public double Diff { get { return result1.NormalizedRuntime - result2.NormalizedRuntime; } }

        public BenchmarkResultViewModel Results1 { get { return result1; } }

        public BenchmarkResultViewModel Results2 { get { return result2; } }


        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
