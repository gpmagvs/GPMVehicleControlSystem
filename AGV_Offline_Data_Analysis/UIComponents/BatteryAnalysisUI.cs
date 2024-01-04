using AGV_Offline_Data_Analysis.Model;
using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AGV_Offline_Data_Analysis.UIComponents
{
    public partial class BatteryAnalysisUI : UserControl, IDatabaseUse
    {
        public AGVDatabase database { get; set; }
        Color BatSeriesColorWhenShowStatusSpans = Color.Black;
        Color IdleColor = Color.FromArgb(255, 252, 194);
        Color RunColor = Color.FromArgb(200, Color.Green);
        Color ChargingColor = Color.FromArgb(118, 186, 227);
        Color DownColor = Color.FromArgb(200, Color.Red);
        public BatteryAnalysisUI()
        {
            InitializeComponent();
        }

        private void BtnQuery_Click(object sender, EventArgs e)
        {

            if (database == null)
                return;
            checkBox1.Checked = false;
            var status = database.QueryStatus();
            RenderChart(status);

        }
        private List<HSpan> StatusSpanStore = new List<HSpan>();
        SignalPlotXY batteryLevel_plt;
        clsAGVStatusTrack[] batDatat_ordered;
        private void RenderChart(List<Model.clsAGVStatusTrack> status)
        {
            StatusSpanStore.Clear();
            formsPlot1.Plot.Clear();
            formsPlot1.Plot.XAxis.DateTimeFormat(true);

            batDatat_ordered = status.OrderBy(dat => dat.Time).ToArray();
            double[] timeLs = batDatat_ordered.Select(data => data.Time.ToOADate()).ToArray();
            double[] batLvLs = batDatat_ordered.Select(data => data.BatteryLevel1).ToArray();
            for (int i = 0; i < batDatat_ordered.Length; i++)
            {
                Model.SUB_STATUS _status = batDatat_ordered[i].Status;
                DateTime status_start_time = batDatat_ordered[i].Time;
                DateTime status_end_time = DateTime.MaxValue;
                try
                {
                    status_end_time = batDatat_ordered[i + 1].Time;
                }
                catch (IndexOutOfRangeException)
                {
                    break;
                }

                Color status_color = GetColorByStatus(_status);
                HSpan span = new HSpan();
                span.X1 = status_start_time.ToOADate();
                span.X2 = status_end_time.ToOADate();
                span.Color = status_color;
                span.Label = _status.ToString();
                StatusSpanStore.Add(span);
            }
            batteryLevel_plt = formsPlot1.Plot.AddSignalXY(timeLs, batLvLs);
            batteryLevel_plt.LineWidth = 3;
            formsPlot1.Plot.AddHorizontalLine(100, color: Color.Red);
            formsPlot1.Plot.AddHorizontalLine(70, color: Color.Red);
            formsPlot1.Plot.Title("", size: 30);
            formsPlot1.Plot.XLabel("時間");
            formsPlot1.Plot.YLabel("電量");
            formsPlot1.Plot.SetAxisLimitsY(0, 100);
            formsPlot1.Render(skipIfCurrentlyRendering: true);
        }

        private Color GetColorByStatus(SUB_STATUS _status)
        {
            switch (_status)
            {
                case SUB_STATUS.IDLE:
                    return IdleColor;
                case SUB_STATUS.RUN:
                    return RunColor;
                case SUB_STATUS.DOWN:
                    return DownColor;
                case SUB_STATUS.Charging:
                    return ChargingColor;
                case SUB_STATUS.Initializing:
                    break;
                case SUB_STATUS.ALARM:
                    break;
                case SUB_STATUS.WARNING:
                    break;
                case SUB_STATUS.STOP:
                    break;
                case SUB_STATUS.UNKNOWN:
                    break;
                default:
                    return Color.Gray;
            }
            return Color.Gray;
        }


        private void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                ShowStatusSpans();
            }
            else
            {
                RemoveStatusSpans();
                batteryLevel_plt.Color = Color.FromArgb(31, 119, 180);
                formsPlot1.Render(skipIfCurrentlyRendering: true);
            }
        }

        private void ShowStatusSpans()
        {
            RemoveStatusSpans();

            double timeMin, timeMax;
            GetCurrentTimeRanges(out timeMin, out timeMax);
            var _spans_filtered = StatusSpanStore.Where(span => span.X1 >= timeMin && span.X2 <= timeMax).ToList();

            foreach (var _span in _spans_filtered)
            {
                formsPlot1.Plot.Add(_span);
            }
            var other_elements = formsPlot1.Plot.GetPlottables().Where(plt => plt.GetType().Name != "HSpan");
            foreach (var _ele in other_elements)
            {
                formsPlot1.Plot.Remove(_ele);
                formsPlot1.Plot.Add(_ele);
            }
            batteryLevel_plt.Color = BatSeriesColorWhenShowStatusSpans;
            formsPlot1.Render(skipIfCurrentlyRendering: true);

        }

        private void GetCurrentTimeRanges(out double timeMin, out double timeMax)
        {
            var axisLimits = formsPlot1.Plot.GetAxisLimits();
            timeMin = axisLimits.XMin;
            timeMax = axisLimits.XMax;
        }

        private void RemoveStatusSpans()
        {
            var spans = formsPlot1.Plot.GetPlottables().Where(plt => plt is HSpan);
            foreach (var _exis_span in spans)
            {
                formsPlot1.Plot.Remove(_exis_span);
            }
            formsPlot1.Render(skipIfCurrentlyRendering: true);
        }

        private void BtnAnalysisSelectedTimeRange_Click(object sender, EventArgs e)
        {
            if (batDatat_ordered == null)
                return;
            GetCurrentTimeRanges(out double timeMin, out double timeMax);

            IEnumerable<clsAGVStatusTrack> data_in_range = batDatat_ordered.Where(dt => dt.Time.ToOADate() >= timeMin && dt.Time.ToOADate() <= timeMax);
            var runStates = data_in_range.Where(dat => dat.Status == SUB_STATUS.RUN);
            var startTime = data_in_range.First().Time;
            var endTime = data_in_range.Last().Time;

            CalculateTransferTaskDistanceInfo(data_in_range, out var shortest_, out var farest_, out var average_);

            double batteryLevelLossByRunning = CalculateBatteryLossCauseByRunning(data_in_range);

            double totalTime = (endTime - startTime).TotalSeconds;
            double workingTime = GetTimeSecByStatus(data_in_range, SUB_STATUS.RUN);
            double idleTime = GetTimeSecByStatus(data_in_range, SUB_STATUS.IDLE)
                              + GetTimeSecByStatus(data_in_range, SUB_STATUS.Charging)
                              + GetTimeSecByStatus(data_in_range, SUB_STATUS.DOWN);
            double startBatLv = data_in_range.First().BatteryLevel1;
            double endBatLv = data_in_range.Last().BatteryLevel1;
            double batteryLevelLoss = endBatLv - startBatLv;
            double batteryVoltageLoss = (data_in_range.Last().BatteryVoltage1 - data_in_range.First().BatteryVoltage1) / 1000.0;
            double totalOdometry = Math.Round((runStates.Last().Odometry - runStates.First().Odometry) * 1000.0, 2);
            double runRatio = Math.Round(workingTime / totalTime * 100, 1);
            double idleRatio = Math.Round(idleTime / totalTime * 100, 1);
            double OdometryBatLvRatio = batteryLevelLossByRunning / totalOdometry;

            List<LDULDRecord[]> transferRecords = GetTransferRecord(startTime, endTime);


            labTotalOdomery.Text = $"{totalOdometry} m";
            labBatLevelLoss.Text = $"{batteryLevelLoss} %";
            labBatLevelLossByRun.Text = $"{batteryLevelLossByRunning} %";
            labBatVoltageLoss.Text = $"{batteryVoltageLoss} V";
            labIdleRatio.Text = $"{idleRatio} %";
            labRunRatio.Text = $"{runRatio} %";
            labOdomBatLvRatio.Text = $"{OdometryBatLvRatio} %/m";
            labTimeRange.Text = $"{startTime}~{endTime}";
            TimeSpan timeSpan = TimeSpan.FromSeconds(totalTime);
            labTotalTime.Text = string.Format("{0}小時{1}分{2}秒",
                                     timeSpan.Hours,
                                     timeSpan.Minutes,
                                     timeSpan.Seconds);
            labTotalTransferTaskNum.Text = $"{transferRecords.Count} 次";
            labStartBatLv.Text = $"{startBatLv} %";
            labEndBatLv.Text = $"{endBatLv} %";
            labTransferMove_ShortestDistance.Text = $"{shortest_} m";
            labTransferMove_FarestDistance.Text = $"{farest_} m";
            labTransferMove_AvgDistance.Text = $"{average_} m";
        }

        private void CalculateTransferTaskDistanceInfo(IEnumerable<clsAGVStatusTrack> data_in_range, out double shortest_dis, out double farest_dis, out double average_dis)
        {
            shortest_dis = 0;
            farest_dis = 0;
            average_dis = 0;
            List<double> distanceCollection = new List<double>();
            IEnumerable<string> taskNames = data_in_range.Where(d => d.Status == SUB_STATUS.RUN && !d.ExecuteTaskName.Contains("Charge")).Select(d => d.ExecuteTaskName).Distinct();
            foreach (var task_name in taskNames)
            {
                IEnumerable<clsAGVStatusTrack> subTasks = data_in_range.Where(d => d.ExecuteTaskSimpleName.Contains(task_name));
                var distance = subTasks.Last().Odometry - subTasks.First().Odometry;
                distanceCollection.Add(distance);
            }
            farest_dis = Math.Round(distanceCollection.Max() * 1000, 2);
            shortest_dis = Math.Round(distanceCollection.Min() * 1000, 2);
            average_dis = Math.Round(distanceCollection.Average() * 1000, 2);

        }

        private double CalculateBatteryLossCauseByRunning(IEnumerable<clsAGVStatusTrack> data_in_range)
        {
            var _datas = data_in_range.ToList();
            double totalBatteryLoss = 0;
            for (int i = 0; i < _datas.Count; i++)
            {
                if (_datas[i].Status == SUB_STATUS.RUN)
                {
                    try
                    {
                        if (_datas[i + 1].BatteryLevel1 < _datas[i].BatteryLevel1)
                        {
                            double loss = _datas[i + 1].BatteryLevel1 - _datas[i].BatteryLevel1;
                            totalBatteryLoss += loss;
                        }

                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            }
            return totalBatteryLoss;
        }

        private List<LDULDRecord[]> GetTransferRecord(DateTime startTime, DateTime endTime)
        {
            List<LDULDRecord[]> records = database.QueryLDULD(startTime, endTime);
            return records;
        }

        private double GetTimeSecByStatus(IEnumerable<clsAGVStatusTrack> data_in_range, SUB_STATUS status)
        {
            double sec = 0;
            var _data = data_in_range.ToArray();
            for (int i = 0; i < _data.Length; i++)
            {
                if (_data[i].Status == status)
                {
                    try
                    {
                        sec += (_data[i + 1].Time - _data[i].Time).TotalSeconds;
                    }
                    catch (IndexOutOfRangeException)
                    {
                        break;
                    }
                }
            }
            return sec;
        }
    }
}
