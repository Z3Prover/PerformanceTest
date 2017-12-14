using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI.DataVisualization;
using System.Web.UI.DataVisualization.Charting;

namespace PerformanceTest
{
    public class Charts
    {
        public static Chart BuildComparisonChart(string prefix, Comparison comparison)
        {
            Chart chart = new Chart();

            Title ttle = new Title((prefix == "") ? "Overall" : "/" + prefix.Replace('|', '/'));
            ttle.Font = new Font(ttle.Font, FontStyle.Bold);
            chart.Titles.Add(ttle);

            chart.Height = 600;
            chart.Width = 525;

            ChartArea ca = new ChartArea("ScatterPlot");

            ca.AxisX.Minimum = 0.1;
            ca.AxisX.IsLogarithmic = true;
            ca.AxisX.LogarithmBase = 10;
            ca.AxisX.IsLabelAutoFit = true;
            ca.AxisX.LabelAutoFitStyle = LabelAutoFitStyles.None;
            ca.AxisX.LabelAutoFitMinFontSize = 8;
            ca.AxisX.LabelAutoFitMaxFontSize = 8;
            ca.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dash;

            ca.AxisY.Minimum = 0.1;
            ca.AxisY.IsLogarithmic = true;
            ca.AxisY.LogarithmBase = 10;
            ca.AxisY.IsLabelAutoFit = true;
            ca.AxisY.LabelAutoFitStyle = LabelAutoFitStyles.None;
            ca.AxisY.LabelAutoFitMinFontSize = 8;
            ca.AxisY.LabelAutoFitMaxFontSize = 8;
            ca.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;

            chart.ChartAreas.Add(ca);

            if (!comparison.HasJobs)
            {
                ca.AxisX.Maximum = 10000;
                ca.AxisY.Maximum = 10000;

                ca.AxisX.Title = comparison.NameX;
                ca.AxisY.Title = comparison.NameY;

                Series ser = new Series("Dummy");
                ser.ChartArea = ca.Name;
                ser.ChartType = SeriesChartType.Point;
                ser.Color = Color.Blue;
                ser.MarkerSize = 2;
                ser.MarkerStyle = MarkerStyle.Cross;
                ser.IsVisibleInLegend = false;
                ser.Points.AddXY(1, 1);
                chart.Series.Add(ser);
            }
            else
            {
                Legend lgnd = new Legend();
                lgnd.Docking = Docking.Top;
                lgnd.IsDockedInsideChartArea = false;
                lgnd.Alignment = StringAlignment.Center;
                lgnd.DockedToChartArea = ca.Name;
                lgnd.Name = "StatisticsLegend";
                chart.Legends.Add(lgnd);

                ca.AxisX.Title = comparison.NameX;
                ca.AxisY.Title = comparison.NameY;

                ca.AxisX.Minimum = comparison.MinX;
                ca.AxisY.Minimum = comparison.MinY;
                ca.AxisX.Maximum = comparison.MaxX;
                ca.AxisY.Maximum = comparison.MaxY;

                ca.AxisX.CustomLabels.Add(new CustomLabel(Math.Log10(comparison.TimeOutX) - 0.2, Math.Log10(comparison.TimeOutX) + 0.2, "", 0, LabelMarkStyle.None, GridTickTypes.None));
                ca.AxisX.CustomLabels.Add(new CustomLabel(Math.Log10(comparison.MemOutX) - 0.2, Math.Log10(comparison.MemOutX) + 0.2, "M", 0, LabelMarkStyle.SideMark, GridTickTypes.None));
                ca.AxisX.CustomLabels.Add(new CustomLabel(Math.Log10(comparison.ErrorX) - 0.2, Math.Log10(comparison.ErrorX) + 0.2, "E", 0, LabelMarkStyle.SideMark, GridTickTypes.None));
                ca.AxisY.CustomLabels.Add(new CustomLabel(Math.Log10(comparison.TimeOutY) - 0.2, Math.Log10(comparison.TimeOutY) + 0.2, "", 0, LabelMarkStyle.None, GridTickTypes.None));
                ca.AxisY.CustomLabels.Add(new CustomLabel(Math.Log10(comparison.MemOutY) - 0.2, Math.Log10(comparison.MemOutY) + 0.2, "M", 0, LabelMarkStyle.SideMark, GridTickTypes.None));
                ca.AxisY.CustomLabels.Add(new CustomLabel(Math.Log10(comparison.ErrorY) - 0.2, Math.Log10(comparison.ErrorY) + 0.2, "E", 0, LabelMarkStyle.SideMark, GridTickTypes.None));

                ca.AxisX.CustomLabels.Add(new CustomLabel(-1.2, -0.8, "0.1", 0, LabelMarkStyle.SideMark, GridTickTypes.TickMark));
                ca.AxisX.CustomLabels.Add(new CustomLabel(-0.1, 0.105, "1", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
                ca.AxisX.CustomLabels.Add(new CustomLabel(0.9, 1.1, "10", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
                ca.AxisX.CustomLabels.Add(new CustomLabel(1.8, 2.2, "100", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
                ca.AxisX.CustomLabels.Add(new CustomLabel(2.8, 3.2, "1K", 0, LabelMarkStyle.SideMark, GridTickTypes.All));

                ca.AxisY.CustomLabels.Add(new CustomLabel(-1.2, -0.8, "0.1", 0, LabelMarkStyle.SideMark, GridTickTypes.TickMark));
                ca.AxisY.CustomLabels.Add(new CustomLabel(-0.1, 0.105, "1", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
                ca.AxisY.CustomLabels.Add(new CustomLabel(0.9, 1.1, "10", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
                ca.AxisY.CustomLabels.Add(new CustomLabel(1.8, 2.2, "100", 0, LabelMarkStyle.SideMark, GridTickTypes.All));
                ca.AxisY.CustomLabels.Add(new CustomLabel(2.8, 3.2, "1K", 0, LabelMarkStyle.SideMark, GridTickTypes.All));

                Series serTimeout = new Series("Timeouts");
                serTimeout.ChartArea = ca.Name;
                serTimeout.ChartType = SeriesChartType.FastLine;
                serTimeout.Color = Color.Black;
                serTimeout.Points.AddXY(ca.AxisX.Minimum, comparison.TimeOutY);
                serTimeout.Points.AddXY(comparison.TimeOutX, comparison.TimeOutY);
                serTimeout.Points.AddXY(comparison.TimeOutX, ca.AxisY.Minimum);
                serTimeout.IsVisibleInLegend = false;
                chart.Series.Add(serTimeout);

                Series serMemout = new Series("Memouts");
                serMemout.ChartArea = ca.Name;
                serMemout.ChartType = SeriesChartType.FastLine;
                serMemout.Color = Color.Orange;
                serMemout.Points.AddXY(ca.AxisX.Minimum, comparison.MemOutY);
                serMemout.Points.AddXY(comparison.MemOutX, comparison.MemOutY);
                serMemout.Points.AddXY(comparison.MemOutX, ca.AxisY.Minimum);
                serMemout.IsVisibleInLegend = false;
                chart.Series.Add(serMemout);

                Series serErrors = new Series("Errors");
                serErrors.ChartArea = ca.Name;
                serErrors.ChartType = SeriesChartType.FastLine;
                serErrors.Color = Color.Red;
                serErrors.Points.AddXY(ca.AxisX.Minimum, comparison.ErrorY);
                serErrors.Points.AddXY(comparison.ErrorX, comparison.ErrorY);
                serErrors.Points.AddXY(comparison.ErrorX, ca.AxisY.Minimum);
                serErrors.IsVisibleInLegend = false;
                chart.Series.Add(serErrors);

                Series serDiag = new Series("Diagonal");
                serDiag.ChartArea = ca.Name;
                serDiag.ChartType = SeriesChartType.FastLine;
                serDiag.Color = Color.Black;
                serDiag.Points.AddXY(ca.AxisX.Minimum, ca.AxisY.Minimum);
                serDiag.Points.AddXY(comparison.ErrorX, comparison.ErrorY);
                serDiag.IsVisibleInLegend = false;
                chart.Series.Add(serDiag);

                Series ser = new Series("Equal outcome");
                ser.ChartArea = ca.Name;
                ser.ChartType = SeriesChartType.Point;
                ser.Color = Color.Blue;
                ser.MarkerSize = 5;
                ser.MarkerStyle = MarkerStyle.Cross;
                ser.MarkerBorderWidth = 0;

                Series serBetter = new Series("Better outcome");
                serBetter.ChartArea = ca.Name;
                serBetter.ChartType = SeriesChartType.Point;
                serBetter.Color = Color.Green;
                serBetter.MarkerSize = 5;
                serBetter.MarkerStyle = MarkerStyle.Cross;
                serBetter.MarkerBorderWidth = 0;

                Series serWorse = new Series("Worse outcome");
                serWorse.ChartArea = ca.Name;
                serWorse.ChartType = SeriesChartType.Point;
                serWorse.Color = Color.Red;
                serWorse.MarkerSize = 5;
                serWorse.MarkerStyle = MarkerStyle.Cross;
                serWorse.MarkerBorderWidth = 0;

                double avg_speedup_x_time = 0.0;
                double avg_speedup_y_time = 0.0;
                int avg_speedup_num_points = 0;
                foreach (Comparison.Point p in comparison.Datapoints)
                {
                    switch (p.type)
                    {
                        case Comparison.PointType.BETTER: serBetter.Points.AddXY(p.x, p.y); ; break;
                        case Comparison.PointType.WORSE: serWorse.Points.AddXY(p.x, p.y); ; break;
                        default:
                            ser.Points.AddXY(p.x, p.y);

                            if (p.x != comparison.MemOutX && p.x != comparison.TimeOutX && p.x != comparison.ErrorX &&
                                p.y != comparison.MemOutY && p.y != comparison.TimeOutY && p.y != comparison.ErrorY)
                            {
                                avg_speedup_x_time += p.x;
                                avg_speedup_y_time += p.y;
                                avg_speedup_num_points++;
                            }
                            break;
                    }
                    // if (p.tooltip != null && p.tooltip != "") ser.Points.Last().ToolTip = p.tooltip;
                }

                chart.Series.Add(ser);
                chart.Series.Add(serBetter);
                chart.Series.Add(serWorse);

                double avg_speedup = avg_speedup_x_time / avg_speedup_y_time;
                TextAnnotation ann = new TextAnnotation();
                ann.Text = String.Format("avg speedup {0:n2}", avg_speedup);
                ann.ForeColor = (avg_speedup >= 1.0) ? Color.Green : Color.Red;
                ann.Font = new Font(ttle.Font, FontStyle.Bold);
                ann.IsSizeAlwaysRelative = false;
                ann.AxisX = ca.AxisX;
                ann.AxisY = ca.AxisY;
                ann.AnchorX = comparison.TimeOutX / 3.8;
                ann.AnchorY = 0.15;
                ann.AnchorAlignment = ContentAlignment.MiddleCenter;
                chart.Annotations.Add(ann);
            }

            return chart;
        }

        public static Chart BuildComparisonHistogramm(Comparison cmp)
        {
            Chart chart = new Chart();

            Title ttle = new Title("Dispersion Analysis");
            ttle.Font = new Font(ttle.Font, FontStyle.Bold);
            chart.Titles.Add(ttle);

            chart.Height = 400;
            chart.Width = 450;

            ChartArea ca = new ChartArea("Histogramm");

            ca.AxisX.LabelAutoFitStyle = LabelAutoFitStyles.LabelsAngleStep30;
            ca.AxisX.IsLabelAutoFit = true;
            ca.AxisX.LabelAutoFitMinFontSize = 8;
            ca.AxisX.LabelAutoFitMaxFontSize = 8;
            ca.AxisX.MajorGrid.Enabled = false;
            ca.AxisX.Minimum = -0.5;
            ca.AxisX.Maximum = 10.5;

            ca.AxisY.IsLogarithmic = true;
            ca.AxisY.LogarithmBase = 10;
            ca.AxisY.Minimum = 1;
            ca.AxisY.LabelAutoFitMinFontSize = 8;
            ca.AxisY.LabelAutoFitMaxFontSize = 8;
            ca.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;

            ca.AxisX.CustomLabels.Add(new CustomLabel(-0.5, 0.5, "Lost", 0, LabelMarkStyle.SideMark, GridTickTypes.TickMark));
            ca.AxisX.CustomLabels.Add(new CustomLabel(0.5, 1.5, "[>-T;-500]", 0, LabelMarkStyle.SideMark, GridTickTypes.TickMark));
            ca.AxisX.CustomLabels.Add(new CustomLabel(1.5, 2.5, "[-500;-50]", 0, LabelMarkStyle.SideMark, GridTickTypes.TickMark));
            ca.AxisX.CustomLabels.Add(new CustomLabel(2.5, 3.5, "[-50;-5]", 0, LabelMarkStyle.SideMark, GridTickTypes.TickMark));
            ca.AxisX.CustomLabels.Add(new CustomLabel(3.5, 4.5, "[-5;-1]", 0, LabelMarkStyle.SideMark, GridTickTypes.TickMark));
            ca.AxisX.CustomLabels.Add(new CustomLabel(4.5, 5.5, "[-.5;+.5]", 0, LabelMarkStyle.SideMark, GridTickTypes.TickMark));
            ca.AxisX.CustomLabels.Add(new CustomLabel(5.5, 6.5, "[+1;+5]", 0, LabelMarkStyle.SideMark, GridTickTypes.TickMark));
            ca.AxisX.CustomLabels.Add(new CustomLabel(6.5, 7.5, "[+5;+50]", 0, LabelMarkStyle.SideMark, GridTickTypes.TickMark));
            ca.AxisX.CustomLabels.Add(new CustomLabel(7.5, 8.5, "[+50;+500]", 0, LabelMarkStyle.SideMark, GridTickTypes.TickMark));
            ca.AxisX.CustomLabels.Add(new CustomLabel(8.5, 9.5, "[+500;+T]", 0, LabelMarkStyle.SideMark, GridTickTypes.TickMark));
            ca.AxisX.CustomLabels.Add(new CustomLabel(9.5, 10.5, "Gained", 0, LabelMarkStyle.SideMark, GridTickTypes.TickMark));

            chart.ChartAreas.Add(ca);

            Legend lgnd = new Legend();
            lgnd.Docking = Docking.Top;
            lgnd.IsDockedInsideChartArea = false;
            lgnd.Alignment = StringAlignment.Center;
            lgnd.DockedToChartArea = ca.Name;
            lgnd.Name = "StatisticsLegend";
            lgnd.Font = new Font(lgnd.Font.FontFamily, 8, lgnd.Font.Style, GraphicsUnit.Point);
            chart.Legends.Add(lgnd);

            Series ser = new Series("Dispersion");
            ser.ChartArea = ca.Name;
            ser.ChartType = SeriesChartType.Column;
            ser.IsVisibleInLegend = false;

            Series serCumulative = new Series("Cumulative");
            serCumulative.ChartArea = ca.Name;
            serCumulative.ChartType = SeriesChartType.Spline;
            serCumulative.Color = Color.Blue;
            serCumulative["LineTension"] = "0.05";
            serCumulative.IsVisibleInLegend = true;

            serCumulative.Points.AddXY(-0.5, 0);

            uint cumCount = 0;

            if (!cmp.HasJobs)
            {
                for (uint i = 0; i < 9; i++)
                    ser.Points.Add(1);
            }
            else
            {
                List<uint> hg = cmp.Histogramm;
                uint column = 0;

                foreach (uint p in hg)
                {
                    ser.Points.AddXY(column, p == 0 ? 1 : p);
                    double prog = column / (double)hg.Count;
                    ser.Points.Last().Color = Color.FromArgb((int)(255 * (1.0 - prog)), (int)(255 * prog), 0);

                    cumCount += p;
                    serCumulative.Points.AddXY(column, cumCount);
                    column++;
                }
            }

            chart.Series.Add(ser);
            chart.Series.Add(serCumulative);


            if (cumCount > 0)
            {
                Series serCD = new Series("Typical");
                serCD.ChartArea = ca.Name;
                serCD.ChartType = SeriesChartType.Spline;
                serCD.Color = Color.Gray;
                serCD.BorderDashStyle = ChartDashStyle.Dash;
                serCD.IsVisibleInLegend = true;

                serCD["LineTension"] = "0.05";

                // CMW: The numbers here are from reference comparison #1598/#1599
                // and they are supposed to represent typical cluster dispersion
                // (without any changes to the binary). This data may be wrong.
                serCD.Points.AddXY(0.0, (2.0 / 101663) * cumCount);
                serCD.Points.AddXY(1.0, (2.0 / 101663) * cumCount);
                serCD.Points.AddXY(2.0, (20.0 / 101663) * cumCount);
                serCD.Points.AddXY(3.0, (250.0 / 101663) * cumCount);
                serCD.Points.AddXY(4.0, (1000.0 / 101663) * cumCount);
                serCD.Points.AddXY(5.0, (100000.0 / 101663) * cumCount);
                serCD.Points.AddXY(6.0, (1000.0 / 101663) * cumCount);
                serCD.Points.AddXY(7.0, (250.0 / 101663) * cumCount);
                serCD.Points.AddXY(8.0, (20.0 / 101663) * cumCount);
                serCD.Points.AddXY(9.0, (2.0 / 101663) * cumCount);
                serCD.Points.AddXY(10.0, (2.0 / 101663) * cumCount);

                chart.Series.Add(serCD);


                Series serSTD = new Series("Std. Dev.");
                serSTD.ChartArea = ca.Name;
                serSTD.ChartType = SeriesChartType.Line;
                serSTD.MarkerSize = 5;
                serSTD.MarkerStyle = MarkerStyle.Diamond;
                serSTD.Color = Color.Purple;
                serSTD.IsVisibleInLegend = true;

                double mean = cmp.Statistics.DeltaMean;
                double mean_log = Math.Abs(mean) < 0.1 ? 0.0 : Math.Sign(mean) * Math.Log10(Math.Abs(10 * mean)) / Math.Log10(10);

                double std = cmp.Statistics.DeltaSTD;
                double std_1_log = Math.Abs(std) < 0.1 ? 0.0 : Math.Sign(std) * Math.Log10(Math.Abs(10 * std)) / Math.Log10(10);
                double std_2_log = Math.Abs(std * 2.0) < 0.1 ? 0.0 : Math.Sign(std) * Math.Log10(Math.Abs(20 * std)) / Math.Log10(10);
                double std_3_log = Math.Abs(std * 3.0) < 0.1 ? 0.0 : Math.Sign(std) * Math.Log10(Math.Abs(30 * std)) / Math.Log10(10);

                serSTD.Points.AddXY(5.0 + mean_log - std_3_log, cumCount * 2);
                serSTD.Points.Last().ToolTip = "-3 Std. Dev.";
                serSTD.Points.Last().Label = "-3\u03C3";
                serSTD.Points.AddXY(5.0 + mean_log - std_2_log, cumCount * 2);
                serSTD.Points.Last().ToolTip = "-2 Std. Dev.";
                serSTD.Points.AddXY(5.0 + mean_log - std_1_log, cumCount * 2);
                serSTD.Points.Last().ToolTip = "-1 Std. Dev.";
                serSTD.Points.Last().Label = "-\u03C3";
                serSTD.Points.AddXY(5.0 + mean_log, cumCount * 2);
                serSTD.Points.Last().ToolTip = "Mean";
                serSTD.Points.AddXY(5.0 + mean_log + std_1_log, cumCount * 2);
                serSTD.Points.Last().ToolTip = "+1 Std. Dev.";
                serSTD.Points.Last().Label = "+\u03C3";
                serSTD.Points.AddXY(5.0 + mean_log + std_2_log, cumCount * 2);
                serSTD.Points.Last().ToolTip = "+2 Std. Dev.";
                serSTD.Points.AddXY(5.0 + mean_log + std_3_log, cumCount * 2);
                serSTD.Points.Last().ToolTip = "+3 Std. Dev.";
                serSTD.Points.Last().Label = "+3\u03C3";
                serSTD.SmartLabelStyle.Enabled = true;
                serSTD.SmartLabelStyle.IsMarkerOverlappingAllowed = true;
                serSTD.SmartLabelStyle.IsOverlappedHidden = true;
                serSTD.SmartLabelStyle.MovingDirection = LabelAlignmentStyles.Bottom;
                serSTD.Font = new Font(serSTD.Font.FontFamily, 6);

                chart.Series.Add(serSTD);

                Series serPs = new Series("Percentiles");
                serPs.ChartArea = ca.Name;
                serPs.ChartType = SeriesChartType.Line;
                serPs.MarkerSize = 5;
                serPs.MarkerStyle = MarkerStyle.Diamond;
                serPs.Color = Color.Violet;
                serPs.IsVisibleInLegend = true;
                serPs.Font = new Font(serSTD.Font.FontFamily, 6);

                double p1 = cmp.Statistics.DeltaP(1);
                double p25 = cmp.Statistics.DeltaP(25);
                double p50 = cmp.Statistics.DeltaP(50);
                double p75 = cmp.Statistics.DeltaP(75);
                double p99 = cmp.Statistics.DeltaP(99);

                double p1_log = Math.Abs(p1) < 0.1 ? 0.0 : Math.Sign(p1) * Math.Log10(Math.Abs(10 * p1)) / Math.Log10(10);
                double p25_log = Math.Abs(p25) < 0.1 ? 0.0 : Math.Sign(p25) * Math.Log10(Math.Abs(10 * p25)) / Math.Log10(10);
                double p50_log = Math.Abs(p50) < 0.1 ? 0.0 : Math.Sign(p50) * Math.Log10(Math.Abs(10 * p50)) / Math.Log10(10);
                double p75_log = Math.Abs(p75) < 0.1 ? 0.0 : Math.Sign(p75) * Math.Log10(Math.Abs(10 * p75)) / Math.Log10(10);
                double p99_log = Math.Abs(p99) < 0.1 ? 0.0 : Math.Sign(p99) * Math.Log10(Math.Abs(10 * p99)) / Math.Log10(10);

                serPs.Points.AddXY(5.0 + p1_log, cumCount * 8);
                serPs.Points.Last().ToolTip = "P1";
                serPs.Points.Last().Label = "P1";
                serPs.Points.AddXY(5.0 + p25_log, cumCount * 8);
                serPs.Points.Last().ToolTip = "P25";
                serPs.Points.AddXY(5.0 + p50_log, cumCount * 8);
                serPs.Points.Last().ToolTip = "P50";
                serPs.Points.Last().Label = "P50";
                serPs.Points.AddXY(5.0 + p75_log, cumCount * 8);
                serPs.Points.Last().ToolTip = "P75";
                serPs.Points.AddXY(5.0 + p99_log, cumCount * 8);
                serPs.Points.Last().ToolTip = "P99";
                serPs.Points.Last().Label = "P99";
                serPs.SmartLabelStyle.Enabled = true;
                serPs.SmartLabelStyle.IsMarkerOverlappingAllowed = true;
                serPs.SmartLabelStyle.IsOverlappedHidden = true;
                serPs.SmartLabelStyle.MovingDirection = LabelAlignmentStyles.Bottom;

                chart.Series.Add(serPs);
            }

            return chart;
        }
    }
}
