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
using static admin_web.Models.Mtapiuse.EditGroup_Model;
using static admin_web.Models.ServerModel;

namespace admin_web.Services.MtapiServices
{
    public class EditGroupService
    {
        private static IFormFile InputFile;
        private static readonly string connectionString = UniversalService.sql_connectionString;
        private static Dictionary<string, ServerRecord> server_dict = new();

        private static void Initiallize()
        {
            server_dict = UniversalService.GetAllServerIP(4);
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

            all_log = all_log
                .GroupBy(x => new { x.Server, x.Group })
                .Select(g => g.First())
                .ToList();


            InsertLogRecordsToDatabase(sql_log);

            return all_log;
        }

        // MT4 執行流程
        private static (List<log_record>, List<sql_record>) MT4_API()
        {
            List<log_record> all_log_list = new();
            List<sql_record> sql_log_list = new();
            var (inputRecords, loginRecords) = Read_Input_MT4();

            // Primary Key
            var duplicateServerGroups = inputRecords
                .SelectMany(kvp => kvp.Value)
                .GroupBy(x => new { x.Server, x.Group })
                .Where(g => g.Count() > 1)
                .Select(g => (g.Key.Server, g.Key.Group))
                .ToList();

            var rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var aa = Path.Combine(rootPath, Environment.Is64BitProcess ? "mtmanapi64.dll" : "mtmanapi.dll");
            var AIROE = new ClrWrapper(aa);

            foreach (var item in inputRecords)
            {
                var server = item.Key;
                List<Group> all_group_before_update = new();
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

                var all_group = AIROE.GroupsRequest();

                foreach (var input_record in item.Value)
                {
                    // 每行資料單個LOG
                    log_record log_record = new();
                    log_record.Server = server;
                    log_record.Group = input_record.Group;
                    try
                    {
                        // 先檢查主Key
                        if (duplicateServerGroups.Any(t => t.Server == input_record.Server && t.Group == input_record.Group))
                        {
                            throw new Exception("相同的 Server, Group 僅能設置一次");
                        }

                        // 檢查是否有該Group
                        if (all_group.Any(record => record.Name == input_record.Group))
                        {
                            // 取出record
                            var group_record_before = DeepCopy(all_group.FirstOrDefault(record => record.Name == input_record.Group));
                            var group_record_after = DeepCopy(group_record_before);
                            all_group_before_update.Add(group_record_before);

                            if (!string.IsNullOrEmpty(input_record.C_SupportPage))
                            {
                                if (input_record.C_SupportPage.Length > 127)
                                {
                                    throw new Exception("C_SupportPage 的字元不可超過 127");
                                }
                                else if (input_record.C_SupportPage == "null")
                                {
                                    group_record_after.SupportPage = "";
                                }
                                else
                                {
                                    group_record_after.SupportPage = input_record.C_SupportPage;
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.P_MaximumSymbols))
                            {
                                if (int.TryParse(input_record.P_MaximumSymbols, out var num))
                                {
                                    if (num > 4096 || num < 0)
                                    {
                                        throw new Exception("P_MaximumSymbols 的設置範圍為 0 ~ 4096(unlimited)");
                                    }
                                    else
                                    {
                                        group_record_after.MaxSecurities = num;
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 P_MaximumSymbols 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.P_MaximumOrders))
                            {
                                if (int.TryParse(input_record.P_MaximumOrders, out var num))
                                {
                                    if (num > 10000 || num < 0)
                                    {
                                        throw new Exception("P_MaximumOrders 的設置範圍為 0(unlimited) ~ 10000");
                                    }
                                    else
                                    {
                                        group_record_after.MaxPositions = num;
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 P_MaximumOrders 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.P_EnableChargeOfSwaps))
                            {
                                if (int.TryParse(input_record.P_EnableChargeOfSwaps, out var Enable))
                                {
                                    if (Enable == 0 || Enable == 1)
                                    {
                                        group_record_after.UseSwap = Enable;
                                    }
                                    else
                                    {
                                        throw new Exception("P_EnableChargeOfSwaps 的設置僅能為 0(Disable), 1(Enable)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 P_EnableChargeOfSwaps 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.P_ProhibitHedgePositions))
                            {
                                if (int.TryParse(input_record.P_ProhibitHedgePositions, out var Enable))
                                {
                                    if (Enable == 0 || Enable == 1)
                                    {
                                        group_record_after.HedgeProhibited = Enable;
                                    }
                                    else
                                    {
                                        throw new Exception("P_ProhibitHedgePositions 的設置僅能為 0(Disable), 1(Enable)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 P_ProhibitHedgePositions 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.A_InactivityPeriod))
                            {
                                if (int.TryParse(input_record.A_InactivityPeriod, out var num))
                                {
                                    if (num != 0 && num != 90 && num != 180 && num != 365)
                                    {
                                        throw new Exception("A_InactivityPeriod 只能設置為 0(Disable), 90, 180, 365");
                                    }
                                    else
                                    {
                                        group_record_after.ArchivePeriod = num;
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 A_InactivityPeriod 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.A_MaximumBalance))
                            {
                                if (int.TryParse(input_record.A_MaximumBalance, out var num))
                                {
                                    group_record_after.ArchiveMaxBalance = num;
                                }
                                else
                                {
                                    throw new Exception("無法將 Filtration_Level 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.A_ArchiveDeletedPendingsOlder))
                            {
                                if (int.TryParse(input_record.A_ArchiveDeletedPendingsOlder, out var num))
                                {
                                    group_record_after.ArchivePendingPeriod = num;
                                }
                                else
                                {
                                    throw new Exception("無法將 A_ArchiveDeletedPendingsOlder 轉換成有效的數字");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.M_MarginCallLevel))
                            {
                                if (int.TryParse(input_record.M_MarginCallLevel, out var num))
                                {
                                    group_record_after.MarginCall = num;
                                }
                                else
                                {
                                    throw new Exception("無法將 M_MarginCallLevel 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.M_StopOutLevel))
                            {
                                if (int.TryParse(input_record.M_StopOutLevel, out var num))
                                {
                                    group_record_after.MarginStopout = num;
                                }
                                else
                                {
                                    throw new Exception("無法將 M_StopOutLevel 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.M_StopOutSkipHedged))
                            {
                                if (int.TryParse(input_record.M_StopOutSkipHedged, out var Enable))
                                {
                                    if (Enable == 0 || Enable == 1)
                                    {
                                        group_record_after.StopOutSkipHedged = Enable;
                                    }
                                    else
                                    {
                                        throw new Exception("M_StopOutSkipHedged 的設置僅能為 0(Disable), 1(Enable)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 M_StopOutSkipHedged 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.R_Enable))
                            {
                                if (int.TryParse(input_record.R_Enable, out var Enable))
                                {
                                    if (Enable == 0 || Enable == 1)
                                    {
                                        group_record_after.Reports = Enable;
                                    }
                                    else
                                    {
                                        throw new Exception("R_Enable 的設置僅能為 0(Disable), 1(Enable)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 R_Enable 轉換成有效的整數");
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.R_SMTPserver))
                            {
                                if (input_record.R_SMTPserver.Length > 63)
                                {
                                    throw new Exception("R_SMTPserver 的字元不可超過 63");
                                }
                                else if (input_record.R_SMTPserver == "null")
                                {
                                    group_record_after.SmtpServer = "";
                                }
                                else
                                {
                                    group_record_after.SmtpServer = input_record.R_SMTPserver;
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.R_SMTPlogin))
                            {
                                if (input_record.R_SMTPlogin.Length > 31)
                                {
                                    throw new Exception("R_SMTPlogin 的字元不可超過 31");
                                }
                                else if (input_record.R_SMTPlogin == "null")
                                {
                                    group_record_after.SmtpLogin = "";
                                }
                                else
                                {
                                    group_record_after.SmtpLogin = input_record.R_SMTPlogin;
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.R_SMTPpassword))
                            {
                                if (input_record.R_SMTPpassword.Length > 31)
                                {
                                    throw new Exception("R_SMTPpassword 的字元不可超過 31");
                                }
                                else if (input_record.R_SMTPpassword == "null")
                                {
                                    group_record_after.SmtpPassword = "";
                                }
                                else
                                {
                                    group_record_after.SmtpPassword = input_record.R_SMTPpassword;
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.R_SupportEmail))
                            {
                                if (input_record.R_SupportEmail.Length > 63)
                                {
                                    throw new Exception("R_SupportEmail 的字元不可超過 63");
                                }
                                else if (input_record.R_SupportEmail == "null")
                                {
                                    group_record_after.SupportEmail = "";
                                }
                                else
                                {
                                    group_record_after.SupportEmail = input_record.R_SupportEmail;
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.R_TemplatesPath))
                            {
                                if (input_record.R_TemplatesPath.Length > 31)
                                {
                                    throw new Exception("R_TemplatesPath 的字元不可超過 31");
                                }
                                else if (input_record.R_TemplatesPath == "null")
                                {
                                    group_record_after.Templates = "";
                                }
                                else
                                {
                                    group_record_after.Templates = input_record.R_TemplatesPath;
                                }
                            }

                            if (!string.IsNullOrEmpty(input_record.R_CopyReportToSupport))
                            {
                                if (int.TryParse(input_record.R_CopyReportToSupport, out var Enable))
                                {
                                    if (Enable == 0 || Enable == 1)
                                    {
                                        group_record_after.Copies = Enable;
                                    }
                                    else
                                    {
                                        throw new Exception("R_CopyReportToSupport 的設置僅能為 0(Disable), 1(Enable)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法將 R_CopyReportToSupport 轉換成有效的整數");
                                }
                            }

                            // 前後比對
                            var differences = new List<string>();
                            var properties = typeof(Group).GetProperties();
                            foreach (var property in properties)
                            {
                                var value1 = property.GetValue(group_record_before)?.ToString();
                                var value2 = property.GetValue(group_record_after)?.ToString();

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
                                int res = AIROE.CfgUpdateGroup(group_record_after);
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
                            throw new Exception("找不到該 Group");
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
                var all_group_after_update = AIROE.GroupsRequest();

                foreach (var input_record in item.Value)
                {
                    var log = all_log_list.FirstOrDefault(record => record.Group == input_record.Group && record.Server == server);

                    // 檢查API Update成功的
                    if (log.Result == "執行成功")
                    {
                        var group_info_before = all_group_before_update.FirstOrDefault(record => record.Name == input_record.Group);
                        var time_set = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        var differences = new List<string>(); // 給User看的比對
                        var group_info_after = all_group_after_update.FirstOrDefault(record => record.Name == group_info_before.Name);

                        if (group_info_before.SupportPage != group_info_after.SupportPage)
                        {
                            differences.Add($"[修改]C_SupportPage: {group_info_before.SupportPage} -> {group_info_after.SupportPage}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "C_SupportPage",
                                Before = group_info_before.SupportPage,
                                After = group_info_after.SupportPage,
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.C_SupportPage) && input_record.C_SupportPage != group_info_after.SupportPage.ToString())
                        {
                            differences.Add($"[未改]C_SupportPage: {group_info_before.SupportPage} -> {group_info_after.SupportPage}");
                        }

                        if (group_info_before.MaxSecurities != group_info_after.MaxSecurities)
                        {
                            differences.Add($"[修改]P_MaximumSymbols: {group_info_before.MaxSecurities} -> {group_info_after.MaxSecurities}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "P_MaximumSymbols",
                                Before = group_info_before.MaxSecurities.ToString(),
                                After = group_info_after.MaxSecurities.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.P_MaximumSymbols) && input_record.P_MaximumSymbols != group_info_after.MaxSecurities.ToString())
                        {
                            differences.Add($"[未改]P_MaximumSymbols: {group_info_before.MaxSecurities} -> {group_info_after.MaxSecurities}");
                        }

                        if (group_info_before.MaxPositions != group_info_after.MaxPositions)
                        {
                            differences.Add($"[修改]P_MaximumOrders: {group_info_before.MaxPositions} -> {group_info_after.MaxPositions}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "P_MaximumOrders",
                                Before = group_info_before.MaxPositions.ToString(),
                                After = group_info_after.MaxPositions.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.P_MaximumOrders) && input_record.P_MaximumOrders != group_info_after.MaxPositions.ToString())
                        {
                            differences.Add($"[未改]P_MaximumOrders: {group_info_before.MaxPositions} -> {group_info_after.MaxPositions}");
                        }

                        if (group_info_before.UseSwap != group_info_after.UseSwap)
                        {
                            differences.Add($"[修改]P_EnableChargeOfSwaps: {group_info_before.UseSwap} -> {group_info_after.UseSwap}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "P_EnableChargeOfSwaps",
                                Before = group_info_before.UseSwap.ToString(),
                                After = group_info_after.UseSwap.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.P_EnableChargeOfSwaps) && input_record.P_EnableChargeOfSwaps != group_info_after.UseSwap.ToString())
                        {
                            differences.Add($"[未改]P_EnableChargeOfSwaps: {group_info_before.UseSwap} -> {group_info_after.UseSwap}");
                        }

                        if (group_info_before.HedgeProhibited != group_info_after.HedgeProhibited)
                        {
                            differences.Add($"[修改]P_ProhibitHedgePositions: {group_info_before.HedgeProhibited} -> {group_info_after.HedgeProhibited}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "P_ProhibitHedgePositions",
                                Before = group_info_before.HedgeProhibited.ToString(),
                                After = group_info_after.HedgeProhibited.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.P_ProhibitHedgePositions) && input_record.P_ProhibitHedgePositions != group_info_after.HedgeProhibited.ToString())
                        {
                            differences.Add($"[未改]P_ProhibitHedgePositions: {group_info_before.HedgeProhibited} -> {group_info_after.HedgeProhibited}");
                        }

                        if (group_info_before.ArchivePeriod != group_info_after.ArchivePeriod)
                        {
                            differences.Add($"[修改]A_InactivityPeriod: {group_info_before.ArchivePeriod} -> {group_info_after.ArchivePeriod}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "A_InactivityPeriod",
                                Before = group_info_before.ArchivePeriod.ToString(),
                                After = group_info_after.ArchivePeriod.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.A_InactivityPeriod) && input_record.A_InactivityPeriod != group_info_after.ArchivePeriod.ToString())
                        {
                            differences.Add($"[未改]A_InactivityPeriod: {group_info_before.ArchivePeriod} -> {group_info_after.ArchivePeriod}");
                        }

                        if (group_info_before.ArchiveMaxBalance != group_info_after.ArchiveMaxBalance)
                        {
                            differences.Add($"[修改]A_MaximumBalance: {group_info_before.ArchiveMaxBalance} -> {group_info_after.ArchiveMaxBalance}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "A_MaximumBalance",
                                Before = group_info_before.ArchiveMaxBalance.ToString(),
                                After = group_info_after.ArchiveMaxBalance.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.A_MaximumBalance) && input_record.A_MaximumBalance != group_info_after.ArchiveMaxBalance.ToString())
                        {
                            differences.Add($"[未改]A_MaximumBalance: {group_info_before.ArchiveMaxBalance} -> {group_info_after.ArchiveMaxBalance}");
                        }

                        if (group_info_before.ArchivePendingPeriod != group_info_after.ArchivePendingPeriod)
                        {
                            differences.Add($"[修改]A_ArchiveDeletedPendingsOlder: {group_info_before.ArchivePendingPeriod} -> {group_info_after.ArchivePendingPeriod}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "A_ArchiveDeletedPendingsOlder",
                                Before = group_info_before.ArchivePendingPeriod.ToString(),
                                After = group_info_after.ArchivePendingPeriod.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.A_ArchiveDeletedPendingsOlder) && input_record.A_ArchiveDeletedPendingsOlder != group_info_after.ArchivePendingPeriod.ToString())
                        {
                            differences.Add($"[未改]A_ArchiveDeletedPendingsOlder: {group_info_before.ArchivePendingPeriod} -> {group_info_after.ArchivePendingPeriod}");
                        }

                        if (group_info_before.MarginCall != group_info_after.MarginCall)
                        {
                            differences.Add($"[修改]M_MarginCallLevel: {group_info_before.MarginCall} -> {group_info_after.MarginCall}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "M_MarginCallLevel",
                                Before = group_info_before.MarginCall.ToString(),
                                After = group_info_after.MarginCall.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.M_MarginCallLevel) && input_record.M_MarginCallLevel != group_info_after.MarginCall.ToString())
                        {
                            differences.Add($"[未改]M_MarginCallLevel: {group_info_before.MarginCall} -> {group_info_after.MarginCall}");
                        }

                        if (group_info_before.MarginStopout != group_info_after.MarginStopout)
                        {
                            differences.Add($"[修改]M_StopOutLevel: {group_info_before.MarginStopout} -> {group_info_after.MarginStopout}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "M_StopOutLevel",
                                Before = group_info_before.MarginStopout.ToString(),
                                After = group_info_after.MarginStopout.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.M_StopOutLevel) && input_record.M_StopOutLevel != group_info_after.MarginStopout.ToString())
                        {
                            differences.Add($"[未改]M_StopOutLevel: {group_info_before.MarginStopout} -> {group_info_after.MarginStopout}");
                        }

                        if (group_info_before.StopOutSkipHedged != group_info_after.StopOutSkipHedged)
                        {
                            differences.Add($"[修改]M_StopOutSkipHedged: {group_info_before.StopOutSkipHedged} -> {group_info_after.StopOutSkipHedged}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "M_StopOutSkipHedged",
                                Before = group_info_before.StopOutSkipHedged.ToString(),
                                After = group_info_after.StopOutSkipHedged.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.M_StopOutSkipHedged) && input_record.M_StopOutSkipHedged != group_info_after.StopOutSkipHedged.ToString())
                        {
                            differences.Add($"[未改]M_StopOutSkipHedged: {group_info_before.StopOutSkipHedged} -> {group_info_after.StopOutSkipHedged}");
                        }

                        if (group_info_before.Reports != group_info_after.Reports)
                        {
                            differences.Add($"[修改]R_Enable: {group_info_before.Reports} -> {group_info_after.Reports}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "R_Enable",
                                Before = group_info_before.Reports.ToString(),
                                After = group_info_after.Reports.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.R_Enable) && input_record.R_Enable != group_info_after.Reports.ToString())
                        {
                            differences.Add($"[未改]R_Enable: {group_info_before.Reports} -> {group_info_after.Reports}");
                        }

                        if (group_info_before.SmtpServer != group_info_after.SmtpServer)
                        {
                            differences.Add($"[修改]R_SMTPserver: {group_info_before.SmtpServer} -> {group_info_after.SmtpServer}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "R_SMTPserver",
                                Before = group_info_before.SmtpServer.ToString(),
                                After = group_info_after.SmtpServer.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.R_SMTPserver) && input_record.R_SMTPserver != group_info_after.SmtpServer.ToString())
                        {
                            differences.Add($"[未改]R_SMTPserver: {group_info_before.SmtpServer} -> {group_info_after.SmtpServer}");
                        }

                        if (group_info_before.SmtpLogin != group_info_after.SmtpLogin)
                        {
                            differences.Add($"[修改]R_SMTPlogin: {group_info_before.SmtpLogin} -> {group_info_after.SmtpLogin}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "R_SMTPlogin",
                                Before = group_info_before.SmtpLogin.ToString(),
                                After = group_info_after.SmtpLogin.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.R_SMTPlogin) && input_record.R_SMTPlogin != group_info_after.SmtpLogin.ToString())
                        {
                            differences.Add($"[未改]R_SMTPlogin: {group_info_before.SmtpLogin} -> {group_info_after.SmtpLogin}");
                        }

                        if (group_info_before.SmtpPassword != group_info_after.SmtpPassword)
                        {
                            differences.Add($"[修改]R_SMTPpassword: {group_info_before.SmtpPassword} -> {group_info_after.SmtpPassword}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "R_SMTPpassword",
                                Before = group_info_before.SmtpPassword.ToString(),
                                After = group_info_after.SmtpPassword.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.R_SMTPpassword) && input_record.R_SMTPpassword != group_info_after.SmtpPassword.ToString())
                        {
                            differences.Add($"[未改]R_SMTPpassword: {group_info_before.SmtpPassword} -> {group_info_after.SmtpPassword}");
                        }

                        if (group_info_before.SupportEmail != group_info_after.SupportEmail)
                        {
                            differences.Add($"[修改]R_SupportEmail: {group_info_before.SupportEmail} -> {group_info_after.SupportEmail}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "R_SupportEmail",
                                Before = group_info_before.SupportEmail.ToString(),
                                After = group_info_after.SupportEmail.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.R_SupportEmail) && input_record.R_SupportEmail != group_info_after.SupportEmail.ToString())
                        {
                            differences.Add($"[未改]R_SupportEmail: {group_info_before.SupportEmail} -> {group_info_after.SupportEmail}");
                        }

                        if (group_info_before.Templates != group_info_after.Templates)
                        {
                            differences.Add($"[修改]R_TemplatesPath: {group_info_before.Templates} -> {group_info_after.Templates}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "R_TemplatesPath",
                                Before = group_info_before.Templates.ToString(),
                                After = group_info_after.Templates.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.R_TemplatesPath) && input_record.R_TemplatesPath != group_info_after.Templates.ToString())
                        {
                            differences.Add($"[未改]R_TemplatesPath: {group_info_before.Templates} -> {group_info_after.Templates}");
                        }

                        if (group_info_before.Copies != group_info_after.Copies)
                        {
                            differences.Add($"[修改]R_CopyReportToSupport: {group_info_before.Copies} -> {group_info_after.Copies}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Group = group_info_before.Name,
                                Item = "R_CopyReportToSupport",
                                Before = group_info_before.Copies.ToString(),
                                After = group_info_after.Copies.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.R_CopyReportToSupport) && input_record.R_CopyReportToSupport != group_info_after.Copies.ToString())
                        {
                            differences.Add($"[未改]R_CopyReportToSupport: {group_info_before.Copies} -> {group_info_after.Copies}");
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
        private static void MT5_API()
        {
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
                                Group = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Group") + 1].Text,
                                C_SupportPage = worksheet_input.Cells[row, Array.IndexOf(headerRow, "C_SupportPage") + 1].Text,
                                P_MaximumSymbols = worksheet_input.Cells[row, Array.IndexOf(headerRow, "P_MaximumSymbols") + 1].Text,
                                P_MaximumOrders = worksheet_input.Cells[row, Array.IndexOf(headerRow, "P_MaximumOrders") + 1].Text,
                                P_EnableChargeOfSwaps = worksheet_input.Cells[row, Array.IndexOf(headerRow, "P_EnableChargeOfSwaps") + 1].Text,
                                P_ProhibitHedgePositions = worksheet_input.Cells[row, Array.IndexOf(headerRow, "P_ProhibitHedgePositions") + 1].Text,
                                A_InactivityPeriod = worksheet_input.Cells[row, Array.IndexOf(headerRow, "A_InactivityPeriod") + 1].Text,
                                A_MaximumBalance = worksheet_input.Cells[row, Array.IndexOf(headerRow, "A_MaximumBalance") + 1].Text,
                                A_ArchiveDeletedPendingsOlder = worksheet_input.Cells[row, Array.IndexOf(headerRow, "A_ArchiveDeletedPendingsOlder") + 1].Text,
                                M_MarginCallLevel = worksheet_input.Cells[row, Array.IndexOf(headerRow, "M_MarginCallLevel") + 1].Text,
                                M_StopOutLevel = worksheet_input.Cells[row, Array.IndexOf(headerRow, "M_StopOutLevel") + 1].Text,
                                M_StopOutSkipHedged = worksheet_input.Cells[row, Array.IndexOf(headerRow, "M_StopOutSkipHedged") + 1].Text,
                                R_Enable = worksheet_input.Cells[row, Array.IndexOf(headerRow, "R_Enable") + 1].Text,
                                R_SMTPserver = worksheet_input.Cells[row, Array.IndexOf(headerRow, "R_SMTPserver") + 1].Text,
                                R_SMTPlogin = worksheet_input.Cells[row, Array.IndexOf(headerRow, "R_SMTPlogin") + 1].Text,
                                R_SMTPpassword = worksheet_input.Cells[row, Array.IndexOf(headerRow, "R_SMTPpassword") + 1].Text,
                                R_SupportEmail = worksheet_input.Cells[row, Array.IndexOf(headerRow, "R_SupportEmail") + 1].Text,
                                R_TemplatesPath = worksheet_input.Cells[row, Array.IndexOf(headerRow, "R_TemplatesPath") + 1].Text,
                                R_CopyReportToSupport = worksheet_input.Cells[row, Array.IndexOf(headerRow, "R_CopyReportToSupport") + 1].Text,
                            };

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

        //private static (Dictionary<string, List<mt5_input>>, Dictionary<string, Login_Record>) Read_Input_MT5()
        //{
        //    ExcelPackage.LicenseContext = LicenseContext.Commercial;
        //    var inputRecords = new Dictionary<string, List<mt5_input>>();
        //    var loginRecords = new Dictionary<string, Login_Record>();

        //    using (var stream = new MemoryStream())
        //    {
        //        InputFile.CopyTo(stream);
        //        using (var package = new ExcelPackage(stream))
        //        {
        //            var worksheet_input = package.Workbook.Worksheets[$"mt5_input"];

        //            if (worksheet_input != null)
        //            {
        //                int rows = worksheet_input.Dimension.Rows;
        //                var headerRow = worksheet_input.Cells[1, 1, 1, worksheet_input.Dimension.End.Column].Select(cell => cell.Text.Trim()).ToArray();

        //                for (int row = 2; row <= rows; row++)
        //                {
        //                    var record = new mt5_input
        //                    {

        //                    };


        //                    if (!inputRecords.ContainsKey(record.Server))
        //                    {
        //                        inputRecords[record.Server] = new List<mt5_input>();
        //                    }

        //                    inputRecords[record.Server].Add(record);
        //                }
        //            }

        //            var worksheet_login = package.Workbook.Worksheets[$"mt5_login"];
        //            if (worksheet_login != null)
        //            {
        //                int rows = worksheet_login.Dimension.Rows;
        //                var headerRow = worksheet_login.Cells[1, 1, 1, worksheet_login.Dimension.End.Column].Select(cell => cell.Text.Trim()).ToArray();

        //                for (int row = 2; row <= rows; row++)
        //                {
        //                    var record = new Login_Record
        //                    {
        //                        Server = worksheet_login.Cells[row, Array.IndexOf(headerRow, "Server") + 1].Text,
        //                        Login = worksheet_login.Cells[row, Array.IndexOf(headerRow, "Login") + 1].Text,
        //                        Password = worksheet_login.Cells[row, Array.IndexOf(headerRow, "Password") + 1].Text
        //                    };

        //                    loginRecords[record.Server] = record;
        //                }
        //            }
        //        }
        //    }

        //    return (inputRecords, loginRecords);
        //}

        private static T DeepCopy<T>(T obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<T>(json);
        }

        // 更新紀錄寫進資料庫
        public static void InsertLogRecordsToDatabase(List<sql_record> tool_Log)
        {
            Initiallize();
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    INSERT INTO admin_tool_log.edit_group (
                        Server, `Group`, Item, `Before`, `After`, UserLogin, Time
                    ) VALUES (
                        @Server, @Group, @Item, @Before, @After, @UserLogin, @Time
                    );";

                foreach (var logRecord in tool_Log)
                {
                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Server", logRecord.Server);
                        cmd.Parameters.AddWithValue("@Group", logRecord.Group);
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
                        FROM admin_tool_log.edit_group
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

                string fileName = "批量修改組別資訊HistoryLog.xlsx";
                var stream = new MemoryStream(package.GetAsByteArray());

                return (stream, fileName);
            }
        }
    }
}
