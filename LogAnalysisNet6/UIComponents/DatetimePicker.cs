using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LogAnalysisNet6.UIComponents
{
    public partial class DatetimePicker : UserControl
    {
        public DatetimePicker()
        {
            InitializeComponent();
        }

        public DateTime[] TimeRange
        {
            get
            {
                var from = new DateTime(date_start.Value.Year, date_start.Value.Month, date_start.Value.Day,
                    time_start.Value.Hour, time_start.Value.Minute, time_start.Value.Second);
                var to = new DateTime(date_end.Value.Year, date_end.Value.Month, date_end.Value.Day,
                    time_end.Value.Hour, time_end.Value.Minute, time_end.Value.Second);
                return new DateTime[] { from, to };
            }
        }

        private void datetime_ValueChanged(object sender, EventArgs e)
        {
            Global.userSettings.IOQueryFromTime = TimeRange[0];
            Global.userSettings.IOQueryToTime = TimeRange[1];
        }

        private void DatetimePicker_Load(object sender, EventArgs e)
        {
            try
            {
                date_start.Value = Global.userSettings.IOQueryFromTime;
                time_start.Value = Global.userSettings.IOQueryFromTime;
                date_end.Value = Global.userSettings.IOQueryToTime;
                time_end.Value = Global.userSettings.IOQueryToTime;
            }
            catch (Exception )
            {
            }
        }
    }
}
