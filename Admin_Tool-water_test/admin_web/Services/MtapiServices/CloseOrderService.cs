using Microsoft.AspNetCore.Http;
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
using static admin_web.Models.Mtapiuse.CloseOrder_Model;
using static admin_web.Models.ServerModel;

namespace admin_web.Services.MtapiServices
{
    public class CloseOrderService
    {
        // variable
        private static Dictionary<string, ServerRecord> server_dict = new(); // MT連線資訊
        private static IFormFile InputFile; // 使用者上傳檔
        private static readonly string connectionString = UniversalService.sql_connectionString;

        private static void Initiallize()
        {
            server_dict = UniversalService.GetAllServerIP(4);
        }

        public static List<log_record> MainProgram(IFormFile file)
        {
            Initiallize();
            InputFile = file;
            List<log_record> all_log = new();

            var mt4_log = MT4_API();
            all_log.AddRange(mt4_log.Where(r => r.Result == "Success"));
            InsertLogRecordsToDatabase(all_log);

            return mt4_log;
        }

        // MT4 執行流程
        private static List<log_record> MT4_API()
        {
            List<log_record> all_log_list = new();
            var (inputRecords, loginRecords) = Read_Input();

            var rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var aa = Path.Combine(rootPath, Environment.Is64BitProcess ? "mtmanapi64.dll" : "mtmanapi.dll");
            var AIROE = new ClrWrapper(aa);

            foreach (var item in inputRecords)
            {
                var server = item.Key;
                Login_Record server_config = new();
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
                        var server_ip = server_dict[server].SERVER_PROXY;
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
                    all_log_list.Add(new log_record
                    {
                        Server = server,
                        Result = ex.Message
                    });
                    continue;
                }

                foreach (var input_record in item.Value)
                {
                    log_record log_record = new();
                    log_record.Server = server;
                    log_record.Ticket = input_record.Ticket;
                    log_record.UserLogin = server_config.Login;
                    try
                    {
                        // 防呆
                        if (!int.TryParse(input_record.Ticket, out var Ticket))
                        {
                            throw new Exception("無法辨識 Ticket 設置");
                        }
                        int Volume = 0;
                        if (!double.TryParse(input_record.Volume, out var Volume_double))
                        {
                            throw new Exception("無法辨識 Volume 設置");
                        }
                        else
                        {
                            if (Volume_double <= 0)
                            {
                                throw new Exception("Volume 設置有誤");
                            }
                            Volume = (int)(Volume_double * 100);
                        }
                        if (!double.TryParse(input_record.Price, out var Price))
                        {
                            throw new Exception("無法辨識 Price 設置");
                        }

                        // 檢查該單Login是否為TESR_*組別
                        string _connect =
                                $"server={server_dict[server].SQL_HOST};" +
                                $"user={server_dict[server].SQL_USER};" +
                                $"password={server_dict[server].SQL_PASSWORD};" +
                                $"port={server_dict[server].SQL_PORT};" +
                                $"charset=utf8;";

                        // 檢查該單Login的組別
                        using (MySqlConnection connection = new MySqlConnection(_connect))
                        {
                            connection.Open();

                            string query = @$"
                                SELECT u.GROUP
                                FROM {server_dict[server].SQL_NAME}.mt4_trades t
                                LEFT JOIN {server_dict[server].SQL_NAME}.mt4_users u
	                                ON t.LOGIN = u.LOGIN
                                WHERE TICKET = {Ticket}
                            ";

                            using (MySqlCommand cmd = new MySqlCommand(query, connection))
                            {
                                using (MySqlDataReader reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        string group = reader["GROUP"].ToString();
                                        Regex regex = new(@"^TESR_.*");
                                        if (!regex.IsMatch(group))
                                        {
                                            throw new Exception("Group Error");
                                        }
                                    }
                                    else
                                    {
                                        throw new Exception("Ticket Not Found");
                                    }
                                }
                            }
                        }

                        var trade_info = new TradeTransInfo()
                        {
                            Type = TradeTransactionType.BrOrderClose,
                            Order = Ticket,
                            Volume = Volume,
                            Price = Price
                        };

                        var res = AIROE.TradeTransaction(trade_info);
                        if (res == 0)
                        {
                            var time_set = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                            log_record.Time = time_set;
                            log_record.Result = "Success";
                        }
                        else if (res == 3)
                        {
                            log_record.Result = "Invalid";
                        }
                        else
                        {
                            log_record.Result = "Fail";
                        }
                    }
                    catch (Exception ex)
                    {
                        log_record.Result = $"Error：{ex.Message}";
                    }
                    finally
                    {
                        all_log_list.Add(log_record);
                    }
                }                

                AIROE.Disconnect();
            }

            return all_log_list;
        }

        // 讀取Input檔案
        private static (Dictionary<string, List<input>>, Dictionary<string, Login_Record>) Read_Input()
        {
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            var inputRecords = new Dictionary<string, List<input>>();
            var loginRecords = new Dictionary<string, Login_Record>();

            using (var stream = new MemoryStream())
            {
                InputFile.CopyTo(stream);
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet_input = package.Workbook.Worksheets[$"mt4_input"];

                    if (worksheet_input != null)
                    {
                        int rows = worksheet_input.Dimension.Rows;
                        var headerRow = worksheet_input.Cells[1, 1, 1, worksheet_input.Dimension.End.Column].Select(cell => cell.Text.Trim()).ToArray();

                        for (int row = 2; row <= rows; row++)
                        {
                            var record = new input
                            {
                                Server = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Server") + 1].Text,
                                Ticket = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Ticket") + 1].Text,
                                Volume = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Volume") + 1].Text,
                                Price = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Price") + 1].Text,
                            };

                            if (record.Server.Length == 0)
                            {
                                continue;
                            }

                            if (!inputRecords.ContainsKey(record.Server))
                            {
                                inputRecords[record.Server] = new List<input>();
                            }

                            inputRecords[record.Server].Add(record);
                        }
                    }

                    var worksheet_login = package.Workbook.Worksheets[$"mt4_login"];
                    if (worksheet_login != null)
                    {
                        int rows = worksheet_login.Dimension.Rows;
                        var headerRow = worksheet_login.Cells[1, 1, 1, worksheet_login.Dimension.End.Column].Select(cell => cell.Text.Trim()).ToArray();

                        for (int row = 2; row <= rows; row++)
                        {
                            var record = new Login_Record
                            {
                                Server = worksheet_login.Cells[row, Array.IndexOf(headerRow, "Server") + 1].Text,
                                Login = worksheet_login.Cells[row, Array.IndexOf(headerRow, "Login") + 1].Text,
                                Password = worksheet_login.Cells[row, Array.IndexOf(headerRow, "Password") + 1].Text
                            };

                            loginRecords[record.Server] = record;
                        }
                    }
                }
            }

            return (inputRecords, loginRecords);
        }

        // 更新紀錄寫進資料庫
        private static void InsertLogRecordsToDatabase(List<log_record> tool_Log)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    INSERT INTO admin_tool_log.close_order (
                        Server, Ticket, UserLogin, Time
                    ) VALUES (
                        @Server, @Ticket, @UserLogin, @Time
                    );";

                foreach (var logRecord in tool_Log)
                {
                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Server", logRecord.Server);
                        cmd.Parameters.AddWithValue("@Ticket", logRecord.Ticket);
                        cmd.Parameters.AddWithValue("@UserLogin", logRecord.UserLogin);
                        cmd.Parameters.AddWithValue("@Time", logRecord.Time);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // 取出歷史紀錄
        public static (MemoryStream stream, string fileName) ExportHistoryLog(string from_date, string to_date)
        {
            Initiallize();
            if (to_date == "undefined")
            {
                to_date = from_date;
            }

            DataTable dataTable = new DataTable();

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = @$"
                        SELECT * 
                        FROM admin_tool_log.close_order
                        WHERE 1 = 1
                            AND Time Between '{from_date} 00:00' AND '{to_date} 23:59'
                        ";
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
                            worksheet.Column(col).Style.Numberformat.Format = "yyyy-MM-dd HH:mm";
                        }
                        else
                        {
                            worksheet.Column(col).Style.Numberformat.Format = "yyyy-MM-dd";
                        }
                    }
                }

                string fileName = "關單工具HistoryLog.xlsx";
                var stream = new MemoryStream(package.GetAsByteArray());

                return (stream, fileName);
            }
        }
    }
}
