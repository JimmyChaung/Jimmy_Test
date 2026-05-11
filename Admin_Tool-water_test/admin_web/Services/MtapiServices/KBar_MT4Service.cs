using Microsoft.AspNetCore.Http;
using MySql.Data.MySqlClient;
using OfficeOpenXml;
using P23.MetaTrader4.Manager;
using P23.MetaTrader4.Manager.Contracts;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static admin_web.Models.Mtapiuse.KBar_Model;
using static admin_web.Models.ServerModel;

namespace admin_web.Services.MtapiServices
{
    public class KBar_MT4Service
    {
        private static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "KBar_MT4");
        private static readonly string Output_Path = Path.Combine(ToolPath, "output");
        private static readonly string Log_Path = Path.Combine(ToolPath, "log");
        private static readonly string connectionString = UniversalService.sql_connectionString;
        private static Dictionary<string, ServerRecord> server_dict = new();
        private static Dictionary<string, SqlConnectRecord> sql_dict = new();
        //private static string IP_Mode = string.Empty;

        // init
        private static void Initiallize()
        {
            GetAllServerIP();
            GetAllSqlConnect();
        }

        // 給前端看得Server表
        public static List<ServerRecord> Get_MT4_Config()
        {
            Initiallize();
            var server_list = new List<ServerRecord>();

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT sl.SQL_NAME, sl.SERVER_NAME, sl.PROXY, sl.DC, sc.*
                    FROM server_health.server_list sl
                    LEFT JOIN(
                     SELECT *
                        FROM server_health.sql_connect
                    ) sc on sl.REPLICA = sc.SERVER
                    WHERE 1 = 1 
                        AND sl.REPLICA NOT REGEXP 'MT5'
                        AND sl.SQL_NAME != ''
                        AND sl.SERVER_NAME != ''
                        AND sl.PROXY != ''
                    ORDER BY sl.SERVER_NAME";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var record = new ServerRecord
                            {
                                SERVER_NAME = reader["SERVER_NAME"].ToString(),
                                SQL_NAME = reader["SQL_NAME"].ToString(),
                                SERVER_PROXY = reader["PROXY"].ToString(),
                            };

                            server_list.Add(record);
                        }
                    }
                }
            }
            return server_list;
        }

        // 撈取所有Server IP
        private static void GetAllServerIP()
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT sl.SQL_NAME, sl.SERVER_NAME, sl.PROXY, sl.DC, sc.*
                    FROM server_health.server_list sl
                    LEFT JOIN(
                     SELECT *
                        FROM server_health.sql_connect
                    ) sc on sl.REPLICA = sc.SERVER
                    WHERE 1 = 1 
                        AND sl.REPLICA NOT REGEXP 'MT5'
                        AND sl.SQL_NAME != ''
                        AND sl.SERVER_NAME != ''
                        AND sl.PROXY != ''";

                // 測試用
                //string query = @"
                //    SELECT sl.SQL_NAME, sl.SERVER_NAME, sl.PROXY, sl.DC, sc.*
                //    FROM for_test.server_list_test sl
                //    LEFT JOIN(
                //     SELECT *
                //        FROM server_health.sql_connect
                //    ) sc on sl.REPLICA = sc.SERVER
                //    WHERE 1 = 1 
                //        AND sl.REPLICA NOT REGEXP 'MT5'
                //        AND sl.SQL_NAME != ''
                //        AND sl.SERVER_NAME != ''
                //        AND sl.PROXY != ''";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string serverName = reader["SERVER_NAME"].ToString();

                            var record = new ServerRecord
                            {
                                SQL_NAME = reader["SQL_NAME"].ToString(),
                                SERVER_PROXY = reader["PROXY"].ToString(),
                                SERVER_DC = reader["DC"].ToString(),
                                SQL_HOST = reader["HOST"].ToString(),
                                SQL_USER = reader["USER"].ToString(),
                                SQL_PASSWORD = reader["PASSWORD"].ToString(),
                                SQL_PORT = reader["PORT"].ToString(),
                            };

                            server_dict[serverName] = record;
                        }
                    }
                }
            }
        }

        // 取得連線方式
        private static void GetAllSqlConnect()
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = "SELECT * FROM server_health.sql_connect;";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string serverName = reader["SERVER"].ToString();

                            var record = new SqlConnectRecord
                            {
                                HOST = reader["HOST"].ToString(),
                                USER = reader["USER"].ToString(),
                                PASSWORD = reader["PASSWORD"].ToString(),
                                PORT = reader["PORT"].ToString(),
                            };

                            sql_dict[serverName] = record;
                        }
                    }
                }
            }
        }

        // 新增K線
        public static List<Dictionary<string, string>> Add_KBar(string ServerName, string Login, string Password, IFormFileCollection files)
        {
            Initiallize();
            // 建立LOG表
            List<Dictionary<string, string>> LOG_dict = new();

            try
            {
                if (!server_dict.TryGetValue(ServerName, out var ServerRecord))
                {
                    throw new Exception("該Server無法使用");
                }

                // API Setting
                var rootPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var aa = Path.Combine(rootPath, Environment.Is64BitProcess ? "mtmanapi64.dll" : "mtmanapi.dll");
                var AIROE = new ClrWrapper(aa);

                // 從哪裡取得user的登入資料
                int retry_count = 0;
                while (retry_count < 3)
                {
                    int res_connect = AIROE.Connect(ServerRecord.SERVER_PROXY);
                    int res_login = AIROE.Login(Convert.ToInt32(Login), Password);
                    if (res_connect == 0 && res_login == 0)
                    {
                        break;
                    }
                    AIROE.Disconnect();
                    retry_count += 1;
                    Thread.Sleep(3000);
                }
                if (retry_count > 2)
                {
                    AIROE.Disconnect();
                    throw new Exception("登入連線異常");
                }

                // Request Symbol List
                var RequestSymbol_List = AIROE.CfgRequestSymbol();

                foreach (var file in files)
                {
                    try
                    {
                        // 檢查是否為CSV檔
                        string fileExtension = Path.GetExtension(file.FileName).ToLower();
                        if (fileExtension != ".csv")
                        {
                            throw new Exception($"不是有效的CSV檔");
                        }

                        // 取得完整檔名
                        string FileName = Path.GetFileNameWithoutExtension(file.FileName);
                        string[] nameSplit = FileName.Split("_");
                        if (nameSplit.Length != 2)
                        {
                            throw new Exception($"請檢查檔案名稱設置");
                        }
                        string Symbol = nameSplit[0];
                        string Period = nameSplit[1];

                        // 取得所有K線資料
                        var RateInfo_List = new List<RateInfo>();

                        using (var reader = new StreamReader(file.OpenReadStream()))
                        {
                            // 紀錄該檔案內的全部資料
                            while (!reader.EndOfStream)
                            {
                                string line = reader.ReadLine();
                                string[] row = line.Split(',');

                                // 轉換timestamp
                                string[] formats = { "yyyy.MM.dd HH:mm", "yyyy.MM.dd" };
                                double timestamp = (DateTime.ParseExact(row[0], formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

                                // 取symbol digit
                                var FilteredSymbol = RequestSymbol_List.FirstOrDefault(item => item.Name == Symbol);
                                int Symbol_Digit = FilteredSymbol.Digits;

                                // digit setting
                                int it_open = (int)((float.Parse(row[1])) * Math.Pow(10, Symbol_Digit));
                                int it_high = (int)((float.Parse(row[2]) - float.Parse(row[1])) * Math.Pow(10, Symbol_Digit));
                                int it_low = (int)((float.Parse(row[3]) - float.Parse(row[1])) * Math.Pow(10, Symbol_Digit));
                                int it_close = (int)((float.Parse(row[4]) - float.Parse(row[1])) * Math.Pow(10, Symbol_Digit));

                                // Rate Info
                                RateInfo rateInfo = new()
                                {
                                    Ctm = (uint)timestamp,
                                    Open = it_open,
                                    High = it_high,
                                    Low = it_low,
                                    Close = it_close,
                                    Vol = (double)0.1,
                                };
                                RateInfo_List.Add(rateInfo);
                            }
                        }

                        // 新增K線
                        var res = AIROE.ChartAdd(Symbol, (int)(ChartPeriod)Enum.Parse(typeof(ChartPeriod), Period), RateInfo_List);
                        LOG_dict.Add(new Dictionary<string, string>
                        {
                            { "File", file.FileName },
                            { "Status", res == 0 ? "SUCCESS" : "FAIL" },
                        });
                    }
                    catch (Exception ex)
                    {
                        LOG_dict.Add(new Dictionary<string, string>
                        {
                            { "File", file.FileName },
                            { "Status", ex.Message }
                        });
                    }
                }
                AIROE.Disconnect();
            }
            catch (Exception ex)
            {
                LOG_dict.Add(new Dictionary<string, string>
                {
                    { "Server", ServerName},
                    { "Status", ex.Message },
                });
            }

            ExportLogToExcel("新增", LOG_dict);

            return LOG_dict;
        }

        // 刪除K線
        public static List<Dictionary<string, string>> Del_KBar(IFormFile file)
        {
            Initiallize();
            // 建立LOG表
            List<Dictionary<string, string>> LOG_dict = new();

            // 讀取input轉
            ExcelPackage.LicenseContext = LicenseContext.Commercial;

            Dictionary<string, List<Del_Record>> needDict = new(); // need表
            Dictionary<string, Login_Record> loginDict = Get_Login_Dict(file); // login表

            // 取得input資料(need)
            try
            {
                using (var stream = new MemoryStream())
                {
                    file.CopyTo(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        // 讀取 need 工作表
                        var needWorksheet = package.Workbook.Worksheets["need"];
                        var needRowCount = needWorksheet.Dimension.Rows;
                        var needColCount = needWorksheet.Dimension.Columns;

                        var needHeaders = new Dictionary<string, int>();
                        for (int col = 1; col <= needColCount; col++)
                        {
                            needHeaders[needWorksheet.Cells[1, col].Text] = col;
                        }

                        for (int row = 2; row <= needRowCount; row++)
                        {
                            var Server = needWorksheet.Cells[row, needHeaders["Server"]].Text;
                            var record = new Del_Record
                            {
                                Symbol = needWorksheet.Cells[row, needHeaders["Symbol"]].Text,
                                Type = needWorksheet.Cells[row, needHeaders["Type"]].Text,
                                From = needWorksheet.Cells[row, needHeaders["From"]].Text,
                                To = needWorksheet.Cells[row, needHeaders["To"]].Text,
                            };

                            if (!needDict.ContainsKey(Server))
                            {
                                needDict[Server] = new List<Del_Record>();
                            }

                            needDict[Server].Add(record);
                        }
                    }
                }
            }
            catch
            {
                LOG_dict.Add(new Dictionary<string, string>
                {
                    { "Status", "讀取上傳檔時發生錯誤" }
                });
                return LOG_dict;
            }

            // key: Server; value: Symbol, Type, From, To
            foreach (var item in needDict)
            {
                var ServerName = item.Key;
                try
                {
                    // 取得Server資料
                    if (!server_dict.TryGetValue(ServerName, out var ServerRecord))
                    {
                        LOG_dict.Add(new Dictionary<string, string>
                        {
                            { "Server", ServerName },
                            { "Status", "無法使用該Server" }
                        });
                        continue;
                    }

                    // 取得login資料
                    if (!loginDict.TryGetValue(ServerName, out var LoginRecord))
                    {
                        LOG_dict.Add(new Dictionary<string, string>
                        {
                            { "Server", ServerName },
                            { "Status", "未設置登入資訊" }
                        });
                        continue;
                    }
                    string Server_Login = LoginRecord.Login;
                    string Server_Password = LoginRecord.Password;

                    // API Setting
                    var rootPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    var aa = Path.Combine(rootPath, Environment.Is64BitProcess ? "mtmanapi64.dll" : "mtmanapi.dll");
                    var AIROE = new ClrWrapper(aa);

                    // 登入MT
                    int retry_count = 0;
                    while (retry_count < 3)
                    {
                        int res_connect = AIROE.Connect(ServerRecord.SERVER_PROXY);
                        int res_login = AIROE.Login(Convert.ToInt32(Server_Login), Server_Password);
                        if (res_connect == 0 && res_login == 0)
                        {
                            break;
                        }
                        AIROE.Disconnect();
                        retry_count += 1;
                        Thread.Sleep(3000);
                    }
                    if (retry_count > 2)
                    {
                        AIROE.Disconnect();
                        LOG_dict.Add(new Dictionary<string, string>
                        {
                            { "Server", ServerName },
                            { "Status", "登入連線失敗" },
                        });
                        continue;
                    }

                    // Period
                    List<int> Period_List = new() { 1, 5, 15, 30, 60, 240, 1440, 10080, 43200 };

                    // 刪除K線
                    foreach (var input in item.Value)
                    {
                        // variable
                        var _type = input.Type;
                        var _symbol = input.Symbol;
                        var _from = input.From;
                        var _to = input.To;

                        try
                        {
                            // 轉換timestamp
                            string[] formats = { "yyyy.MM.dd HH:mm", "yyyy.MM.dd" };
                            double timestamp_start = (DateTime.ParseExact(_from, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                            double timestamp_end = (DateTime.ParseExact(_to, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

                            if (timestamp_end < timestamp_start)
                            {
                                throw new Exception("時間區間設置有誤");
                            }

                            if (_type == "ALL")
                            {
                                foreach (var period in Period_List)
                                {
                                    ChartInfo chartInfo = new ChartInfo()
                                    {
                                        Symbol = _symbol,
                                        Period = (ChartPeriod)period,
                                        Start = (uint)timestamp_start,
                                        End = (uint)timestamp_end,
                                        TimeSign = 0,
                                        Mode = 0,
                                    };

                                    IList<RateInfo> RateInfo = AIROE.ChartRequest(chartInfo, out _);
                                    RateInfo = RateInfo.Where(r => r.Ctm >= timestamp_start && r.Ctm <= timestamp_end).ToList();

                                    if (RateInfo.Count > 0)
                                    {
                                        int res = AIROE.ChartDelete(_symbol, period, RateInfo);
                                        LOG_dict.Add(new Dictionary<string, string>
                                        {
                                            { "Server", ServerName },
                                            { "Symbol", _symbol },
                                            { "Period", chartInfo.Period.ToString()},
                                            { "Time", $"{_from} ~ {_to}" },
                                            { "Status", res == 0 ? "SUCCESS" : "FAIL"}
                                        });
                                    }
                                    else
                                    {
                                        LOG_dict.Add(new Dictionary<string, string>
                                        {
                                            { "Server", ServerName },
                                            { "Symbol", _symbol },
                                            { "Period", chartInfo.Period.ToString()},
                                            { "Time", $"{_from} ~ {_to}" },
                                            { "Status", "無可刪除的K線資料"}
                                        });
                                    }
                                }
                            }
                            else
                            {
                                int period = (int)(ChartPeriod)Enum.Parse(typeof(ChartPeriod), _type);
                                ChartInfo chartInfo = new ChartInfo()
                                {
                                    Symbol = _symbol,
                                    Period = (ChartPeriod)period,
                                    Start = (uint)timestamp_start,
                                    End = (uint)timestamp_end,
                                    TimeSign = 0,
                                    Mode = 0,
                                };

                                IList<RateInfo> RateInfo = AIROE.ChartRequest(chartInfo, out _);
                                RateInfo = RateInfo.Where(r => r.Ctm >= timestamp_start && r.Ctm <= timestamp_end).ToList();

                                if (RateInfo.Count > 0)
                                {
                                    int res = AIROE.ChartDelete(_symbol, period, RateInfo);
                                    LOG_dict.Add(new Dictionary<string, string>
                                    {
                                        { "Server", ServerName },
                                        { "Symbol", _symbol },
                                        { "Period",  _type},
                                        { "Time", $"{_from} ~ {_to}" },
                                        { "Status", res == 0 ? "SUCCESS" : "FAIL"}
                                    });
                                }
                                else
                                {
                                    LOG_dict.Add(new Dictionary<string, string>
                                    {
                                        { "Server", ServerName },
                                        { "Symbol", _symbol },
                                        { "Period",  _type},
                                        { "Time", $"{_from} ~ {_to}" },
                                        { "Status", "無可刪除的K線資料"}
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("not recognized as a valid DateTime"))
                            {
                                LOG_dict.Add(new Dictionary<string, string>
                                {
                                    { "Server", ServerName },
                                    { "Symbol", _symbol },
                                    { "Period",  _type},
                                    { "Time", $"{_from} ~ {_to}" },
                                    { "Status", $"無法辨別該時間設置" },
                                });
                            }
                            else
                            {
                                LOG_dict.Add(new Dictionary<string, string>
                                {
                                    { "Server", ServerName },
                                    { "Symbol", _symbol },
                                    { "Period",  _type},
                                    { "Time", $"{_from} ~ {_to}" },
                                    { "Status", ex.Message}
                                });
                            }
                        }
                    }
                    AIROE.Disconnect();
                }
                catch
                {
                    LOG_dict.Add(new Dictionary<string, string>
                    {
                        { "Server", ServerName },
                        { "Status", "執行時發生錯誤" }
                    });
                }
            }

            ExportLogToExcel("刪除", LOG_dict);

            return LOG_dict;
        }

        // 修補K線 - 產檔
        public static string Edit_KBar_1(IFormFile file)
        {
            Initiallize();
            // input檔
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            var data = new List<Dictionary<string, string>>();
            string outputPath = Path.Combine(Output_Path, Guid.NewGuid().ToString() + ".xlsx");

            using (var stream = new MemoryStream())
            {
                file.CopyTo(stream);

                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets["need"];
                    if (worksheet == null)
                    {
                        return null;
                    }
                    var rowCount = worksheet.Dimension.Rows;
                    var colCount = worksheet.Dimension.Columns;

                    var headers = new List<string>();
                    for (int col = 1; col <= colCount; col++)
                    {
                        headers.Add(worksheet.Cells[1, col].Text);
                    }

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var rowData = new Dictionary<string, string>();
                        for (int col = 1; col <= colCount; col++)
                        {
                            rowData[headers[col - 1]] = worksheet.Cells[row, col].Text;
                        }
                        data.Add(rowData);
                    }

                    // 建立補K線用表格
                    DataTable result = new();
                    result.Columns.Add("OutServer", typeof(string));
                    result.Columns.Add("OutSymbol", typeof(string));
                    result.Columns.Add("InServer", typeof(string));
                    result.Columns.Add("InSymbol", typeof(string));
                    result.Columns.Add("From", typeof(string));
                    result.Columns.Add("To", typeof(string));

                    // 整理K線資料
                    foreach (var input in data)
                    {
                        var matchingRecord = server_dict[input["InServer"]];

                        if (matchingRecord != null)
                        {
                            string sqlQuery = matchingRecord.SQL_NAME;
                            DataTable sqlDataTable = ExecuteSQL(input["InServer"], sqlQuery, input["InSymbol"]);

                            foreach (DataRow sqlRow in sqlDataTable.Rows)
                            {
                                DataRow newRow = result.NewRow();
                                newRow["InServer"] = sqlRow["InServer"];
                                newRow["InSymbol"] = sqlRow["InSymbol"];
                                newRow["OutServer"] = input["OutServer"];
                                newRow["OutSymbol"] = input["OutSymbol"];
                                newRow["From"] = input["From"];
                                newRow["To"] = input["To"];

                                result.Rows.Add(newRow);
                            }
                        }
                        else
                        {
                            DataRow newRow = result.NewRow();
                            newRow["InServer"] = input["InServer"];
                            newRow["InSymbol"] = input["InSymbol"];
                            newRow["OutServer"] = input["OutServer"];
                            newRow["OutSymbol"] = input["OutSymbol"];
                            newRow["From"] = input["From"];
                            newRow["To"] = input["To"];
                            result.Rows.Add(newRow);
                        }
                    }

                    if (result.Rows.Count == 0)
                    {
                        return null;
                    }

                    result = result.AsEnumerable().Distinct(DataRowComparer.Default).CopyToDataTable();

                    // 清空原始資料表內容
                    worksheet.Cells.Clear();

                    // 將處理後的結果寫回表格
                    for (int col = 0; col < result.Columns.Count; col++)
                    {
                        worksheet.Cells[1, col + 1].Value = result.Columns[col].ColumnName; // 寫入標頭
                    }

                    for (int row = 0; row < result.Rows.Count; row++)
                    {
                        for (int col = 0; col < result.Columns.Count; col++)
                        {
                            worksheet.Cells[row + 2, col + 1].Value = result.Rows[row][col];
                        }
                    }

                    // 儲存檔案到指定路徑
                    FileInfo newFile = new FileInfo(outputPath);
                    package.SaveAs(newFile);
                }
            }

            return outputPath;
        }

        // 修補K線 - API
        public static List<Dictionary<string, string>> Edit_KBar_2(IFormFile file)
        {
            Initiallize();
            var LOG_dict = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string>
                {
                    { "Output", string.Empty },
                    { "Input", string.Empty },
                    { "Time", string.Empty },
                    { "Period", string.Empty },
                    { "Status", string.Empty }
                }
            };

            List<Edit_Need_Record> needList = new();
            Dictionary<string, Login_Record> loginDict = Get_Login_Dict(file);

            try
            {
                using (var stream = new MemoryStream())
                {
                    file.CopyTo(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var needWorksheet = package.Workbook.Worksheets["need"];
                        var needRowCount = needWorksheet.Dimension.Rows;
                        var needColCount = needWorksheet.Dimension.Columns;

                        var needHeaders = new Dictionary<string, int>();
                        for (int col = 1; col <= needColCount; col++)
                        {
                            needHeaders[needWorksheet.Cells[1, col].Text] = col;
                        }

                        for (int row = 2; row <= needRowCount; row++)
                        {
                            var record = new Edit_Need_Record
                            {
                                OutServer = needWorksheet.Cells[row, needHeaders["OutServer"]].Text,
                                OutSymbol = needWorksheet.Cells[row, needHeaders["OutSymbol"]].Text,
                                InServer = needWorksheet.Cells[row, needHeaders["InServer"]].Text,
                                InSymbol = needWorksheet.Cells[row, needHeaders["InSymbol"]].Text,
                                From = needWorksheet.Cells[row, needHeaders["From"]].Text,
                                To = needWorksheet.Cells[row, needHeaders["To"]].Text
                            };

                            if (string.IsNullOrWhiteSpace(record.OutServer) ||
                                string.IsNullOrWhiteSpace(record.OutSymbol) ||
                                string.IsNullOrWhiteSpace(record.InServer) ||
                                string.IsNullOrWhiteSpace(record.InSymbol))
                            {
                                continue; // 略過此列
                            }

                            needList.Add(record);
                        }
                    }
                }
            }
            catch
            {
                LOG_dict.Add(new Dictionary<string, string>
                {
                    { "Status", "讀取檔案時發生問題，請檢查need工作表是否存在，或是欄位名稱是否有誤" },
                });
                return LOG_dict;
            }

            // 取資料階段
            var Period_List = new List<int> { 1, 5, 15, 30, 60, 240, 1440, 10080, 43200 };
            var lockObj = new object();
            var symbolDataDict = new Dictionary<(string, string, string, string, int), List<RateInfo>>();

            var outServerGroups = needList.GroupBy(x => x.OutServer);

            foreach (var outServerGroup in outServerGroups)
            {
                string outServer = outServerGroup.Key;

                try
                {
                    var AIROE_Out = new ClrWrapper(GetDllPath());

                    if (!server_dict.TryGetValue(outServer, out var outServerRecord))
                        throw new Exception($"OutServer {outServer} 無法使用");

                    if (!loginDict.TryGetValue(outServer, out var loginRecord) || string.IsNullOrEmpty(loginRecord.Login) || string.IsNullOrEmpty(loginRecord.Password))
                        throw new Exception($"OutServer {outServer} 登入資訊錯誤");

                    if (!LoginServer(AIROE_Out, outServerRecord.SERVER_PROXY, loginRecord))
                        throw new Exception($"OutServer {outServer} 登入失敗");

                    // 這邊只取該 OutServer 的所有 Symbol + From + To
                    var symbolTimeGroups = outServerGroup.GroupBy(x => new { x.OutSymbol, x.From, x.To });

                    foreach (var group in symbolTimeGroups)
                    {
                        var key = group.Key;

                        try
                        {
                            string[] formats = { "yyyy.MM.dd HH:mm", "yyyy.MM.dd" };
                            double timestamp_start = (DateTime.ParseExact(key.From, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;
                            double timestamp_end = (DateTime.ParseExact(key.To, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;

                            if (timestamp_end < timestamp_start)
                                throw new Exception("時間區間設置有誤");

                            foreach (var period in Period_List)
                            {
                                ChartInfo chartInfo = new ChartInfo()
                                {
                                    Symbol = key.OutSymbol,
                                    Period = (ChartPeriod)period,
                                    Start = (uint)timestamp_start,
                                    End = (uint)timestamp_end,
                                    TimeSign = 0,
                                    Mode = (ChartRequestMode)0,
                                };

                                IList<RateInfo> rateInfos = AIROE_Out.ChartRequest(chartInfo, out _);
                                var validRates = rateInfos.Where(r => r.Ctm >= timestamp_start && r.Ctm <= timestamp_end).ToList();

                                lock (lockObj)
                                {
                                    symbolDataDict[(outServer, key.OutSymbol, key.From, key.To, period)] = validRates;
                                }

                                Thread.Sleep(500);
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (lockObj)
                            {
                                LOG_dict.Add(new Dictionary<string, string>
                                {
                                    { "Output", outServer },
                                    { "Status", ex.Message }
                                });
                            }
                        }
                    }
                    AIROE_Out.Disconnect();
                }
                catch (Exception ex)
                {
                    LOG_dict.Add(new Dictionary<string, string>
                    {
                        { "Output", outServer },
                        { "Status", ex.Message }
                    });
                }
            }

            // 寫入階段
            var tasks = new List<Task>();

            foreach (var item in needList)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var AIROE_In = new ClrWrapper(GetDllPath());

                        if (!server_dict.TryGetValue(item.InServer, out var inServerRecord))
                            throw new Exception($"InServer {item.InServer} 無法使用");

                        if (!loginDict.TryGetValue(item.InServer, out var loginRecord) || string.IsNullOrEmpty(loginRecord.Login) || string.IsNullOrEmpty(loginRecord.Password))
                            throw new Exception($"InServer {item.InServer} 登入資訊錯誤");

                        if (!LoginServer(AIROE_In, inServerRecord.SERVER_PROXY, loginRecord))
                            throw new Exception($"InServer {item.InServer} 登入失敗");

                        string[] formats = { "yyyy.MM.dd HH:mm", "yyyy.MM.dd" };

                        foreach (var period in Period_List)
                        {
                            var key = (item.OutServer, item.OutSymbol, item.From, item.To, period);

                            if (symbolDataDict.TryGetValue(key, out var rateInfo))
                            {
                                if (rateInfo.Count > 0)
                                {
                                    int res = AIROE_In.ChartUpdate(item.InSymbol, period, rateInfo);
                                    var status = res == 0 ? "SUCCESS" : "FAIL";

                                    lock (LOG_dict)
                                    {
                                        LOG_dict.Add(new Dictionary<string, string>
                                        {
                                            { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                                            { "Input", $"{item.InServer}[{item.InSymbol}]" },
                                            { "Time", $"{item.From} ~ {item.To}" },
                                            { "Period", ((ChartPeriod)period).ToString() },
                                            { "Status", status }
                                        });
                                    }
                                }
                                else
                                {
                                    lock (LOG_dict)
                                    {
                                        LOG_dict.Add(new Dictionary<string, string>
                                        {
                                            { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                                            { "Input", $"{item.InServer}[{item.InSymbol}]" },
                                            { "Time", $"{item.From} ~ {item.To}" },
                                            { "Period", ((ChartPeriod)period).ToString() },
                                            { "Status", "該時段沒有可用於修補K線的資料" }
                                        });
                                    }
                                }
                            }
                            else
                            {
                                lock (LOG_dict)
                                {
                                    LOG_dict.Add(new Dictionary<string, string>
                                    {
                                        { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                                        { "Input", $"{item.InServer}[{item.InSymbol}]" },
                                        { "Time", $"{item.From} ~ {item.To}" },
                                        { "Period", ((ChartPeriod)period).ToString() },
                                        { "Status", "找不到對應的資料" }
                                    });
                                }
                            }

                            Thread.Sleep(1000);
                        }

                        AIROE_In.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        lock (LOG_dict)
                        {
                            LOG_dict.Add(new Dictionary<string, string>
                            {
                                { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                                { "Input", $"{item.InServer}[{item.InSymbol}]" },
                                { "Time", $"{item.From} ~ {item.To}" },
                                { "Period", "" },
                                { "Status", ex.Message }
                            });
                        }
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            var mergedLogs = LOG_dict
                .Where(log => log.ContainsKey("Output") && !string.IsNullOrWhiteSpace(log["Output"]))
                .GroupBy(log => new
                {
                    Output = log.ContainsKey("Output") ? log["Output"] : string.Empty,
                    Input = log.ContainsKey("Input") ? log["Input"] : string.Empty,
                    Time = log.ContainsKey("Time") ? log["Time"] : string.Empty
                })
                .Select(group => new Dictionary<string, string>
                {
                    { "Output", group.Key.Output },
                    { "Input", group.Key.Input },
                    { "Time", group.Key.Time },
                    { "Period_Status", string.Join("<br>", group.Select(g =>
                        {
                            string period = g.ContainsKey("Period") ? g["Period"] : "";
                            string status = g.ContainsKey("Status") ? g["Status"] : "(無 Status)";
                            return $"{period}: {status}";
                        }))
                    }
                })
                .OrderBy(log => log["Output"])
                .ThenBy(log => log["Input"])
                .ThenBy(log => log["Time"])
                .ToList();

            ExportLogToExcel("修補", mergedLogs);

            return mergedLogs;
        }

        private static string GetDllPath()
        {
            var rootPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            return Path.Combine(rootPath, Environment.Is64BitProcess ? "mtmanapi64.dll" : "mtmanapi.dll");
        }

        private static bool LoginServer(ClrWrapper client, string ip, Login_Record login)
        {
            int retryCount = 0;
            while (retryCount < 3)
            {
                int res_connect = client.Connect(ip);
                int res_login = client.Login(Convert.ToInt32(login.Login), login.Password);
                if (res_connect == 0 && res_login == 0)
                    return true;

                client.Disconnect();
                retryCount++;
                Thread.Sleep(1000);
            }
            client.Disconnect();
            return false;
        }

        public static List<Dictionary<string, string>> Edit_KBar_3(IFormFile file)
        {
            Initiallize();
            // 建立LOG表
            List<Dictionary<string, string>> LOG_dict = new();

            // 讀取input轉DT
            DataTable result = new DataTable();
            ExcelPackage.LicenseContext = LicenseContext.Commercial;

            List<Edit_Need_Record> needList = new(); // need表
            Dictionary<string, Login_Record> loginDict = Get_Login_Dict(file); // login表

            // 取得input資料
            try
            {
                using (var stream = new MemoryStream())
                {
                    file.CopyTo(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        // 讀取 need 工作表
                        var needWorksheet = package.Workbook.Worksheets["need"];
                        var needRowCount = needWorksheet.Dimension.Rows;
                        var needColCount = needWorksheet.Dimension.Columns;

                        var needHeaders = new Dictionary<string, int>();
                        for (int col = 1; col <= needColCount; col++)
                        {
                            needHeaders[needWorksheet.Cells[1, col].Text] = col;
                        }

                        for (int row = 2; row <= needRowCount; row++)
                        {
                            var record = new Edit_Need_Record
                            {
                                OutServer = needWorksheet.Cells[row, needHeaders["OutServer"]].Text,
                                OutSymbol = needWorksheet.Cells[row, needHeaders["OutSymbol"]].Text,
                                InServer = needWorksheet.Cells[row, needHeaders["InServer"]].Text,
                                InSymbol = needWorksheet.Cells[row, needHeaders["InSymbol"]].Text,
                                From = needWorksheet.Cells[row, needHeaders["From"]].Text,
                                To = needWorksheet.Cells[row, needHeaders["To"]].Text
                            };
                            needList.Add(record);
                        }
                    }
                }
            }
            catch
            {
                LOG_dict.Add(new Dictionary<string, string>
                {
                    { "Status", "讀取檔案時發生問題，請檢查need工作表是否存在，或是欄位名稱是否有誤" },
                });
                return LOG_dict;
            }

            // 修補K線
            var rootPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var aa = Path.Combine(rootPath, Environment.Is64BitProcess ? "mtmanapi64.dll" : "mtmanapi.dll");
            var AIROE_Out = new ClrWrapper(aa);
            var AIROE_In = new ClrWrapper(aa);
            List<int> Period_List = new() { 1, 5, 15, 30, 60, 240, 1440, 10080, 43200 };

            foreach (var item in needList)
            {
                try
                {
                    // 檢查該Out Server是否存在
                    if (!server_dict.TryGetValue(item.OutServer, out var OutServerRecord))
                    {
                        throw new Exception("輸出的Server無法使用");
                    }
                    var outServer_IP = OutServerRecord.SERVER_PROXY;

                    // 檢查該Out Server連接登入狀況
                    var outServer_Login = loginDict[item.OutServer].Login;
                    var outServer_Password = loginDict[item.OutServer].Password;
                    if (outServer_Login.Length == 0 || outServer_Password.Length == 0)
                    {
                        throw new Exception("輸出的Server未設置登入資訊");
                    }

                    int retry_out_count = 0;
                    while (retry_out_count < 3)
                    {
                        int res_connect = AIROE_Out.Connect(outServer_IP);
                        int res_login = AIROE_Out.Login(Convert.ToInt32(outServer_Login), outServer_Password);
                        if (res_connect == 0 && res_login == 0)
                        {
                            break;
                        }
                        AIROE_Out.Disconnect();
                        retry_out_count += 1;
                        Thread.Sleep(3000);
                    }
                    if (retry_out_count > 2)
                    {
                        AIROE_Out.Disconnect();
                        throw new Exception("輸出的Server登入連線失敗");
                    }

                    // 檢查該In Server是否存在
                    if (!server_dict.TryGetValue(item.InServer, out var InServerRecord))
                    {
                        throw new Exception("輸入的Server無法使用");
                    }
                    var inServer_IP = InServerRecord.SERVER_PROXY;

                    // 檢查該In Server登入連線狀況
                    var inServer_Login = loginDict[item.InServer].Login;
                    var inServer_Password = loginDict[item.InServer].Password;
                    if (inServer_Login.Length == 0 || inServer_Password.Length == 0)
                    {
                        throw new Exception("輸入的Server未設置登入資訊");
                    }

                    int retry_in_count = 0;
                    while (retry_in_count < 3)
                    {
                        int res_connect = AIROE_In.Connect(inServer_IP);
                        int res_login = AIROE_In.Login(Convert.ToInt32(inServer_Login), inServer_Password);
                        if (res_connect == 0 && res_login == 0)
                        {
                            break;
                        }
                        AIROE_In.Disconnect();
                        retry_in_count += 1;
                        Thread.Sleep(3000);
                    }
                    if (retry_in_count > 2)
                    {
                        AIROE_In.Disconnect();
                        throw new Exception("輸入的Server登入連線失敗");
                    }

                    // 檢查補K線狀況
                    string[] formats = { "yyyy.MM.dd HH:mm", "yyyy.MM.dd" };
                    double timestamp_start = (DateTime.ParseExact(item.From, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                    double timestamp_end = (DateTime.ParseExact(item.To, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

                    if (timestamp_end < timestamp_start)
                    {
                        throw new Exception("時間區間設置有誤");
                    }

                    string status = string.Empty; // 紀錄LOG

                    foreach (var period in Period_List)
                    {
                        ChartInfo out_ChartInfo = new ChartInfo()
                        {
                            Symbol = item.OutSymbol,
                            Period = (ChartPeriod)period,
                            Start = (uint)timestamp_start,
                            End = (uint)timestamp_end,
                            TimeSign = 0,
                            Mode = (ChartRequestMode)0,
                        };
                        IList<RateInfo> out_RateInfo = AIROE_Out.ChartRequest(out_ChartInfo, out _);
                        out_RateInfo = out_RateInfo.Where(r => r.Ctm >= timestamp_start && r.Ctm <= timestamp_end).ToList();

                        if (out_RateInfo.Count > 0)
                        {
                            int res = AIROE_In.ChartUpdate(item.InSymbol, period, out_RateInfo);
                            status = res == 0 ? "SUCCESS" : "FAIL";
                        }
                        else
                        {
                            status = "該時段沒有可用於修補K線的資料";
                        }
                        out_RateInfo = null;
                        out_ChartInfo = null;

                        LOG_dict.Add(new Dictionary<string, string>
                        {
                            { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                            { "Input", $"{item.InServer}[{item.InSymbol}]" },
                            { "Time", $"{item.From} ~ {item.To}" },
                            { "Period", ((ChartPeriod)period).ToString() },
                            { "Status", status },
                        });

                        Thread.Sleep(3000);
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("not recognized as a valid DateTime"))
                    {
                        LOG_dict.Add(new Dictionary<string, string>
                        {
                            { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                            { "Input", $"{item.InServer}[{item.InSymbol}]" },
                            { "Time", $"{item.From} ~ {item.To}" },
                            { "Period", "" },
                            { "Status", $"無法辨別該時間設置" },
                        });
                    }
                    else
                    {
                        LOG_dict.Add(new Dictionary<string, string>
                        {
                            { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                            { "Input", $"{item.InServer}[{item.InSymbol}]" },
                            { "Time", $"{item.From} ~ {item.To}" },
                            { "Period", "" },
                            { "Status", ex.Message },
                        });
                    }
                }
                AIROE_Out.Disconnect();
                AIROE_In.Disconnect();
                Thread.Sleep(3000);
            }
            return LOG_dict;
        }

        // SQL資料撈取(Edit用)
        private static DataTable ExecuteSQL(string server, string sql, string symbol)
        {
            DataTable data = new();
            string sqlcommand = $@"SELECT '{server}' as InServer, A.Name as InSymbol 
                                FROM configurations.symbolhistory a 
                                WHERE 1 = 1
                                    AND a.time = concat(DATE_SUB(CURDATE(), INTERVAL 1 DAY),' 23:59:59') 
                                    AND a.server = '{sql}' 
                                    AND a.Name regexp '{symbol}'";

            //string sqlcommand = $@"SELECT '{server}' as InServer, A.Name as InSymbol 
            //                    FROM configurations.symbols a 
            //                    WHERE 1 = 1
            //                        AND a.server = '{sql}' 
            //                        AND a.Name regexp '{symbol}'";

            // 這邊要自動抓configuration庫
            var configuration_sql_config = sql_dict["Configuration"];
            string _connection =
                    $"server={configuration_sql_config.HOST};" +
                    $"user={configuration_sql_config.USER};" +
                    $"password={configuration_sql_config.PASSWORD};" +
                    $"port={configuration_sql_config.PORT};" +
                    $"charset=utf8;";

            using (MySqlConnection connection = new MySqlConnection(_connection))
            {
                connection.Open();

                using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                {
                    using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                    {
                        data.Load(sqlreader);
                    }
                }
                connection.Close();
            }
            return data;
        }

        // 從上傳檔取得login資訊
        private static Dictionary<string, Login_Record> Get_Login_Dict(IFormFile file)
        {
            Dictionary<string, Login_Record> loginDict = new(); // login表
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            using (var stream = new MemoryStream())
            {
                file.CopyTo(stream);
                using (var package = new ExcelPackage(stream))
                {
                    // 讀取 login 工作表
                    var loginWorksheet = package.Workbook.Worksheets["login"];
                    var loginRowCount = loginWorksheet.Dimension.Rows;
                    var loginColCount = loginWorksheet.Dimension.Columns;

                    var loginHeaders = new Dictionary<string, int>();
                    for (int col = 1; col <= loginColCount; col++)
                    {
                        loginHeaders[loginWorksheet.Cells[1, col].Text] = col;
                    }

                    for (int row = 2; row <= loginRowCount; row++)
                    {
                        string server = loginWorksheet.Cells[row, loginHeaders["Server"]].Text;
                        if (!string.IsNullOrWhiteSpace(server))
                        {
                            loginDict[server] = new Login_Record
                            {
                                Login = loginWorksheet.Cells[row, loginHeaders["Login"]].Text,
                                Password = loginWorksheet.Cells[row, loginHeaders["Password"]].Text
                            };
                        }
                    }
                }

                return loginDict;
            }
        }

        public static void ExportLogToExcel(string mode, List<Dictionary<string, string>> LOG_dict)
        {
            // 產生日期時間作為檔名
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{mode}_LOG_{timestamp}.xlsx";
            string filePath = Path.Combine(Log_Path, fileName);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Log");

                var headers = new List<string>(LOG_dict[0].Keys);

                for (int col = 0; col < headers.Count; col++)
                {
                    worksheet.Cells[1, col + 1].Value = headers[col];
                }

                for (int row = 0; row < LOG_dict.Count; row++)
                {
                    var entry = LOG_dict[row];
                    for (int col = 0; col < headers.Count; col++)
                    {
                        worksheet.Cells[row + 2, col + 1].Value = entry[headers[col]];
                    }
                }

                package.SaveAs(new FileInfo(filePath));
            }
        }
    }
}
