using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Drawing;
using System.Threading.Tasks;

using System.Globalization;

using AjaxControlToolkit;
using AzurePerformanceTest;
using Nightly.Properties;
using PerformanceTest.Alerts;
using PerformanceTest;

namespace Nightly
{
    public partial class Compare : System.Web.UI.Page
    {
        public DateTime _startTime = DateTime.Now;
        uint top_n = 100;
        Comparison cmp = null;

        public TimeSpan RenderTime
        {
            get { return DateTime.Now - _startTime; }
        }

        protected string JX = (int.MaxValue - 1).ToString();
        protected string JY = (int.MaxValue).ToString();

        protected string Prefix
        {
            get
            {
                object o = ViewState["prefix"];
                if (o != null) return (string)o;
                return "";
            }
            set { ViewState["prefix"] = value; }
        }


        protected async void Page_Load(object sender, EventArgs e)
        {
            try
            {
                string summaryName = Request.Params.Get("summary");
                summaryName = summaryName == null ? Settings.Default.SummaryName : summaryName;
                
                string connectionString = await SiteMaster.GetConnectionString();
                AzureSummaryManager summaryManager = new AzureSummaryManager(connectionString, Helpers.GetDomainResolver());
                    
                string px = null, py = null;
                Tags tags = await Helpers.GetTags(summaryName, summaryManager);

                if (!IsPostBack)
                {
                    var last2 = await Helpers.GetTwoLastExperimentsId(summaryName, summaryManager);
                    string penultimate = last2.Item1.ToString();
                    string latest = last2.Item2.ToString();

                    lstTagX.Items.Add(new ListItem("Latest (" + latest + ")", latest));
                    lstTagX.Items.Add(new ListItem("Penultimate (" + penultimate + ")", penultimate));
                    //lstTagX.Items.Add(new ListItem("Records (best ever)", "RECORDS"));
                    lstTagY.Items.Add(new ListItem("Latest (" + latest + ")", latest));
                    lstTagY.Items.Add(new ListItem("Penultimate (" + penultimate + ")", penultimate));

                    foreach (KeyValuePair<string, int> kvp in tags)
                    {
                        lstTagX.Items.Add(new ListItem(kvp.Key + " (" + kvp.Value.ToString() + ")", kvp.Value.ToString()));
                        lstTagY.Items.Add(new ListItem(kvp.Key + " (" + kvp.Value.ToString() + ")", kvp.Value.ToString()));
                    }

                    px = Request.Params.Get("jobX");
                    py = Request.Params.Get("jobY");
                    Prefix = Request.Params.Get("prefix");

                    if (px == null) rbnTagX.Checked = true; else { txtIDX.Text = px; rbnIDX.Checked = true; }
                    if (py == null) rbnTagY.Checked = true; else { txtIDY.Text = py; rbnIDY.Checked = true; }

                    if (px != null)
                    {
                        if (tags.HasID(int.Parse(px, CultureInfo.InvariantCulture)))
                        {
                            rbnTagX.Checked = true;
                            lstTagX.SelectedValue = px;
                        }
                        else
                            lstTagX.SelectedValue = penultimate;
                    }
                    else
                        lstTagX.SelectedValue = penultimate;

                    if (py != null)
                    {
                        if (tags.HasID(int.Parse(py, CultureInfo.InvariantCulture)))
                        {
                            rbnTagY.Checked = true;
                            lstTagY.Items.FindByText(py.ToString());
                            lstTagY.SelectedValue = py.ToString();
                        }
                        else
                            lstTagY.SelectedValue = latest;
                    }
                    else
                        lstTagY.SelectedValue = latest;
                }

                JX = px != null ? px : rbnTagX.Checked ? lstTagX.SelectedValue : txtIDX.Text;
                JY = py != null ? py : rbnTagY.Checked ? lstTagY.SelectedValue : txtIDY.Text;

                ComparableExperiment jX = null, jY = null;

                try
                {
                    AzureExperimentManager expMan = AzureExperimentManager.Open(connectionString);
                    var t1 = Task.Run(() => Helpers.GetComparableExperiment(int.Parse(JX, CultureInfo.InvariantCulture), expMan));
                    var t2 = Task.Run(() => Helpers.GetComparableExperiment(int.Parse(JY, CultureInfo.InvariantCulture), expMan));

                    jX = await t1;
                    jY = await t2;
                }
                catch (Exception)
                {
                }

                txtIDX.Text = JX;
                txtIDY.Text = JY;

                cmp = new Comparison(jX, jY, Prefix.Replace('|', '/'), tags);

                phPre.Controls.Add(buildHeader("CHART_PRE", ""));
                phChart.Controls.Add(Charts.BuildComparisonChart(Prefix, cmp));
                phHisto.Controls.Add(Charts.BuildComparisonHistogramm(cmp));
                phMain.Controls.Add(buildTabPanels());
                //phMain.Controls.Add(buildFooter());
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

        protected string selfLink(string prefix = null, int jobX = 0, int jobY = 0)
        {
            string res = Request.FilePath;
            Dictionary<string, string> p = new Dictionary<string, string>();
            p.Add("prefix", (prefix != null) ? prefix : Prefix);
            p.Add("jobX", (jobX != 0) ? jobX.ToString() : JX);
            p.Add("jobY", (jobY != 0) ? jobY.ToString() : JY);
            bool first = true;
            foreach (KeyValuePair<string, string> kvp in p)
            {
                if (first) res += "?"; else res += "&";
                res += kvp.Key + "=" + kvp.Value;
                first = false;
            }
            return res;
        }

        public Control buildFooter()
        {
            Panel p = new Panel();
            p.Style["text-align"] = "justify";

            Panel space = new Panel();
            space.Height = 15;
            p.Controls.Add(space);

            Label l = new Label();
            l.Text = "Subcategories: ";
            l.Font.Size = 8;
            l.Font.Name = "helvetica";
            l.ForeColor = Color.Black;
            p.Controls.Add(l);

            if (Prefix == "")
            {
                l = new Label();
                l.Text = "UP";
                l.Font.Size = 8;
                l.Font.Name = "helvetica";
                l.Font.Bold = true;
                l.ForeColor = Color.Gray;
                p.Controls.Add(l);
            }
            else
            {
                HyperLink h = new HyperLink();
                h.Text = "UP";
                int lio = Prefix.LastIndexOf("|");
                h.NavigateUrl = selfLink(lio == -1 ? "" : Prefix.Substring(0, lio));
                h.Style["text-decoration"] = "none";
                h.Font.Size = 8;
                h.Font.Name = "helvetica";
                h.ForeColor = Color.Green;
                p.Controls.Add(h);
            }

            foreach (KeyValuePair<string, ComparisonStatistics> kvp in cmp.Statistics.subdirs)
            {
                string postfix = kvp.Key;
                l = new Label();
                l.Text += "&nbsp;| ";
                p.Controls.Add(l);

                HyperLink h = new HyperLink();
                h.Text = postfix;
                h.NavigateUrl = selfLink((Prefix == "") ? postfix : Prefix + "|" + postfix);
                h.Style["text-decoration"] = "none";
                h.Font.Size = 8;
                h.Font.Name = "helvetica";
                p.Controls.Add(h);
            }

            return p;
        }

        public static System.Web.UI.WebControls.Image buildAlertImage(AlertLevel level)
        {
            System.Web.UI.WebControls.Image res = new System.Web.UI.WebControls.Image();

            switch (level)
            {
                case AlertLevel.None:
                    res.ImageUrl = "~/img/ok.png";
                    res.AlternateText = "OK";
                    break;
                case AlertLevel.Warning:
                    res.ImageUrl = "~/img/warning.png";
                    res.AlternateText = "Warning";
                    break;
                case AlertLevel.Critical:
                default:
                    res.ImageUrl = "~/img/critical.png";
                    res.AlternateText = "Critical";
                    break;
            }

            res.BorderWidth = 0;
            res.Style["vertical-align"] = "middle";

            return res;
        }

        protected class TabHeaderTemplate : ITemplate
        {
            AlertLevel _al = AlertLevel.None;
            public string _title = null;
            public string _toolTip = null;

            public TabHeaderTemplate(AlertLevel alertLevel, string title, string toolTip)
            {
                _al = alertLevel;
                _title = title;
                _toolTip = toolTip;
            }

            public void InstantiateIn(Control c)
            {
                c.Controls.Add(buildAlertImage(_al));
                Label l = new Label();
                l.Text = _title;
                l.ToolTip = _toolTip;
                l.Style["margin-left"] = "5px";
                c.Controls.Add(l);
            }
        }

        protected class TabContentTemplate : ITemplate
        {
            public TabContentTemplate()
            {
            }

            public void InstantiateIn(Control c)
            {
            }
        }

        protected TableRow buildStatisticsRow(string cat, double val1, double val2, string unit, double diff, string diffunit, Color posColor, Color negColor)
        {
            TableRow row = new TableRow();
            TableCell cell = new TableCell();
            cell.HorizontalAlign = HorizontalAlign.Left;
            cell.Text = cat;
            row.Cells.Add(cell);
            cell = new TableCell();
            cell.HorizontalAlign = HorizontalAlign.Right;
            cell.Text = val1.ToString("F3") + " " + unit;
            row.Cells.Add(cell);
            cell = new TableCell();
            cell.HorizontalAlign = HorizontalAlign.Right;
            cell.Text = val2.ToString("F3") + " " + unit;
            row.Cells.Add(cell);
            cell = new TableCell();
            cell.HorizontalAlign = HorizontalAlign.Right;
            cell.Text = (diff == 0.0 ? "&plusmn;" : (diff > 0.0) ? "+" : "") + diff.ToString("F3") + " " + diffunit;
            cell.ForeColor = (diff >= 0.0) ? posColor : negColor;
            row.Cells.Add(cell);
            return row;
        }

        protected Panel buildSummary()
        {
            ComparisonStatistics cs = cmp.Statistics;

            Panel p = new Panel();
            Table t = new Table();
            t.BorderWidth = 1;

            TableRow row = new TableRow();
            TableHeaderCell thc = new TableHeaderCell();
            thc.Text = "";
            thc.HorizontalAlign = HorizontalAlign.Left;
            row.Cells.Add(thc);
            thc = new TableHeaderCell();
            thc.HorizontalAlign = HorizontalAlign.Center;
            thc.Text = cmp.ShortNameX;
            row.Cells.Add(thc);
            thc = new TableHeaderCell();
            thc.HorizontalAlign = HorizontalAlign.Center;
            thc.Text = cmp.ShortNameY;
            row.Cells.Add(thc);
            thc = new TableHeaderCell();
            thc.HorizontalAlign = HorizontalAlign.Center;
            thc.Text = "Relative";
            row.Cells.Add(thc);
            t.Rows.Add(row);

            row = new TableRow();
            TableCell tc = new TableCell();
            row.Cells.Add(tc);
            tc.Text = "Date";
            tc = new TableCell();
            tc.Text = cmp.DateX;
            row.Cells.Add(tc);
            tc = new TableCell();
            tc.Text = cmp.DateY;
            row.Cells.Add(tc);
            tc = new TableCell();
            if (cmp.JobY != null && cmp.JobX != null)
                tc.Text = (cmp.JobY.SubmissionTime - cmp.JobX.SubmissionTime).ToString();
            row.Cells.Add(tc);
            t.Rows.Add(row);

            t.Rows.Add(buildStatisticsRow("Files:", cs.x_files, cs.y_files, "", 100.0 * ((cs.y_files / cs.x_files) - 1.0), "%", Color.Green, Color.Red));
            t.Rows.Add(buildStatisticsRow("Results:", cs.CountX, cs.CountY, "", 100.0 * ((cs.CountY / cs.CountX) - 1.0), "%", Color.Green, Color.Red));
            t.Rows.Add(buildStatisticsRow("Results (SAT):", cs.x_countSAT, cs.y_countSAT, "", 100.0 * ((cs.y_countSAT / cs.x_countSAT) - 1.0), "%", Color.Green, Color.Red));
            t.Rows.Add(buildStatisticsRow("Results (UNSAT):", cs.x_countUNSAT, cs.y_countUNSAT, "", 100.0 * ((cs.y_countUNSAT / cs.x_countUNSAT) - 1.0), "%", Color.Green, Color.Red));
            t.Rows.Add(buildStatisticsRow("Results (UNKNOWN):", cs.x_countUNKNOWN, cs.y_countUNKNOWN, "", 100.0 * ((cs.y_countUNKNOWN / cs.x_countUNKNOWN) - 1.0), "%", Color.Red, Color.Green));
            t.Rows.Add(buildStatisticsRow("Avg. Time/Result:", cs.TimeX / cs.CountX, cs.TimeY / cs.CountY, "sec.", 100.0 * (((cs.TimeY / cs.CountY) / (cs.TimeX / cs.CountX)) - 1.0), "%", Color.Red, Color.Green));
            t.Rows.Add(buildStatisticsRow("Avg. Time/Result (SAT):", cs.x_cumulativeTimeSAT / cs.x_countSAT, cs.y_cumulativeTimeSAT / cs.y_countSAT, "sec.", 100.0 * (((cs.y_cumulativeTimeSAT / cs.y_countSAT) / (cs.x_cumulativeTimeSAT / cs.x_countSAT)) - 1.0), "%", Color.Red, Color.Green));
            t.Rows.Add(buildStatisticsRow("Avg. Time/Result (UNSAT):", cs.x_cumulativeTimeUNSAT / cs.x_countUNSAT, cs.y_cumulativeTimeUNSAT / cs.y_countUNSAT, "sec.", 100.0 * (((cs.y_cumulativeTimeUNSAT / cs.y_countUNSAT) / (cs.x_cumulativeTimeUNSAT / cs.x_countUNSAT)) - 1.0), "%", Color.Red, Color.Green));
            t.Rows.Add(buildStatisticsRow("Avg. Time/Result (UNKNOWN):", cs.x_cumulativeTimeUNKNOWN / cs.x_countUNKNOWN, cs.y_cumulativeTimeUNKNOWN / cs.y_countUNKNOWN, "sec.", 100.0 * (((cs.y_cumulativeTimeUNKNOWN / cs.y_countUNKNOWN) / (cs.x_cumulativeTimeUNKNOWN / cs.x_countUNKNOWN)) - 1.0), "%", Color.Red, Color.Green));


            Table t2 = new Table();
            row = new TableRow();
            thc = new TableHeaderCell();
            thc.Text = "Statistic";
            thc.HorizontalAlign = HorizontalAlign.Left;
            row.Cells.Add(thc);
            thc = new TableHeaderCell();
            thc.Text = "Value";
            thc.HorizontalAlign = HorizontalAlign.Left;
            row.Cells.Add(thc);
            t2.Rows.Add(row);

            row = new TableRow();
            tc = new TableCell();
            tc.Text = "Mean Delta";
            tc.HorizontalAlign = HorizontalAlign.Left;
            row.Cells.Add(tc);
            tc = new TableCell();
            tc.HorizontalAlign = HorizontalAlign.Right;
            tc.Text = cmp.Statistics.DeltaMean.ToString("F3");
            row.Cells.Add(tc);
            t2.Rows.Add(row);

            row = new TableRow();
            tc = new TableCell();
            tc.Text = "Dispersion Delta (Std. Dev.)";
            tc.HorizontalAlign = HorizontalAlign.Left;
            row.Cells.Add(tc);
            tc = new TableCell();
            tc.HorizontalAlign = HorizontalAlign.Right;
            tc.Text = cmp.Statistics.DeltaSTD.ToString("F3");
            row.Cells.Add(tc);
            t2.Rows.Add(row);

            row = new TableRow();
            tc = new TableCell();
            tc.Text = "Median Delta (P50)";
            tc.HorizontalAlign = HorizontalAlign.Left;
            row.Cells.Add(tc);
            tc = new TableCell();
            tc.HorizontalAlign = HorizontalAlign.Right;
            tc.Text = cmp.Statistics.DeltaP50.ToString("F3");
            row.Cells.Add(tc);
            t2.Rows.Add(row);

            row = new TableRow();
            tc = new TableCell();
            tc.Text = "[P1;P99]";
            tc.HorizontalAlign = HorizontalAlign.Left;
            row.Cells.Add(tc);
            tc = new TableCell();
            tc.HorizontalAlign = HorizontalAlign.Right;
            tc.Text = "[" + cmp.Statistics.DeltaP1.ToString("F2") + ";" + cmp.Statistics.DeltaP99.ToString("F2") + "]";
            row.Cells.Add(tc);
            t2.Rows.Add(row);

            Table bigtable = new Table();
            TableRow bigrow = new TableRow();
            TableCell bigcell = new TableCell();
            bigcell.HorizontalAlign = HorizontalAlign.Center;
            bigcell.Controls.Add(t);
            bigrow.Cells.Add(bigcell);
            bigcell = new TableCell();
            bigcell.HorizontalAlign = HorizontalAlign.Center;
            bigcell.Controls.Add(t2);
            bigrow.Cells.Add(bigcell);
            bigtable.Rows.Add(bigrow);
            p.Controls.Add(bigtable);
            return p;
        }

        protected Control buildTabPanels()
        {
            TabContainer tc = new TabContainer();
            tc.Height = 250;
            tc.ScrollBars = ScrollBars.Vertical;

            TabPanel tabStats = new TabPanel();
            tabStats.HeaderTemplate = new TabHeaderTemplate(AlertLevel.None, "Statistics", "Various statistical values.");
            tabStats.Controls.Add(buildSummary());
            tc.Tabs.Add(tabStats);

            tabStats = new TabPanel();
            tabStats.HeaderTemplate = new TabHeaderTemplate(AlertLevel.None, "Mean Happiness [Subdirs]", "A rating of the subdirectories by mean happiness.");
            tabStats.Controls.Add(buildMeanHappinessPanel());
            tc.Tabs.Add(tabStats);

            tabStats = new TabPanel();
            tabStats.HeaderTemplate = new TabHeaderTemplate(AlertLevel.None, "Mean Happiness [Top " + top_n + " Users]", "A rating of the users by mean happiness.");
            tabStats.Controls.Add(buildMeanHappinessPanelUsers(top_n));
            tc.Tabs.Add(tabStats);

            tabStats = new TabPanel();
            tabStats.HeaderTemplate = new TabHeaderTemplate(AlertLevel.None, "Dispersion Happiness [Subdirs]", "A rating of the subdirectories by dispersion happiness.");
            tabStats.Controls.Add(buildDispersionHappinessPanel());
            tc.Tabs.Add(tabStats);

            tabStats = new TabPanel();
            tabStats.HeaderTemplate = new TabHeaderTemplate(AlertLevel.None, "Dispersion Happiness [Top " + top_n + " Users]", "A rating of the users by dispersion happiness.");
            tabStats.Controls.Add(buildDispersionHappinessPanelUsers(top_n));
            tc.Tabs.Add(tabStats);

            return tc;
        }

        protected TableRow buildDispersionRow(string name, double value, double vs, Color good, Color bad)
        {
            TableRow r = new TableRow();
            TableCell c = new TableCell();

            Label l = new Label();
            l.Text = string.Format("<a href='" + selfLink(Prefix + (Prefix != "" ? "|" : "") + name.Replace('/', '|')) + "' style='text-decoration:none;'>{0}</a>:", name);
            c.Controls.Add(l);
            r.Cells.Add(c);

            c = new TableCell();
            c.HorizontalAlign = HorizontalAlign.Right;
            l = new Label();
            l.Text = value.ToString("F3");
            l.ForeColor = Math.Abs(value) > vs ? bad : good;
            c.Controls.Add(l);
            r.Cells.Add(c);

            return r;
        }

        private int CompareSubdirsByDispersion(string x, string y)
        {
            ComparisonStatistics a = cmp.Statistics.subdirs[x];
            ComparisonStatistics b = cmp.Statistics.subdirs[y];
            double valA = Math.Abs(a.DeltaSTD);
            double valB = Math.Abs(b.DeltaSTD);
            if (valA > valB)
                return -1;
            if (valA < valB)
                return 1;
            return 0;
        }

        protected Panel buildDispersionHappinessPanel()
        {
            ComparisonStatistics cs = cmp.Statistics;

            Panel p = new Panel();
            Table t = new Table();
            t.Enabled = true;
            t.BorderWidth = 1;

            TableRow row = new TableRow();
            TableHeaderCell thc = new TableHeaderCell();
            thc.Text = "Subdirectory";
            row.Cells.Add(thc);
            thc = new TableHeaderCell();
            thc.Text = "Dispersion";
            row.Cells.Add(thc);
            t.Rows.Add(row);

            List<string> sds = new List<string>();
            foreach (KeyValuePair<string, ComparisonStatistics> kvp in cmp.Statistics.subdirs)
                sds.Add(kvp.Key);
            sds.Sort(CompareSubdirsByDispersion);

            double vs = 15;

            foreach (string s in sds)
                t.Rows.Add(buildDispersionRow(s, cmp.Statistics.subdirs[s].DeltaSTD, vs, Color.Green, Color.Red));

            p.Controls.Add(t);
            return p;
        }


        private int CompareSubdirsByPostfixDispersion(string x, string y)
        {
            ComparisonStatistics a = cmp.Statistics.postfixes[x];
            ComparisonStatistics b = cmp.Statistics.postfixes[y];
            double absA = Math.Abs(a.DeltaSTD);
            double absB = Math.Abs(b.DeltaSTD);
            if (absA > absB)
                return -1;
            if (absA < absB)
                return 1;
            return 0;
        }

        protected Panel buildDispersionHappinessPanelUsers(uint n)
        {
            ComparisonStatistics cs = cmp.Statistics;

            Panel p = new Panel();
            Table t = new Table();
            t.Enabled = true;
            t.BorderWidth = 1;

            TableRow row = new TableRow();
            TableHeaderCell thc = new TableHeaderCell();
            thc.Text = "User";
            row.Cells.Add(thc);
            thc = new TableHeaderCell();
            thc.Text = "Dispersion";
            row.Cells.Add(thc);
            t.Rows.Add(row);

            List<string> sds = new List<string>();
            foreach (KeyValuePair<string, ComparisonStatistics> kvp in cmp.Statistics.postfixes)
                sds.Add(kvp.Key);
            sds.Sort(CompareSubdirsByPostfixDispersion);

            double vs = 15;

            uint count = 0;
            foreach (string s in sds)
            {
                t.Rows.Add(buildDispersionRow(s, cmp.Statistics.postfixes[s].DeltaSTD, vs, Color.Green, Color.Red));
                if (++count == n) break;
            }

            p.Controls.Add(t);
            return p;
        }

        protected TableRow buildMeanRow(string name, double value, Color good, Color bad)
        {
            TableRow r = new TableRow();
            TableCell c = new TableCell();

            Label l = new Label();
            l.Text = string.Format("<a href='" + selfLink(Prefix + (Prefix != "" ? "|" : "") + name.Replace('/', '|')) + "' style='text-decoration:none;'>{0}</a>:", name);
            c.Controls.Add(l);
            r.Cells.Add(c);

            c = new TableCell();
            c.HorizontalAlign = HorizontalAlign.Right;
            l = new Label();
            l.Text = value.ToString("F3");
            l.ForeColor = value < 0.0 ? bad : good;
            c.Controls.Add(l);
            r.Cells.Add(c);

            return r;
        }

        private int CompareSubdirsByMean(string x, string y)
        {
            ComparisonStatistics a = cmp.Statistics.subdirs[x];
            ComparisonStatistics b = cmp.Statistics.subdirs[y];
            if (a.DeltaMean < b.DeltaMean)
                return -1;
            else if (a.DeltaMean > b.DeltaMean)
                return +1;
            return 0;
        }

        protected Panel buildMeanHappinessPanel()
        {
            ComparisonStatistics cs = cmp.Statistics;

            Panel p = new Panel();
            Table t = new Table();
            t.Enabled = true;
            t.BorderWidth = 1;

            TableRow row = new TableRow();
            TableHeaderCell thc = new TableHeaderCell();
            thc.Text = "Subdirectory";
            row.Cells.Add(thc);
            thc = new TableHeaderCell();
            thc.Text = "Mean";
            row.Cells.Add(thc);
            t.Rows.Add(row);

            List<string> sds = new List<string>();
            foreach (KeyValuePair<string, ComparisonStatistics> kvp in cmp.Statistics.subdirs)
                sds.Add(kvp.Key);
            sds.Sort(CompareSubdirsByMean);

            foreach (string s in sds)
                t.Rows.Add(buildMeanRow(s, cmp.Statistics.subdirs[s].DeltaMean, Color.Green, Color.Red));

            p.Controls.Add(t);
            return p;
        }

        private int CompareSubdirsByPostfixMean(string x, string y)
        {
            ComparisonStatistics a = cmp.Statistics.postfixes[x];
            ComparisonStatistics b = cmp.Statistics.postfixes[y];
            if (a.DeltaMean < b.DeltaMean)
                return -1;
            else if (a.DeltaMean > b.DeltaMean)
                return +1;
            return 0;
        }

        protected Panel buildMeanHappinessPanelUsers(uint n)
        {
            ComparisonStatistics cs = cmp.Statistics;

            Panel p = new Panel();
            Table t = new Table();
            t.Enabled = true;
            t.BorderWidth = 1;

            TableRow row = new TableRow();
            TableHeaderCell thc = new TableHeaderCell();
            thc.Text = "User";
            row.Cells.Add(thc);
            thc = new TableHeaderCell();
            thc.Text = "Mean";
            row.Cells.Add(thc);
            t.Rows.Add(row);

            List<string> sds = new List<string>();
            foreach (KeyValuePair<string, ComparisonStatistics> kvp in cmp.Statistics.postfixes)
                sds.Add(kvp.Key);
            sds.Sort(CompareSubdirsByPostfixMean);

            uint count = 0;
            foreach (string s in sds)
            {
                t.Rows.Add(buildMeanRow(s, cmp.Statistics.postfixes[s].DeltaMean, Color.Green, Color.Red));
                if (++count == n) break;
            }

            p.Controls.Add(t);
            return p;
        }

        protected Control buildHeader(string tag, string summaryText)
        {
            Panel res = new Panel();

            Panel p = new Panel();
            p.ID = "Panel_" + tag + "_Header";
            p.CssClass = "collapsePanelHeader";
            p.Height = 30;
            p.BackColor = ColorTranslator.FromHtml("#88EEBB");

            Panel p1 = new Panel();
            p1.Style["padding"] = "5px";
            p1.Style["cursor"] = "pointer";
            p1.Style["vertical-align"] = "middle";

            System.Web.UI.WebControls.Image ib = new System.Web.UI.WebControls.Image();
            ib.Style["float"] = "left";
            ib.ImageUrl = "~/img/lookingglass.png";
            p1.Controls.Add(ib);

            Label l1 = new Label();
            l1.Style["float"] = "left";
            l1.Style["margin-left"] = "5px";
            l1.ForeColor = Color.Black;
            l1.Text = cmp.Title + "  /" + Prefix.Replace('|', '/');
            l1.Font.Bold = true;
            p1.Controls.Add(l1);

            System.Web.UI.WebControls.Image ai = buildAlertImage(AlertLevel.None);
            ai.Style["float"] = "right";
            p1.Controls.Add(ai);

            p.Controls.Add(p1);
            res.Controls.Add(p);

            p = new Panel();
            Label tl = new Label();
            tl.Text = summaryText;
            p.Controls.Add(tl);

            Panel space = new Panel();
            space.Height = 25;
            p.Controls.Add(space);

            res.Controls.Add(p);

            return res;
        }

        protected void btnCSV_Click(object sender, EventArgs e)
        {
            HttpResponse response = HttpContext.Current.Response;

            string filename = JX + "_vs_" + JY;
            if (Prefix != "") filename += " " + Prefix.Replace('/', '|');
            filename += ".csv";

            response.ContentType = "text/csv";
            response.AppendHeader("Content-Disposition", "attachment; filename=" + filename);

            response.Write("File,[" + JX + "],[" + JY + "],Dispersion" + Environment.NewLine);

            foreach (Comparison.Point q in cmp.Datapoints)
            {
                string r = q.tooltip + ",";
                r += (q.x == cmp.TimeOutX) ? "TIME" : (q.x == cmp.MemOutX) ? "MEMORY" : (q.x == cmp.ErrorX) ? "ERROR" : q.x.ToString();
                r += ",";
                r += (q.y == cmp.TimeOutY) ? "TIME" : (q.y == cmp.MemOutY) ? "MEMORY" : (q.y == cmp.ErrorY) ? "ERROR" : q.y.ToString();
                r += ",";

                if (q.x >= cmp.TimeOutX && q.y >= cmp.TimeOutY)
                {
                    r += "0,";
                }
                else if (q.x < cmp.TimeOutX && q.y < cmp.TimeOutY)
                {
                    r += (q.x - q.y).ToString() + ",";
                }
                else if (q.x < cmp.TimeOutX && q.y >= cmp.TimeOutY)
                {
                    r += (-cmp.TimeOutY).ToString() + ",";
                }
                else
                {
                    r += (cmp.TimeOutX).ToString() + ",";
                }

                response.Write(r + Environment.NewLine);
            }

            response.Flush(); // Sends all currently buffered output to the client.
            response.SuppressContent = true;  // Gets or sets a value indicating whether to send HTTP content to the client.
            HttpContext.Current.ApplicationInstance.CompleteRequest(); // Causes ASP.NET to bypass all events and filtering in the HTTP pipeline chain of execution and directly execute the EndRequest event.
        }
    }
}
