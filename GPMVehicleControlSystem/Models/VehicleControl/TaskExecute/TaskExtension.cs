using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using Newtonsoft.Json;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public static class TaskExtension
    {
        /// <summary>
        /// 取得任務切片
        /// </summary>
        /// <param name="task_download"></param>
        /// <param name="point_int"></param>
        /// <returns></returns>
        public static clsTaskDownloadData Splice(this clsTaskDownloadData task_download, int start_index, int length,bool chaged_destination)
        {
            clsMapPoint[] to_splice_Ori_traj = task_download.ExecutingTrajecory;

            clsTaskDownloadData new_data = JsonConvert.DeserializeObject<clsTaskDownloadData>(task_download.ToJson()); //深拷貝
            if (new_data.Action_Type == ACTION_TYPE.None)
            {
                new_data.Trajectory = new clsMapPoint[length];
                Array.Copy(to_splice_Ori_traj, start_index, new_data.Trajectory, 0, length);
            }
            else
            {
                new_data.Homing_Trajectory = new clsMapPoint[length];
                Array.Copy(to_splice_Ori_traj, start_index, new_data.Homing_Trajectory, 0, length);
            }
            if (chaged_destination)
            {
                new_data.Destination = new_data.ExecutingTrajecory.Last().Point_ID;
            }
            return new_data;
        }
    }
}
