namespace AGV_Offline_Data_Analysis.UIComponents
{
    partial class BatteryAnalysisUI
    {
        /// <summary> 
        /// 設計工具所需的變數。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清除任何使用中的資源。
        /// </summary>
        /// <param name="disposing">如果應該處置受控資源則為 true，否則為 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 元件設計工具產生的程式碼

        /// <summary> 
        /// 此為設計工具支援所需的方法 - 請勿使用程式碼編輯器修改
        /// 這個方法的內容。
        /// </summary>
        private void InitializeComponent()
        {
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnQuery = new System.Windows.Forms.Button();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.btnAnalysisSelectedTimeRange = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.labTotalOdomery = new System.Windows.Forms.Label();
            this.labBatLevelLoss = new System.Windows.Forms.Label();
            this.labBatVoltageLoss = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.labIdleRatio = new System.Windows.Forms.Label();
            this.labRunRatio = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.labOdomBatLvRatio = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.labTimeRange = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.labTotalTime = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.labTotalTransferTaskNum = new System.Windows.Forms.Label();
            this.panel3 = new System.Windows.Forms.Panel();
            this.formsPlot1 = new ScottPlot.FormsPlot();
            this.label9 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.label13 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.labBatLevelLossByRun = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.labStartBatLv = new System.Windows.Forms.Label();
            this.labEndBatLv = new System.Windows.Forms.Label();
            this.label17 = new System.Windows.Forms.Label();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this.labTransferMove_AvgDistance = new System.Windows.Forms.Label();
            this.labTransferMove_FarestDistance = new System.Windows.Forms.Label();
            this.label20 = new System.Windows.Forms.Label();
            this.label21 = new System.Windows.Forms.Label();
            this.label22 = new System.Windows.Forms.Label();
            this.labTransferMove_ShortestDistance = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel3.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.Controls.Add(this.checkBox1);
            this.panel1.Controls.Add(this.btnQuery);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1106, 49);
            this.panel1.TabIndex = 1;
            // 
            // btnQuery
            // 
            this.btnQuery.Location = new System.Drawing.Point(5, 3);
            this.btnQuery.Name = "btnQuery";
            this.btnQuery.Size = new System.Drawing.Size(86, 41);
            this.btnQuery.TabIndex = 3;
            this.btnQuery.Text = "查詢";
            this.btnQuery.UseVisualStyleBackColor = true;
            this.btnQuery.Click += new System.EventHandler(this.BtnQuery_Click);
            // 
            // checkBox1
            // 
            this.checkBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.checkBox1.AutoSize = true;
            this.checkBox1.Location = new System.Drawing.Point(1031, 28);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(72, 16);
            this.checkBox1.TabIndex = 4;
            this.checkBox1.Text = "顯示狀態";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.CheckBox1_CheckedChanged);
            // 
            // btnAnalysisSelectedTimeRange
            // 
            this.btnAnalysisSelectedTimeRange.Font = new System.Drawing.Font("新細明體", 12F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.btnAnalysisSelectedTimeRange.Location = new System.Drawing.Point(6, 6);
            this.btnAnalysisSelectedTimeRange.Name = "btnAnalysisSelectedTimeRange";
            this.btnAnalysisSelectedTimeRange.Size = new System.Drawing.Size(399, 46);
            this.btnAnalysisSelectedTimeRange.TabIndex = 5;
            this.btnAnalysisSelectedTimeRange.Text = "統計此區間";
            this.btnAnalysisSelectedTimeRange.UseVisualStyleBackColor = true;
            this.btnAnalysisSelectedTimeRange.Click += new System.EventHandler(this.BtnAnalysisSelectedTimeRange_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 29.64824F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 70.35176F));
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel3, 1, 7);
            this.tableLayoutPanel1.Controls.Add(this.label17, 0, 7);
            this.tableLayoutPanel1.Controls.Add(this.label5, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.labBatVoltageLoss, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.labTotalOdomery, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.labIdleRatio, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.labRunRatio, 1, 5);
            this.tableLayoutPanel1.Controls.Add(this.label7, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.labTotalTime, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.labTotalTransferTaskNum, 1, 6);
            this.tableLayoutPanel1.Controls.Add(this.label8, 0, 6);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel2, 1, 2);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(6, 85);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 8;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 14.12894F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 14.12894F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25.72816F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 8.495146F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11.16505F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11.16505F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 14.12894F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 62F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(399, 466);
            this.tableLayoutPanel1.TabIndex = 6;
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label1.Location = new System.Drawing.Point(4, 58);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(111, 27);
            this.label1.TabIndex = 0;
            this.label1.Text = "總行走距離";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label2
            // 
            this.label2.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label2.Location = new System.Drawing.Point(4, 115);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(111, 27);
            this.label2.TabIndex = 1;
            this.label2.Text = "電量總耗損";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label3
            // 
            this.label3.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label3.Location = new System.Drawing.Point(4, 218);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(111, 27);
            this.label3.TabIndex = 2;
            this.label3.Text = "電壓耗損";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labTotalOdomery
            // 
            this.labTotalOdomery.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labTotalOdomery.Location = new System.Drawing.Point(122, 58);
            this.labTotalOdomery.Name = "labTotalOdomery";
            this.labTotalOdomery.Size = new System.Drawing.Size(216, 27);
            this.labTotalOdomery.TabIndex = 3;
            this.labTotalOdomery.Text = "-";
            this.labTotalOdomery.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labBatLevelLoss
            // 
            this.labBatLevelLoss.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labBatLevelLoss.Location = new System.Drawing.Point(4, 70);
            this.labBatLevelLoss.Name = "labBatLevelLoss";
            this.labBatLevelLoss.Size = new System.Drawing.Size(83, 25);
            this.labBatLevelLoss.TabIndex = 4;
            this.labBatLevelLoss.Text = "-";
            this.labBatLevelLoss.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labBatVoltageLoss
            // 
            this.labBatVoltageLoss.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labBatVoltageLoss.Location = new System.Drawing.Point(122, 218);
            this.labBatVoltageLoss.Name = "labBatVoltageLoss";
            this.labBatVoltageLoss.Size = new System.Drawing.Size(216, 27);
            this.labBatVoltageLoss.TabIndex = 5;
            this.labBatVoltageLoss.Text = "-";
            this.labBatVoltageLoss.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label4
            // 
            this.label4.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label4.Location = new System.Drawing.Point(4, 252);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(111, 27);
            this.label4.TabIndex = 6;
            this.label4.Text = "IDLE 占比";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label5
            // 
            this.label5.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label5.Location = new System.Drawing.Point(4, 297);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(111, 27);
            this.label5.TabIndex = 7;
            this.label5.Text = "RUN 占比";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labIdleRatio
            // 
            this.labIdleRatio.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labIdleRatio.Location = new System.Drawing.Point(122, 252);
            this.labIdleRatio.Name = "labIdleRatio";
            this.labIdleRatio.Size = new System.Drawing.Size(216, 27);
            this.labIdleRatio.TabIndex = 8;
            this.labIdleRatio.Text = "-";
            this.labIdleRatio.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labRunRatio
            // 
            this.labRunRatio.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labRunRatio.Location = new System.Drawing.Point(122, 297);
            this.labRunRatio.Name = "labRunRatio";
            this.labRunRatio.Size = new System.Drawing.Size(216, 27);
            this.labRunRatio.TabIndex = 9;
            this.labRunRatio.Text = "-";
            this.labRunRatio.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label6
            // 
            this.label6.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label6.Location = new System.Drawing.Point(184, 47);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(85, 22);
            this.label6.TabIndex = 10;
            this.label6.Text = "電耗(%/m)";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labOdomBatLvRatio
            // 
            this.labOdomBatLvRatio.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labOdomBatLvRatio.Location = new System.Drawing.Point(184, 70);
            this.labOdomBatLvRatio.Name = "labOdomBatLvRatio";
            this.labOdomBatLvRatio.Size = new System.Drawing.Size(85, 25);
            this.labOdomBatLvRatio.TabIndex = 11;
            this.labOdomBatLvRatio.Text = "-";
            this.labOdomBatLvRatio.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.labTimeRange);
            this.panel2.Controls.Add(this.btnAnalysisSelectedTimeRange);
            this.panel2.Controls.Add(this.tableLayoutPanel1);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Right;
            this.panel2.Location = new System.Drawing.Point(698, 49);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(408, 564);
            this.panel2.TabIndex = 2;
            // 
            // labTimeRange
            // 
            this.labTimeRange.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labTimeRange.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labTimeRange.Location = new System.Drawing.Point(6, 55);
            this.labTimeRange.Name = "labTimeRange";
            this.labTimeRange.Size = new System.Drawing.Size(399, 27);
            this.labTimeRange.TabIndex = 8;
            this.labTimeRange.Text = "時間";
            this.labTimeRange.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label7
            // 
            this.label7.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label7.Location = new System.Drawing.Point(4, 1);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(111, 27);
            this.label7.TabIndex = 12;
            this.label7.Text = "總時間";
            this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labTotalTime
            // 
            this.labTotalTime.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labTotalTime.Location = new System.Drawing.Point(122, 1);
            this.labTotalTime.Name = "labTotalTime";
            this.labTotalTime.Size = new System.Drawing.Size(220, 27);
            this.labTotalTime.TabIndex = 13;
            this.labTotalTime.Text = "-";
            this.labTotalTime.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label8
            // 
            this.label8.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label8.Location = new System.Drawing.Point(4, 342);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(111, 27);
            this.label8.TabIndex = 14;
            this.label8.Text = "搬運任務次數";
            this.label8.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labTotalTransferTaskNum
            // 
            this.labTotalTransferTaskNum.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labTotalTransferTaskNum.Location = new System.Drawing.Point(122, 342);
            this.labTotalTransferTaskNum.Name = "labTotalTransferTaskNum";
            this.labTotalTransferTaskNum.Size = new System.Drawing.Size(216, 27);
            this.labTotalTransferTaskNum.TabIndex = 15;
            this.labTotalTransferTaskNum.Text = "-";
            this.labTotalTransferTaskNum.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.label12);
            this.panel3.Controls.Add(this.label11);
            this.panel3.Controls.Add(this.label10);
            this.panel3.Controls.Add(this.label9);
            this.panel3.Controls.Add(this.formsPlot1);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(0, 49);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(698, 564);
            this.panel3.TabIndex = 9;
            // 
            // formsPlot1
            // 
            this.formsPlot1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.formsPlot1.Location = new System.Drawing.Point(8, 36);
            this.formsPlot1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.formsPlot1.Name = "formsPlot1";
            this.formsPlot1.Size = new System.Drawing.Size(684, 526);
            this.formsPlot1.TabIndex = 1;
            // 
            // label9
            // 
            this.label9.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(252)))), ((int)(((byte)(194)))));
            this.label9.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.label9.Font = new System.Drawing.Font("微軟正黑體", 7F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label9.Location = new System.Drawing.Point(6, 6);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(64, 21);
            this.label9.TabIndex = 2;
            this.label9.Text = "IDLE";
            this.label9.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label10
            // 
            this.label10.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(55)))), ((int)(((byte)(155)))), ((int)(((byte)(55)))));
            this.label10.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.label10.Font = new System.Drawing.Font("微軟正黑體", 7F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label10.ForeColor = System.Drawing.Color.White;
            this.label10.Location = new System.Drawing.Point(76, 6);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(64, 21);
            this.label10.TabIndex = 3;
            this.label10.Text = "RUN";
            this.label10.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label11
            // 
            this.label11.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(55)))), ((int)(((byte)(55)))));
            this.label11.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.label11.Font = new System.Drawing.Font("微軟正黑體", 7F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label11.ForeColor = System.Drawing.Color.White;
            this.label11.Location = new System.Drawing.Point(146, 6);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(64, 21);
            this.label11.TabIndex = 4;
            this.label11.Text = "DOWN";
            this.label11.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label12
            // 
            this.label12.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(118)))), ((int)(((byte)(186)))), ((int)(((byte)(227)))));
            this.label12.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.label12.Font = new System.Drawing.Font("微軟正黑體", 7F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label12.Location = new System.Drawing.Point(216, 6);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(64, 21);
            this.label12.TabIndex = 5;
            this.label12.Text = "CHARGE";
            this.label12.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
            this.tableLayoutPanel2.ColumnCount = 3;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334F));
            this.tableLayoutPanel2.Controls.Add(this.labEndBatLv, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this.labStartBatLv, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this.label16, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this.label15, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.label13, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this.label14, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this.label6, 2, 2);
            this.tableLayoutPanel2.Controls.Add(this.labBatLevelLoss, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this.labBatLevelLossByRun, 1, 3);
            this.tableLayoutPanel2.Controls.Add(this.labOdomBatLvRatio, 2, 3);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(122, 118);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 4;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(273, 96);
            this.tableLayoutPanel2.TabIndex = 6;
            // 
            // label13
            // 
            this.label13.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label13.Location = new System.Drawing.Point(4, 47);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(83, 22);
            this.label13.TabIndex = 6;
            this.label13.Text = "總耗損";
            this.label13.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label14
            // 
            this.label14.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label14.Location = new System.Drawing.Point(94, 47);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(83, 22);
            this.label14.TabIndex = 7;
            this.label14.Text = "移動耗損";
            this.label14.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labBatLevelLossByRun
            // 
            this.labBatLevelLossByRun.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labBatLevelLossByRun.Location = new System.Drawing.Point(94, 70);
            this.labBatLevelLossByRun.Name = "labBatLevelLossByRun";
            this.labBatLevelLossByRun.Size = new System.Drawing.Size(83, 25);
            this.labBatLevelLossByRun.TabIndex = 8;
            this.labBatLevelLossByRun.Text = "-";
            this.labBatLevelLossByRun.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label15
            // 
            this.label15.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label15.Location = new System.Drawing.Point(4, 1);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(83, 22);
            this.label15.TabIndex = 7;
            this.label15.Text = "起始電量";
            this.label15.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label16
            // 
            this.label16.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label16.Location = new System.Drawing.Point(94, 1);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(83, 22);
            this.label16.TabIndex = 7;
            this.label16.Text = "結束電量";
            this.label16.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labStartBatLv
            // 
            this.labStartBatLv.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labStartBatLv.Location = new System.Drawing.Point(4, 24);
            this.labStartBatLv.Name = "labStartBatLv";
            this.labStartBatLv.Size = new System.Drawing.Size(83, 22);
            this.labStartBatLv.TabIndex = 6;
            this.labStartBatLv.Text = "-";
            this.labStartBatLv.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labEndBatLv
            // 
            this.labEndBatLv.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labEndBatLv.Location = new System.Drawing.Point(94, 24);
            this.labEndBatLv.Name = "labEndBatLv";
            this.labEndBatLv.Size = new System.Drawing.Size(83, 22);
            this.labEndBatLv.TabIndex = 6;
            this.labEndBatLv.Text = "-";
            this.labEndBatLv.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label17
            // 
            this.label17.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label17.Location = new System.Drawing.Point(4, 399);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(111, 27);
            this.label17.TabIndex = 16;
            this.label17.Text = "搬運距離";
            this.label17.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
            this.tableLayoutPanel3.ColumnCount = 3;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334F));
            this.tableLayoutPanel3.Controls.Add(this.labTransferMove_ShortestDistance, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this.labTransferMove_AvgDistance, 1, 1);
            this.tableLayoutPanel3.Controls.Add(this.labTransferMove_FarestDistance, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this.label20, 1, 0);
            this.tableLayoutPanel3.Controls.Add(this.label21, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this.label22, 2, 0);
            this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel3.Location = new System.Drawing.Point(122, 402);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 2;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel3.Size = new System.Drawing.Size(273, 60);
            this.tableLayoutPanel3.TabIndex = 17;
            // 
            // labTransferMove_AvgDistance
            // 
            this.labTransferMove_AvgDistance.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labTransferMove_AvgDistance.Location = new System.Drawing.Point(184, 30);
            this.labTransferMove_AvgDistance.Name = "labTransferMove_AvgDistance";
            this.labTransferMove_AvgDistance.Size = new System.Drawing.Size(83, 22);
            this.labTransferMove_AvgDistance.TabIndex = 6;
            this.labTransferMove_AvgDistance.Text = "-";
            this.labTransferMove_AvgDistance.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labTransferMove_FarestDistance
            // 
            this.labTransferMove_FarestDistance.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labTransferMove_FarestDistance.Location = new System.Drawing.Point(94, 30);
            this.labTransferMove_FarestDistance.Name = "labTransferMove_FarestDistance";
            this.labTransferMove_FarestDistance.Size = new System.Drawing.Size(83, 22);
            this.labTransferMove_FarestDistance.TabIndex = 6;
            this.labTransferMove_FarestDistance.Text = "-";
            this.labTransferMove_FarestDistance.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label20
            // 
            this.label20.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label20.Location = new System.Drawing.Point(94, 1);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(83, 22);
            this.label20.TabIndex = 7;
            this.label20.Text = "最遠";
            this.label20.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label21
            // 
            this.label21.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label21.Location = new System.Drawing.Point(4, 1);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(83, 22);
            this.label21.TabIndex = 7;
            this.label21.Text = "最近";
            this.label21.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label22
            // 
            this.label22.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label22.Location = new System.Drawing.Point(184, 1);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(83, 22);
            this.label22.TabIndex = 8;
            this.label22.Text = "平均";
            this.label22.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labTransferMove_ShortestDistance
            // 
            this.labTransferMove_ShortestDistance.Font = new System.Drawing.Font("新細明體", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labTransferMove_ShortestDistance.Location = new System.Drawing.Point(4, 30);
            this.labTransferMove_ShortestDistance.Name = "labTransferMove_ShortestDistance";
            this.labTransferMove_ShortestDistance.Size = new System.Drawing.Size(83, 22);
            this.labTransferMove_ShortestDistance.TabIndex = 9;
            this.labTransferMove_ShortestDistance.Text = "-";
            this.labTransferMove_ShortestDistance.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // BatteryAnalysisUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Controls.Add(this.panel3);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Name = "BatteryAnalysisUI";
            this.Size = new System.Drawing.Size(1106, 613);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel3.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnQuery;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.Button btnAnalysisSelectedTimeRange;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label labTotalOdomery;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label labBatLevelLoss;
        private System.Windows.Forms.Label labBatVoltageLoss;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label labIdleRatio;
        private System.Windows.Forms.Label labRunRatio;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label labOdomBatLvRatio;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label labTimeRange;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label labTotalTime;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label labTotalTransferTaskNum;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label9;
        private ScottPlot.FormsPlot formsPlot1;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Label labBatLevelLossByRun;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label labEndBatLv;
        private System.Windows.Forms.Label labStartBatLv;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.Label labTransferMove_ShortestDistance;
        private System.Windows.Forms.Label labTransferMove_AvgDistance;
        private System.Windows.Forms.Label labTransferMove_FarestDistance;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.Label label17;
    }
}
