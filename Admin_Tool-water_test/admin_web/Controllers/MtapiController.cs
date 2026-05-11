using System.Linq;
using System;
using admin_web.Services.MtapiServices;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;
using System.Data;
using Microsoft.AspNetCore.Http;
using admin_web.Services;
using System.Diagnostics;

namespace admin_web.Controllers
{
    public class MtapiController : Controller
    {
        // ------------------------------------------------------ 修改組別工具------------------------------------------------------

        // 介面
        public IActionResult EditGroup()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("EditGroup");
            return View("EditGroup/main");
        }

        [HttpPost]
        public IActionResult EditGroup_Program()
        {
            var file = Request.Form.Files[0];
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if ((fileExtension == ".xlsm" || fileExtension == ".xlsx") && file != null && file.Length > 0)
            {
                var log = EditGroupService.MainProgram(file);
                return Ok(new { data = log });
            }
            else
            {
                return BadRequest("檔案異常，請上傳xlsx檔");
            }
        }

        // ------------------------------------------------------ 批量創帳號 ------------------------------------------------------
        public IActionResult CreateAccount()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("CreateAccount");
            return View("CreateAccount/main");
        }

        [HttpPost]
        public IActionResult CreateAccount_Program()
        {
            var file = Request.Form.Files.FirstOrDefault();

            if (file == null || file.Length == 0)
            {
                return BadRequest("檔案異常，請上傳 xlsx 檔案");
            }

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (fileExtension != ".xlsx")
            {
                return BadRequest("只支援 xlsx 檔案格式");
            }

            try
            {
                var log = CreateAccountService.MainProgram(file);
                if (log.Where(r => r.Result == "創建成功").ToArray().Length < 1)
                {
                    return Ok(new { data = log, downloadUrl = "" });
                }
                HttpContext.Session.Set("CreateAccount_Log2Excel", System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(log));

                return Ok(new { data = log, downloadUrl = "/Mtapi/CreateAccount_Log2Excel" });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return BadRequest(new { error = "執行工具時發生錯誤", details = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult CreateAccount_Log2Excel()
        {
            if (!HttpContext.Session.TryGetValue("CreateAccount_Log2Excel", out byte[] logBytes))
            {
                return BadRequest("無法找到歷史記錄");
            }

            var (stream, fileName) = CreateAccountService.Log2Excel(logBytes);

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpGet]
        public IActionResult CreateAccount_ExportHistoryLog(string startDate, string endDate)
        {
            if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            {
                return BadRequest("請選擇有效的日期範圍");
            }

            var (stream, fileName) = CreateAccountService.ExportHistoryLog(startDate, endDate);
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ------------------------------------------------------ 關單工具 ------------------------------------------------------
        public IActionResult CloseOrder()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("CloseOrder");
            return View("Close_Order/main");
        }

        [HttpPost]
        public IActionResult CloseOrder_Program()
        {
            var file = Request.Form.Files[0];
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (fileExtension == ".xlsx" && file != null && file.Length > 0)
            {
                var log = CloseOrderService.MainProgram(file);
                return Ok(new { data = log });
            }
            else
            {
                return BadRequest("檔案異常，請上傳xlsx檔");
            }
        }

        [HttpGet]
        public IActionResult CloseOrder_ExportHistoryLog(string startDate, string endDate)
        {
            if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            {
                return BadRequest("請選擇有效的日期範圍");
            }

            var (stream, fileName) = CloseOrderService.ExportHistoryLog(startDate, endDate);
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        // ------------------------------------------------------ 創建展示帳號 ------------------------------------------------------
        //public IActionResult CreateDisplayAccount()
        //{
        //    ViewData["Title"] = UniversalService.Get_Tool_Name("CreateDisplayAccount");
        //    ViewBag.Capital_List = DisplayAccountCreateService.GetCapitalPool();
        //    // ViewBag.NextLogin_List = DisplayAccountCreateSettingService.Get_NextLogin_Config();
        //    return View("CreateDisplayAccount/main");
        //}

        //[HttpGet]
        //public IActionResult CreateDisplayAccount_RefreshCapital()
        //{
        //    DisplayAccountCreateService.Insert_Mysql_Capital_pool();
        //    return Ok(DisplayAccountCreateService.GetCapitalPool());
        //}

        //[HttpPost]
        //public IActionResult CreateDisplayAccount_Program()
        //{
        //    var mode = Request.Form["mode"].ToString();
        //    var file = Request.Form.Files[0];
        //    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

        //    if (fileExtension == ".xlsx" && file != null && file.Length > 0)
        //    {
        //        var log_view = DisplayAccountCreateService.MainProgram(mode, file);
        //        return Ok(log_view);
        //    }
        //    else
        //    {
        //        return BadRequest("檔案異常，請上傳xlsx檔");
        //    }
        //}

        //[HttpGet]
        //public IActionResult CreateDisplayAccount_ExportHistoryLog()
        //{
        //    var (stream, fileName) = DisplayAccountCreateService.ExportHistoryLog();
        //    return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        //}

        //[HttpPost]
        //public IActionResult CreateDisplayAccount_UpdateConfig([FromBody] List<NextLogin_Record> records)
        //{
        //    var result = DisplayAccountCreateSettingService.Update_Config(records);
        //    if (result == "SUCCESS")
        //    {
        //        return Ok();
        //    }
        //    else
        //    {
        //        return BadRequest(result);
        //    }
        //}
        
        // 測試環境
        //public IActionResult CreateDisplayAccountTest()
        //{
        //    ViewBag.Capital_List = DisplayAccountCreateTestService.GetCapitalPool();
        //    //ViewBag.NextLogin_List = DisplayAccountCreateSettingService.Get_NextLogin_Config();
        //    return View("CreateDisplayAccount/test_env");
        //}

        //[HttpPost]
        //public IActionResult CreateDisplayAccountTest_Program()
        //{
        //    var mode = Request.Form["mode"].ToString();
        //    var file = Request.Form.Files[0];
        //    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

        //    if (fileExtension == ".xlsx" && file != null && file.Length > 0)
        //    {
        //        var log_view = DisplayAccountCreateTestService.MainProgram(mode, file);
        //        return Ok(log_view);
        //    }
        //    else
        //    {
        //        return BadRequest("檔案異常，請上傳xlsx檔");
        //    }
        //}

        //[HttpGet]
        //public IActionResult CreateDisplayAccountTest_RefreshCapital()
        //{
        //    DisplayAccountCreateTestService.Test_insert_Mysql_Capital_pool();
        //    return Ok(DisplayAccountCreateTestService.GetCapitalPool());
        //}

        //[HttpGet]
        //public IActionResult CreateDisplayAccountTest_ExportHistoryLog()
        //{
        //    var (stream, fileName) = DisplayAccountCreateTestService.ExportHistoryLog();
        //    return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        //}

        // ------------------------------------------------------ 補K線新版 ------------------------------------------------------
        public IActionResult KBar_MT4()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("KBar_MT4");
            ViewBag.ServerConfig = KBar_MT4Service.Get_MT4_Config();
            return View("KBar/KBar_MT4");
        }

        [HttpPost]
        public IActionResult KBar_MT4_Add(IFormCollection form)
        {
            string server = form["server"];
            string login = form["login"];
            string password = form["password"];
            IFormFileCollection files = form.Files;

            try
            {
                return Ok(KBar_MT4Service.Add_KBar(server, login, password, files));
            }
            catch (Exception)
            {
                return BadRequest("新增K線時發生錯誤");
            }

        }

        [HttpPost]
        public IActionResult KBar_MT4_Edit1()
        {
            var file = Request.Form.Files[0];
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (fileExtension == ".xlsx")
            {
                try
                {
                    return Ok(KBar_MT4Service.Edit_KBar_1(file));
                }
                catch (Exception)
                {
                    return BadRequest("製作檔案時發生錯誤");
                }
            }
            else
            {
                return BadRequest("檔案類型錯誤，請上傳xlsx檔");
            }
        }

        [HttpPost]
        public IActionResult KBar_MT4_Edit2()
        {
            var file = Request.Form.Files[0];
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (fileExtension == ".xlsx")
            {
                try
                {
                    return Ok(KBar_MT4Service.Edit_KBar_2(file));
                }
                catch (Exception)
                {
                    return BadRequest("補K線時發生錯誤");
                }
            }
            else
            {
                return BadRequest("檔案類型錯誤，請上傳xlsx檔");
            }
        }

        [HttpPost]
        public IActionResult KBar_MT4_Del()
        {
            var file = Request.Form.Files[0];
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (fileExtension == ".xlsx")
            {
                try
                {
                    return Ok(KBar_MT4Service.Del_KBar(file));
                }
                catch (Exception)
                {
                    return BadRequest("刪除K線時發生錯誤");
                }
            }
            else
            {
                return BadRequest("檔案類型錯誤，請上傳xlsx檔");
            }
        }

        public IActionResult KBar_MT5()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("KBar_MT5");
            ViewBag.ServerConfig = KBar_MT5Service.Get_MT5_Config();
            return View("KBar/KBar_MT5");
        }

        [HttpPost]
        public IActionResult KBar_MT5_Add(IFormCollection form)
        {
            string server = form["server"];
            string login = form["login"];
            string password = form["password"];

            IFormFileCollection files = form.Files;

            try
            {
                return Ok(KBar_MT5Service.Add_KBar(server, login, password, files));
            }
            catch (Exception)
            {
                return BadRequest("新增K線時發生錯誤");
            }

        }

        [HttpPost]
        public IActionResult KBar_MT5_Edit1()
        {
            var file = Request.Form.Files[0];
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (fileExtension == ".xlsx")
            {
                try
                {
                    return Ok(KBar_MT5Service.Edit_KBar_1(file));
                }
                catch (Exception)
                {
                    return BadRequest("製作檔案時發生錯誤");
                }
            }
            else
            {
                return BadRequest("檔案類型錯誤，請上傳xlsx檔");
            }
        }

        [HttpPost]
        public IActionResult KBar_MT5_Edit2()
        {
            var file = Request.Form.Files[0];
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (fileExtension == ".xlsx")
            {
                try
                {
                    return Ok(KBar_MT5Service.Edit_KBar_2(file));
                }
                catch
                {
                    return BadRequest("補K線時發生錯誤");
                }
            }
            else
            {
                return BadRequest("檔案類型錯誤，請上傳xlsx檔");
            }
        }

        [HttpPost]
        public IActionResult KBar_MT5_Del()
        {
            var file = Request.Form.Files[0];
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (fileExtension == ".xlsx")
            {
                try
                {
                    return Ok(KBar_MT5Service.Del_KBar(file));
                }
                catch (Exception)
                {
                    return BadRequest("刪除K線時發生錯誤");
                }
            }
            else
            {
                return BadRequest("檔案類型錯誤，請上傳xlsx檔");
            }
        }


        [HttpGet]
        public IActionResult KBar_Download(string output_path, string FileName)
        {
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", output_path);
            string filePath = Path.Combine(folderPath, FileName);
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    var fileContent = System.IO.File.ReadAllBytes(filePath);
                    var contentType = "application/octet-stream";
                    var fileResult = File(fileContent, contentType, "symbols.xlsx");
                    System.IO.File.Delete(filePath);

                    return fileResult;
                }
                else
                {
                    return NotFound($"檔案 '{FileName}' 不存在於 '{folderPath}' 中。");
                }
            }
            catch (Exception ex)
            {
                return Content($"Error: {ex.Message}");
            }
        }

        // ------------------------------------------------------ 修改帳號工具------------------------------------------------------

        // 介面
        public IActionResult EditAccount()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("EditAccount");
            return View("EditAccount/main");
        }

        [HttpPost]
        public IActionResult EditAccount_Program()
        {
            var file = Request.Form.Files[0];
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if ((fileExtension == ".xlsm" || fileExtension == ".xlsx") && file != null && file.Length > 0)
            {
                var log = EditAccountService.MainProgram(file);
                return Ok(new { data = log });
            }
            else
            {
                return BadRequest("檔案異常，請上傳xlsx檔");
            }
        }

        [HttpGet]
        public IActionResult EditAccount_ExportHistoryLog(string startDate, string endDate)
        {
            if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            {
                return BadRequest("請選擇有效的日期範圍");
            }

            var (stream, fileName) = EditAccountService.ExportHistoryLog(startDate, endDate);
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ------------------------------------------------------ 修改商品工具------------------------------------------------------

        public IActionResult EditSymbol()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("EditSymbol");
            return View("EditSymbol/main");
        }

        [HttpPost]
        public IActionResult EditSymbol_Program()
        {
            var file = Request.Form.Files[0];
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if ((fileExtension == ".xlsx") && file != null && file.Length > 0)
            {
                var log = EditSymbolService.MainProgram(file);
                return Ok(new { data = log });
            }
            else
            {
                return BadRequest("檔案異常，請上傳xlsx檔");
            }
        }

        [HttpGet]
        public IActionResult EditSymbol_ExportHistoryLog(string startDate, string endDate)
        {
            if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            {
                return BadRequest("請選擇有效的日期範圍");
            }

            var (stream, fileName) = EditSymbolService.ExportHistoryLog(startDate, endDate);
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ------------------------------------------------------ PEDelay ------------------------------------------------------

        // PEDelayMT4介面
        public IActionResult PEDelayMT4()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("PEDelayMT4");
            string currentDirectory = Directory.GetCurrentDirectory();
            ViewBag.test = currentDirectory;

            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "PEDelayMT4", "output_file");
            List<string> fileNameList = PEDelayService.GetFileNames(folderPath);

            ViewBag.FileNameList = fileNameList
                .Select(file => file.Replace("log_DELAY_", "").Replace("_summary.xlsx", ""))
                .Select(file => DateTime.Parse(file)) // 轉換為DateTime
                .OrderBy(date => date) // 根據日期排序
                .Select(date => date.ToString("yyyy-MM-dd")) // 轉換回字串
                .ToList();

            string fileName = fileNameList.Last(); // 取出最新的資料來呈現在介面上
            ViewBag.FileName = fileName;
            string filePath = Path.Combine(folderPath, fileName);
            var viewModel = PEDelayService.View_Data(filePath);

            //string QAfilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ASIC Tool", "QA.txt");
            //ViewBag.FileContent = PEDelayService.ReadTxtFile(QAfilePath);

            return View("PEDelay/PEDelayMT4", viewModel);
        }

        // PEDelayMT5介面
        public IActionResult Refresh_PEDelayMT4_Data(string fileName)
        {
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "PEDelayMT4", "output_file");
            string filePath = Path.Combine(folderPath, fileName);
            var viewModel = PEDelayService.View_Data(filePath);
            return Json(new { viewModel.Tabs });
        }

        public IActionResult PEDelayMT5()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("PEDelayMT5");
            string currentDirectory = Directory.GetCurrentDirectory();
            ViewBag.test = currentDirectory;

            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "PEDelayMT5", "output_file");
            List<string> fileNameList = PEDelayService.GetFileNames(folderPath);

            ViewBag.FileNameList = fileNameList
                .Select(file => file.Replace("log_DELAY_", "").Replace("_summary.xlsx", ""))
                .Select(file => DateTime.Parse(file)) // 轉換為DateTime
                .OrderBy(date => date) // 根據日期排序
                .Select(date => date.ToString("yyyy-MM-dd")) // 轉換回字串
                .ToList();

            string fileName = fileNameList.Last(); // 取出最新的資料來呈現在介面上
            ViewBag.FileName = fileName;
            string filePath = Path.Combine(folderPath, fileName);
            var viewModel = PEDelayService.View_Data(filePath);

            return View("PEDelay/PEDelayMT5", viewModel);
        }
        public IActionResult Refresh_PEDelayMT5_Data(string fileName)
        {
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "PEDelayMT5", "output_file");
            string filePath = Path.Combine(folderPath, fileName);
            var viewModel = PEDelayService.View_Data(filePath);
            return Json(new { viewModel.Tabs });
        }
    }
}
