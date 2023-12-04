
using AGVSystemCommonNet6.Vehicle_Control.LogAnalysis;
using AGVSystemCommonNet6.Vehicle_Control.LogAnalysis.Models;
using System.Windows.Forms;

namespace LogAnalysisNet6
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        List<AGVSystemCommonNet6.Vehicle_Control.LogAnalysis.Models.clsIOLog> io_log_query_result;
        private void btnQueryIOLog_Click(object sender, EventArgs e)
        {
            LogReader reader = new LogReader(txbLogFolder.Text);
            var time_start = datetimePicker1.TimeRange[0];
            var time_end = datetimePicker1.TimeRange[1];
            dataGridView1.DataSource = null;
            Task.Factory.StartNew(() =>
            {
                io_log_query_result = reader.QueryIO(time_start, time_end);

                if (ckbNoShowDirectionLighter.Checked)
                {
                    io_log_query_result = io_log_query_result.FindAll(x => !x.IO_Name.Contains("AGV_DiractionLight")).ToList();
                }

                Invoke(new Action(() =>
                {
                    dataGridView1.DataSource = io_log_query_result;
                    ckbHightLigtEQ_BUSY_SIGNAL_CheckedChanged(null, null);
                    MessageBox.Show($"Finish!");
                }));
            });
        }

        private void ckbHightLigtEQ_BUSY_SIGNAL_CheckedChanged(object sender, EventArgs e)
        {
            if (io_log_query_result != null)
            {
                foreach (DataGridViewRow? row in dataGridView1.Rows.Cast<DataGridViewRow>().Where(row => (row.DataBoundItem as clsIOLog).IO_Name.Contains("EQ_BUSY")))
                {
                    if (row != null)
                    {
                        row.DefaultCellStyle.BackColor = ckbHightLigtEQ_BUSY_SIGNAL.Checked ? Color.Orange : Color.White;
                    }
                }

            }
        }

        private void txbLogFolder_TextChanged(object sender, EventArgs e)
        {
            Global.userSettings.LogFolder = txbLogFolder.Text;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            txbLogFolder.Text = Global.userSettings.LogFolder;
        }
    }
}
