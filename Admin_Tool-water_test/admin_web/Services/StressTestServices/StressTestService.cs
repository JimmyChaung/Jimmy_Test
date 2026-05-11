using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using P23.MetaTrader4.Manager;
using P23.MetaTrader4.Manager.Contracts;
using MetaQuotes.MT5ManagerAPI;
using MetaQuotes.MT5CommonAPI;
using static admin_web.Models.StressTest.StressTestModel;
using MySql.Data.MySqlClient;
using System.IO;
using System.Reflection;
using System.Data;
using admin_web.Models.StressTest;
using System.Text.RegularExpressions;
using System.Dynamic;
using System.Diagnostics;
using static admin_web.Controllers.PressureLabController;
using Xceed.Words.NET;

using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading;
using System.Text;

namespace admin_web.Services.StressTestServices
{
    public class StressTestService
    {
        private readonly IConfiguration _configuration;

        public StressTestService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public interface IMTService<TApiClient>
        {
            // API 連線同步
            Task<ServerResult<TApiClient>> ServerAPIAsync(ServerBase request);
            // 透過 API 獲取 Group  
            Task<ServerResult<TApiClient>> ApiGetGroupsAsync(ServerResult<TApiClient> request);
            // 透過 DB groups 獲取 Group
            Task<ServerBase> DBGetGroupsAsync(ServerBase request);
            // 關閉連線
            ServerResult<TApiClient> CloseConnection(ServerResult<TApiClient> request);
            // 創建帳號 (API)
            ServerResult<TApiClient> CreateAccount(ServerResult<TApiClient> request, int volumne, string group, int leverage, string comment);
            // 出入金 (API)
            ServerResult<TApiClient> Deposit(ServerResult<TApiClient> request, double balance, string comment);
            // 後續有其他功能都從這往下增加。
            bool DeleteAccount(ServerResult<TApiClient> request, long start_acc, long end_acc);

            Respect_Red_Result get_mt_journal(ServerResult<TApiClient> request,string cpu_mode, string server_hour, DateTime action_time);

            ServerResult<TApiClient> GetSymbol(ServerResult<TApiClient> request);
            Task<ServerResult<TApiClient>> OpenTrade(ServerResult<TApiClient> request, StressTestRequest info);
        }

        public class MT4Service : IMTService<ClrWrapper>
        {
            public async Task<ServerResult<ClrWrapper>> ServerAPIAsync(ServerBase ServerRequest)
            {
                try
                {
                    var rootPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    var aa = System.IO.Path.Combine(rootPath, Environment.Is64BitProcess ? "mtmanapi64.dll" : "mtmanapi.dll");
                    var AIROE = new ClrWrapper(aa);

                    AIROE.Connect(ServerRequest.ServerIp);
                    AIROE.Login((int)ServerRequest.AdminLogin, ServerRequest.Password);
                    return new ServerResult<ClrWrapper>(ServerRequest) { Status = AIROE, IsConnected = true };
                }
                catch (Exception e)
                {
                    throw new Exception($"Function MT4 ServerAPIAsync Error MSG : {e.Message}");
                }
            }
            public async Task<ServerResult<ClrWrapper>> ApiGetGroupsAsync(ServerResult<ClrWrapper> ServerRequest)
            {
                try
                {
                    string pattern = @"remove|manager|coverage|system";
                    var groups = ServerRequest.Status.GroupsRequest().ToList();
                    ServerRequest.ServerGroup = String.Join(",", groups.Where(s => s.Enable != 0).Where(s => !Regex.IsMatch(s.Name, pattern, RegexOptions.IgnoreCase)).Select(s => s.Name));
                    return ServerRequest;
                }
                catch (Exception e)
                {
                    throw new Exception($"Function MT4 GetGroupsAsync Error MSG : {e.Message}");
                }
            }
            public ServerResult<ClrWrapper> CloseConnection(ServerResult<ClrWrapper> ServerRequest)
            {
                ServerRequest.Status.Disconnect();
                ServerRequest.IsConnected = false;
                return ServerRequest;
            }
            public async Task<ServerBase> DBGetGroupsAsync(ServerBase request)
                => throw new NotImplementedException("MT4 暫不支援 DB 撈取");
            public ServerResult<ClrWrapper> CreateAccount(ServerResult<ClrWrapper> ServerRequest, int volumne, string group, int leverage, string comment)
            { 
                try
                {
                    var user = new UserRecord
                    {
                        Name = "Pressure Lab demo account",
                        Status = "RE",
                        Country = "Taiwan",
                        Email = "water@aaa.com",
                        Group = group,
                        Leverage = leverage,
                        Comment = comment,
                        //EnableChangePassword = 1,
                        //SendReports = 1,
                        Enable = 1,
                    };

                    for (int i = 0;i < volumne; i++)
                    {
                        ServerRequest.initLogin += 1;
                        user.Login = ServerRequest.initLogin;
                        var isSuccess = ServerRequest.Status.UserRecordNew(user);

                        if (isSuccess == 0)
                        {
                            ServerRequest.AddResult(ServerRequest.initLogin, OperationType.CreateAccount, true, comment);
                        }
                        else
                        {
                            ServerRequest.AddResult(ServerRequest.initLogin, OperationType.CreateAccount, false, comment);
                        }
                    }
                    return ServerRequest;
                } catch(Exception e)
                {
                    throw new Exception($"Function MT4 CreateAccount Error Message : {e.Message}");
                }
            }

            public ServerResult<ClrWrapper> Deposit(ServerResult<ClrWrapper> ServerRequest, double balance, string comment)
            {
                try
                {
                    var successLogin = ServerRequest.LoginResults
                        .Where(t => t.Value.Any(r => r.Operation == OperationType.CreateAccount && r.IsSuccess))
                        .Select(t => t.Key).ToList();

                    foreach (var Login in successLogin)
                    {
                        var trans = new TradeTransInfo
                        {
                            Type = TradeTransactionType.BrBalance,
                            Cmd = TradeCommand.Balance,
                            OrderBy = (int)Login,
                            Comment = comment,
                            Price = balance
                        };
                        int isSuccess = ServerRequest.Status.TradeTransaction(trans);
                        if (isSuccess == 0)
                            ServerRequest.AddResult(Login, OperationType.Deposit, true, comment);
                        else
                            ServerRequest.AddResult(Login, OperationType.Deposit, false, comment);
                    }
                    return ServerRequest;
                } catch(Exception e)
                {
                    throw new Exception($"Function MT4 Deposit Error MSG : {e.Message}");
                }
            }

             public Respect_Red_Result get_mt_journal(ServerResult<ClrWrapper> ServerRequest,string cpu_mode ,string filter_str, DateTime action_time)
            {
                Respect_Red_Result respect_Red_Result = new Respect_Red_Result();
                //取的現在的時間 
                var stop_time = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).DateTime;

                string[] formats = { "yyyy.MM.dd HH:mm", "yyyy.MM.dd" };

                if (cpu_mode == "cpu_value")
                {
                    try
                    {
                        int filter_int = Convert.ToInt32(filter_str);

                        // 時間設定
                        var endUtc = DateTimeOffset.UtcNow.AddHours(3);
                        var startUtc = endUtc.AddHours(-filter_int);

                        var journal_log = ServerRequest.Status.JournalRequest(
                            0,
                            (uint)startUtc.ToUnixTimeSeconds(),
                            (uint)endUtc.ToUnixTimeSeconds(),
                            "memory&connection&!journal"
                        );
                        var filtered = journal_log
                            .Where(log => !log.Message.Contains("----"))
                            .ToList();

                        // Regex
                        var memoryRegex = new Regex(@"free memory:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                        var cpuRegex = new Regex(@"cpu:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

                        DateTime start = startUtc.UtcDateTime;
                        DateTime end = endUtc.UtcDateTime;

                        var matched = filtered
                            .Select(log =>
                            {
                                if (!DateTime.TryParseExact(
                                        log.Time,
                                        "yyyy.MM.dd HH:mm:ss.fff",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out var dt))
                                {
                                    Debug.WriteLine($"[SKIP] 時間格式解析失敗: '{log.Time}'");
                                    return null;
                                }

                                var utcTime = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

                                Debug.WriteLine($"[TIME] 原始='{log.Time}' | 解析={dt:O} | UTC={utcTime:O}");

                                var memoryMatch = memoryRegex.Match(log.Message);
                                var cpuMatch = cpuRegex.Match(log.Message);

                                if (!memoryMatch.Success || !cpuMatch.Success)
                                {
                                    Debug.WriteLine($"[SKIP] Regex 未命中 | Memory={memoryMatch.Success} CPU={cpuMatch.Success} | Message='{log.Message}'");
                                    return null;
                                }
                                return new
                                {
                                    Time = utcTime,
                                    Memory = int.Parse(memoryMatch.Groups[1].Value),
                                    Cpu = int.Parse(cpuMatch.Groups[1].Value)
                                };
                            })
                            .Where(x => x != null && x.Time >= start && x.Time <= end)
                            .ToList();

                        if (!matched.Any())
                        {
                            Debug.WriteLine("該時間區間內沒有符合的資料");
                            return respect_Red_Result;
                        }

                        // 👉 共用方法：取 Max/Min + 時間
                        (int value, DateTime time) GetMax<T>(IEnumerable<T> data, Func<T, int> selector, Func<T, DateTime> timeSelector)
                        {
                            var maxItem = data.OrderByDescending(selector).First();
                            return (selector(maxItem), timeSelector(maxItem));
                        }

                        (int value, DateTime time) GetMin<T>(IEnumerable<T> data, Func<T, int> selector, Func<T, DateTime> timeSelector)
                        {
                            var minItem = data.OrderBy(selector).First();
                            return (selector(minItem), timeSelector(minItem));
                        }

                        var startTime = matched.Min(x => x.Time);
                        var endTime = matched.Max(x => x.Time);

                        // CPU
                        var (cpuMax, cpuMaxTime) = GetMax(matched, x => x.Cpu, x => x.Time);
                        var (cpuMin, cpuMinTime) = GetMin(matched, x => x.Cpu, x => x.Time);
                        var cpuAvg = matched.Average(x => x.Cpu);

                        // Memory
                        var (memMax, memMaxTime) = GetMax(matched, x => x.Memory, x => x.Time);
                        var (memMin, memMinTime) = GetMin(matched, x => x.Memory, x => x.Time);
                        var memAvg = matched.Average(x => x.Memory);

                        // 寫入結果
                        respect_Red_Result.Server_Start_Time = startTime.ToString("yyyy-MM-dd HH:mm:ss");
                        respect_Red_Result.Server_End_Time = endTime.ToString("yyyy-MM-dd HH:mm:ss");

                        respect_Red_Result.Cpu_Max = cpuMax.ToString();
                        respect_Red_Result.Cpu_Average = cpuAvg.ToString("F2");
                        respect_Red_Result.Cpu_Max_Time = cpuMaxTime.ToString("yyyy-MM-dd HH:mm:ss");

                        respect_Red_Result.Memory_Max = memMax.ToString();
                        respect_Red_Result.Memory_Average = memAvg.ToString("F2");
                        respect_Red_Result.Memory_Max_Time = memMaxTime.ToString("yyyy-MM-dd HH:mm:ss");

                        respect_Red_Result.IOPS_Max = "0";
                        respect_Red_Result.IOPS_Average = "0";
                        respect_Red_Result.IOPS_Max_Time = "0";
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Function MT4 Deposit Error MSG : {e.Message}");
                    }
                }
                else
                {
                    try
                    {
                        // 取得現在 UTC 時間
                        var utcNow = DateTimeOffset.UtcNow;

                        // 建立 GMT+3 時區
                        var timeGmt3 = utcNow.ToOffset(TimeSpan.FromHours(3));

                        DateTimeOffset start_time, end_time;
                        DateTimeOffset start_replace, end_replace;

                        // 判斷星期一/平日
                        if (timeGmt3.DayOfWeek == DayOfWeek.Monday)
                        {

                            // 週一 → 往前 3 天（保留時間）
                            start_time = timeGmt3.AddDays(-3);
                            end_time = timeGmt3;

                        }
                        else
                        {
                            // 週一 → 往前 3 天（保留時間）
                            start_time = timeGmt3.AddDays(-1);
                            end_time = timeGmt3;
                        }

                        // 週一 → 往前抓 3 天（週五 00:00 GMT+3）
                        start_replace = new DateTimeOffset(start_time.AddDays(-3).Year,
                                                        start_time.AddDays(-3).Month,
                                                        start_time.AddDays(-3).Day,
                                                        0, 0, 0,
                                                        TimeSpan.FromHours(3));

                        end_replace = new DateTimeOffset(end_time.Year,
                                                      end_time.Month,
                                                      end_time.Day,
                                                      23, 59, 59,
                                                      TimeSpan.FromHours(3));


                        string startStr = start_replace.ToString("yyyy.MM.dd HH:mm");
                        string endStr = end_replace.ToString("yyyy.MM.dd HH:mm");

                        double timestamp_start = (DateTime.ParseExact(startStr, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;
                        double timestamp_end = (DateTime.ParseExact(endStr, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;

                        var journal_log = ServerRequest.Status.JournalRequest(
                            0,
                            (uint)timestamp_start,
                            (uint)timestamp_end,
                            "memory&connection&!journal"
                        );


                        var filtered = journal_log
                            .Where(log => !log.Message.Contains("----"))
                            .ToList();

                        // Regex
                        var memoryRegex = new Regex(@"free memory:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                        var cpuRegex = new Regex(@"cpu:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

                        DateTime start = start_time.UtcDateTime;
                        DateTime end = end_time.UtcDateTime;

                        var matched = filtered
                            .Select(log =>
                            {
                                if (!DateTime.TryParseExact(
                                        log.Time,
                                        "yyyy.MM.dd HH:mm:ss.fff",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out var dt))
                                {
                                    Debug.WriteLine($"[SKIP] 時間格式解析失敗: '{log.Time}'");
                                    return null;
                                }

                                var utcTime = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

                                var memoryMatch = memoryRegex.Match(log.Message);
                                var cpuMatch = cpuRegex.Match(log.Message);

                                if (!memoryMatch.Success || !cpuMatch.Success)
                                {
                                    return null;
                                }

                                return new
                                {
                                    Time = utcTime,
                                    Memory = int.Parse(memoryMatch.Groups[1].Value),
                                    Cpu = int.Parse(cpuMatch.Groups[1].Value)
                                };
                            })
                            .Where(x => x != null && x.Time >= start && x.Time <= end)
                            .ToList();
                        if (!matched.Any())
                        {
                            return respect_Red_Result;
                        }

                        // 👉 共用方法：取 Max/Min + 時間
                        (int value, DateTime time) GetMax<T>(IEnumerable<T> data, Func<T, int> selector, Func<T, DateTime> timeSelector)
                        {
                            var maxItem = data.OrderByDescending(selector).First();
                            return (selector(maxItem), timeSelector(maxItem));
                        }

                        (int value, DateTime time) GetMin<T>(IEnumerable<T> data, Func<T, int> selector, Func<T, DateTime> timeSelector)
                        {
                            var minItem = data.OrderBy(selector).First();
                            return (selector(minItem), timeSelector(minItem));
                        }

                        // 取 CPU 前五名平均
                        double cpuAvgTop5 = matched
                            .OrderByDescending(x => x.Cpu)
                            .Take(5)
                            .Select(x => x.Cpu)
                            .DefaultIfEmpty(0)
                            .Average();

                        // 取 Memory 前五名平均
                        double memoryAvgTop5 = matched
                            .OrderByDescending(x => x.Memory)
                            .Take(5)
                            .Select(x => x.Memory)
                            .DefaultIfEmpty(0)
                            .Average();

                        var startTime = matched.Min(x => x.Time);
                        var endTime = matched.Max(x => x.Time);

                        // CPU
                        var (cpuMax, cpuMaxTime) = GetMax(matched, x => x.Cpu, x => x.Time);
                        var (cpuMin, cpuMinTime) = GetMin(matched, x => x.Cpu, x => x.Time);
                        var cpuAvg = matched.Average(x => x.Cpu);

                        // Memory
                        var (memMax, memMaxTime) = GetMax(matched, x => x.Memory, x => x.Time);
                        var (memMin, memMinTime) = GetMin(matched, x => x.Memory, x => x.Time);
                        var memAvg = matched.Average(x => x.Memory);

                        // 寫入結果
                        respect_Red_Result.Server_Start_Time = startTime.ToString("yyyy-MM-dd HH:mm:ss");
                        respect_Red_Result.Server_End_Time = endTime.ToString("yyyy-MM-dd HH:mm:ss");

                        respect_Red_Result.Cpu_Max = cpuAvgTop5.ToString("F2");
                        //respect_Red_Result.Cpu_Average = cpuAvgTop5.ToString("F2");
                        respect_Red_Result.Cpu_Max_Time = cpuMaxTime.ToString("yyyy-MM-dd HH:mm:ss");

                        respect_Red_Result.Memory_Max = memoryAvgTop5.ToString("F2");
                        //respect_Red_Result.Memory_Average = memoryAvgTop5.ToString("F2");
                        respect_Red_Result.Memory_Max_Time = memMaxTime.ToString("yyyy-MM-dd HH:mm:ss");

                        respect_Red_Result.IOPS_Max = "0";
                        respect_Red_Result.IOPS_Average = "0";
                        respect_Red_Result.IOPS_Max_Time = "0";
                     
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Function MT4 Deposit Error MSG : {e.Message}");
                    }
                }

                ////test data
                //action_time = new DateTime(2026, 3, 31, 0, 0, 0);
                //stop_time = new DateTime(2026, 4, 2, 23, 59, 0);


                string actionStr = action_time.ToString("yyyy.MM.dd 00:00");
                string stopStr = stop_time.ToString("yyyy.MM.dd 00:00");


                double timestamp_action = (DateTime.ParseExact(actionStr, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;
                double timestamp_stop = (DateTime.ParseExact(stopStr, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;



                var journal_request = ServerRequest.Status.JournalRequest(
                    0,
                    (uint)timestamp_action,
                    (uint)timestamp_stop,
                    "request from"
                );


                var journal_request_filter = journal_request
                    .Where(log => !log.Message.Contains("----"))
                    .ToList();


                var result = journal_request_filter
                  .Where(log => DateTime.TryParseExact(
                      log.Time,
                      "yyyy.MM.dd HH:mm:ss.fff",
                      CultureInfo.InvariantCulture,
                      DateTimeStyles.None,
                      out _))
                  .Select(log =>
                  {
                      var dt = DateTime.ParseExact(
                          log.Time,
                          "yyyy.MM.dd HH:mm:ss.fff",
                          CultureInfo.InvariantCulture);
                      return dt; // ✅ 原本時間 그대로
                })
                  .Where(t => t >= action_time && t <= stop_time)
                  .ToList();


                var perMinute = result
                .GroupBy(t => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0))
                .Select(g => new
                {
                    Time = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Time)
                .ToList();


                //找到最高的count
                var maxItem = perMinute.OrderByDescending(x => x.Count).First();
               int maxCount = maxItem != null ? maxItem.Count : 0;
                DateTime maxTime = maxItem != null ? maxItem.Time : DateTime.MinValue;
                // 2️⃣ 計算平均 Count
                double avgCount = perMinute.Count > 0 ? perMinute.Average(x => x.Count) : 0;

                respect_Red_Result.Request_Max = maxCount.ToString();
                respect_Red_Result.Request_Max_time = maxTime.ToString();
                respect_Red_Result.Request_Max_Average = Math.Round(avgCount, 2).ToString();


                //out file
                return respect_Red_Result;
                }

            public ServerResult<ClrWrapper> GetSymbol(ServerResult<ClrWrapper> request)
            {
                try
                {
                    var symbols = request.Status.CfgRequestSymbol();
                    request.ServerSymbol = String.Join(',', symbols.Select(s => s.Name));
                    return request;
                } catch (Exception e)
                {
                    throw new Exception($"Function GetSymbol Error : {e.Message}");
                }
            }

            public async Task<ServerResult<ClrWrapper>> OpenTrade(ServerResult<ClrWrapper> request, StressTestRequest info)
            {
                var validLogins = request.LoginResults
                    .Where(kv => kv.Key.HasValue)
                    .Where(kv =>
                        kv.Value.Any(r => r.Operation == OperationType.CreateAccount && r.IsSuccess) &&
                        kv.Value.Any(r => r.Operation == OperationType.Deposit && r.IsSuccess)
                    ).Select(kv => kv.Key.Value).ToList();

                if (!validLogins.Any())
                {
                    Console.WriteLine("沒有任何帳號符合開單資格。");
                    return request;
                }

                int timetmp = 0;
                uint fromT;
                uint dateNow;
                double executionPrice;
                uint timesign;

                var tasks = validLogins.Select(loginId => Task.Run(() =>
                {
                    for (int i = 0; i < info.ORDER__VOLUME; i++)
                    {
                        timetmp = request.Status.ServerTime();
                        fromT = (uint)(timetmp - 3600);
                        dateNow = (uint)timetmp;

                        executionPrice = info.ORDER__PRICE;
                        //var tickRequest = new TickRequest 
                        //{
                        //    Symbol = info.ORDER_SYMBOL,
                        //    From = fromT,
                        //    To = dateNow,
                        //    Flags = ' '
                        //};
                        //var tickData = request.Status.TicksRequest(tickRequest);
                        timesign = 0;
                        var chartRequest = new ChartInfo
                        {
                            Symbol = info.ORDER_SYMBOL,
                            Period = ChartPeriod.M1,
                            Start = fromT,
                            End = dateNow,
                            Mode = ChartRequestMode.RangeIn
                        };
                        var chartData = request.Status.ChartRequest(chartRequest, out timesign);
                        var getALL = request.Status.CfgRequestSymbol();
                        var getSymbolDigits = getALL.FirstOrDefault(s => s.Name == info.ORDER_SYMBOL);
                        var multiplier = Math.Pow(10, getSymbolDigits.Digits);
                        if (executionPrice == 0)
                        {
                            executionPrice = chartData.Last().Open / Math.Pow(10, getSymbolDigits.Digits);
                        }

                        if (info.ORDER_MODEL_TYPE == "b")
                        {
                            info.ORDER_TP_VALUE = chartData.Last().Open / Math.Pow(10, getSymbolDigits.Digits) + (1 + (info.ORDER_TP_VALUE / 100.0));
                            info.ORDER_SL_VALUE = chartData.Last().Open / Math.Pow(10, getSymbolDigits.Digits) - (1 + (info.ORDER_SL_VALUE / 100.0));
                        }
                        var trade_info = new TradeTransInfo()
                        {
                            Type = TradeTransactionType.BrOrderOpen,
                            Cmd = TradeCommand.Buy,
                            Symbol = info.ORDER_SYMBOL,
                            Volume = (int)(info.ORDER__LOTS * 100),
                            Price = executionPrice,
                            OrderBy = (int)loginId,
                            Comment = info.Comment
                        };

                        if (info.ORDER_CHECK_TP_SL)
                        {
                            trade_info.Tp = Math.Floor(info.ORDER_TP_VALUE * multiplier) / multiplier;
                            trade_info.Sl = Math.Floor(info.ORDER_SL_VALUE * multiplier) / multiplier;
                        }
                        int res = request.Status.TradeTransaction(trade_info);
                        bool isSuccess = (res == 0);

                        string priceMode = info.ORDER__PRICE == 0 ? "Market" : "Limit";
                        request.AddResult(loginId, OperationType.OpenTrade, isSuccess,
                            $"[{priceMode}] Seq:{i + 1}, Price:{executionPrice}, RetCode:{res}");
                        getALL = null;
                    }
                }));

                await Task.WhenAll(tasks);

                return request;
            }

            public bool DeleteAccount(ServerResult<ClrWrapper> serverrequest, long start_acc, long end_acc)
            {
                throw new NotImplementedException();
                //try
                //{
                //    var deleteaccountlist = serverrequest.loginresults
                //        .where(t => t.value.any(r => r.operation == operationtype.createaccount && r.issuccess))
                //        .select(t => t.key).tolist();


                //    return;
                //}
                //catch (exception e)
                //{
                //    throw new exception($"function deleteaccount error msg : {e.message}");
                //}
            }
        }

        public class MT5Service : IMTService<CIMTManagerAPI>
        {
            public async Task<ServerResult<CIMTManagerAPI>> ServerAPIAsync(ServerBase ServerRequest)
            {
                try
                {
                    string _rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    MTRetCode res = MTRetCode.MT_RET_OK;

                    if ((res = SMTManagerAPIFactory.Initialize(_rootPath)) != MTRetCode.MT_RET_OK)
                    {
                        throw new Exception($"Loading manager api failed. ({res})");
                    }

                    var _manager = SMTManagerAPIFactory.CreateManager(SMTManagerAPIFactory.ManagerAPIVersion, out res);
                    if ((res != MTRetCode.MT_RET_OK) || _manager == null)
                    {
                        SMTManagerAPIFactory.Shutdown();
                        string message = string.Format("Creating manager failed ({0})", (res == MTRetCode.MT_RET_OK ? "Manager api is null" : res.ToString()));
                        throw new Exception($"{message}");
                    }
                    res = _manager.Connect(
                        ServerRequest.ServerIp,
                        (ulong)ServerRequest.AdminLogin,
                        ServerRequest.Password,
                        null,
                        CIMTManagerAPI.EnPumpModes.PUMP_MODE_SYMBOLS,
                        60000
                    );
                    if ((res != MTRetCode.MT_RET_OK) || _manager == null)
                    {
                        SMTManagerAPIFactory.Shutdown();
                        string message = string.Format("Creating manager failed ({0})", (res == MTRetCode.MT_RET_OK ? "Manager api is null" : res.ToString()));
                        throw new Exception($"{message}");
                    }
                    return new ServerResult<CIMTManagerAPI>(ServerRequest) { Status = _manager, IsConnected = true };
                }
                catch (Exception e)
                {
                    throw new Exception($"Function MT5 ServerAPIAsync Error MSG : {e.Message}");
                }
            }
            public async Task<ServerResult<CIMTManagerAPI>> ApiGetGroupsAsync(ServerResult<CIMTManagerAPI> ServerRequest)
            {
                try
                {
                    CIMTConGroupArray groups = ServerRequest.Status.GroupCreateArray();
                    var ret = ServerRequest.Status.GroupRequestArray("*", groups);

                    if (ret != MTRetCode.MT_RET_OK) throw new Exception($"RetCode : {ret}");

                    var groupsArray = groups.ToArray();
                    string pattern = @"manager|coverage|remove|system|preliminary|real";
                    ServerRequest.ServerGroup = String.Join(",", groupsArray.Select(s => s.Group()).Where(s => !Regex.IsMatch(s, pattern, RegexOptions.IgnoreCase)));
                    return ServerRequest;
                }catch(Exception e)
                {
                    throw new Exception($"Function MT5 ApiGetGroupsAsync Error MSG : {e.Message}");
                }
            }
            public async Task<ServerBase> DBGetGroupsAsync(ServerBase ServerRequest)
            {
                try
                {
                    using var connection = new MySqlConnection(ServerRequest.DBConnection);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = $@"
                        SELECT `Group`
                        FROM `{ServerRequest.ServerName}`.mt5_groups
                        WHERE PermissionsFlags & 2 != 0;
                    ";
                    var groups = new List<string>();
                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        groups.Add(reader.GetString("Group"));
                    };
                    string pattern = @"manager|coverage|remove|system|preliminary|real";
                    ServerRequest.ServerGroup = string.Join(",", groups.Where(s => !Regex.IsMatch(s, pattern, RegexOptions.IgnoreCase)));
                    return ServerRequest;
                }
                catch (Exception e)
                {
                    throw new Exception($"Function MT5 ServerDBAsync Error MSG : {e.Message}");
                }
            }
            public ServerResult<CIMTManagerAPI> CloseConnection(ServerResult<CIMTManagerAPI> ServerRequest)
            {
                ServerRequest.Status.Disconnect();
                ServerRequest.IsConnected = false;
                return ServerRequest;
            }

            public ServerResult<CIMTManagerAPI> CreateAccount(ServerResult<CIMTManagerAPI> ServerRequest, int volumne, string group, int leverage, string comment)
            {
                try
                {
                    var _acc = ServerRequest.Status.UserCreate();
                    _acc.Group(group);
                    _acc.Name("Pressure Lab Demo Account");
                    _acc.Leverage((uint)leverage);
                    _acc.Country("TW");
                    _acc.Status("RE");
                    _acc.EMail("water@aaa.com");
                    _acc.Comment(comment);
                    _acc.Rights((CIMTUser.EnUsersRights)2403);

                    for (int i = 0; i < volumne; i++)
                    {
                        ServerRequest.initLogin += 1;
                        _acc.Login((ulong)ServerRequest.initLogin);
                        var result = ServerRequest.Status.UserAdd(_acc, "Water13!", "Water13!");

                        if (result == MTRetCode.MT_RET_OK)
                        {
                            ServerRequest.AddResult(ServerRequest.initLogin, OperationType.CreateAccount, true, comment);
                        }
                        else
                        {
                            ServerRequest.AddResult(ServerRequest.initLogin, OperationType.CreateAccount, false, comment);
                        }
                    }
                    return ServerRequest;
                }
                catch (Exception e)
                {
                    throw new Exception($"Function MT5 CreateAccount error MSG : {e.Message}");
                }
            }

            public ServerResult<CIMTManagerAPI> Deposit(ServerResult<CIMTManagerAPI> ServerRequest, double balance, string comment)
            {
                try
                {
                    var successLogins = ServerRequest.LoginResults
                        .Where(kv => kv.Value.Any(r =>
                        r.Operation == OperationType.CreateAccount && r.IsSuccess))
                        .Select(kv => kv.Key)
                        .ToList();

                    foreach (var login in successLogins)
                    {
                        var isSuccess = ServerRequest.Status.DealerBalance((ulong)login, balance, 2, comment, out ulong depositId);

                        if (isSuccess == MTRetCode.MT_RET_REQUEST_DONE)
                            ServerRequest.AddResult(login, OperationType.Deposit, true, comment);
                        else
                            ServerRequest.AddResult(login, OperationType.Deposit, false, comment);
                    }
                    return ServerRequest;
                }
                catch (Exception e)
                {
                    throw new Exception($"Function MT5 Deposit Error MSG : {e.Message}");
                }
            }

            public Respect_Red_Result get_mt_journal(ServerResult<CIMTManagerAPI> ServerRequest, string cpu_mode , string filter_str, DateTime action_time)
            {
                Respect_Red_Result respect_Red_Result = new Respect_Red_Result();

                //取的現在的時間 
                var stop_time = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).DateTime;

                string[] formats = { "yyyy.MM.dd HH:mm", "yyyy.MM.dd" };

                DateTime? c_start_time = null, c_end_time = null, c_max_time = null;
                int c_sum = 0, c_count = 0, c_max = int.MinValue, c_min = int.MaxValue;

                MTRetCode code = new MTRetCode();


                if (cpu_mode == "cpu_value")
                {
                    try
                    {
                        int filter_int = Convert.ToInt32(filter_str);

                        var endUtc = DateTimeOffset.UtcNow.AddHours(3);
                        var startUtc = endUtc.AddHours(-filter_int);

                        long start = startUtc.ToUnixTimeSeconds();
                        long end = endUtc.ToUnixTimeSeconds();

                        // ================= CPU =================
                        var journal_cpu_log = ServerRequest.Status.LoggerServerRequest(
                            EnMTLogRequestMode.MTLogModeStd,
                            EnMTLogType.MTLogTypeAll,
                            (uint)start,
                            (uint)end,
                            "cpu & !journal",
                            out code
                        );


                        foreach (var item in journal_cpu_log)
                        {
                            var t = DateTimeOffset.FromUnixTimeMilliseconds(item.datetime_msc).UtcDateTime;
                            if (t < startUtc.UtcDateTime || t > endUtc.UtcDateTime) continue;

                            var match = Regex.Match(item.message, @"cpu\s*:\s*(\d+)", RegexOptions.IgnoreCase);
                            if (!match.Success) continue;

                            int val = int.Parse(match.Groups[1].Value);

                            if (c_start_time == null || t < c_start_time) c_start_time = t;
                            if (c_end_time == null || t > c_end_time) c_end_time = t;

                            c_sum += val;
                            c_count++;

                            if (val > c_max)
                            {
                                c_max = val;
                                c_max_time = t;
                            }

                            if (val < c_min) c_min = val;
                        }

                        double c_avg = c_count > 0 ? (double)c_sum / c_count : 0;

                        // ================= Memory =================
                        var journal_memory_log = ServerRequest.Status.LoggerServerRequest(
                            EnMTLogRequestMode.MTLogModeStd,
                            EnMTLogType.MTLogTypeAll,
                            (uint)start,
                            (uint)end,
                            "memory & !journal",
                            out MTRetCode memory_code
                        );

                        DateTime? m_start_time = null, m_end_time = null, m_max_time = null;
                        int m_sum = 0, m_count = 0, m_max = int.MinValue, m_min = int.MaxValue;

                        foreach (var item in journal_memory_log)
                        {
                            var t = DateTimeOffset.FromUnixTimeMilliseconds(item.datetime_msc).UtcDateTime;
                            if (t < startUtc.UtcDateTime || t > endUtc.UtcDateTime) continue;

                            var match = Regex.Match(item.message, @"memory\s*available:\s*(\d+)", RegexOptions.IgnoreCase);
                            if (!match.Success) continue;

                            int val = int.Parse(match.Groups[1].Value);

                            if (m_start_time == null || t < m_start_time) m_start_time = t;
                            if (m_end_time == null || t > m_end_time) m_end_time = t;

                            m_sum += val;
                            m_count++;

                            if (val > m_max)
                            {
                                m_max = val;
                                m_max_time = t;
                            }

                            if (val < m_min) m_min = val;
                        }

                        double m_avg = m_count > 0 ? (double)m_sum / m_count : 0;

                        // ================= IOPS =================
                        DateTime? i_start_time = null, i_end_time = null, i_max_time = null;
                        int i_sum = 0, i_count = 0, i_max = int.MinValue, i_min = int.MaxValue;

                        foreach (var item in journal_cpu_log)
                        {
                            var t = DateTimeOffset.FromUnixTimeMilliseconds(item.datetime_msc).UtcDateTime;
                            if (t < startUtc.UtcDateTime || t > endUtc.UtcDateTime) continue;

                            var match = Regex.Match(item.message, @"iops:\s*(\d+)", RegexOptions.IgnoreCase);
                            if (!match.Success) continue;

                            int val = int.Parse(match.Groups[1].Value);

                            if (i_start_time == null || t < i_start_time) i_start_time = t;
                            if (i_end_time == null || t > i_end_time) i_end_time = t;

                            i_sum += val;
                            i_count++;

                            if (val > i_max)
                            {
                                i_max = val;
                                i_max_time = t;
                            }

                            if (val < i_min) i_min = val;
                        }

                        double i_avg = i_count > 0 ? (double)i_sum / i_count : 0;

                        // ================= Debug（保留但集中） =================
                        Debug.WriteLine($"[CPU] count={c_count}, avg={c_avg:F2}, max={c_max}, min={c_min}");
                        Debug.WriteLine($"[Memory] count={m_count}, avg={m_avg:F2}, max={m_max}, min={m_min}");
                        Debug.WriteLine($"[IOPS] count={i_count}, avg={i_avg:F2}, max={i_max}, min={i_min}");

                        // ================= 寫入 =================
                        respect_Red_Result.Server_Start_Time = m_start_time?.ToString();
                        respect_Red_Result.Server_End_Time = m_end_time?.ToString();

                        respect_Red_Result.Cpu_Max = c_max.ToString();
                        respect_Red_Result.Cpu_Max_Time = c_max_time?.ToString();
                        respect_Red_Result.Cpu_Average = c_avg.ToString();

                        respect_Red_Result.Memory_Max = m_max.ToString();
                        respect_Red_Result.Memory_Max_Time = m_max_time?.ToString();
                        respect_Red_Result.Memory_Average = m_avg.ToString();

                        respect_Red_Result.IOPS_Max = i_max.ToString();
                        respect_Red_Result.IOPS_Max_Time = i_max_time?.ToString();
                        respect_Red_Result.IOPS_Average = i_avg.ToString();

                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Function MT5 Deposit Error MSG : {e.Message}");
                    }
                }
                else
                {

                    try
                    {
                        // 取得現在 UTC 時間
                        var utcNow = DateTimeOffset.UtcNow;

                        // 建立 GMT+3 時區
                        var timeGmt3 = utcNow.ToOffset(TimeSpan.FromHours(3));


                        DateTimeOffset start_time, end_time;
                        DateTimeOffset start_replace, end_replace;

                        // 判斷星期一/平日
                        if (timeGmt3.DayOfWeek == DayOfWeek.Monday)
                        {

                            // 週一 → 往前 3 天（保留時間）
                            start_time = timeGmt3.AddDays(-3);
                            end_time = timeGmt3;
                           
                        }
                        else
                        {
                            // 週一 → 往前 3 天（保留時間）
                            start_time = timeGmt3.AddDays(-1);
                            end_time = timeGmt3;
                        }

                        // 週一 → 往前抓 3 天（週五 00:00 GMT+3）
                        start_replace = new DateTimeOffset(start_time.AddDays(-3).Year,
                                                        start_time.AddDays(-3).Month,
                                                        start_time.AddDays(-3).Day,
                                                        0, 0, 0,
                                                        TimeSpan.FromHours(3));

                        end_replace = new DateTimeOffset(end_time.Year,
                                                      end_time.Month,
                                                      end_time.Day,
                                                      23, 59, 59,
                                                      TimeSpan.FromHours(3));


                        string startStr = start_replace.ToString("yyyy.MM.dd HH:mm");
                        string endStr = end_replace.ToString("yyyy.MM.dd HH:mm");

                        double timestamp_start = (DateTime.ParseExact(startStr, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;
                        double timestamp_end = (DateTime.ParseExact(endStr, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;


                        // ================= CPU =================
                        var journal_cpu_log = ServerRequest.Status.LoggerServerRequest(
                            EnMTLogRequestMode.MTLogModeStd,
                            EnMTLogType.MTLogTypeAll,
                            (uint)timestamp_start,
                            (uint)timestamp_end,
                            "cpu & !journal",
                            out code
                        );


                        var cpuValues = new List<(DateTime Time, int Value)>();

                        foreach (var item in journal_cpu_log)
                        {
                            var t = DateTimeOffset.FromUnixTimeMilliseconds(item.datetime_msc).UtcDateTime;
                            if (t < start_time.UtcDateTime || t > end_time.UtcDateTime) continue;

                            var match = Regex.Match(item.message, @"cpu\s*:\s*(\d+)", RegexOptions.IgnoreCase);
                            if (!match.Success) continue;

                            int val = int.Parse(match.Groups[1].Value);

                            cpuValues.Add((t, val));

                            if (c_start_time == null || t < c_start_time) c_start_time = t;
                            if (c_end_time == null || t > c_end_time) c_end_time = t;

                            if (val > c_max)
                            {
                                c_max = val;
                                c_max_time = t;
                            }

                            if (val < c_min) c_min = val;
                        }

                        // 取最高前五名計算平均
                        double c_avgTop5 = cpuValues
                            .OrderByDescending(x => x.Value)
                            .Take(5)
                            .Select(x => x.Value)
                            .DefaultIfEmpty(0)
                            .Average();

                        Console.WriteLine($"最高前五名平均 CPU: {c_avgTop5:F2}");

                        double c_avg = c_count > 0 ? (double)c_sum / c_count : 0;


                        // ================= Memory =================
                        var journal_memory_log = ServerRequest.Status.LoggerServerRequest(
                            EnMTLogRequestMode.MTLogModeStd,
                            EnMTLogType.MTLogTypeAll,
                           (uint)timestamp_start,
                           (uint)timestamp_end,
                            "memory & !journal",
                            out MTRetCode memory_code
                        );

                        DateTime? m_start_time = null, m_end_time = null, m_max_time = null;
                        int m_sum = 0, m_count = 0, m_max = int.MinValue, m_min = int.MaxValue;


                        var memoryValues = new List<(DateTime Time, int Value)>();


                        foreach (var item in journal_memory_log)
                        {
                            var t = DateTimeOffset.FromUnixTimeMilliseconds(item.datetime_msc).UtcDateTime;
                            if (t < start_time.UtcDateTime || t > end_time.UtcDateTime) continue;

                            var match = Regex.Match(item.message, @"memory\s*available:\s*(\d+)", RegexOptions.IgnoreCase);
                            if (!match.Success) continue;

                            int val = int.Parse(match.Groups[1].Value);

                            if (m_start_time == null || t < m_start_time) m_start_time = t;
                            if (m_end_time == null || t > m_end_time) m_end_time = t;

                            m_sum += val;
                            m_count++;

                            if (val > m_max)
                            {
                                m_max = val;
                                m_max_time = t;
                            }

                            if (val < m_min) m_min = val;
                        }

                        // 取最高前五名計算平均
                        double m_Top5 = memoryValues
                            .OrderByDescending(x => x.Value)
                            .Take(5)
                            .Select(x => x.Value)
                            .DefaultIfEmpty(0)
                            .Average();

                        Console.WriteLine($"最高前五名平均 CPU: {m_Top5:F2}");

                        double m_avg = m_count > 0 ? (double)m_sum / m_count : 0;

                        // ================= IOPS =================
                        DateTime? i_start_time = null, i_end_time = null, i_max_time = null;
                        int i_sum = 0, i_count = 0, i_max = int.MinValue, i_min = int.MaxValue;




                        var iopsValues = new List<(DateTime Time, int Value)>();

                        foreach (var item in journal_cpu_log)
                        {
                            var t = DateTimeOffset.FromUnixTimeMilliseconds(item.datetime_msc).UtcDateTime;
                            if (t < start_time.UtcDateTime || t > end_time.UtcDateTime) continue;

                            var match = Regex.Match(item.message, @"iops:\s*(\d+)", RegexOptions.IgnoreCase);
                            if (!match.Success) continue;

                            int val = int.Parse(match.Groups[1].Value);

                            if (i_start_time == null || t < i_start_time) i_start_time = t;
                            if (i_end_time == null || t > i_end_time) i_end_time = t;

                            i_sum += val;
                            i_count++;

                            if (val > i_max)
                            {
                                i_max = val;
                                i_max_time = t;
                            }

                            if (val < i_min) i_min = val;
                        }

                        // 取最高前五名計算平均
                        double i_Top5 = iopsValues
                            .OrderByDescending(x => x.Value)
                            .Take(5)
                            .Select(x => x.Value)
                            .DefaultIfEmpty(0)
                            .Average();

                        Console.WriteLine($"最高前五名平均 CPU: {i_Top5:F2}");

                        double i_avg = i_count > 0 ? (double)i_sum / i_count : 0;

                        // ================= Debug（保留但集中） =================
                        Debug.WriteLine($"[CPU] count={c_count}, avg={c_avg:F2}, max={c_max}, min={c_min}");
                        Debug.WriteLine($"[Memory] count={m_count}, avg={m_avg:F2}, max={m_max}, min={m_min}");
                        Debug.WriteLine($"[IOPS] count={i_count}, avg={i_avg:F2}, max={i_max}, min={i_min}");

                        // ================= 寫入 =================
                        respect_Red_Result.Server_Start_Time = m_start_time?.ToString();
                        respect_Red_Result.Server_End_Time = m_end_time?.ToString();

                        respect_Red_Result.Cpu_Max = c_avgTop5.ToString();
                        respect_Red_Result.Cpu_Max_Time = c_max_time?.ToString();

                        respect_Red_Result.Memory_Max = m_Top5.ToString();
                        respect_Red_Result.Memory_Max_Time = m_max_time?.ToString();

                        respect_Red_Result.IOPS_Max = i_Top5.ToString();
                        respect_Red_Result.IOPS_Max_Time = i_max_time?.ToString();

                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Function MT5 Deposit Error MSG : {e.Message}");
                    }

                }


                ////test data
                //action_time = new DateTime(2026, 3, 31, 0, 0, 0);
                //stop_time = new DateTime(2026, 4, 2, 23, 59, 0);


                string actionStr = action_time.ToString("yyyy.MM.dd 00:00");
                string stopStr = stop_time.ToString("yyyy.MM.dd 00:00");

                double timestamp_action = (DateTime.ParseExact(actionStr, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;
                double timestamp_stop = (DateTime.ParseExact(stopStr, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1)).TotalSeconds;

                // ================= CPU =================
                var journal_requests_log = ServerRequest.Status.LoggerServerRequest(
                    EnMTLogRequestMode.MTLogModeStd,
                    EnMTLogType.MTLogTypeAll,
                    (uint)timestamp_action,
                    (uint)timestamp_stop,
                    "requests & !journal&ticks",
                    out code
                );



                var journal_requests_log_filter = journal_requests_log
                .Where(log => !log.message.Contains("----"))
                .ToList();


                //// 將 log.datetime(long) 轉成 DateTime
                //var result = journal_requests_log_filter
                //    .Select(log => new DateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(log.datetime).UtcDateTime).DateTime) // 原始 UTC 時間
                //    .Where(t => t >= action_time && t <= stop_time)
                //    .ToList();



                // Regex 取 requests 後面的數值
                var requestsRegex = new Regex(@"requests\s*:\s*(\d+)", RegexOptions.IgnoreCase);
                var logsWithRequests = journal_requests_log_filter
                    .Select(log =>
                    {
                    // 轉時間
                    var logTime = DateTimeOffset.FromUnixTimeSeconds(log.datetime).UtcDateTime;

                    // 過濾時間區間
                    if (logTime < action_time || logTime > stop_time)
                                    return null;

                    // 取 requests 數值
                    var match = requestsRegex.Match(log.message ?? "");
                    if (!match.Success)
                        return null;

                    int requestsValue = int.Parse(match.Groups[1].Value);

                    return new
                    {
                        Time = logTime,
                        Requests = requestsValue
                    };
                })
                .Where(x => x != null)
                .ToList();

                // 找最大值及時間
                var maxItem = logsWithRequests.OrderByDescending(x => x.Requests).FirstOrDefault();
                int maxRequests = maxItem?.Requests ?? 0;
                DateTime maxTime = maxItem?.Time ?? DateTime.MinValue;

                // 計算平均值
                double avgRequests = logsWithRequests.Count > 0
                    ? Math.Round(logsWithRequests.Average(x => x.Requests), 2)
                    : 0;



                respect_Red_Result.Request_Max = maxRequests.ToString();
                respect_Red_Result.Request_Max_time = maxTime.ToString();
                respect_Red_Result.Request_Max_Average = avgRequests.ToString();


                return respect_Red_Result;
            }

            public ServerResult<CIMTManagerAPI> GetSymbol(ServerResult<CIMTManagerAPI> request)
            {
                try
                {
                    var symbolArray = request.Status.SymbolCreateArray();
                    var checkedRequest = request.Status.SymbolRequestArray("*", null, symbolArray);
                    if (checkedRequest != MTRetCode.MT_RET_OK) throw new Exception($"Request Server occure error, error code : {checkedRequest}");
                    var result = symbolArray.ToArray();
                    var symbolName = result.Select(s => s.Symbol());
                    request.ServerSymbol = String.Join(',', symbolName);
                    return request;
                }
                catch (Exception e)
                {
                    throw new Exception($"Function MT5 GetSymbol Error : {e.Message}");
                }
            }

            private class DealerSink : CIMTDealerSink
            {
                public readonly ManualResetEventSlim CallbackEvent = new(false);
                public MTRetCode RetCode { get; private set; }
                public uint RequestId { get; private set; }
                public override void OnDealerAnswer(CIMTRequest request)
                {
                    RetCode = request.ResultRetcode();
                    CallbackEvent.Set();
                }
            }
            public async Task<ServerResult<CIMTManagerAPI>> OpenTrade(ServerResult<CIMTManagerAPI> request, StressTestRequest info)
            {
                var validLogins = request.LoginResults
                    .Where(kv => kv.Key.HasValue)
                    .Where(kv =>
                        kv.Value.Any(r => r.Operation == OperationType.CreateAccount && r.IsSuccess) &&
                        kv.Value.Any(r => r.Operation == OperationType.Deposit && r.IsSuccess)
                    ).Select(kv => kv.Key.Value).ToList();

                if (!validLogins.Any()) return request;
                request.Status.DealerStart();
                
                var tasks = validLogins.Select(loginId => Task.Run(() =>
                {
                    MTRetCode ret = new MTRetCode();
                    //var fromT = new DateTime(1970, 01, 01, 0, 0, 0);
                    var fromT = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
                    var dateNow = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 23, 59, 59);
                    long LongfromT = ((DateTimeOffset)fromT).ToUnixTimeSeconds();
                    long Longdatenow = ((DateTimeOffset)dateNow).ToUnixTimeSeconds();
                    var tickGet = request.Status.TickHistoryRequest(info.ORDER_SYMBOL, LongfromT, Longdatenow, out ret);
                    var lastTick = tickGet[tickGet.Length-1].ask;
                    var sink = new DealerSink();
                    sink.RegisterSink();
                    var mtRequest = request.Status.RequestCreate();
                    
                    if(info.ORDER_MODEL_TYPE == "b")
                    {
                        info.ORDER_TP_VALUE = lastTick + (1 + (info.ORDER_TP_VALUE / 100.0));
                        info.ORDER_SL_VALUE = lastTick - (1 + (info.ORDER_SL_VALUE / 100.0));
                    }
                    string priceMode = info.ORDER__PRICE == 0 ? "Market" : "Limit";

                    for (int i = 0; i < info.ORDER__VOLUME; i++)
                    {

                        double executionPrice = Convert.ToDouble(info.ORDER__PRICE);

                        mtRequest.Clear();
                        mtRequest.Login((ulong)loginId);
                        mtRequest.SourceLogin((ulong)request.AdminLogin);
                        mtRequest.Symbol(info.ORDER_SYMBOL);
                        mtRequest.Volume((ulong)(info.ORDER__LOTS * 10000));
                        mtRequest.PriceOrder(executionPrice);
                        mtRequest.Action(CIMTRequest.EnTradeActions.TA_DEALER_POS_EXECUTE);
                        mtRequest.Type(CIMTOrder.EnOrderType.OP_BUY);
                        mtRequest.Comment(info.OPEN_ORDER_COMMENT);
                        if (info.ORDER_CHECK_TP_SL)
                        {
                            mtRequest.PriceTP(info.ORDER_TP_VALUE);
                            mtRequest.PriceSL(info.ORDER_SL_VALUE);
                        }
                        request.Status.DealerSend(mtRequest, sink, out uint reqId);

                        bool responded = sink.CallbackEvent.Wait(10000);

                        bool isSuccess = responded && sink.RetCode == MTRetCode.MT_RET_REQUEST_DONE;
                        string msg = responded
                            ? $"[{priceMode}] Seq:{i + 1}, ReqID:{reqId}, RetCode:{sink.RetCode}"
                            : $"[{priceMode}] Seq:{i + 1}, ReqID:{reqId}, Timeout";

                        request.AddResult(loginId, OperationType.OpenTrade, isSuccess, msg);
                    }

                    sink.Release();
                }));

                await Task.WhenAll(tasks);

                request.Status.DealerStop();
                return request;
            }
            public bool DeleteAccount(ServerResult<CIMTManagerAPI> request,long start_acc, long end_acc)
            {
                var fromT = new DateTime(1970, 01, 01, 0, 0, 0);
                var dateNow = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 23, 59, 59);
                long LongfromT = ((DateTimeOffset)fromT).ToUnixTimeSeconds();
                long Longdatenow = ((DateTimeOffset)dateNow).ToUnixTimeSeconds();

                ulong[] acc = new ulong[end_acc - start_acc];
                MTRetCode[] ret = new MTRetCode[end_acc - start_acc];
                int i = 0;
                while (true)
                {
                    if (start_acc == end_acc) break;
                    acc[i++] = (ulong)(++start_acc);
                }

                CIMTDealArray getDealArray = request.Status.DealCreateArray();
                CIMTOrderArray getHistoryOrderArray = request.Status.OrderCreateArray();

                MTRetCode checkDeal = request.Status.DealRequestByLogins(acc, LongfromT, Longdatenow, getDealArray);
                MTRetCode checkHistoryOrder = request.Status.HistoryRequestByLogins(acc, LongfromT, Longdatenow, getHistoryOrderArray);

                ulong[] ticketDeal = getDealArray.ToArray().Select(d => d.Deal()).ToArray();
                ulong[] ticketHistory = getHistoryOrderArray.ToArray().Select(h => h.Order()).ToArray();
                MTRetCode[] dealRet = new MTRetCode[getDealArray.Total()];
                MTRetCode[] orderRet = new MTRetCode[getHistoryOrderArray.Total()];

                if (checkHistoryOrder == MTRetCode.MT_RET_OK && ticketHistory.Length > 0)
                {
                    request.Status.OrderDeleteBatch(ticketHistory, dealRet);
                }

                // 刪除 Deals
                if (checkDeal == MTRetCode.MT_RET_OK && ticketDeal.Length > 0)
                {
                    request.Status.DealDeleteBatch(ticketDeal, orderRet);
                }
                
                MTRetCode result = request.Status.UserDeleteBatch(acc, ret);
                return result == MTRetCode.MT_RET_OK;
            }
        }

        public interface IMTExecutor
        {
            Task ConnectAsync();
            Task<ServerBase> GetGroupsAsync();
            ServerBase CreateAccount(int volume, string group, int leverage, string comment);
            ServerBase Deposit(double balance, string comment);
            void Disconnect();
            ServerBase GetResult();
            void ClearResult();
            Respect_Red_Result get_mt_journal(string cpu_mode, string server_hour, DateTime action_time);
            ServerBase GetSymbol();
            Task<DocxReportData> OpenTrade(StressTestRequest request);
            Task<bool> DeleteAccount(long start_acc, long end_acc);
        }

        public class MT4Executor : IMTExecutor
        {
            private readonly ServerBase _config;
            private readonly MT4Service _service;
            private ServerResult<ClrWrapper> _result;

            public MT4Executor(ServerBase config)
            {
                _config = config;
                _service = new MT4Service();
                ClearResult();
            }

            public async Task ConnectAsync()
                => _result = await _service.ServerAPIAsync(_config);

            public async Task<ServerBase> GetGroupsAsync()
            {
                _result = await _service.ApiGetGroupsAsync(_result);
                return _result;
            }

            public ServerBase CreateAccount(int volume, string group, int leverage, string comment)
            {
                _result = _service.CreateAccount(_result, volume, group, leverage, comment);
                return _result;
            }

            public ServerBase Deposit(double balance, string comment)
            {
                _result = _service.Deposit(_result, balance, comment);
                return _result;
            }

            public void Disconnect() => _service.CloseConnection(_result);
            public ServerBase GetResult() => _result;
            public void ClearResult() => _result = new ServerResult<ClrWrapper>(new ServerBase());

            public Respect_Red_Result get_mt_journal(string cpu_mode , string server_hour, DateTime action_time)
            { 
                var test = _service.get_mt_journal(_result, cpu_mode, server_hour, action_time);
                return test;
            }

            public ServerBase GetSymbol()
            {
                _result = _service.GetSymbol(_result);
                return _result;
            }

            public async Task<DocxReportData> OpenTrade(StressTestRequest info)
            {
                _result = await _service.OpenTrade(_result, info);
                return new DocxReportData(info);
            }

            public async Task<bool> DeleteAccount(long start_acc, long end_acc)
            {
                throw new NotImplementedException();
            }
        }

        public class MT5Executor : IMTExecutor
        {
            private readonly ServerBase _config;
            private readonly MT5Service _service;
            private ServerResult<CIMTManagerAPI> _result;

            public MT5Executor(ServerBase config)
            {
                _config = config;
                _service = new MT5Service();
                ClearResult();
            }

            public async Task ConnectAsync()
                => _result = await _service.ServerAPIAsync(_config);

            public async Task<ServerBase> GetGroupsAsync()
                => _result = await _service.ApiGetGroupsAsync(_result);
                //=> await _service.DBGetGroupsAsync(_config);

            public ServerBase CreateAccount(int volume, string group, int leverage, string comment)
            {
                _result = _service.CreateAccount(_result, volume, group, leverage, comment);
                return _result;
            }

            public ServerBase Deposit(double balance, string comment)
            {
                _result = _service.Deposit(_result, balance, comment);
                return _result;
            }

            public void Disconnect() => _service.CloseConnection(_result);
            public ServerBase GetResult() => _result;
            public void ClearResult() => _result = new ServerResult<CIMTManagerAPI>(_config);
            public Respect_Red_Result get_mt_journal(string cpu_mode, string server_hour, DateTime action_time)
            {
                var test = _service.get_mt_journal(_result,cpu_mode, server_hour, action_time);
                return test;
            }

            public ServerBase GetSymbol()
            {
                _result = _service.GetSymbol(_result);
                return _result;
            }
            public async  Task<DocxReportData> OpenTrade(StressTestRequest info)
            {
                _result = await _service.OpenTrade(_result , info);
                return new DocxReportData(info);
            }

            public async Task<bool> DeleteAccount(long start_acc, long end_acc)
            {
                var result = _service.DeleteAccount(_result, start_acc, end_acc);
                return result;
            }
        }
        public interface IMTExecutorFactory
        {
            IMTExecutor Create(string serverName);
            Task<int> InsertLogToDB(ServerBase request, string type, string status);
            Task<List<Object>> GetDBLogAllData();
            Task<(string ServerName, long StartLogin, long EndLogin)> GetDBSpecifyLog(int Id);
            Task<bool> DeleteDBSpecifyLog(int Id);
            Task<bool> UpdateDBLogStatus(int Id, string status);
            void ExecuteLogtoTxt(string log);
            Task<bool> LoginRecord(int id, string serverName, int startLogin, int endLogin);
            Task<int> LoginAsync(string serverName);
            void FillDocx(int Id, DocxReportData data);
            List<string> GetServerNames();
        }

        public class MTExecutorFactory : IMTExecutorFactory
        {
            private readonly IConfiguration _configuration;
            private readonly object _fileLock = new object();
            // 要加伺服器的話在這邊添加就行了
            private static readonly List<ServerBase> AllServerDataset = new List<ServerBase>
            {
                new ServerBase
                {
                    ServerName = "mt4_vt_ny_staging",
                    ServerIp = "3.232.79.252:443",
                    MTType = MTServerType.MT4,
                    AdminLogin = 1234,
                    Password = "1234Test",
                    DBConnection = "Server=live-mt5vtnystg01-reportdb.vi-data.net;Port=3306;Database=mt5_vt_ny_staging;Uid=mt5_report_rw;Pwd=AwKWnn%dgbd3A4D9;Connection Timeout=120;default command timeout=120;",
                },
                new ServerBase
                {
                    ServerName = "mt5_vt_mena_staging",
                    ServerIp = "40.172.39.247:443",
                    MTType = MTServerType.MT5,
                    AdminLogin = null,
                    Password = null,
                    DBConnection = "Server=live-mt5vtnystg01-reportdb.vi-data.net;Port=3306;Database=mt5_vt_ny_staging;Uid=mt5_report_rw;Pwd=AwKWnn%dgbd3A4D9;Connection Timeout=120;default command timeout=120;"
                },
                new ServerBase
                {
                    ServerName = "mt5_vt_ny_staging",
                    ServerIp = "23.22.211.147:443",
                    MTType = MTServerType.MT5,
                    AdminLogin = 1234,
                    Password = "1234Test!",
                    DBConnection = "Server=live-mt5vtnystg01-reportdb.vi-data.net;Port=3306;Database=mt5_vt_ny_staging;Uid=mt5_report_rw;Pwd=AwKWnn%dgbd3A4D9;Connection Timeout=120;default command timeout=120;"
                },
                new ServerBase
                {
                    ServerName = "mt5_vfx_test",
                    ServerIp = "107.21.13.225:443",
                    MTType = MTServerType.MT5,
                    AdminLogin = 1779,
                    Password = "Zd!0QhAc",
                    DBConnection = "Server=test-mt5-reportdb.vi-data.net;Port=3306;Database=mt5_vfx_test;Uid=mt5_report_rw;Pwd=AwKWnn%dgbd3A4D9;Connection Timeout=120;default command timeout=120;",
                    initLogin = 70000001
                },
                new ServerBase
                {
                    ServerName = "mt4_vfx_test",
                    ServerIp = "3.105.241.144:443",
                    MTType = MTServerType.MT4,
                    AdminLogin = 1779,
                    Password = "Zd!0QhAc",
                    DBConnection = "Server=test-mt5-reportdb.vi-data.net;Port=3306;Database=mt5_vfx_test;Uid=mt5_report_rw;Pwd=AwKWnn%dgbd3A4D9;Connection Timeout=120;default command timeout=120;",
                    initLogin = 70000001
                },new ServerBase
                {
                    ServerName = "pe_mt4_vfx_test",
                    ServerIp = "54.204.106.141:443",
                    MTType = MTServerType.MT4,
                    AdminLogin = 1562,
                    Password = "1562TEST",
                    DBConnection = "",
                    initLogin = 70000001
                },
            };

            public List<string> GetServerNames()
                => AllServerDataset.Select(s => s.ServerName).ToList();

            public MTExecutorFactory(IConfiguration configuration)
            {
                _configuration = configuration;
            }

            public IMTExecutor Create(string serverName)
            {
                var config = AllServerDataset
                    .FirstOrDefault(s => s.ServerName == serverName)
                    ?? throw new Exception($"找不到伺服器: {serverName}");

                config.LoginResults = new Dictionary<long?, List<OperationResult>>();

                return config.MTType switch
                {
                    MTServerType.MT4 => new MT4Executor(config),
                    MTServerType.MT5 => new MT5Executor(config),
                    _ => throw new NotSupportedException($"不支援: {config.MTType}")
                };
            }

            public async Task<int> InsertLogToDB(ServerBase request, string type, string status)
            {
                try
                {
                    using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();
                    var timeNow = DateTime.Now.ToString("yyy-MM-dd HH:mm:ss");
                    string sql = @"
                        INSERT INTO admin_tool_config.pressurelab_status 
                            (TYPE, SERVER, STATUS, START_TIME, END_TIME)
                        VALUES 
                            (@Type, @ServerName, @Status, @timeNow, null);
                        SELECT LAST_INSERT_ID();
                    ";

                    using var command = new MySqlCommand(sql, connection);
                    command.Parameters.AddWithValue("@Type", type);
                    command.Parameters.AddWithValue("@ServerName", request.ServerName);
                    command.Parameters.AddWithValue("@Status", status);
                    command.Parameters.AddWithValue("@timeNow", timeNow);

                    var newId = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return newId;
                }
                catch (Exception e)
                {
                    throw new Exception($"InsertLogToDB Error: {e.Message}");
                }
            }

            public async Task<List<Object>> GetDBLogAllData()
            {
                var resultList = new List<object>();
                try
                {
                    using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT st.*, (il.ID IS NOT NULL) AS IsRecordExist 
                        FROM admin_tool_config.pressurelab_status st
                        LEFT JOIN admin_tool_config.pressurelab_initlogin il ON st.ID = il.ID;
                    ";

                    using var cmd = new MySqlCommand(sql, connection);
                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var row = new ExpandoObject() as IDictionary<String, object>;

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string columnName = reader.GetName(i);
                            object value = reader.IsDBNull(i) ? null : reader.GetValue(i);

                            row.Add(columnName, value);
                        }
                        resultList.Add(row);
                    }
                    return resultList;
                }
                catch (Exception e)
                {
                    throw new Exception($"Function GetDBLogAllData Error: {e.Message}");
                }
            }
            public async Task<bool> DeleteDBSpecifyLog(int Id)
            {
                try
                {
                    using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    string sql = @"
                        DELETE FROM admin_tool_config.pressurelab_initlogin WHERE ID = @id;
                    ";

                    using var cmd = new MySqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@id", Id);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
                catch (MySqlException ex)
                {
                    throw new Exception($"Database error in DeleteDBSpecifyLog: {ex.Message}");
                }
                catch (Exception e)
                {
                    throw new Exception($"Function DeleteDBSpecifyLog Error: {e.Message}");
                }
            }

            public async Task<bool> UpdateDBLogStatus(int id, string status)
            {
                try
                {
                    using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();
                    var timeNow = DateTime.Now.ToString("yyy-MM-dd HH:mm:ss");

                    string sql = @"
                        UPDATE admin_tool_config.pressurelab_status 
                        SET STATUS = @status, END_TIME = @timeNow
                        WHERE ID = @id";

                    using var cmd = new MySqlCommand(sql, connection);

                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@timeNow", timeNow);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    return rowsAffected > 0;
                }
                catch (Exception e)
                {
                    throw new Exception($"UpdateDBLogStatus Error (ID: {id}): {e.Message}");
                }
            }

            public void ExecuteLogtoTxt(string log)
            {
                try
                {
                    string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Pressure_LAB", "Log");

                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    string fileName = $"{DateTime.Now:yyyyMMdd}.txt";
                    string fullPath = Path.Combine(directoryPath, fileName);

                    string timestamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff");
                    string logEntry = $"{timestamp}\t{log}{Environment.NewLine}";

                    lock (_fileLock)
                    {
                        File.AppendAllText(fullPath, logEntry);
                    }
                } catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"Log Error: {e.Message}");
                }
            }

            public async Task<bool> LoginRecord(int id, string serverName, int startLogin, int endLogin)
            {
                try
                {
                    using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    string sql = @"
                        INSERT INTO admin_tool_config.pressurelab_initlogin (ID, SERVERNAME, start_login, end_login) 
                        VALUES (@id, @serverName, @startLogin, @endLogin)
                        ON DUPLICATE KEY UPDATE 
                                SERVERNAME = @serverName, 
                                start_login = @startLogin, 
                                end_login = @endLogin;
                    ";

                    using var cmd = new MySqlCommand(sql, connection);

                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@serverName", serverName);
                    cmd.Parameters.AddWithValue("@startLogin", startLogin);
                    cmd.Parameters.AddWithValue("@endLogin", endLogin);

                    var result = await cmd.ExecuteNonQueryAsync();

                    return result > 0;
                }
                catch (Exception e)
                {
                    throw new Exception($"Function LoginRecord error : {e.Message}", e);
                }
            }

            public async Task<int> LoginAsync(string serverName)
            {
                try
                {
                    using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT end_login
                        FROM admin_tool_config.pressurelab_initlogin
                        WHERE SERVERNAME = @serverName
                        ORDER BY ID DESC
                        LIMIT 1;
                    ";

                    using var cmd = new MySqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@serverName", serverName);

                    var result = await cmd.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }

                    return 0;
                } catch (Exception e)
                {
                    throw new Exception($"Function LoginAsync Failed : {e.Message}");
                }
            }

            public void FillDocx(int Id, DocxReportData data)
            {
                try
                {
                    string baseDir = Directory.GetCurrentDirectory();
                    string reportDir = Path.Combine(baseDir, "wwwroot", "tools", "Pressure_LAB", "Report");
                    string templatePath = Path.Combine(reportDir, "example.docx");
                    string outputPath = Path.Combine(reportDir, $"report_{Id}.docx");

                    if (!Directory.Exists(reportDir))
                        Directory.CreateDirectory(reportDir);
                    if (!File.Exists(templatePath))
                        throw new FileNotFoundException($"Template not found: {templatePath}");

                    File.Copy(templatePath, outputPath, overwrite: true);

                    var props = typeof(DocxReportData).GetProperties();

                    using (DocX document = DocX.Load(outputPath))
                    {
                        for (int i = props.Length; i >= 1; i--)
                        {
                            var p = props[i - 1];
                            object rawValue = p.GetValue(data);
                            string finalValue = (rawValue != null && !string.IsNullOrWhiteSpace(rawValue.ToString())) ? rawValue.ToString() : "";//$"index{i}"
                            string index = $"index{i}";
                            document.ReplaceText(index, finalValue);
                        }
                        document.Save();
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"Function FillDocx error : {e.Message}");
                }
            }

            public async Task<(string ServerName, long StartLogin, long EndLogin)> GetDBSpecifyLog(int Id)
            {
                try
                {
                    using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

                    await connection.OpenAsync();
                    string sql = @"
                        SELECT SERVERNAME, start_login, end_login
                        FROM admin_tool_config.pressurelab_initlogin
                        WHERE ID = @Id;
                    ";

                    using var cmd = new MySqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@Id", Id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    string serverName = null;
                    long startLogin = 0;
                    long endLogin = 0;

                    while (await reader.ReadAsync())
                    {
                        serverName = reader.GetString("SERVERNAME");
                        startLogin = reader.GetInt64("start_login");
                        endLogin = reader.GetInt64("end_login");
                    }
                    return (serverName, startLogin, endLogin);
                }
                catch (Exception e)
                {
                    throw new Exception($"Function LoginAsync Failed : {e.Message}");
                }
            }
        }
    }
}
