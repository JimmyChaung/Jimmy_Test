using MetaQuotes.MT5CommonAPI;
using MetaQuotes.MT5ManagerAPI;
using Microsoft.AspNetCore.Http;
using MySql.Data.MySqlClient;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static admin_web.Models.Mtapiuse.KBar_Model;
using static admin_web.Models.ServerModel;

namespace admin_web.Services.MtapiServices
{
    public class KBar_MT5Service
    {
        private static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "KBar_MT5");
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
        }

        // 給前端看得Server表
        public static List<ServerRecord> Get_MT5_Config()
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
                        AND sl.REPLICA REGEXP 'MT5'
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
                        AND sl.REPLICA REGEXP 'MT5'
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
                //    WHERE sl.REPLICA REGEXP 'MT5'";

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
                string _rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                MTRetCode res = MTRetCode.MT_RET_OK;

                // 檢查API連接狀況
                if ((res = SMTManagerAPIFactory.Initialize(_rootPath)) != MTRetCode.MT_RET_OK)
                {
                    throw new Exception("與MT5 API建立連接時發生錯誤");
                }

                // 建立manager
                var manager = SMTManagerAPIFactory.CreateManager(SMTManagerAPIFactory.ManagerAPIVersion, out res);
                if ((res != MTRetCode.MT_RET_OK) || manager == null)
                {
                    SMTManagerAPIFactory.Shutdown();
                    string message = string.Format("Creating manager failed ({0})", (res == MTRetCode.MT_RET_OK ? "Manager api is null" : res.ToString()));
                    throw new Exception(message);
                }

                // 從哪裡取得user的登入資料
                int retry_count = 0;
                while (retry_count < 3)
                {
                    var res_connect = manager.Connect(ServerRecord.SERVER_PROXY, (ulong)Convert.ToInt32(Login), Password, null,
                            CIMTManagerAPI.EnPumpModes.PUMP_MODE_SYMBOLS, 3600000);
                    if (res_connect == MTRetCode.MT_RET_OK)
                    {
                        break;
                    }
                    manager.Disconnect();
                    retry_count += 1;
                    Thread.Sleep(3000);
                }
                if (retry_count > 2)
                {
                    manager.Disconnect();
                    throw new Exception("登入連線失敗，請確認帳號密碼設置");
                }

                foreach (var file in files)
                {
                    try
                    {
                        // 檢查是否為CSV檔
                        string fileExtension = Path.GetExtension(file.FileName).ToLower();
                        if (fileExtension != ".csv")
                        {
                            throw new Exception("不是有效的CSV檔");
                        }

                        // 取得完整檔名
                        string Symbol = Path.GetFileNameWithoutExtension(file.FileName);

                        // 取symbol digit
                        CIMTConSymbol symbol_obj = manager.SymbolCreate();
                        MTRetCode res_symbolrequest = manager.SymbolRequest(Symbol, symbol_obj);
                        if (res_symbolrequest != MTRetCode.MT_RET_OK)
                        {
                            throw new Exception("Server下無該商品");
                        }

                        // digit setting
                        var Symbol_Digit = symbol_obj.Digits();

                        // 紀錄該檔案內的全部資料
                        List<MTChartBar> chartBarsList = new List<MTChartBar>();

                        using (var reader = new StreamReader(file.OpenReadStream()))
                        {
                            while (!reader.EndOfStream)
                            {
                                string line = reader.ReadLine();
                                string[] row = line.Split(',');

                                // 轉換timestamp
                                string[] formats = { "yyyy.MM.dd HH:mm", "yyyy.MM.dd" };
                                double timestamp = (DateTime.ParseExact(row[0], formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

                                double it_open = double.Parse(row[1]);
                                double it_high = double.Parse(row[2]);
                                double it_low = double.Parse(row[3]);
                                double it_close = double.Parse(row[4]);

                                chartBarsList.Add(new MTChartBar
                                {
                                    datetime = (long)timestamp,
                                    open = it_open,
                                    high = it_high,
                                    low = it_low,
                                    close = it_close,
                                    tick_volume = 0,
                                    spread = -1,
                                    volume = 0
                                });
                            }
                        }

                        // 撈取該時段的K線
                        MTChartBar[] chart_request = manager.ChartRequest(Symbol, chartBarsList.First().datetime, chartBarsList.Last().datetime, out MTRetCode res_request);
                        List<MTChartBar> chart_result = new();
                        int TickEmptyCheck = 0;

                        if ((res_request != MTRetCode.MT_RET_OK) || (res_request != 0))
                        {
                            // 如果撈不到K線，則直接使用input資料
                            if (res_request == MTRetCode.MT_RET_OK_NONE)
                            {
                                // 從ticks計算spread 
                                foreach (var bar in chartBarsList)
                                {
                                    var ticks = manager.TickHistoryRequest(Symbol, (long)bar.datetime, (long)(bar.datetime + 60), out MTRetCode res_tick);
                                    int Symobl_Spread = int.MaxValue;
                                    // 如果tick撈不到，則錯誤
                                    if ((res_tick != MTRetCode.MT_RET_OK) || (res_tick != 0) || ticks.Length == 0)
                                    {
                                        if (res_tick == MTRetCode.MT_RET_OK_NONE || ticks.Length == 0)
                                        {
                                            TickEmptyCheck++;
                                            continue;
                                        }
                                        else
                                        {
                                            throw new Exception("在檢索K線時發生錯誤");
                                        }
                                    }
                                    else
                                    {
                                        // 補上spread
                                        foreach (var tick in ticks)
                                        {
                                            int currentSpread = (int)((tick.ask * Math.Pow(10, Symbol_Digit) - tick.bid * Math.Pow(10, Symbol_Digit)));

                                            if (currentSpread < Symobl_Spread)
                                            {
                                                Symobl_Spread = currentSpread;
                                            }
                                        }
                                        MTChartBar newBar = bar;
                                        newBar.spread = Symobl_Spread;
                                        chart_result.Add(newBar);
                                    }
                                }
                            }
                            else
                            {
                                throw new Exception("在檢索K線時發生錯誤");
                            }
                        }
                        else
                        {
                            // 如果撈到K線，則補上有空缺的K線
                            List<MTChartBar> chart_request_bars = chart_request.ToList();
                            foreach (var bar in chartBarsList)
                            {
                                if (!chart_request.Any(chart => chart.datetime == bar.datetime))
                                {
                                    // 從ticks計算spread 
                                    var ticks = manager.TickHistoryRequest(Symbol, (long)bar.datetime, (long)(bar.datetime + 60), out MTRetCode res_tick);
                                    int Symobl_Spread = int.MaxValue;
                                    if ((res_tick != MTRetCode.MT_RET_OK) || (res_tick != 0) || ticks.Length == 0)
                                    {
                                        if (res_tick == MTRetCode.MT_RET_OK_NONE || ticks.Length == 0)
                                        {
                                            // throw new Exception($"該商品設置部分時段無Tick資料");
                                            TickEmptyCheck++;
                                            continue;
                                        }
                                        else
                                        {
                                            throw new Exception("在檢索K線時發生錯誤");
                                        }
                                    }
                                    else
                                    {
                                        // 補上spread
                                        foreach (var tick in ticks)
                                        {
                                            int currentSpread = (int)((tick.ask * Math.Pow(10, Symbol_Digit) - tick.bid * Math.Pow(10, Symbol_Digit)));

                                            if (currentSpread < Symobl_Spread)
                                            {
                                                Symobl_Spread = currentSpread;
                                            }
                                        }
                                        MTChartBar newBar = bar;
                                        newBar.spread = Symobl_Spread;
                                        chart_request_bars.Add(newBar);
                                    }
                                }
                            }
                            chart_result = chart_request_bars;
                        }

                        // 新增K線
                        MTChartBar[] ChartBars = chart_result.ToArray();
                        var res_update = manager.ChartUpdate(Symbol, ChartBars);
                        if ((res_update != MTRetCode.MT_RET_OK) || (res_update != 0))
                        {
                            throw new Exception("FAIL");
                        }
                        else
                        {
                            LOG_dict.Add(new Dictionary<string, string>
                            {
                                { "File", file.FileName },
                                { "Status", TickEmptyCheck == 0 ? "SUCCESS" : "部分成功，有些K線因缺少Tick而無法新增K線。"},
                            });
                        }
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
                manager.Disconnect();
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
            var LOG_dict = new List<Dictionary<string, string>>();

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

            string _rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            SMTManagerAPIFactory.Initialize(_rootPath);

            // 取資料
            var symbolDataDict = new Dictionary<(string OutServer, string OutSymbol, string From, string To), MTChartBar[]>();
            var outServerGroups = needList.GroupBy(x => x.OutServer);

            foreach (var outGroup in outServerGroups)
            {
                string outServer = outGroup.Key;

                if (!server_dict.TryGetValue(outServer, out var OutServerRecord))
                {
                    LOG_dict.Add(new Dictionary<string, string> { { "Status", $"{outServer} 不存在" } });
                    continue;
                }

                var manager = SMTManagerAPIFactory.CreateManager(SMTManagerAPIFactory.ManagerAPIVersion, out MTRetCode res_create);

                if (res_create != MTRetCode.MT_RET_OK || manager == null)
                {
                    LOG_dict.Add(new Dictionary<string, string> { { "Status", $"連接 {outServer} 失敗" } });
                    continue;
                }

                var loginInfo = loginDict[outServer];
                int retryCount = 0;
                while (retryCount < 3)
                {
                    var res_connect = manager.Connect(OutServerRecord.SERVER_PROXY, (ulong)Convert.ToInt32(loginInfo.Login), loginInfo.Password, null, CIMTManagerAPI.EnPumpModes.PUMP_MODE_SYMBOLS, 3600000);
                    if (res_connect == 0)
                        break;

                    manager.Disconnect();
                    retryCount++;
                    Thread.Sleep(1000);
                }
                if (retryCount >= 3)
                {
                    LOG_dict.Add(new Dictionary<string, string> { { "Status", $"{outServer} 連線失敗" } });
                    continue;
                }

                // 批次撈資料
                foreach (var item in outGroup)
                {
                    try
                    {
                        string[] formats = { "yyyy.MM.dd HH:mm", "yyyy.MM.dd" };
                        double timestamp_start = (DateTime.ParseExact(item.From, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;
                        double timestamp_end = (DateTime.ParseExact(item.To, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;

                        if (timestamp_end < timestamp_start)
                            throw new Exception("時間區間設置有誤");

                        var chartData = manager.ChartRequest(item.OutSymbol, Convert.ToUInt32(timestamp_start), Convert.ToUInt32(timestamp_end), out MTRetCode chartRes);

                        if (chartData != null && chartData.Length > 0)
                        {
                            var key = (item.OutServer, item.OutSymbol, item.From, item.To);
                            symbolDataDict[key] = chartData;
                        }
                        else
                        {
                            LOG_dict.Add(new Dictionary<string, string>
                            {
                                { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                                { "Input", $"{item.InServer}[{item.InSymbol}]" },
                                { "Time", $"{item.From} ~ {item.To}" },
                                { "Status", chartRes == MTRetCode.MT_RET_OK_NONE ? "該時段沒有資料" : "ChartRequest 失敗" }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        LOG_dict.Add(new Dictionary<string, string>
                        {
                            { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                            { "Input", $"{item.InServer}[{item.InSymbol}]" },
                            { "Time", $"{item.From} ~ {item.To}" },
                            { "Status", $"撈資料失敗：{ex.Message}" }
                        });
                    }
                }

                manager.Disconnect();
                Thread.Sleep(500);
            }

            // 寫入
            var tasks = new List<Task>();

            var inServerGroups = needList.GroupBy(x => x.InServer);

            foreach (var inGroup in inServerGroups)
            {
                string inServer = inGroup.Key;

                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        if (!server_dict.TryGetValue(inServer, out var InServerRecord))
                        {
                            lock (LOG_dict)
                            {
                                LOG_dict.Add(new Dictionary<string, string> { { "Status", $"{inServer} 不存在" } });
                            }
                            return;
                        }

                        var manager = SMTManagerAPIFactory.CreateManager(SMTManagerAPIFactory.ManagerAPIVersion, out MTRetCode res_create2);
                        if (res_create2 != MTRetCode.MT_RET_OK || manager == null)
                        {
                            lock (LOG_dict)
                            {
                                LOG_dict.Add(new Dictionary<string, string> { { "Status", $"登入 {inServer} 失敗" } });
                            }
                            return;
                        }

                        var loginInfo = loginDict[inServer];
                        int retryCount = 0;
                        while (retryCount < 3)
                        {
                            var res_connect = manager.Connect(InServerRecord.SERVER_PROXY, (ulong)Convert.ToInt32(loginInfo.Login), loginInfo.Password, null, CIMTManagerAPI.EnPumpModes.PUMP_MODE_SYMBOLS, 3600000);
                            if (res_connect == 0)
                                break;

                            manager.Disconnect();
                            retryCount++;
                            Thread.Sleep(3000);
                        }
                        if (retryCount >= 3)
                        {
                            lock (LOG_dict)
                            {
                                LOG_dict.Add(new Dictionary<string, string> { { "Status", $"{inServer} 連線失敗" } });
                            }
                            return;
                        }

                        foreach (var item in inGroup)
                        {
                            try
                            {
                                var key = (item.OutServer, item.OutSymbol, item.From, item.To);
                                if (!symbolDataDict.TryGetValue(key, out var chartData))
                                {
                                    lock (LOG_dict)
                                    {
                                        LOG_dict.Add(new Dictionary<string, string>
                                        {
                                            { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                                            { "Input", $"{item.InServer}[{item.InSymbol}]" },
                                            { "Time", $"{item.From} ~ {item.To}" },
                                            { "Status", "找不到對應的輸出資料" }
                                        });
                                    }
                                    continue;
                                }

                                string[] formats = { "yyyy.MM.dd HH:mm", "yyyy.MM.dd" };
                                double timestamp_start = (DateTime.ParseExact(item.From, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;
                                double timestamp_end = (DateTime.ParseExact(item.To, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;

                                var res = manager.ChartReplace(item.InSymbol, Convert.ToUInt32(timestamp_start), Convert.ToUInt32(timestamp_end), chartData);

                                lock (LOG_dict)
                                {
                                    LOG_dict.Add(new Dictionary<string, string>
                                    {
                                        { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                                        { "Input", $"{item.InServer}[{item.InSymbol}]" },
                                        { "Time", $"{item.From} ~ {item.To}" },
                                        { "Status", res == MTRetCode.MT_RET_OK || res == 0 ? "SUCCESS" : "補K線失敗" }
                                    });
                                }
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
                                        { "Status", $"寫入失敗：{ex.Message}" }
                                    });
                                }
                            }
                        }

                        manager.Disconnect();
                        Thread.Sleep(500);
                    }
                    catch (Exception ex)
                    {
                        lock (LOG_dict)
                        {
                            LOG_dict.Add(new Dictionary<string, string> { { "Status", $"發生錯誤：{ex.Message}" } });
                        }
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            ExportLogToExcel("修補", LOG_dict);

            return LOG_dict;
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
            string _rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // 檢查API連接狀況
            if ((SMTManagerAPIFactory.Initialize(_rootPath)) != MTRetCode.MT_RET_OK ||
                (SMTManagerAPIFactory.Initialize(_rootPath)) != MTRetCode.MT_RET_OK)
            {
                LOG_dict.Add(new Dictionary<string, string>
                {
                    { "Status", "MT5 API 發生錯誤，請重新嘗試" },
                });
                return LOG_dict;
            }

            // 建立manager
            MTRetCode res_out = MTRetCode.MT_RET_OK;
            MTRetCode res_in = MTRetCode.MT_RET_OK;
            var manager_out = SMTManagerAPIFactory.CreateManager(SMTManagerAPIFactory.ManagerAPIVersion, out res_out);
            if ((res_out != MTRetCode.MT_RET_OK) || manager_out == null)
            {
                SMTManagerAPIFactory.Shutdown();
                string message = string.Format("Creating manager failed ({0})", (res_out == MTRetCode.MT_RET_OK ? "Manager api is null" : res_out.ToString()));
                LOG_dict.Add(new Dictionary<string, string>
                {
                    { "Status", message },

                });
                manager_out.Disconnect();
                return LOG_dict;
            }
            var manager_in = SMTManagerAPIFactory.CreateManager(SMTManagerAPIFactory.ManagerAPIVersion, out res_in);
            if ((res_in != MTRetCode.MT_RET_OK) || manager_in == null)
            {
                SMTManagerAPIFactory.Shutdown();
                string message = string.Format("Creating manager failed ({0})", (res_in == MTRetCode.MT_RET_OK ? "Manager api is null" : res_in.ToString()));
                LOG_dict.Add(new Dictionary<string, string>
                {
                    { "Status", message },
                });
                manager_out.Disconnect();
                manager_in.Disconnect();
                return LOG_dict;
            }

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
                        var res_connect = manager_out.Connect(outServer_IP, (ulong)Convert.ToInt32(outServer_Login), outServer_Password, null, CIMTManagerAPI.EnPumpModes.PUMP_MODE_SYMBOLS, 3600000);
                        if (res_connect == 0)
                        {
                            break;
                        }
                        manager_out.Disconnect();
                        retry_out_count += 1;
                        Thread.Sleep(3000);
                    }
                    if (retry_out_count > 2)
                    {
                        manager_out.Disconnect();
                        throw new Exception("輸出的Server登入連線失敗");
                    }

                    // In Server連線配置
                    var inServer_IP = server_dict[item.InServer].SERVER_PROXY;
                    var inServer_Login = loginDict[item.InServer].Login;
                    var inServer_Password = loginDict[item.InServer].Password;

                    int retry_in_count = 0;
                    while (retry_in_count < 3)
                    {
                        var res_connect = manager_in.Connect(inServer_IP, (ulong)Convert.ToInt32(inServer_Login), inServer_Password, null, CIMTManagerAPI.EnPumpModes.PUMP_MODE_SYMBOLS, 3600000);
                        if (res_connect == 0)
                        {
                            break;
                        }
                        manager_in.Disconnect();
                        retry_in_count += 1;
                        Thread.Sleep(3000);
                    }
                    if (retry_in_count > 2)
                    {
                        manager_in.Disconnect();
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

                    MTChartBar[] CharBar_out = manager_out.ChartRequest(item.OutSymbol, Convert.ToUInt32(timestamp_start), Convert.ToUInt32(timestamp_end), out MTRetCode ChartRequest_res);

                    if (CharBar_out != null)
                    {
                        var res = manager_in.ChartReplace(item.InSymbol, Convert.ToUInt32(timestamp_start), Convert.ToUInt32(timestamp_end), CharBar_out);
                        if ((res != MTRetCode.MT_RET_OK) || (res != 0))
                        {
                            LOG_dict.Add(new Dictionary<string, string>
                            {
                                { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                                { "Input", $"{item.InServer}[{item.InSymbol}]" },
                                { "Time", $"{item.From} ~ {item.To}" },
                                { "Status", "補K線時發生錯誤" },
                            });
                        }
                        else
                        {
                            LOG_dict.Add(new Dictionary<string, string>
                            {
                                { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                                { "Input", $"{item.InServer}[{item.InSymbol}]" },
                                { "Time", $"{item.From} ~ {item.To}" },
                                { "Status", "SUCCESS" },
                            });
                        }
                    }
                    else
                    {
                        if (ChartRequest_res == MTRetCode.MT_RET_OK_NONE)
                        {
                            LOG_dict.Add(new Dictionary<string, string>
                            {
                                { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                                { "Input", $"{item.InServer}[{item.InSymbol}]" },
                                { "Time", $"{item.From} ~ {item.To}" },
                                { "Status", $"該時段沒有可用於修補K線的資料" },
                            });
                        }
                        else
                        {
                            LOG_dict.Add(new Dictionary<string, string>
                            {
                                { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                                { "Input", $"{item.InServer}[{item.InSymbol}]" },
                                { "Time", $"{item.From} ~ {item.To}" },
                                { "Status", "FAIL" },
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
                            { "Output", $"{item.OutServer}[{item.OutSymbol}]" },
                            { "Input", $"{item.InServer}[{item.InSymbol}]" },
                            { "Time", $"{item.From} ~ {item.To}" },
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
                            { "Status", ex.Message },
                        });
                    }
                }
                manager_out.Disconnect();
                manager_in.Disconnect();
                Thread.Sleep(3000);
            }
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

            // key: Server; value: Symbol, From, To
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
                    string _rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    MTRetCode res = MTRetCode.MT_RET_OK;

                    // 檢查API連接狀況
                    if ((res = SMTManagerAPIFactory.Initialize(_rootPath)) != MTRetCode.MT_RET_OK)
                    {
                        LOG_dict.Add(new Dictionary<string, string>
                        {
                            { "Server", ServerName },
                            { "message", "MT5 API設置建立時發生錯誤，請重新嘗試" }
                        });
                        return LOG_dict;
                    }

                    // 建立manager
                    var manager = SMTManagerAPIFactory.CreateManager(SMTManagerAPIFactory.ManagerAPIVersion, out res);
                    if ((res != MTRetCode.MT_RET_OK) || manager == null)
                    {
                        SMTManagerAPIFactory.Shutdown();
                        string message = string.Format("Creating manager failed ({0})", (res == MTRetCode.MT_RET_OK ? "Manager api is null" : res.ToString()));
                        LOG_dict.Add(new Dictionary<string, string>
                        {
                            { "Server", ServerName },
                            { "message", message }
                        });
                        return LOG_dict;
                    }

                    // 建立連線
                    int retry_count = 0;
                    while (retry_count < 3)
                    {
                        var res_connect = manager.Connect(ServerRecord.SERVER_PROXY, (ulong)Convert.ToInt32(Server_Login), Server_Password, null,
                                CIMTManagerAPI.EnPumpModes.PUMP_MODE_SYMBOLS, 3600000);
                        if (res_connect == 0)
                        {
                            break;
                        }
                        manager.Disconnect();
                        retry_count += 1;
                        Thread.Sleep(3000);
                    }
                    if (retry_count > 2)
                    {
                        manager.Disconnect();
                        LOG_dict.Add(new Dictionary<string, string>
                        {
                            { "Server", ServerName },
                            { "Status", $"登入連線失敗" },
                        });
                        continue;
                    }

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
                            int totalDays = (int)((timestamp_end - timestamp_start) / 60);
                            long[] bars_dates = new long[totalDays + 1];

                            if (timestamp_end < timestamp_start)
                            {
                                throw new Exception("時間區間設置有誤");
                            }

                            for (int i = 0; i <= totalDays; i++)
                            {
                                bars_dates[i] = (long)(timestamp_start + i * 60);  // 每次增加 1 分鐘
                            }

                            // 撈原有K線(刪除存在的K線)
                            MTChartBar[] chart_request = manager.ChartRequest(_symbol, (long)timestamp_start, (long)timestamp_end, out MTRetCode res_request);
                            if (res_request == MTRetCode.MT_RET_OK)
                            {
                                HashSet<long> chartDatetimes = new HashSet<long>(chart_request.Select(c => c.datetime));
                                long[] newBarsDates = bars_dates.Where(date => chartDatetimes.Contains(date)).ToArray();

                                MTRetCode res_del = manager.ChartDelete(_symbol, newBarsDates);
                                if (res_del == MTRetCode.MT_RET_OK)
                                {
                                    LOG_dict.Add(new Dictionary<string, string>
                                    {
                                        { "Server", ServerName },
                                        { "Symbol", _symbol },
                                        { "Time", $"{_from} ~ {_to}" },
                                        { "Status", "SUCCESS"}
                                    });
                                }
                                else
                                {
                                    LOG_dict.Add(new Dictionary<string, string>
                                    {
                                        { "Server", ServerName },
                                        { "Symbol", _symbol },
                                        { "Time", $"{_from} ~ {_to}" },
                                        { "Status", "FAIL"}
                                    });
                                }
                            }
                            else
                            {
                                LOG_dict.Add(new Dictionary<string, string>
                                {
                                    { "Server", ServerName },
                                    { "Symbol", _symbol },
                                    { "Time", $"{_from} ~ {_to}" },
                                    { "Status", "該時段無可刪除的K線資料"}
                                });
                            }
                        }
                        catch
                        {
                            LOG_dict.Add(new Dictionary<string, string>
                            {
                                { "Server", ServerName },
                                { "Symbol", _symbol },
                                { "Time", $"{_from} ~ {_to}" },
                                { "Status", "ERROR"}
                            });
                        }
                    }
                    manager.Disconnect();
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

        // SQL資料撈取(Edit用)
        public static DataTable ExecuteSQL(string server, string sql, string symbol)
        {
            Initiallize();
            DataTable data = new();
            string sqlcommand = $"SELECT '{server}' as InServer, Symbol as InSymbol FROM {sql}.mt5_symbols where Symbol regexp '{symbol}'";

            // 這邊要自動抓mt5要用的連線
            var sql_config = server_dict[server];
            string _connection =
                    $"server={sql_config.SQL_HOST};" +
                    $"user={sql_config.SQL_USER};" +
                    $"password={sql_config.SQL_PASSWORD};" +
                    $"port={sql_config.SQL_PORT};" +
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