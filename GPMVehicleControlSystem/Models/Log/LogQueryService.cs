using AGVSystemCommonNet6.Log;
using System.Globalization;
using System.Reflection.Metadata.Ecma335;

namespace GPMVehicleControlSystem.Models.Log
{
    public class LogQueryService
    {
        private static string LogBaseFolder => LOG.LogFolder;
        internal static async Task<clsLogQueryResults> QueryLog(clsLogQueryOptions option)
        {
            await Task.Delay(10);
            var result = new clsLogQueryResults()
            {
                FromTimeStr = option.FromTimeStr,
                ToTimeStr = option.ToTimeStr,
                Page = option.Page,
                NumberPerPage = option.NumberPerPage,
                SpeficStrings = option.SpeficStrings,
            };

            List<string> folders_match = GetDateMatchFolders(option.FromTime, option.ToTime);
            folders_match.Sort();
            List<string> contents = new List<string>();
            foreach (string folder_ in folders_match)
            {
                var files = Directory.GetFiles(folder_);

                var files_matched = files.ToList().FindAll(file => GetDateTimeFromFileName(Path.GetFileNameWithoutExtension(file)) >= option.FromTime &&
                                                        GetDateTimeFromFileName(Path.GetFileNameWithoutExtension(file)) <= option.ToTime);
                foreach (var fileName in files_matched)
                {
                    var contentLines = File.ReadAllLines(fileName);
                    contents.AddRange(contentLines);
                }
            }
            result.TotalCount = contents.Count;
            result.LogMessageList = contents.Skip((option.Page - 1) * option.NumberPerPage).Take(option.NumberPerPage).Select(line => GetLogDto(line)).ToList();
            //2023/9/6 下午 12:50:59  [Trace][<>c__DisplayClass22_0] [IO]-[X000A]-Bumper_Sensor Changed to : 1
            return result;
        }
        private static clsLogQuResultDto GetLogDto(string line)
        {
            var splited = line.Split(" ");
            var timeStr = string.Join(" ", new string[] { splited[0], splited[1], splited[2] });
            var msgAry = new string[splited.Length - 3];
            Array.Copy(splited, 3, msgAry, 0, msgAry.Length);
            var msg = string.Join(" ", msgAry);
            var levelstr = msg.Split('|')[1];
            var classstr = msg.Split('|')[2];

            return new clsLogQuResultDto()
            {
                Message = msg.Replace($"|{levelstr}|{classstr}|", ""),
                Time = timeStr,
                Level = levelstr,
                Class = classstr
            };
        }
        private static List<string> GetDateMatchFolders(DateTime from, DateTime to)
        {
            string[] subfolders = Directory.GetDirectories(LogBaseFolder);
            var _from = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0);
            var _to = new DateTime(to.Year, to.Month, to.Day, 0, 0, 0);
            var folders_matched = subfolders.Where(path => GetDateTimeFromFolderName(Path.GetFileNameWithoutExtension(path)) >= _from && GetDateTimeFromFolderName(Path.GetFileNameWithoutExtension(path)) <= _to);
            return folders_matched.ToList();

        }
        private static DateTime GetDateTimeFromFolderName(string folder_name)
        {
            if (DateTime.TryParseExact(folder_name, "yyyy-MM-dd", CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime result))
                return result;
            else
                return new DateTime(2999, 1, 1);
        }
        private static DateTime GetDateTimeFromFileName(string file_name)
        {
            if (DateTime.TryParseExact(file_name, "yyyy-MM-dd HH", CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime result))
                return result;
            else
                return new DateTime(2999, 1, 1);
        }
    }
}
