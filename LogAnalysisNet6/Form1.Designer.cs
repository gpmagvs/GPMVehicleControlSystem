namespace LogAnalysisNet6
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            label1 = new Label();
            txbLogFolder = new TextBox();
            button1 = new Button();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            checkBox1 = new CheckBox();
            ckbNoShowDirectionLighter = new CheckBox();
            ckbHightLigtEQ_BUSY_SIGNAL = new CheckBox();
            dataGridView1 = new DataGridView();
            TimeStr = new DataGridViewTextBoxColumn();
            iODirectionDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            iONameDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            valueDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            messageDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            clsIOLogBindingSource = new BindingSource(components);
            ckbOnlyShowExistSensorIO = new Button();
            datetimePicker1 = new UIComponents.DatetimePicker();
            tabPage2 = new TabPage();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)clsIOLogBindingSource).BeginInit();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 15);
            label1.Name = "label1";
            label1.Size = new Size(71, 15);
            label1.TabIndex = 0;
            label1.Text = "LOG 資料夾";
            // 
            // txbLogFolder
            // 
            txbLogFolder.Location = new Point(101, 12);
            txbLogFolder.Name = "txbLogFolder";
            txbLogFolder.Size = new Size(759, 23);
            txbLogFolder.TabIndex = 1;
            txbLogFolder.Text = "D:\\車載Log\\AOI\\AGV2\\AOI_AGV2_GPM_AGV_LOG_1130";
            txbLogFolder.TextChanged += txbLogFolder_TextChanged;
            // 
            // button1
            // 
            button1.Location = new Point(866, 12);
            button1.Name = "button1";
            button1.Size = new Size(75, 23);
            button1.TabIndex = 2;
            button1.Text = "button1";
            button1.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Location = new Point(12, 54);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1081, 569);
            tabControl1.TabIndex = 3;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(checkBox1);
            tabPage1.Controls.Add(ckbNoShowDirectionLighter);
            tabPage1.Controls.Add(ckbHightLigtEQ_BUSY_SIGNAL);
            tabPage1.Controls.Add(dataGridView1);
            tabPage1.Controls.Add(ckbOnlyShowExistSensorIO);
            tabPage1.Controls.Add(datetimePicker1);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(1073, 541);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "tabPage1";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Location = new Point(149, 52);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(99, 19);
            checkBox1.TabIndex = 7;
            checkBox1.Text = "僅顯示在席IO";
            checkBox1.UseVisualStyleBackColor = true;
            // 
            // ckbNoShowDirectionLighter
            // 
            ckbNoShowDirectionLighter.AutoSize = true;
            ckbNoShowDirectionLighter.Checked = true;
            ckbNoShowDirectionLighter.CheckState = CheckState.Checked;
            ckbNoShowDirectionLighter.Location = new Point(291, 52);
            ckbNoShowDirectionLighter.Name = "ckbNoShowDirectionLighter";
            ckbNoShowDirectionLighter.Size = new Size(111, 19);
            ckbNoShowDirectionLighter.TabIndex = 6;
            ckbNoShowDirectionLighter.Text = "不顯示方向燈IO";
            ckbNoShowDirectionLighter.UseVisualStyleBackColor = true;
            // 
            // ckbHightLigtEQ_BUSY_SIGNAL
            // 
            ckbHightLigtEQ_BUSY_SIGNAL.AutoSize = true;
            ckbHightLigtEQ_BUSY_SIGNAL.Location = new Point(7, 52);
            ckbHightLigtEQ_BUSY_SIGNAL.Name = "ckbHightLigtEQ_BUSY_SIGNAL";
            ckbHightLigtEQ_BUSY_SIGNAL.Size = new Size(136, 19);
            ckbHightLigtEQ_BUSY_SIGNAL.TabIndex = 5;
            ckbHightLigtEQ_BUSY_SIGNAL.Text = "HighLight EQ_BUSY";
            ckbHightLigtEQ_BUSY_SIGNAL.UseVisualStyleBackColor = true;
            ckbHightLigtEQ_BUSY_SIGNAL.CheckedChanged += ckbHightLigtEQ_BUSY_SIGNAL_CheckedChanged;
            // 
            // dataGridView1
            // 
            dataGridView1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { TimeStr, iODirectionDataGridViewTextBoxColumn, iONameDataGridViewTextBoxColumn, valueDataGridViewTextBoxColumn, messageDataGridViewTextBoxColumn });
            dataGridView1.DataSource = clsIOLogBindingSource;
            dataGridView1.Location = new Point(7, 87);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new Size(1060, 448);
            dataGridView1.TabIndex = 4;
            // 
            // TimeStr
            // 
            TimeStr.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            TimeStr.DataPropertyName = "TimeStr";
            TimeStr.HeaderText = "TimeStr";
            TimeStr.Name = "TimeStr";
            TimeStr.ReadOnly = true;
            TimeStr.Width = 75;
            // 
            // iODirectionDataGridViewTextBoxColumn
            // 
            iODirectionDataGridViewTextBoxColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            iODirectionDataGridViewTextBoxColumn.DataPropertyName = "IO_Direction";
            iODirectionDataGridViewTextBoxColumn.HeaderText = "IO_Direction";
            iODirectionDataGridViewTextBoxColumn.Name = "iODirectionDataGridViewTextBoxColumn";
            iODirectionDataGridViewTextBoxColumn.Width = 101;
            // 
            // iONameDataGridViewTextBoxColumn
            // 
            iONameDataGridViewTextBoxColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            iONameDataGridViewTextBoxColumn.DataPropertyName = "IO_Name";
            iONameDataGridViewTextBoxColumn.HeaderText = "IO_Name";
            iONameDataGridViewTextBoxColumn.Name = "iONameDataGridViewTextBoxColumn";
            iONameDataGridViewTextBoxColumn.Width = 85;
            // 
            // valueDataGridViewTextBoxColumn
            // 
            valueDataGridViewTextBoxColumn.DataPropertyName = "Value";
            valueDataGridViewTextBoxColumn.HeaderText = "Value";
            valueDataGridViewTextBoxColumn.Name = "valueDataGridViewTextBoxColumn";
            // 
            // messageDataGridViewTextBoxColumn
            // 
            messageDataGridViewTextBoxColumn.DataPropertyName = "Message";
            messageDataGridViewTextBoxColumn.HeaderText = "Message";
            messageDataGridViewTextBoxColumn.Name = "messageDataGridViewTextBoxColumn";
            // 
            // clsIOLogBindingSource
            // 
            clsIOLogBindingSource.DataSource = typeof(AGVSystemCommonNet6.Vehicle_Control.LogAnalysis.Models.clsIOLog);
            // 
            // ckbOnlyShowExistSensorIO
            // 
            ckbOnlyShowExistSensorIO.Location = new Point(817, 7);
            ckbOnlyShowExistSensorIO.Name = "ckbOnlyShowExistSensorIO";
            ckbOnlyShowExistSensorIO.Size = new Size(245, 38);
            ckbOnlyShowExistSensorIO.TabIndex = 3;
            ckbOnlyShowExistSensorIO.Text = "Query";
            ckbOnlyShowExistSensorIO.UseVisualStyleBackColor = true;
            ckbOnlyShowExistSensorIO.Click += btnQueryIOLog_Click;
            // 
            // datetimePicker1
            // 
            datetimePicker1.AutoSize = true;
            datetimePicker1.Location = new Point(7, 7);
            datetimePicker1.Margin = new Padding(4);
            datetimePicker1.Name = "datetimePicker1";
            datetimePicker1.Size = new Size(803, 38);
            datetimePicker1.TabIndex = 0;
            // 
            // tabPage2
            // 
            tabPage2.Location = new Point(4, 24);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(192, 72);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "tabPage2";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1105, 635);
            Controls.Add(tabControl1);
            Controls.Add(button1);
            Controls.Add(txbLogFolder);
            Controls.Add(label1);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)clsIOLogBindingSource).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private TextBox txbLogFolder;
        private Button button1;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private DataGridView dataGridView1;
        private Button ckbOnlyShowExistSensorIO;
        private UIComponents.DatetimePicker datetimePicker1;
        private BindingSource clsIOLogBindingSource;
        private DataGridViewTextBoxColumn TimeStr;
        private DataGridViewTextBoxColumn iODirectionDataGridViewTextBoxColumn;
        private DataGridViewTextBoxColumn iONameDataGridViewTextBoxColumn;
        private DataGridViewTextBoxColumn valueDataGridViewTextBoxColumn;
        private DataGridViewTextBoxColumn messageDataGridViewTextBoxColumn;
        private CheckBox ckbHightLigtEQ_BUSY_SIGNAL;
        private CheckBox ckbNoShowDirectionLighter;
        private CheckBox checkBox1;
    }
}
