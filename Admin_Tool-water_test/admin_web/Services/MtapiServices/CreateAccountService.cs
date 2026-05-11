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
using System.Text;
using System.Threading;
using static admin_web.Models.Mtapiuse.CreateAccount_Model;
using static admin_web.Models.ServerModel;

namespace admin_web.Services.MtapiServices
{
    public class CreateAccountService
    {
        // variable
        private static Dictionary<string, ServerRecord> server_dict = new(); // MT連線資訊
        private static IFormFile InputFile; // 使用者上傳檔
        private static readonly string connectionString = UniversalService.sql_connectionString;

        private static void Initiallize()
        {
            GetAllServerIP();
        }

        public static List<log_record> MainProgram(IFormFile file)
        {
            Initiallize();
            InputFile = file;
            List<log_record> all_log = new();

            var mt4_log = MT4_API();
            all_log.AddRange(mt4_log);

            var mt5_log = MT5_API();
            all_log.AddRange(mt5_log);

            InsertLogRecordsToDatabase(all_log);

            return all_log;
        }

        // MT4 執行流程
        private static List<log_record> MT4_API()
        {
            List<log_record> all_log_list = new();
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

                var all_user_list = AIROE.UsersRequest();

                foreach (var input_record in item.Value)
                {
                    // 每行資料單個LOG
                    log_record logRecord = new();
                    logRecord.Server = server;
                    logRecord.Login = input_record.Login;
                    logRecord.UserLogin = server_config.Login;
                    try
                    {
                        if (!int.TryParse(input_record.Login, out var _Login))
                        {
                            throw new Exception("無法辨別 Login 設置");
                        }

                        // 檢查是否有該Login
                        if (!all_user_list.Any(record => record.Login == _Login))
                        {
                            // Step1: 創建帳號
                            // 全面防最呆的那種
                            // Name, Group 必填
                            if (string.IsNullOrEmpty(input_record.Name) || (string.IsNullOrEmpty(input_record.Group)))
                            {
                                throw new Exception("Name, Group 為必填，不可空白");
                            }

                            // Name
                            string _Name = input_record.Name;
                            if (_Name.Length > 127)
                            {
                                throw new Exception("Name 限制 127 字元");
                            }

                            // Group
                            string _Group = input_record.Group;
                            if (_Group.Length > 15)
                            {
                                throw new Exception("Group 限制 15 字元");
                            }

                            // Leverage
                            int _Leverage = 100;
                            if (!string.IsNullOrEmpty(input_record.Leverage))
                            {
                                if (int.TryParse(input_record.Leverage, out _Leverage))
                                {
                                    if (_Leverage > 1000 || _Leverage < 1)
                                    {
                                        throw new Exception("Leverage 僅能設置 1 ~ 1000");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法辨別 Leverage 設置");
                                }
                            }

                            // AgentAccount 預設 0
                            int _AgentAccount = 0;
                            if (!string.IsNullOrEmpty(input_record.AgentAccount))
                            {
                                if (int.TryParse(input_record.AgentAccount, out _AgentAccount))
                                {
                                    if (_AgentAccount < 0)
                                    {
                                        throw new Exception("AgentAccount 不可為負");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法辨別 AgentAccount 設置");
                                }
                            }

                            // Taxes 預設 0
                            int _Taxes = 0;
                            if (!string.IsNullOrEmpty(input_record.Taxes))
                            {
                                if (int.TryParse(input_record.Taxes, out _Taxes))
                                {
                                    if (_Taxes < 0)
                                    {
                                        throw new Exception("Taxes 不可為負");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法辨別 Taxes 設置");
                                }
                            }

                            // SendReports 預設 1
                            int _SendReports = 1;
                            if (!string.IsNullOrEmpty(input_record.SendReports))
                            {
                                if (int.TryParse(input_record.SendReports, out _SendReports))
                                {
                                    if (_SendReports != 0 && _SendReports != 1)
                                    {
                                        throw new Exception("SendReports 僅能設置 1(啟用)、0(禁用)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法辨別 SendReports 設置");
                                }
                            }

                            // Mqid 預設 0
                            uint _Mqid = 0;
                            if (!string.IsNullOrEmpty(input_record.Mqid))
                            {
                                if (!uint.TryParse(input_record.Mqid, out _Mqid))
                                {
                                    throw new Exception("無法辨別 Mqid 設置");
                                }
                            }

                            // Status 預設 空白
                            string _Status = "";
                            if (!string.IsNullOrEmpty(input_record.Status))
                            {
                                if (input_record.Status.Length > 15)
                                {
                                    throw new Exception("Status 限制 15 字元");
                                }
                                else
                                {
                                    _Status = input_record.Status;
                                }
                            }

                            // Id 預設 空白
                            string _Id = "";
                            if (!string.IsNullOrEmpty(input_record.Id))
                            {
                                if (input_record.Id.Length > 31)
                                {
                                    throw new Exception("Id 限制 31 字元");
                                }
                                else
                                {
                                    _Id = input_record.Id;
                                }
                            }

                            // Comment 預設 空白
                            string _Comment = "";
                            if (!string.IsNullOrEmpty(input_record.Comment))
                            {
                                if (input_record.Comment.Length > 63)
                                {
                                    throw new Exception("Comment 限制 63 字元");
                                }
                                else
                                {
                                    _Comment = input_record.Comment;
                                }
                            }

                            // EnableReadOnly 預設 0
                            int _EnableReadOnly = 0;
                            if (!string.IsNullOrEmpty(input_record.EnableReadOnly))
                            {
                                if (int.TryParse(input_record.EnableReadOnly, out _EnableReadOnly))
                                {
                                    if (_EnableReadOnly != 0 && _EnableReadOnly != 1)
                                    {
                                        throw new Exception("EnableReadOnly 僅能設置 1(啟用)、0(禁用)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法辨別 EnableReadOnly 設置");
                                }
                            }

                            // EnableChangePassword 預設 1
                            int _EnableChangePassword = 1;
                            if (!string.IsNullOrEmpty(input_record.EnableChangePassword))
                            {
                                if (int.TryParse(input_record.EnableChangePassword, out _EnableChangePassword))
                                {
                                    if (_EnableChangePassword != 0 && _EnableChangePassword != 1)
                                    {
                                        throw new Exception("EnableChangePassword 僅能設置 1(啟用)、0(禁用)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法辨別 EnableChangePassword 設置");
                                }
                            }

                            // EnableOTP 預設 1
                            int _EnableOTP = 1;
                            if (!string.IsNullOrEmpty(input_record.EnableOTP))
                            {
                                if (int.TryParse(input_record.EnableOTP, out _EnableOTP))
                                {
                                    if (_EnableOTP != 0 && _EnableOTP != 1)
                                    {
                                        throw new Exception("EnableOTP 僅能設置 1(啟用)、0(禁用)");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法辨別 EnableOTP 設置");
                                }
                            }

                            // Country 預設 空白
                            string _Country = "";
                            if (!string.IsNullOrEmpty(input_record.Country))
                            {
                                if (input_record.Country.Length > 31)
                                {
                                    throw new Exception("Country 限制 31 字元");
                                }
                                else
                                {
                                    _Country = input_record.Country;
                                }
                            }

                            // City 預設 空白
                            string _City = "";
                            if (!string.IsNullOrEmpty(input_record.City))
                            {
                                if (input_record.City.Length > 31)
                                {
                                    throw new Exception("City 限制 31 字元");
                                }
                                else
                                {
                                    _City = input_record.City;
                                }
                            }

                            // State 預設 空白
                            string _State = "";
                            if (!string.IsNullOrEmpty(input_record.State))
                            {
                                if (input_record.State.Length > 31)
                                {
                                    throw new Exception("State 限制 31 字元");
                                }
                                else
                                {
                                    _State = input_record.State;
                                }
                            }

                            // ZipCode 預設 空白
                            string _ZipCode = "";
                            if (!string.IsNullOrEmpty(input_record.ZipCode))
                            {
                                if (input_record.ZipCode.Length > 15)
                                {
                                    throw new Exception("ZipCode 限制 15 字元");
                                }
                                else
                                {
                                    _ZipCode = input_record.ZipCode;
                                }
                            }

                            // Address 預設 空白
                            string _Address = "";
                            if (!string.IsNullOrEmpty(input_record.Address))
                            {
                                if (input_record.Address.Length > 95)
                                {
                                    throw new Exception("Address 限制 95 字元");
                                }
                                else
                                {
                                    _Address = input_record.Address;
                                }
                            }

                            // LeadSource 預設 空白
                            string _LeadSource = "";
                            if (!string.IsNullOrEmpty(input_record.LeadSource))
                            {
                                if (input_record.LeadSource.Length > 31)
                                {
                                    throw new Exception("LeadSource 限制 31 字元");
                                }
                                else
                                {
                                    _LeadSource = input_record.LeadSource;
                                }
                            }

                            // Phone 預設 空白
                            string _Phone = "";
                            if (!string.IsNullOrEmpty(input_record.Phone))
                            {
                                if (input_record.Phone.Length > 31)
                                {
                                    throw new Exception("Phone 限制 31 字元");
                                }
                                else
                                {
                                    _Phone = input_record.Phone;
                                }
                            }

                            // Email 預設 空白
                            string _Email = "";
                            if (!string.IsNullOrEmpty(input_record.Email))
                            {
                                if (input_record.Email.Length > 47)
                                {
                                    throw new Exception("Email 限制 47 字元");
                                }
                                else
                                {
                                    _Email = input_record.Email;
                                }
                            }

                            var urd = new UserRecord()
                            {
                                Login = _Login,
                                Enable = 1, // 預設啟用                              
                                Name = _Name,
                                City = _City,
                                State = _State,
                                Country = _Country,
                                Address = _Address,
                                Phone = _Phone,
                                Email = _Email,
                                Id = _Id,
                                Status = _Status,
                                Group = _Group,
                                Comment = _Comment,
                                Leverage = _Leverage,
                                Taxes = _Taxes,
                                AgentAccount = _AgentAccount,
                                LeadSource = _LeadSource,
                                Mqid = _Mqid,
                                ZipCode = _ZipCode,
                                EnableReadOnly = _EnableReadOnly,
                                EnableChangePassword = _EnableChangePassword,
                                EnableOTP = _EnableOTP,
                                SendReports = _SendReports
                            };

                            logRecord.Group = _Group;
                            logRecord.Leverage = _Leverage.ToString();
                            logRecord.Comment = _Comment;
                            logRecord.Email = _Email;

                            int res = AIROE.UserRecordNew(urd);

                            if (res == 0)
                            {
                                logRecord.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                logRecord.Result = "創建成功";
                            }
                            else
                            {
                                throw new Exception("創建失敗");
                            }

                            // Step2: 修改密碼
                            string master_password = GeneratePassword();
                            string invest_password = GeneratePassword();
                            var res_pwdSetMaster = AIROE.UserPasswordSet(_Login, master_password, 0, 1);
                            var res_pwdSetInvestor = AIROE.UserPasswordSet(_Login, invest_password, 1, 1);
                            if (res_pwdSetMaster != 0 || res_pwdSetInvestor != 0)
                            {
                                throw new Exception("創建成功，但修改密碼時發生錯誤");
                            }
                            logRecord.Password = master_password;
                            logRecord.Investor_Password = invest_password;
                        }
                        else
                        {
                            throw new Exception("該 Login 已存在");
                        }
                    }
                    catch (Exception ex)
                    {
                        logRecord.Result = ex.Message;
                    }
                    finally
                    {
                        all_log_list.Add(logRecord);
                    }
                }

                AIROE.Disconnect();
            }

            return all_log_list;
        }

        // MT5 執行流程
        private static List<log_record> MT5_API()
        {
            List<log_record> all_log_list = new();
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

                foreach (var input_record in item.Value)
                {
                    // 每行資料單個LOG
                    log_record logRecord = new();
                    logRecord.Server = server;
                    logRecord.Login = input_record.Login;
                    logRecord.UserLogin = server_config.Login;

                    try
                    {
                        if (!uint.TryParse(input_record.Login, out var _Login))
                        {
                            throw new Exception("無法辨別 Login 設置");
                        }

                        // 檢查是否有該Login
                        var urd = manager.UserCreate();
                        var res = manager.UserRequest(_Login, urd);
                        if (res == 0)
                        {
                            throw new Exception("該 Login 已存在");
                        }

                        if (urd.Login() == 0 && res == (MTRetCode)13)
                        {
                            // Step1: 創建帳號
                            // 全面防呆
                            // Name, Group 必填
                            if (string.IsNullOrEmpty(input_record.Name) || (string.IsNullOrEmpty(input_record.Group)))
                            {
                                throw new Exception("Name, Group 為必填，不可空白");
                            }

                            // Name
                            string _Name = input_record.Name;
                            if (_Name.Length > 127)
                            {
                                throw new Exception("Name 限制 127 字元");
                            }

                            // Group
                            string _Group = input_record.Group;
                            if (_Group.Length > 63)
                            {
                                throw new Exception("Group 限制 63 字元");
                            }

                            // Leverage 預設 100
                            uint _Leverage = 100;
                            if (!string.IsNullOrEmpty(input_record.Leverage))
                            {
                                if (uint.TryParse(input_record.Leverage, out _Leverage))
                                {
                                    if ((_Leverage > 1000 || _Leverage < 1) & !input_record.Server.ToLower().Contains("test"))
                                    {
                                        throw new Exception("Leverage 僅能設置 1 ~ 1000");
                                    }
                                }
                                else
                                {
                                    throw new Exception("無法辨別 Leverage 設置");
                                }
                            }

                            // Company 預設 空白
                            string _Company = "";
                            if (!string.IsNullOrEmpty(input_record.Company))
                            {
                                if (input_record.Company.Length > 63)
                                {
                                    throw new Exception("Company 限制 63 字元");
                                }
                                else
                                {
                                    _Company = input_record.Company;
                                }
                            }

                            // Email 預設 空白
                            string _Email = "";
                            if (!string.IsNullOrEmpty(input_record.Email))
                            {
                                if (input_record.Email.Length > 63)
                                {
                                    throw new Exception("Email 限制 63 字元");
                                }
                                else
                                {
                                    _Email = input_record.Email;
                                }
                            }

                            // Phone 預設 空白
                            string _Phone = "";
                            if (!string.IsNullOrEmpty(input_record.Phone))
                            {
                                if (input_record.Phone.Length > 31)
                                {
                                    throw new Exception("Phone 限制 31 字元");
                                }
                                else
                                {
                                    _Phone = input_record.Phone;
                                }
                            }

                            // Country 預設 空白
                            string _Country = "";
                            if (!string.IsNullOrEmpty(input_record.Country))
                            {
                                if (input_record.Country.Length > 63)
                                {
                                    throw new Exception("Country 限制 63 字元");
                                }
                                else
                                {
                                    _Country = input_record.Country;
                                }
                            }

                            // City 預設 空白
                            string _City = "";
                            if (!string.IsNullOrEmpty(input_record.City))
                            {
                                if (input_record.City.Length > 63)
                                {
                                    throw new Exception("City 限制 63 字元");
                                }
                                else
                                {
                                    _City = input_record.City;
                                }
                            }

                            // State 預設 空白
                            string _State = "";
                            if (!string.IsNullOrEmpty(input_record.State))
                            {
                                if (input_record.State.Length > 63)
                                {
                                    throw new Exception("State 限制 63 字元");
                                }
                                else
                                {
                                    _State = input_record.State;
                                }
                            }

                            // ZipCode 預設 空白
                            string _ZipCode = "";
                            if (!string.IsNullOrEmpty(input_record.ZipCode))
                            {
                                if (input_record.ZipCode.Length > 15)
                                {
                                    throw new Exception("ZipCode 限制 15 字元");
                                }
                                else
                                {
                                    _ZipCode = input_record.ZipCode;
                                }
                            }

                            // Address 預設 空白
                            string _Address = "";
                            if (!string.IsNullOrEmpty(input_record.Address))
                            {
                                if (input_record.Address.Length > 127)
                                {
                                    throw new Exception("Address 限制 127 字元");
                                }
                                else
                                {
                                    _Address = input_record.Address;
                                }
                            }

                            // Comment 預設 空白
                            string _Comment = "";
                            if (!string.IsNullOrEmpty(input_record.Comment))
                            {
                                if (input_record.Comment.Length > 63)
                                {
                                    throw new Exception("Comment 限制 63 字元");
                                }
                                else
                                {
                                    _Comment = input_record.Comment;
                                }
                            }

                            // 防呆完全塞
                            urd.Login(_Login);
                            urd.Name(_Name);
                            urd.Group(_Group);
                            urd.Leverage(_Leverage);
                            urd.Company(_Company);
                            urd.EMail(_Email);
                            urd.Phone(_Phone);
                            urd.Country(_Country);
                            urd.ZIPCode(_ZipCode);
                            urd.State(_State);
                            urd.Address(_Address);
                            urd.Comment(_Comment);
                            urd.Rights((CIMTUser.EnUsersRights)2403);

                            // log
                            logRecord.Group = _Group;
                            logRecord.Leverage = _Leverage.ToString();
                            logRecord.Comment = _Comment;
                            logRecord.Email = _Email;

                            // 創建
                            string master_pwd = GeneratePassword();
                            string investor_pwd = GeneratePassword();
                            var res_userAdd = manager.UserAdd(urd, master_pwd, investor_pwd);
                            if (res_userAdd == 0)
                            {
                                logRecord.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                logRecord.Result = "創建成功";
                                logRecord.Password = master_pwd;
                                logRecord.Investor_Password = investor_pwd;
                            }
                            else
                            {
                                throw new Exception("創建失敗");
                            }
                        }
                        else
                        {
                            throw new Exception("該 Login 已存在");
                        }
                    }
                    catch (Exception ex)
                    {
                        logRecord.Result = ex.Message;
                    }
                    finally
                    {
                        all_log_list.Add(logRecord);
                    }
                }

                manager.Disconnect();
            }
            return all_log_list;
        }

        // 密碼產生
        public static string GeneratePassword(uint minLength = 8)
        {
            Random random = new Random();

            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string specialCharacters = "!@#$%^&*?";

            StringBuilder password = new StringBuilder();
            password.Append(uppercase[random.Next(uppercase.Length)]);
            password.Append(lowercase[random.Next(lowercase.Length)]);
            password.Append(digits[random.Next(digits.Length)]);
            password.Append(specialCharacters[random.Next(specialCharacters.Length)]);

            string allCharacters = uppercase + lowercase + digits + specialCharacters;
            for (int i = 4; i < minLength; i++)
            {
                password.Append(allCharacters[random.Next(allCharacters.Length)]);
            }

            return new string(password.ToString().ToCharArray().OrderBy(x => random.Next()).ToArray());
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
                                Name = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Name") + 1].Text,
                                Group = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Group") + 1].Text,
                                Leverage = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Leverage") + 1].Text,
                                AgentAccount = worksheet_input.Cells[row, Array.IndexOf(headerRow, "AgentAccount") + 1].Text,
                                Taxes = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Taxes") + 1].Text,
                                SendReports = worksheet_input.Cells[row, Array.IndexOf(headerRow, "SendReports") + 1].Text,
                                Mqid = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Mqid") + 1].Text,
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
                                Leverage = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Leverage") + 1].Text,
                                Name = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Name") + 1].Text,
                                Company = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Company") + 1].Text,
                                Email = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Email") + 1].Text,
                                Phone = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Phone") + 1].Text,
                                Country = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Country") + 1].Text,
                                City = worksheet_input.Cells[row, Array.IndexOf(headerRow, "City") + 1].Text,
                                State = worksheet_input.Cells[row, Array.IndexOf(headerRow, "State") + 1].Text,
                                ZipCode = worksheet_input.Cells[row, Array.IndexOf(headerRow, "ZipCode") + 1].Text,
                                Address = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Address") + 1].Text,
                                Comment = worksheet_input.Cells[row, Array.IndexOf(headerRow, "Comment") + 1].Text,
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

        // 撈取所有Server Config
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
        public static void InsertLogRecordsToDatabase(List<log_record> all_Log)
        {
            Initiallize();
            var tool_Log = all_Log.Where(record => record.Result.Contains("創建成功"));
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    INSERT INTO admin_tool_log.create_account (
                        `Server`, `Login`, `Group`, `Leverage`, `Email`, `Comment`, `UserLogin`, `Time`
                    ) VALUES (
                        @Server, @Login, @Group, @Leverage, @Email, @Comment, @UserLogin, @Time
                    );";

                foreach (var logRecord in tool_Log)
                {
                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Server", logRecord.Server);
                        cmd.Parameters.AddWithValue("@Login", logRecord.Login);
                        cmd.Parameters.AddWithValue("@Group", logRecord.Group);
                        cmd.Parameters.AddWithValue("@Leverage", logRecord.Leverage);
                        cmd.Parameters.AddWithValue("@Email", logRecord.Email);
                        cmd.Parameters.AddWithValue("@Comment", logRecord.Comment);
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
                        FROM admin_tool_log.create_account
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

                string fileName = "創建帳號HistoryLog.xlsx";
                var stream = new MemoryStream(package.GetAsByteArray());

                return (stream, fileName);
            }
        }

        // 給User的檔案
        public static (MemoryStream, string) Log2Excel(byte[] logBytes)
        {
            var all_log = System.Text.Json.JsonSerializer.Deserialize<List<log_record>>(logBytes);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Output");

                DataTable dataTable = new DataTable();
                dataTable.Columns.Add("Server", typeof(string));
                dataTable.Columns.Add("Login", typeof(string));                
                dataTable.Columns.Add("Password", typeof(string));
                dataTable.Columns.Add("Investor_Password", typeof(string));
                dataTable.Columns.Add("Group", typeof(string));
                dataTable.Columns.Add("Leverage", typeof(string));
                dataTable.Columns.Add("Comment", typeof(string));
                dataTable.Columns.Add("Email", typeof(string));

                foreach (var log in all_log)
                {
                    if (log.Result != "創建成功")
                    {
                        continue;
                    }
                    dataTable.Rows.Add(log.Server, log.Login, log.Password, log.Investor_Password, log.Group, log.Leverage, log.Comment, log.Email);
                }

                worksheet.Cells["A1"].LoadFromDataTable(dataTable, true);

                string fileName = $"add_account_{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
                var stream = new MemoryStream(package.GetAsByteArray());

                return (stream, fileName);
            }
        }

    }
}
