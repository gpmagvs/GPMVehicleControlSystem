using GPMVehicleControlSystem.Models.VCSSystem;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.Models.WorkStation.ForkTeach;
using GPMVehicleControlSystem.ViewModels.ForkTeach;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public partial class clsForkLifter
    {
        public string ForkTeachSettingsJsonFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "param/Station_teach.json");


        internal bool SaveUnitTeachData(clsSaveUnitTeachDataVM unit_teach_data_model)
        {
            int tag = unit_teach_data_model.Tag;
            int layer = unit_teach_data_model.Layer;
            if (StationDatas.TryGetValue(tag, out clsForkWorkStationData? station))
            {
                if (station.LayerDatas.TryGetValue(layer, out var LayerTeachData))
                {
                    station.LayerDatas[layer].Up_Pose = unit_teach_data_model.TeachData.Up_Pose_Limit;
                    station.LayerDatas[layer].Down_Pose = unit_teach_data_model.TeachData.Down_Pose_Limit;
                }
                else
                {
                    station.LayerDatas.Add(layer, new clsStationLayerData
                    {
                        Up_Pose = unit_teach_data_model.TeachData.Up_Pose_Limit,
                        Down_Pose = unit_teach_data_model.TeachData.Down_Pose_Limit
                    });
                }
            }
            else
            {
                StationDatas.Add(tag, new clsForkWorkStationData
                {
                    LayerDatas = new Dictionary<int, clsStationLayerData>
                     {
                         {1,new clsStationLayerData() },
                         {2,new clsStationLayerData() },
                         {3,new clsStationLayerData() },
                     }
                });
                // StationDatas.Teaches[tag][layer] = unit_teach_data_model.TeachData;
            }
            return SaveTeachDAtaSettings();
        }
        internal bool SaveTeachDAtaSettings()
        {
            try
            {
                forkAGV.SaveTeachDAtaSettings();
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        internal bool RemoveTagTeachData(int tag)
        {
            bool removed = StationDatas.Remove(tag);
            if (removed)
                SaveTeachDAtaSettings();
            return removed;
        }

        internal bool RemoveUnitTeachData(int tag, int layer)
        {
            if (StationDatas.TryGetValue(tag, out clsForkWorkStationData? dat))
            {
                if (!dat.LayerDatas.TryGetValue(layer, out var unit_data))
                    return false;
                return dat.LayerDatas.Remove(layer);
            }
            else
                return false;
        }
    }
}
