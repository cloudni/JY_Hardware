﻿namespace Demo_JYPXIE9529AI
{
    partial class AIDemo
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea8 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Series series8 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AIDemo));
            this.WaveChart = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.label6 = new System.Windows.Forms.Label();
            this.SampleCount = new System.Windows.Forms.TextBox();
            this.SampleRate = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.SampleMode = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.cbChannelSelection = new System.Windows.Forms.ComboBox();
            this.TerminConfig = new System.Windows.Forms.ComboBox();
            this.DeviceName = new System.Windows.Forms.ComboBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.Quit = new System.Windows.Forms.Button();
            this.AcquireAction = new System.Windows.Forms.Button();
            this.ContSamplesTimer = new System.Windows.Forms.Timer(this.components);
            this.SingleReadTimer = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.WaveChart)).BeginInit();
            this.SuspendLayout();
            // 
            // WaveChart
            // 
            this.WaveChart.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            chartArea8.AxisX.MajorGrid.LineColor = System.Drawing.Color.Silver;
            chartArea8.AxisX.MajorGrid.LineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Dot;
            chartArea8.AxisX.MajorTickMark.LineWidth = 2;
            chartArea8.AxisX.MajorTickMark.Size = 3F;
            chartArea8.AxisX.MajorTickMark.TickMarkStyle = System.Windows.Forms.DataVisualization.Charting.TickMarkStyle.InsideArea;
            chartArea8.AxisX.MinorTickMark.Enabled = true;
            chartArea8.AxisX.MinorTickMark.Size = 1.5F;
            chartArea8.AxisX.MinorTickMark.TickMarkStyle = System.Windows.Forms.DataVisualization.Charting.TickMarkStyle.InsideArea;
            chartArea8.AxisY.MajorGrid.LineColor = System.Drawing.Color.Silver;
            chartArea8.AxisY.MajorGrid.LineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Dot;
            chartArea8.AxisY.MajorTickMark.LineWidth = 2;
            chartArea8.AxisY.MajorTickMark.Size = 0.8F;
            chartArea8.AxisY.MajorTickMark.TickMarkStyle = System.Windows.Forms.DataVisualization.Charting.TickMarkStyle.InsideArea;
            chartArea8.AxisY.MinorTickMark.Enabled = true;
            chartArea8.AxisY.MinorTickMark.Size = 0.4F;
            chartArea8.AxisY.MinorTickMark.TickMarkStyle = System.Windows.Forms.DataVisualization.Charting.TickMarkStyle.InsideArea;
            chartArea8.Name = "ChartArea1";
            this.WaveChart.ChartAreas.Add(chartArea8);
            this.WaveChart.Location = new System.Drawing.Point(13, 13);
            this.WaveChart.Margin = new System.Windows.Forms.Padding(4);
            this.WaveChart.Name = "WaveChart";
            series8.ChartArea = "ChartArea1";
            series8.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.FastLine;
            series8.Name = "Series1";
            this.WaveChart.Series.Add(series8);
            this.WaveChart.Size = new System.Drawing.Size(1027, 306);
            this.WaveChart.TabIndex = 3;
            this.WaveChart.Text = "chart1";
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft YaHei", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label6.Location = new System.Drawing.Point(302, 403);
            this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(82, 24);
            this.label6.TabIndex = 70;
            this.label6.Text = "采样点数";
            // 
            // SampleCount
            // 
            this.SampleCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.SampleCount.Location = new System.Drawing.Point(397, 406);
            this.SampleCount.Margin = new System.Windows.Forms.Padding(4);
            this.SampleCount.Name = "SampleCount";
            this.SampleCount.Size = new System.Drawing.Size(180, 25);
            this.SampleCount.TabIndex = 71;
            this.SampleCount.Text = "1000";
            // 
            // SampleRate
            // 
            this.SampleRate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.SampleRate.Location = new System.Drawing.Point(107, 405);
            this.SampleRate.Margin = new System.Windows.Forms.Padding(4);
            this.SampleRate.Name = "SampleRate";
            this.SampleRate.Size = new System.Drawing.Size(180, 25);
            this.SampleRate.TabIndex = 81;
            this.SampleRate.Text = "1000";
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft YaHei", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label2.Location = new System.Drawing.Point(33, 402);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(64, 24);
            this.label2.TabIndex = 80;
            this.label2.Text = "采样率";
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft YaHei", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label4.Location = new System.Drawing.Point(303, 326);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(82, 24);
            this.label4.TabIndex = 79;
            this.label4.Text = "通道选择";
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft YaHei", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label5.Location = new System.Drawing.Point(13, 365);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(82, 24);
            this.label5.TabIndex = 78;
            this.label5.Text = "采集模式";
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft YaHei", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label3.Location = new System.Drawing.Point(302, 365);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(82, 24);
            this.label3.TabIndex = 77;
            this.label3.Text = "输入配置";
            // 
            // SampleMode
            // 
            this.SampleMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.SampleMode.FormattingEnabled = true;
            this.SampleMode.Items.AddRange(new object[] {
            "Finite",
            "Continuous"});
            this.SampleMode.Location = new System.Drawing.Point(107, 366);
            this.SampleMode.Margin = new System.Windows.Forms.Padding(4);
            this.SampleMode.Name = "SampleMode";
            this.SampleMode.Size = new System.Drawing.Size(180, 23);
            this.SampleMode.TabIndex = 74;
            this.SampleMode.Text = "<请选择采集模式>";
            this.SampleMode.SelectedIndexChanged += new System.EventHandler(this.SampleMode_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft YaHei", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label1.Location = new System.Drawing.Point(13, 323);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(82, 24);
            this.label1.TabIndex = 76;
            this.label1.Text = "板卡编号";
            // 
            // cbChannelSelection
            // 
            this.cbChannelSelection.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cbChannelSelection.FormattingEnabled = true;
            this.cbChannelSelection.Location = new System.Drawing.Point(397, 327);
            this.cbChannelSelection.Margin = new System.Windows.Forms.Padding(4);
            this.cbChannelSelection.Name = "cbChannelSelection";
            this.cbChannelSelection.Size = new System.Drawing.Size(180, 23);
            this.cbChannelSelection.TabIndex = 73;
            this.cbChannelSelection.Text = "0";
            // 
            // TerminConfig
            // 
            this.TerminConfig.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.TerminConfig.FormattingEnabled = true;
            this.TerminConfig.Location = new System.Drawing.Point(397, 365);
            this.TerminConfig.Margin = new System.Windows.Forms.Padding(4);
            this.TerminConfig.Name = "TerminConfig";
            this.TerminConfig.Size = new System.Drawing.Size(180, 23);
            this.TerminConfig.TabIndex = 75;
            this.TerminConfig.Text = "<请选择配置方式>";
            // 
            // DeviceName
            // 
            this.DeviceName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.DeviceName.FormattingEnabled = true;
            this.DeviceName.Items.AddRange(new object[] {
            "0",
            "1",
            "2"});
            this.DeviceName.Location = new System.Drawing.Point(107, 327);
            this.DeviceName.Margin = new System.Windows.Forms.Padding(4);
            this.DeviceName.Name = "DeviceName";
            this.DeviceName.Size = new System.Drawing.Size(180, 23);
            this.DeviceName.TabIndex = 72;
            this.DeviceName.Text = "<请选择板卡编号>";
            this.DeviceName.SelectedIndexChanged += new System.EventHandler(this.DeviceName_SelectedIndexChanged);
            // 
            // checkBox1
            // 
            this.checkBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.checkBox1.AutoSize = true;
            this.checkBox1.Location = new System.Drawing.Point(891, 327);
            this.checkBox1.Margin = new System.Windows.Forms.Padding(4);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(149, 19);
            this.checkBox1.TabIndex = 84;
            this.checkBox1.Text = "使能内部信号输出";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // Quit
            // 
            this.Quit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Quit.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.Quit.FlatAppearance.BorderSize = 0;
            this.Quit.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.Quit.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.Control;
            this.Quit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.Quit.Image = ((System.Drawing.Image)(resources.GetObject("Quit.Image")));
            this.Quit.Location = new System.Drawing.Point(986, 404);
            this.Quit.Margin = new System.Windows.Forms.Padding(4);
            this.Quit.Name = "Quit";
            this.Quit.Size = new System.Drawing.Size(53, 45);
            this.Quit.TabIndex = 82;
            this.Quit.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.Quit.UseCompatibleTextRendering = true;
            this.Quit.UseVisualStyleBackColor = true;
            this.Quit.Click += new System.EventHandler(this.Quit_Click);
            // 
            // AcquireAction
            // 
            this.AcquireAction.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.AcquireAction.FlatAppearance.BorderSize = 0;
            this.AcquireAction.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.AcquireAction.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.Control;
            this.AcquireAction.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.AcquireAction.Image = ((System.Drawing.Image)(resources.GetObject("AcquireAction.Image")));
            this.AcquireAction.Location = new System.Drawing.Point(912, 404);
            this.AcquireAction.Margin = new System.Windows.Forms.Padding(4);
            this.AcquireAction.Name = "AcquireAction";
            this.AcquireAction.Size = new System.Drawing.Size(53, 45);
            this.AcquireAction.TabIndex = 83;
            this.AcquireAction.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.AcquireAction.UseCompatibleTextRendering = true;
            this.AcquireAction.UseVisualStyleBackColor = true;
            this.AcquireAction.Click += new System.EventHandler(this.AcquireAction_Click);
            // 
            // ContSamplesTimer
            // 
            this.ContSamplesTimer.Tick += new System.EventHandler(this.ContSamplesTimer_Tick);
            // 
            // SingleReadTimer
            // 
            this.SingleReadTimer.Tick += new System.EventHandler(this.SingleReadTimer_Tick);
            // 
            // AIDemo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1059, 476);
            this.Controls.Add(this.checkBox1);
            this.Controls.Add(this.Quit);
            this.Controls.Add(this.AcquireAction);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.SampleCount);
            this.Controls.Add(this.SampleRate);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.SampleMode);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cbChannelSelection);
            this.Controls.Add(this.TerminConfig);
            this.Controls.Add(this.DeviceName);
            this.Controls.Add(this.WaveChart);
            this.MinimumSize = new System.Drawing.Size(825, 419);
            this.Name = "AIDemo";
            this.Text = "AIDemo";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.AIDemo_FormClosing);
            this.Load += new System.EventHandler(this.AIDemo_Load);
            ((System.ComponentModel.ISupportInitialize)(this.WaveChart)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataVisualization.Charting.Chart WaveChart;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox SampleCount;
        private System.Windows.Forms.TextBox SampleRate;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox SampleMode;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cbChannelSelection;
        private System.Windows.Forms.ComboBox TerminConfig;
        private System.Windows.Forms.ComboBox DeviceName;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.Button Quit;
        private System.Windows.Forms.Button AcquireAction;
        private System.Windows.Forms.Timer ContSamplesTimer;
        private System.Windows.Forms.Timer SingleReadTimer;
    }
}