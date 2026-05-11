using admin_web.Models;
using OfficeOpenXml;
using System;
using System.Data;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using admin_web.Models.DataProduct;
using System.IO;
using System.Linq;

namespace admin_web.Services.DataProductService
{
    public class Pamm_RolloverService
    {
        private static readonly string connectFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Pamm_Rollover", "config", "sql_connect.csv");
        private static readonly string connectionString = GetConnectionStringFromCsv(connectFilePath);

        public static DataTable ExecuteSQL_status()
        {
            DataTable data = new();

            string sqlcommand;

            sqlcommand = $@"SELECT Server_name,MM_id,MM_name,
                            CASE 
                                WHEN managerCanConfirm = 1 THEN 'Open'
                                WHEN managerCanConfirm = 0 THEN 'Close'
                                ELSE ''
                            END AS managerCanConfirm_status,
                            CASE 
                                WHEN managerCanWithdraw = 1 THEN 'Open'
                                WHEN managerCanWithdraw = 0 THEN 'Close'
                                ELSE ''
                            END AS managerCanWithdraw_status,
                            CASE 
                                WHEN managerCanConfirmDeposit = 1 THEN 'Open'
                                WHEN managerCanConfirmDeposit = 0 THEN 'Close'
                                ELSE ''
                            END AS managerCanConfirmDeposit_status,
                            CASE 
                                WHEN managerCanConfirmWithdraw = 1 THEN 'Open'
                                WHEN managerCanConfirmWithdraw = 0 THEN 'Close'
                                ELSE ''
                            END AS managerCanConfirmWithdraw_status,
                            CASE 
                                WHEN managerCanConfirmCloseAccount = 1 THEN 'Open'
                                WHEN managerCanConfirmCloseAccount = 0 THEN 'Close'
                                ELSE ''
                            END AS managerCanConfirmCloseAccount_status,
                            Time
                            FROM admin_tool.pamm_rollover 
                            where 1=1 
                            and time = (SELECT MAX(time) FROM admin_tool.pamm_rollover )   

                           ";

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 處理例外狀況，例如記錄日誌或顯示錯誤訊息
                    //Console.WriteLine($"連接失敗，重新嘗試中: {ex.Message}");
                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return data;
        }

        private static string GetConnectionStringFromCsv(string filePath)
        {
            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    // 讀取 CSV 第一行作為標題
                    reader.ReadLine();
                    // 讀取第二行內容
                    string line = reader.ReadLine();
                    if (line != null)
                    {
                        var values = line.Split(',');
                        string server = values[0];
                        string user = values[1];
                        string password = values[2];

                        // 使用 CSV 中的資料構建連接字串
                        return $"server={server};user={user};password={password};charset=utf8;";
                    }
                    else
                    {
                        throw new Exception("CSV 文件內容不正確");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"取得資料失敗: {ex.Message}");
                throw;
            }

        }
        public static List<List<string>> ReadCsv(string filePath)
        {
            var csvData = new List<List<string>>();

            if (!File.Exists(filePath))
            {
                return csvData; // 回傳空的資料
            }

            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var values = line.Split(',').ToList();
                    csvData.Add(values);
                }
            }

            return csvData;
        }
    }
}
