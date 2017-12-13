namespace PerformanceTest.Management
{
    partial class Scatterplot
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Title title1 = new System.Windows.Forms.DataVisualization.Charting.Title();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Scatterplot));
            this.chart = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.cbFancy = new System.Windows.Forms.CheckBox();
            this.lblAvgSpeedupTxt = new System.Windows.Forms.Label();
            this.lblAvgSpeedup = new System.Windows.Forms.Label();
            this.ckSAT = new System.Windows.Forms.CheckBox();
            this.ckUNSAT = new System.Windows.Forms.CheckBox();
            this.gpOptions = new System.Windows.Forms.GroupBox();
            this.ckMEMORY = new System.Windows.Forms.CheckBox();
            this.ckBUG = new System.Windows.Forms.CheckBox();
            this.ckERROR = new System.Windows.Forms.CheckBox();
            this.lblIgnore = new System.Windows.Forms.Label();
            this.ckIgnoreContainer = new System.Windows.Forms.CheckBox();
            this.ckIgnoreExtension = new System.Windows.Forms.CheckBox();
            this.ckIgnoreCategory = new System.Windows.Forms.CheckBox();
            this.lblSlower = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.lblFaster = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.lblTotal = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.lblDatapoints = new System.Windows.Forms.Label();
            this.ckTIME = new System.Windows.Forms.CheckBox();
            this.ckUNKNOWN = new System.Windows.Forms.CheckBox();
            this.rbNormalized = new System.Windows.Forms.RadioButton();
            this.rbNonNormalized = new System.Windows.Forms.RadioButton();
            this.rbWallClock = new System.Windows.Forms.RadioButton();
            this.rbMemoryUsed = new System.Windows.Forms.RadioButton();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.gbStatistics = new System.Windows.Forms.GroupBox();
            this.panel1 = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.chart)).BeginInit();
            this.gpOptions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.gbStatistics.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            //
            // chart
            //
            chartArea1.Name = "ChartArea1";
            this.chart.ChartAreas.Add(chartArea1);
            this.chart.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chart.Location = new System.Drawing.Point(0, 0);
            this.chart.Name = "chart";
            series1.ChartArea = "ChartArea1";
            series1.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.FastPoint;
            series1.Name = "Series1";
            this.chart.Series.Add(series1);
            this.chart.Size = new System.Drawing.Size(681, 695);
            this.chart.TabIndex = 0;
            this.chart.Text = "Scatterplot";
            title1.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            title1.Name = "Title1";
            title1.Text = "Title";
            this.chart.Titles.Add(title1);
            //
            // cbFancy
            //
            this.cbFancy.AutoSize = true;
            this.cbFancy.Location = new System.Drawing.Point(6, 19);
            this.cbFancy.Name = "cbFancy";
            this.cbFancy.Size = new System.Drawing.Size(55, 17);
            this.cbFancy.TabIndex = 1;
            this.cbFancy.Text = "Fancy";
            this.cbFancy.UseVisualStyleBackColor = true;
            this.cbFancy.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // lblAvgSpeedupTxt
            //
            this.lblAvgSpeedupTxt.AutoSize = true;
            this.lblAvgSpeedupTxt.Location = new System.Drawing.Point(6, 19);
            this.lblAvgSpeedupTxt.Name = "lblAvgSpeedupTxt";
            this.lblAvgSpeedupTxt.Size = new System.Drawing.Size(130, 13);
            this.lblAvgSpeedupTxt.TabIndex = 2;
            this.lblAvgSpeedupTxt.Text = "Avg. speedup (excl. T/O):";
            //
            // lblAvgSpeedup
            //
            this.lblAvgSpeedup.Location = new System.Drawing.Point(141, 19);
            this.lblAvgSpeedup.Name = "lblAvgSpeedup";
            this.lblAvgSpeedup.Size = new System.Drawing.Size(72, 16);
            this.lblAvgSpeedup.TabIndex = 3;
            this.lblAvgSpeedup.Text = "label1";
            this.lblAvgSpeedup.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // ckSAT
            //
            this.ckSAT.AutoSize = true;
            this.ckSAT.Checked = true;
            this.ckSAT.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ckSAT.Location = new System.Drawing.Point(6, 42);
            this.ckSAT.Name = "ckSAT";
            this.ckSAT.Size = new System.Drawing.Size(47, 17);
            this.ckSAT.TabIndex = 4;
            this.ckSAT.Text = "SAT";
            this.ckSAT.UseVisualStyleBackColor = true;
            this.ckSAT.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // ckUNSAT
            //
            this.ckUNSAT.AutoSize = true;
            this.ckUNSAT.Checked = true;
            this.ckUNSAT.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ckUNSAT.Location = new System.Drawing.Point(59, 42);
            this.ckUNSAT.Name = "ckUNSAT";
            this.ckUNSAT.Size = new System.Drawing.Size(63, 17);
            this.ckUNSAT.TabIndex = 5;
            this.ckUNSAT.Text = "UNSAT";
            this.ckUNSAT.UseVisualStyleBackColor = true;
            this.ckUNSAT.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // lblIgnore
            //
            this.lblIgnore.Location = new System.Drawing.Point(6, 92);
            this.lblIgnore.Name = "lblIgnore";
            this.lblIgnore.Size = new System.Drawing.Size(47, 18);
            this.lblIgnore.TabIndex = 6;
            this.lblIgnore.Text = "Ignore:";
            this.lblIgnore.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // ckIgnorePrefix
            //
            this.ckIgnoreContainer.AutoSize = true;
            this.ckIgnoreContainer.Checked = false;
            this.ckIgnoreContainer.CheckState = System.Windows.Forms.CheckState.Unchecked;
            this.ckIgnoreContainer.Location = new System.Drawing.Point(59, 92);
            this.ckIgnoreContainer.Name = "ckIgnorePrefix";
            this.ckIgnoreContainer.Size = new System.Drawing.Size(63, 18);
            this.ckIgnoreContainer.TabIndex = 7;
            this.ckIgnoreContainer.Text = "Container";
            this.ckIgnoreContainer.UseVisualStyleBackColor = true;
            this.ckIgnoreContainer.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // ckIgnorePostfix
            //
            this.ckIgnoreExtension.AutoSize = true;
            this.ckIgnoreExtension.Checked = false;
            this.ckIgnoreExtension.CheckState = System.Windows.Forms.CheckState.Unchecked;
            this.ckIgnoreExtension.Location = new System.Drawing.Point(128, 92);
            this.ckIgnoreExtension.Name = "ckIgnorePostfix";
            this.ckIgnoreExtension.Size = new System.Drawing.Size(63, 18);
            this.ckIgnoreExtension.TabIndex = 8;
            this.ckIgnoreExtension.Text = "Extension";
            this.ckIgnoreExtension.UseVisualStyleBackColor = true;
            this.ckIgnoreExtension.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // ckIgnoreCategory
            //
            this.ckIgnoreCategory.AutoSize = true;
            this.ckIgnoreCategory.Checked = false;
            this.ckIgnoreCategory.CheckState = System.Windows.Forms.CheckState.Unchecked;
            this.ckIgnoreCategory.Location = new System.Drawing.Point(208, 92);
            this.ckIgnoreCategory.Name = "ckIgnoreCategory";
            this.ckIgnoreCategory.Size = new System.Drawing.Size(63, 18);
            this.ckIgnoreCategory.TabIndex = 9;
            this.ckIgnoreCategory.Text = "Category";
            this.ckIgnoreCategory.UseVisualStyleBackColor = true;
            this.ckIgnoreCategory.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // gpOptions
            //
            this.gpOptions.Controls.Add(this.panel1);
            this.gpOptions.Controls.Add(this.gbStatistics);
            this.gpOptions.Controls.Add(this.ckMEMORY);
            this.gpOptions.Controls.Add(this.ckBUG);
            this.gpOptions.Controls.Add(this.ckERROR);
            this.gpOptions.Controls.Add(this.ckTIME);
            this.gpOptions.Controls.Add(this.ckUNKNOWN);
            this.gpOptions.Controls.Add(this.cbFancy);
            this.gpOptions.Controls.Add(this.ckUNSAT);
            this.gpOptions.Controls.Add(this.ckSAT);
            this.gpOptions.Controls.Add(this.lblIgnore);
            this.gpOptions.Controls.Add(this.ckIgnoreContainer);
            this.gpOptions.Controls.Add(this.ckIgnoreExtension);
            this.gpOptions.Controls.Add(this.ckIgnoreCategory);
            this.gpOptions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gpOptions.Location = new System.Drawing.Point(0, 0);
            this.gpOptions.Name = "gpOptions";
            this.gpOptions.Size = new System.Drawing.Size(681, 116);
            this.gpOptions.TabIndex = 6;
            this.gpOptions.TabStop = false;
            this.gpOptions.Text = "Options";
            //
            // ckMEMORY
            //
            this.ckMEMORY.AutoSize = true;
            this.ckMEMORY.Checked = true;
            this.ckMEMORY.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ckMEMORY.Location = new System.Drawing.Point(128, 65);
            this.ckMEMORY.Name = "ckMEMORY";
            this.ckMEMORY.Size = new System.Drawing.Size(74, 17);
            this.ckMEMORY.TabIndex = 28;
            this.ckMEMORY.Text = "MEMORY";
            this.ckMEMORY.UseVisualStyleBackColor = true;
            this.ckMEMORY.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // ckBUG
            //
            this.ckBUG.AutoSize = true;
            this.ckBUG.Checked = true;
            this.ckBUG.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ckBUG.Location = new System.Drawing.Point(6, 65);
            this.ckBUG.Name = "ckBUG";
            this.ckBUG.Size = new System.Drawing.Size(49, 17);
            this.ckBUG.TabIndex = 27;
            this.ckBUG.Text = "BUG";
            this.ckBUG.UseVisualStyleBackColor = true;
            this.ckBUG.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // ckERROR
            //
            this.ckERROR.AutoSize = true;
            this.ckERROR.Checked = true;
            this.ckERROR.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ckERROR.Location = new System.Drawing.Point(59, 65);
            this.ckERROR.Name = "ckERROR";
            this.ckERROR.Size = new System.Drawing.Size(65, 17);
            this.ckERROR.TabIndex = 26;
            this.ckERROR.Text = "ERROR";
            this.ckERROR.UseVisualStyleBackColor = true;
            this.ckERROR.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // lblSlower
            //
            this.lblSlower.Location = new System.Drawing.Point(122, 73);
            this.lblSlower.Name = "lblSlower";
            this.lblSlower.Size = new System.Drawing.Size(91, 13);
            this.lblSlower.TabIndex = 25;
            this.lblSlower.Text = "label1";
            this.lblSlower.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // label5
            //
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(73, 73);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(49, 13);
            this.label5.TabIndex = 24;
            this.label5.Text = "Y Slower";
            //
            // lblFaster
            //
            this.lblFaster.Location = new System.Drawing.Point(122, 58);
            this.lblFaster.Name = "lblFaster";
            this.lblFaster.Size = new System.Drawing.Size(91, 13);
            this.lblFaster.TabIndex = 23;
            this.lblFaster.Text = "label1";
            this.lblFaster.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(73, 58);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(46, 13);
            this.label3.TabIndex = 22;
            this.label3.Text = "Y Faster";
            //
            // lblTotal
            //
            this.lblTotal.Location = new System.Drawing.Point(122, 42);
            this.lblTotal.Name = "lblTotal";
            this.lblTotal.Size = new System.Drawing.Size(91, 16);
            this.lblTotal.TabIndex = 21;
            this.lblTotal.Text = "label1";
            this.lblTotal.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(73, 43);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(31, 13);
            this.label1.TabIndex = 20;
            this.label1.Text = "Total";
            //
            // lblDatapoints
            //
            this.lblDatapoints.AutoSize = true;
            this.lblDatapoints.Location = new System.Drawing.Point(6, 42);
            this.lblDatapoints.Name = "lblDatapoints";
            this.lblDatapoints.Size = new System.Drawing.Size(61, 13);
            this.lblDatapoints.TabIndex = 19;
            this.lblDatapoints.Text = "Datapoints:";
            //
            // ckTIME
            //
            this.ckTIME.AutoSize = true;
            this.ckTIME.Checked = true;
            this.ckTIME.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ckTIME.Location = new System.Drawing.Point(208, 65);
            this.ckTIME.Name = "ckTIME";
            this.ckTIME.Size = new System.Drawing.Size(75, 17);
            this.ckTIME.TabIndex = 7;
            this.ckTIME.Text = "TIMEOUT";
            this.ckTIME.UseVisualStyleBackColor = true;
            this.ckTIME.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // ckUNKNOWN
            //
            this.ckUNKNOWN.AutoSize = true;
            this.ckUNKNOWN.Checked = true;
            this.ckUNKNOWN.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ckUNKNOWN.Location = new System.Drawing.Point(128, 42);
            this.ckUNKNOWN.Name = "ckUNKNOWN";
            this.ckUNKNOWN.Size = new System.Drawing.Size(84, 17);
            this.ckUNKNOWN.TabIndex = 6;
            this.ckUNKNOWN.Text = "UNKNOWN";
            this.ckUNKNOWN.UseVisualStyleBackColor = true;
            this.ckUNKNOWN.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // rbNormalized
            //
            this.rbNormalized.AutoSize = true;
            this.rbNormalized.Checked = false;
            this.rbNormalized.Location = new System.Drawing.Point(3, 6);
            this.rbNormalized.Name = "rbNormalized";
            this.rbNormalized.Size = new System.Drawing.Size(139, 17);
            this.rbNormalized.TabIndex = 0;
            this.rbNormalized.TabStop = true;
            this.rbNormalized.Text = "Normalized CPU time (sec)";
            this.rbNormalized.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // rbNonNormalized
            //
            this.rbNonNormalized.AutoSize = true;
            this.rbNonNormalized.Checked = true;
            this.rbNonNormalized.Location = new System.Drawing.Point(3, 25);
            this.rbNonNormalized.Name = "rbNonNormalized";
            this.rbNonNormalized.Size = new System.Drawing.Size(160, 17);
            this.rbNonNormalized.TabIndex = 0;
            this.rbNonNormalized.Text = "CPU time (sec)";
            this.rbNonNormalized.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // rbWallClock
            //
            this.rbWallClock.AutoSize = true;
            this.rbWallClock.Checked = false;
            this.rbWallClock.Location = new System.Drawing.Point(3, 43);
            this.rbWallClock.Name = "rbWallClock";
            this.rbWallClock.Size = new System.Drawing.Size(122, 17);
            this.rbWallClock.TabIndex = 0;
            this.rbWallClock.Text = "Wall-clock time (sec)";
            this.rbWallClock.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // rbMemoryUsed
            //
            this.rbMemoryUsed.AutoSize = true;
            this.rbMemoryUsed.Checked = false;
            this.rbMemoryUsed.Location = new System.Drawing.Point(3, 60);
            this.rbMemoryUsed.Name = "rbMemoryUsed";
            this.rbMemoryUsed.Size = new System.Drawing.Size(113, 17);
            this.rbMemoryUsed.TabIndex = 0;
            this.rbMemoryUsed.Text = "Memory used (MB)";
            this.rbMemoryUsed.CheckedChanged += new System.EventHandler(this.ckCheckedChanged);
            //
            // splitContainer1
            //
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.None;
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitContainer1.Size = new System.Drawing.Size(681, 649);
            this.splitContainer1.SplitterDistance = 550;
            this.splitContainer1.TabIndex = 7;
            //
            // splitContainer1.Panel1
            //
            this.splitContainer1.Panel1.Controls.Add(this.chart);
            //
            // splitContainer1.Panel2
            //
            this.splitContainer1.Panel2.Controls.Add(this.gpOptions);
            //
            // gbStatistics
            //
            this.gbStatistics.Controls.Add(this.lblAvgSpeedupTxt);
            this.gbStatistics.Controls.Add(this.lblDatapoints);
            this.gbStatistics.Controls.Add(this.label1);
            this.gbStatistics.Controls.Add(this.label3);
            this.gbStatistics.Controls.Add(this.label5);
            this.gbStatistics.Controls.Add(this.lblSlower);
            this.gbStatistics.Controls.Add(this.lblAvgSpeedup);
            this.gbStatistics.Controls.Add(this.lblFaster);
            this.gbStatistics.Controls.Add(this.lblTotal);
            this.gbStatistics.Location = new System.Drawing.Point(281, 0);
            this.gbStatistics.Name = "gbStatistics";
            this.gbStatistics.Size = new System.Drawing.Size(222, 125);
            this.gbStatistics.TabIndex = 30;
            this.gbStatistics.TabStop = false;
            //
            // panel1
            //
            this.panel1.Controls.Add(this.rbMemoryUsed);
            this.panel1.Controls.Add(this.rbWallClock);
            this.panel1.Controls.Add(this.rbNonNormalized);
            this.panel1.Controls.Add(this.rbNormalized);
            this.panel1.Location = new System.Drawing.Point(506, 13);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(170, 91);
            this.panel1.TabIndex = 31;
            //
            // Scatterplot
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(681, 815);
            this.Controls.Add(this.splitContainer1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Scatterplot";
            this.Text = "ScatterPlot";
            this.Load += new System.EventHandler(this.scatterTest_Load);
            ((System.ComponentModel.ISupportInitialize)(this.chart)).EndInit();
            this.gpOptions.ResumeLayout(false);
            this.gpOptions.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.gbStatistics.ResumeLayout(false);
            this.gbStatistics.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataVisualization.Charting.Chart chart;
        private System.Windows.Forms.CheckBox cbFancy;
        private System.Windows.Forms.Label lblAvgSpeedupTxt;
        private System.Windows.Forms.Label lblAvgSpeedup;
        private System.Windows.Forms.CheckBox ckSAT;
        private System.Windows.Forms.CheckBox ckUNSAT;
        private System.Windows.Forms.GroupBox gpOptions;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.CheckBox ckTIME;
        private System.Windows.Forms.CheckBox ckUNKNOWN;
        private System.Windows.Forms.Label lblSlower;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label lblFaster;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblTotal;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblDatapoints;
        private System.Windows.Forms.CheckBox ckERROR;
        private System.Windows.Forms.CheckBox ckMEMORY;
        private System.Windows.Forms.CheckBox ckBUG;
        private System.Windows.Forms.RadioButton rbNormalized;
        private System.Windows.Forms.RadioButton rbNonNormalized;
        private System.Windows.Forms.RadioButton rbWallClock;
        private System.Windows.Forms.RadioButton rbMemoryUsed;
        private System.Windows.Forms.GroupBox gbStatistics;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label lblIgnore;
        private System.Windows.Forms.CheckBox ckIgnoreContainer;
        private System.Windows.Forms.CheckBox ckIgnoreExtension;
        private System.Windows.Forms.CheckBox ckIgnoreCategory;
    }
}