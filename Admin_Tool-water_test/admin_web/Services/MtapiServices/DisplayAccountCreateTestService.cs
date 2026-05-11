using MetaQuotes.MT5CommonAPI;
using MetaQuotes.MT5ManagerAPI;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using OfficeOpenXml;
using P23.MetaTrader4.Manager;
using P23.MetaTrader4.Manager.Contracts;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using static admin_web.Models.Mtapiuse.DisplayAccountCreate_Model;
using static admin_web.Models.ServerModel;

namespace admin_web.Services.MtapiServices
{
    public class DisplayAccountCreateTestService
    {
        // variable
        private static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "DisplayAccountCreate");
        private static readonly string LogPath = Path.Combine(ToolPath, "Log_Test");
        private static readonly string BrandRulePath = Path.Combine(ToolPath, "Template", "Template.xlsx");
        private static Dictionary<string, ServerRecord> server_dict = new(); // MT連線資訊
        private static Dictionary<string, Login_Record> loginRecords = new(); // admin tool account
        private static List<Eod_price_Insert_Cop> eod_price_list = new(); // 換匯用
        private static Dictionary<(string Brand, List<string> Servers), RuleRecord> Brand_Rule_Dict = new(); // 品牌規範
        private static IFormFile InputFile; // 使用者上傳檔
        private static string IP_Mode; // 使用者當前選擇PROXY/DC
        private static readonly string connectionString = UniversalService.sql_connectionString;

        private static readonly string mt4connectionString =
        $"server=live-mt4tdt-reportdb.vi-data.net;" +
        $"user=risktp;" +
        $"password=fSvz7WEAC5WoAmvdVk547Shx;" +
        $"port=3306;" +
        $"charset=utf8;";

        private static readonly string mt5connectionString =
        $"server=192.168.1.48;" +
        $"user=risk_read;" +
        $"password=EdCF3r9zEfPk3bwK;" +
        $"port=3306;" +
        $"charset=utf8;";

        private static readonly string mt5TestconnectionString =
        $"server=test-mt5vjp-reportdb.vi-data.net;" +
        $"user=mt5_report_rw;" +
        $"password=AwKWnn%dgbd3A4D9;" +
        $"port=3306;" +
        $"charset=utf8;";

        private static readonly string eod_price_connectionString =
        $"server=192.168.1.179;" +
        $"user=tp_admin_reader;" +
        $"password=sMbXpVfGRKnYwDM3;" +
        $"port=3306;" +
        $"charset=utf8;";

        private static void Initiallize()
        {
            server_dict = GetAllServerIP(); // 到SQL取所有Server IP
            Brand_Rule_Dict = GetBrandRule(); // 取得品牌規範
            GetDailyFx(); // 取得換匯用資料
            loginRecords = DisplayAccountCreateService.Get_tool_account();
        }

        // 從SQL下載歷史創建帳號紀錄
        public static (MemoryStream stream, string fileName) ExportHistoryLog()
        {
            Initiallize();
            DataTable dataTable = new DataTable();

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM admin_tool_log.display_account_create_test";
                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }
                }
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("HistoryLog");
                worksheet.Cells["A1"].LoadFromDataTable(dataTable, true);

                for (int col = 1; col <= dataTable.Columns.Count; col++)
                {
                    DataColumn column = dataTable.Columns[col - 1];

                    if (column.DataType == typeof(DateTime))
                    {
                        if (column.ColumnName == "Time")
                        {
                            worksheet.Column(col).Style.Numberformat.Format = "yyyy-MM-dd HH:mm:ss";
                        }
                        else
                        {
                            worksheet.Column(col).Style.Numberformat.Format = "yyyy-MM-dd";
                        }
                    }
                }

                string fileName = "創建展示帳號HistoryLog.xlsx";
                var stream = new MemoryStream(package.GetAsByteArray());

                return (stream, fileName);
            }
        }

        // 主程式
        public static LogViewModel MainProgram(string mode, IFormFile file)
        {
            Initiallize();
            InputFile = file; // input檔設全域
            IP_Mode = mode; // proxy/dc設全域

            try
            {
                // 執行前先刷新，刷新失敗則報錯
                if (!Test_insert_Mysql_Capital_pool())
                {
                    return null;
                }

                string FileName;
                var process_Log = new List<Log_Record>();
                var tool_Log = new List<Sql_Log_Record>();
                var input_check_log = Check_All_Input_TESR();

                // 先檢查Balance, Account
                if (input_check_log.Any(record => record.Status != "Legal"))
                {
                    FileName = DisplayAccountCreateService.ExportLogToExcel(input_check_log);
                }
                else
                {
                    var MT4_Log = MT4_API();
                    var MT5_Log = MT5_API();

                    process_Log.AddRange(MT4_Log.Item1);
                    process_Log.AddRange(MT5_Log.Item1);

                    tool_Log.AddRange(MT4_Log.Item2);
                    tool_Log.AddRange(MT5_Log.Item2);
                    tool_Log.RemoveAll(record => string.IsNullOrEmpty(record.Login));

                    FileName = DisplayAccountCreateService.ExportLogToExcel(process_Log);
                    InsertLogRecordsToDatabase(tool_Log);
                }

                // 執行後再刷新，不管是否成功，都要回傳創建完的結果
                Test_insert_Mysql_Capital_pool();

                return string.IsNullOrEmpty(FileName) ? null : DisplayAccountCreateService.ReadExcel_View(FileName);
            }
            catch
            {
                return null;
            }
        }

        // 讀取Input檔案
        public static Dictionary<string, List<Input_Record>> Read_Input(string MetaTrader)
        {
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            var inputRecords = new Dictionary<string, List<Input_Record>>();
            var loginRecords = new Dictionary<string, Login_Record>();

            using (var stream = new MemoryStream())
            {
                InputFile.CopyTo(stream);
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet_input = package.Workbook.Worksheets[$"{MetaTrader}_input"];

                    if (worksheet_input != null)
                    {
                        int rows = worksheet_input.Dimension.Rows;
                        var headerRow = worksheet_input.Cells[1, 1, 1, worksheet_input.Dimension.End.Column].Select(cell => cell.Text.Trim()).ToArray();

                        for (int row = 2; row <= rows; row++)
                        {
                            var record = new Input_Record
                            {
                                Server = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Server") + 1].Text,
                                Name = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Name") + 1].Text,
                                Group = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Group") + 1].Text,
                                Email = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Email") + 1].Text,
                                Leverage = string.IsNullOrEmpty(worksheet_input.Cells[row, Array.IndexOf(headerRow, "Leverage") + 1].Text) ?
                                            "100" : worksheet_input.Cells[row, Array.IndexOf(headerRow, "Leverage") + 1].Text,
                                Balance = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Balance") + 1].Text ?? string.Empty,
                                Balance_Comment = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Balance_Comment") + 1].Text ?? string.Empty,
                                // 以下為LOG
                                Sales = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Sales") + 1].Text ?? string.Empty,
                                Applicant = worksheet_input.Cells[row, Array.IndexOf(headerRow, "申請人") + 1].Text ?? string.Empty,
                                Department = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Department") + 1].Text ?? string.Empty,
                                Approval = worksheet_input.Cells[row, Array.IndexOf(headerRow, "審批") + 1].Text ?? string.Empty,
                            };

                            if (record.Server.Length == 0)
                            {
                                continue;
                            }

                            if (!inputRecords.ContainsKey(record.Server))
                            {
                                inputRecords[record.Server] = new List<Input_Record>();
                            }

                            inputRecords[record.Server].Add(record);
                        }
                    }
                }
            }

            return inputRecords;
        }

        // MT4 Server 執行流程
        public static (List<Log_Record>, List<Sql_Log_Record>) MT4_API()
        {
            var All_Log = new List<Log_Record>();
            var Sql_Log = new List<Sql_Log_Record>();

            var inputRecords = Read_Input("mt4");

            var rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var aa = Path.Combine(rootPath, Environment.Is64BitProcess ? "mtmanapi64.dll" : "mtmanapi.dll");
            var AIROE = new ClrWrapper(aa);

            foreach (var item in inputRecords)
            {
                // Server連接
                Login_Record server_config = new();
                var server = item.Key;
                try
                {
                    if (!loginRecords.TryGetValue(server, out server_config))
                    {
                        // 沒有設置server
                        throw new Exception("無該Server的連線登入設置");
                    }

                    if (server_config.Login == string.Empty || server_config.Password == string.Empty)
                    {
                        throw new Exception($"未設置 {server} 的登入資訊");
                    }

                    int retry_count = 0;
                    while (retry_count < 3)
                    {
                        var server_ip = IP_Mode == "proxy" ? server_dict[server].SERVER_PROXY : server_dict[server].SERVER_DC;
                        var res_connect = AIROE.Connect(server_ip);
                        var res_login = AIROE.Login(Convert.ToInt32(server_config.Login), server_config.Password);
                        if (res_connect == 0 && res_login == 0)
                        {
                            break;
                        }
                        AIROE.Disconnect();
                        retry_count += 1;
                        Thread.Sleep(1500);
                    }
                    if (retry_count > 2)
                    {
                        // 登入連線異常
                        AIROE.Disconnect();
                        throw new Exception("登入連線異常");
                    }
                }
                catch (Exception ex)
                {
                    // Log
                    var logRecord = new Log_Record
                    {
                        Server = item.Key,
                        Status = ex.Message
                    };
                    All_Log.Add(logRecord);
                    continue;
                }

                // SQL連接設置
                if (!server_dict.TryGetValue(server, out var sql_config))
                {
                    throw new Exception($"找不到 Server:{server} 的資料庫設置");
                }
                string mt_sql_connection =
                    $"server={sql_config.SQL_HOST};" +
                    $"user={sql_config.SQL_USER};" +
                    $"password={sql_config.SQL_PASSWORD};" +
                    $"port={sql_config.SQL_PORT};" +
                    $"charset=utf8;";

                // 創建帳號
                foreach (var input_record in item.Value)
                {
                    // Log
                    var logRecord = new Log_Record();
                    var sqlRecord = new Sql_Log_Record();
                    logRecord.Server = item.Key;
                    var input = input_record;
                    try
                    {
                        // 給前端的log
                        logRecord.Name = input.Name;
                        logRecord.Group = input.Group;
                        logRecord.Leverage = input.Leverage;
                        logRecord.Email = input.Email;
                        logRecord.Balance = input.Balance.Replace(",", "");
                        logRecord.Balance_Comment = input.Balance_Comment;

                        // 防呆[Start] --------------------------------------------------
                        // 檢查必填
                        if (input.Name == string.Empty || input.Group == string.Empty || input.Balance == string.Empty || input.Balance_Comment == string.Empty)
                        {
                            throw new Exception("Name, Group, Balance, Balance_Comment 為必填欄位，請勿空白");
                        }
                        // 檢查槓桿設置
                        if (Convert.ToDouble(input.Leverage) > 1000 || Convert.ToDouble(input.Leverage) <= 0)
                        {
                            throw new Exception("槓桿設置錯誤，0 < Leverage <= 1000");
                        }
                        // 檢查Name設置
                        if (input.Name.Length > 128)
                        {
                            throw new Exception("Name超出長度限制(128字元)");
                        }
                        // 檢查入金Comment設置
                        if (input.Balance_Comment.Length > 31)
                        {
                            throw new Exception("入金Comment超出長度限制(31字元)");
                        }
                        // email長度限制
                        if (input.Email.Length > 47)
                        {
                            throw new Exception("Email長度限制(47字元)");
                        }
                        // 防呆[End] --------------------------------------------------

                        // step1: 品牌規範
                        var check_BS = Brand_Specification(server, input);
                        if (check_BS.Item1 != "SUCCESS")
                        {
                            throw new Exception(check_BS.Item1);
                        }
                        input = check_BS.Item2;

                        // step2: 查詢創建人的名字
                        string create_by = string.Empty;
                        using (MySqlConnection connection = new MySqlConnection(mt_sql_connection))
                        {
                            connection.Open();

                            string query = $@"
                                SELECT u.Name
                                FROM {sql_config.SQL_NAME}.mt4_users u
                                WHERE LOGIN = {server_config.Login}
                                ";

                            using (MySqlCommand cmd = new MySqlCommand(query, connection))
                            {
                                using (MySqlDataReader reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        create_by = reader["Name"].ToString();
                                    }
                                    else
                                    {
                                        throw new Exception("無法從資料庫取得創建人的Name");
                                    }
                                }
                            }
                        }

                        // step3: 創建帳號
                        logRecord.Login = input.Login;
                        logRecord.User_Comment = input.User_Comment;

                        var urd = new UserRecord()
                        {
                            Enable = 1,
                            Name = input.Name,
                            Login = Convert.ToInt32(input.Login),
                            Group = input.Group,
                            Leverage = Convert.ToInt32(input.Leverage),
                            Email = input.Email,
                            Comment = input.User_Comment,
                        };
                        int res_urdNew = AIROE.UserRecordNew(urd);
                        if (res_urdNew != 0)
                        {
                            throw new Exception("創建帳號失敗，該帳號可能已存在或其他設置有誤");
                        }

                        // 帳號創建成功才紀錄該LOG到資料庫
                        sqlRecord.MT4or5 = "MT4";
                        sqlRecord.Server = input.Server;
                        sqlRecord.Login = input.Login;
                        sqlRecord.Name = input.Name;
                        sqlRecord.Group = input.Group;
                        sqlRecord.Email = input.Email;
                        sqlRecord.Created_by = create_by;
                        sqlRecord.Leverage = input.Leverage;
                        sqlRecord.User_Comment = input.User_Comment;
                        sqlRecord.Sales = input.Sales;
                        sqlRecord.Applicant = input.Applicant;
                        sqlRecord.Department = input.Department;
                        sqlRecord.Date_Create = DateTime.Now.ToString("yyyy-MM-dd");
                        sqlRecord.Approval = input.Approval;
                        sqlRecord.Date_Limit = DateTime.Now.AddDays(Convert.ToInt32(input.Expired)).ToString("yyyy-MM-dd");
                        sqlRecord.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        // step4: 更改密碼
                        string master_password = DisplayAccountCreateService.GeneratePassword();
                        string invest_password = DisplayAccountCreateService.GeneratePassword();
                        var res_pwdSetMaster = AIROE.UserPasswordSet(Convert.ToInt32(input.Login), master_password, 0, 1);
                        var res_pwdSetInvestor = AIROE.UserPasswordSet(Convert.ToInt32(input.Login), invest_password, 1, 1);
                        if (res_pwdSetMaster != 0 || res_pwdSetInvestor != 0)
                        {
                            throw new Exception("修改密碼時發生錯誤");
                        }
                        logRecord.Password = sqlRecord.Password = master_password;
                        logRecord.Investor_Password = sqlRecord.Investor_Password = invest_password;

                        // step5: 入金
                        var data = new TradeTransInfo()
                        {
                            Type = TradeTransactionType.BrBalance,
                            Cmd = TradeCommand.Balance,
                            OrderBy = Convert.ToInt32(input.Login),
                            Price = Convert.ToDouble(input.Balance),
                            Comment = input.Balance_Comment
                        };
                        var res = AIROE.TradeTransaction(data);
                        if (res != 0)
                        {
                            throw new Exception("Balance入金失敗");
                        }
                        sqlRecord.Balance = input.Balance;
                        sqlRecord.Balance_Comment = input.Balance_Comment;

                        // step6: 更新當前最新帳號
                        using (MySqlConnection connection = new MySqlConnection(connectionString))
                        {
                            connection.Open();

                            string query = $@"
                                    UPDATE `admin_tool_config`.`display_account_create` 
                                    SET `LOGIN` = '{Convert.ToInt32(input.Login) + 1}' 
                                    WHERE (`BRAND` = '{input.Brand}' AND `SERVER` = '{server}')
                            ";

                            using (MySqlCommand cmd = new MySqlCommand(query, connection))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // step7: 完成
                        logRecord.Status = "SUCCESS";
                    }
                    catch (Exception ex)
                    {
                        logRecord.Status = ex.Message;
                    }
                    finally
                    {
                        All_Log.Add(logRecord);
                        Sql_Log.Add(sqlRecord);
                    }
                }

                // 中斷連線
                AIROE.Disconnect();
            }

            return (All_Log, Sql_Log);
        }

        // MT5 Server 執行流程
        public static (List<Log_Record>, List<Sql_Log_Record>) MT5_API()
        {
            var All_Log = new List<Log_Record>();
            var Sql_Log = new List<Sql_Log_Record>();

            var inputRecords = Read_Input("mt5");

            string _rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            SMTManagerAPIFactory.Initialize(_rootPath);
            var manager = SMTManagerAPIFactory.CreateManager(SMTManagerAPIFactory.ManagerAPIVersion, out var _);

            foreach (var item in inputRecords)
            {
                // Server連接
                Login_Record server_config = new();
                var server = item.Key;
                try
                {
                    if (!loginRecords.TryGetValue(server, out server_config))
                    {
                        // 沒有設置server
                        throw new Exception("無該Server的連線登入設置");
                    }

                    if (server_config.Login == string.Empty || server_config.Password == string.Empty)
                    {
                        throw new Exception($"未設置 {server} 的登入資訊");
                    }

                    int retry_count = 0;
                    while (retry_count < 3)
                    {
                        var server_ip = IP_Mode == "proxy" ? server_dict[server].SERVER_PROXY : server_dict[server].SERVER_DC;
                        var res_connect = manager.Connect(server_ip, Convert.ToUInt32(server_config.Login), server_config.Password, null,
                                        CIMTManagerAPI.EnPumpModes.PUMP_MODE_SYMBOLS, 3600000);
                        if (res_connect == 0)
                        {
                            break;
                        }
                        manager.Disconnect();
                        retry_count += 1;
                        Thread.Sleep(1500);
                    }
                    if (retry_count > 2)
                    {
                        // 登入連線異常
                        manager.Disconnect();
                        throw new Exception("登入連線異常");
                    }
                }
                catch (Exception ex)
                {
                    // Log
                    var logRecord = new Log_Record
                    {
                        Server = item.Key,
                        Status = ex.Message
                    };
                    All_Log.Add(logRecord);
                    continue;
                }

                // SQL連接設置
                if (!server_dict.TryGetValue(server, out var sql_config))
                {
                    throw new Exception($"找不到 Server:{server} 的資料庫設置");
                }
                string mt_sql_connection =
                    $"server={sql_config.SQL_HOST};" +
                    $"user={sql_config.SQL_USER};" +
                    $"password={sql_config.SQL_PASSWORD};" +
                    $"port={sql_config.SQL_PORT};" +
                    $"charset=utf8;";

                foreach (var record in item.Value)
                {
                    Input_Record input = record; // 另存資料

                    // Log
                    var logRecord = new Log_Record();
                    var sqlRecord = new Sql_Log_Record();
                    logRecord.Server = item.Key;

                    try
                    {
                        // 給前端的log
                        logRecord.Name = input.Name;
                        logRecord.Group = input.Group;
                        logRecord.Leverage = input.Leverage;
                        logRecord.Email = input.Email;
                        logRecord.Balance = input.Balance.Replace(",", "");
                        logRecord.Balance_Comment = input.Balance_Comment;

                        // 防呆[Start] --------------------------------------------------
                        // 檢查必填
                        if (input.Name == string.Empty || input.Group == string.Empty || input.Balance == string.Empty || input.Balance_Comment == string.Empty)
                        {
                            throw new Exception("Name, Group, Balance, Balance_Comment 為必填欄位，請勿空白");
                        }
                        // 檢查槓桿設置
                        if (Convert.ToDouble(input.Leverage) > 1000 || Convert.ToDouble(input.Leverage) <= 0)
                        {
                            throw new Exception("槓桿設置錯誤，0 < Leverage <= 1000");
                        }
                        // 檢查Name設置
                        if (input.Name.Length > 128)
                        {
                            throw new Exception("Name超出長度限制(128字元)");
                        }
                        // 檢查入金Comment設置
                        if (input.Balance_Comment.Length > 31)
                        {
                            throw new Exception("入金Comment超出長度限制(31字元)");
                        }
                        // 防呆[End] --------------------------------------------------

                        // step1: 品牌規範
                        var check_BS = Brand_Specification(server, input);
                        if (check_BS.Item1 != "SUCCESS")
                        {
                            throw new Exception(check_BS.Item1);
                        }
                        input = check_BS.Item2;

                        // Step2: 查詢組別密碼最小長度規範
                        logRecord.Login = input.Login;
                        logRecord.User_Comment = input.User_Comment;

                        uint AuthPasswordMin = 0;
                        using (MySqlConnection connection = new MySqlConnection(mt_sql_connection))
                        {
                            connection.Open();
                            string query = $@"
                                SELECT *
                                FROM {sql_config.SQL_NAME}.mt5_groups g
                                WHERE g.Group = '{input.Group.Replace("\\", "\\\\")}'
                                ";

                            using (MySqlCommand cmd = new MySqlCommand(query, connection))
                            {
                                using (MySqlDataReader reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        AuthPasswordMin = (uint)reader["AuthPasswordMin"];
                                    }
                                    else
                                    {
                                        throw new Exception("無法從資料庫取得組別密碼最小限制");
                                    }
                                }
                            }
                        }

                        // step3: 製作密碼
                        string MasterPassword = DisplayAccountCreateService.GeneratePassword(AuthPasswordMin);
                        string InvestorPassword = DisplayAccountCreateService.GeneratePassword(AuthPasswordMin);

                        // step4: 創建人的名字
                        string create_by = string.Empty;
                        using (MySqlConnection connection = new MySqlConnection(mt_sql_connection))
                        {
                            connection.Open();

                            string query = $@"
                                SELECT u.Name
                                FROM {sql_config.SQL_NAME}.mt5_users u
                                WHERE LOGIN = {server_config.Login}
                                ";

                            using (MySqlCommand cmd = new MySqlCommand(query, connection))
                            {
                                using (MySqlDataReader reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        create_by = reader["Name"].ToString();
                                    }
                                    else
                                    {
                                        throw new Exception("無法從資料庫取得創建人的Name");
                                    }
                                }
                            }
                        }

                        // step5: 創建帳號
                        CIMTUser urd = manager.UserCreate();
                        urd.Login(Convert.ToUInt32(input.Login));
                        urd.Group(input.Group);
                        urd.Name(input.Name);
                        urd.Leverage((uint)Convert.ToInt32(input.Leverage));
                        urd.EMail(input.Email);
                        urd.Comment(input.User_Comment);
                        urd.Country("");
                        urd.Rights((CIMTUser.EnUsersRights)355);
                        MTRetCode res_userAdd = manager.UserAdd(urd, MasterPassword, InvestorPassword);
                        if (res_userAdd != MTRetCode.MT_RET_OK)
                        {
                            throw new Exception("創建帳號失敗，該帳號可能已存在或設置有誤");
                        }

                        // 帳號創建成功才紀錄該LOG到資料庫
                        sqlRecord.MT4or5 = "MT5";
                        logRecord.Password = sqlRecord.Password = MasterPassword;
                        logRecord.Investor_Password = sqlRecord.Investor_Password = InvestorPassword;
                        sqlRecord.Server = input.Server;
                        sqlRecord.Login = input.Login;
                        sqlRecord.Name = input.Name;
                        sqlRecord.Group = input.Group;
                        sqlRecord.Email = input.Email;
                        sqlRecord.Created_by = create_by;
                        sqlRecord.Leverage = input.Leverage;
                        sqlRecord.User_Comment = input.User_Comment;
                        sqlRecord.Sales = input.Sales;
                        sqlRecord.Applicant = input.Applicant;
                        sqlRecord.Department = input.Department;
                        sqlRecord.Date_Create = DateTime.Now.ToString("yyyy-MM-dd");
                        sqlRecord.Approval = input.Approval;
                        sqlRecord.Date_Limit = DateTime.Now.AddDays(Convert.ToInt32(input.Expired)).ToString("yyyy-MM-dd");
                        sqlRecord.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        // step6: 入金
                        var res = manager.DealerBalance(Convert.ToUInt32(input.Login), Convert.ToDouble(input.Balance), 2, input.Balance_Comment, out var _);
                        if (res != MTRetCode.MT_RET_REQUEST_DONE)
                        {
                            throw new Exception("Balance入金失敗");
                        }
                        sqlRecord.Balance = input.Balance;
                        sqlRecord.Balance_Comment = input.Balance_Comment;

                        // step7: 更新當前最新帳號
                        using (MySqlConnection connection = new MySqlConnection(connectionString))
                        {
                            connection.Open();

                            string query = $@"
                                    UPDATE `admin_tool_config`.`display_account_create` 
                                    SET `LOGIN` = '{Convert.ToInt32(input.Login) + 1}' 
                                    WHERE (`BRAND` = '{input.Brand}' AND `SERVER` = '{server}')
                            ";

                            using (MySqlCommand cmd = new MySqlCommand(query, connection))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // step8: 完成
                        logRecord.Status = "SUCCESS";
                    }
                    catch (Exception ex)
                    {
                        // 例外處理
                        logRecord.Status = ex.Message;
                    }
                    finally
                    {
                        All_Log.Add(logRecord);
                        Sql_Log.Add(sqlRecord);
                    }
                }

                // 中斷連線
                manager.Disconnect();
            }

            return (All_Log, Sql_Log);
        }

        // 先檢查當前input總和是否會超過資金池和帳號數量上限
        public static List<Log_Record> Check_All_Input_TESR()
        {
            var Log_List = new List<Log_Record>();

            var mt4_input = Read_Input("mt4");
            var mt5_input = Read_Input("mt5");
            var input = mt4_input.Concat(mt5_input).ToDictionary(x => x.Key, x => x.Value);
            Dictionary<string, List<Input_Record>> brand_dict = new();
            Dictionary<string, (int TESR_ACCOUNT, int TESR_BALANCE)> capital_dict = new();

            foreach (var entry in input)
            {
                var server = entry.Key;
                var records = entry.Value;

                foreach (var record in records)
                {
                    var all_match_brand = Brand_Rule_Dict.Where(rule_record => rule_record.Key.Servers.Contains(server) && Regex.IsMatch(record.Balance_Comment, rule_record.Value.Deposite_Comment));
                    if (all_match_brand.Count() != 1) // > 1 or = 0
                    {
                        // 若辨別出不只一個品牌規範，則忽略(規範檢查時會抓到錯誤)
                        continue;
                    }
                    var match_brand = all_match_brand.FirstOrDefault();
                    var _brand = match_brand.Key.Brand;

                    if (string.IsNullOrEmpty(_brand))
                    {
                        // 如果無符合的品牌，則忽略(規範檢查時會抓到錯誤)
                        continue;
                    }

                    if (!brand_dict.ContainsKey(_brand))
                    {
                        brand_dict[_brand] = new List<Input_Record>();
                    }
                    brand_dict[_brand].Add(record);
                }
            }

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = $@"
                        SELECT *
                        FROM admin_tool.capital_pool_test";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string BRAND = reader["BRAND"].ToString();
                            int TESR_ACCOUNT = reader.GetInt32("TESR_ACCOUNT");
                            int TESR_BALANCE = reader.GetInt32("TESR_BALANCE");

                            capital_dict[BRAND] = (TESR_ACCOUNT, TESR_BALANCE);
                        }
                    }
                }
            }

            foreach (var entry in brand_dict)
            {
                var brand = entry.Key;
                var records = entry.Value;
                try
                {
                    if (records.Any(r => !new[] { "N", "Y" }.Contains(r.Approval) || !string.IsNullOrEmpty(r.Server) && string.IsNullOrEmpty(r.Approval)))
                    {
                        throw new Exception("審批只能填寫 Y(通過)、N(未通過)");
                    }

                    // 只計算未審批的
                    records = records.Where(r => r.Approval == "N").ToList();

                    // 如果沒有未審批的，則忽略規範
                    if (records.Count == 0)
                    {
                        continue;
                    }

                    var (tesr_acc, tesr_balance) = capital_dict[brand];
                    int total_account = records.Count();
                    double total_balance = 0;

                    // 找品牌規範
                    var match_brand = Brand_Rule_Dict.FirstOrDefault(rule_record => rule_record.Key.Brand == brand);
                    var limit_acc = Convert.ToDouble(match_brand.Value.Max_Account);
                    var limit_bal = Convert.ToDouble(match_brand.Value.Max_Amount);

                    // 帳號數量比對
                    if (total_account + tesr_acc > limit_acc)
                    {
                        throw new Exception($"展示帳號可創數量: {limit_acc - tesr_acc}");
                    }

                    // 計算總Balance
                    foreach (var item in records)
                    {
                        string input_ccy = item.Group.Length >= 3 ? item.Group.Substring(item.Group.Length - 3) : item.Group;
                        if (double.TryParse(item.Balance, out var input_balance) && eod_price_list.Any(eod_record => eod_record.Ccy == input_ccy))
                        {
                            total_balance += input_balance * eod_price_list.FirstOrDefault(eod_record => eod_record.Ccy == input_ccy).To_Usd;
                        }
                        // else 無法辨別幣別做換匯 或 無法辨別入金金額 then 不計算到總資金 (第二次規範檢查時會抓出報錯)
                    }
                    if (total_balance + tesr_balance >= limit_bal)
                    {
                        throw new Exception($"資金池可用額度: {limit_bal - tesr_balance}");
                    }

                    Log_List.Add(new Log_Record
                    {
                        Server = $"品牌：{brand}",
                        Status = "Legal"
                    });
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("was not present in the dictionary"))
                    {
                        Log_List.Add(new Log_Record
                        {
                            Server = $"品牌：{brand}",
                            Status = "查無此品牌規範"
                        });
                    }
                    else
                    {
                        Log_List.Add(new Log_Record
                        {
                            Server = $"品牌：{brand}",
                            Status = ex.Message
                        });
                    }
                }
            }

            return Log_List;
        }

        // 品牌規範檢查
        public static (string, Input_Record) Brand_Specification(string server, Input_Record record)
        {
            try
            {
                // 先確認要使用的規範(用Server和入金Comment判斷欲使用的品牌規範)
                var all_match_brand = Brand_Rule_Dict.Where(brand_record => brand_record.Key.Servers.Contains(server) && Regex.IsMatch(record.Balance_Comment, brand_record.Value.Deposite_Comment));
                if (all_match_brand.Count() > 1)
                {
                    // 若辨別出不只一個品牌規範，則報錯(規範限制僅能有一個)
                    throw new Exception("出現了一個以上符合的品牌規範，請檢查品牌規範表設置是否有誤");
                }
                else if (all_match_brand.Count() == 0)
                {
                    throw new Exception("找不到符合的品牌規範");
                }
                var match_brand = all_match_brand.FirstOrDefault();

                var brand = match_brand.Key.Brand;
                record.Brand = brand;

                if (string.IsNullOrEmpty(brand))
                {
                    // 如果無符合的品牌，則報錯
                    throw new Exception($"'{server}' Server下無符合 '{record.Balance_Comment}' 的出入金Comment設置規範");
                }
                var brand_rule = match_brand.Value;

                // Check: 出入金設置
                if (!double.TryParse(record.Balance, out var input_balance))
                {
                    throw new Exception($"無法辨別入金金額");
                }
                if (input_balance < 0)
                {
                    throw new Exception($"入金金額不可小於0");
                }

                // Check: 入金金額限制
                string input_ccy = record.Group.Length >= 3 ? record.Group.Substring(record.Group.Length - 3) : record.Group; // 組名取幣別

                if (!double.TryParse(brand_rule.Max_Deposite, out var max_deposite))
                {
                    throw new Exception($"無法解析規範的最大入金金額限制");
                }

                var eod_record = eod_price_list.FirstOrDefault(eod_record => eod_record.Ccy == input_ccy);
                if (eod_record == null)
                {
                    throw new Exception($"找不到對應幣別的資料");
                }

                double input_deposit_tousd = input_balance * eod_record.To_Usd;
                if (input_deposit_tousd > max_deposite)
                {
                    throw new Exception($"入金金額: {Math.Round(input_deposit_tousd, 2)} (USD) 超過規範限制，不可超過 {max_deposite}");
                }

                // Check: 帳號數量上限 + 資金池
                if (record.Approval == "N")
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        string query = $@"
                        SELECT *
                        FROM admin_tool.capital_pool_test
                        WHERE BRAND = '{brand}'";

                        using (MySqlCommand cmd = new MySqlCommand(query, connection))
                        {
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    double TESR_ACCOUNT = reader.GetDouble("TESR_ACCOUNT");
                                    double TESR_BALANCE = reader.GetDouble("TESR_BALANCE");
                                    if (double.TryParse(brand_rule.Max_Account, out var max_account))
                                    {
                                        if (TESR_ACCOUNT + 1 > max_account)
                                        {
                                            throw new Exception($"展示帳號數量已達規範上限");
                                        }
                                    }
                                    else
                                    {
                                        throw new Exception($"無法辨別展示帳號上限設置");
                                    }

                                    if (double.TryParse(brand_rule.Max_Amount, out var max_amount))
                                    {
                                        if (input_deposit_tousd + TESR_BALANCE >= max_amount)
                                        {
                                            throw new Exception($"當前入金會超過資金池限制");
                                        }
                                    }
                                    else
                                    {
                                        throw new Exception($"無法辨別資金池上限設置{brand_rule.Max_Amount}");
                                    }
                                }
                                else
                                {
                                    throw new Exception($"找不到該品牌的資金池及當前帳號數量");
                                }
                            }
                        }
                    }
                }

                // Check: 組別
                if (server.Contains("MT5"))
                {
                    // MT5 Server組別檢查
                    if (!brand_rule.MT5_Group.Contains(record.Group))
                    {
                        throw new Exception("該MT5組別不符合品牌設置規範");
                    }
                }
                else
                {
                    // MT4 Server組別檢查
                    if (!brand_rule.MT4_Group.Contains(record.Group))
                    {
                        throw new Exception("該MT4組別不符合品牌設置規範");
                    }
                }

                // Check: Login號碼設置
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    string query = $@"
                        SELECT ac.LOGIN
                        FROM admin_tool_config.display_account_create ac
                        WHERE 1 = 1
	                        AND ac.BRAND = '{brand}'
                            AND ac.SERVER = '{server}'";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                record.Login = reader["LOGIN"].ToString();
                            }
                            else
                            {
                                throw new Exception($"無法取得該Server最新的Login");
                            }
                        }
                    }
                }

                var acc_segment = brand_rule.Account_Segment.Replace("\n", "").Trim().Split("~");
                if (acc_segment.Length == 2 && int.TryParse(acc_segment[0], out var acc_from) && int.TryParse(acc_segment[1], out var acc_to))
                {
                    // 不做事
                }
                else
                {
                    throw new Exception($"無法辨別帳號區段限制");
                }

                ServerRecord server_record = server_dict[server];
                List<int> loginList = new();
                string server_conn =
                    $"server={server_record.SQL_HOST};" +
                    $"user={server_record.SQL_USER};" +
                    $"password={server_record.SQL_PASSWORD};" +
                    $"port={server_record.SQL_PORT};" +
                    $"charset=utf8;";
                using (MySqlConnection connection = new MySqlConnection(server_conn))
                {
                    connection.Open();

                    string query = server_record.MT.ToLower() == "mt4" ?
                        $@"SELECT LOGIN FROM {server_record.SQL_NAME}.mt4_users WHERE LOGIN BETWEEN {acc_from} AND {acc_to};" : // mt4
                        $@"SELECT LOGIN FROM {server_record.SQL_NAME}.mt5_users WHERE LOGIN BETWEEN {acc_from} AND {acc_to};"; // mt5
                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (int.TryParse(reader["LOGIN"].ToString(), out int login))
                                {
                                    loginList.Add(login);
                                }
                            }
                        }
                    }
                }

                int input_login = Convert.ToInt32(record.Login);
                while (loginList.Contains(input_login) && input_login >= acc_from && input_login <= acc_to)
                {
                    input_login += 1;
                }

                if (input_login < acc_from || input_login > acc_to)
                {
                    throw new Exception($"當前可新增Login已超過規範號段限制");
                }
                record.Login = input_login.ToString();

                // 客戶信息(按品牌設置)
                if (string.IsNullOrEmpty(brand_rule.Account_Comment))
                {
                    throw new Exception("該品牌未設置客戶信息，請確認品牌規範表");
                }
                record.User_Comment = brand_rule.Account_Comment;

                // 設置逾期天數
                record.Expired = brand_rule.Expired;

                return ("SUCCESS", record); // 符合規範則回傳SUCCESS，並將設置回傳
            }
            catch (Exception ex)
            {
                return (ex.Message, record); // 遇到不符合規範的設置，則回傳該問題
            }
        }


        // 撈取所有Server Config
        private static Dictionary<string, ServerRecord> GetAllServerIP()
        {
            Dictionary<string, ServerRecord> dict = new();
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT *
                    FROM server_health.server_list sl
                    LEFT JOIN(
                        SELECT *
                        FROM server_health.sql_connect
                    ) sc on sl.REPLICA = sc.SERVER
                    WHERE 1 = 1
                        AND sl.SQL_NAME != ''
                        AND sl.SERVER_NAME != ''
                        AND sl.PROXY != ''
                        AND sl.SERVER_NAME REGEXP 'TEST'";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string serverName = reader["SERVER_NAME"].ToString();

                            var record = new ServerRecord
                            {
                                MT = reader["MT"].ToString(),
                                SERVER_PROXY = reader["PROXY"].ToString(),
                                SERVER_DC = reader["DC"].ToString(),
                                SQL_NAME = reader["SQL_NAME"].ToString(),
                                SQL_HOST = reader["HOST"].ToString(),
                                SQL_USER = reader["USER"].ToString(),
                                SQL_PASSWORD = reader["PASSWORD"].ToString(),
                                SQL_PORT = reader["PORT"].ToString()
                            };
                            dict[serverName] = record;
                        }
                    }
                }
            }
            return dict;
        }

        // 創建帳號紀錄寫進資料庫
        public static void InsertLogRecordsToDatabase(List<Sql_Log_Record> tool_Log)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    INSERT INTO admin_tool_log.display_account_create_test (
                        MT4or5, Server, Login, Password, Investor_Password, Name, `Group`, Email, Leverage, Comment,
                        Balance, Balance_Comment, 建立人, Sales,
                        申請人, Department, 創建日期, 檢查期限, 審批, Time
                    ) VALUES (
                        @MT4or5, @Server, @Login, @Password, @Investor_Password, @Name, @Group, @Email, @Leverage, @Comment,
                        @Balance, @Balance_Comment, @Created_by, @Sales,
                        @Applicant, @Department, @Date_Create, @Date_Limit, @Approval, @Time
                    );";

                foreach (var logRecord in tool_Log)
                {
                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@MT4or5", logRecord.MT4or5);
                        cmd.Parameters.AddWithValue("@Server", logRecord.Server);
                        cmd.Parameters.AddWithValue("@Login", logRecord.Login);
                        cmd.Parameters.AddWithValue("@Password", logRecord.Password);
                        cmd.Parameters.AddWithValue("@Investor_Password", logRecord.Investor_Password);
                        cmd.Parameters.AddWithValue("@Name", logRecord.Name);
                        cmd.Parameters.AddWithValue("@Group", logRecord.Group);
                        cmd.Parameters.AddWithValue("@Email", logRecord.Email);
                        cmd.Parameters.AddWithValue("@Leverage", logRecord.Leverage);
                        cmd.Parameters.AddWithValue("@Comment", logRecord.User_Comment);
                        cmd.Parameters.AddWithValue("@Balance", logRecord.Balance.Replace(",", ""));
                        cmd.Parameters.AddWithValue("@Balance_Comment", logRecord.Balance_Comment);
                        cmd.Parameters.AddWithValue("@Created_by", logRecord.Created_by);
                        cmd.Parameters.AddWithValue("@Sales", logRecord.Sales);
                        cmd.Parameters.AddWithValue("@Applicant", logRecord.Applicant);
                        cmd.Parameters.AddWithValue("@Department", logRecord.Department);
                        cmd.Parameters.AddWithValue("@Date_Create", logRecord.Date_Create);
                        cmd.Parameters.AddWithValue("@Date_Limit", logRecord.Date_Limit);
                        cmd.Parameters.AddWithValue("@Time", logRecord.Time);
                        cmd.Parameters.AddWithValue("@Approval", logRecord.Approval);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // 讀取品牌規範
        public static Dictionary<(string, List<string>), RuleRecord> GetBrandRule()
        {
            var result = new Dictionary<(string, List<string>), RuleRecord>();

            ExcelPackage.LicenseContext = LicenseContext.Commercial;

            using (var package = new ExcelPackage(new FileInfo(BrandRulePath)))
            {
                var worksheet = package.Workbook.Worksheets["規範"];
                int rowCount = worksheet.Dimension.End.Row;
                int colCount = worksheet.Dimension.End.Column;

                // 解除合併儲存格
                foreach (var merge in worksheet.MergedCells)
                {
                    var mergeRange = worksheet.Cells[merge];
                    var mergeText = mergeRange.Text;

                    for (int row = mergeRange.Start.Row; row <= mergeRange.End.Row; row++)
                    {
                        for (int col = mergeRange.Start.Column; col <= mergeRange.End.Column; col++)
                        {
                            worksheet.Cells[row, col].Value = mergeText;
                        }
                    }
                }

                // 第一欄: 欄位名稱
                var fieldNames = worksheet.Cells[1, 1, rowCount, 1]
                    .Select(cell => cell.Text.Trim())
                    .ToArray();

                // 逐欄讀取資料
                for (int col = 2; col <= colCount; col++)
                {
                    var brand = worksheet.Cells[Array.IndexOf(fieldNames, "Brand") + 1, col].Text.Trim();

                    var servers = worksheet.Cells[Array.IndexOf(fieldNames, "Server") + 1, col].Text
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .ToList();

                    var RuleRecord = new RuleRecord
                    {
                        Deposite_Comment = worksheet.Cells[Array.IndexOf(fieldNames, "出入金Comment") + 1, col].Text.Trim(),
                        Max_Account = worksheet.Cells[Array.IndexOf(fieldNames, "帳號數上限") + 1, col].Text.Replace("_", "").Trim(),
                        Max_Amount = worksheet.Cells[Array.IndexOf(fieldNames, "資金池上限") + 1, col].Text.Replace("_", "").Trim(),
                        Max_Deposite = worksheet.Cells[Array.IndexOf(fieldNames, "入金上限") + 1, col].Text.Replace("_", "").Trim(),
                        Account_Segment = worksheet.Cells[Array.IndexOf(fieldNames, "帳號號段") + 1, col].Text.Replace("\n", "").Replace("_", "").Trim(),
                        Expired = worksheet.Cells[Array.IndexOf(fieldNames, "過期天數") + 1, col].Text.Trim(),
                        Account_Comment = worksheet.Cells[Array.IndexOf(fieldNames, "客戶信息") + 1, col].Text.Trim(),
                        MT4_Group = worksheet.Cells[Array.IndexOf(fieldNames, "MT4組別") + 1, col].Text.Replace("\n", "").Trim()
                            .Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                        MT5_Group = worksheet.Cells[Array.IndexOf(fieldNames, "MT5組別") + 1, col].Text.Replace("\n", "").Trim()
                            .Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    };

                    var key = (brand, servers);
                    if (!result.ContainsKey(key))
                    {
                        result[key] = RuleRecord;
                    }
                }
            }
            return result;
        }

        // 取得當前資金池表(給前端看的)
        public static List<CapitalRecord> GetCapitalPool()
        {
            Initiallize();
            var capital_list = new List<CapitalRecord>();

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = @"SELECT * FROM admin_tool.capital_pool_test;";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var record = new CapitalRecord
                            {
                                BRAND = reader["BRAND"].ToString(),
                                TESR_ACCOUNT = reader["TESR_ACCOUNT"].ToString(),
                                TESR_BALANCE = Math.Round(Convert.ToDouble(reader["TESR_BALANCE"]), 2).ToString(),
                                UPDATE_TIME = reader["UPDATE_TIME"].ToString(),
                            };

                            capital_list.Add(record);
                        }
                    }
                }
            }
            return capital_list;
        }

        // 撈Data庫
        private static void GetDailyFx()
        {
            List<Eod_price_Insert_Cop> list = new();
            DateTime yesterday = DateTime.Now.AddDays(-1);
            string formattedDate = yesterday.ToString("yyyy-MM-dd");

            using (MySqlConnection admin_connection = new MySqlConnection(eod_price_connectionString))
            {
                admin_connection.Open();
                string query = @$"SELECT * FROM eod.daily_fx where 1 = 1 and date = '{formattedDate}'";

                using (MySqlCommand command = new MySqlCommand(query, admin_connection))
                {
                    //connect reader
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        // 讀取資料並將其轉換為模型
                        while (reader.Read())
                        {
                            Eod_price_Insert_Cop model = new Eod_price_Insert_Cop
                            {
                                To_data = Convert.ToDateTime(reader["date"]),
                                Ccy = reader["ccy"].ToString(),
                                To_Usd = Convert.ToDouble(reader["to_usd"]),
                                To_Ccy = Convert.ToDouble(reader["to_ccy"]),

                            };
                            // 將模型添加到列表中
                            list.Add(model);
                        }
                    }
                }
            }
            eod_price_list = list;
        }

        //Test_insert_Mysql_Capital_pool
        public static bool Test_insert_Mysql_Capital_pool()
        {
            Initiallize();
            DataTable Server_list_df = new DataTable();

            //先計算，balance
            using (MySqlConnection admin_connection = new MySqlConnection(connectionString))
            {
                admin_connection.Open();
                string query = $@"
                SELECT * FROM server_health.server_list heal left 
                join server_health.sql_connect sqlcon on heal.REPLICA = sqlcon.SERVER";

                using (MySqlCommand command = new MySqlCommand(query, admin_connection))
                {
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(command))
                    {
                        adapter.Fill(Server_list_df);
                    }
                }
            }

            //塞選servename只需要留test
            var filteredRows = Server_list_df.AsEnumerable()
                                .Where(row =>
                                row.Field<string>("SQL_NAME") == "mt5_vjp_test"
                                ||
                                row.Field<string>("SQL_NAME") == "live-mt4tdt-reportdb.vi-data.net"
                                );

            List<Mt5_Out_Insert_Cop> Mt5_Out_Insert_Cop_list = new List<Mt5_Out_Insert_Cop>();
            List<Mt4_Out_Insert_Cop> Mt4_Out_Insert_Cop_list = new List<Mt4_Out_Insert_Cop>();

            //列出
            Debug.WriteLine("Filtered DataTable Content:");
            foreach (var row in filteredRows)
            {
                //0>SQL_NAME、1>BRAND、2>SERVER_NAME、3>REPLICA、4>MT、5>S3、
                //6>IT_RISK > 7>LOADING、8>PROXY、9>DC、10>Archive、
                //11>SERVER、12>HOST、13>USER、14>PASSWORD、15>PORT


                //判斷row[0]是否有包含到
                string sqlName = row[0].ToString();
                //判斷row[0]是否有包含到
                string hostname = row[12].ToString();
                //price表
                if (sqlName.Contains("mt5", StringComparison.OrdinalIgnoreCase))
                {
                    //搜尋mt5資料庫
                    using (MySqlConnection admin_connection = new MySqlConnection(mt5TestconnectionString))
                    {
                        admin_connection.Open();

                        string servername = row[0].ToString();


                        string brand = row[1].ToString();
                        string query = $@"
                        SELECT a.*, b.`ENABLE`
                        FROM (SELECT '{servername}' AS 'Server', u.LOGIN, u.GROUP, 
                        SUBSTRING_INDEX(u.`group`, '\\', -1) AS GROUP2, 
                        u.NAME, u.BALANCE, 
                        RIGHT(SUBSTRING_INDEX(u.`group`, '\\\\', -1), 3) AS CCY, 
                        'TEST' AS 'TYPE', '{brand}' AS 'BRAND', 
                        u.Registration, u.LastAccess, u.`comment` AS 'COMMENT' FROM {servername}.mt5_users u 
                        WHERE 1 = 1 AND u.`GROUP` REGEXP 'TEST|^T_') a 
                        INNER JOIN (SELECT '{servername}' AS 'Server', a.Login AS LOGIN, 1 AS 'ENABLE' 
                        FROM {servername}.mt5_users a 
                        WHERE a.Rights & 0x0000000000000001 != 0) b 
                        ON a.`Server` = b.`Server` AND a.LOGIN = b.LOGIN
                        ";


                        using (MySqlCommand command = new MySqlCommand(query, admin_connection))
                        {
                            using (MySqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var Mt5_Out_Insert_Cop = new Mt5_Out_Insert_Cop
                                    {
                                        Server = reader["Server"].ToString(),
                                        Login = Convert.ToInt32(reader["LOGIN"]),
                                        Group = reader["GROUP"].ToString(),
                                        Group2 = reader["GROUP2"].ToString(),
                                        Name = reader["NAME"].ToString(),
                                        Balance = Convert.ToDouble(reader["BALANCE"]),
                                        Ccy = reader["CCY"].ToString(),
                                        Type = reader["TYPE"].ToString(),
                                        Brand = reader["BRAND"].ToString(),
                                        Enable = Convert.ToInt32(reader["ENABLE"]),
                                        Comment = reader["COMMENT"].ToString()

                                    };
                                    Mt5_Out_Insert_Cop_list.Add(Mt5_Out_Insert_Cop);
                                }
                            }
                        }

                    }

                    //搜尋mt5資料庫
                    using (MySqlConnection admin_connection = new MySqlConnection(mt5TestconnectionString))
                    {
                        admin_connection.Open();

                        string servername = row[0].ToString();
                        string brand = row[1].ToString();
                        string query = $@" 
                        SELECT a.*, b.`ENABLE`
                        FROM (SELECT '{servername}' AS 'Server', u.LOGIN, u.GROUP, 
                        SUBSTRING_INDEX(u.`group`, '\\', -1) AS GROUP2, 
                        u.NAME, u.BALANCE, 
                        RIGHT(SUBSTRING_INDEX(u.`group`, '\\\\', -1), 3) AS CCY, 
                        'TESR' AS 'TYPE', '{brand}' AS 'BRAND', 
                        u.Registration, u.LastAccess, u.`comment` AS 'COMMENT' FROM {servername}.mt5_users u 
                        WHERE 1 = 1 and u.`Group` regexp 'TESR' 
                        and u.`Group` not regexp 'TESR_MONBEN_USD|^TESR_IT|^TESR_RISK|^TESR_Swap|^tesr_dassra|^TESR_USC$|^TESR_USD$' ) a 
                        INNER JOIN (SELECT '{servername}' AS 'Server', a.Login AS LOGIN, 1 AS 'ENABLE' 
                        FROM {servername}.mt5_users a 
                        WHERE a.Rights & 0x0000000000000001 != 0) b 
                        ON a.`Server` = b.`Server` AND a.LOGIN = b.LOGIN 
                        ";

                        using (MySqlCommand command = new MySqlCommand(query, admin_connection))
                        {
                            using (MySqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    try
                                    {
                                        // 檢查 Login 欄位的值是否在 Int32 範圍內
                                        int login = 0;
                                        if (reader["LOGIN"] != DBNull.Value && !Int32.TryParse(reader["LOGIN"].ToString(), out login))
                                        {
                                            throw new OverflowException($"Login value {reader["LOGIN"]} is out of range for Int32.");
                                        }

                                        // 檢查 Enable 欄位的值是否在 Int32 範圍內
                                        int enable = 0;
                                        if (reader["ENABLE"] != DBNull.Value && !Int32.TryParse(reader["ENABLE"].ToString(), out enable))
                                        {
                                            throw new OverflowException($"Enable value {reader["ENABLE"]} is out of range for Int32.");
                                        }

                                        // 檢查 Balance 欄位是否為有效的 Double
                                        double balance = 0.0;
                                        if (reader["BALANCE"] != DBNull.Value && !Double.TryParse(reader["BALANCE"].ToString(), out balance))
                                        {
                                            throw new OverflowException($"Balance value {reader["BALANCE"]} is not a valid Double.");
                                        }

                                        // 填充資料
                                        var Mt5_Out_Insert_Cop = new Mt5_Out_Insert_Cop
                                        {
                                            Server = reader["Server"].ToString(),
                                            Login = login,
                                            Group = reader["GROUP"].ToString(),
                                            Group2 = reader["GROUP2"].ToString(),
                                            Name = reader["NAME"].ToString(),
                                            Balance = balance,
                                            Ccy = reader["CCY"].ToString(),
                                            Type = reader["TYPE"].ToString(),
                                            Brand = reader["BRAND"].ToString(),
                                            Enable = enable,
                                            Comment = reader["COMMENT"].ToString(),
                                        };

                                        Mt5_Out_Insert_Cop_list.Add(Mt5_Out_Insert_Cop);
                                    }
                                    catch (OverflowException ex)
                                    {
                                        // 捕捉 OverflowException 並顯示錯誤資訊
                                        Console.WriteLine($"Error at row: {reader["Server"]}, Login: {reader["LOGIN"]}. Exception: {ex.Message}");
                                    }
                                    catch (Exception ex)
                                    {
                                        // 捕捉其他異常
                                        Console.WriteLine($"Error at row: {reader["Server"]}, Login: {reader["LOGIN"]}. Exception: {ex.Message}");
                                    }
                                }
                            }
                        }

                    }


                }
                else
                {
                    //重新建立連線
                    string mt4connectionSg =
                    $"server={hostname};" +
                    $"user=risktp;" +
                    $"password=fSvz7WEAC5WoAmvdVk547Shx;" +
                    $"port=3306;" +
                    $"charset=utf8;";

                    //搜尋mt4資料庫
                    using (MySqlConnection admin_connection = new MySqlConnection(mt4connectionSg))
                    {
                        admin_connection.Open();

                        string servername = row[0].ToString();
                        string brand = row[1].ToString();

                        // TEST
                        string query = $@" 
                        SELECT '{servername}' as 'Server',u.ENABLE, u.LOGIN,u.GROUP,u.NAME,
                        u.BALANCE,u.CURRENCY as 'CCY', 'TEST' as 'TYPE',
                        '{brand}' as 'BRAND',u.REGDATE,u.LASTDATE,u.`COMMENT` as 'COMMENT' 
                        FROM {servername}.mt4_users u where 1 = 1 
                        and u.`Group` regexp 'TEST|^T_'
                        ";

                        using (MySqlCommand command = new MySqlCommand(query, admin_connection))
                        {
                            using (MySqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var Mt4_Out_Insert_Cop = new Mt4_Out_Insert_Cop
                                    {
                                        Server = reader["Server"].ToString(),
                                        Enable = Convert.ToInt32(reader["ENABLE"]),
                                        Login = Convert.ToInt32(reader["LOGIN"]),
                                        Group = reader["GROUP"].ToString(),
                                        Name = reader["NAME"].ToString(),
                                        Balance = Convert.ToDouble(reader["BALANCE"]),
                                        Ccy = reader["CCY"].ToString(),
                                        Type = reader["TYPE"].ToString(),
                                        Brand = reader["BRAND"].ToString(),
                                        Comment = reader["COMMENT"].ToString(),
                                    };
                                    Mt4_Out_Insert_Cop_list.Add(Mt4_Out_Insert_Cop);
                                }
                            }

                        }

                        // TESR資料

                        query = $@" 
                        SELECT '{servername}' as 'Server',u.ENABLE, u.LOGIN,u.GROUP,u.NAME,
                        u.BALANCE,u.CURRENCY as 'CCY', 'TESR' as 'TYPE',
                        '{brand}' as 'BRAND',u.REGDATE,u.LASTDATE,u.`COMMENT` as 'COMMENT' 
                        FROM {servername}.mt4_users u where 1 = 1 
                        and u.`GROUP` regexp 'TESR'
                        and u.`Group` not regexp 'TESR_MONBEN_USD|^TESR_IT|^TESR_RISK|^TESR_Swap|^tesr_dassra|^TESR_USC$|^TESR_USD$' 
                        ";

                        using (MySqlCommand command = new MySqlCommand(query, admin_connection))
                        {
                            using (MySqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var Mt4_Out_Insert_Cop = new Mt4_Out_Insert_Cop
                                    {
                                        Server = reader["Server"].ToString(),
                                        Enable = Convert.ToInt32(reader["ENABLE"]),
                                        Login = Convert.ToInt32(reader["LOGIN"]),
                                        Group = reader["GROUP"].ToString(),
                                        Name = reader["NAME"].ToString(),
                                        Balance = Convert.ToDouble(reader["BALANCE"]),
                                        Ccy = reader["CCY"].ToString(),
                                        Type = reader["TYPE"].ToString(),
                                        Brand = reader["BRAND"].ToString(),
                                        Comment = reader["COMMENT"].ToString(),

                                    };
                                    Mt4_Out_Insert_Cop_list.Add(Mt4_Out_Insert_Cop);
                                }
                            }

                        }
                    }
                }
            }

            //將Mt5_Out_Insert_Cop_list 並到 eod_price_list
            // 關聯數據
            var mt5_all = from mt5 in Mt5_Out_Insert_Cop_list
                          join eod in eod_price_list on mt5.Ccy equals eod.Ccy
                          where !string.IsNullOrEmpty(mt5.Brand) // 過濾 Brand 為空的記錄
                          select new
                          {
                              mt5.Server,
                              mt5.Login,
                              mt5.Group,
                              mt5.Group2,
                              mt5.Name,
                              mt5.Balance,
                              mt5.Ccy,
                              mt5.Type,
                              mt5.Brand,
                              mt5.Comment,
                              mt5.Enable,
                              eod.To_Usd,
                              eod.To_Ccy,
                              Out_Usd = mt5.Balance * eod.To_Usd // 計算 out_usd
                          };

            // 關聯數據
            var mt4_all = from mt4 in Mt4_Out_Insert_Cop_list
                          join eod in eod_price_list on mt4.Ccy equals eod.Ccy
                          where !string.IsNullOrEmpty(mt4.Brand) // 過濾 Brand 為空的記錄
                          select new
                          {
                              mt4.Server,
                              mt4.Login,
                              mt4.Group,
                              mt4.Name,
                              mt4.Balance,
                              mt4.Ccy,
                              mt4.Type,
                              mt4.Brand,
                              mt4.Comment,
                              mt4.Enable,
                              eod.To_Usd,
                              eod.To_Ccy,
                              Out_Usd = mt4.Balance * eod.To_Usd // 計算 out_usd
                          };


            var mt5_test_condition = mt5_all.Where(data => new[] { "Test" }.Contains(data.Brand) && data.Enable == 1 && Regex.IsMatch(data.Login.ToString(), @"^7\d{8}$"));
            var mt4_test_condition = mt4_all.Where(data => new[] { "Test" }.Contains(data.Brand) && data.Enable == 1 && Regex.IsMatch(data.Login.ToString(), @"^7\d{8}$"));

            // 處理 TEST 和 TESR 條件的統計
            var (mt5_vt_test_account, mt5_vt_test_usd) = ProcessConditions(mt5_test_condition, "TEST");
            var (mt5_vt_tesr_account, mt5_vt_tesr_usd) = ProcessConditions(mt5_test_condition, "TESR");
            var (mt4_vt_test_account, mt4_vt_test_usd) = ProcessConditions(mt4_test_condition, "TEST");
            var (mt4_vt_tesr_account, mt4_vt_tesr_usd) = ProcessConditions(mt4_test_condition, "TESR");

            // 總計
            var vt_all_account_test = mt5_vt_test_account + mt4_vt_test_account;
            var vt_all_usd_test = mt5_vt_test_usd + mt4_vt_test_usd;
            var vt_all_account_tesr = mt5_vt_tesr_account + mt4_vt_tesr_account;
            var vt_all_usd_tesr = mt5_vt_tesr_usd + mt4_vt_tesr_usd;

            //將資料寫入
            DateTime currentTime = DateTime.Now;
            string formattedTime = currentTime.ToString("yyyy-MM-dd HH:mm:ss");

            string tableName = "capital_pool_test";

            var dataList = new List<(string Column1, double Column2, double Column3, double Column4, double Column5, string Column6)>
            {
                ("Test",vt_all_account_test,vt_all_usd_test,vt_all_account_tesr,vt_all_usd_tesr,formattedTime),
            };
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    // 1. 刪除資料
                    string deleteQuery = $"DELETE FROM admin_tool.`{tableName}`;";
                    using (MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, connection))
                    {
                        int rowsDeleted = deleteCommand.ExecuteNonQuery();
                        Console.WriteLine($"已刪除 {rowsDeleted} 筆資料");
                    }

                    // 2. 批次插入新資料
                    var insertValues = new List<string>();
                    var insertParameters = new Dictionary<string, object>();

                    int index = 0;
                    foreach (var (col1, col2, col3, col4, col5, col6) in dataList)
                    {
                        insertValues.Add($"(@Column1_{index}, @Column2_{index}, @Column3_{index}, @Column4_{index}, @Column5_{index}, @Column6_{index})");

                        insertParameters.Add($"@Column1_{index}", col1);
                        insertParameters.Add($"@Column2_{index}", col2);
                        insertParameters.Add($"@Column3_{index}", col3);
                        insertParameters.Add($"@Column4_{index}", col4);
                        insertParameters.Add($"@Column5_{index}", col5);
                        insertParameters.Add($"@Column6_{index}", col6);
                        index++;
                    }

                    string insertQuery = $@"
                  INSERT INTO admin_tool.`{tableName}` (`BRAND`, `TEST_ACCOUNT`, `TEST_BALANCE`, `TESR_ACCOUNT`, `TESR_BALANCE`,`UPDATE_TIME`) 
                  VALUES {string.Join(", ", insertValues)};";

                    using (MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection))
                    {
                        // 添加參數
                        foreach (var param in insertParameters)
                        {
                            insertCommand.Parameters.AddWithValue(param.Key, param.Value);
                        }
                        Debug.WriteLine(insertCommand);

                        int rowsInserted = insertCommand.ExecuteNonQuery();

                        Debug.WriteLine($"已新增 {rowsInserted} 筆資料");
                    }

                    connection.Close();
                    Debug.WriteLine("操作完成!");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"操作失敗: {ex.Message}");
                return false;
            }
        }

        public static (int Account, double Usd) ProcessConditions(IEnumerable<dynamic> data, string type)
        {
            // 當類型是 TESR 時，篩選時同時檢查 Comment 為 "Live Demo Account"
            if (type == "TESR")
            {
                return (
                    data.Count(d => d.Type == type && d.Comment == "Live Demo Account"), // 加上對 Comment 的篩選
                    data.Where(d => d.Type == type && d.Comment == "Live Demo Account").Sum(d => (double)d.Out_Usd) // 同樣加上對 Comment 的篩選
                );
            }
            // 如果是其他類型（例如 TEST），則只根據 Type 進行篩選
            return (
                data.Count(d => d.Type == type),
                data.Where(d => d.Type == type).Sum(d => (double)d.Out_Usd)
            );
        }
    }
}