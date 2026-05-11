using admin_web.Models;
using MySql.Data.MySqlClient;
using OfficeOpenXml;
using P23.MetaTrader4.Manager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static admin_web.Models.MondayTroubleShoot.Compare_Manager_Model;

namespace admin_web.Services.MondayTroubleShootServices
{
    public class Compare_Manager_MT4Service
    {
        private static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Compare_Manager_MT4");
        private static readonly string OutputPath = Path.Combine(ToolPath, "output");

        // 取得檔案清單
        public static List<string> GetFileNameList()
        {
            List<string> fileNameList = GetFileNames();
            fileNameList = fileNameList
                .Select(file => file.Replace("MT4_Manager_", "").Replace(".xlsx", ""))
                .OrderBy(fileName =>
                {
                    string datePart = fileName.Split('_')[1];
                    return DateTime.Parse(datePart);
                })
                .ToList();
            return fileNameList;
        }

        public static List<string> GetFileNames()
        {
            string[] files = Directory.GetFiles(OutputPath, "MT4_Manager_*.xlsx");
            List<string> fileNameList = new();
            foreach (var item in files.Select(Path.GetFileName).ToArray())
            {
                fileNameList.Add(item);
            }
            return fileNameList;
        }

        public static ExcelViewModel View_Data(string FileName)
        {
            string FullFilePath = Path.Combine(OutputPath, $"MT4_Manager_{FileName}.xlsx");
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            using var package = new ExcelPackage(new FileInfo(FullFilePath));

            var viewModel = new ExcelViewModel
            {
                Tabs = new List<ExcelViewModel.Tab>()
            };

            List<string> SheetName = package.Workbook.Worksheets.Select(ws => ws.Name).ToList();
            foreach (var item in SheetName)
            {
                var Tab = new ExcelViewModel.Tab();
                var AllData = package.Workbook.Worksheets[item]; // excel裡的資料

                if (item.Contains("其他"))
                {
                    continue;
                }

                Tab.Name = item;
                if (AllData != null)
                {
                    Tab.Check = true;
                    if (AllData.Dimension != null)
                    {
                        for (int col = 1; col <= AllData.Dimension.End.Column; col++) // 欄位名稱
                        {
                            string headerValue = AllData.Cells[1, col].Text;
                            Tab.HeaderList.Add(headerValue);
                        }

                        for (int row = 2; row <= AllData.Dimension.End.Row; row++)
                        {
                            List<string> Data = new();
                            for (int col = 1; col <= AllData.Dimension.End.Column; col++)
                            {
                                Data.Add(AllData.Cells[row, col].Value?.ToString() ?? "");
                            }
                            Tab.DataList.Add(Data);
                        }
                    }
                }
                viewModel.Tabs.Add(Tab);
            }
            return viewModel;
        }

        //轉換IP
        public static string ConvertToIP(ulong number)
        {
            if (number == 0)
                return "0.0.0.0";

            uint lower32 = (uint)(number & 0xFFFFFFFF);
            byte[] bytes = BitConverter.GetBytes(lower32);


            Array.Reverse(bytes);
            return string.Join(".", bytes);
        }



        public static int search_manager_token()
        {

            //建立一個api insert data
            string filePath = @"E:\admin_web_tool\admin_web\wwwroot\tools\Compare_Manager_MT4\setting_fu\setting_amd.xlsx";
            if (!File.Exists(filePath))
            {
                Console.WriteLine("檔案不存在: " + filePath);
            }
            List<MT4ServerRecord> MT4ServerRecords = ReadMT4ServerSheet(filePath);
            SQLServerRecord sqlRecords = ReadSQLServerSheet(filePath);

            // sql_server(excel)
            string SQL_Host = sqlRecords.Host;
            int SQL_Port = sqlRecords.Port;
            string SQL_User = sqlRecords.User;
            string SQL_Password = sqlRecords.Password;
            string SQL_DB = sqlRecords.DB;
            int SQL_TimeZoneAdd = sqlRecords.TimeZoneAdd;

            //抓取目前系統時間
            DateTime currentTime = DateTime.Now.AddHours(SQL_TimeZoneAdd);

            // 連上該SQL Server的DB
            string connString = @$"
                    server={SQL_Host}; 
                    port={SQL_Port}; 
                    user id={SQL_User}; 
                    password={SQL_Password};
                    database={SQL_DB};
                    charset=utf8;
                ";

            using MySqlConnection conn = new(connString);

            // Data Table 檢查
            try
            {
                conn.Open();
                string query = $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'mt4_permissions'";
                MySqlCommand cmd = new(query, conn);
                var result = cmd.ExecuteScalar();

                // 如果不存在，則建立
                if (result == null)
                {
                    Console.WriteLine($"Table 'mt4_permissions' does not exist in database '{SQL_DB}'.");
                    string sql = @"CREATE TABLE IF NOT EXISTS `mt4_permissions`(
                               `math_id` INT UNSIGNED AUTO_INCREMENT,
                               `Server` VARCHAR(100) NOT NULL,
                               `Login` int NOT NULL,
                               `Name` VARCHAR(300) NOT NULL,
                               `Enable`int NOT NULL,
                               `Groups` VARCHAR(1000) NOT NULL,
                               `IpFilter` int NOT NULL,
                               `IpFrom` BIGINT NOT NULL, 
                               `IpTo` BIGINT NOT NULL,
                               `Manager` int NOT NULL,
                               `Admiinistrator` int NOT NULL,
                               `Reports` int NOT NULL,
                               `InternalMailSystem` int NOT NULL,
                               `SendNews` int NOT NULL,
                               `ShowOnlineClients` int NOT NULL,
                               `ConfigureServerPlugins` int NOT NULL,
                               `PushNotifications` int NOT NULL,
                               `SuperviseTraders`  int NOT NULL,
                               `Accountant`  int NOT NULL,
                               `RiskManager`  int NOT NULL,
                               `Journals` int NOT NULL,
                               `MarketWatch`  int NOT NULL,
                               `PersonalDetails`  int NOT NULL,
                               `AutomaticServerReport` int NOT NULL,
                               `AccessToApplicationMarket`  int NOT NULL,
                               `Dealer` int NOT NULL,
                               `Trade`  int NOT NULL,
                               `Available`  int NOT NULL,
                               `InputTime`  DATETIME,
                                PRIMARY KEY ( `math_id` )
                                )ENGINE=InnoDB DEFAULT CHARSET=utf8";

                    cmd = new(sql, conn);
                    int res = cmd.ExecuteNonQuery();
                    if (res == 0)
                    {
                        Console.WriteLine("Table 'mt4_permissions' created successfully.");
                    }
                    else
                    {
                        Console.WriteLine("An error occurred while creating the table 'mt4_permissions'.");
                    }
                }
                else
                {
                    Console.WriteLine($"Table 'mt4_permissions' exists in database '{SQL_DB}'.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            //manager api 
            var rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var aa = Path.Combine(rootPath, Environment.Is64BitProcess ? "mtmanapi64.dll" : "mtmanapi.dll");
            var AIROE = new ClrWrapper(aa);

            int _count = 0;
            //DateTime today = DateTime.Today.AddDays(-1);
            DateTime today = DateTime.Today;
            string today_date = today.ToString("yyyy-MM-dd");
            string present_time = today_date + " 23:59:59";

            string ipFromStr = "";
            string ipToStr = "";


            foreach (var record in MT4ServerRecords)
            {
                try
                {
                    Console.WriteLine($"\n[START]{record.Server}");

                    // 檢查DB內有無該Server的資料
                    string select_sql = $"SELECT math_id FROM `mt4_permissions` WHERE Server = '{record.Server}' AND InputTime regexp '{today_date}'";
                    MySqlCommand cmd2 = new(select_sql, conn);

                    List<string> mathIdList = new List<string>();
                    using (MySqlDataReader reader = cmd2.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            mathIdList.Add(reader["math_id"].ToString());
                        }
                    }

                    // API連接
                    AIROE.Connect(record.Connect);
                    var res = AIROE.Login(int.Parse(record.Login), record.Password);

                    if (res == 0)
                    {
                        Console.WriteLine("[Connection Successful]");
                        //manager api
                        var man_all = AIROE.CfgRequestManager();

                        //Acount api
                        var avcior = AIROE.UsersRequest();

                        // 如果SQL有撈到任何今日的資料，先全部刪掉
                        if (mathIdList.Count > 0)
                        {
                            string delete_all = "";
                            foreach (var mathid in mathIdList)
                            {
                                delete_all += $"DELETE FROM `configurations`.`mt4_permissions` WHERE(`math_id` = '{mathid}');";
                            }
                            try
                            {
                                MySqlCommand cmd = new(delete_all, conn);
                                cmd.ExecuteNonQuery();
                                Console.WriteLine("[Del All Data Completed]");
                            }
                            catch (InvalidCastException)
                            {
                                Console.WriteLine("An error occurred while executing SQL.");
                            };
                        }

                        // Insert資料
                        string content_all = "";
                        string sql_command = @"INSERT INTO `mt4_permissions` (`Server`, `Login`, `Name`, `Enable`, `Groups`, `IpFilter`, `IpFrom`, `IpTo`, `Manager`, `Admiinistrator`, `Reports`,
                                `InternalMailSystem`, `SendNews`, `ShowOnlineClients`, `ConfigureServerPlugins`, `PushNotifications`, `SuperviseTraders`, `Accountant`, `RiskManager`,
                                `Journals`, `MarketWatch`, `PersonalDetails`, `AutomaticServerReport`, `AccessToApplicationMarket`, `Dealer`, `Trade`, `Available`, `InputTime`
                                ) VALUES";

                        foreach (var i in man_all)
                        {
                            try
                            {
                                var match = avcior.Where(j => i.Login == j.Login).ToList();
                                foreach (var j in match)
                                {
                                    ipFromStr = ConvertToIP(i.IpFrom);
                                    ipToStr = ConvertToIP(i.IpTo);

                                    content_all += $@"('{record.Server}', {i.Login}, '{j.Name}', {j.Enable}, '{i.Groups}', {i.IpFilter}, '{ipFromStr}', '{ipToStr}', {i.ManagerRights},  {i.Admin} ,  {i.Reports} ,
                                    {i.Email} ,  {i.News} ,  {i.Online} ,  {i.Plugins} ,  {i.Notifications} ,  {i.SeeTrades} ,  { i.Money} ,  {i.Riskman} ,{i.Logs} ,  {i.MarketWatch} ,
                                    {i.UserDetails} ,  {i.ServerReports} , {i.Market} ,  {i.Broker} ,  {i.Trades} ,  {i.InfoDepth} ,  '{present_time}' )";
                                    if (i == man_all.Last()) //man_all的最後一個
                                    {
                                        content_all += ";";
                                    }
                                    else
                                    {
                                        content_all += ",";
                                    }
                                }
                            }
                            catch (InvalidCastException)
                            {
                                Console.WriteLine("No manager permissions.");
                            };
                        }
                        sql_command += content_all;

                        try
                        {
                            MySqlCommand cmd = new(sql_command, conn);
                            cmd.ExecuteNonQuery();
                            Console.WriteLine("[Add All Data Completed]");
                        }
                        catch (InvalidCastException)
                        {
                            Console.WriteLine("An error occurred while executing SQL.");
                        };
                    }
                    else
                    {
                        Console.WriteLine("[Connection Failed]");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception on '{record.Server}': {ex.Message}");
                }
                finally
                {
                    AIROE.Disconnect();
                    Console.WriteLine($"[END]{record.Server}");
                }
            }
            conn.Close();

            return 0;
        }

        public static List<MT4ServerRecord> ReadMT4ServerSheet(string filePath)
        {
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            List<MT4ServerRecord> records = new();
            FileInfo fileInfo = new(filePath);

            using ExcelPackage package = new(fileInfo);
            var workbook = package.Workbook;
            var worksheet = workbook.Worksheets["MT4_Server"];

            if (worksheet != null)
            {
                int rowCount = worksheet.Dimension.Rows;

                for (int row = 2; row <= rowCount; row++) // Assuming first row is header
                {
                    MT4ServerRecord record = new()
                    {
                        Server = worksheet.Cells[row, 1].Text,
                        Connect = worksheet.Cells[row, 2].Text,
                        Login = worksheet.Cells[row, 3].Text,
                        Password = worksheet.Cells[row, 4].Text
                    };
                    records.Add(record);
                }
            }
            return records;
        }

        public static SQLServerRecord ReadSQLServerSheet(string filePath)
        {
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            List<SQLServerRecord> records = new();
            FileInfo fileInfo = new(filePath);

            using ExcelPackage package = new(fileInfo);
            var workbook = package.Workbook;
            var worksheet = workbook.Worksheets["SQL_Server"];

            if (worksheet != null)
            {
                if (worksheet.Dimension.Rows >= 2) // Ensure there is at least one row of data
                {
                    return new SQLServerRecord
                    {

                        //     // sql_server(excel)
                        //        string SQL_Host = sqlRecords.Host;
                        //        int SQL_Port = sqlRecords.Port;
                        //        string SQL_User = sqlRecords.User;
                        //        string SQL_Password = sqlRecords.Password;
                        //        string SQL_DB = sqlRecords.DB;
                        //        int SQL_TimeZoneAdd = sqlRecords.TimeZoneAdd;
                        Host = worksheet.Cells[2, 1].Text,
                        Port = worksheet.Cells[2, 2].GetValue<int>(), // 假設第二列是 Port，這裡直接取得 int 值
                        User = worksheet.Cells[2, 3].Text,
                        Password = worksheet.Cells[2, 4].Text,
                        DB = worksheet.Cells[2, 5].Text,
                        TimeZoneAdd = worksheet.Cells[2, 6].GetValue<int>(),
                    };
                }
            }
            return null;
        }


    }
}
