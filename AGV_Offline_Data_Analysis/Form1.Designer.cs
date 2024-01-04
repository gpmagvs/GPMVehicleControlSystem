namespace AGV_Offline_Data_Analysis
{
    partial class Form1
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

        #region Windows Form 設計工具產生的程式碼

        /// <summary>
        /// 此為設計工具支援所需的方法 - 請勿使用程式碼編輯器修改
        /// 這個方法的內容。
        /// </summary>
        private void InitializeComponent()
        {
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.batteryAnalysisUI1 = new AGV_Offline_Data_Analysis.UIComponents.BatteryAnalysisUI();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnOpenDatabase = new System.Windows.Forms.Button();
            this.btnSelectFolder = new System.Windows.Forms.Button();
            this.txbDatabaseFilePath = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 47);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1118, 573);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.batteryAnalysisUI1);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(1110, 547);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "電量分析";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // batteryAnalysisUI1
            // 
            this.batteryAnalysisUI1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.batteryAnalysisUI1.database = null;
            this.batteryAnalysisUI1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.batteryAnalysisUI1.Location = new System.Drawing.Point(3, 3);
            this.batteryAnalysisUI1.Name = "batteryAnalysisUI1";
            this.batteryAnalysisUI1.Size = new System.Drawing.Size(1104, 541);
            this.batteryAnalysisUI1.TabIndex = 0;
            // 
            // tabPage2
            // 
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(1110, 547);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "tabPage2";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnOpenDatabase);
            this.panel1.Controls.Add(this.btnSelectFolder);
            this.panel1.Controls.Add(this.txbDatabaseFilePath);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1118, 47);
            this.panel1.TabIndex = 1;
            // 
            // btnOpenDatabase
            // 
            this.btnOpenDatabase.Location = new System.Drawing.Point(620, 10);
            this.btnOpenDatabase.Name = "btnOpenDatabase";
            this.btnOpenDatabase.Size = new System.Drawing.Size(79, 23);
            this.btnOpenDatabase.TabIndex = 2;
            this.btnOpenDatabase.Text = "開啟資料庫";
            this.btnOpenDatabase.UseVisualStyleBackColor = true;
            this.btnOpenDatabase.Click += new System.EventHandler(this.BtnOpenDatabase_Click);
            // 
            // btnSelectFolder
            // 
            this.btnSelectFolder.Location = new System.Drawing.Point(587, 10);
            this.btnSelectFolder.Name = "btnSelectFolder";
            this.btnSelectFolder.Size = new System.Drawing.Size(27, 23);
            this.btnSelectFolder.TabIndex = 0;
            this.btnSelectFolder.Text = "...";
            this.btnSelectFolder.UseVisualStyleBackColor = true;
            this.btnSelectFolder.Click += new System.EventHandler(this.BtnSelectFolder_Click);
            // 
            // txbDatabaseFilePath
            // 
            this.txbDatabaseFilePath.Location = new System.Drawing.Point(111, 10);
            this.txbDatabaseFilePath.Name = "txbDatabaseFilePath";
            this.txbDatabaseFilePath.Size = new System.Drawing.Size(470, 22);
            this.txbDatabaseFilePath.TabIndex = 1;
            this.txbDatabaseFilePath.Text = "C:\\Users\\jinwei\\Documents\\AOI_AGV1_1224-0101\\VMS.db";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(92, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "AGV 資料庫路徑";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1118, 620);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.panel1);
            this.Name = "Form1";
            this.Text = "AGV OFFLINE DATA ANALYSIS";
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private UIComponents.BatteryAnalysisUI batteryAnalysisUI1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnSelectFolder;
        private System.Windows.Forms.TextBox txbDatabaseFilePath;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnOpenDatabase;
    }
}

