using MetaQuotes.MT5ManagerAPI;
using Microsoft.AspNetCore.Http;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using OfficeOpenXml;
using P23.MetaTrader4.Manager;
using P23.MetaTrader4.Manager.Contracts.Configuration;
using P23.MetaTrader4.Manager.Contracts.Configuration.Enums;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using static admin_web.Models.Mtapiuse.EditSymbol_Model;
using static admin_web.Models.ServerModel;

namespace admin_web.Services.MtapiServices
{
    public class EditSymbolService
    {
        private static IFormFile InputFile;
        private static readonly string connectionString = UniversalService.sql_connectionString;
        private static Dictionary<string, ServerRecord> server_dict = new();

        private static void Initiallize()
        {
            server_dict = UniversalService.GetAllServerIP();
        }

        public static List<log_record> MainProgram(IFormFile file)
        {
            Initiallize();
            InputFile = file;
            List<log_record> all_log = new();
            List<sql_record> sql_log = new();

            var (mt4_log, mt4_sql_log) = MT4_API();
            all_log.AddRange(mt4_log);
            sql_log.AddRange(mt4_sql_log);

            //var mt5_log = MT5_API();
            //var (mt5_log, mt5_sql_log) = MT5_API();
            //all_log.AddRange(mt5_log);
            //sql_log.AddRange(mt5_sql_log);

            InsertLogRecordsToDatabase(sql_log);

            return all_log;
        }

        // MT4 執行流程
        private static (List<log_record>, List<sql_record>) MT4_API()
        {
            List<log_record> all_log_list = new();
            List<sql_record> sql_log_list = new();
            var (inputRecords, loginRecords) = Read_Input_MT4();

            var rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var aa = Path.Combine(rootPath, Environment.Is64BitProcess ? "mtmanapi64.dll" : "mtmanapi.dll");
            var AIROE = new ClrWrapper(aa);

            foreach (var item in inputRecords)
            {
                var server = item.Key;
                List<Symbol> all_symbol_update_before = new();
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

                var all_symbol = AIROE.CfgRequestSymbol();

                foreach (var input_record in item.Value)
                {
                    // 每行資料單個LOG
                    log_record log_record = new();
                    log_record.Server = server;
                    log_record.Symbol = input_record.Symbol;
                    try
                    {
                        // 檢查是否有該Login
                        if (all_symbol.Any(record => record.Name == input_record.Symbol))
                        {
                            // 取出record
                            var symbol_record_before = DeepCopy(all_symbol.FirstOrDefault(record => record.Name == input_record.Symbol));
                            var symbol_record_after = DeepCopy(symbol_record_before);
                            all_symbol_update_before.Add(symbol_record_before);

                            if (!string.IsNullOrEmpty(input_record.Symbol_Description))
                            {
                                if (input_record.Symbol_Description.Length > 63)
                                {
                                    throw new Exception("警告：Symbol_Description 的字元不可超過 63");
                                }
                                else if (input_record.Symbol_Description == "null")
                                {
                                    symbol_record_after.Description = "";
                                }
                                else
                                {
                                    symbol_record_after.Description = input_record.Symbol_Description;
                                }

                            }

                            if (!string.IsNullOrEmpty(input_record.Symbol_Type))
                            {
                                if (int.TryParse(input_record.Symbol_Type, out var type_num))
                                {
                                    if (type_num > 31 || type_num < 0)
                                    {
                                        throw new Exception("Symbol_Type 的設置範圍為 0 ~ 31");
                                    }
                                    else
                                    {
                                        symbol_record_after.Type = type_num;
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Symbol_Type 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Symbol_Trade))
                            {
                                if (int.TryParse(input_record.Symbol_Trade, out var trade_num))
                                {
                                    if (new[] { 0, 1, 2 }.Contains(trade_num))
                                    {
                                        symbol_record_after.Trade = (TradeMode)trade_num;
                                    }
                                    else
                                    {
                                        throw new Exception("Symbol_Trade 的設置僅能為整數 0(No), 1(Close Only), 2(Full Access)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Symbol_Trade 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Symbol_StopLevel))
                            {
                                if (int.TryParse(input_record.Symbol_StopLevel, out var StopLevel))
                                {
                                    if (StopLevel > 1000 || StopLevel < 0)
                                    {
                                        throw new Exception("Symbol_StopLevel 的設置範圍為 0 ~ 1000");
                                    }
                                    else
                                    {
                                        symbol_record_after.StopsLevel = StopLevel;
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Symbol_StopLevel 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Symbol_FreezeLevel))
                            {
                                if (int.TryParse(input_record.Symbol_FreezeLevel, out var FreezeLevel))
                                {
                                    if (FreezeLevel > 1000 || FreezeLevel < 0)
                                    {
                                        throw new Exception("Symbol_FreezeLevel 的設置範圍為 0 ~ 1000");
                                    }
                                    else
                                    {
                                        symbol_record_after.FreezeLevel = FreezeLevel;
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Symbol_FreezeLevel 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Symbol_LongOnly))
                            {
                                if (int.TryParse(input_record.Symbol_LongOnly, out var LongOnly_num))
                                {
                                    if (LongOnly_num == 0 || LongOnly_num == 1)
                                    {
                                        symbol_record_after.LongOnly = LongOnly_num;
                                    }
                                    else
                                    {
                                        throw new Exception("Symbol_LongOnly 的設置僅能為 0(Disable), 1(Enable)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Symbol_LongOnly 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Filtration_Level))
                            {
                                if (int.TryParse(input_record.Filtration_Level, out var Level))
                                {
                                    if (Level > 99999 || Level < 0)
                                    {
                                        throw new Exception("Filtration_Level 的設置範圍為 0 ~ 99999");
                                    }
                                    else
                                    {
                                        symbol_record_after.Filter = Level;
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Filtration_Level 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Filtration_AutomaticLimit))
                            {
                                if (double.TryParse(input_record.Filtration_AutomaticLimit, out var Limit))
                                {
                                    if (new[] { 0, 0.1, 0.5, 1, 3, 5, 10, 15, 20 }.Contains(Limit))
                                    {
                                        symbol_record_after.FilterLimit = Limit * 0.01;
                                    }
                                    else
                                    {
                                        throw new Exception("Filtration_AutomaticLimit 的設置僅能為 0, 0.1, 0.5, 1, 3, 5, 10, 15, 20 (%)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Filtration_Level 轉換成有效的數字");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Filtration_Filter))
                            {
                                if (int.TryParse(input_record.Filtration_Filter, out var filter))
                                {
                                    if (filter > 10 || filter < 1)
                                    {
                                        throw new Exception("Filtration_Filter 的設置範圍為 1 ~ 10");
                                    }
                                    else
                                    {
                                        symbol_record_after.FilterCounter = filter;
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Filtration_Filter 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Filtration_IgnoreQuotes))
                            {
                                if (int.TryParse(input_record.Filtration_IgnoreQuotes, out var IgnoreQuotes))
                                {
                                    if (IgnoreQuotes < 0)
                                    {
                                        throw new Exception("Filtration_IgnoreQuotes 不可為負數");
                                    }
                                    symbol_record_after.QuotesDelay = IgnoreQuotes;
                                }
                                else
                                {
                                    throw new Exception("無法將 Filtration_IgnoreQuotes 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Swap_Enable))
                            {
                                if (int.TryParse(input_record.Swap_Enable, out var Enable))
                                {
                                    if (Enable == 0 || Enable == 1)
                                    {
                                        symbol_record_after.SwapEnable = Enable;
                                    }
                                    else
                                    {
                                        throw new Exception("Swap_Enable 的設置僅能為 0(Disable), 1(Enable)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Swap_Enable 轉換成有效的整數");
                                }
                            }

                            // 前後比對
                            var differences = new List<string>();
                            var properties = typeof(Symbol).GetProperties();
                            foreach (var property in properties)
                            {
                                var value1 = property.GetValue(symbol_record_before)?.ToString();
                                var value2 = property.GetValue(symbol_record_after)?.ToString();

                                if (value1 == null && value2 == null)
                                {
                                    continue;
                                }
                                else if (value1 == null || value2 == null || !value1.Equals(value2))
                                {
                                    differences.Add($"[修改]{property.Name}: {value1} -> {value2}");
                                }
                            }

                            // 簡單比對: 有和原本設置不同的就做update，不管update後有無不同
                            if (differences.Count > 0)
                            {
                                int res = AIROE.CfgUpdateSymbol(symbol_record_after);
                                if (res != 0)
                                {
                                    throw new Exception("執行失敗");
                                }
                                else
                                {
                                    log_record.Result = "執行成功";
                                }
                            }
                            else
                            {
                                log_record.Result = "無變動";
                            }
                        }
                        else
                        {
                            throw new Exception("找不到該Symbol");
                        }
                    }
                    catch (Exception ex)
                    {
                        log_record.Difference = null;
                        log_record.Result = "警告：" + ex.Message;
                    }
                    finally
                    {
                        all_log_list.Add(log_record);
                    }
                }

                // Update後的比對
                var all_symbol_AfterUpdate = AIROE.CfgRequestSymbol();

                foreach (var input_record in item.Value)
                {
                    var log = all_log_list.FirstOrDefault(record => record.Symbol == input_record.Symbol && record.Server == server);

                    // 檢查API Update成功的
                    if (log.Result == "執行成功")
                    {
                        var symbol_info_before = all_symbol_update_before.FirstOrDefault(record => record.Name == input_record.Symbol);
                        var time_set = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        var differences = new List<string>(); // 給User看的比對
                        var symbol_info_after = all_symbol_AfterUpdate.FirstOrDefault(symbol => symbol.Name == symbol_info_before.Name);
                        if (symbol_info_before.Description != symbol_info_after.Description)
                        {
                            differences.Add($"[修改]Symbol_Description: {symbol_info_before.Description} -> {symbol_info_after.Description}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Symbol = symbol_info_before.Name,
                                Item = "Symbol_Description",
                                Before = symbol_info_before.Description,
                                After = symbol_info_after.Description,
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Symbol_Description) && input_record.Symbol_Description != symbol_info_after.Description.ToString())
                        {
                            differences.Add($"[未改]Symbol_Description: {symbol_info_before.Description} -> {symbol_info_after.Description}");
                        }

                        if (symbol_info_before.Type != symbol_info_after.Type)
                        {
                            differences.Add($"[修改]Symbol_Type: {symbol_info_before.Type} -> {symbol_info_after.Type}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Symbol = symbol_info_before.Name,
                                Item = "Symbol_Type",
                                Before = symbol_info_before.Type.ToString(),
                                After = symbol_info_after.Type.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Symbol_Type) && input_record.Symbol_Type != symbol_info_after.Type.ToString())
                        {
                            differences.Add($"[未改]Symbol_Type: {symbol_info_before.Type} -> {symbol_info_after.Type}");
                        }

                        if (symbol_info_before.Trade != symbol_info_after.Trade)
                        {
                            differences.Add($"[修改]Symbol_Trade: {symbol_info_before.Trade} -> {symbol_info_after.Trade}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Symbol = symbol_info_before.Name,
                                Item = "Symbol_Trade",
                                Before = symbol_info_before.Trade.ToString(),
                                After = symbol_info_after.Trade.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Symbol_Trade) && input_record.Symbol_Trade != symbol_info_after.Trade.ToString())
                        {
                            differences.Add($"[未改]Symbol_Trade: {symbol_info_before.Trade} -> {symbol_info_after.Trade}");
                        }

                        if (symbol_info_before.LongOnly != symbol_info_after.LongOnly)
                        {
                            differences.Add($"[修改]Symbol_LongOnly: {symbol_info_before.LongOnly} -> {symbol_info_after.LongOnly}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Symbol = symbol_info_before.Name,
                                Item = "Symbol_LongOnly",
                                Before = symbol_info_before.LongOnly.ToString(),
                                After = symbol_info_after.LongOnly.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Symbol_LongOnly) && input_record.Symbol_LongOnly != symbol_info_after.LongOnly.ToString())
                        {
                            differences.Add($"[未改]Symbol_LongOnly: {symbol_info_before.LongOnly} -> {symbol_info_after.LongOnly}");
                        }

                        if (symbol_info_before.StopsLevel != symbol_info_after.StopsLevel)
                        {
                            differences.Add($"[修改]Symbol_StopLevel: {symbol_info_before.StopsLevel} -> {symbol_info_after.StopsLevel}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Symbol = symbol_info_before.Name,
                                Item = "Symbol_StopLevel",
                                Before = symbol_info_before.StopsLevel.ToString(),
                                After = symbol_info_after.StopsLevel.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Symbol_StopLevel) && input_record.Symbol_StopLevel != symbol_info_after.StopsLevel.ToString())
                        {
                            differences.Add($"[未改]Symbol_StopLevel: {symbol_info_before.StopsLevel} -> {symbol_info_after.StopsLevel}");
                        }

                        if (symbol_info_before.FreezeLevel != symbol_info_after.FreezeLevel)
                        {
                            differences.Add($"[修改]Symbol_FreezeLevel: {symbol_info_before.FreezeLevel} -> {symbol_info_after.FreezeLevel}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Symbol = symbol_info_before.Name,
                                Item = "Symbol_FreezeLevel",
                                Before = symbol_info_before.FreezeLevel.ToString(),
                                After = symbol_info_after.FreezeLevel.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Symbol_FreezeLevel) && input_record.Symbol_FreezeLevel != symbol_info_after.FreezeLevel.ToString())
                        {
                            differences.Add($"[未改]Symbol_FreezeLevel: {symbol_info_before.FreezeLevel} -> {symbol_info_after.FreezeLevel}");
                        }

                        if (symbol_info_before.Filter != symbol_info_after.Filter)
                        {
                            differences.Add($"[修改]Filtration_Level: {symbol_info_before.Filter} -> {symbol_info_after.Filter}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Symbol = symbol_info_before.Name,
                                Item = "Filtration_Level",
                                Before = symbol_info_before.Filter.ToString(),
                                After = symbol_info_after.Filter.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Filtration_Level) && input_record.Filtration_Level != symbol_info_after.Filter.ToString())
                        {
                            differences.Add($"[未改]Filtration_Level: {symbol_info_before.Filter} -> {symbol_info_after.Filter}");
                        }

                        if (symbol_info_before.FilterLimit != symbol_info_after.FilterLimit)
                        {
                            differences.Add($"[修改]Filtration_AutomaticLimit: {symbol_info_before.FilterLimit*100}% -> {symbol_info_after.FilterLimit*100}%");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Symbol = symbol_info_before.Name,
                                Item = "Filtration_AutomaticLimit",
                                Before = symbol_info_before.FilterLimit.ToString(),
                                After = symbol_info_after.FilterLimit.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Filtration_AutomaticLimit) && input_record.Filtration_AutomaticLimit != symbol_info_after.FilterLimit.ToString())
                        {
                            differences.Add($"[未改]Filtration_AutomaticLimit: {symbol_info_before.FilterLimit*100}% -> {symbol_info_after.FilterLimit*100}%");
                        }

                        if (symbol_info_before.FilterCounter != symbol_info_after.FilterCounter)
                        {
                            differences.Add($"[修改]Filtration_Filter: {symbol_info_before.FilterCounter} -> {symbol_info_after.FilterCounter}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Symbol = symbol_info_before.Name,
                                Item = "Filtration_Filter",
                                Before = symbol_info_before.FilterCounter.ToString(),
                                After = symbol_info_after.FilterCounter.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Filtration_Filter) && input_record.Filtration_Filter != symbol_info_after.FilterCounter.ToString())
                        {
                            differences.Add($"[未改]Filtration_Filter: {symbol_info_before.FilterCounter} -> {symbol_info_after.FilterCounter}");
                        }

                        if (symbol_info_before.QuotesDelay != symbol_info_after.QuotesDelay)
                        {
                            differences.Add($"[修改]Filtration_IgnoreQuotes: {symbol_info_before.QuotesDelay} -> {symbol_info_after.QuotesDelay}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Symbol = symbol_info_before.Name,
                                Item = "Filtration_IgnoreQuotes",
                                Before = symbol_info_before.QuotesDelay.ToString(),
                                After = symbol_info_after.QuotesDelay.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Filtration_IgnoreQuotes) && input_record.Filtration_IgnoreQuotes != symbol_info_after.QuotesDelay.ToString())
                        {
                            differences.Add($"[未改]Filtration_IgnoreQuotes: {symbol_info_before.QuotesDelay} -> {symbol_info_after.QuotesDelay}");
                        }

                        if (symbol_info_before.SwapEnable != symbol_info_after.SwapEnable)
                        {
                            differences.Add($"[修改]Swap_Enable: {symbol_info_before.SwapEnable} -> {symbol_info_after.SwapEnable}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Symbol = symbol_info_before.Name,
                                Item = "Swap_Enable",
                                Before = symbol_info_before.SwapEnable.ToString(),
                                After = symbol_info_after.SwapEnable.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Swap_Enable) && input_record.Swap_Enable != symbol_info_after.SwapEnable.ToString())
                        {
                            differences.Add($"[未改]Swap_Enable: {symbol_info_before.SwapEnable} -> {symbol_info_after.SwapEnable}");
                        }

                        // 給User看的比對結果
                        if (differences.Count > 0)
                        {
                            log.Difference = differences;
                        }
                        else
                        {
                            log.Result = "無變動";
                        }
                    }
                }

                AIROE.Disconnect();
            }

            return (all_log_list, sql_log_list);
        }

        // MT5 執行流程
        private static List<log_record> MT5_API()
        {
            var all_log_list = new List<log_record>();
            var (inputRecords, loginRecords) = Read_Input_MT5();

            string _rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            SMTManagerAPIFactory.Initialize(_rootPath);
            var admin = SMTManagerAPIFactory.CreateAdmin(SMTManagerAPIFactory.ManagerAPIVersion, out var _);

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
                        var res_connect = admin.Connect(server_ip, ulong.Parse(server_config.Login), server_config.Password, null,
                                        CIMTAdminAPI.EnPumpModes.PUMP_MODE_NEWS, 3600000);
                        if (res_connect == 0)
                        {
                            break;
                        }
                        admin.Disconnect();
                        retry_count += 1;
                        Thread.Sleep(1500);
                    }
                    if (retry_count > 2)
                    {
                        // 登入連線異常
                        admin.Disconnect();
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
                    // 每行資料單個LOG
                    log_record log_record = new();
                    log_record.Server = server;
                    log_record.Symbol = input_record.Symbol;
                    try
                    {
                        // 檢查是否有該Login
                        var symbol_record_after = admin.SymbolCreate();
                        if (admin.SymbolGet(input_record.Symbol, symbol_record_after) == 0)
                        {
                            // 更新前先撈一次做比對用
                            var symbol_record_before = admin.SymbolCreate();
                            if (admin.SymbolGet(input_record.Symbol, symbol_record_before) != 0)
                            {
                                throw new Exception("執行API時發生問題");
                            }

                            if (!string.IsNullOrEmpty(input_record.Common_Description))
                            {
                                if (input_record.Common_Description.Length > 63)
                                {
                                    throw new Exception("Common_Description 的字數上限為 63 個字元");
                                }
                                else if (input_record.Common_Description == "null")
                                {
                                    symbol_record_after.Description("");
                                }
                                else
                                {
                                    symbol_record_after.Description(input_record.Common_Description);
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Common_ISIN))
                            {
                                if (input_record.Common_ISIN.Length > 15)
                                {
                                    throw new Exception("Common_ISIN 的字數上限為 15 個字元");
                                }
                                else if (input_record.Common_ISIN == "null")
                                {
                                    symbol_record_after.ISIN("");
                                }
                                else
                                {
                                    symbol_record_after.ISIN(input_record.Common_ISIN);
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Common_International))
                            {
                                if (input_record.Common_International.Length > 63)
                                {
                                    throw new Exception("Common_International 的字數上限為 63 個字元");
                                }
                                else if (input_record.Common_International == "null")
                                {
                                    symbol_record_after.International("");
                                }
                                else
                                {
                                    symbol_record_after.International(input_record.Common_International);
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Common_Exchange))
                            {
                                if (input_record.Common_Exchange.Length > 63)
                                {
                                    throw new Exception("Common_Exchange 的字數上限為 63 個字元");
                                }
                                else if (input_record.Common_Exchange == "null")
                                {
                                    symbol_record_after.Exchange("");
                                }
                                else
                                {
                                    symbol_record_after.Exchange(input_record.Common_Exchange);
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Common_CFI))
                            {
                                if (input_record.Common_CFI.Length > 7)
                                {
                                    throw new Exception("Common_CFI 的字數上限為 7 個字元");
                                }
                                else if (input_record.Common_CFI == "null")
                                {
                                    symbol_record_after.CFI("");
                                }
                                else
                                {
                                    symbol_record_after.CFI(input_record.Common_CFI);
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Quotes_SoftFiltrationLevel))
                            {
                                if (uint.TryParse(input_record.Quotes_SoftFiltrationLevel, out var level))
                                {
                                    if (level > 99999 || level < 0)
                                    {
                                        throw new Exception("Quotes_SoftFiltrationLevel 的設置範圍為 0 ~ 99999");
                                    }
                                    else
                                    {
                                        symbol_record_after.FilterSoft(level);
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Quotes_SoftFiltrationLevel 轉換成有效的正整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Quotes_SoftFilter))
                            {
                                if (uint.TryParse(input_record.Quotes_SoftFilter, out var ticks))
                                {
                                    if (ticks > 10 || ticks < 1)
                                    {
                                        throw new Exception("Quotes_SoftFilter 的設置範圍為 1 ~ 10");
                                    }
                                    else
                                    {
                                        symbol_record_after.FilterSoftTicks(ticks);
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Quotes_SoftFilter 轉換成有效的正整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Quotes_HardFiltrationLevel))
                            {
                                if (uint.TryParse(input_record.Quotes_HardFiltrationLevel, out var level))
                                {
                                    if (level > 99999 || level < 0)
                                    {
                                        throw new Exception("Quotes_HardFiltrationLevel 的設置範圍為 0 ~ 99999");
                                    }
                                    else
                                    {
                                        symbol_record_after.FilterHard(level);
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Quotes_HardFiltrationLevel 轉換成有效的正整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Quotes_HardFilter))
                            {
                                if (uint.TryParse(input_record.Quotes_HardFilter, out var ticks))
                                {
                                    if (ticks > 10 || ticks < 1)
                                    {
                                        throw new Exception("Quotes_HardFilter 的設置範圍為 1 ~ 10");
                                    }
                                    else
                                    {
                                        symbol_record_after.FilterHardTicks(ticks);
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Quotes_HardFilter 轉換成有效的正整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Trade_VolumesMin))
                            {
                                if (ulong.TryParse(input_record.Trade_VolumesMin, out var num))
                                {
                                    if (num > 1000 || num < 0)
                                    {
                                        throw new Exception("Trade_VolumesMin 的設置範圍為 0 ~ 1000");
                                    }
                                    else
                                    {
                                        symbol_record_after.VolumeMin((ulong)(num * 0.0001));
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Trade_VolumesMin 轉換成有效的正長整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Trade_VolumesMax))
                            {
                                if (ulong.TryParse(input_record.Trade_VolumesMax, out var num))
                                {
                                    if (num > 1000 || num < 1)
                                    {
                                        throw new Exception("Trade_VolumesMax 的設置範圍為 1 ~ 1000");
                                    }
                                    else
                                    {
                                        symbol_record_after.VolumeMax((ulong)(num * 0.0001));
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Trade_VolumesMax 轉換成有效的正長整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Trade_VolumesStep))
                            {
                                if (ulong.TryParse(input_record.Trade_VolumesStep, out var num))
                                {
                                    if (num > 1000 || num < 0)
                                    {
                                        throw new Exception("Trade_VolumesStep 的設置範圍為 0 ~ 1000");
                                    }
                                    else
                                    {
                                        symbol_record_after.VolumeStep((ulong)(num * 0.0001));
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Trade_VolumesStep 轉換成有效的正長整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Trade_StopLevel))
                            {
                                if (int.TryParse(input_record.Trade_StopLevel, out var level))
                                {
                                    if (level > 1000 || level < 1)
                                    {
                                        throw new Exception("Trade_StopLevel 的設置範圍為 1 ~ 1000");
                                    }
                                    else
                                    {
                                        symbol_record_after.StopsLevel(level);
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Trade_StopLevel 轉換成有效的正整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Trade_FreezeLevel))
                            {
                                if (int.TryParse(input_record.Trade_FreezeLevel, out var level))
                                {
                                    if (level > 1000 || level < 1)
                                    {
                                        throw new Exception("Trade_FreezeLevel 的設置範圍為 1 ~ 1000");
                                    }
                                    else
                                    {
                                        symbol_record_after.FreezeLevel(level);
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Trade_FreezeLevel 轉換成有效的正整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.Swaps_Enable))
                            {
                                if (uint.TryParse(input_record.Swaps_Enable, out var Enable))
                                {
                                    if (Enable == 0 || Enable == 1)
                                    {
                                        symbol_record_after.SwapMode(Enable);
                                    }
                                    else
                                    {
                                        throw new Exception("Swaps_Enable 的設置僅能為 0(Disable), 1(Enable)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 Swaps_Enable 轉換成有效的正整數");
                                }
                            }

                            // 前後比對
                            List<string> differences = new();
                            void AddDifference<T>(string propertyName, Func<T> getterBefore, Func<T> getterAfter)
                            {
                                var valueBefore = getterBefore();
                                var valueAfter = getterAfter();
                                if (!EqualityComparer<T>.Default.Equals(valueBefore, valueAfter))
                                {
                                    differences.Add($"[修改]{propertyName}: {valueBefore} -> {valueAfter}");
                                }
                            }
                            AddDifference("Common_Description", symbol_record_before.Description, symbol_record_after.Description);
                            AddDifference("Common_Exchange", symbol_record_before.Exchange, symbol_record_after.Exchange);
                            AddDifference("Common_International", symbol_record_before.International, symbol_record_after.International);
                            AddDifference("Common_ISIN", symbol_record_before.ISIN, symbol_record_after.ISIN);
                            AddDifference("Common_CFI", symbol_record_before.CFI, symbol_record_after.CFI);
                            AddDifference("Quotes_SoftFiltrationLevel", symbol_record_before.FilterSoft, symbol_record_after.FilterSoft);
                            AddDifference("Quotes_SoftFilter", symbol_record_before.FilterSoftTicks, symbol_record_after.FilterSoftTicks);
                            AddDifference("Quotes_HardFiltrationLevel", symbol_record_before.FilterHard, symbol_record_after.FilterHard);
                            AddDifference("Quotes_HardFilter", symbol_record_before.FilterHardTicks, symbol_record_after.FilterHardTicks);
                            AddDifference("Trade_VolumesMin", symbol_record_before.VolumeMin, symbol_record_after.VolumeMin);
                            AddDifference("Trade_VolumesStep", symbol_record_before.VolumeStep, symbol_record_after.VolumeStep);
                            AddDifference("Trade_VolumesMax", symbol_record_before.VolumeMax, symbol_record_after.VolumeMax);
                            AddDifference("Trade_StopLevel", symbol_record_before.StopsLevel, symbol_record_after.StopsLevel);
                            AddDifference("Trade_FreezeLevel", symbol_record_before.FreezeLevel, symbol_record_after.FreezeLevel);
                            AddDifference("Swaps_Enable", symbol_record_before.SwapMode, symbol_record_after.SwapMode);

                            if (differences.Count == 0)
                            {
                                log_record.Result = "無變動";
                            }
                            else
                            {
                                log_record.Difference = differences;

                                var res = admin.SymbolUpdate(symbol_record_after);
                                if (res != 0)
                                {
                                    throw new Exception("更新失敗");
                                }
                                log_record.Result = "更新成功";
                            }
                        }
                        else
                        {
                            throw new Exception("找不到該Symbol");
                        }
                    }
                    catch (Exception ex)
                    {
                        log_record.Difference = null;
                        log_record.Result = "警告：" + ex.Message;
                    }
                    finally
                    {
                        all_log_list.Add(log_record);
                    }
                }
                admin.Disconnect();
            }
            return all_log_list;
        }

        // 讀取Input檔案
        private static (Dictionary<string, List<mt4_input>>, Dictionary<string, Login_Record>) Read_Input_MT4()
        {
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            var inputRecords = new Dictionary<string, List<mt4_input>>();
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
                            var record = new mt4_input
                            {
                                Server = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Server") + 1].Text,
                                Symbol = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Symbol") + 1].Text,
                                Symbol_Description = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Symbol_Description") + 1].Text,
                                Symbol_Type = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Symbol_Type") + 1].Text,
                                Symbol_Trade = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Symbol_Trade") + 1].Text,
                                Symbol_StopLevel = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Symbol_StopLevel") + 1].Text,
                                Symbol_FreezeLevel = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Symbol_FreezeLevel") + 1].Text,
                                Symbol_LongOnly = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Symbol_LongOnly") + 1].Text,
                                Filtration_Level = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Filtration_Level") + 1].Text,
                                Filtration_AutomaticLimit = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Filtration_AutomaticLimit") + 1].Text,
                                Filtration_Filter = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Filtration_Filter") + 1].Text,
                                Filtration_IgnoreQuotes = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Filtration_IgnoreQuotes") + 1].Text,
                                Swap_Enable = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Swap_Enable") + 1].Text,
                            };

                            if (record.Server.Length == 0)
                            {
                                continue;
                            }

                            if (!inputRecords.ContainsKey(record.Server))
                            {
                                inputRecords[record.Server] = new List<mt4_input>();
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

        private static (Dictionary<string, List<mt5_input>>, Dictionary<string, Login_Record>) Read_Input_MT5()
        {
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            var inputRecords = new Dictionary<string, List<mt5_input>>();
            var loginRecords = new Dictionary<string, Login_Record>();

            using (var stream = new MemoryStream())
            {
                InputFile.CopyTo(stream);
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet_input = package.Workbook.Worksheets[$"mt5_input"];

                    if (worksheet_input != null)
                    {
                        int rows = worksheet_input.Dimension.Rows;
                        var headerRow = worksheet_input.Cells[1, 1, 1, worksheet_input.Dimension.End.Column].Select(cell => cell.Text.Trim()).ToArray();

                        for (int row = 2; row <= rows; row++)
                        {
                            var record = new mt5_input
                            {
                                Server = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Server") + 1].Text,
                                Symbol = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Symbol") + 1].Text,
                                Common_Description = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Common_Description") + 1].Text,
                                Common_Exchange = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Common_Exchange") + 1].Text,
                                Common_International = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Common_International") + 1].Text,
                                Common_ISIN = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Common_ISIN") + 1].Text,
                                Common_CFI = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Common_CFI") + 1].Text,
                                Quotes_SoftFiltrationLevel = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Quotes_SoftFiltrationLevel") + 1].Text,
                                Quotes_SoftFilter = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Quotes_SoftFilter") + 1].Text,
                                Quotes_HardFiltrationLevel = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Quotes_HardFiltrationLevel") + 1].Text,
                                Quotes_HardFilter = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Quotes_HardFilter") + 1].Text,
                                Trade_VolumesMin = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Trade_VolumesMin") + 1].Text,
                                Trade_VolumesStep = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Trade_VolumesStep") + 1].Text,
                                Trade_VolumesMax = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Trade_VolumesMax") + 1].Text,
                                Trade_StopLevel = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Trade_StopLevel") + 1].Text,
                                Trade_FreezeLevel = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Trade_FreezeLevel") + 1].Text,
                                Swaps_Enable = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Swaps_Enable") + 1].Text,
                            };

                            if (record.Server.Length == 0)
                            {
                                continue;
                            }

                            if (!inputRecords.ContainsKey(record.Server))
                            {
                                inputRecords[record.Server] = new List<mt5_input>();
                            }

                            inputRecords[record.Server].Add(record);
                        }
                    }

                    var worksheet_login = package.Workbook.Worksheets[$"mt5_login"];
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

        private static T DeepCopy<T>(T obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<T>(json);
        }


        // 撈取所有Server IP
        private static void GetAllServerIP()
        {
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
                        AND sl.PROXY != ''";

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
                                //SQL_NAME = reader["SQL_NAME"].ToString(),
                                //SQL_HOST = reader["HOST"].ToString(),
                                //SQL_USER = reader["USER"].ToString(),
                                //SQL_PASSWORD = reader["PASSWORD"].ToString(),
                                //SQL_PORT = reader["PORT"].ToString()
                            };
                            server_dict[serverName] = record;
                        }
                    }
                }
            }
        }

        // 更新紀錄寫進資料庫
        public static void InsertLogRecordsToDatabase(List<sql_record> tool_Log)
        {
            Initiallize();
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    INSERT INTO admin_tool_log.edit_symbol (
                        Server, Symbol, Item, `Before`, `After`, UserLogin, Time
                    ) VALUES (
                        @Server, @Symbol, @Item, @Before, @After, @UserLogin, @Time
                    );";

                foreach (var logRecord in tool_Log)
                {
                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Server", logRecord.Server);
                        cmd.Parameters.AddWithValue("@Symbol", logRecord.Symbol);
                        cmd.Parameters.AddWithValue("@Item", logRecord.Item);
                        cmd.Parameters.AddWithValue("@Before", logRecord.Before);
                        cmd.Parameters.AddWithValue("@After", logRecord.After);
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
                        FROM admin_tool_log.edit_symbol
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

                string fileName = "批量修改商品資訊HistoryLog.xlsx";
                var stream = new MemoryStream(package.GetAsByteArray());

                return (stream, fileName);
            }
        }
    }
}
