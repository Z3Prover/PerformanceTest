using AjaxControlToolkit;
using AzurePerformanceTest;
using Nightly.Properties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.DataVisualization;
using System.Web.UI.DataVisualization.Charting;
using System.Web.UI.WebControls;
using System.Threading.Tasks;
using PerformanceTest.Alerts;
using PerformanceTest;
using Measurement;
using PerformanceTest.Records;

namespace Nightly
{
    public partial class _Default : System.Web.UI.Page
    {
        public DateTime _startTime = DateTime.Now;
        private uint _listLimit = 1000;
        private Dictionary<string, string> _defaultParams = null;
        private Timeline vm;
        private Settings config = Settings.Default;

        public static CultureInfo culture = new CultureInfo("en-US");

        protected async void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                try
                {
                    _defaultParams = new Dictionary<string, string>();

                    string p = Request.Params.Get("days");
                    _defaultParams.Add("days", (p == null) ? config.daysback.ToString() : p);

                    p = Request.Params.Get("cat");
                    _defaultParams.Add("cat", (p == null) ? "" : p);

                    string summaryName = Request.Params.Get("summary");
                    summaryName = summaryName == null ? config.SummaryName : summaryName;
                    _defaultParams.Add("summary", summaryName);


                    var connectionString = await Helpers.GetConnectionString();
                    var expManager = AzureExperimentManager.Open(connectionString);
                    var summaryManager = new AzureSummaryManager(connectionString, Helpers.GetDomainResolver());

                    vm = await Helpers.GetTimeline(summaryName, expManager, summaryManager);

                    buildCategoryPanels();
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

        public TimeSpan RenderTime
        {
            get { return DateTime.Now - _startTime; }
        }

        protected string selfLink(string cat = null, string days = null)
        {
            string res = Request.FilePath;
            Dictionary<string, string> p = new Dictionary<string, string>();
            p.Add("cat", (cat != null) ? cat : _defaultParams["cat"]);
            p.Add("days", (days != null) ? days : _defaultParams["days"]);
            p.Add("summary", vm.SummaryName);
            bool first = true;
            foreach (KeyValuePair<string, string> kvp in p)
            {
                if (first) res += "?"; else res += "&";
                res += kvp.Key + "=" + kvp.Value;
                first = false;
            }
            return res;
        }

        protected double GetRowValue(ExperimentViewModel exp, string cat, List<string> subcats)
        {
            double value = 0.0;
            if (cat == "")
            {
                foreach (var ecat in exp.Summary.CategorySummary.Keys)
                {
                    value += SumValuesForCategories(ecat, subcats, exp);
                }
            }
            else
            {
                value += SumValuesForCategories(cat, subcats, exp);
            }
            return value;
        }

        public Series series(string name, Color col, int width, string cat, double maxdays, AxisType axisType,
                             List<string> subcats, List<string> avgcats = null, bool logarithmic = false)
        {
            double logMultiplier = 20000.0;
            DateTime now = DateTime.Now;
            Series ser = new Series((logarithmic) ? name + " (log)" : name);
            ser.ChartType = SeriesChartType.Line;
            ser.YAxisType = axisType;
            ser.Color = col;
            ser.BorderWidth = width;

            double earliest_x = double.MinValue;
            double earliest_y = 0.0;
            bool need_earliest = true;

            double latest_x = double.MinValue;
            double latest_y = 0.0;
            bool need_latest = true;

            double logMax = 10.0;
            if (logarithmic)
            {
                // we need to find the max value.
                foreach (ExperimentViewModel exp in vm.Experiments)
                {
                    double value = GetRowValue(exp, cat, subcats);
                    if (value > logMax) logMax = value;
                }
                logMultiplier = logMax / Math.Log10(logMax);
            }

            foreach (ExperimentViewModel exp in vm.Experiments)
            {
                var pdt = exp.SubmissionTime;
                string date_str = pdt.ToString(culture);

                double value = GetRowValue(exp, cat, subcats);

                if (avgcats != null)
                {
                    double contravalue = 0.0;
                    if (cat == "")
                    {
                        foreach (var ecat in exp.Summary.CategorySummary.Keys)
                        {
                            contravalue += SumValuesForCategories(ecat, avgcats, exp);
                        }
                    }
                    else
                    {
                        contravalue += SumValuesForCategories(cat, avgcats, exp);
                    }

                    value /= contravalue;
                    if (double.IsNaN(value)) value = 0.0;
                }

                if (date_str != null)
                {
                    double x = (now - pdt).TotalDays;
                    if (x <= maxdays)
                    {
                        if (logarithmic && value != 0.0)
                            ser.Points.AddXY(-x, Math.Log10(value) * logMultiplier);
                        else
                            ser.Points.AddXY(-x, value);
                        ser.Points.Last().ToolTip = string.Concat(date_str, ": ", name, ": ", value);

                        if (x == maxdays)
                            need_earliest = false;
                        else if (x == 0.0)
                            need_latest = false;

                        if (-x > latest_x)
                        {
                            latest_x = -x;
                            latest_y = value;
                        }
                    }
                    else if (-x > earliest_x)
                    {
                        earliest_x = -x;
                        earliest_y = value;
                    }
                }
            }

            if (need_latest)
            {
                if (logarithmic && latest_y != 0.0)
                    ser.Points.AddXY(0.0, Math.Log10(latest_y) * logMultiplier);
                else
                    ser.Points.AddXY(0.0, latest_y);
                ser.Points.Last().ToolTip = "Latest: " + latest_y.ToString();
            }

            if (need_earliest && earliest_x != double.MinValue)
            {
                if (logarithmic && earliest_y != 0.0)
                    ser.Points.InsertXY(0, -maxdays, Math.Log10(earliest_y) * logMultiplier);
                else
                    ser.Points.InsertXY(0, -maxdays, earliest_y);
                ser.Points.First().ToolTip = "Before: " + earliest_y.ToString();
            }

            ser.MarkerSize = 2;
            ser.MarkerStyle = MarkerStyle.Circle;
            return ser;
        }

        private static double SumValuesForCategories(string cat, List<string> avgcats, ExperimentViewModel exp)
        {
            double contravalue = 0.0;
            foreach (string sc in avgcats)
            {
                AggregatedAnalysis catsum;
                if (exp.Summary.CategorySummary.TryGetValue(cat, out catsum))
                {
                    string s;
                    double d;
                    switch (sc)
                    {
                        case "BUG":
                            contravalue += catsum.Bugs;
                            break;
                        case "ERROR":
                            contravalue += catsum.Errors;
                            break;
                        case "INFERR":
                            contravalue += catsum.InfrastructureErrors;
                            break;
                        case "TIMEOUT":
                            contravalue += catsum.Timeouts;
                            break;
                        case "MEMORY":
                            contravalue += catsum.MemoryOuts;
                            break;
                        default:
                            if (catsum.Properties.TryGetValue(sc, out s) && double.TryParse(s, out d))
                                contravalue += d;
                            break;
                    }
                }
            }

            return contravalue;
        }

        public void buildStatisticsChart(string name, Chart chart)
        {
            ChartArea ca = new ChartArea();
            ca.Name = "Statistics";

            double maxdays = config.daysback;

            string rdays = Request.Params.Get("days");
            if (rdays != null) maxdays = Convert.ToUInt32(rdays);

            ca.AxisX.Title = "Days in the past";
            ca.AxisX.Interval = 30;
            ca.AxisX.Minimum = -maxdays;
            ca.AxisX.Maximum = 0;

            // ca.AxisY.Title = "# Benchmarks";
            ca.AxisY.TextOrientation = TextOrientation.Rotated270;
            ca.AxisY.IsLogarithmic = false;
            ca.AxisY.Minimum = 0;
            ca.AxisY.LabelStyle.IsEndLabelVisible = true;

            ca.AxisY2.IsLogarithmic = false;
            ca.AxisY2.Minimum = 0;
            ca.AxisY2.LabelStyle.IsEndLabelVisible = true;
            ca.AxisY2.MajorGrid.Enabled = false;
            ca.AxisY2.MinorGrid.Enabled = false;

            ca.Position.Height = 70;
            ca.Position.Width = 85;
            ca.Position.Auto = false;
            ca.Position.X = 0;
            ca.Position.Y = 5;

            chart.ChartAreas.Add(ca);

            Legend l = new Legend();
            l.Docking = Docking.Right;
            l.IsDockedInsideChartArea = false;
            l.Alignment = StringAlignment.Near;
            l.DockedToChartArea = ca.Name;
            l.Name = "StatisticsLegend";
            chart.Legends.Add(l);

            chart.Series.Add(series("# solved", Color.Green, 1, name, maxdays, AxisType.Primary, new List<string>() { "SAT", "UNSAT" }));
            chart.Series.Last().ChartArea = ca.Name;
            chart.Series.Last().Legend = l.Name;
            chart.Series.Add(series("# errors", Color.OrangeRed, 2, name, maxdays, AxisType.Primary, new List<string>() { "ERROR" }, null, true));
            chart.Series.Last().ChartArea = ca.Name;
            chart.Series.Last().Legend = l.Name;
            chart.Series.Add(series("# inf. errors", Color.LightSalmon, 1, name, maxdays, AxisType.Primary, new List<string>() { "INFERR" }, null, true));
            chart.Series.Last().ChartArea = ca.Name;
            chart.Series.Last().Legend = l.Name;
            chart.Series.Add(series("# bugs", Color.Red, 2, name, maxdays, AxisType.Primary, new List<string>() { "BUG" }, null, true));
            chart.Series.Last().ChartArea = ca.Name;
            chart.Series.Last().Legend = l.Name;
            chart.Series.Add(series("# unsolved", Color.Blue, 1, name, maxdays, AxisType.Primary, new List<string>() { "TIMEOUT", "MEMORY", "UNKNOWN" }));
            chart.Series.Last().ChartArea = ca.Name;
            chart.Series.Last().Legend = l.Name;

            chart.Series.Add(series("avg  runtime [s]", Color.Gray, 1, name, maxdays, AxisType.Secondary,
                new List<string>() { "SATTIME", "UNSATTIME" },
                new List<string>() { "SAT", "UNSAT" }));
            chart.Series.Last().ChartArea = "Statistics";
        }

        public void buildPerformanceChart(string category, Chart chart)
        {
            //Chart chart = new Chart();
            //chart.Style["margin-top"] = "0px";
            //chart.Style["margin-left"] = "20px";
            //chart.Style["margin-bottom"] = "20px";

            // chart.Titles.Add((name == "") ? "Overall" : name);
            ChartArea ca = new ChartArea();
            ca.Name = "Performance";
            double maxdays = config.daysback;

            string rdays = Request.Params.Get("days");
            if (rdays != null) maxdays = Convert.ToUInt32(rdays);

            ca.AxisX.Interval = 30;
            ca.AxisX.Minimum = -maxdays;
            ca.AxisX.Maximum = 0.0;
            ca.AxisX.LabelStyle.Enabled = false;

            // ca.AxisY.TextOrientation = TextOrientation.Rotated270;
            ca.AxisY.Minimum = 0.0;
            ca.AxisY.Maximum = 100.0;
            ca.AxisY.LabelStyle.IsEndLabelVisible = true;
            ca.AxisY.LabelAutoFitStyle = LabelAutoFitStyles.None;
            ca.AxisY.LabelAutoFitMinFontSize = 8;
            ca.AxisY.LabelAutoFitMaxFontSize = 8;
            ca.AxisY.CustomLabels.Add(new CustomLabel(-4, 4, "0%", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
            ca.AxisY.CustomLabels.Add(new CustomLabel(25, 25, "", 0, LabelMarkStyle.None, GridTickTypes.Gridline));
            ca.AxisY.CustomLabels.Add(new CustomLabel(46, 54, "50%", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
            ca.AxisY.CustomLabels.Add(new CustomLabel(75, 75, "", 0, LabelMarkStyle.None, GridTickTypes.Gridline));
            ca.AxisY.CustomLabels.Add(new CustomLabel(96, 104, "100%", 0, LabelMarkStyle.SideMark, GridTickTypes.All));

            ca.Position.Height = 25;
            ca.Position.Width = 85;
            ca.Position.Auto = false;
            ca.Position.X = 0;
            ca.Position.Y = 75;

            ca.AlignWithChartArea = "Statistics";
            ca.AlignmentOrientation = AreaAlignmentOrientations.Vertical;
            ca.AlignmentStyle = AreaAlignmentStyles.PlotPosition;

            chart.ChartAreas.Add(ca);

            Legend l = new Legend();
            l.Docking = Docking.Right;
            l.IsDockedInsideChartArea = false;
            l.Alignment = StringAlignment.Near;
            l.Name = "PerformanceLegend";
            l.DockedToChartArea = ca.Name;
            chart.Legends.Add(l);

            RecordsTable records = vm.Records;
            CategoryRecord virtualBest;
            if (category == "")
                virtualBest = records.Overall;
            else
            {
                if (records.CategoryRecords.ContainsKey(category))
                    virtualBest = records.CategoryRecords[category];
                else
                    return;
            }
            double virtualBestAvg = (virtualBest.Runtime / virtualBest.Files);

            DateTime now = DateTime.Now;

            //Series ser = new Series("Perf. Index");
            //ser.ChartArea = ca.Name;
            //ser.Legend = l.Name;
            //ser.ChartType = SeriesChartType.Line;
            //ser.YAxisType = AxisType.Primary;
            //ser.Color = Color.Blue;
            //ser.MarkerSize = 2;
            //ser.MarkerStyle = MarkerStyle.Circle;

            Series ser2 = new Series("Solved [%]");
            ser2.ChartArea = ca.Name;
            ser2.Legend = l.Name;
            ser2.ChartType = SeriesChartType.Line;
            ser2.YAxisType = AxisType.Primary;
            ser2.Color = Color.PaleGreen;
            ser2.MarkerSize = 2;
            ser2.MarkerStyle = MarkerStyle.Circle;

            //Series ser3 = new Series("P. Closeness [%]");
            //ser3.ChartArea = ca.Name;
            //ser3.Legend = l.Name;
            //ser3.ChartType = SeriesChartType.Line;
            //ser3.YAxisType = AxisType.Primary;
            //ser3.Color = Color.PaleVioletRed;
            //ser3.MarkerSize = 2;
            //ser3.MarkerStyle = MarkerStyle.Circle;

            //Series ser4 = new Series("Exp. Index [%]");
            //ser4.ChartArea = ca.Name;
            //ser4.Legend = l.Name;
            //ser4.ChartType = SeriesChartType.Line;
            //ser4.YAxisType = AxisType.Primary;
            //ser4.Color = Color.Red;
            //ser4.MarkerSize = 2;
            //ser4.MarkerStyle = MarkerStyle.Circle;

            bool need_earliest = true;
            double earliest_x = double.MaxValue;
            double earliest_y = 0.0;
            bool need_latest = true;
            double latest_x = double.MinValue;
            double latest_y = 0.0;

            foreach (ExperimentViewModel exp in vm.Experiments)
            {
                if (!exp.IsFinished) continue;

                //var jstats = exp.Summary.Overall;
                //var z3stats = Z3SummaryProperties.TryWrap(jstats);
                // todo: probably use average normalized timeout?
                // or non-normalized for stat?
                // todo: sat+unsat seems to be non-comparable with number of files
                //double vbaGoodPart = virtualBestAvg * jstats.Runs;
                //double vbaBadPart = (jstats.Runs - (z3stats.Sat + z3stats.Unsat)) * exp.Timeout.TotalSeconds;
                //double vbaBoth = (vbaGoodPart + vbaBadPart) / jstats.Runs;
                // vbaBoth ~ virtualBestAvg + timeout * (1 - (sat+unsat)/files)

                //double vwa = exp.Timeout.TotalSeconds;

                DateTime pdt = Convert.ToDateTime(exp.SubmissionTime, culture);
                double x = (now - pdt).TotalDays;
                if (x <= maxdays)
                {
                    double y = 0.0, y2 = 0.0; // y3 = 0.0, y4 = 0.0;
                    string tt = "";

                    if (category == "" || exp.Summary.CategorySummary.ContainsKey(category))
                    {
                        var cs = category == "" ? exp.Summary.Overall : exp.Summary.CategorySummary[category];
                        Z3SummaryProperties csZ3 = Z3SummaryProperties.TryWrap(cs);
                        if (csZ3 != null)
                        {
                            //double st = csZ3.TimeSat;
                            //double ut = csZ3.TimeUnsat;

                            double solvedProblems = csZ3.Sat + csZ3.Unsat;
                            double totalProblems = csZ3.TargetSat + csZ3.TargetUnsat + csZ3.TargetUnknown;

                            y2 = 100.0 * solvedProblems / totalProblems;

                            //double curAvg = (solvedProblems == 0) ? vwa : (st + ut) / solvedProblems;
                            //y3 = 100.0 * (1.0 - (vbaBoth / vwa));
                            //if (y3 > 100.0) y3 = 100; else if (y3 < 0.0) y3 = 0.0;

                            //y = (y2 * y3) / 100.0;
                            //tt = pdt.ToString() + ": " + y.ToString();
                            //if (y > 100.0) y = 100.0;

                            //y4 = (y2 + y3) / 2.0;
                        }
                    }
                    else
                    {
                        //y = 0.0;
                        y2 = 0.0;
                        //y3 = 0.0;
                        //y4 = 0.0;
                        tt = pdt.ToString() + ": no data";
                    }

                    //ser.Points.AddXY(-x, y);
                    //ser.Points.Last().ToolTip = tt;
                    ser2.Points.AddXY(-x, y2);
                    //ser.Points.Last().ToolTip = y2.ToString();
                    //ser3.Points.AddXY(-x, y3);
                    //ser.Points.Last().ToolTip = y3.ToString();
                    //ser4.Points.AddXY(-x, y4);
                    //ser.Points.Last().ToolTip = y4.ToString();

                    if (x == maxdays)
                        need_earliest = false;
                    else if (x == 0.0)
                        need_latest = false;

                    if (-x < earliest_x)
                    {
                        earliest_x = -x;
                        earliest_y = y;
                    }
                    else if (-x > latest_x)
                    {
                        latest_x = -x;
                        latest_y = y;
                    }
                }
            }

            if (need_latest)
            {
                ser2.Points.AddXY(0.0, latest_y);
                ser2.Points.Last().ToolTip = "Latest: " + latest_y.ToString();
            }

            if (need_earliest)
            {
                ser2.Points.InsertXY(0, -maxdays, earliest_y);
                ser2.Points.First().ToolTip = "Before: " + earliest_y.ToString();
            }

            //chart.Series.Add(ser);
            chart.Series.Add(ser2);
            //chart.Series.Add(ser3);
            //chart.Series.Add(ser4);
        }

        protected void buildPerformanceVectorGraph(string category, Chart chart)
        {
            ChartArea ca = new ChartArea("PerformanceVectors");
            double maxdays = config.daysback;

            string rdays = Request.Params.Get("days");
            if (rdays != null) maxdays = Convert.ToUInt32(rdays);

            ca.AxisX.Minimum = 0.0;
            ca.AxisX.Maximum = 100.0;
            ca.AxisX.LabelStyle.IsEndLabelVisible = true;
            ca.AxisX.LabelAutoFitStyle = LabelAutoFitStyles.None;
            ca.AxisX.LabelAutoFitMinFontSize = 8;
            ca.AxisX.LabelAutoFitMaxFontSize = 8;
            ca.AxisX.CustomLabels.Add(new CustomLabel(0, 8, "0%", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
            ca.AxisX.CustomLabels.Add(new CustomLabel(25, 25, "", 0, LabelMarkStyle.None, GridTickTypes.Gridline));
            ca.AxisX.CustomLabels.Add(new CustomLabel(46, 54, "50%", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
            ca.AxisX.CustomLabels.Add(new CustomLabel(75, 75, "", 0, LabelMarkStyle.None, GridTickTypes.Gridline));
            ca.AxisX.CustomLabels.Add(new CustomLabel(92, 100, "100%", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
            ca.AxisX.Title = "Rel. Success";

            ca.AxisY.Minimum = 0.0;
            ca.AxisY.Maximum = 100.0;
            ca.AxisY.LabelStyle.IsEndLabelVisible = true;
            ca.AxisY.LabelAutoFitStyle = LabelAutoFitStyles.None;
            ca.AxisY.LabelAutoFitMinFontSize = 8;
            ca.AxisY.LabelAutoFitMaxFontSize = 8;
            ca.AxisY.CustomLabels.Add(new CustomLabel(0, 8, "0%", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
            ca.AxisY.CustomLabels.Add(new CustomLabel(25, 25, "", 0, LabelMarkStyle.None, GridTickTypes.Gridline));
            ca.AxisY.CustomLabels.Add(new CustomLabel(46, 54, "50%", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
            ca.AxisY.CustomLabels.Add(new CustomLabel(75, 75, "", 0, LabelMarkStyle.None, GridTickTypes.Gridline));
            ca.AxisY.CustomLabels.Add(new CustomLabel(92, 100, "100%", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
            ca.AxisY.Title = "Rel. Speed";

            ca.Position.Height = 25;
            ca.Position.Width = 25;
            ca.Position.Auto = false;
            ca.Position.X = 0;
            ca.Position.Y = 75;

            chart.ChartAreas.Add(ca);

            Series series = new Series();
            series.ChartArea = "PerformanceVectors";
            series.ChartArea = ca.Name;
            //series.Legend = l.Name;
            series.ChartType = SeriesChartType.Point;
            series.YAxisType = AxisType.Primary;
            series.Color = Color.Blue;
            series.MarkerSize = 4;
            series.MarkerStyle = MarkerStyle.Circle;

            RecordsTable records = vm.Records;
            CategoryRecord virtualBest = (category == "") ? records.Overall : records.CategoryRecords[category];
            double virtualBestAvg = (virtualBest.Runtime / virtualBest.Files);

            DateTime now = DateTime.Now;

            double youngest = double.MaxValue;
            double youngest_x = 0.0;
            double youngest_y = 0.0;

            foreach (ExperimentViewModel exp in vm.Experiments)
            {
                if (!exp.IsFinished) continue;

                DateTime pdt = Convert.ToDateTime(exp.SubmissionTime, culture);
                double age = (now - pdt).TotalDays;

                if (age > maxdays) continue;

                if (category == "" || exp.Summary.CategorySummary.ContainsKey(category))
                {
                    var cs = (category == "") ? exp.Summary.Overall : exp.Summary.CategorySummary[category];
                    var csZ3 = Z3SummaryProperties.TryWrap(cs);

                    if (csZ3 != null)
                    {
                        int solved = (csZ3.Sat + csZ3.Unsat);
                        int unsolved = (cs.Files - (csZ3.Sat + csZ3.Unsat));
                        double avg_time = (csZ3.TimeSat + csZ3.TimeUnsat) / (double)solved;
                        double top_speed = virtualBestAvg;

                        double x = 100.0 * solved / (double)cs.Files; // % solved.
                        double y = 100.0 * top_speed / avg_time; // rel. speed?

                        int inx = series.Points.AddXY(x, y);
                        series.Points[inx].ToolTip = exp.SubmissionTime.ToString();

                        int intensity = (int)(255.0 * (age / maxdays));
                        series.Points[inx].MarkerColor = Color.FromArgb(intensity, intensity, 255);

                        if (age < youngest)
                        {
                            youngest_x = x;
                            youngest_y = y;
                        }
                    }
                }
            }

            series.Points.AddXY(youngest_x, youngest_y);
            series.Points.Last().MarkerColor = Color.Red;

            chart.Series.Add(series);
        }

        protected Chart buildChart(string category)
        {
            Chart chart = new Chart();
            chart.Titles.Add((category == "") ? "Overall" : category);

            chart.Height = 600;
            chart.Width = 900;

            buildStatisticsChart(category, chart);

            if (Request.Params.Get("pvg") == null)
                buildPerformanceChart(category, chart);
            else
                buildPerformanceVectorGraph(category, chart);

            return chart;
        }

        public Panel buildAlertMessages(AlertSet cas, string zero_message)
        {
            Panel res = new Panel();

            if (cas.Count == 0)
            {
                Panel lp = new Panel();
                lp.Controls.Add(buildAlertImage(AlertLevel.None));
                Label l = new Label();
                l.Text = zero_message;
                l.ForeColor = Color.Green;
                l.Style["vertical-align"] = "middle";
                l.Style["margin-left"] = "5px";
                lp.Controls.Add(l);
                res.Controls.Add(lp);
            }
            else
            {
                if (cas.Messages.ContainsKey(AlertLevel.Critical))
                    foreach (string s in cas.Messages[AlertLevel.Critical])
                    {
                        Panel lp = new Panel();
                        lp.Controls.Add(buildAlertImage(AlertLevel.Critical));
                        Label l = new Label();
                        l.Text = s;
                        l.ForeColor = Color.Red;
                        l.Style["vertical-align"] = "middle";
                        l.Style["margin-left"] = "5px";
                        lp.Controls.Add(l);
                        res.Controls.Add(lp);
                    }

                if (cas.Messages.ContainsKey(AlertLevel.Warning))
                    foreach (string s in cas.Messages[AlertLevel.Warning])
                    {
                        Panel lp = new Panel();
                        lp.Controls.Add(buildAlertImage(AlertLevel.Warning));
                        Label l = new Label();
                        l.Text = s;
                        l.ForeColor = Color.Orange;
                        l.Style["vertical-align"] = "middle";
                        l.Style["margin-left"] = "5px";
                        lp.Controls.Add(l);
                        res.Controls.Add(lp);
                    }

                if (cas.Messages.ContainsKey(AlertLevel.None))
                    foreach (string s in cas.Messages[AlertLevel.None])
                    {
                        Panel lp = new Panel();
                        lp.Controls.Add(buildAlertImage(AlertLevel.None));
                        Label l = new Label();
                        l.Text = s;
                        l.ForeColor = Color.Green;
                        l.Style["vertical-align"] = "middle";
                        l.Style["margin-left"] = "5px";
                        lp.Controls.Add(l);
                        res.Controls.Add(lp);
                    }
            }

            return res;
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

        protected Control MakeDaysLink(string text, uint d)
        {
            if (_defaultParams["days"] != d.ToString())
            {
                HyperLink res = new HyperLink();
                res.NavigateUrl = selfLink(null, d.ToString());
                res.Style["text-decoration"] = "none";
                res.Style["font-family"] = "monospace";
                res.Text = text;
                return res;
            }
            else
            {
                Label res = new Label();
                res.Style["text-decoration"] = "none";
                res.Style["font-family"] = "monospace";
                res.Text = text;
                res.ForeColor = Color.Black;
                return res;
            }
        }

        protected Panel MakeDaySelectPanel()
        {
            Panel daySelect = new Panel();
            daySelect.Style["float"] = "right";
            Label l = new Label();
            l.Text = "Timeframe:&nbsp;";
            l.ForeColor = Color.Black;
            daySelect.Controls.Add(l);
            daySelect.Controls.Add(MakeDaysLink("1yr", 365));
            l = new Label();
            l.Text = "&nbsp;|&nbsp;";
            daySelect.Controls.Add(l);
            daySelect.Controls.Add(MakeDaysLink("6mo", 180));
            l = new Label();
            l.Text = "&nbsp;|&nbsp;";
            daySelect.Controls.Add(l);
            daySelect.Controls.Add(MakeDaysLink("1mo", 30));
            l = new Label();
            l.Text = "&nbsp;|&nbsp;";
            daySelect.Controls.Add(l);
            daySelect.Controls.Add(MakeDaysLink("1wk", 7));
            return daySelect;
        }

        public void buildCategoryPanel(string title, string category, string tag,
                                       bool collapsed, bool isOdd, bool collapsible, bool titleBold,
                                       string summaryText, ExperimentAlerts alerts)
        {
            Panel p = new Panel();
            p.ID = "Panel_" + tag + "_Header";
            p.CssClass = "collapsePanelHeader";
            p.Height = 30;
            if (isOdd)
                p.BackColor = ColorTranslator.FromHtml("#88EEBB");
            else
                p.BackColor = ColorTranslator.FromHtml("#EEEEEE");


            Panel p1 = new Panel();
            p1.Style["padding"] = "5px";
            p1.Style["cursor"] = "pointer";
            p1.Style["vertical-align"] = "middle";

            if (collapsible)
            {
                ImageButton ib = new ImageButton();
                ib.Style["float"] = "left";
                ib.ID = "I_" + tag;
                ib.ImageUrl = "~/img/expand_blue.jpg";
                ib.AlternateText = "(...)";
                p1.Controls.Add(ib);

                Label l2 = new Label();
                l2.ID = "L_" + tag;
                l2.Style["float"] = "left";
                l2.Style["margin-left"] = "5px";
                p1.Controls.Add(l2);
            }
            else
            {
                System.Web.UI.WebControls.Image ib = new System.Web.UI.WebControls.Image();
                ib.Style["float"] = "left";
                ib.ImageUrl = "~/img/lookingglass.png";
                p1.Controls.Add(ib);
            }

            Label l1 = new Label();
            l1.Style["float"] = "left";
            l1.Style["margin-left"] = "5px";
            l1.ForeColor = Color.Black;
            l1.Text = title;
            l1.Font.Bold = titleBold;
            p1.Controls.Add(l1);

            System.Web.UI.WebControls.Image ai = buildAlertImage(alerts[category].Level);
            ai.Style["float"] = "right";
            p1.Controls.Add(ai);

            p.Controls.Add(p1);

            phMain.Controls.Add(p);


            p = new Panel();
            p.ID = "Panel_" + tag + "_Content";
            p.CssClass = "collapsePanel";
            p.Height = 0;

            Table t = new Table();
            t.Style["border-width"] = "0px";

            TableRow r = new TableRow();
            TableCell tc = new TableCell();

            Label tl = new Label();
            tl.Text = summaryText;
            tc.Controls.Add(tl);
            r.Cells.Add(tc);
            t.Rows.Add(r);

            r = new TableRow();
            tc = new TableCell();
            tc.Controls.Add(buildChart(category));
            r.Cells.Add(tc);
            t.Rows.Add(r);

            //r = new TableRow();
            //tc = new TableCell();
            //tc.Style["padding"] = "0px";
            //ChartArea pchart = buildPerformanceChart(category, 900);
            //tc.Controls.Add(pchart);
            //r.Cells.Add(tc);
            //t.Rows.Add(r);

            p.Controls.Add(t);
            phMain.Controls.Add(p);

            Panel space = new Panel();
            space.Height = 15;
            phMain.Controls.Add(space);


            CollapsiblePanelExtender cep = new CollapsiblePanelExtender();

            cep.ID = tag + "_CPE";
            cep.TargetControlID = "Panel_" + tag + "_Content";
            cep.SuppressPostBack = true;
            cep.Collapsed = collapsible && collapsed;
            if (collapsible)
            {
                cep.ExpandControlID = "Panel_" + tag + "_Header";
                cep.CollapseControlID = "Panel_" + tag + "_Header";
                cep.TextLabelID = "L_" + tag;
                cep.ImageControlID = "I_" + tag;
                cep.ExpandedImage = "~/img/collapse_blue.jpg";
                cep.CollapsedImage = "~/img/expand_blue.jpg";
                cep.CollapsedText = "[+]";
                cep.ExpandedText = "[-]";
            }

            phMain.Controls.Add(cep);
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
            List<string> _files = null;

            public TabContentTemplate(List<string> files)
            {
                _files = files;
            }

            public void InstantiateIn(Control c)
            {
            }
        }

        public Panel buildList(List<string> items)
        {
            Panel p = new Panel();
            Label l = new Label();
            uint i = 0;

            foreach (string s in items)
            {
                l.Text += s + "<br/>";
                i++;
                if (i >= _listLimit)
                {
                    l.Text += "[Output truncated at " + _listLimit.ToString() + " entries; to see more use (...)?limit=x]<br/>";
                    break;
                }
            }

            p.Controls.Add(l);
            return p;
        }

        TabPanel buildSummaryTab(string category, string alliswelltext, ExperimentAlerts alerts)
        {
            TabPanel tabSummary = new TabPanel();
            string toolTip = "This tab lists all alerts.";

            if (category == "")
            {
                int total = 0;
                AlertLevel al = AlertLevel.None;

                foreach (string cat in vm.Categories)
                {
                    AlertSet catAlerts = alerts[cat];
                    total += catAlerts.Count;

                    if ((al == AlertLevel.None && catAlerts.Level != al) ||
                        (al == AlertLevel.Warning && catAlerts.Level == AlertLevel.Critical))
                        al = catAlerts.Level;

                    if (catAlerts.Count > 0)
                    {
                        Label l = new Label();
                        l.Text = string.Format("ExperimentAlerts in <a href='" + selfLink(cat) + "' style='text-decoration:none;'>{0}</a>:", cat);

                        tabSummary.Controls.Add(l);
                        tabSummary.Controls.Add(buildAlertMessages(catAlerts, ""));
                    }
                }

                TabHeaderTemplate htm = new TabHeaderTemplate(al, "ExperimentAlerts", toolTip);
                if (total > 0) htm._title += " (" + total + ")";
                tabSummary.HeaderTemplate = htm;
                tabSummary.ContentTemplate = new TabContentTemplate(new List<string>());

                if (total == 0)
                {
                    Label l = new Label();
                    l.Text = alliswelltext;
                    l.ForeColor = Color.Green;
                    tabSummary.Controls.Add(l);
                }
            }
            else
            {
                AlertSet catAlerts = alerts[category];
                TabHeaderTemplate htm = new TabHeaderTemplate(catAlerts.Level, "ExperimentAlerts", toolTip);
                if (catAlerts.Count > 0) htm._title += " (" + catAlerts.Count + ")";
                tabSummary.HeaderTemplate = htm;
                tabSummary.ContentTemplate = new TabContentTemplate(new List<string>());
                tabSummary.Controls.Add(buildAlertMessages(catAlerts, alliswelltext));
            }

            return tabSummary;
        }

        TabPanel buildStatsTab(ExperimentViewModel exp, ExperimentStatusSummary expStatus, string category)
        {
            TabPanel res = new TabPanel();
            res.HeaderTemplate = new TabHeaderTemplate(AlertLevel.None, "Statistics", "Statistical information about the job.");
            res.ContentTemplate = new TabContentTemplate(new List<string>());
            res.Controls.Add(buildStatistics(exp, expStatus, category));
            return res;
        }

        TableRow buildStatisticsRow(string text, uint value, string unit, Color flagColor)
        {
            TableRow row = new TableRow();
            TableCell cell = new TableCell();
            cell.Text = text;
            row.Cells.Add(cell);
            cell = new TableCell();
            cell.Text = value.ToString() + unit;
            if (value != 0) cell.ForeColor = flagColor;
            row.Cells.Add(cell);
            return row;
        }

        TableRow buildStatisticsRow(string text, TimeSpan value, string unit, Color flagColor)
        {
            TableRow row = new TableRow();
            TableCell cell = new TableCell();
            cell.Text = text;
            row.Cells.Add(cell);
            cell = new TableCell();
            cell.Text = value.ToString() + unit;
            cell.ForeColor = flagColor;
            row.Cells.Add(cell);
            return row;
        }

        TableRow buildStatisticsRow(string text, double value, string unit, Color flagColor)
        {
            TableRow row = new TableRow();
            TableCell cell = new TableCell();
            cell.Text = text;
            row.Cells.Add(cell);
            cell = new TableCell();
            cell.Text = value.ToString() + " " + unit;
            cell.ForeColor = flagColor;
            row.Cells.Add(cell);
            return row;
        }

        TableRow buildStatisticsRow(string text, string value, string unit, Color flagColor)
        {
            TableRow row = new TableRow();
            TableCell cell = new TableCell();
            cell.Text = text;
            row.Cells.Add(cell);
            cell = new TableCell();
            cell.Text = value.ToString() + " " + unit;
            cell.ForeColor = flagColor;
            row.Cells.Add(cell);
            return row;
        }

        Control buildStatistics(ExperimentViewModel exp, ExperimentStatusSummary expStatus, string category)
        {
            var cs = exp[category];

            Panel p = new Panel();
            Table t = new Table();

            string st = exp.SubmissionTime.ToString();
            if (!exp.IsFinished)
                st += " <font color=red>unfinished</font>";
            t.Rows.Add(buildStatisticsRow("Experiment submission time:", st, "", Color.Black));
            string id_msg = exp.Id.ToString();
            if (expStatus.ReferenceId.HasValue)
                id_msg += " (Reference: " + expStatus.ReferenceId.Value + ")";
            else
                id_msg += " (no reference)";

            int sat = cs.Properties.ContainsKey(Z3Domain.KeySat) ? int.Parse(cs.Properties[Z3Domain.KeySat], CultureInfo.InvariantCulture) : 0;
            int unsat = cs.Properties.ContainsKey(Z3Domain.KeyUnsat) ? int.Parse(cs.Properties[Z3Domain.KeyUnsat], CultureInfo.InvariantCulture) : 0;
            int unk = cs.Properties.ContainsKey(Z3Domain.KeyUnknown) ? int.Parse(cs.Properties[Z3Domain.KeyUnknown], CultureInfo.InvariantCulture) : 0;
            double timesat = cs.Properties.ContainsKey(Z3Domain.KeyTimeSat) ? double.Parse(cs.Properties[Z3Domain.KeyTimeSat], CultureInfo.InvariantCulture) : 0;
            double timeunsat = cs.Properties.ContainsKey(Z3Domain.KeyTimeUnsat) ? double.Parse(cs.Properties[Z3Domain.KeyTimeUnsat], CultureInfo.InvariantCulture) : 0;

            t.Rows.Add(buildStatisticsRow("Experiment ID:", id_msg, "", Color.Black));
            t.Rows.Add(buildStatisticsRow("Files:", cs.Files, "", Color.Black));
            t.Rows.Add(buildStatisticsRow("SAT:", sat, "", Color.Black));
            t.Rows.Add(buildStatisticsRow("UNSAT:", unsat, "", Color.Black));
            t.Rows.Add(buildStatisticsRow("UNKNOWN:", unk, "", Color.Black));
            t.Rows.Add(buildStatisticsRow("Errors:", cs.Errors, "", Color.Orange));
            t.Rows.Add(buildStatisticsRow("Infrastructure Errors:", cs.InfrastructureErrors, "", Color.Red));
            t.Rows.Add(buildStatisticsRow("Bugs:", cs.Bugs, "", Color.Red));
            t.Rows.Add(buildStatisticsRow("Memoryout:", cs.MemoryOuts, "", Color.Black));
            t.Rows.Add(buildStatisticsRow("Timeout:", cs.Timeouts, "", Color.Black));
            t.Rows.Add(buildStatisticsRow("Total time (SAT):", TimeSpan.FromSeconds(timesat), "", Color.Black));
            t.Rows.Add(buildStatisticsRow("Total time (UNSAT):", TimeSpan.FromSeconds(timeunsat), "", Color.Black));
            t.Rows.Add(buildStatisticsRow("Avg. time (SAT/UNSAT):", (timesat + timeunsat) /
                                                                    (sat + unsat), "sec.", Color.Black));
            t.Rows.Add(buildStatisticsRow("Overperformers:", cs.Properties.ContainsKey(Z3Domain.KeyOverperformed) ? cs.Properties[Z3Domain.KeyOverperformed] : "0", "", Color.Black));
            t.Rows.Add(buildStatisticsRow("Underperformers:", cs.Properties.ContainsKey(Z3Domain.KeyUnderperformed) ? cs.Properties[Z3Domain.KeyUnderperformed] : "0", "", Color.Black));

            p.Controls.Add(t);
            return p;
        }

        TabPanel buildListTab(string title, AlertLevel level, List<string> items, string toolTip)
        {
            TabPanel result = new TabPanel();
            TabHeaderTemplate htm = new TabHeaderTemplate(items.Count == 0 ? AlertLevel.None : level, title, toolTip);
            if (items.Count > 0) htm._title += " (" + items.Count + ")";
            result.HeaderTemplate = htm;
            result.ContentTemplate = new TabContentTemplate(new List<string>());
            result.Controls.Add(buildList(items));
            return result;
        }

        void buildJobPanel(ExperimentViewModel exp, ExperimentStatusSummary expStatus, ExperimentAlerts alerts, string category, string alliswelltext)
        {
            AlertSet catAlerts = alerts[category];

            TabContainer tc = new TabContainer();
            tc.Height = 250;
            tc.ScrollBars = ScrollBars.Vertical;

            tc.Tabs.Add(buildSummaryTab(category, alliswelltext, alerts));
            tc.Tabs.Add(buildStatsTab(exp, expStatus, category));

            tc.Tabs.Add(buildListTab("Errors", AlertLevel.Warning, GetStatuses(expStatus.ErrorsByCategory, category), "A benchmark is classified as erroneous when its return value is non-zero (except for memory outs)."));
            tc.Tabs.Add(buildListTab("Bugs", AlertLevel.Critical, GetStatuses(expStatus.BugsByCategory, category), "A benchmark is classified as buggy when its result does not agree with its annotation."));
            tc.Tabs.Add(buildListTab("Underperformers", AlertLevel.None, expStatus.TagsByCategory.ContainsKey(Z3Domain.TagUnderperformers) ? GetStatuses(expStatus.TagsByCategory[Z3Domain.TagUnderperformers], category) : new List<string>(), "A benchmark underperforms when it has SAT/UNSAT annotations and some of them were not achieved."));
            tc.Tabs.Add(buildListTab("Dippers", AlertLevel.None, GetStatuses(expStatus.DippersByCategory, category), "A benchmark is classified as a dipper when it takes more than 10x more time than in a reference job (usually the previous)."));

            phMain.Controls.Add(tc);
        }

        private static List<string> GetStatuses(Dictionary<string, List<string>> dict, string cat)
        {
            if (dict == null || !dict.ContainsKey(cat)) return new List<string>();
            return dict[cat];
        }

        public Control buildFooter(string category)
        {
            Panel space = new Panel();
            space.Height = 15;
            phMain.Controls.Add(space);

            Panel p = new Panel();
            p.Style["text-align"] = "justify";

            Label l = new Label();
            l.Text = "Categories: ";
            l.Font.Size = 8;
            l.Font.Name = "helvetica";
            p.Controls.Add(l);

            if (category == "")
            {
                l = new Label();
                l.Text = "Overall";
                l.Font.Size = 8;
                l.Font.Name = "helvetica";
                l.Font.Bold = true;
                l.ForeColor = Color.Green;
                p.Controls.Add(l);
            }
            else
            {
                HyperLink h = new HyperLink();
                h.Text = "Overall";
                h.NavigateUrl = selfLink("");
                h.Style["text-decoration"] = "none";
                h.Font.Size = 8;
                h.Font.Name = "helvetica";
                p.Controls.Add(h);
            }

            foreach (string cat in vm.Categories)
            {
                l = new Label();
                l.Text += "&nbsp;| ";
                p.Controls.Add(l);

                if (category == cat)
                {
                    l = new Label();
                    l.Text = cat;
                    l.Font.Size = 8;
                    l.Font.Name = "helvetica";
                    l.Font.Bold = true;
                    l.ForeColor = Color.Green;
                    p.Controls.Add(l);
                }
                else
                {
                    HyperLink h = new HyperLink();
                    h.Text = cat;
                    h.NavigateUrl = selfLink(cat);
                    h.Style["text-decoration"] = "none";
                    h.Font.Size = 8;
                    h.Font.Name = "helvetica";
                    p.Controls.Add(h);
                }
            }

            return p;
        }

        public async void buildCategoryPanels()
        {
            string limit_str = Request.Params.Get("limit");
            if (limit_str != null)
                _listLimit = Convert.ToUInt32(limit_str);

            string category = Request.Params.Get("cat");
            if (category == null) category = "";

            string jobid = Request.Params.Get("job");

            phTop.Controls.Add(MakeDaySelectPanel());

            if (jobid == null || jobid == "")
            {
                if (category == "" || vm.Categories.Contains(category))
                {
                    var exp = vm.GetLastExperiment();
                    var statusSummary = await vm.GetStatusSummary(exp.Id);

                    ExperimentAlerts alerts = new ExperimentAlerts(exp.Summary, statusSummary, Request.FilePath);
                    string alliswelltext = (category == "") ? "All is well everywhere!" : "All is well in this category.";
                    buildCategoryPanel(category != "" ? category : "OVERALL", category, category, false, true, false, true, "", alerts);

                    buildJobPanel(exp, statusSummary, alerts, category, alliswelltext);
                }
                else
                {
                    Label l = new Label();
                    l.Text = "Category not found: " + category;
                    phMain.Controls.Add(l);
                }
            }
            else
            {
                try
                {
                    int id = int.Parse(jobid, CultureInfo.InvariantCulture);
                    ExperimentViewModel exp = vm.GetExperiment(id);
                    var statusSummary = await vm.GetStatusSummary(id);

                    if (category == "" || vm.Categories.Contains(category))
                    {
                        ExperimentAlerts alerts = new ExperimentAlerts(exp.Summary, statusSummary, Request.FilePath);
                        string alliswelltext = (category == "") ? "All is well everywhere!" : "All is well in this category.";
                        buildJobPanel(exp, statusSummary, alerts, category, alliswelltext);
                    }
                    else
                    {
                        Label l = new Label();
                        l.Text = "Category not found: " + category;
                        phMain.Controls.Add(l);
                    }
                }
                catch (FileNotFoundException ex)
                {
                    Label l = new Label();
                    l.Text = "Error: There is no job #" + jobid + " (" + ex.Message + ").";
                    phMain.Controls.Add(l);
                }
            }

            phMain.Controls.Add(buildFooter(category));
        }
    }
}