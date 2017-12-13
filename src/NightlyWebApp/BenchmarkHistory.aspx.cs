using AzurePerformanceTest;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Nightly
{
    public partial class BenchmarkHistory : System.Web.UI.Page
    {
        public DateTime _startTime = DateTime.Now;
        Dictionary<string, string> _defaultParams = null;

        private AzureExperimentManager expManager = null;
        private AzureSummaryManager summaryManager = null;
        private Timeline timeline = null;

        public TimeSpan RenderTime
        {
            get { return DateTime.Now - _startTime; }
        }

        public string DiskSpace ()
        {
            string res = "";
            string notready = "";

            foreach (var d in System.IO.DriveInfo.GetDrives())
            {
                if (d.IsReady)
                {
                    res += d.Name.TrimEnd('\\') + " ";
                    res += (d.AvailableFreeSpace / 1073741824).ToString("F2") + " GB, ";
                }
                else
                    notready += d.Name.TrimEnd('\\') + ", ";
            }
            res += "temp @ " + Path.GetTempPath() + " ";
            res += "(N/R: " + notready.TrimEnd(' ', ',') + ")";
            return res;
        }

        public string TempSpace()
        {
            string res = "?";
            string root = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()));

            foreach (var d in System.IO.DriveInfo.GetDrives())
                if (d.IsReady && d.Name == root)
                {
                    res = Path.GetTempPath() + " " + (d.AvailableFreeSpace / 1073741824).ToString("F2") + " GB";
                    break;
                }
            return res;
        }

        protected async void Page_Load(object sender, EventArgs e)
        {
            try
            {
                _defaultParams = new Dictionary<string, string>();

                string summaryName = Request.Params.Get("summary");
                summaryName = summaryName == null ? Properties.Settings.Default.SummaryName : summaryName;
                _defaultParams.Add("summary", summaryName);

                var connectionString = await Helpers.GetConnectionString();
                expManager = AzureExperimentManager.Open(connectionString);
                summaryManager = new AzureSummaryManager(connectionString, Helpers.GetDomainResolver());

                timeline = await Helpers.GetTimeline(summaryName, expManager, summaryManager, true);

                string fn = Request.Params.Get("filename");
                if (fn != null) txtFilename.Text = fn;

                string db = Request.Params.Get("daysback");
                if (db != null) txtDaysBack.Text = db;

                if (txtFilename.Text != "")
                    BuildEntries();
            }
            catch (Exception ex)
            {
                Label l = new Label();
                l.Text = "Error loading dataset: " + ex.Message;
                phMain.Controls.Add(l);
                l = new Label();
                l.Text = "Stacktrace: " + ex.StackTrace;
                phMain.Controls.Add(l);
            }
        }

        protected class TableRow
        {
            public string ID = "";
            public string SubmissionTime = "";
            public bool IsFinished = false;
            public IEnumerable<BenchmarkResult> Results = null;
        }

        protected async Task<TableRow> GetRow(int id)
        {
            ExperimentViewModel x = timeline.GetExperiment(id);
            var s = await expManager.GetResults(id, fn => fn == txtFilename.Text);

            TableRow r = new TableRow();
            r.ID = x.Id.ToString();
            r.SubmissionTime = x.SubmissionTime.ToString();
            r.IsFinished = x.IsFinished;
            r.Results = s.Benchmarks;
            return r;
        }

        protected async void BuildEntries()
        {
            System.Web.UI.WebControls.TableRow tr;
            TableCell tc;
            HyperLink h;

            int ecnt = timeline.Experiments.Count();
            int i = 0;
            DateTime before = DateTime.Now;

            int daysback = Convert.ToInt32(txtDaysBack.Text);

            List<Task<TableRow>> tasks = new List<Task<TableRow>>();
            List<TableRow> rows = new List<TableRow>();

            for (i = 0; i < daysback && i < ecnt; i++)
            {
                ExperimentViewModel e = timeline.Experiments[ecnt - i - 1];
                tasks.Add(GetRow(e.Id));
            }

            await Task.WhenAll(tasks);
            foreach (var t in tasks)
                rows.Add(t.Result);

            System.Diagnostics.Debug.Print("Data load time {0:n2} sec", (DateTime.Now - before).TotalSeconds);

            i = 0;
            foreach (TableRow r in rows)
            {
                tr = new System.Web.UI.WebControls.TableRow();

                if (i++ % 2 == 0) tr.BackColor = Color.PaleGreen;
                else tr.BackColor = Color.LightGray;

                tc = new TableCell();
                h = new HyperLink();
                h.Text = r.ID;
                h.NavigateUrl = "Default.aspx?job=" + r.ID;
                tc.HorizontalAlign = HorizontalAlign.Right;
                if (r.Results.Count() > 1)
                    tc.RowSpan = r.Results.Count();
                tc.Controls.Add(h);
                tr.Cells.Add(tc);

                tc = new TableCell();
                tc.Text = r.SubmissionTime.ToString();
                tc.HorizontalAlign = HorizontalAlign.Left;
                if (r.Results.Count() > 1)
                    tc.RowSpan = r.Results.Count();
                tc.ForeColor = r.IsFinished ? Color.Black : Color.Gray;
                tr.Cells.Add(tc);

                if (r.Results.Count() == 0)
                {
                    tc = new TableCell();
                    tc.Text = "---";
                    tc.ColumnSpan = 6;
                    tc.HorizontalAlign = HorizontalAlign.Center;
                    tr.Cells.Add(tc);
                }
                else
                {
                    foreach (BenchmarkResult result in r.Results)
                    {
                        tc = new TableCell();
                        tc.Text = result.Status.ToString();
                        tc.HorizontalAlign = HorizontalAlign.Left;
                        tr.Cells.Add(tc);

                        tc = new TableCell();
                        tc.Text = result.ExitCode.ToString();
                        tc.HorizontalAlign = HorizontalAlign.Right;
                        tr.Cells.Add(tc);

                        tc = new TableCell();
                        tc.Text = result.CPUTime.TotalSeconds.ToString("F2");
                        tc.HorizontalAlign = HorizontalAlign.Right;
                        tr.Cells.Add(tc);

                        tc = new TableCell();
                        tc.Text = result.NormalizedCPUTime.ToString("F2");
                        tc.HorizontalAlign = HorizontalAlign.Right;
                        tr.Cells.Add(tc);

                        tc = new TableCell();
                        tc.Text = result.WallClockTime.TotalSeconds.ToString("F2");
                        tc.HorizontalAlign = HorizontalAlign.Right;
                        tr.Cells.Add(tc);

                        tc = new TableCell();
                        tc.Text = result.PeakMemorySizeMB.ToString("F2");
                        tc.HorizontalAlign = HorizontalAlign.Right;
                        tr.Cells.Add(tc);
                    }
                }

                tblEntries.Rows.Add(tr);
            }
        }

    }
}