using MetaQuotes.MT5CommonAPI;
using MetaQuotes.MT5ManagerAPI;
using Microsoft.AspNetCore.Http;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using OfficeOpenXml;
using P23.MetaTrader4.Manager;
using P23.MetaTrader4.Manager.Contracts;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using static admin_web.Models.Mtapiuse.EditAccount_Model;
using static admin_web.Models.ServerModel;

namespace admin_web.Services.MtapiServices
{
    public class EditAccountService
    {
        // variable
        private static Dictionary<string, ServerRecord> server_dict = new(); // MT連線資訊
        private static IFormFile InputFile; // 使用者上傳檔
        private static readonly string connectionString = UniversalService.sql_connectionString;

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

            var (mt5_log, mt5_sql_log) = MT5_API();
            all_log.AddRange(mt5_log);
            sql_log.AddRange(mt5_sql_log);

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
                List<UserRecord> all_user_update_before = new();
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

                //var all_user_list = AIROE.UsersRequest();
                List<int> login_list = item.Value
                            .Select(input => input.Login)
                            .Distinct()
                            .Select(login =>
                            {
                                int parsedLogin;
                                if (int.TryParse(login, out parsedLogin))
                                {
                                    return (int?)parsedLogin;
                                }
                                return null;
                            })
                            .Where(x => x.HasValue)
                            .Select(x => x.Value)
                            .Distinct()
                            .ToList();
                var all_user_list = AIROE.UserRecordsRequest(login_list);

                foreach (var input_record in item.Value)
                {
                    // 每行資料單個LOG
                    log_record log_record = new();
                    log_record.Server = server;
                    log_record.Login = input_record.Login;
                    try
                    {
                        // 檢查是否有該Login
                        if (all_user_list.Any(record => record.Login == Convert.ToInt32(input_record.Login)))
                        {
                            // 取出record
                            var user_record_before = DeepCopy(all_user_list.FirstOrDefault(record => record.Login == Convert.ToInt32(input_record.Login)));
                            var user_record_after = DeepCopy(user_record_before);
                            all_user_update_before.Add(user_record_before);

                            // Enable: 0
                            if (!string.IsNullOrEmpty(input_record.Enable))
                            {
                                if (int.TryParse(input_record.Enable, out var enable))
                                {
                                    if (enable == 1 || enable == 0)
                                    {
                                        user_record_after.Enable = enable;
                                    }
                                    else
                                    {
                                        throw new Exception("警告：Enable 僅能設置 1(啟用)、0(禁用)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("警告：無法辨別 Enable 設置");
                                }
                            }
                            // Name: 無預設
                            if (!string.IsNullOrEmpty(input_record.Name))
                            {
                                if (input_record.Name.Length > 127)
                                {
                                    throw new Exception("警告：Name 的字元不可超過 127");
                                }
                                else
                                {
                                    user_record_after.Name = input_record.Name;
                                }
                            }
                            // Color
                            if (!string.IsNullOrEmpty(input_record.Color))
                            {
                                if (input_record.Color == "null")
                                {
                                    user_record_after.UserColor = 4278190080;
                                }
                                else if (TryParseRGB(input_record.Color, out int r_color, out int g_color, out int b_color))
                                {
                                    user_record_after.UserColor = (uint)(r_color + g_color * 256 + b_color * 256 * 256);
                                }
                                else
                                {
                                    throw new Exception("警告：Color的格式有誤，例：R,G,B，0~255");
                                }
                            }
                            // Group: 無預設
                            if (!string.IsNullOrEmpty(input_record.Group))
                            {
                                if (input_record.Group.Length > 15)
                                {
                                    throw new Exception("警告：Group 的字元不可超過 15");
                                }
                                else
                                {
                                    user_record_after.Group = input_record.Group;
                                }
                            }
                            // Leverage
                            if (!string.IsNullOrEmpty(input_record.Leverage))
                            {
                                if (int.TryParse(input_record.Leverage, out var leverage))
                                {
                                    if (leverage > 1000 || leverage < 1)
                                    {
                                        throw new Exception("警告：Leverage 僅能設置 1 ~ 1000");
                                    }
                                    else
                                    {
                                        user_record_after.Leverage = leverage;
                                    }
                                }
                                else
                                {
                                    throw new Exception("警告：無法辨別 Leverage 設置");
                                }
                            }
                            // AgentAccount
                            if (!string.IsNullOrEmpty(input_record.AgentAccount))
                            {
                                if (int.TryParse(input_record.AgentAccount, out var acc))
                                {
                                    if (acc < 0)
                                    {
                                        throw new Exception("警告：AgentAccount 不可為負");
                                    }
                                    else
                                    {
                                        user_record_after.AgentAccount = acc;
                                    }
                                }
                                else
                                {
                                    throw new Exception("警告：無法辨別 AgentAccount 設置");
                                }
                            }
                            // Taxes
                            if (!string.IsNullOrEmpty(input_record.Taxes))
                            {
                                if (int.TryParse(input_record.Taxes, out var taxes))
                                {
                                    if (taxes < 0)
                                    {
                                        throw new Exception("警告：Taxes 不可為負");
                                    }
                                    else
                                    {
                                        user_record_after.Taxes = taxes;
                                    }
                                }
                                else
                                {
                                    throw new Exception("警告：無法辨別 Taxes 設置");
                                }
                            }
                            // SendReports
                            if (!string.IsNullOrEmpty(input_record.SendReports))
                            {
                                if (int.TryParse(input_record.SendReports, out var enable))
                                {
                                    if (enable == 0 || enable == 1)
                                    {
                                        user_record_after.SendReports = enable;
                                    }
                                    else
                                    {
                                        throw new Exception("警告：SendReports 僅能設置 1(啟用)、0(禁用)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("警告：無法辨別 SendReports 設置");
                                }
                            }
                            
                            // Mqid
                            //if (!string.IsNullOrEmpty(input_record.Mqid))
                            //{
                            //    if (uint.TryParse(input_record.Mqid, out var mqid))
                            //    {
                            //        user_record_after.Mqid = mqid;
                            //    }
                            //    else
                            //    {
                            //        throw new Exception("警告：無法辨別 Mqid 設置");
                            //    }
                            //}

                            // Status: 空白
                            if (!string.IsNullOrEmpty(input_record.Status))
                            {
                                if (input_record.Status == "null")
                                {
                                    user_record_after.Status = "";
                                }
                                else
                                {
                                    if (input_record.Status.Length > 15)
                                    {
                                        throw new Exception("警告：Status 的字元不可超過 15");
                                    }
                                    else
                                    {
                                        user_record_after.Status = input_record.Status;
                                    }
                                }
                            }
                            // Id: 空白
                            if (!string.IsNullOrEmpty(input_record.Id))
                            {
                                if (input_record.Id == "null")
                                {
                                    user_record_after.Id = "";
                                }
                                else
                                {
                                    if (input_record.Id.Length > 31)
                                    {
                                        throw new Exception("警告：Id 的字元不可超過 31");
                                    }
                                    else
                                    {
                                        user_record_after.Id = input_record.Id;
                                    }
                                }
                            }
                            // Comment: 空白
                            if (!string.IsNullOrEmpty(input_record.Comment))
                            {
                                if (input_record.Comment == "null")
                                {
                                    user_record_after.Comment = "";
                                }
                                else
                                {
                                    if (input_record.Comment.Length > 63)
                                    {
                                        throw new Exception("警告：Comment 的字元不可超過 63");
                                    }
                                    else
                                    {
                                        user_record_after.Comment = input_record.Comment;
                                    }
                                }
                            }
                            // EnableChangePassword
                            if (!string.IsNullOrEmpty(input_record.EnableChangePassword))
                            {
                                if (int.TryParse(input_record.EnableChangePassword, out var enable))
                                {
                                    if (enable == 0 || enable == 1)
                                    {
                                        user_record_after.EnableChangePassword = enable;
                                    }
                                    else
                                    {
                                        throw new Exception("警告：EnableChangePassword 僅能設置 1(啟用)、0(禁用)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("警告：無法辨別 EnableChangePassword 設置");
                                }
                            }
                            // EnableReadOnly
                            if (!string.IsNullOrEmpty(input_record.EnableReadOnly))
                            {
                                if (int.TryParse(input_record.EnableReadOnly, out var enable))
                                {
                                    if (enable == 0 || enable == 1)
                                    {
                                        user_record_after.EnableReadOnly = enable;
                                    }
                                    else
                                    {
                                        throw new Exception("警告：EnableReadOnly 僅能設置 1(啟用)、0(禁用)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("警告：無法辨別 EnableReadOnly 設置");
                                }
                            }
                            // EnableOTP
                            if (!string.IsNullOrEmpty(input_record.EnableOTP))
                            {
                                if (int.TryParse(input_record.EnableOTP, out var enable))
                                {
                                    if (enable == 0 || enable == 1)
                                    {
                                        user_record_after.EnableOTP = enable;
                                    }
                                    else
                                    {
                                        throw new Exception("警告：EnableOTP 僅能設置 1(啟用)、0(禁用)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("警告：無法辨別 EnableOTP 設置");
                                }
                            }
                            // PasswordPhone: 空白
                            if (!string.IsNullOrEmpty(input_record.PasswordPhone))
                            {
                                if (input_record.PasswordPhone == "null")
                                {
                                    user_record_after.PasswordPhone = "";
                                }
                                else
                                {
                                    if (input_record.PasswordPhone.Length > 31)
                                    {
                                        throw new Exception("警告：PasswordPhone 的字元不可超過 31");
                                    }
                                    else
                                    {
                                        user_record_after.PasswordPhone = input_record.PasswordPhone;
                                    }
                                }
                            }
                            // Country: 空白
                            if (!string.IsNullOrEmpty(input_record.Country))
                            {
                                if (input_record.Country == "null")
                                {
                                    user_record_after.Country = "";
                                }
                                else
                                {
                                    if (input_record.Country.Length > 31)
                                    {
                                        throw new Exception("警告：Country 的字元不可超過 31");
                                    }
                                    else
                                    {
                                        user_record_after.Country = input_record.Country;
                                    }
                                }
                            }
                            // City: 空白
                            if (!string.IsNullOrEmpty(input_record.City))
                            {
                                if (input_record.City == "null")
                                {
                                    user_record_after.City = "";
                                }
                                else
                                {
                                    if (input_record.City.Length > 31)
                                    {
                                        throw new Exception("警告：City 的字元不可超過 31");
                                    }
                                    else
                                    {
                                        user_record_after.City = input_record.City;
                                    }
                                }
                            }
                            // State: 空白
                            if (!string.IsNullOrEmpty(input_record.State))
                            {
                                if (input_record.State == "null")
                                {
                                    user_record_after.State = "";
                                }
                                else
                                {
                                    if (input_record.State.Length > 31)
                                    {
                                        throw new Exception("警告：State 的字元不可超過 31");
                                    }
                                    else
                                    {
                                        user_record_after.State = input_record.State;
                                    }
                                }
                            }
                            // ZipCode: 空白
                            if (!string.IsNullOrEmpty(input_record.ZipCode))
                            {
                                if (input_record.ZipCode == "null")
                                {
                                    user_record_after.ZipCode = "";
                                }
                                else
                                {
                                    if (input_record.ZipCode.Length > 15)
                                    {
                                        throw new Exception("警告：ZipCode 的字元不可超過 15");
                                    }
                                    else
                                    {
                                        user_record_after.ZipCode = input_record.ZipCode;
                                    }
                                }
                            }
                            // Address: 空白
                            if (!string.IsNullOrEmpty(input_record.Address))
                            {
                                if (input_record.Address == "null")
                                {
                                    user_record_after.Address = "";
                                }
                                else
                                {
                                    if (input_record.Address.Length > 95)
                                    {
                                        throw new Exception("警告：Address 的字元不可超過 95");
                                    }
                                    else
                                    {
                                        user_record_after.Address = input_record.Address;
                                    }
                                }
                            }
                            // LeadSource: 空白
                            if (!string.IsNullOrEmpty(input_record.LeadSource))
                            {
                                if (input_record.LeadSource == "null")
                                {
                                    user_record_after.LeadSource = "";
                                }
                                else
                                {
                                    if (input_record.LeadSource.Length > 31)
                                    {
                                        throw new Exception("警告：LeadSource 的字元不可超過 31");
                                    }
                                    else
                                    {
                                        user_record_after.LeadSource = input_record.LeadSource;
                                    }
                                }
                            }
                            // Phone: 空白
                            if (!string.IsNullOrEmpty(input_record.Phone))
                            {
                                if (input_record.Phone == "null")
                                {
                                    user_record_after.Phone = "";
                                }
                                else
                                {
                                    if (input_record.Phone.Length > 31)
                                    {
                                        throw new Exception("警告：Phone 的字元不可超過 31");
                                    }
                                    else
                                    {
                                        user_record_after.Phone = input_record.Phone;
                                    }
                                }
                            }
                            // Email: 空白
                            if (!string.IsNullOrEmpty(input_record.Email))
                            {
                                if (input_record.Email == "null")
                                {
                                    user_record_after.Email = "";
                                }
                                else
                                {
                                    if (input_record.Email.Length > 47)
                                    {
                                        throw new Exception("警告：Email 的字元不可超過 47");
                                    }
                                    else
                                    {
                                        user_record_after.Email = input_record.Email;
                                    }
                                }
                            }

                            // 前後比對
                            var differences = new List<string>();
                            var properties = typeof(UserRecord).GetProperties();
                            foreach (var property in properties)
                            {
                                var value1 = property.GetValue(user_record_before)?.ToString();
                                var value2 = property.GetValue(user_record_after)?.ToString();

                                if (value1 == null && value2 == null)
                                {
                                    continue;
                                }
                                else if (value1 == null || value2 == null || !value1.Equals(value2))
                                {
                                    differences.Add($"{property.Name}: {value1} -> {value2}");
                                }
                            }

                            // 簡單比對: 有和原本設置不同的就做update，不管update後有無不同
                            if (differences.Count > 0)
                            {
                                int res = AIROE.UserRecordUpdate(user_record_after);
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
                            throw new Exception("找不到該Login");
                        }
                    }
                    catch (Exception ex)
                    {
                        log_record.Difference = null;
                        log_record.Result = ex.Message;
                    }
                    finally
                    {
                        all_log_list.Add(log_record);
                    }
                }

                // Update後的比對
                var all_user_AfterUpdate = AIROE.UsersRequest();

                foreach (var input_record in item.Value)
                {
                    var log = all_log_list.FirstOrDefault(record => record.Login == input_record.Login.ToString() && record.Server == server);

                    // 檢查API Update成功的
                    if (log.Result == "執行成功")
                    {
                        var user_info_before = all_user_update_before.FirstOrDefault(record => record.Login.ToString() == input_record.Login);
                        var time_set = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        var differences = new List<string>(); // 給User看的比對
                        var user_info_after = all_user_AfterUpdate.FirstOrDefault(user => user.Login == user_info_before.Login);

                        // API正常更新
                        if (user_info_before.Enable != user_info_after.Enable)
                        {
                            differences.Add($"[修改] Enable: {user_info_before.Enable} -> {user_info_after.Enable}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Enable",
                                Before = user_info_before.Enable.ToString(),
                                After = user_info_after.Enable.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        // API不給更新
                        else if(!string.IsNullOrEmpty(input_record.Enable) && input_record.Enable != user_info_after.Enable.ToString())
                        {
                            differences.Add($"[未改] Enable: {user_info_before.Enable} -> {input_record.Enable}");
                        }

                        if (user_info_before.Name != user_info_after.Name)
                        {
                            differences.Add($"[修改] Name: {user_info_before.Name} -> {user_info_after.Name}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Name",
                                Before = user_info_before.Name.ToString(),
                                After = user_info_after.Name.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Name) && input_record.Name != user_info_after.Name.ToString())
                        {
                            differences.Add($"[未改] Name: {user_info_before.Name} -> {input_record.Name}");
                        }

                        if (user_info_before.UserColor != user_info_after.UserColor)
                        {
                            differences.Add($"[修改] Color: {user_info_before.UserColor} -> {user_info_after.UserColor}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Color",
                                Before = user_info_before.UserColor.ToString(),
                                After = user_info_after.UserColor.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Color) && input_record.Color != user_info_after.UserColor.ToString())
                        {
                            differences.Add($"[未改] Color: {user_info_before.UserColor} -> {input_record.Color}");
                        }

                        if (user_info_before.Group != user_info_after.Group)
                        {
                            differences.Add($"[修改] Group: {user_info_before.Group} -> {user_info_after.Group}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Group",
                                Before = user_info_before.Group.ToString(),
                                After = user_info_after.Group.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Group) && input_record.Group != user_info_after.Group.ToString())
                        {
                            differences.Add($"[未改] Group: {user_info_before.Group} -> {input_record.Group}");
                        }

                        if (user_info_before.Leverage != user_info_after.Leverage)
                        {
                            differences.Add($"[修改] Leverage: {user_info_before.Leverage} -> {user_info_after.Leverage}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Leverage",
                                Before = user_info_before.Leverage.ToString(),
                                After = user_info_after.Leverage.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Leverage) && input_record.Leverage != user_info_after.Leverage.ToString())
                        {
                            differences.Add($"[未改] Leverage: {user_info_before.Leverage} -> {input_record.Leverage}");
                        }

                        if (user_info_before.AgentAccount != user_info_after.AgentAccount)
                        {
                            differences.Add($"[修改] AgentAccount: {user_info_before.AgentAccount} -> {user_info_after.AgentAccount}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "AgentAccount",
                                Before = user_info_before.AgentAccount.ToString(),
                                After = user_info_after.AgentAccount.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.AgentAccount) && input_record.AgentAccount != user_info_after.AgentAccount.ToString())
                        {
                            differences.Add($"[未改] AgentAccount: {user_info_before.AgentAccount} -> {input_record.AgentAccount}");
                        }

                        if (user_info_before.Taxes != user_info_after.Taxes)
                        {
                            differences.Add($"[修改] Taxes: {user_info_before.Taxes} -> {user_info_after.Taxes}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Taxes",
                                Before = user_info_before.Taxes.ToString(),
                                After = user_info_after.Taxes.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Taxes) && input_record.Taxes != user_info_after.Taxes.ToString())
                        {
                            differences.Add($"[未改] Taxes: {user_info_before.Taxes} -> {input_record.Taxes}");
                        }

                        if (user_info_before.SendReports != user_info_after.SendReports)
                        {
                            differences.Add($"[修改] SendReports: {user_info_before.SendReports} -> {user_info_after.SendReports}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "SendReports",
                                Before = user_info_before.SendReports.ToString(),
                                After = user_info_after.SendReports.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.SendReports) && input_record.SendReports != user_info_after.SendReports.ToString())
                        {
                            differences.Add($"[未改] SendReports: {user_info_before.SendReports} -> {input_record.SendReports}");
                        }

                        if (user_info_before.Mqid != user_info_after.Mqid)
                        {
                            differences.Add($"[修改] Mqid: {user_info_before.Mqid} -> {user_info_after.Mqid}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Mqid",
                                Before = user_info_before.Mqid.ToString(),
                                After = user_info_after.Mqid.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Mqid) && input_record.Mqid != user_info_after.Mqid.ToString())
                        {
                            differences.Add($"[未改] Mqid: {user_info_before.Mqid} -> {input_record.Mqid}");
                        }

                        if (user_info_before.Status != user_info_after.Status)
                        {
                            differences.Add($"[修改] Status: {user_info_before.Status} -> {user_info_after.Status}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Status",
                                Before = user_info_before.Status.ToString(),
                                After = user_info_after.Status.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Status) && input_record.Status != user_info_after.Status.ToString())
                        {
                            differences.Add($"[未改] Status: {user_info_before.Status} -> {input_record.Status}");
                        }

                        if (user_info_before.Id != user_info_after.Id)
                        {
                            differences.Add($"[修改] Status: {user_info_before.Id} -> {user_info_after.Id}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Id",
                                Before = user_info_before.Id.ToString(),
                                After = user_info_after.Id.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Id) && input_record.Id != user_info_after.Id.ToString())
                        {
                            differences.Add($"[未改] Id: {user_info_before.Id} -> {input_record.Id}");
                        }

                        if (user_info_before.Comment != user_info_after.Comment)
                        {
                            differences.Add($"[修改] Comment: {user_info_before.Comment} -> {user_info_after.Comment}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Comment",
                                Before = user_info_before.Comment.ToString(),
                                After = user_info_after.Comment.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Comment) && input_record.Comment != user_info_after.Comment.ToString())
                        {
                            differences.Add($"[未改] Comment: {user_info_before.Comment} -> {input_record.Comment}");
                        }

                        if (user_info_before.EnableChangePassword != user_info_after.EnableChangePassword)
                        {
                            differences.Add($"[修改] EnableChangePassword: {user_info_before.EnableChangePassword} -> {user_info_after.EnableChangePassword}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "EnableChangePassword",
                                Before = user_info_before.EnableChangePassword.ToString(),
                                After = user_info_after.EnableChangePassword.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.EnableChangePassword) && input_record.EnableChangePassword != user_info_after.EnableChangePassword.ToString())
                        {
                            differences.Add($"[未改] EnableChangePassword: {user_info_before.EnableChangePassword} -> {input_record.EnableChangePassword}");
                        }

                        if (user_info_before.EnableReadOnly != user_info_after.EnableReadOnly)
                        {
                            differences.Add($"[修改] EnableReadOnly: {user_info_before.EnableReadOnly} -> {user_info_after.EnableReadOnly}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "EnableReadOnly",
                                Before = user_info_before.EnableReadOnly.ToString(),
                                After = user_info_after.EnableReadOnly.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.EnableReadOnly) && input_record.EnableReadOnly != user_info_after.EnableReadOnly.ToString())
                        {
                            differences.Add($"[未改] EnableReadOnly: {user_info_before.EnableReadOnly} -> {input_record.EnableReadOnly}");
                        }

                        if (user_info_before.EnableOTP != user_info_after.EnableOTP)
                        {
                            differences.Add($"[修改] EnableOTP: {user_info_before.EnableOTP} -> {user_info_after.EnableOTP}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "EnableOTP",
                                Before = user_info_before.EnableOTP.ToString(),
                                After = user_info_after.EnableOTP.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.EnableOTP) && input_record.EnableOTP != user_info_after.EnableOTP.ToString())
                        {
                            differences.Add($"[未改] EnableOTP: {user_info_before.EnableOTP} -> {input_record.EnableOTP}");
                        }

                        if (user_info_before.PasswordPhone != user_info_after.PasswordPhone)
                        {
                            differences.Add($"[修改] PasswordPhone: {user_info_before.PasswordPhone} -> {user_info_after.PasswordPhone}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "PasswordPhone",
                                Before = user_info_before.PasswordPhone.ToString(),
                                After = user_info_after.PasswordPhone.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.PasswordPhone) && input_record.PasswordPhone != user_info_after.PasswordPhone.ToString())
                        {
                            differences.Add($"[未改] PasswordPhone: {user_info_before.PasswordPhone} -> {input_record.PasswordPhone}");
                        }

                        if (user_info_before.Country != user_info_after.Country)
                        {
                            differences.Add($"[修改] Country: {user_info_before.Country} -> {user_info_after.Country}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Country",
                                Before = user_info_before.Country.ToString(),
                                After = user_info_after.Country.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Country) && input_record.Country != user_info_after.Country.ToString())
                        {
                            differences.Add($"[未改] Country: {user_info_before.Country} -> {input_record.Country}");
                        }

                        if (user_info_before.City != user_info_after.City)
                        {
                            differences.Add($"[修改] City: {user_info_before.City} -> {user_info_after.City}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "City",
                                Before = user_info_before.City.ToString(),
                                After = user_info_after.City.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.City) && input_record.City != user_info_after.City.ToString())
                        {
                            differences.Add($"[未改] City: {user_info_before.City} -> {input_record.City}");
                        }

                        if (user_info_before.State != user_info_after.State)
                        {
                            differences.Add($"[修改] State: {user_info_before.State} -> {user_info_after.State}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "State",
                                Before = user_info_before.State.ToString(),
                                After = user_info_after.State.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.State) && input_record.State != user_info_after.State.ToString())
                        {
                            differences.Add($"[未改] State: {user_info_before.State} -> {input_record.State}");
                        }

                        if (user_info_before.ZipCode != user_info_after.ZipCode)
                        {
                            differences.Add($"[修改] ZipCode: {user_info_before.ZipCode} -> {user_info_after.ZipCode}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "ZipCode",
                                Before = user_info_before.ZipCode.ToString(),
                                After = user_info_after.ZipCode.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.ZipCode) && input_record.ZipCode != user_info_after.ZipCode.ToString())
                        {
                            differences.Add($"[未改] ZipCode: {user_info_before.ZipCode} -> {input_record.ZipCode}");
                        }

                        if (user_info_before.Address != user_info_after.Address)
                        {
                            differences.Add($"[修改] Address: {user_info_before.Address} -> {user_info_after.Address}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Address",
                                Before = user_info_before.Address.ToString(),
                                After = user_info_after.Address.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Address) && input_record.Address != user_info_after.Address.ToString())
                        {
                            differences.Add($"[未改] Address: {user_info_before.Address} -> {input_record.Address}");
                        }

                        if (user_info_before.LeadSource != user_info_after.LeadSource)
                        {
                            differences.Add($"[修改] LeadSource: {user_info_before.LeadSource} -> {user_info_after.LeadSource}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "LeadSource",
                                Before = user_info_before.LeadSource.ToString(),
                                After = user_info_after.LeadSource.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.LeadSource) && input_record.LeadSource != user_info_after.LeadSource.ToString())
                        {
                            differences.Add($"[未改] LeadSource: {user_info_before.LeadSource} -> {input_record.LeadSource}");
                        }

                        if (user_info_before.Phone != user_info_after.Phone)
                        {
                            differences.Add($"[修改] Phone: {user_info_before.Phone} -> {user_info_after.Phone}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Phone",
                                Before = user_info_before.Phone.ToString(),
                                After = user_info_after.Phone.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Phone) && input_record.Phone != user_info_after.Phone.ToString())
                        {
                            differences.Add($"[未改] Phone: {user_info_before.Phone} -> {input_record.Phone}");
                        }

                        if (user_info_before.Email != user_info_after.Email)
                        {
                            differences.Add($"[修改] Email: {user_info_before.Email} -> {user_info_after.Email}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = user_info_before.Login,
                                Item = "Email",
                                Before = user_info_before.Email.ToString(),
                                After = user_info_after.Email.ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Email) && input_record.Email != user_info_after.Email.ToString())
                        {
                            differences.Add($"[未改] Email: {user_info_before.Email} -> {input_record.Email}");
                        }

                        // 給User看的比對結果
                        log.Difference = differences;
                    }
                }

                AIROE.Disconnect();
            }

            return (all_log_list, sql_log_list);
        }

        // MT5 執行流程
        private static (List<log_record>, List<sql_record>) MT5_API()
        {
            List<log_record> all_log_list = new();
            List<sql_record> sql_log_list = new();
            var (inputRecords, loginRecords) = Read_Input_MT5();

            string _rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            SMTManagerAPIFactory.Initialize(_rootPath);
            var manager = SMTManagerAPIFactory.CreateManager(SMTManagerAPIFactory.ManagerAPIVersion, out var _);

            foreach (var item in inputRecords)
            {
                var server = item.Key;
                List<CIMTUser> all_user_update_before = new();
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
                    all_log_list.Add(new log_record
                    {
                        Server = server,
                        Result = ex.Message
                    });
                    continue;
                }

                // 取出要改的LOGIN原本的設置
                List<CIMTUser> all_login = new();
                try
                {
                    var user_array = manager.UserCreateArray();
                    ulong[] logins = item.Value.Select(x => ulong.Parse(x.Login)).ToArray();
                    if (manager.UserRequestByLogins(logins, user_array) != 0)
                    {
                        throw new Exception("取得帳號原設置時發生錯誤");
                    }
                    manager.UserRequestByLogins(logins, user_array);
                    all_login = user_array.ToArray().ToList();
                }
                catch
                {
                    all_log_list.Add(new log_record
                    {
                        Server = server,
                        Result = "讀取該Server的資料時發生錯誤"
                    });
                    continue;
                }

                foreach (var input_record in item.Value)
                {
                    // 每行資料單個LOG
                    log_record log_record = new();
                    log_record.Server = server;
                    log_record.Login = input_record.Login;
                    try
                    {
                        // 檢查是否有該Login
                        if (all_login.Any(record => record.Login() == ulong.Parse(input_record.Login)))
                        {
                            // 取出record
                            var user_record_after = all_login.FirstOrDefault(record => record.Login() == ulong.Parse(input_record.Login));

                            // Rights: 無預設
                            user_record_after.Rights(!string.IsNullOrEmpty(input_record.Rights) ? (CIMTUser.EnUsersRights)Convert.ToDouble(input_record.Rights) : user_record_after.Rights());
                            // Group: 無預設
                            user_record_after.Group(!string.IsNullOrEmpty(input_record.Group) ? input_record.Group : user_record_after.Group());
                            // Leverage
                            if (!string.IsNullOrEmpty(input_record.Leverage))
                            {
                                if (uint.TryParse(input_record.Leverage, out var leverage))
                                {
                                    if (leverage > 1000 || leverage < 1)
                                    {
                                        throw new Exception("警告：Leverage 僅能設置 1 ~ 1000");
                                    }
                                    else
                                    {
                                        user_record_after.Leverage(leverage);
                                    }
                                }
                                else
                                {
                                    throw new Exception("警告： 無法辨別 Leverage 設置");
                                }
                            }

                            // Name: 空白
                            if (!string.IsNullOrEmpty(input_record.Name))
                            {
                                if (input_record.Name.Length > 127)
                                {
                                    throw new Exception("警告：Name 的字元不可超過 127");
                                }
                                else
                                {
                                    if (input_record.Name == "null")
                                    {
                                        user_record_after.Name("");
                                    }
                                    else
                                    {
                                        user_record_after.Name(input_record.Name);
                                    }
                                }                                
                            }

                            // Color
                            if (!string.IsNullOrEmpty(input_record.Color))
                            {
                                if (input_record.Color == "null")
                                {
                                    user_record_after.Color(4278190080);
                                }
                                else if (TryParseRGB(input_record.Color, out int r_color, out int g_color, out int b_color))
                                {
                                    user_record_after.Color((uint)(r_color + g_color * 256 + b_color * 256 * 256));
                                }
                                else
                                {
                                    throw new Exception("警告：Color的格式有誤，例：R,G,B，0~255");
                                }
                            }

                            // Company: 空白
                            if (!string.IsNullOrEmpty(input_record.Company))
                            {
                                if (input_record.Company == "null")
                                {
                                    user_record_after.Company("");
                                }
                                else
                                {
                                    if (input_record.Company.Length > 63)
                                    {
                                        throw new Exception("警告：Company 的字元不可超過 63");
                                    }
                                    else
                                    {
                                        user_record_after.Company(input_record.Company);
                                    }
                                }
                            }
                            // ID_number: 空白
                            if (!string.IsNullOrEmpty(input_record.ID_number))
                            {
                                if (input_record.ID_number == "null")
                                {
                                    user_record_after.ID("");
                                }
                                else
                                {
                                    if (input_record.ID_number.Length > 31)
                                    {
                                        throw new Exception("警告：ID_number 的字元不可超過 31");
                                    }
                                    else
                                    {
                                        user_record_after.ID(input_record.ID_number);
                                    }
                                }
                            }
                            // Status: 空白
                            if (!string.IsNullOrEmpty(input_record.Status))
                            {
                                if (input_record.Status == "null")
                                {
                                    user_record_after.Status("");
                                }
                                else
                                {
                                    if (input_record.Status.Length > 15)
                                    {
                                        throw new Exception("警告：Status 的字元不可超過 15");
                                    }
                                    else
                                    {
                                        user_record_after.Status(input_record.Status);
                                    }
                                }
                            }
                            // Lead_campaign: 空白
                            if (!string.IsNullOrEmpty(input_record.Lead_campaign))
                            {
                                if (input_record.Lead_campaign == "null")
                                {
                                    user_record_after.LeadCampaign("");
                                }
                                else
                                {
                                    if (input_record.Lead_campaign.Length > 63)
                                    {
                                        throw new Exception("警告：Lead_campaign 的字元不可超過 63");
                                    }
                                    else
                                    {
                                        user_record_after.LeadCampaign(input_record.Lead_campaign);
                                    }
                                }
                            }
                            // Lead_source: 空白
                            if (!string.IsNullOrEmpty(input_record.Lead_source))
                            {
                                if (input_record.Lead_source == "null")
                                {
                                    user_record_after.LeadSource("");
                                }
                                else
                                {
                                    if (input_record.Lead_source.Length > 63)
                                    {
                                        throw new Exception("警告：Lead_source 的字元不可超過 63");
                                    }
                                    else
                                    {
                                        user_record_after.LeadSource(input_record.Lead_source);
                                    }
                                }
                            }
                            // EMail: 空白
                            if (!string.IsNullOrEmpty(input_record.Email))
                            {
                                if (input_record.Email == "null")
                                {
                                    user_record_after.EMail("");
                                }
                                else
                                {
                                    if (input_record.Email.Length > 63)
                                    {
                                        throw new Exception("警告：Email 的字元不可超過 63");
                                    }
                                    else
                                    {
                                        user_record_after.EMail(input_record.Email);
                                    }
                                }
                            }
                            // Phone: 空白
                            if (!string.IsNullOrEmpty(input_record.Phone))
                            {
                                if (input_record.Phone == "null")
                                {
                                    user_record_after.Phone("");
                                }
                                else
                                {
                                    if (input_record.Phone.Length > 31)
                                    {
                                        throw new Exception("警告：Phone 的字元不可超過 31");
                                    }
                                    else
                                    {
                                        user_record_after.Phone(input_record.Phone);
                                    }
                                }
                            }
                            // Country: 空白
                            if (!string.IsNullOrEmpty(input_record.Country))
                            {
                                if (input_record.Country == "null")
                                {
                                    user_record_after.Country("");
                                }
                                else
                                {
                                    if (input_record.Country.Length > 63)
                                    {
                                        throw new Exception("警告：Country 的字元不可超過 63");
                                    }
                                    else
                                    {
                                        user_record_after.Country(input_record.Country);
                                    }
                                }
                            }
                            // City: 空白
                            if (!string.IsNullOrEmpty(input_record.City))
                            {
                                if (input_record.City == "null")
                                {
                                    user_record_after.City("");
                                }
                                else
                                {
                                    if (input_record.City.Length > 63)
                                    {
                                        throw new Exception("警告：City 的字元不可超過 63");
                                    }
                                    else
                                    {
                                        user_record_after.City(input_record.City);
                                    }
                                }
                            }
                            // State: 空白
                            if (!string.IsNullOrEmpty(input_record.State))
                            {
                                if (input_record.State == "null")
                                {
                                    user_record_after.State("");
                                }
                                else
                                {
                                    if (input_record.State.Length > 63)
                                    {
                                        throw new Exception("警告：State 的字元不可超過 63");
                                    }
                                    else
                                    {
                                        user_record_after.State(input_record.State);
                                    }
                                }
                            }
                            // Zip_code: 空白
                            if (!string.IsNullOrEmpty(input_record.Zip_code))
                            {
                                if (input_record.Zip_code == "null")
                                {
                                    user_record_after.ZIPCode("");
                                }
                                else
                                {
                                    if (input_record.Zip_code.Length > 15)
                                    {
                                        throw new Exception("警告：Zip_code 的字元不可超過 15");
                                    }
                                    else
                                    {
                                        user_record_after.ZIPCode(input_record.Zip_code);
                                    }
                                }
                            }
                            // Address: 空白
                            if (!string.IsNullOrEmpty(input_record.Address))
                            {
                                if (input_record.Address == "null")
                                {
                                    user_record_after.Address("");
                                }
                                else
                                {
                                    if (input_record.Address.Length > 127)
                                    {
                                        throw new Exception("警告：Address 的字元不可超過 127");
                                    }
                                    else
                                    {
                                        user_record_after.Address(input_record.Address);
                                    }
                                }
                            }
                            // Comment: 空白
                            if (!string.IsNullOrEmpty(input_record.Comment))
                            {
                                if (input_record.Comment == "null")
                                {
                                    user_record_after.Comment("");
                                }
                                else
                                {
                                    if (input_record.Comment.Length > 63)
                                    {
                                        throw new Exception("警告：Comment 的字元不可超過 63");
                                    }
                                    else
                                    {
                                        user_record_after.Comment(input_record.Comment);
                                    }
                                }
                            }
                            // Bank_Account: 空白
                            if (!string.IsNullOrEmpty(input_record.Bank_Account))
                            {
                                if (input_record.Bank_Account == "null")
                                {
                                    user_record_after.Account("");
                                }
                                else
                                {
                                    if (input_record.Bank_Account.Length > 31)
                                    {
                                        throw new Exception("警告：Bank_Account 的字元不可超過 31");
                                    }
                                    else
                                    {
                                        user_record_after.Account(input_record.Bank_Account);
                                    }
                                }
                            }
                            // Agent_account: 0(空白)
                            if (!string.IsNullOrEmpty(input_record.Agent_account))
                            {
                                if (ulong.TryParse(input_record.Agent_account, out var acc))
                                {
                                    user_record_after.Agent(acc);
                                }
                                else
                                {
                                    throw new Exception("警告：無法辨別 Agent_account 設置");
                                }
                            }
                            // LimitPositionsValue: 0(預設)
                            if (!string.IsNullOrEmpty(input_record.LimitPositionsValue))
                            {
                                if (double.TryParse(input_record.LimitPositionsValue, out var _value))
                                {
                                    if (_value < 0)
                                    {
                                        throw new Exception("警告：LimitPositionsValue 不可為負");
                                    }
                                    user_record_after.LimitPositionsValue(_value);
                                }
                                else
                                {
                                    throw new Exception("警告：無法辨別 LimitPositionsValue 設置");
                                }
                            }
                            // Registration: 無預設
                            //string[] formats = { "yyyy.MM.dd HH:mm:ss" };
                            //if (!string.IsNullOrEmpty(input_record.Registration))
                            //{
                            //    // 轉換timestamp
                            //    double timestamp = (DateTime.ParseExact(input_record.Registration, formats, CultureInfo.InvariantCulture) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                            //    var res_setReg = user_record_after.RegistrationSet((long)timestamp);
                            //};

                            // 更新前先撈一次做比對用
                            var user_record_before = manager.UserCreate();
                            if (manager.UserRequest(ulong.Parse(input_record.Login), user_record_before) != 0)
                            {
                                throw new Exception("執行API時發生問題");
                            }
                            all_user_update_before.Add(user_record_before); // 用於最後比對用

                            // 簡單比對: 有不一樣的就update
                            List<string> differences = new();
                            void AddDifference<T>(string propertyName, Func<T> getterBefore, Func<T> getterAfter)
                            {
                                var valueBefore = getterBefore();
                                var valueAfter = getterAfter();
                                if (!EqualityComparer<T>.Default.Equals(valueBefore, valueAfter))
                                {
                                    differences.Add($"[修改] {propertyName}: {valueBefore} -> {valueAfter}");
                                }
                            }
                            AddDifference("Rights", user_record_before.Rights, user_record_after.Rights);
                            AddDifference("Group", user_record_before.Group, user_record_after.Group);
                            AddDifference("Leverage", user_record_before.Leverage, user_record_after.Leverage);
                            AddDifference("Name", user_record_before.Name, user_record_after.Name);
                            AddDifference("Color", user_record_before.Color, user_record_after.Color);
                            //AddDifference("FirstName", user_record_before.FirstName, user_record_after.FirstName);
                            //AddDifference("MiddleName", user_record_before.MiddleName, user_record_after.MiddleName);
                            //AddDifference("LastName", user_record_before.LastName, user_record_after.LastName);
                            AddDifference("Company", user_record_before.Company, user_record_after.Company);
                            // Registration[START]
                            //if (user_record_before.Registration() != user_record_after.Registration())
                            //{
                            //    var timestamp_before = DateTimeOffset.FromUnixTimeSeconds(user_record_before.Registration()).UtcDateTime.ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);
                            //    var timestamp_after = DateTimeOffset.FromUnixTimeSeconds(user_record_after.Registration()).UtcDateTime.ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);
                            //    differences.Add($"[修改] Registration: {timestamp_before} -> {timestamp_after}");
                            //}
                            // Registration[END]
                            AddDifference("ID_number", user_record_before.ID, user_record_after.ID);
                            AddDifference("Status", user_record_before.Status, user_record_after.Status);
                            AddDifference("Lead_campaign", user_record_before.LeadCampaign, user_record_after.LeadCampaign);
                            AddDifference("Lead_source", user_record_before.LeadSource, user_record_after.LeadSource);
                            AddDifference("Email", user_record_before.EMail, user_record_after.EMail);
                            AddDifference("Phone", user_record_before.Phone, user_record_after.Phone);
                            AddDifference("Country", user_record_before.Country, user_record_after.Country);
                            AddDifference("City", user_record_before.City, user_record_after.City);
                            AddDifference("State", user_record_before.State, user_record_after.State);
                            AddDifference("Zip_code", user_record_before.ZIPCode, user_record_after.ZIPCode);
                            AddDifference("Address", user_record_before.Address, user_record_after.Address);
                            AddDifference("Comment", user_record_before.Comment, user_record_after.Comment);
                            AddDifference("Bank_Account", user_record_before.Account, user_record_after.Account);
                            AddDifference("Agent_account", user_record_before.Agent, user_record_after.Agent);
                            AddDifference("LimitPositionsValue", user_record_before.LimitPositionsValue, user_record_after.LimitPositionsValue);

                            if (differences.Count > 0)
                            {
                                var res = manager.UserUpdate(user_record_after);
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
                            throw new Exception("找不到該Login");
                        }
                    }
                    catch (Exception ex)
                    {
                        log_record.Difference = null;
                        log_record.Result = ex.Message;
                    }
                    finally
                    {
                        all_log_list.Add(log_record);
                    }
                }

                // Update後的比對
                List<CIMTUser> all_user_update_after = new();
                try
                {
                    var user_array = manager.UserCreateArray();
                    ulong[] logins = all_user_update_before.Select(user => user.Login()).ToArray();
                    if (manager.UserRequestByLogins(logins, user_array) != 0)
                    {
                        throw new Exception("取得帳號原設置時發生錯誤");
                    }
                    manager.UserRequestByLogins(logins, user_array);
                    all_user_update_after = user_array.ToArray().ToList();
                }
                catch
                {
                    all_log_list.Add(new log_record
                    {
                        Server = server,
                        Result = "讀取該Server的資料時發生錯誤"
                    });
                    continue;
                }

                foreach (var input_record in item.Value)
                {
                    var log = all_log_list.FirstOrDefault(record => record.Login == input_record.Login.ToString() && record.Server == server);

                    // 檢查API Update成功的
                    if (log.Result == "執行成功")
                    {
                        var user_info_before = all_user_update_before.FirstOrDefault(record => record.Login().ToString() == input_record.Login);
                        var time_set = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        var user_info_after = all_user_update_after.FirstOrDefault(user => user.Login() == user_info_before.Login());
                        List<string> differences = new();
                        if (user_info_before.Group() != user_info_after.Group())
                        {
                            differences.Add($"[修改] Group: {user_info_before.Group()} -> {user_info_after.Group()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Group",
                                Before = user_info_before.Group(),
                                After = user_info_after.Group(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Group) && input_record.Group != user_info_after.Group().ToString())
                        {
                            differences.Add($"[未改] Group: {user_info_before.Group()} -> {input_record.Group}");
                        }

                        if (user_info_before.Rights() != user_info_after.Rights())
                        {
                            differences.Add($"[修改] Rights: {user_info_before.Rights()} -> {user_info_after.Rights()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Rights",
                                Before = user_info_before.Rights().ToString(),
                                After = user_info_after.Rights().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Rights) && input_record.Rights != user_info_after.Rights().ToString())
                        {
                            differences.Add($"[未改] Rights: {user_info_before.Rights()} -> {input_record.Rights}");
                        }

                        if (user_info_before.Color() != user_info_after.Color())
                        {
                            differences.Add($"[修改] Color: {user_info_before.Color()} -> {user_info_after.Color()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Color",
                                Before = user_info_before.Color().ToString(),
                                After = user_info_after.Color().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Color) && input_record.Color != user_info_after.Color().ToString())
                        {
                            differences.Add($"[未改] Color: {user_info_before.Color()} -> {input_record.Color}");
                        }

                        if (user_info_before.Leverage() != user_info_after.Leverage())
                        {
                            differences.Add($"[修改] Leverage: {user_info_before.Leverage()} -> {user_info_after.Leverage()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Leverage",
                                Before = user_info_before.Leverage().ToString(),
                                After = user_info_after.Leverage().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Leverage) && input_record.Leverage != user_info_after.Leverage().ToString())
                        {
                            differences.Add($"[未改] Leverage: {user_info_before.Leverage()} -> {input_record.Leverage}");
                        }

                        if (user_info_before.Name() != user_info_after.Name())
                        {
                            differences.Add($"[修改] Name: {user_info_before.Name()} -> {user_info_after.Name()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Name",
                                Before = user_info_before.Name().ToString(),
                                After = user_info_after.Name().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Name) && input_record.Name != user_info_after.Name().ToString())
                        {
                            differences.Add($"[未改] Name: {user_info_before.Name()} -> {input_record.Name}");
                        }

                        //if (user_info_before.FirstName() != user_info_after.FirstName())
                        //{
                        //    differences.Add($"[修改] FirstName: {user_info_before.FirstName()} -> {user_info_after.FirstName()}");
                        //    sql_log_list.Add(new sql_record
                        //    {
                        //        Server = server,
                        //        Login = (int)user_info_before.Login(),
                        //        Item = "FirstName",
                        //        Before = user_info_before.FirstName().ToString(),
                        //        After = user_info_after.FirstName().ToString(),
                        //        UserLogin = server_config.Login,
                        //        Time = time_set
                        //    });
                        //}
                        //else if (!string.IsNullOrEmpty(input_record.FirstName) && input_record.FirstName != user_info_after.FirstName().ToString())
                        //{
                        //    differences.Add($"[未改] FirstName: {user_info_before.FirstName()} -> {input_record.FirstName}");
                        //}

                        //if (user_info_before.LastName() != user_info_after.LastName())
                        //{
                        //    differences.Add($"[修改] LastName: {user_info_before.LastName()} -> {user_info_after.LastName()}");
                        //    sql_log_list.Add(new sql_record
                        //    {
                        //        Server = server,
                        //        Login = (int)user_info_before.Login(),
                        //        Item = "LastName",
                        //        Before = user_info_before.LastName().ToString(),
                        //        After = user_info_after.LastName().ToString(),
                        //        UserLogin = server_config.Login,
                        //        Time = time_set
                        //    });
                        //}
                        //else if (!string.IsNullOrEmpty(input_record.LastName) && input_record.LastName != user_info_after.LastName().ToString())
                        //{
                        //    differences.Add($"[未改] LastName: {user_info_before.LastName()} -> {input_record.LastName}");
                        //}

                        //if (user_info_before.MiddleName() != user_info_after.MiddleName())
                        //{
                        //    differences.Add($"[修改] MiddleName: {user_info_before.MiddleName()} -> {user_info_after.MiddleName()}");
                        //    sql_log_list.Add(new sql_record
                        //    {
                        //        Server = server,
                        //        Login = (int)user_info_before.Login(),
                        //        Item = "MiddleName",
                        //        Before = user_info_before.MiddleName().ToString(),
                        //        After = user_info_after.MiddleName().ToString(),
                        //        UserLogin = server_config.Login,
                        //        Time = time_set
                        //    });
                        //}
                        //else if (!string.IsNullOrEmpty(input_record.MiddleName) && input_record.MiddleName != user_info_after.MiddleName().ToString())
                        //{
                        //    differences.Add($"[未改] MiddleName: {user_info_before.MiddleName()} -> {input_record.MiddleName}");
                        //}

                        if (user_info_before.Company() != user_info_after.Company())
                        {
                            differences.Add($"[修改] Company: {user_info_before.Company()} -> {user_info_after.Company()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Company",
                                Before = user_info_before.Company().ToString(),
                                After = user_info_after.Company().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Company) && input_record.Company != user_info_after.Company().ToString())
                        {
                            differences.Add($"[未改] Company: {user_info_before.Company()} -> {input_record.Company}");
                        }

                        //if (user_info_before.Registration() != user_info_after.Registration())
                        //{
                        //    var timestamp_before = DateTimeOffset.FromUnixTimeSeconds(user_info_before.Registration()).UtcDateTime.ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);
                        //    var timestamp_after = DateTimeOffset.FromUnixTimeSeconds(user_info_after.Registration()).UtcDateTime.ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);

                        //    differences.Add($"[修改] Registration: {timestamp_before} -> {timestamp_after}");
                        //    sql_log_list.Add(new sql_record
                        //    {
                        //        Server = server,
                        //        Login = (int)user_info_before.Login(),
                        //        Item = "Registration",
                        //        Before = timestamp_before,
                        //        After = timestamp_after,
                        //        UserLogin = server_config.Login,
                        //        Time = time_set
                        //    });
                        //}
                        //else if (!string.IsNullOrEmpty(input_record.Registration) && input_record.Registration != user_info_after.Registration().ToString())
                        //{
                        //    var timestamp_before = DateTimeOffset.FromUnixTimeSeconds(user_info_before.Registration()).UtcDateTime.ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);
                        //    var timestamp_after = DateTimeOffset.FromUnixTimeSeconds(long.Parse(input_record.Registration)).UtcDateTime.ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);
                        //    differences.Add($"[未改] Registration: {timestamp_before} -> {timestamp_after}");
                        //}

                        if (user_info_before.ID() != user_info_after.ID())
                        {
                            differences.Add($"[修改] ID_number: {user_info_before.ID()} -> {user_info_after.ID()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "ID_number",
                                Before = user_info_before.ID().ToString(),
                                After = user_info_after.ID().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.ID_number) && input_record.ID_number != user_info_after.ID().ToString())
                        {
                            differences.Add($"[未改] ID_number: {user_info_before.ID()} -> {input_record.ID_number}");
                        }

                        if (user_info_before.Status() != user_info_after.Status())
                        {
                            differences.Add($"[修改] Status: {user_info_before.Status()} -> {user_info_after.Status()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Status",
                                Before = user_info_before.Status().ToString(),
                                After = user_info_after.Status().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Status) && input_record.Status != user_info_after.Status().ToString())
                        {
                            differences.Add($"[未改] Status: {user_info_before.Status()} -> {input_record.Status}");
                        }

                        if (user_info_before.LeadCampaign() != user_info_after.LeadCampaign())
                        {
                            differences.Add($"[修改] Lead_campaign: {user_info_before.LeadCampaign()} -> {user_info_after.LeadCampaign()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Lead_campaign",
                                Before = user_info_before.LeadCampaign().ToString(),
                                After = user_info_after.LeadCampaign().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Lead_campaign) && input_record.Lead_campaign != user_info_after.LeadCampaign().ToString())
                        {
                            differences.Add($"[未改] Lead_campaign: {user_info_before.LeadCampaign()} -> {input_record.Lead_campaign}");
                        }

                        if (user_info_before.LeadSource() != user_info_after.LeadSource())
                        {
                            differences.Add($"[修改] Lead_source: {user_info_before.LeadSource()} -> {user_info_after.LeadSource()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Lead_Source",
                                Before = user_info_before.LeadSource().ToString(),
                                After = user_info_after.LeadSource().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Lead_source) && input_record.Lead_source != user_info_after.LeadSource().ToString())
                        {
                            differences.Add($"[未改] Lead_source: {user_info_before.LeadSource()} -> {input_record.Lead_source}");
                        }

                        if (user_info_before.EMail() != user_info_after.EMail())
                        {
                            differences.Add($"[修改] EMail: {user_info_before.EMail()} -> {user_info_after.EMail()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "EMail",
                                Before = user_info_before.EMail().ToString(),
                                After = user_info_after.EMail().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Email) && input_record.Email != user_info_after.EMail().ToString())
                        {
                            differences.Add($"[未改] Email: {user_info_before.EMail()} -> {input_record.Email}");
                        }

                        if (user_info_before.Phone() != user_info_after.Phone())
                        {
                            differences.Add($"[修改] Phone: {user_info_before.Phone()} -> {user_info_after.Phone()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Phone",
                                Before = user_info_before.Phone().ToString(),
                                After = user_info_after.Phone().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Phone) && input_record.Phone != user_info_after.Phone().ToString())
                        {
                            differences.Add($"[未改] Phone: {user_info_before.Phone()} -> {input_record.Phone}");
                        }

                        if (user_info_before.Country() != user_info_after.Country())
                        {
                            differences.Add($"[修改] Country: {user_info_before.Country()} -> {user_info_after.Country()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Country",
                                Before = user_info_before.Country().ToString(),
                                After = user_info_after.Country().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Country) && input_record.Country != user_info_after.Country().ToString())
                        {
                            differences.Add($"[未改] Country: {user_info_before.Country()} -> {input_record.Country}");
                        }

                        if (user_info_before.City() != user_info_after.City())
                        {
                            differences.Add($"[修改] City: {user_info_before.City()} -> {user_info_after.City()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "City",
                                Before = user_info_before.City().ToString(),
                                After = user_info_after.City().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.City) && input_record.City != user_info_after.City().ToString())
                        {
                            differences.Add($"[未改] City: {user_info_before.City()} -> {input_record.City}");
                        }

                        if (user_info_before.State() != user_info_after.State())
                        {
                            differences.Add($"[修改] State: {user_info_before.State()} -> {user_info_after.State()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "State",
                                Before = user_info_before.State().ToString(),
                                After = user_info_after.State().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.State) && input_record.State != user_info_after.State().ToString())
                        {
                            differences.Add($"[未改] State: {user_info_before.State()} -> {input_record.State}");
                        }

                        if (user_info_before.ZIPCode() != user_info_after.ZIPCode())
                        {
                            differences.Add($"[修改] Zip_code: {user_info_before.ZIPCode()} -> {user_info_after.ZIPCode()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Zip_code",
                                Before = user_info_before.ZIPCode().ToString(),
                                After = user_info_after.ZIPCode().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Zip_code) && input_record.Zip_code != user_info_after.ZIPCode().ToString())
                        {
                            differences.Add($"[未改] Zip_code: {user_info_before.ZIPCode()} -> {input_record.Zip_code}");
                        }

                        if (user_info_before.Address() != user_info_after.Address())
                        {
                            differences.Add($"[修改] Address: {user_info_before.Address()} -> {user_info_after.Address()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Address",
                                Before = user_info_before.Address().ToString(),
                                After = user_info_after.Address().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Address) && input_record.Address != user_info_after.Address().ToString())
                        {
                            differences.Add($"[未改] Address: {user_info_before.Address()} -> {input_record.Address}");
                        }

                        if (user_info_before.Comment() != user_info_after.Comment())
                        {
                            differences.Add($"[修改] Comment: {user_info_before.Comment()} -> {user_info_after.Comment()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Comment",
                                Before = user_info_before.Comment().ToString(),
                                After = user_info_after.Comment().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Comment) && input_record.Comment != user_info_after.Comment().ToString())
                        {
                            differences.Add($"[未改] Comment: {user_info_before.Comment()} -> {input_record.Comment}");
                        }

                        if (user_info_before.Account() != user_info_after.Account())
                        {
                            differences.Add($"[修改] Bank_Account: {user_info_before.Account()} -> {user_info_after.Account()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Bank_Account",
                                Before = user_info_before.Account().ToString(),
                                After = user_info_after.Account().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Bank_Account) && input_record.Bank_Account != user_info_after.Account().ToString())
                        {
                            differences.Add($"[未改] Bank_Account: {user_info_before.Account()} -> {input_record.Bank_Account}");
                        }

                        if (user_info_before.Agent() != user_info_after.Agent())
                        {
                            differences.Add($"[修改] Agent: {user_info_before.Agent()} -> {user_info_after.Agent()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "Agent_Account",
                                Before = user_info_before.Agent().ToString(),
                                After = user_info_after.Agent().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.Agent_account) && input_record.Agent_account != user_info_after.Agent().ToString())
                        {
                            differences.Add($"[未改] Agent_account: {user_info_before.Agent()} -> {input_record.Agent_account}");
                        }

                        if (user_info_before.LimitPositionsValue() != user_info_after.LimitPositionsValue())
                        {
                            differences.Add($"[修改] LimitPositionsValue: {user_info_before.LimitPositionsValue()} -> {user_info_after.LimitPositionsValue()}");
                            sql_log_list.Add(new sql_record
                            {
                                Server = server,
                                Login = (int)user_info_before.Login(),
                                Item = "LimitPositionsValue",
                                Before = user_info_before.LimitPositionsValue().ToString(),
                                After = user_info_after.LimitPositionsValue().ToString(),
                                UserLogin = server_config.Login,
                                Time = time_set
                            });
                        }
                        else if (!string.IsNullOrEmpty(input_record.LimitPositionsValue) && input_record.LimitPositionsValue != user_info_after.LimitPositionsValue().ToString())
                        {
                            differences.Add($"[未改] LimitPositionsValue: {user_info_before.LimitPositionsValue()} -> {input_record.LimitPositionsValue}");
                        }

                        // 給User看的比對結果
                        log.Difference = differences;
                    }
                }

                manager.Disconnect();
            }
            return (all_log_list, sql_log_list);
        }

        private static T DeepCopy<T>(T obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<T>(json);
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
                                Login = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Login") + 1].Text,
                                Enable = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Enable") + 1].Text,
                                Name = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Name") + 1].Text,
                                Color = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Color") + 1].Text,
                                Group = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Group") + 1].Text,
                                Leverage = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Leverage") + 1].Text,
                                AgentAccount = worksheet_input.Cells[row, Array.IndexOf(headerRow, "AgentAccount") + 1].Text,
                                Taxes = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Taxes") + 1].Text,
                                SendReports = worksheet_input.Cells[row, Array.IndexOf(headerRow, "SendReports") + 1].Text,
                                //Mqid = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Mqid") + 1].Text,
                                Status = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Status") + 1].Text,
                                Id = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Id") + 1].Text,
                                Comment = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Comment") + 1].Text,
                                EnableChangePassword = worksheet_input.Cells[row, Array.IndexOf(headerRow, "EnableChangePassword") + 1].Text,
                                EnableReadOnly = worksheet_input.Cells[row, Array.IndexOf(headerRow, "EnableReadOnly") + 1].Text,
                                EnableOTP = worksheet_input.Cells[row, Array.IndexOf(headerRow, "EnableOTP") + 1].Text,
                                PasswordPhone = worksheet_input.Cells[row, Array.IndexOf(headerRow, "PasswordPhone") + 1].Text,
                                Country = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Country") + 1].Text,
                                City = worksheet_input.Cells[row, Array.IndexOf(headerRow, "City") + 1].Text,
                                State = worksheet_input.Cells[row, Array.IndexOf(headerRow, "State") + 1].Text,
                                ZipCode = worksheet_input.Cells[row, Array.IndexOf(headerRow, "ZipCode") + 1].Text,
                                Address = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Address") + 1].Text,
                                LeadSource = worksheet_input.Cells[row, Array.IndexOf(headerRow, "LeadSource") + 1].Text,
                                Phone = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Phone") + 1].Text,
                                Email = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Email") + 1].Text,
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
                                Login = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Login") + 1].Text,
                                Group = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Group") + 1].Text,
                                Rights = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Rights") + 1].Text,
                                Leverage = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Leverage") + 1].Text,
                                Name = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Name") + 1].Text,
                                Color = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Color") + 1].Text,
                                //LastName = worksheet_input.Cells[row, Array.IndexOf(headerRow, "LastName") + 1].Text,
                                //MiddleName = worksheet_input.Cells[row, Array.IndexOf(headerRow, "MiddleName") + 1].Text,
                                //FirstName = worksheet_input.Cells[row, Array.IndexOf(headerRow, "FirstName") + 1].Text,
                                Company = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Company") + 1].Text,
                                //Registration = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Registration") + 1].Text,
                                ID_number = worksheet_input.Cells[row, Array.IndexOf(headerRow, "ID_number") + 1].Text,
                                Status = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Status") + 1].Text,
                                Lead_campaign = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Lead_campaign") + 1].Text,
                                Lead_source = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Lead_source") + 1].Text,
                                Email = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Email") + 1].Text,
                                Phone = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Phone") + 1].Text,
                                Country = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Country") + 1].Text,
                                City = worksheet_input.Cells[row, Array.IndexOf(headerRow, "City") + 1].Text,
                                State = worksheet_input.Cells[row, Array.IndexOf(headerRow, "State") + 1].Text,
                                Zip_code = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Zip_code") + 1].Text,
                                Address = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Address") + 1].Text,
                                Comment = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Comment") + 1].Text,
                                Bank_Account = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Bank_Account") + 1].Text,
                                Agent_account = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Agent_account") + 1].Text,
                                LimitPositionsValue = worksheet_input.Cells[row, Array.IndexOf(headerRow, "LimitPositionsValue") + 1].Text,
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

        // 更新紀錄寫進資料庫
        public static void InsertLogRecordsToDatabase(List<sql_record> tool_Log)
        {
            Initiallize();
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    INSERT INTO admin_tool_log.edit_account (
                        Server, Login, Item, `Before`, `After`, UserLogin, Time
                    ) VALUES (
                        @Server, @Login, @Item, @Before, @After, @UserLogin, @Time
                    );";

                foreach (var logRecord in tool_Log)
                {
                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Server", logRecord.Server);
                        cmd.Parameters.AddWithValue("@Login", logRecord.Login);
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
                        FROM admin_tool_log.edit_account
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

                string fileName = "批量修改帳號資訊HistoryLog.xlsx";
                var stream = new MemoryStream(package.GetAsByteArray());

                return (stream, fileName);
            }
        }

        // RGB格式檢查
        public static bool TryParseRGB(string s, out int r, out int g, out int b)
        {
            r = g = b = 0;
            string[] parts = s.Split(',');

            if (parts.Length != 3)
                return false;

            bool R_check = int.TryParse(parts[0], out r);
            bool G_check = int.TryParse(parts[1], out g);
            bool B_check = int.TryParse(parts[2], out b);

            if (!R_check || !G_check || !B_check)
                return false;

            if (r < 0 || r > 255 || g < 0 || g > 255 || b < 0 || b > 255)
                return false;

            return true;
        }
    }
}
