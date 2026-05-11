using Microsoft.AspNetCore.Mvc;
using admin_web.Services.DataProductService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data;
using admin_web.Models;
using System.Threading;
using System.Diagnostics;
using admin_web.Services;
using admin_web.Models.DataProduct;
using CsvHelper.Configuration;
using System.Globalization;
using CsvHelper;
using Microsoft.AspNetCore.Http;
using System.Text;
using static admin_web.Models.DataProduct.Pe_setting;
using admin_web.Services.DataProductServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Threading.Tasks;
using static admin_web.Models.DataProduct.New_Pe_setting;

namespace admin_web.Controllers
{
    public class DataProductController : Controller
    {

        // ------------------------------------------------------ MT5伺服器水平比對 ------------------------------------------------------
        public IActionResult ServerCompare()
        {


            return View("ServerCompare/main");
        }

        [HttpPost]
        public IActionResult Upload_json_File(IFormFile file, string path, string file_name, bool del_all = false)
        {
            // 
            var uploadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerCompare", path);

            // 
            if (!Directory.Exists(uploadDirectory))
            {
                Directory.CreateDirectory(uploadDirectory);
            }

            // **del_all=true，清空資料夾**
            if (del_all)
            {
                DirectoryInfo di = new DirectoryInfo(uploadDirectory);
                foreach (FileInfo file_d in di.GetFiles()) { file_d.Delete(); }
                foreach (DirectoryInfo dir in di.GetDirectories()) { dir.Delete(true); }
            }

            // **如果沒有上傳檔案（但只是要清空），直接回傳成功**
            if (file == null || file.Length == 0)
            {
                return Ok($"{path} 資料夾已清空。");
            }

            // **儲存新檔案**
            var newFilePath = Path.Combine(uploadDirectory, string.IsNullOrEmpty(file_name) ? file.FileName : file_name);
            using var stream = new FileStream(newFilePath, FileMode.Create);
            file.CopyTo(stream);

            return Ok($"{file.FileName} uploaded successfully.");
        }

        [HttpGet]
        public IActionResult match_network(IFormFile file, string path, string file_name, bool del_all = false)
        {
            Debug.WriteLine("???");

            string sourceDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerCompare", "SourceServer");
            string targetDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerCompare", "TargetServer");
            // Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerCompare", path)
            string[] sourceFiles = Directory.GetFiles(sourceDir, "*.json");
            string[] targetFiles = Directory.GetFiles(targetDir, "*.json");

            if (sourceFiles.Length == 0)
                throw new FileNotFoundException("找不到 SourceServer 資料夾中的 JSON 檔案");
            if (targetFiles.Length == 0)
                throw new FileNotFoundException("找不到 TargetServer 資料夾中的 JSON 檔案");

            string sourceFile = sourceFiles[0];
            string targetFile = targetFiles[0];

            // 讀取並解析 JSON（utf-16 對應 Encoding.Unicode）
            string json1Text = System.IO.File.ReadAllText(sourceFile, Encoding.Unicode);
            string json2Text = System.IO.File.ReadAllText(targetFile, Encoding.Unicode);

            var json1 = JsonConvert.DeserializeObject<JsonRoot>(json1Text);
            var json2 = JsonConvert.DeserializeObject<JsonRoot>(json2Text);

            var config1 = json1?.Server?.FirstOrDefault()?.ConfigNetwork ?? new List<NetworkConfig>();
            var config2 = json2?.Server?.FirstOrDefault()?.ConfigNetwork ?? new List<NetworkConfig>();

            int maxLen = Math.Max(config1.Count, config2.Count);

            List<ConfigPair> rows = new();

            for (int i = 0; i < maxLen; i++)
            {
                string name1 = i < config1.Count ? config1[i].Name ?? "" : "";
                string name2 = i < config2.Count ? config2[i].Name ?? "" : "";
                rows.Add(new ConfigPair { A_Name = name1, B_Name = name2 });

                System.Diagnostics.Debug.WriteLine($"Row {i + 1}: A_Name = {name1}, B_Name = {name2}");
            }
            return Ok(rows);
        }

        [HttpPost]
        public IActionResult Save_Pairs([FromBody] List<PairResult> pairs)
        {
            try
            {
                var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerCompare", "setting", "config.csv");
                using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
                {
                    writer.WriteLine("Source,Target,Custom");
                    foreach (var pair in pairs)
                    {
                        var line = $"{pair.Source},{pair.Target},{pair.Custom}";
                        writer.WriteLine(line);
                    }
                }

                return Ok("儲存成功");
            }
            catch (Exception ex)
            {
                return BadRequest($"儲存失敗: {ex.Message}");
            }
        }


        // ------------------------------------------------------ 垂直比對工具 ------------------------------------------------------
        //新版
        public IActionResult ConfigDiffRecordsVerticalNew(string _from, string _to)
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("ConfigDiffRecordsVerticalNew");
            ViewBag.RefreshTIme = ConfigDiffRecordsNewService.Get_Today_Refresh_Time();
            ViewBag.FromDate = _from;
            ViewBag.ToDate = _to;
            var view_model = ConfigDiffRecordsNewService.CompareData_NEW(_from, _to);

            return View("ConfigDiffRecords/vertical_new", view_model);
        }

        [HttpGet]
        public async Task<IActionResult> ConfigDiffRecordsVerticalNew_Refresh()
        {
            string log = await ConfigDiffRecordsNewService.Postgre2MysqlAsync();
            if (log == "刷新成功")
            {
                string _refreshTime = ConfigDiffRecordsNewService.Get_Today_Refresh_Time();
                return Ok(new { message = log, refreshTime = _refreshTime });
            }
            else
            {
                return BadRequest(log);
            }
        }

        // 舊版
        //public IActionResult ConfigDiffRecordsVertical(string _from, string _to)
        //{
        //    ViewData["Title"] = UniversalService.Get_Tool_Name("ConfigDiffRecordsVertical");
        //    ViewBag.RefreshTIme = ConfigDiffRecordsService.Get_Today_Refresh_Time();
        //    ViewBag.FromDate = _from;
        //    ViewBag.ToDate = _to;
        //    var view_model = ConfigDiffRecordsService.CompareData_NEW(_from, _to);

        //    return View("ConfigDiffRecords/vertical", view_model);
        //}

        //[HttpGet]
        //public async Task<IActionResult> ConfigDiffRecordsVertical_Refresh()
        //{
        //    string log = await ConfigDiffRecordsService.Postgre2MysqlAsync();
        //    if (log == "刷新成功")
        //    {
        //        string _refreshTime = ConfigDiffRecordsService.Get_Today_Refresh_Time();
        //        return Ok(new { message = log, refreshTime = _refreshTime });
        //    }
        //    else
        //    {
        //        return BadRequest(log);
        //    }
        //}
        // ------------------------------------------------------ 萬用比對工具 ------------------------------------------------------
        public IActionResult CompareGod()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("CompareGod");
            return View("CompareGod/main");
        }

        [HttpPost]
        public IActionResult CompareGod_Main(IFormCollection form)
        {
            var beforeFiles = form.Files.GetFiles("before_files[]");
            var afterFiles = form.Files.GetFiles("after_files[]");
            var primaryKey = form["primaryKey"].ToString();

            var log = CompareGodService.MainProgram(primaryKey, beforeFiles, afterFiles);

            return Ok(log);
        }

        public IActionResult CompareGod_DownloadFile(string fileName)
        {
            string filePath = Path.Combine(CompareGodService.ToolPath, fileName);

            if (System.IO.File.Exists(filePath))
            {
                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            else
            {
                return NotFound("檔案遺失，請洽自動化團隊確認原因");
            }
        }

        [HttpPost]
        public IActionResult CompareGod_DeleteFile(string fileName)
        {
            string filePath = Path.Combine(CompareGodService.ToolPath, fileName);

            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                    return Ok("檔案已刪除");
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"刪除檔案時發生錯誤: {ex.Message}");
                }
            }
            else
            {
                return NotFound("找不到該檔案");
            }
        }

        // ------------------------------------------------------ ticket system ------------------------------------------------------
        public IActionResult Ticket_system(string date)
        {
            //Ticket_system_Model viewModel = new();

            List<string> fileNameList = Ticket_systemService.GetFileNameList();
            ViewBag.FileNameList = fileNameList;
            ExcelViewModel viewModel = new();
            if (fileNameList.Count != 0)
            {
                if (date == "" || date == null)
                {
                    string FileName = fileNameList.Last();
                    viewModel = Ticket_systemService.View_Data(FileName);
                    ViewBag.SelectDate = FileName;
                }
                else
                {
                    viewModel = Ticket_systemService.View_Data(date);
                    ViewBag.SelectDate = date;
                }
                return View("Ticket_system/Ticket_system", viewModel);
            }
            else
            {
                return View("Ticket_system/Ticket_system");
            }

        }

        [HttpPost]
        public IActionResult Upload_6_File(IFormFile file, string path, string file_name, bool del_all = false)
        {
            // 
            var uploadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Ticket_system", path);

            // 
            if (!Directory.Exists(uploadDirectory))
            {
                Directory.CreateDirectory(uploadDirectory);
            }

            // **del_all=true，清空資料夾**
            if (del_all)
            {
                DirectoryInfo di = new DirectoryInfo(uploadDirectory);
                foreach (FileInfo file_d in di.GetFiles()) { file_d.Delete(); }
                foreach (DirectoryInfo dir in di.GetDirectories()) { dir.Delete(true); }
            }

            // **如果沒有上傳檔案（但只是要清空），直接回傳成功**
            if (file == null || file.Length == 0)
            {
                return Ok($"{path} 資料夾已清空。");
            }

            // **儲存新檔案**
            var newFilePath = Path.Combine(uploadDirectory, string.IsNullOrEmpty(file_name) ? file.FileName : file_name);
            using var stream = new FileStream(newFilePath, FileMode.Create);
            file.CopyTo(stream);

            return Ok($"{file.FileName} uploaded successfully.");
        }

        // 執行工具
        [HttpPost]
        public IActionResult RunExe_parameter_2(string tool_path, string output_path, string param1, string param2, string exe_name, bool DeleteAllFile = false)
        {
            string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", tool_path);
            string folderPath = Path.Combine(ToolPath, output_path);
            string exePath = Path.Combine(ToolPath, exe_name);

            // 刪除Output資料夾內所有檔案(預設為不會刪除Output路徑內的所有檔案)
            if (DeleteAllFile)
            {
                if (Directory.Exists(folderPath))
                {
                    DirectoryInfo di = new(folderPath);

                    foreach (FileInfo file_d in di.GetFiles())
                    {
                        file_d.Delete();
                    }
                    foreach (DirectoryInfo dir in di.GetDirectories())
                    {
                        dir.Delete(true);
                    }
                }
            }

            // 執行工具
            if (System.IO.File.Exists(exePath))
            {
                try
                {
                    using Process process = new();
                    process.StartInfo.FileName = exePath;
                    process.StartInfo.WorkingDirectory = ToolPath;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.Arguments = $"\"{param1}\" \"{param2}\" ";


                    process.Start();
                    while (!process.HasExited)
                    {
                        Thread.Sleep(1000);
                    }
                    process.WaitForExit();

                    int exitCode = process.ExitCode;

                    if (exitCode == 0)
                    {
                        return Ok("工具執行完成");
                    }
                    else
                    {
                        string errorOutput = process.StandardError.ReadToEnd();
                        return BadRequest($"工具執行失敗，退出碼：{exitCode}\n錯誤信息：{errorOutput}");
                    }
                }
                catch
                {
                    return BadRequest("工具發生錯誤");
                }
            }
            else
            {
                return BadRequest("未找到工具");
            }
        }


        // ------------------------------------------------------ PAMM Rollover自動化 ------------------------------------------------------
        public IActionResult Pamm_Rollover()
        {
            PammRollover_Model viewModel = new();


            List<string> fileNameList = Pamm_balanceService.GetFileNameList();
            ViewBag.FileNameList = fileNameList;

            return View("Pamm_Rollover/Pamm_Rollover", viewModel);
        }

        [HttpPost]
        public IActionResult Refreshstatus()
        {
            // 執行 SQL 查詢，取得資料
            DataTable data_position_realtime = Pamm_RolloverService.ExecuteSQL_status();

            // 將 DataTable 轉換為物件的列表
            var data = data_position_realtime.AsEnumerable()
                .Select(row => data_position_realtime.Columns.Cast<DataColumn>()
                    .ToDictionary(col => col.ColumnName, col => row[col]))
                .ToList();

            return Json(data); // 以 JSON 格式返回資料
        }

        [HttpPost]
        public JsonResult RefreshServer()
        {
            //var pammList = new List<PammRollover_Model>();
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Pamm_Rollover", "config_set", "pammManagerRolloverConfig.csv");

            if (!System.IO.File.Exists(filePath))
            {
                return Json(new { success = false, message = "CSV 檔案不存在" });
            }

            try
            {
                var records = new List<PammRollover_Model>();

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true, // CSV 有標題列
                    IgnoreBlankLines = true,
                    TrimOptions = TrimOptions.Trim
                };

                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvReader(reader, config))
                {
                    var allRecords = csv.GetRecords<FullPammConfig>().ToList();

                    // 只選擇需要的欄位
                    records = allRecords.Select(x => new PammRollover_Model
                    {
                        Pamm_name = x.Pamm_name,
                        Base_url = x.Base_url,
                        Cfg_ids = x.Cfg_ids // 這裡要注意 CSV 標題是否符合 C# 變數命名
                    }).ToList();
                }

                return Json(new { success = true, data = records });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "讀取 CSV 失敗", error = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult UploadsetCSV(IFormFile file)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Pamm_Rollover", "config_set", "pammManagerRolloverConfig.csv");

            if (file == null || file.Length == 0)
            {
                return BadRequest("檔案錯誤");
            }

            // 確保檔名正確
            if (file.FileName != "pammManagerRolloverConfig.csv")
            {
                return BadRequest("請確認檔名為 pammManagerRolloverConfig.csv");
            }

            try
            {
                using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
                {
                    var lines = new List<string>();
                    while (!reader.EndOfStream)
                    {
                        lines.Add(reader.ReadLine());
                    }

                    if (lines.Count < 1)
                    {
                        return BadRequest("檔案無資料");
                    }

                    // 
                    var headers = lines[0].Split(',');
                    var requiredHeaders = new string[] { "pamm_name", "base_url", "username", "password", "client_id", "client_secret", "cfg_ids/All" };

                    // 檢查欄位
                    if (!requiredHeaders.SequenceEqual(headers))
                    {
                        return BadRequest("檔案欄位請包含：pamm_name, base_url, username, password, client_id, client_secret, cfg_ids/All");
                    }

                    // 上傳
                    System.IO.File.WriteAllText(filePath, string.Join("\n", lines), Encoding.UTF8);
                }

                return Ok(new { message = "檔案上傳成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "伺服器錯誤：" + ex.Message);
            }
        }

        public IActionResult GetErrorFileContent()
        {
            try
            {
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                string logFileName = $"{today}_error_log.txt";

                var logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Pamm_Rollover", "log", logFileName);  // 檔案的完整路徑
                if (!System.IO.File.Exists(logFilePath))
                {
                    return Content("今日無錯誤記錄或日誌檔案不存在。");
                }

                var content = System.IO.File.ReadAllText(logFilePath);  // 讀取檔案內容
                return Content(content);  // 返回檔案內容
            }
            catch (Exception ex)
            {
                return StatusCode(500, "檔案讀取失敗：" + ex.Message);
            }

        }

        [HttpGet]
        public IActionResult GetModifyLogFiles()
        {
            try
            {
                string logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Pamm_Rollover", "log");
                if (!Directory.Exists(logDirectory))
                {
                    return NotFound("日誌目錄不存在。");
                }

                var files = Directory.GetFiles(logDirectory, "*_modify_log.csv")
                                     .Select(Path.GetFileName)
                                     .OrderByDescending(f => f)
                                     .ToList();

                return Json(files);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "讀取日誌檔案列表失敗：" + ex.Message);
            }
        }

        [HttpGet]
        public IActionResult GetModifyLogContent(string fileName)
        {
            try
            {
                string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Pamm_Rollover", "log", fileName);
                if (!System.IO.File.Exists(logFilePath))
                {
                    return NotFound("日誌檔案不存在。");
                }

                var content = System.IO.File.ReadAllText(logFilePath);
                return Content(content);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "讀取日誌內容失敗：" + ex.Message);
            }
        }
        [HttpPost]
        public IActionResult RunExe_rollover(string tool_path, string output_path, string exe_name, bool DeleteAllFile = false)
        {
            string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", tool_path);
            string folderPath = Path.Combine(ToolPath, output_path);
            string exePath = Path.Combine(ToolPath, exe_name);

            // 刪除Output資料夾內所有檔案(預設為不會刪除Output路徑內的所有檔案)
            if (DeleteAllFile)
            {
                if (Directory.Exists(folderPath))
                {
                    DirectoryInfo di = new(folderPath);

                    foreach (FileInfo file_d in di.GetFiles())
                    {
                        file_d.Delete();
                    }
                    foreach (DirectoryInfo dir in di.GetDirectories())
                    {
                        dir.Delete(true);
                    }
                }
            }

            // 執行工具
            if (System.IO.File.Exists(exePath))
            {
                try
                {
                    using Process process = new();
                    process.StartInfo.FileName = exePath;
                    process.StartInfo.WorkingDirectory = ToolPath;
                    process.StartInfo.UseShellExecute = false;
                    //process.StartInfo.RedirectStandardError = true;
                    //process.StartInfo.RedirectStandardOutput = true;
                    // 遠端環境不給用
                    // process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                    // process.StartInfo.StandardOutputEncoding = Encoding.UTF8;

                    process.Start();
                    while (!process.HasExited)
                    {
                        Thread.Sleep(1000);
                    }
                    process.WaitForExit();

                    int exitCode = process.ExitCode;

                    if (exitCode == 0)
                    {
                        return Ok("工具執行完成");
                    }
                    else
                    {
                        string errorOutput = process.StandardError.ReadToEnd();
                        return BadRequest($"工具執行失敗，退出碼：{exitCode}\n錯誤信息：{errorOutput}");
                    }
                }
                catch
                {
                    return BadRequest("工具發生錯誤");
                }
            }
            else
            {
                return BadRequest("未找到工具");
            }
        }
        // ------------------------------------------------------ PAMM 跨月出入金 ------------------------------------------------------
        public IActionResult Pamm_balance(string date)
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("Pamm_balance");
            List<string> fileNameList = Pamm_balanceService.GetFileNameList();
            ViewBag.FileNameList = fileNameList;
            ExcelViewModel viewModel = new();
            if (fileNameList.Count != 0)
            {
                if (date == "" || date == null)
                {
                    string FileName = fileNameList.Last();
                    viewModel = Pamm_balanceService.View_Data(FileName);
                    ViewBag.SelectDate = FileName;
                }
                else
                {
                    viewModel = Pamm_balanceService.View_Data(date);
                    ViewBag.SelectDate = date;
                }
                return View("Pamm_balance/Pamm_balance", viewModel);
            }
            else
            {
                return View("Leverage_Check/Leverage_Check_FCA");
            }
        }
        // ------------------------------------------------------ ST 客戶漏單自動抓取 ------------------------------------------------------
        public IActionResult ST_Delay(string Path, string Date)
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("ST_Delay");
            List<string> fileNameList = ST_DelayService.GetFileNameList();
            ViewBag.FileNameList = fileNameList;

            if (Path == "setting")
            {
                var viewModel = ST_DelayService.ReadCsv_St();
                return View("ST_Delay/setting", viewModel);
            }
            else
            {
                if (fileNameList.Count == 0)
                {
                    return View("ST_Delay/main");
                }
                else if (Date != null && Date != "")
                {
                    var viewModel = ST_DelayService.View_Data(Date);
                    ViewBag.FileSelect = Date;

                    return View("ST_Delay/main", viewModel);
                }
                else
                {
                    string FileDate = fileNameList.Last();
                    ViewBag.FileSelect = FileDate;
                    var viewModel = ST_DelayService.View_Data(FileDate);
                    return View("ST_Delay/main", viewModel);
                }
            }
        }

        // ST資訊(新增)
        [HttpPost]
        public JsonResult ST_Delay_StRecords_Add(string num, string server)
        {
            string msg = ST_DelayService.StConfig_Add(num, server);
            bool success = msg.Contains("成功");

            return Json(new
            {
                success = success,
                message = msg
            });
        }

        // ST資訊(編輯)
        [HttpPost]
        public JsonResult ST_Delay_StRecords_Edit(string numAfter, string serverBefore, string serverAfter)
        {
            string msg = ST_DelayService.StConfig_Edit(numAfter, serverBefore, serverAfter);
            bool success = msg.Contains("成功");

            return Json(new
            {
                success = success,
                message = msg
            });
        }

        // ST資訊(刪除)
        [HttpPost]
        public JsonResult ST_Delay_StRecords_Delete(string server)
        {
            string msg = ST_DelayService.StConfig_Delete(server);
            bool success = msg.Contains("成功");

            return Json(new
            {
                success = success,
                message = msg
            });
        }

        // ------------------------------------------------------ PAMM 盈利報告工具 ------------------------------------------------------

        // 介面
        public IActionResult pamm_closedonly(string path, string PammSelect, string DateSelect)
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("pamm_closedonly");
            ViewBag.PammList = pamm_closedonlyService.GetPammNameList();
            ViewBag.PammSelect = PammSelect;
            ViewBag.No_Color_Id = pamm_closedonlyService.GetNoColorId();
            ViewBag.Special_Approve_Login = pamm_closedonlyService.GetSpecialId();
            if (path == "setting")
            {
                var viewModel = pamm_closedonlyService.ReadCsv_Pm();
                return View("pamm_closedonly/setting", viewModel);
            }
            else if (DateSelect != null)
            {
                ViewBag.DateSelect = DateSelect;
                ViewBag.DateList = pamm_closedonlyService.GetPammDateList(PammSelect);
                var viewModel = pamm_closedonlyService.View_Data(PammSelect, DateSelect);
                return View("pamm_closedonly/main", viewModel);
            }
            else
            {
                return View("pamm_closedonly/main");
            }
        }

        // 依pamm取得所有日期
        [HttpPost]
        public JsonResult pamm_closedonly_GetPammDate(string PammName)
        {
            if (PammName == "")
            {
                return Json("");
            }
            return Json(pamm_closedonlyService.GetPammDateList(PammName));
        }

        // 取得選擇的檔案
        [HttpGet]
        public IActionResult pamm_closedonly_GetView(string PammName, string PammDate)
        {
            var viewModel = pamm_closedonlyService.View_Data(PammName, PammDate);
            return View(viewModel);
        }

        // Pm資訊(新增)
        [HttpPost]
        public JsonResult pamm_closedonly_PmRecords_Add(string sql_na, string host, string user, string password, string mt4_server, string Floating_configid, string No_Color_configid, string Test_Id, string Special_Approve_Login)
        {
            string msg = pamm_closedonlyService.PmRecords_Add(sql_na, host, user, password, mt4_server, Floating_configid, No_Color_configid, Test_Id, Special_Approve_Login);
            bool success = msg.Contains("成功");

            return Json(new
            {
                success = success,
                message = msg
            });
        }

        // Pm資訊(編輯)
        [HttpPost]
        public JsonResult pamm_closedonly_PmRecords_Edit(string sql_naBefore, string sql_naAfter, string hostAfter, string userAfter, string passwordAfter, string mt4_serverAfter, string Floating_configidAfter, string No_Color_configidAfter, string Test_IdAfter, string Special_Approve_LoginAfter)
        {
            string msg = pamm_closedonlyService.PmRecords_Edit(sql_naBefore, sql_naAfter, hostAfter, userAfter, passwordAfter, mt4_serverAfter, Floating_configidAfter, No_Color_configidAfter, Test_IdAfter, Special_Approve_LoginAfter);
            bool success = msg.Contains("成功");

            return Json(new
            {
                success = success,
                message = msg
            });
        }

        // Pm資訊(刪除)
        [HttpPost]
        public JsonResult pamm_closedonly_PmRecords_Delete(string sql_na)
        {
            string msg = pamm_closedonlyService.PmRecords_Delete(sql_na);
            bool success = msg.Contains("成功");

            return Json(new
            {
                success = success,
                message = msg
            });
        }

        // ------------------------------------------------------ PAMM Pending出入金工具 ------------------------------------------------------

        // 介面
        public IActionResult PAMM_pending_des(string Path, string Date)
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("PAMM_pending_des");
            if (Path == "setting")
            {
                var viewModel = PAMM_pending_desService.ReadCsv_Pm();
                return View("PAMM_pending_des/setting", viewModel);
            }
            else
            {
                List<string> fileNameList = PAMM_pending_desService.GetFileNameList();
                ViewBag.FileNameList = fileNameList;

                if (fileNameList.Count == 0)
                {
                    return View("PAMM_pending_des/main");
                }
                else if (Date != null && Date != "")
                {
                    var viewModel = PAMM_pending_desService.View_Data(Date);
                    ViewBag.SelectDate = Date;

                    return View("PAMM_pending_des/main", viewModel);
                }
                else if (Path == null || Path == "")
                {
                    string fileName = fileNameList.Last();
                    var viewModel = PAMM_pending_desService.View_Data(fileName);
                    ViewBag.SelectDate = fileName;

                    return View("PAMM_pending_des/main", viewModel);
                }
                else
                {
                    return View("PAMM_pending_des/main");
                }
            }
        }

        // Pm資訊(新增)
        [HttpPost]
        public JsonResult PAMM_pending_des_PmRecords_Add(string sql_na, string host, string user, string password, string mt_server)
        {
            string msg = PAMM_pending_desService.ServerConfig_Add(sql_na, host, user, password, mt_server);
            bool success = msg.Contains("成功");

            return Json(new
            {
                success = success,
                message = msg
            });
        }

        // Pm資訊(編輯)
        [HttpPost]
        public JsonResult PAMM_pending_des_PmRecords_Edit(string sql_naBefore, string sql_naAfter, string hostBefore, string hostAfter, string userBefore, string userAfter, string passwordBefore, string passwordAfter, string mt_serverBefore, string mt_serverAfter)
        {
            string msg = PAMM_pending_desService.ServerConfig_Edit(sql_naBefore, sql_naAfter, hostBefore, hostAfter, userBefore, userAfter, passwordBefore, passwordAfter, mt_serverBefore, mt_serverAfter);
            bool success = msg.Contains("成功");

            return Json(new
            {
                success = success,
                message = msg
            });
        }

        // Pm資訊(刪除)
        [HttpPost]
        public JsonResult PAMM_pending_des_PmRecords_Delete(string sql_na, string host, string user, string password, string mt_server)
        {
            string msg = PAMM_pending_desService.ServerConfig_Delete(sql_na, host, user, password, mt_server);
            bool success = msg.Contains("成功");

            return Json(new
            {
                success = success,
                message = msg
            });
        }

        #region EA開關單與改單整合工具
        //// 介面
        //public IActionResult EA_OpenAndCloseOrder(string FileDate)
        //{
        //    ViewData["Title"] = UniversalService.Get_Tool_Name("EA_OpenAndCloseOrder");
        //    List<string> fileNameList = EA_OpenAndCloseOrderService.GetEaRankList();
        //    var viewModel_mail = EA_OpenAndCloseOrderService.GetMailRecords();
        //    ViewBag.FileNameList = fileNameList;
        //    ExcelViewModel viewModel = new();

        //    if (fileNameList.Count == 0)
        //    {
        //        viewModel = null;
        //    }
        //    else
        //    {
        //        if (FileDate == "" || FileDate == null)
        //        {
        //            string FileName = fileNameList.Last();
        //            viewModel = EA_OpenAndCloseOrderService.View_Data(FileName);
        //            ViewBag.SelectDate = FileName;
        //        }
        //        else
        //        {
        //            viewModel = EA_OpenAndCloseOrderService.View_Data(FileDate);
        //            ViewBag.SelectDate = FileDate;
        //        }
        //    }
        //    return View("EA_OpenAndCloseOrder/main", Tuple.Create(viewModel, viewModel_mail));
        //}

        //// 取得寄信資訊
        //[HttpGet]
        //public JsonResult EA_OpenAndCloseOrder_GetMailRecords()
        //{
        //    var mailRecords = EA_OpenAndCloseOrderService.GetMailRecords();
        //    return Json(mailRecords);
        //}

        //// 取得寄信LOG
        //[HttpGet]
        //public JsonResult EA_OpenAndCloseOrder_GetMailResults()
        //{
        //    var mailResults = EA_OpenAndCloseOrderService.GetMailResults();
        //    var jsonMailResults = new JsonResult(mailResults);
        //    jsonMailResults.ContentType = "application/json; charset=Big5";
        //    return jsonMailResults;
        //}

        //// 寄信資訊(新增)
        //[HttpPost]
        //public JsonResult EA_OpenAndCloseOrder_MailConfig_Add(string brand, string send_mail, string receive_mail)
        //{
        //    string msg = EA_OpenAndCloseOrderService.MailConfig_Add(brand, send_mail, receive_mail);
        //    bool success = msg.Contains("成功");

        //    return Json(new
        //    {
        //        success = success,
        //        message = msg
        //    });
        //}

        //// 寄信資訊(編輯)
        //[HttpPost]
        //public JsonResult EA_OpenAndCloseOrder_MailConfig_Edit(string brandBefore, string brandAfter, string send_mailBefore, string send_mailAfter, string receive_mailBefore, string receive_mailAfter)
        //{
        //    string msg = EA_OpenAndCloseOrderService.MailConfig_Edit(brandBefore, brandAfter, send_mailBefore, send_mailAfter, receive_mailBefore, receive_mailAfter);
        //    bool success = msg.Contains("成功");

        //    return Json(new
        //    {
        //        success = success,
        //        message = msg
        //    });
        //}

        //// 寄信資訊(刪除)
        //[HttpPost]
        //public JsonResult EA_OpenAndCloseOrder_MailConfig_Delete(string brand, string send_mail, string receive_mail)
        //{
        //    string msg = EA_OpenAndCloseOrderService.MailConfig_Delete(brand, send_mail, receive_mail);
        //    bool success = msg.Contains("成功");

        //    return Json(new
        //    {
        //        success = success,
        //        message = msg
        //    });
        //}
        
        #endregion

        // ------------------------------------------------------ PE Report工具 ------------------------------------------------------

        // 介面
        public IActionResult PE_Report()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("PE_Report");
            ViewBag.entries = PE_ReportService.GetOutputDirEntries();
            return View("PE_Report/main");
        }

        // 開啟資料夾
        [HttpGet]
        public IActionResult GetFileSystemEntries(string path)
        {
            var entries = PE_ReportService.GetFileSystemEntriesList(path.TrimStart('/'));
            return PartialView("_FileSystemEntries", entries);
        }

        // 開啟Excel
        [HttpGet]
        public IActionResult DisplayExcel(string path)
        {
            var viewModel = PE_ReportService.View_Data(path.TrimStart('/'));
            return PartialView("_ExcelView", viewModel);
        }

        // ------------------------------------------------------ EA 緊急查詢工具 ------------------------------------------------------

        //  介面
        public IActionResult EA_Account()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("EA_Account");
            List<string> Labels = EA_AccountService.Get_ServerList();
            ViewBag.Labels = Labels;
            Dictionary<string, DataTable> result = new();
            ViewBag.Result = result;

            return View("EA_Account/main");
        }

        // 刷新表格內容
        [HttpPost]
        public IActionResult Refresh_EA_Data(List<string> ServerList, string StartTime, string EndTime)
        {
            Dictionary<string, DataTable> result = new();
            if (ServerList.Count > 0)
            {
                result = EA_AccountService.EA(ServerList, StartTime, EndTime);
            }
            var jsonResult = result.ToDictionary(
                            kvp => kvp.Key,
                            kvp => new
                            {
                                Columns = kvp.Value.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList(),
                                Rows = kvp.Value.AsEnumerable().Select(r => r.ItemArray.Select(o => o?.ToString() ?? string.Empty).ToList()).ToList()
                            });

            return Json(new { result = jsonResult });
        }

        // ------------------------------------------------------ Holiday轉換工具 ------------------------------------------------------

        // 介面
        public IActionResult Holiday()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("Holiday");
            return View("Holiday/main");
        }

        [HttpGet]
        public IActionResult Holiday_CheckOutput()
        {
            bool outputCheck = HolidayService.oupput_check();
            string outputFileName = HolidayService.Get_Oupput_FileName();
            return Json(new { success = outputCheck, fileName = outputFileName });
        }

        // ------------------------------------------------------ Pelican比對工具 ------------------------------------------------------

        // 介面
        public IActionResult Pelican_Compare(string date)
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("Pelican_Compare");
            List<string> fileNameList = Pelican_CompareService.GetFileNameList();
            ViewBag.FileNameList = fileNameList;
            object viewModel = null;

            if (fileNameList.Count != 0)
            {
                if (date == "" || date == null)
                {
                    string FileName = fileNameList.Last();
                    viewModel = Pelican_CompareService.View_Data(FileName);
                    ViewBag.SelectDate = FileName;
                }
                else
                {
                    viewModel = Pelican_CompareService.View_Data(date);
                    ViewBag.SelectDate = date;
                }
            }
            return View("Pelican_Compare/main", viewModel);
        }


        [HttpPost]
        public IActionResult Pelican_Compare_UpdateExcel([FromForm] string data, [FromForm] string file)
        {
            Pelican_CompareService.UpdateExcel(data, file);
            return Ok();
        }

        // ------------------------------------------------------ Pelican每月費用比對 ------------------------------------------------------

        // 介面
        public IActionResult Pelican_Month_Fee()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("Pelican_Month_Fee");
            string FileName = Pelican_Month_FeeService.GetFileName();
            ViewBag.FileName = FileName;
            return View("Pelican_Month_Fee/Pelican_Month_Fee");
        }

        // 執行工具
        [HttpPost]
        public IActionResult RunExe_parameter(string tool_path, string output_path, string param1, string param2, string param3, string exe_name, bool DeleteAllFile = false)
        {
            string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", tool_path);
            string folderPath = Path.Combine(ToolPath, output_path);
            string exePath = Path.Combine(ToolPath, exe_name);

            // 刪除Output資料夾內所有檔案(預設為不會刪除Output路徑內的所有檔案)
            if (DeleteAllFile)
            {
                if (Directory.Exists(folderPath))
                {
                    DirectoryInfo di = new(folderPath);

                    foreach (FileInfo file_d in di.GetFiles())
                    {
                        file_d.Delete();
                    }
                    foreach (DirectoryInfo dir in di.GetDirectories())
                    {
                        dir.Delete(true);
                    }
                }
            }

            // 執行工具
            if (System.IO.File.Exists(exePath))
            {
                try
                {
                    using Process process = new();
                    process.StartInfo.FileName = exePath;
                    process.StartInfo.WorkingDirectory = ToolPath;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.Arguments = $"\"{param1}\" \"{param2}\" \"{param3}\"";


                    process.Start();
                    while (!process.HasExited)
                    {
                        Thread.Sleep(1000);
                    }
                    process.WaitForExit();

                    int exitCode = process.ExitCode;

                    if (exitCode == 0)
                    {
                        return Ok("工具執行完成");
                    }
                    else
                    {
                        string errorOutput = process.StandardError.ReadToEnd();
                        return BadRequest($"工具執行失敗，退出碼：{exitCode}\n錯誤信息：{errorOutput}");
                    }
                }
                catch
                {
                    return BadRequest("工具發生錯誤");
                }
            }
            else
            {
                return BadRequest("未找到工具");
            }
        }
        //-------------------------------------Benson-----------------------------------------------------

        private readonly ILogger<Pe_setting> _logger;
        private readonly IConfiguration _configuration;

        public DataProductController(ILogger<Pe_setting> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }


        [HttpGet]
        //水平比對工具
        public IActionResult Level_comparison_tool()
        {
            var pe_Level_Setting = new Pe_level_Setting(_logger, _configuration);
            var (aggregationconfigList, executionprofilesList, marketinformationList, pricestreamList, volumebandList) =
                pe_Level_Setting.SQL_list_symbol();

            var viewModel = new Pe_setting.SymbolSettingViewModel
            {
                AggregationconfigList = aggregationconfigList,
                ExecutionprofilesList = executionprofilesList,
                MarketinformationList = marketinformationList,
                PricestreamList = pricestreamList,
                VolumebandList = volumebandList
            };

            return View("PE_level_com/main", viewModel);
        }

        [HttpPost]
        //水平比對工具
        public IActionResult Level_comparison_tool([FromBody] Pe_setting.AjaxDataModel data)
        {

            var pe_Level_Setting = new Pe_level_Setting(_logger, _configuration);

            var sqlnames = data.sqlnames ?? new List<string>();
            var regions = data.regions ?? new List<string>();
            var symbols = data.symbols ?? new List<string>();
            var time_comp = data.times?.ToString("yyyy-MM-dd");


            //判斷要比那些地區
            // 確保它不是 null、且裡面東西不能是空集合
            if (data.regions != null && data.regions.Any())
            {
                var out_ = pe_Level_Setting.ExecuteSQL_serverform(sqlnames, regions, symbols, time_comp);

                if (sqlnames.Count > 0 && sqlnames[0] == "AggregationConfig")
                {
                    var regionGroups = out_.AggregationconfigList
    .GroupBy(x => x.REGION)
    .ToDictionary(g => g.Key, g => g.ToDictionary(i => i.Name));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(aggregationconfig).GetProperties().Select(p => p.Name).ToList();
                    var excludeProps = new[] { "Id", "REGION" };
                    var excludeReturnProps = new[] { "Id", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate", "REGION" };

                    // 只挑出在 2 個以上區域出現的 Name
                    var allNames = out_.AggregationconfigList
                        .GroupBy(x => x.Name)
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();

                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                        // 特別處理 AggregationRule 欄位
                                        if (propName == "AggregationRule" && value is string json && !string.IsNullOrWhiteSpace(json))
                                        {
                                            try
                                            {
                                                var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                                                foreach (var dict in dictList)
                                                {
                                                    dict.Remove("SymbolId");
                                                }

                                                var cleanedJson = JsonConvert.SerializeObject(dictList);
                                                data[propName] = cleanedJson;
                                            }
                                            catch
                                            {
                                                data[propName] = value; // fallback 原始字串
                                            }
                                        }
                                        else
                                        {
                                            data[propName] = value;
                                        }
                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;

                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values
                           .Select(v => v?.ToString() ?? "")
                           .Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    return Json(new
                    {
                        sqlnames,
                        regions = activeRegions,
                        symbols,
                        finalResult = result
                    });


                }
                else if (sqlnames.Count > 0 && sqlnames[0] == "ExecutionProfiles")
                {
                    var regionGroups = out_.ExecutionprofilesList
     .GroupBy(x => x.REGION)
     .ToDictionary(g => g.Key, g => g.ToDictionary(i => i.Name));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(executionprofiles).GetProperties().Select(p => p.Name).ToList();
                    var excludeProps = new[] { "Id", "REGION", "MarketId", "Description", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate" };
                    var excludeReturnProps = new[] { "Id", "MarketId", "Description", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate" };

                    // 只挑出出現在兩個以上區域的 Name
                    var allNames = out_.ExecutionprofilesList
                        .GroupBy(x => x.Name)
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        // 每個區域的資料先收集
                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();
                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);
                                        data[propName] = value;
                                    }
                                }
                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {

                            // 不回傳的就跳過
                            if (excludeReturnProps.Contains(prop)) continue;

                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values.Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                // ➕ 有差異的欄位才回傳
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    return Json(new
                    {
                        sqlnames,
                        regions = activeRegions,
                        symbols,
                        finalResult = result
                    });



                }
                //20250424
                else if (sqlnames.Count > 0 && sqlnames[0] == "MarketInformation")
                {

                    var regionGroups = out_.MarketinformationList
                     .GroupBy(x => x.REGION)
                     .ToDictionary(
                         g => g.Key,
                         g => g.ToDictionary(
                             i => $"{i.FeederSource}-{i.Symbol}", // 使用 FeederSource-Symbol 作為唯一鍵
                             i => i
                         )
                     );

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(marketinformation).GetProperties().Select(p => p.Name).ToList();

                    var excludeProps = new[] { "Id", "INPUT_TIME", "REGION", "MarketId", "Description", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate", "AmountDP", "MarketScope", "MarketTimeZone", "SymbolGroupId" };
                    var excludeReturnProps = new[] { "Id", "MarketStatus", "INPUT_TIME", "REGION", "Name", "MarketId", "Description", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate", "AmountDP", "MarketScope", "SymbolGroupId" };


                    // 只挑出出現在兩個以上區域的 FeederSource-Symbol
                    var allNames = out_.MarketinformationList
                        .GroupBy(x => $"{x.FeederSource}-{x.Symbol}")
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        // 每個區域的資料先收集
                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();
                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);
                                        data[propName] = value;
                                    }
                                }
                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            // 不回傳的就跳過
                            if (excludeReturnProps.Contains(prop)) continue;

                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values.Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                // ➕ 有差異的欄位才回傳
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    return Json(new
                    {
                        sqlnames,
                        regions = activeRegions,
                        symbols,
                        finalResult = result
                    });


                }
                else if (sqlnames.Count > 0 && sqlnames[0] == "PriceStream")
                {
                    var regionGroups = out_.PricestreamList
                    .Where(i => !string.IsNullOrEmpty(i.PriceStreamProfileName) && !string.IsNullOrEmpty(i.Symbol) && !string.IsNullOrEmpty(i.REGION)) // 確保欄位不為空
                    .GroupBy(i => i.REGION)
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToDictionary(
                            i => $"{i.PriceStreamProfileName}-{i.Symbol}", // 使用 PriceStreamProfileName+Symbol 當唯一鍵
                            i => i
                        )
                    );

                    var allRegions = regions;
                    var activeRegions = allRegions
                        .Where(region => regionGroups.ContainsKey(region) && regionGroups[region].Any())
                        .ToList();

                    var propertyNames = typeof(pricestream).GetProperties().Select(p => p.Name).ToList();
                    var allNames = out_.PricestreamList
                        .Where(x => !string.IsNullOrEmpty(x.PriceStreamProfileName) && !string.IsNullOrEmpty(x.Symbol))
                        .Select(x => $"{x.PriceStreamProfileName}-{x.Symbol}")
                        .Distinct()
                        .ToList();

                    //不比較
                    var excludeProps = new[] { "Id", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate", "MarketId", "PricingStreamProfileId", "REGION", "VolumeBandConfigurationId" };
                    //排除顯示欄位
                    var excludeReturnProps = new[] { "Id", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate", "MarketId", "PricingStreamProfileId", "REGION", "VolumeBandConfigurationId" };

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        // 收集各區域資料
                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.TryGetValue(region, out var group) && group.TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();
                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);
                                        data[propName] = value;
                                    }
                                }
                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        // 比對每個欄位
                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;

                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values.Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    return Json(new
                    {
                        sqlnames,
                        regions = activeRegions,
                        symbols,
                        finalResult = result
                    });


                }
                else if (sqlnames.Count > 0 && sqlnames[0] == "Volumeband")
                {
                    var allRegions = regions;

                    var regionGroups = out_.VolumebandList
                        .Where(x => !string.IsNullOrEmpty(x.Region))
                        .GroupBy(x => x.Region)
                        .ToDictionary(
                            g => g.Key,
                            g => g.ToDictionary(i => i.VolumeBandName, i => i)
                        );

                    var activeRegions = allRegions
                        .Where(region => regionGroups.ContainsKey(region) && regionGroups[region].Any())
                        .ToList();

                    var propertyNames = typeof(volumeband).GetProperties().Select(p => p.Name).ToList();
                    var allNames = out_.VolumebandList
                        .Where(x => !string.IsNullOrEmpty(x.VolumeBandName))
                        .Select(x => x.VolumeBandName)
                        .Distinct()
                        .ToList();

                    var excludeProps = new HashSet<string>
                    {
                        "Id","SpreadMode","MinSpread","MaxSpread","FixSpread","EnableOverflow","ApplyVWAP","Multiplier",
                        "VolumeBandConfigurationId","Region","InputTime","Name","UpdatedBy2","UpdatedDate2","CreatedBy2","CreatedDate2",
                        "InputTime2","Region2", "Description", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate"
                    };

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.TryGetValue(region, out var regionItems) && regionItems.TryGetValue(name, out var item))
                            {
                                var data = new Dictionary<string, object>();
                                foreach (var prop in propertyNames)
                                {
                                    if (!excludeProps.Contains(prop))
                                    {
                                        var value = item.GetType().GetProperty(prop)?.GetValue(item);

                                        // 特殊處理 VolumeBandLayer 欄位
                                        if (prop == "VolumeBandLayer" && value is string json && !string.IsNullOrWhiteSpace(json))
                                        {
                                            try
                                            {
                                                var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                                                foreach (var dict in dictList)
                                                {
                                                    dict.Remove("VolumeBandConfigId");
                                                    dict.Remove("UpdatedBy");
                                                    dict.Remove("UpdatedDate");
                                                    dict.Remove("CreatedBy");
                                                    dict.Remove("CreatedDate");

                                                }

                                                var cleanedJson = JsonConvert.SerializeObject(dictList);
                                                data[prop] = cleanedJson;
                                            }
                                            catch
                                            {
                                                data[prop] = value; // fallback 原始字串
                                            }
                                        }
                                        else
                                        {
                                            data[prop] = value;
                                        }
                                    }
                                }
                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeProps.Contains(prop)) continue;

                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values.Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    return Json(new
                    {
                        sqlnames,
                        regions = activeRegions,
                        symbols,
                        finalResult = result
                    });

                }
                else
                {
                    return Json(new
                    {
                        sqlnames,
                        regions,
                        symbols,

                    });
                }
            }
            else
            {
                return Json(new
                {
                    sqlnames,
                    regions,
                    symbols,
                });
            }
        }

        [HttpPost]
        //水平比對工具
        public IActionResult Level_comparison_weekly([FromBody] Pe_setting.AjaxDataModel data)
        {

            // tablelist
            var pe_Level_Setting = new Pe_level_Setting(_logger, _configuration);

            var sqlnames = new List<string> { "AggregationConfig", "ExecutionProfiles", "MarketInformation", "PriceStream", "Volumeband" };
            //var sqlnames = new List<string> { "AggregationConfig", "ExecutionProfiles" };

            var regions = new List<string> { "LD", "NY", "TY" };

            var symbols = new List<string> { "ALL" };
            var time_comp = data.times?.ToString("yyyy-MM-dd");


            // 初始化 list_all
            List<object> list_all = new List<object>();

            //判斷要比那些地區

            for (int i = 0; i < sqlnames.Count; i++)
            {
                var out_ = pe_Level_Setting.ExecuteSQL_serverform(sqlnames, regions, symbols, time_comp);

                if (sqlnames[i] == "AggregationConfig")
                {
                    var excludeProps = new[] { "Id", "REGION" };
                    var excludeReturnProps = new[] { "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate", "REGION" };

                    var finalResult = pe_Level_Setting.ProcessRegionData(out_.AggregationconfigList, excludeProps, excludeReturnProps);

                    var AggregationConfig_out = new
                    {
                        sqlnames = sqlnames[i],
                        regions = new[] { "NY", "LD", "TY" },
                        symbols,
                        finalResult
                    };
                    list_all.Add(AggregationConfig_out);
                }
                else if (sqlnames[i] == "ExecutionProfiles")
                {
                    var excludeProps = new[] { "Id", "REGION", "MarketId", "Description", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate" };
                    var excludeReturnProps = new[] { "MarketId", "Description", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate" };

                    var finalResult = pe_Level_Setting.ProcessRegionData(out_.ExecutionprofilesList, excludeProps, excludeReturnProps);

                    var ExecutionProfiles_out = new
                    {
                        sqlnames = sqlnames[i],
                        regions = new[] { "NY", "LD", "TY" },
                        symbols,
                        finalResult
                    };
                    list_all.Add(ExecutionProfiles_out);
                }
                else if (sqlnames.Count > 0 && sqlnames[i] == "MarketInformation")
                {

                    var regionGroups = out_.MarketinformationList
                     .GroupBy(x => x.REGION)
                     .ToDictionary(
                         g => g.Key,
                         g => g.ToDictionary(
                             i => $"{i.FeederSource}-{i.Symbol}",
                             i => i
                         )
                     );

                    var allRegions = new[] { "NY", "LD", "TY" };
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(marketinformation).GetProperties().Select(p => p.Name).ToList();

                    var excludeProps = new[] { "Id", "INPUT_TIME", "REGION", "MarketId", "Description", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate", "AmountDP", "MarketScope", "MarketTimeZone", "SymbolGroupId" };
                    var excludeReturnProps = new[] { "Id", "MarketStatus", "INPUT_TIME", "REGION", "Name", "MarketId", "Description", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate", "AmountDP", "MarketScope", "SymbolGroupId" };

                    // 挑出出現在兩個以上區域的 FeederSource-Symbol
                    var allNames = out_.MarketinformationList
                        .GroupBy(x => $"{x.FeederSource}-{x.Symbol}")
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        // 取出每個區域資料
                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();
                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);
                                        data[propName] = value;
                                    }
                                }
                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;

                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values.Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    var MarketInformation_out = new
                    {
                        sqlnames = sqlnames[i],
                        regions = activeRegions,
                        symbols,
                        finalResult = result
                    };

                    list_all.Add(MarketInformation_out);

                }
                else if (sqlnames.Count > 0 && sqlnames[i] == "PriceStream")
                {
                    var regionGroups = out_.PricestreamList
       .Where(i => !string.IsNullOrEmpty(i.PriceStreamProfileName) && !string.IsNullOrEmpty(i.Symbol) && !string.IsNullOrEmpty(i.REGION))
       .GroupBy(i => i.REGION)
       .ToDictionary(
           g => g.Key,
           g => g.ToDictionary(
               i => $"{i.PriceStreamProfileName}{i.Symbol}", // 使用 PriceStreamProfileName 和 Symbol 作為唯一鍵
               i => i
           )
       );

                    var allRegions = new[] { "NY", "LD", "TY" };
                    var activeRegions = allRegions
                        .Where(region => regionGroups.ContainsKey(region) && regionGroups[region].Any()) // 只保留有資料的區域
                        .ToList();

                    var propertyNames = typeof(pricestream).GetProperties().Select(p => p.Name).ToList();
                    var allNames = out_.PricestreamList
                        .Where(x => !string.IsNullOrEmpty(x.PriceStreamProfileName) && !string.IsNullOrEmpty(x.Symbol))
                        .Select(x => $"{x.PriceStreamProfileName}{x.Symbol}")
                        .Distinct()
                        .ToList();
                    //不比較
                    var excludeProps = new[] { "Id", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate", "MarketId", "PricingStreamProfileId", "REGION", "VolumeBandConfigurationId" };
                    //排除顯示欄位
                    var excludeReturnProps = new[] { "Id", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate", "MarketId", "PricingStreamProfileId", "REGION", "VolumeBandConfigurationId" };

                    var result = allNames.Select(name =>
                    {
                        var columns = new Dictionary<string, object>();
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        // 收集每個區域的資料
                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.TryGetValue(region, out var group) && group.TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();
                                foreach (var propName in propertyNames)
                                {
                                    var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                    if (!excludeReturnProps.Contains(propName))
                                    {
                                        columns[$"{propName}{region}"] = value; // 填入欄位資料
                                    }

                                    if (!excludeProps.Contains(propName))
                                    {
                                        data[propName] = value; // 收集每個區域的資料
                                    }
                                }
                                regionColumnData[region] = data;
                            }
                            else
                            {
                                // 若區域無資料，補充 null 欄位
                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeReturnProps.Contains(propName))
                                    {
                                        columns[$"{propName}{region}"] = null;
                                    }
                                }
                            }
                        }

                        // 檢查資料是否完全相同
                        bool allRegionsIdentical = regionColumnData.Values
                            .Select(dict => string.Join(";", dict.OrderBy(k => k.Key).Select(kv => $"{kv.Key}:{kv.Value}")))
                            .Distinct()
                            .Count() == 1;

                        return allRegionsIdentical ? null : new
                        {
                            Name = name,
                            Columns = columns
                        };

                    }).Where(x => x != null);

                    var finalResult = result.ToList();

                    var PriceStream_out = new
                    {
                        sqlnames = sqlnames[i],
                        regions = activeRegions,
                        symbols,
                        finalResult
                    };

                    list_all.Add(PriceStream_out);


                }
                else if (sqlnames.Count > 0 && sqlnames[i] == "Volumeband")
                {
                    var regionGroups = out_.VolumebandList
    .Where(x => !string.IsNullOrEmpty(x.Region))
    .GroupBy(x => x.Region)
    .ToDictionary(
        g => g.Key,
        g => g.ToDictionary(
            i => i.VolumeBandName,
            i => i
        )
    );

                    var allRegions = new[] { "NY", "LD", "TY" };
                    var activeRegions = allRegions.Where(region => regionGroups.ContainsKey(region) && regionGroups[region].Any()).ToList();

                    var propertyNames = typeof(volumeband).GetProperties().Select(p => p.Name).ToList();
                    var allNames = out_.VolumebandList.Select(x => x.VolumeBandName).Distinct();

                    var excludeProps = new[] {
    "Id","SpreadMode","MinSpread","MaxSpread","FixSpread","EnableOverflow","ApplyVWAP","Multiplier",
    "VolumeBandConfigurationId","Region","InputTime","Name","UpdatedBy2","UpdatedDate2","CreatedBy2","CreatedDate2",
    "InputTime2","Region2", "Description", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate"
};

                    var excludeReturnProps = new[] {
    "Id","SpreadMode","MinSpread","MaxSpread","FixSpread","EnableOverflow","ApplyVWAP","Multiplier",
    "VolumeBandConfigurationId","Region","InputTime","Name","UpdatedBy2","UpdatedDate2","CreatedBy2","CreatedDate2",
    "InputTime2","Region2", "Description", "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate"
};

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();
                                foreach (var propName in propertyNames)
                                {
                                    var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                    if (propName == "VolumeBandLayer" && value is string jsonString && !string.IsNullOrWhiteSpace(jsonString))
                                    {
                                        try
                                        {
                                            var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonString);

                                            foreach (var dict in dictList)
                                            {
                                                dict.Remove("VolumeBandConfigId");
                                                dict.Remove("UpdatedBy");
                                                dict.Remove("UpdatedDate");
                                                dict.Remove("CreatedBy");
                                                dict.Remove("CreatedDate");
                                            }

                                            var cleanedJson = JsonConvert.SerializeObject(dictList);
                                            if (!excludeProps.Contains(propName))
                                            {
                                                data[propName] = cleanedJson;
                                            }
                                        }
                                        catch
                                        {
                                            if (!excludeProps.Contains(propName))
                                            {
                                                data[propName] = value;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (!excludeProps.Contains(propName))
                                        {
                                            data[propName] = value;
                                        }
                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;

                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values.Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    var Volumeband_out = new
                    {
                        sqlnames = sqlnames[i],
                        regions = activeRegions,
                        symbols,
                        finalResult = result
                    };

                    list_all.Add(Volumeband_out);

                }
            }
            // 返回 list_all 作為 JSON
            return Json(list_all);
        }


        //-------------------------------------Benson_new-----------------------------------------------------





        //要先get
        [HttpGet]
        public IActionResult Level_comparison_toolNew()
        {
            var pe_Level_Setting = new Pe_level_Setting(_logger, _configuration);

            var (model_markets_symbolList, source_markets_symbolList, price_streamList, aggregator_marketsList, aggregator_markets_ruleList,
                execution_profilesList, volume_bandList) =
                pe_Level_Setting.New_SQL_list_symbol();

            var viewModel = new New_Pe_setting.SymbolSettingViewModel
            {
                model_markets_symbolList = model_markets_symbolList,
                source_markets_symbolList = source_markets_symbolList,
                price_streamList = price_streamList,
                aggregator_marketsList = aggregator_marketsList,
                aggregator_markets_ruleList = aggregator_markets_ruleList,
                execution_profilesList = execution_profilesList,
                volume_bandList = volume_bandList,
            };
            return View("PE_level_com/main_new", viewModel);
        }


        [HttpPost]
        public IActionResult Level_comparison_toolNew([FromBody] New_Pe_setting.AjaxDataModel data)
        {
            var pe_Level_Setting = new Pe_level_Setting(_logger, _configuration);

            var (model_markets_symbolList, source_markets_symbolList, price_streamList, aggregator_marketsList, aggregator_markets_ruleList,
                execution_profilesList, volume_bandList) =
                pe_Level_Setting.New_SQL_list_symbol();

            var viewModel = new New_Pe_setting.SymbolSettingViewModel
            {
                model_markets_symbolList = model_markets_symbolList,
                source_markets_symbolList = source_markets_symbolList,
                price_streamList = price_streamList,
                aggregator_marketsList = aggregator_marketsList,
                aggregator_markets_ruleList = aggregator_markets_ruleList,
                execution_profilesList = execution_profilesList,
                volume_bandList = volume_bandList,
            };
            New_Pe_setting new_Pe_Setting = new New_Pe_setting();

            var sqlnames = data.sqlnames ?? new List<string>();
            var regions = data.regions ?? new List<string>();
            var symbols = data.symbols ?? new List<string>();
            var time_comp = data.times?.ToString("yyyy-MM-dd");

            Debug.WriteLine($@"
            sqlnames: {string.Join(", ", sqlnames)}
            regions: {string.Join(", ", regions)}
            symbols: {string.Join(", ", symbols)}
            time_comp: {time_comp}
            ");


            //判斷要比那些地區
            // 確保它不是 null、且裡面東西不能是空集合
            if (data.regions != null && data.regions.Any())
            {
                var out_ = pe_Level_Setting.New_ExecuteSQL_serverform(sqlnames, regions, symbols, time_comp);

                if (sqlnames.Count > 0 && sqlnames[0] == "model_markets_symbol")
                {
                    var regionGroups = out_.model_markets_symbolList
                    .GroupBy(x => x.Region)
                    .ToDictionary(g => g.Key, g => g.ToDictionary(i => i.Symbol));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(model_markets_symbol).GetProperties().Select(p => p.Name).ToList();
                    var excludeProps = new[] {
                        "Id",
                        "REGION",
                        "Region",
                        "SymbolGroupId",
                         "Name"
                    };
                    var excludeReturnProps = new[] {
                        "Id",
                        "UpdatedBy",
                        "REGION",
                        "SymbolGroupId",
                        "Name"
                    };

                    // 只挑出在 2 個以上區域出現的 Name
                    var allNames = out_.model_markets_symbolList
                        .GroupBy(x => x.Symbol)
                        .Where(g => g.Select(x => x.Region).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();

                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                        data[propName] = value;

                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;
                            //這個可以
                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values
                            .Select(v => v?.ToString() ?? "")
                            .Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    return Json(new
                    {
                        sqlnames,
                        regions = activeRegions,
                        symbols,
                        finalResult = result
                    });

                }
                else if (sqlnames.Count > 0 && sqlnames[0] == "source_markets_symbol")
                {

                    var regionGroups = out_.source_markets_symbolList
                   .GroupBy(x => x.REGION)
                   .ToDictionary(g => g.Key, g => g.ToDictionary(i => $"{i.FeederSource}-{i.Symbol}"));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(source_markets_symbol).GetProperties().Select(p => p.Name).ToList();
                    var excludeProps = new[] { "Id", "REGION" };
                    var excludeReturnProps = new[] { "Id", "REGION" };

                    // 只挑出在 2 個以上區域出現的 Name
                    var allNames = out_.source_markets_symbolList
                        .GroupBy(x => x.Name)
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();

                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                        // 特別處理 AggregationRule 欄位
                                        if (propName == "AggregationRule" && value is string json && !string.IsNullOrWhiteSpace(json))
                                        {
                                            try
                                            {
                                                var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                                                foreach (var dict in dictList)
                                                {
                                                    dict.Remove("SymbolId");
                                                }

                                                var cleanedJson = JsonConvert.SerializeObject(dictList);
                                                data[propName] = cleanedJson;
                                            }
                                            catch
                                            {
                                                data[propName] = value; // fallback 原始字串
                                            }
                                        }
                                        else
                                        {
                                            data[propName] = value;
                                        }
                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;
                            //這個可以
                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values
                            .Select(v => v?.ToString() ?? "")
                            .Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    return Json(new
                    {
                        sqlnames,
                        regions = activeRegions,
                        symbols,
                        finalResult = result
                    });

                }
                else if (sqlnames.Count > 0 && sqlnames[0] == "price_stream")
                {

                    var regionGroups = out_.price_streamList
                   .GroupBy(x => x.REGION)
                   .ToDictionary(g => g.Key, g => g.ToDictionary(i => $"{i.Name}"));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(price_stream).GetProperties().Select(p => p.Name).ToList();
                    var excludeProps = new[] { "REGION", "Id", "ModelMarketId", "VolumeBandId", "PriceStreamId" };
                    var excludeReturnProps = new[] { "REGION", "Id", "ModelMarketId", "VolumeBandId", "PriceStreamId" };

                    // 只挑出在 2 個以上區域出現的 Name
                    var allNames = out_.price_streamList
                        .GroupBy(x => x.Name)
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();

                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                        // 特別處理 AggregationRule 欄位
                                        if (propName == "AggregationRule" && value is string json && !string.IsNullOrWhiteSpace(json))
                                        {
                                            try
                                            {
                                                var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                                                foreach (var dict in dictList)
                                                {
                                                    dict.Remove("SymbolId");
                                                }

                                                var cleanedJson = JsonConvert.SerializeObject(dictList);
                                                data[propName] = cleanedJson;
                                            }
                                            catch
                                            {
                                                data[propName] = value; // fallback 原始字串
                                            }
                                        }
                                        else
                                        {
                                            data[propName] = value;
                                        }
                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;
                            //這個可以
                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values
                            .Select(v => v?.ToString() ?? "")
                            .Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    return Json(new
                    {
                        sqlnames,
                        regions = activeRegions,
                        symbols,
                        finalResult = result
                    });

                }
                else if (sqlnames.Count > 0 && sqlnames[0] == "aggregator_markets")
                {

                    var regionGroups = out_.aggregator_marketsList
                    .GroupBy(x => x.REGION)
                    .ToDictionary(g => g.Key, g => g.ToDictionary(i => $"{i.Name}"));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(aggregator_markets).GetProperties().Select(p => p.Name).ToList();
                    var excludeProps = new[] { "REGION", "Id" };
                    var excludeReturnProps = new[] { "REGION", "Id" };

                    // 只挑出在 2 個以上區域出現的 Name
                    var allNames = out_.aggregator_marketsList
                        .GroupBy(x => x.Name)
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();

                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                        // 特別處理 AggregationRule 欄位
                                        if (propName == "AggregationRule" && value is string json && !string.IsNullOrWhiteSpace(json))
                                        {
                                            try
                                            {
                                                var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                                                foreach (var dict in dictList)
                                                {
                                                    dict.Remove("SymbolId");
                                                }

                                                var cleanedJson = JsonConvert.SerializeObject(dictList);
                                                data[propName] = cleanedJson;
                                            }
                                            catch
                                            {
                                                data[propName] = value; // fallback 原始字串
                                            }
                                        }
                                        else
                                        {
                                            data[propName] = value;
                                        }
                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;
                            //這個可以
                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values
                            .Select(v => v?.ToString() ?? "")
                            .Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    return Json(new
                    {
                        sqlnames,
                        regions = activeRegions,
                        symbols,
                        finalResult = result
                    });

                }
                //else if (sqlnames.Count > 0 && sqlnames[0] == "aggregator_markets_rule")
                //{

                //    var regionGroups = out_.aggregator_markets_ruleList
                //    .GroupBy(x => x.REGION)
                //    .ToDictionary(g => g.Key, g => g.ToDictionary(i => $"{i.Name}"));

                //    var allRegions = regions;
                //    var activeRegions = allRegions.ToList();

                //    var propertyNames = typeof(aggregator_markets_rule).GetProperties().Select(p => p.Name).ToList();
                //    var excludeProps = new[] { "Id", "AggregationMarketId", "SourceMarketId", "REGION" };
                //    var excludeReturnProps = new[] { "Id", "AggregationMarketId", "SourceMarketId", "REGION" };

                //    // 只挑出在 2 個以上區域出現的 Name
                //    var allNames = out_.aggregator_markets_ruleList
                //        .GroupBy(x => x.Name)
                //        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                //        .Select(g => g.Key)
                //        .ToList();

                //    var result = allNames.Select(name =>
                //    {
                //        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                //        foreach (var region in activeRegions)
                //        {
                //            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                //            {
                //                var data = new Dictionary<string, object>();

                //                foreach (var propName in propertyNames)
                //                {
                //                    if (!excludeProps.Contains(propName))
                //                    {
                //                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                //                        // 特別處理 AggregationRule 欄位
                //                        if (propName == "AggregationRule" && value is string json && !string.IsNullOrWhiteSpace(json))
                //                        {
                //                            try
                //                            {
                //                                var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                //                                foreach (var dict in dictList)
                //                                {
                //                                    dict.Remove("SymbolId");
                //                                }

                //                                var cleanedJson = JsonConvert.SerializeObject(dictList);
                //                                data[propName] = cleanedJson;
                //                            }
                //                            catch
                //                            {
                //                                data[propName] = value; // fallback 原始字串
                //                            }
                //                        }
                //                        else
                //                        {
                //                            data[propName] = value;
                //                        }
                //                    }
                //                }

                //                regionColumnData[region] = data;
                //            }
                //        }

                //        var diffColumns = new Dictionary<string, object>();

                //        foreach (var prop in propertyNames)
                //        {
                //            if (excludeReturnProps.Contains(prop)) continue;
                //            //這個可以
                //            var valuesByRegion = regionColumnData
                //                .Where(r => r.Value.ContainsKey(prop))
                //                .ToDictionary(r => r.Key, r => r.Value[prop]);

                //            var distinctValues = valuesByRegion.Values
                //            .Select(v => v?.ToString() ?? "")
                //            .Distinct();

                //            if (distinctValues.Count() > 1)
                //            {
                //                foreach (var region in activeRegions)
                //                {
                //                    valuesByRegion.TryGetValue(region, out var value);
                //                    diffColumns[$"{prop}{region}"] = value;
                //                }
                //            }
                //        }

                //        return diffColumns.Any() ? new
                //        {
                //            Name = name,
                //            Columns = diffColumns
                //        } : null;

                //    }).Where(x => x != null).ToList();

                //    return Json(new
                //    {
                //        sqlnames,
                //        regions = activeRegions,
                //        symbols,
                //        finalResult = result
                //    });

                //}
                else if (sqlnames.Count > 0 && sqlnames[0] == "execution_profiles")
                {

                    var regionGroups = out_.execution_profilesList
                    .GroupBy(x => x.REGION)
                    .ToDictionary(g => g.Key, g => g.ToDictionary(i => $"{i.Name}"));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(execution_profiles).GetProperties().Select(p => p.Name).ToList();
                    //不想要看的欄位
                    var excludeProps = new[] { "REGION", "Id" };
                    var excludeReturnProps = new[] { "REGION", "Id" };

                    // 只挑出在 2 個以上區域出現的 Name
                    var allNames = out_.execution_profilesList
                        .GroupBy(x => x.Name)
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();

                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                        // 特別處理 AggregationRule 欄位
                                        if (propName == "AggregationRule" && value is string json && !string.IsNullOrWhiteSpace(json))
                                        {
                                            try
                                            {
                                                var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                                                foreach (var dict in dictList)
                                                {
                                                    dict.Remove("SymbolId");
                                                }

                                                var cleanedJson = JsonConvert.SerializeObject(dictList);
                                                data[propName] = cleanedJson;
                                            }
                                            catch
                                            {
                                                data[propName] = value; // fallback 原始字串
                                            }
                                        }
                                        else
                                        {
                                            data[propName] = value;
                                        }
                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;
                            //這個可以
                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values
                            .Select(v => v?.ToString() ?? "")
                            .Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    return Json(new
                    {
                        sqlnames,
                        regions = activeRegions,
                        symbols,
                        finalResult = result
                    });

                }
                else if (sqlnames.Count > 0 && sqlnames[0] == "volume_band")
                {

                    var regionGroups = out_.volume_bandList
                    .GroupBy(x => x.REGION)
                    .ToDictionary(g => g.Key, g => g.ToDictionary(i => $"{i.Name}"));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(volume_band).GetProperties().Select(p => p.Name).ToList();
                    var excludeProps = new[] { "Id", "REGION", "VolumeBandId", "Description" };
                    var excludeReturnProps = new[] { "Id", "VolumeBandId", "Description" };

                    // 只挑出在 2 個以上區域出現的 Name
                    var allNames = out_.volume_bandList
                        .GroupBy(x => x.Name)
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();

                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                        // 特別處理 AggregationRule 欄位
                                        if (propName == "AggregationRule" && value is string json && !string.IsNullOrWhiteSpace(json))
                                        {
                                            try
                                            {
                                                var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                                                foreach (var dict in dictList)
                                                {
                                                    dict.Remove("SymbolId");
                                                }

                                                var cleanedJson = JsonConvert.SerializeObject(dictList);
                                                data[propName] = cleanedJson;
                                            }
                                            catch
                                            {
                                                data[propName] = value; // fallback 原始字串
                                            }
                                        }
                                        else
                                        {
                                            data[propName] = value;
                                        }
                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;
                            //這個可以
                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values
                            .Select(v => v?.ToString() ?? "")
                            .Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    return Json(new
                    {
                        sqlnames,
                        regions = activeRegions,
                        symbols,
                        finalResult = result
                    });


                }

            }

            return Json(new
            {
                sqlnames,
                regions,
                symbols,
            });
        }

        [HttpPost]
        //水平比對工具
        public IActionResult Level_comparison_weeklyNew([FromBody] New_Pe_setting.AjaxDataModel data)
        {

            // tablelist
            var pe_Level_Setting = new Pe_level_Setting(_logger, _configuration);

            var sqlnames = new List<string> {
                "model_markets_symbol",
                "source_markets_symbol",
                "price_stream",
                "aggregator_markets",
                "aggregator_markets_rule",
                "execution_profiles",
                "volume_band"
            };
            //var sqlnames = new List<string> { "AggregationConfig", "ExecutionProfiles" };

            var regions = new List<string> { "LD", "NY", "TY", "HK","MENA"};

            var symbols = new List<string> { "ALL" };
            var time_comp = data.times?.ToString("yyyy-MM-dd");

            // 初始化 list_all
            List<object> list_all = new List<object>();

            for (int i = 0; i < sqlnames.Count; i++)
            {
                var out_ = pe_Level_Setting.New_ExecuteSQL_serverform(sqlnames, regions, symbols, time_comp);

                if (sqlnames[i] == "model_markets_symbol")
                {
                    var regionGroups = out_.model_markets_symbolList
                   .GroupBy(x => x.Region)
                   .ToDictionary(g => g.Key, g => g.ToDictionary(i => i.Symbol));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(model_markets_symbol).GetProperties().Select(p => p.Name).ToList();
                    var excludeProps = new[] {
                    "Id",
                    "REGION",
                    "Region",
                    "SymbolGroupId",
                    "Name"
                    };
                    var excludeReturnProps = new[] {
                    "Id",
                    "UpdatedBy",
                    "REGION",
                    "SymbolGroupId",
                    "Name"
                    };
                    // 只挑出在 2 個以上區域出現的 Name
                    var allNames = out_.model_markets_symbolList
                        .GroupBy(x => x.Symbol)
                        .Where(g => g.Select(x => x.Region).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();

                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                        data[propName] = value;

                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;
                            //這個可以
                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values
                            .Select(v => v?.ToString() ?? "")
                            .Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();


                    //model_markets_symbol
                    var model_markets_symbol_out = new
                    {
                        sqlnames = sqlnames[i],
                        regions = new[] { "NY", "LD", "TY", "HK", "MENA" },
                        symbols,
                        finalResult = result
                    };
                    list_all.Add(model_markets_symbol_out);
                }
                else if (sqlnames[i] == "source_markets_symbol")
                {
                    //source_markets_symbol
                    var regionGroups = out_.source_markets_symbolList
                .GroupBy(x => x.REGION)
                .ToDictionary(g => g.Key, g => g.ToDictionary(i => $"{i.FeederSource}-{i.Symbol}"));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(source_markets_symbol).GetProperties().Select(p => p.Name).ToList();
                    var excludeProps = new[] { "Id", "REGION" };
                    var excludeReturnProps = new[] { "Id", "REGION" };

                    // 只挑出在 2 個以上區域出現的 Name
                    var allNames = out_.source_markets_symbolList
                        .GroupBy(x => x.Name)
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();

                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                        // 特別處理 AggregationRule 欄位
                                        if (propName == "AggregationRule" && value is string json && !string.IsNullOrWhiteSpace(json))
                                        {
                                            try
                                            {
                                                var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                                                foreach (var dict in dictList)
                                                {
                                                    dict.Remove("SymbolId");
                                                }

                                                var cleanedJson = JsonConvert.SerializeObject(dictList);
                                                data[propName] = cleanedJson;
                                            }
                                            catch
                                            {
                                                data[propName] = value; // fallback 原始字串
                                            }
                                        }
                                        else
                                        {
                                            data[propName] = value;
                                        }
                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;
                            //這個可以
                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values
                            .Select(v => v?.ToString() ?? "")
                            .Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    //source_markets_symbol
                    var source_markets_symbol_out = new
                    {
                        sqlnames = sqlnames[i],
                        regions = new[] { "NY", "LD", "TY", "HK", "MENA" },
                        symbols,
                        finalResult = result
                    };
                    list_all.Add(source_markets_symbol_out);

                }


                else if (sqlnames.Count > 0 && sqlnames[i] == "price_stream")
                {

                    var regionGroups = out_.price_streamList
                 .GroupBy(x => x.REGION)
                 .ToDictionary(g => g.Key, g => g.ToDictionary(i => $"{i.Name}"));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(price_stream).GetProperties().Select(p => p.Name).ToList();
                    var excludeProps = new[] { "REGION", "Id", "ModelMarketId", "VolumeBandId", "PriceStreamId" };
                    var excludeReturnProps = new[] { "REGION", "Id", "ModelMarketId", "VolumeBandId", "PriceStreamId" };

                    // 只挑出在 2 個以上區域出現的 Name
                    var allNames = out_.price_streamList
                        .GroupBy(x => x.Name)
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();

                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                        // 特別處理 AggregationRule 欄位
                                        if (propName == "AggregationRule" && value is string json && !string.IsNullOrWhiteSpace(json))
                                        {
                                            try
                                            {
                                                var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                                                foreach (var dict in dictList)
                                                {
                                                    dict.Remove("SymbolId");
                                                }

                                                var cleanedJson = JsonConvert.SerializeObject(dictList);
                                                data[propName] = cleanedJson;
                                            }
                                            catch
                                            {
                                                data[propName] = value; // fallback 原始字串
                                            }
                                        }
                                        else
                                        {
                                            data[propName] = value;
                                        }
                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;
                            //這個可以
                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values
                            .Select(v => v?.ToString() ?? "")
                            .Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();




                    //price_stream
                    var price_stream_out = new
                    {
                        sqlnames = sqlnames[i],
                        regions = new[] { "NY", "LD", "TY", "HK", "MENA" },
                        symbols,
                        finalResult = result
                    };
                    list_all.Add(price_stream_out);


                }
                else if (sqlnames.Count > 0 && sqlnames[i] == "aggregator_markets")
                {

                    var regionGroups = out_.aggregator_marketsList
                    .GroupBy(x => x.REGION)
                    .ToDictionary(g => g.Key, g => g.ToDictionary(i => $"{i.Name}"));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(aggregator_markets).GetProperties().Select(p => p.Name).ToList();
                    var excludeProps = new[] { "REGION", "Id" };
                    var excludeReturnProps = new[] { "REGION", "Id" };

                    // 只挑出在 2 個以上區域出現的 Name
                    var allNames = out_.aggregator_marketsList
                        .GroupBy(x => x.Name)
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();

                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                        // 特別處理 AggregationRule 欄位
                                        if (propName == "AggregationRule" && value is string json && !string.IsNullOrWhiteSpace(json))
                                        {
                                            try
                                            {
                                                var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                                                foreach (var dict in dictList)
                                                {
                                                    dict.Remove("SymbolId");
                                                }

                                                var cleanedJson = JsonConvert.SerializeObject(dictList);
                                                data[propName] = cleanedJson;
                                            }
                                            catch
                                            {
                                                data[propName] = value; // fallback 原始字串
                                            }
                                        }
                                        else
                                        {
                                            data[propName] = value;
                                        }
                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;
                            //這個可以
                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values
                            .Select(v => v?.ToString() ?? "")
                            .Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();

                    //aggregator_markets
                    var aggregator_markets_out = new
                    {
                        sqlnames = sqlnames[i],
                        regions = new[] { "NY", "LD", "TY", "HK", "MENA" },
                        symbols,
                        finalResult = result
                    };
                    list_all.Add(aggregator_markets_out);

                }
                //else if (sqlnames.Count > 0 && sqlnames[i] == "aggregator_markets_rule")
                //{
                //}
                else if (sqlnames.Count > 0 && sqlnames[i] == "execution_profiles")
                {
                    var regionGroups = out_.execution_profilesList
                   .GroupBy(x => x.REGION)
                   .ToDictionary(g => g.Key, g => g.ToDictionary(i => $"{i.Name}"));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(execution_profiles).GetProperties().Select(p => p.Name).ToList();
                    //不想要看的欄位
                    var excludeProps = new[] { "REGION", "Id" };
                    var excludeReturnProps = new[] { "REGION", "Id" };

                    // 只挑出在 2 個以上區域出現的 Name
                    var allNames = out_.execution_profilesList
                        .GroupBy(x => x.Name)
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();

                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                        // 特別處理 AggregationRule 欄位
                                        if (propName == "AggregationRule" && value is string json && !string.IsNullOrWhiteSpace(json))
                                        {
                                            try
                                            {
                                                var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                                                foreach (var dict in dictList)
                                                {
                                                    dict.Remove("SymbolId");
                                                }

                                                var cleanedJson = JsonConvert.SerializeObject(dictList);
                                                data[propName] = cleanedJson;
                                            }
                                            catch
                                            {
                                                data[propName] = value; // fallback 原始字串
                                            }
                                        }
                                        else
                                        {
                                            data[propName] = value;
                                        }
                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;
                            //這個可以
                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values
                            .Select(v => v?.ToString() ?? "")
                            .Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();



                    //aggregator_markets
                    var execution_profiles_out = new
                    {
                        sqlnames = sqlnames[i],
                        regions = new[] { "NY", "LD", "TY", "HK", "MENA" },
                        symbols,
                        finalResult = result
                    };
                    list_all.Add(execution_profiles_out);



                }
                else if (sqlnames.Count > 0 && sqlnames[i] == "volume_band")
                {
                    var regionGroups = out_.volume_bandList
                   .GroupBy(x => x.REGION)
                   .ToDictionary(g => g.Key, g => g.ToDictionary(i => $"{i.Name}"));

                    var allRegions = regions;
                    var activeRegions = allRegions.ToList();

                    var propertyNames = typeof(volume_band).GetProperties().Select(p => p.Name).ToList();
                    var excludeProps = new[] { "Id", "REGION", "VolumeBandId", "Description" };
                    var excludeReturnProps = new[] { "Id", "VolumeBandId", "Description" };

                    // 只挑出在 2 個以上區域出現的 Name
                    var allNames = out_.volume_bandList
                        .GroupBy(x => x.Name)
                        .Where(g => g.Select(x => x.REGION).Distinct().Count() >= 2)
                        .Select(g => g.Key)
                        .ToList();

                    var result = allNames.Select(name =>
                    {
                        var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                        foreach (var region in activeRegions)
                        {
                            if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                            {
                                var data = new Dictionary<string, object>();

                                foreach (var propName in propertyNames)
                                {
                                    if (!excludeProps.Contains(propName))
                                    {
                                        var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem, null);

                                        // 特別處理 AggregationRule 欄位
                                        if (propName == "AggregationRule" && value is string json && !string.IsNullOrWhiteSpace(json))
                                        {
                                            try
                                            {
                                                var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                                                foreach (var dict in dictList)
                                                {
                                                    dict.Remove("SymbolId");
                                                }

                                                var cleanedJson = JsonConvert.SerializeObject(dictList);
                                                data[propName] = cleanedJson;
                                            }
                                            catch
                                            {
                                                data[propName] = value; // fallback 原始字串
                                            }
                                        }
                                        else
                                        {
                                            data[propName] = value;
                                        }
                                    }
                                }

                                regionColumnData[region] = data;
                            }
                        }

                        var diffColumns = new Dictionary<string, object>();

                        foreach (var prop in propertyNames)
                        {
                            if (excludeReturnProps.Contains(prop)) continue;
                            //這個可以
                            var valuesByRegion = regionColumnData
                                .Where(r => r.Value.ContainsKey(prop))
                                .ToDictionary(r => r.Key, r => r.Value[prop]);

                            var distinctValues = valuesByRegion.Values
                            .Select(v => v?.ToString() ?? "")
                            .Distinct();

                            if (distinctValues.Count() > 1)
                            {
                                foreach (var region in activeRegions)
                                {
                                    valuesByRegion.TryGetValue(region, out var value);
                                    diffColumns[$"{prop}{region}"] = value;
                                }
                            }
                        }

                        return diffColumns.Any() ? new
                        {
                            Name = name,
                            Columns = diffColumns
                        } : null;

                    }).Where(x => x != null).ToList();




                    //aggregator_markets
                    var volume_band_out = new
                    {
                        sqlnames = sqlnames[i],
                        regions = new[] { "NY", "LD", "TY", "HK", "MENA" },
                        symbols,
                        finalResult = result
                    };
                    list_all.Add(volume_band_out);

                }
            }
            // 返回 list_all 作為 JSON
            return Json(list_all);
        }

    }
}
