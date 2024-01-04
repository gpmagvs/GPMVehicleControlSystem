using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AGV_Offline_Data_Analysis
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private AGVDatabase _database;
        private void BtnSelectFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
            {
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    string folderPath = folderBrowserDialog.SelectedPath;
                    txbDatabaseFilePath.Text = folderPath;
                    // 使用 folderPath
                }
            }
        }

        private void BtnOpenDatabase_Click(object sender, EventArgs e)
        {
            _database = new AGVDatabase();
            if (_database.Open(txbDatabaseFilePath.Text, out string err_msg))
            {
                MessageBox.Show("資料庫開啟成功!");

            }
            else
            {
                MessageBox.Show($"資料庫開啟失敗!:{err_msg}");
            }

            batteryAnalysisUI1.database = _database;
        }
    }
}
