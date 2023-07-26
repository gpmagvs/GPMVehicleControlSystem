using GPMVehicleControlSystem.Models.ForkTeach;
using GPMVehicleControlSystem.Models.VCSSystem;
using GPMVehicleControlSystem.ViewModels.ForkTeach;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public partial class clsForkLifter
    {
        public string ForkTeachSettingsJsonFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "param/Station_teach.json");

        internal void LoadTeachDataSettingFromJsonConfigs()
        {
            try
            {

                if (File.Exists(ForkTeachSettingsJsonFilePath))
                {
                    string json = File.ReadAllText(ForkTeachSettingsJsonFilePath);
                    if (json == null)
                    {
                        StaSysMessageManager.AddNewMessage("Load Fork Teach Data Fail...Read Json Null", 2);
                        return;
                    }
                    ForkTeachData = JsonConvert.DeserializeObject<clsForkTeach>(json);
                }
            }
            catch (Exception ex)
            {
                StaSysMessageManager.AddNewMessage($"Load Fork Teach Data Fail...{ex.Message}", 2);
            }
            finally
            {
                SaveTeachDAtaSettings();
            }
        }

        internal bool SaveUnitTeachData(clsSaveUnitTeachDataVM unit_teach_data_model)
        {
            int tag = unit_teach_data_model.Tag;
            int layer = unit_teach_data_model.Layer;
            if (ForkTeachData.Teaches.TryGetValue(tag, out Dictionary<int, clsTeachData> tag_teaches))
            {
                if (tag_teaches.TryGetValue(layer, out var LayerTeachData))
                {
                    tag_teaches[layer] = unit_teach_data_model.TeachData;
                }
                else
                {
                    tag_teaches.Add(layer, unit_teach_data_model.TeachData);
                }
            }
            else
            {
                ForkTeachData.Teaches.Add(tag, new Dictionary<int, clsTeachData>()
                {
                    { 0, new clsTeachData()},
                    { 1, new clsTeachData()},
                    { 2, new clsTeachData()}
                });
                ForkTeachData.Teaches[tag][layer] = unit_teach_data_model.TeachData;
            }
            return SaveTeachDAtaSettings();
        }
        internal bool SaveTeachDAtaSettings()
        {
            return SaveTeachDAtaSettings(ForkTeachData);
        }
        internal bool SaveTeachDAtaSettings(clsForkTeach data)
        {
            try
            {

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(ForkTeachSettingsJsonFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        internal bool RemoveTagTeachData(int tag)
        {
            bool removed = ForkTeachData.Teaches.Remove(tag);
            if (removed)
                SaveTeachDAtaSettings();
            return removed;
        }

        internal bool RemoveUnitTeachData(int tag,int layer)
        {
            if (ForkTeachData.Teaches.TryGetValue(tag, out var dat))
            {
                if (!dat.TryGetValue(layer, out var unit_data))
                    return false;
                return dat.Remove(layer);
            }
            else
                return false;
        }
    }
}
