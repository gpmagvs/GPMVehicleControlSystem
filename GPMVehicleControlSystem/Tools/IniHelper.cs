using IniParser;
using IniParser.Parser;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace GPMVehicleControlSystem.Tools
{
    public class IniHelper
    {
        private FileIniDataParser _iniParser = new FileIniDataParser();
        public string FilePath { get; }
        public IniHelper(string iniFilePath)
        {
            FilePath = iniFilePath;
        }
        public bool SetValue(string section, string key, string value, out string error_msg)
        {
            error_msg = "";
            try
            {
                IniParser.Model.IniData data = _iniParser.ReadFile(FilePath);
                if (data[section][key] != null)
                {
                    data[section][key] = value;
                }
                else
                {
                    data[section].AddKey(key, value);
                    IniParser.Model.KeyData[] sorted = data[section].OrderBy(i => i.KeyName).ToArray();
                    data.Sections.RemoveSection(section);
                    data.Sections.AddSection(section);
                    for (int i = 0; i < sorted.Length; i++)
                    {
                        data.Sections[section].SetKeyData(sorted[i]);
                    }
                }
                _iniParser.WriteFile(FilePath, data);
                return true;
            }
            catch (Exception ex)
            {
                error_msg = ex.Message;
                return false;
            }
        }
        public string GetValue(string section, string key)
        {
            try
            {
                IniParser.Model.IniData data = _iniParser.ReadFile(FilePath);
                return data[section][key];
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }


        public bool RemoveKey(string section, string key, out string error_msg)
        {
            error_msg = "";
            try
            {
                IniParser.Model.IniData data = _iniParser.ReadFile(FilePath);
                data[section].RemoveKey(key);
                _iniParser.WriteFile(FilePath, data);
                return true;
            }
            catch (Exception ex)
            {
                error_msg = ex.Message;
                return false;
            }

        }

    }
}
