using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogAnalysisNet6
{
    internal class Global
    {
        internal static clsUserSettings userSettings = new clsUserSettings();
        internal static void Initialize()
        {
            userSettings = clsUserSettings.RestoreFromTempFile();
        }
    }
}
