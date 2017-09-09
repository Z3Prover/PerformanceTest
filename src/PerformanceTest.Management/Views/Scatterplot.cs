using Measurement;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Input;

namespace PerformanceTest.Management
{
    public partial class Scatterplot : Form
    {
        private readonly IUIService uiService;
        private readonly CompareExperimentsViewModel vm;
        private readonly ExperimentStatusViewModel experiment1, experiment2;
        private string category = "";
        private bool fancy = false;
        private double axisMinimum = 0.1;
        private uint axisMaximum = 1800;
        private uint errorLine = 100;
        private uint timeoutX = 1800;
        private uint timeoutY = 1800;
        private uint timeoutXmin = 1800;
        private uint timeoutXmax = 1800;
        private uint timeoutYmin = 1800;
        private uint timeoutYmax = 1800;
        private uint memoutX = 1800;
        private uint memoutY = 1800;
        private Dictionary<string, int> classes = new Dictionary<string, int>();


        public Scatterplot(CompareExperimentsViewModel vm, ExperimentStatusViewModel exp1, ExperimentStatusViewModel exp2, double timeout1, double timeout2, double memout1, double memout2,  IUIService uiService)
        {
            if (vm == null) throw new ArgumentNullException("vm");
            if (exp1 == null) throw new ArgumentNullException("exp1");
            if (exp2 == null) throw new ArgumentNullException("exp2");
            if (uiService == null) throw new ArgumentNullException("uiService");

            this.uiService = uiService;
            this.StartPosition = FormStartPosition.CenterScreen;
            InitializeComponent();
            this.vm = vm;
            this.experiment1 = exp1;
            this.experiment2 = exp2;

            string category1 = experiment1.Category == null ? "" : experiment1.Category;
            string category2 = experiment2.Category == null ? "" : experiment2.Category;
            category = (category1 == category2) ? category1 : category1 + " -vs- " + category2;
            timeoutX = (uint)timeout1;
            timeoutY = (uint)timeout2;
            timeoutXmin = timeoutXmax = timeoutX;
            timeoutYmin = timeoutYmax = timeoutY;
            memoutX = (uint)memout1;
            memoutY = (uint)memout2;
            UpdateStatus(true);

            vm.PropertyChanged += (s, a) =>
            {
                if (a.PropertyName == nameof(vm.CompareItems))
                {
                    updateTimeouts();
                    SetupChart();
                    RefreshChart();
                }
            };
        }
        private void updateTimeouts()
        {
            bool empty = vm.CompareItems == null || vm.CompareItems.Length == 0;

            if (rbNonNormalized.Checked)
            {
                timeoutXmin = empty ? timeoutX : vm.CompareItems.Min(item => item.Results1.Status == ResultStatus.Timeout ? (uint)item.Results1.NormalizedCPUTime : UInt32.MaxValue);
                timeoutXmax = empty ? timeoutX : vm.CompareItems.Max(item => item.Results1.Status == ResultStatus.Timeout ? (uint)item.Results1.NormalizedCPUTime : UInt32.MinValue);
                if (timeoutXmin == UInt32.MaxValue && timeoutXmax == UInt32.MinValue)
                    timeoutXmin = timeoutXmax = timeoutX;

                timeoutYmin = empty ? timeoutY : vm.CompareItems.Min(item => item.Results2.Status == ResultStatus.Timeout ? (uint)item.Results2.NormalizedCPUTime : UInt32.MaxValue);
                timeoutYmax = empty ? timeoutY : vm.CompareItems.Max(item => item.Results2.Status == ResultStatus.Timeout ? (uint)item.Results2.NormalizedCPUTime : UInt32.MinValue);
                if (timeoutYmin == UInt32.MaxValue && timeoutYmax == UInt32.MinValue)
                    timeoutYmin = timeoutYmax = timeoutY;
            }
            else
            {
                timeoutXmin = timeoutXmax = timeoutX;
                timeoutYmin = timeoutYmax = timeoutY;
            }
        }

        private void UpdateStatus(bool isBusy)
        {
            if (isBusy)
            {
                this.Text = string.Format("Plot: {0} vs {1} (working...)", experiment1.ID, experiment2.ID);
                Cursor = System.Windows.Forms.Cursors.AppStarting;
                gpOptions.Enabled = false;
            }
            else
            {
                this.Text = string.Format("Plot: {0} vs {1}", experiment1.ID, experiment2.ID);
                Cursor = System.Windows.Forms.Cursors.Default;
                gpOptions.Enabled = true;
            }
        }

        private void SetupChart()
        {
            chart.Legends.Clear();
            chart.Titles.Clear();

            axisMaximum = rbMemoryUsed.Checked ? memoutX : timeoutX;
            if (rbMemoryUsed.Checked && memoutY > axisMaximum) axisMaximum = memoutY;
            else if (!rbMemoryUsed.Checked && timeoutY > axisMaximum) axisMaximum = timeoutY;
            // Round max up to next order of magnitude.
            {
                uint orders = 0;
                uint temp = axisMaximum;
                while (temp > 0)
                {
                    temp = temp / 10;
                    orders++;
                }

                uint newmax = 1;
                for (uint i = 0; i < orders; i++)
                    newmax *= 10;

                if (newmax <= axisMaximum)
                {
                    // errorLine = ((newmax * 10) - newmax) / 2;
                    axisMaximum *= 10;
                }
                else
                {
                    // errorLine = axisMaximum + ((newmax - axisMaximum) / 2);
                    axisMaximum = newmax;
                }

                errorLine = axisMaximum;
            }

            Title t = new Title(category, Docking.Top);
            t.Font = new Font(FontFamily.GenericSansSerif, 16.0f, FontStyle.Bold);
            chart.Titles.Add(t);
            string xTitle = "Experiment #" + experiment1.ID + ": " + experiment1.Note;
            if (experiment1.Definition.AdaptiveRunMaxRepetitions != 1 || experiment1.Definition.AdaptiveRunMaxTimeInSeconds != 0) xTitle = xTitle + " (adaptive)";
            string yTitle = "Experiment #" + experiment2.ID + ": " + experiment2.Note;
            if (experiment2.Definition.AdaptiveRunMaxRepetitions != 1 || experiment2.Definition.AdaptiveRunMaxTimeInSeconds != 0) yTitle = yTitle + " (adaptive)";
            chart.ChartAreas[0].AxisX.Title = xTitle;
            chart.ChartAreas[0].AxisY.Title = yTitle;
            chart.ChartAreas[0].AxisY.TextOrientation = TextOrientation.Rotated270;
            chart.ChartAreas[0].AxisX.Minimum = axisMinimum;
            chart.ChartAreas[0].AxisX.Maximum = axisMaximum;
            chart.ChartAreas[0].AxisX.IsLogarithmic = true;
            chart.ChartAreas[0].AxisY.Minimum = axisMinimum;
            chart.ChartAreas[0].AxisY.Maximum = axisMaximum;
            chart.ChartAreas[0].AxisY.IsLogarithmic = true;
            chart.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            chart.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            chart.ChartAreas[0].AxisX.MinorGrid.Enabled = true;
            chart.ChartAreas[0].AxisY.MinorGrid.Enabled = true;
            chart.ChartAreas[0].AxisX.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            chart.ChartAreas[0].AxisY.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            chart.ChartAreas[0].AxisX.MinorGrid.LineColor = Color.LightGray;
            chart.ChartAreas[0].AxisY.MinorGrid.LineColor = Color.LightGray;
            chart.ChartAreas[0].AxisX.MinorGrid.Interval = 1;
            chart.ChartAreas[0].AxisY.MinorGrid.Interval = 1;

            chart.Series.Clear();

            chart.Series.Add("Timeout Markers");
            chart.Series[0].ChartType = SeriesChartType.FastLine;
            chart.Series[0].Color = Color.Green;
            chart.Series[0].BorderDashStyle = ChartDashStyle.Dash;
            if (rbMemoryUsed.Checked)
            {
                chart.Series[0].Points.AddXY(axisMinimum, memoutY);
                chart.Series[0].Points.AddXY(memoutX, memoutY);
                chart.Series[0].Points.AddXY(memoutX, axisMinimum);
            }
            else if (rbNormalized.Checked)
            {
                chart.Series[0].Points.AddXY(axisMinimum, timeoutYmin);
                chart.Series[0].Points.AddXY(timeoutXmin, timeoutYmin);
                chart.Series[0].Points.AddXY(timeoutXmin, axisMinimum);

                chart.Series[0].Points.AddXY(timeoutXmax, axisMinimum);
                chart.Series[0].Points.AddXY(timeoutXmax, timeoutYmax);
                chart.Series[0].Points.AddXY(axisMinimum, timeoutYmax);
            }
            else
            {
                chart.Series[0].Points.AddXY(axisMinimum, timeoutY);
                chart.Series[0].Points.AddXY(timeoutX, timeoutY);
                chart.Series[0].Points.AddXY(timeoutX, axisMinimum);
            }

            chart.Series.Add("Error Markers");
            chart.Series[1].ChartType = SeriesChartType.FastLine;
            chart.Series[1].Color = Color.Red;
            chart.Series[1].BorderDashStyle = ChartDashStyle.Dash;
            chart.Series[1].Points.AddXY(axisMinimum, errorLine);
            chart.Series[1].Points.AddXY(errorLine, errorLine);
            chart.Series[1].Points.AddXY(errorLine, axisMinimum);

            chart.Series.Add("Diagonal");
            chart.Series[2].ChartType = SeriesChartType.FastLine;
            chart.Series[2].Color = Color.Blue;
            chart.Series[2].BorderDashStyle = ChartDashStyle.Dash;
            chart.Series[2].Points.AddXY(axisMinimum, axisMinimum);
            chart.Series[2].Points.AddXY(axisMaximum, axisMaximum);

            classes.Clear();

            if (!fancy)
                addSeries("default");

            foreach (double d in new double[] { 5.0, 10.0 })
            {
                addSpeedupLine(chart, d, Color.LightBlue);
                addSpeedupLine(chart, 1.0 / d, Color.LightBlue);
            }
        }
        private void addSeries(string title)
        {
            if (fancy)
            {
                chart.Series.Add(title);
                Series newSeries = chart.Series.Last();
                int inx = chart.Series.Count - 1;
                int m3 = inx % 3;
                int d3 = inx / 3;
                newSeries.ChartType = SeriesChartType.Point;
                newSeries.MarkerStyle = MarkerStyle.Cross;

                newSeries.MarkerSize = 6;
                switch (m3)
                {
                    case 0: newSeries.MarkerColor = Color.FromArgb(0, 0, 255 / d3); break;
                    case 1: newSeries.MarkerColor = Color.FromArgb(0, 255 / d3, 0); break;
                    case 2: newSeries.MarkerColor = Color.FromArgb(255 / d3, 0, 0); break;
                }
                newSeries.XAxisType = AxisType.Primary;
                newSeries.YAxisType = AxisType.Primary;
            }
            else if (chart.Series.Count <= 6)
            {
                chart.Series.Add(title);
                Series newSeries = chart.Series.Last();

                newSeries.ChartType = SeriesChartType.FastPoint;
                newSeries.MarkerStyle = MarkerStyle.Cross;

                newSeries.MarkerSize = 6;
                newSeries.MarkerColor = Color.Blue;
                newSeries.XAxisType = AxisType.Primary;
                newSeries.YAxisType = AxisType.Primary;

                chart.Series.Add("Winners");
                newSeries = chart.Series.Last();
                newSeries.ChartType = SeriesChartType.FastPoint;

                newSeries.MarkerStyle = MarkerStyle.Cross;

                newSeries.MarkerSize = 6;
                newSeries.MarkerColor = Color.Green;
                newSeries.XAxisType = AxisType.Primary;
                newSeries.YAxisType = AxisType.Primary;

                chart.Series.Add("Losers");
                newSeries = chart.Series.Last();
                newSeries.ChartType = SeriesChartType.FastPoint;

                newSeries.MarkerStyle = MarkerStyle.Cross;

                newSeries.MarkerSize = 6;
                newSeries.MarkerColor = Color.OrangeRed;
                newSeries.XAxisType = AxisType.Primary;
                newSeries.YAxisType = AxisType.Primary;

                chart.Series.Add("Timeout");
                newSeries = chart.Series.Last();
                newSeries.ChartType = SeriesChartType.FastPoint;
                newSeries.MarkerStyle = MarkerStyle.Cross;
                newSeries.MarkerSize = 6;
                newSeries.MarkerColor = Color.Blue;
                newSeries.XAxisType = AxisType.Primary;
                newSeries.YAxisType = AxisType.Primary;

                chart.Series.Add("Timeout Winners");
                newSeries = chart.Series.Last();
                newSeries.ChartType = SeriesChartType.FastPoint;
                newSeries.MarkerStyle = MarkerStyle.Cross;
                newSeries.MarkerSize = 6;
                newSeries.MarkerColor = Color.Green;
                newSeries.XAxisType = AxisType.Primary;
                newSeries.YAxisType = AxisType.Primary;

                chart.Series.Add("Timeout Losers");
                newSeries = chart.Series.Last();
                newSeries.ChartType = SeriesChartType.FastPoint;
                newSeries.MarkerStyle = MarkerStyle.Cross;
                newSeries.MarkerSize = 6;
                newSeries.MarkerColor = Color.OrangeRed;
                newSeries.XAxisType = AxisType.Primary;
                newSeries.YAxisType = AxisType.Primary;
            }
        }
        private void RefreshChart()
        {
            double totalX = 0.0, totalY = 0.0;
            uint total = 0, y_faster = 0, y_slower = 0;

            var handle = uiService.StartIndicateLongOperation("Displaying scatter plot...");
            try
            {
                UpdateStatus(true);

                bool cksat = ckSAT.Checked;
                bool ckunsat = ckUNSAT.Checked;
                bool ckunk = ckUNKNOWN.Checked;
                bool ckbug = ckBUG.Checked;
                bool ckerror = ckERROR.Checked;
                bool cktime = ckTIME.Checked;
                bool ckmemory = ckMEMORY.Checked;
                if (vm.CompareItems != null)
                {
                    vm.CheckIgnorePrefix = ckIgnorePrefix.Checked;
                    vm.CheckIgnorePostfix = ckIgnorePostfix.Checked;
                    vm.CheckIgnoreCategory = ckIgnoreCategory.Checked;
                }
                if (vm.CompareItems != null && vm.CompareItems.Length > 0)
                {
                    foreach (var item in vm.CompareItems)
                    {
                        double x = 0.0, y = 0.0;
                        if (rbNonNormalized.Checked)
                        {
                            x = item.Results1.CPUTime;
                            y = item.Results2.CPUTime;
                        } else if (rbWallClock.Checked)
                        {
                            x = item.Results1.WallClockTime;
                            y = item.Results2.WallClockTime;
                        } else if (rbMemoryUsed.Checked)
                        {
                            x = item.Results1.MemorySizeMB;
                            y = item.Results2.MemorySizeMB;
                        } else
                        {
                            x = item.Results1.NormalizedCPUTime;
                            y = item.Results2.NormalizedCPUTime;
                        }

                        if (x < axisMinimum) x = axisMinimum;
                        if (y < axisMinimum) y = axisMinimum;

                        ResultStatus rc1 = item.Results1.Status;
                        ResultStatus rc2 = item.Results2.Status;
                        int res1 = item.Results1.Sat + item.Results1.Unsat;
                        int res2 = item.Results2.Sat + item.Results2.Unsat;

                        if ((!cksat && (item.Results1.Sat > 0 || item.Results2.Sat > 0)) ||
                             (!ckunsat && (item.Results1.Unsat > 0 || item.Results2.Unsat > 0)) ||
                             (!ckunk && ((rc1 == ResultStatus.Success && res1 == 0) || (rc2 == ResultStatus.Success && res2 == 0))) ||
                             (!ckbug && (rc1 == ResultStatus.Bug || rc2 == ResultStatus.Bug)) ||
                             (!ckerror && (rc1 == ResultStatus.Error || rc2 == ResultStatus.Error || rc1 == ResultStatus.InfrastructureError || rc2 == ResultStatus.InfrastructureError)) ||
                             (!cktime && (rc1 == ResultStatus.Timeout || rc2 == ResultStatus.Timeout)) ||
                             (!ckmemory && (rc1 == ResultStatus.OutOfMemory || rc2 == ResultStatus.OutOfMemory)))
                            continue;

                        if (rbMemoryUsed.Checked && (rc1 != ResultStatus.Success && rc1 != ResultStatus.OutOfMemory || x < memoutX && res1 == 0)
                            || (rbNonNormalized.Checked || rbWallClock.Checked) && (rc1 != ResultStatus.Success && rc1 != ResultStatus.Timeout || x != timeoutX && res1 == 0)
                            || rbNormalized.Checked && (rc1 != ResultStatus.Success && rc1 != ResultStatus.Timeout || res1 == 0 && (x < timeoutXmin || x > timeoutXmax)))
                            x = errorLine;
                        if (rbMemoryUsed.Checked && (rc2 != ResultStatus.Success && rc2 != ResultStatus.OutOfMemory || y < memoutY && res2 == 0)
                            || (rbNonNormalized.Checked || rbWallClock.Checked) && (rc2 != ResultStatus.Success && rc2 != ResultStatus.Timeout || y != timeoutY && res2 == 0)
                            || rbNormalized.Checked && (rc2 != ResultStatus.Success && rc2 != ResultStatus.Timeout || res2 == 0 && (y < timeoutYmin || y > timeoutYmax)))
                            y = errorLine;
                        if (rbMemoryUsed.Checked && (rc1 == ResultStatus.OutOfMemory)) x = memoutX;
                        if (rbMemoryUsed.Checked && (rc2 == ResultStatus.OutOfMemory)) y = memoutY;
                        if (rbMemoryUsed.Checked && rc1 != ResultStatus.OutOfMemory && rc2 != ResultStatus.OutOfMemory && x < memoutX && y < memoutY
                            || (rbNonNormalized.Checked || rbWallClock.Checked) && rc1 != ResultStatus.Timeout && rc2 != ResultStatus.Timeout && x < timeoutX && y < timeoutY
                            || rbNormalized.Checked && rc1 != ResultStatus.Timeout && rc2 != ResultStatus.Timeout && x < timeoutXmax && y < timeoutYmax)
                        {
                            totalX += x;
                            totalY += y;
                        }

                        if (fancy)
                        {
                            string name = item.Filename;
                            int inx = name.IndexOf('/', name.IndexOf('/') + 1);
                            string c = (inx > 0) ? name.Substring(0, inx) : name;

                            Series s;
                            int k;

                            if (classes.TryGetValue(c, out k))
                                s = chart.Series[k];
                            else
                            {
                                addSeries(c);
                                int l = chart.Series.Count - 1;
                                classes.Add(c, l);
                                s = chart.Series.Last();
                            }

                            int j = s.Points.AddXY(x, y);
                            s.Points.Last().ToolTip = name;
                        }
                        else
                        {
                            if ((item.Results1.Sat < item.Results2.Sat && item.Results1.Unsat == item.Results2.Unsat) ||
                               (item.Results1.Sat == item.Results2.Sat && item.Results1.Unsat < item.Results2.Unsat))
                            {
                                if (rbNormalized.Checked && (rc1 == ResultStatus.Timeout || rc2 == ResultStatus.Timeout))
                                    chart.Series[7].Points.AddXY(x, y);
                                else
                                    chart.Series[4].Points.AddXY(x, y);
                            }
                            else if ((item.Results1.Sat > item.Results2.Sat && item.Results1.Unsat == item.Results2.Unsat) ||
                                (item.Results1.Sat == item.Results2.Sat && item.Results1.Unsat > item.Results2.Unsat))
                            {
                                if (rbNormalized.Checked && (rc1 == ResultStatus.Timeout || rc2 == ResultStatus.Timeout))
                                    chart.Series[8].Points.AddXY(x, y);
                                else
                                    chart.Series[5].Points.AddXY(x, y);
                            }
                            else
                            {
                                if (rbNormalized.Checked && (rc1 == ResultStatus.Timeout || rc2 == ResultStatus.Timeout))
                                    chart.Series[6].Points.AddXY(x, y);
                                else
                                    chart.Series[3].Points.AddXY(x, y);
                            }
                        }

                        if (x > y) y_faster++; else if (y > x) y_slower++;
                        total++;
                    };
                }
            }
            finally
            {

                chart.Update();
                if(vm.CompareItems != null) UpdateStatus(false);
                uiService.StopIndicateLongOperation(handle);
            }

            double avgSpeedup = totalX / totalY;
            lblAvgSpeedup.Text = avgSpeedup.ToString("N3");
            if (avgSpeedup >= 1.0)
                lblAvgSpeedup.ForeColor = rbMemoryUsed.Checked ? Color.Red : Color.Green;
            else if (avgSpeedup < 1.0)
                lblAvgSpeedup.ForeColor = rbMemoryUsed.Checked ? Color.Green : Color.Red;

            lblTotal.Text = total.ToString();
            lblFaster.Text = y_faster.ToString();
            lblSlower.Text = y_slower.ToString();
        }
        private void scatterTest_Load(object sender, EventArgs e)
        {
            SetupChart();
            RefreshChart();
        }
        private void ckCheckedChanged(object sender, EventArgs e)
        {
            fancy = cbFancy.Checked;
            SetupChart();
            if (rbMemoryUsed.Checked)
            {
                lblAvgSpeedupTxt.Text = "Avg. memory used (excl. M/O):";
                label3.Text = "Y Less Memory";
                label5.Text = "Y More Memory";
            }
            else
            {
                lblAvgSpeedupTxt.Text = "Avg. speedup (excl. T/O):";
                label3.Text = "Y Faster";
                label5.Text = "Y Slower";
            }
            RefreshChart();
        }
        private void addSpeedupLine(Chart chart, double f, Color c)
        {
            Series s = chart.Series.Add("x" + f.ToString());
            s.ChartType = SeriesChartType.FastLine;
            s.Color = c;
            s.BorderDashStyle = ChartDashStyle.Dot;
            s.Points.AddXY(axisMinimum, axisMinimum * f);
            s.Points.AddXY(axisMaximum / f, axisMaximum);
            s.Points.AddXY(axisMaximum, axisMaximum);
        }
    }
}
