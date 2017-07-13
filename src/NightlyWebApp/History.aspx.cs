using AzurePerformanceTest;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Nightly
{
    public partial class History : System.Web.UI.Page
    {
        public DateTime _startTime = DateTime.Now;
        Dictionary<string, string> _defaultParams = null;

        private Timeline vm;
        private Tags tags;

        public TimeSpan RenderTime
        {
            get { return DateTime.Now - _startTime; }
        }

        protected async void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {

                try
                {
                    _defaultParams = new Dictionary<string, string>();

                    string summaryName = Request.Params.Get("summary");
                    summaryName = summaryName == null ? Properties.Settings.Default.SummaryName : summaryName;
                    _defaultParams.Add("summary", summaryName);

                    var connectionString = await Helpers.GetConnectionString();
                    var expManager = AzureExperimentManager.Open(connectionString);
                    var summaryManager = new AzureSummaryManager(connectionString, Helpers.GetDomainResolver());

                    vm = await Helpers.GetTimeline(summaryName, expManager, summaryManager);
                    tags = await Helpers.GetTags(summaryName, summaryManager);

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
        }

        protected void BuildEntries()
        {
            var last_job = vm.GetLastExperiment();
            int last_tag_id = 0;
            string last_tag_name = "";
            foreach (KeyValuePair<string, int> kvp in tags)
            {
                if (kvp.Value > last_tag_id)
                {
                    last_tag_name = kvp.Key;
                    last_tag_id = kvp.Value;
                }
            }

            TableRow tr;
            TableCell tc;
            HyperLink h;


            int n = vm.Experiments.Length;
            for (int i = n; --i >= 0;)
            {
                var exp = vm.Experiments[i];
                tr = new TableRow();

                if (i % 2 == 0) tr.BackColor = Color.LightGreen;
                else tr.BackColor = Color.LightGray;

                string id = exp.Id.ToString();

                tc = new TableCell();
                h = new HyperLink();
                h.Text = id;
                h.NavigateUrl = "Default.aspx?job=" + id;
                tc.Controls.Add(h);
                tr.Cells.Add(tc);

                tc = new TableCell();
                tc.Text = exp.SubmissionTime.ToString();
                tc.HorizontalAlign = HorizontalAlign.Right;
                if (exp.IsFinished)
                    tc.ForeColor = Color.Black;
                else
                    tc.ForeColor = Color.Gray;
                tr.Cells.Add(tc);

                tc = new TableCell();
                h = new HyperLink();
                h.NavigateUrl = "Compare.aspx?jobX=" + last_tag_id + "&jobY=" + id;
                h.Text = last_tag_name;
                tc.Controls.Add(h);
                tc.HorizontalAlign = HorizontalAlign.Center;
                tr.Cells.Add(tc);

                tc = new TableCell();
                h = new HyperLink();
                h.NavigateUrl = "Compare.aspx?jobX=" + last_job.Id + "&jobY=" + id;
                h.Text = "latest";
                tc.Controls.Add(h);
                tr.Cells.Add(tc);

                tblEntries.Rows.Add(tr);
            }
        }

    }
}