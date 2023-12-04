namespace LogAnalysisNet6.UIComponents
{
    partial class DatetimePicker
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
            time_start = new DateTimePicker();
            date_start = new DateTimePicker();
            label1 = new Label();
            label2 = new Label();
            time_end = new DateTimePicker();
            date_end = new DateTimePicker();
            SuspendLayout();
            // 
            // time_start
            // 
            time_start.Dock = DockStyle.Left;
            time_start.Font = new Font("新細明體", 14F, FontStyle.Regular, GraphicsUnit.Point);
            time_start.Format = DateTimePickerFormat.Time;
            time_start.Location = new Point(227, 0);
            time_start.Margin = new Padding(4);
            time_start.Name = "time_start";
            time_start.Size = new Size(177, 30);
            time_start.TabIndex = 3;
            time_start.ValueChanged += datetime_ValueChanged;
            // 
            // date_start
            // 
            date_start.Dock = DockStyle.Left;
            date_start.Font = new Font("新細明體", 14F, FontStyle.Regular, GraphicsUnit.Point);
            date_start.Format = DateTimePickerFormat.Short;
            date_start.Location = new Point(48, 0);
            date_start.Margin = new Padding(4);
            date_start.Name = "date_start";
            date_start.Size = new Size(179, 30);
            date_start.TabIndex = 2;
            date_start.ValueChanged += datetime_ValueChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Dock = DockStyle.Left;
            label1.Font = new Font("新細明體", 14F, FontStyle.Regular, GraphicsUnit.Point);
            label1.Location = new Point(0, 0);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Padding = new Padding(0, 6, 0, 0);
            label1.Size = new Size(48, 25);
            label1.TabIndex = 4;
            label1.Text = "From";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Dock = DockStyle.Left;
            label2.Font = new Font("新細明體", 14F, FontStyle.Regular, GraphicsUnit.Point);
            label2.Location = new Point(404, 0);
            label2.Margin = new Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Padding = new Padding(14, 6, 0, 0);
            label2.Size = new Size(43, 25);
            label2.TabIndex = 7;
            label2.Text = "To";
            // 
            // time_end
            // 
            time_end.Dock = DockStyle.Left;
            time_end.Font = new Font("新細明體", 14F, FontStyle.Regular, GraphicsUnit.Point);
            time_end.Format = DateTimePickerFormat.Time;
            time_end.Location = new Point(626, 0);
            time_end.Margin = new Padding(4);
            time_end.Name = "time_end";
            time_end.Size = new Size(177, 30);
            time_end.TabIndex = 6;
            time_end.ValueChanged += datetime_ValueChanged;
            // 
            // date_end
            // 
            date_end.Dock = DockStyle.Left;
            date_end.Font = new Font("新細明體", 14F, FontStyle.Regular, GraphicsUnit.Point);
            date_end.Format = DateTimePickerFormat.Short;
            date_end.Location = new Point(447, 0);
            date_end.Margin = new Padding(4);
            date_end.Name = "date_end";
            date_end.Size = new Size(179, 30);
            date_end.TabIndex = 5;
            date_end.ValueChanged += datetime_ValueChanged;
            // 
            // DatetimePicker
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(time_end);
            Controls.Add(date_end);
            Controls.Add(label2);
            Controls.Add(time_start);
            Controls.Add(date_start);
            Controls.Add(label1);
            Margin = new Padding(4);
            Name = "DatetimePicker";
            Size = new Size(817, 34);
            Load += DatetimePicker_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DateTimePicker time_start;
        private System.Windows.Forms.DateTimePicker date_start;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.DateTimePicker time_end;
        private System.Windows.Forms.DateTimePicker date_end;
    }
}
