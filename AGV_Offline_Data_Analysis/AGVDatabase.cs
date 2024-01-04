using AGV_Offline_Data_Analysis.Model;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGV_Offline_Data_Analysis
{
    public class AGVDatabase : IDisposable
    {
        private SQLiteConnection db;
        private bool disposedValue;

        public bool Open(string db_file_path, out string errmsg)
        {
            errmsg = string.Empty;
            try
            {
                db = new SQLiteConnection(db_file_path);
                return true;
            }
            catch (Exception ex)
            {
                errmsg = ex.Message;
                return false;
            }
        }

        public List<clsAGVStatusTrack> QueryStatus()
        {
            return db.Table<clsAGVStatusTrack>().ToList();
        }

        internal List<LDULDRecord[]> QueryLDULD(DateTime startTime, DateTime endTime)
        {
            List<LDULDRecord[]> transferReocords = new List<LDULDRecord[]>();
            var lduldRecords = db.Table<LDULDRecord>().Where(record => record.StartTime >= startTime && record.StartTime <= endTime).ToList();
            var unloads = lduldRecords.Where(record => record.Action == ACTION_TYPE.Unload);

            foreach (var _unload in unloads)
            {
                var load = lduldRecords.FirstOrDefault(rd => rd.Action == ACTION_TYPE.Load && rd.TaskName == _unload.TaskName);
                if (load != null)
                {
                    transferReocords.Add(new LDULDRecord[] { _unload, load });
                }
            }
            return transferReocords;
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 處置受控狀態 (受控物件)
                }
                db.Close();
                db.Dispose();
                // TODO: 釋出非受控資源 (非受控物件) 並覆寫完成項
                // TODO: 將大型欄位設為 Null
                disposedValue = true;
            }
        }

        // // TODO: 僅有當 'Dispose(bool disposing)' 具有會釋出非受控資源的程式碼時，才覆寫完成項
        // ~AGVDatabase()
        // {
        //     // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

    }
}
