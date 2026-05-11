using admin_web.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using admin_web.Services.MondayTroubleShootServices;
using static admin_web.Models.MondayTroubleShoot.Compare_Manager_Model;
using admin_web.Services;
using System.Threading.Tasks;

namespace admin_web.Controllers
{
    public class MondayTroubleshootController : Controller
    {
        // ------------------------------------------------------ 基本準則比對工具 ------------------------------------------------------
        // MT4 ------------------------------------------------------------------------
        // 介面
        public IActionResult Compare_OBP_MT4(string Path, string Date)
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("Compare_OBP_MT4");
            List<string> fileNameList = Comapare_OBP_MT4Service.GetFileNameList();
            ViewBag.FileNameList = fileNameList;
            ExcelViewModel viewModel = new();
            if (fileNameList.Count == 0)
            {
                return View("Compare_OBP/MT4");
            }
            else
            {
                if (Date != "" && Date != null)
                {
                    viewModel = Comapare_OBP_MT4Service.View_Data(Date);
                    ViewBag.SelectDate = Date;
                }
                else if (Path == null || Path == "")
                {
                    string fileName = fileNameList.Last();
                    viewModel = Comapare_OBP_MT4Service.View_Data(fileName);
                    ViewBag.SelectDate = fileName;
                }
                return View("Compare_OBP/MT4", viewModel);
            }
        }

        // MT5 ------------------------------------------------------------------------
        // 介面
        public IActionResult Compare_OBP_MT5(string Path, string Date)
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("Compare_OBP_MT5");
            List<string> fileNameList = Comapare_OBP_MT5Service.GetFileNameList();
            ViewBag.FileNameList = fileNameList;
            ExcelViewModel viewModel = new();

            if (fileNameList.Count == 0)
            {
                return View("Compare_OBP/MT5");
            }
            else
            {
                if (Date != "" && Date != null)
                {
                    viewModel = Comapare_OBP_MT5Service.View_Data(Date);
                    ViewBag.SelectDate = Date;
                }
                else if (Path == null || Path == "")
                {
                    string fileName = fileNameList.Last();
                    viewModel = Comapare_OBP_MT5Service.View_Data(fileName);
                    ViewBag.SelectDate = fileName;
                }
                return View("Compare_OBP/MT5", viewModel);
            }
        }

        // ------------------------------------------------------ 剩餘組別排查工具------------------------------------------------------

        // 介面
        public IActionResult RemainGroup()
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("RemainGroup");
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Check Remain Group Tool", "CheckList");
            List<string> fileNameList = RemainGroupService.GetFileNames();
            if (fileNameList.Count != 0 && fileNameList != null)
            {
                ViewBag.FileNameList = fileNameList
                    .Select(file => file.Replace("Check", "").Replace(".xlsx", ""))
                    .Select(file => DateTime.Parse(file)) // 轉換為DateTime
                    .OrderBy(date => date) // 根據日期排序
                    .Select(date => date.ToString("yyyy-MM-dd")) // 轉換回字串
                    .ToList();
                string fileName = fileNameList.Last();
                ViewBag.FileName = fileName;
                var viewModel = RemainGroupService.View_Data(fileName);
                return View("RemainGroup/main", viewModel);
            }
            else
            {
                ViewBag.FileNameList = null;
                return View("RemainGroup/main");
            }
        }

        // ------------------------------------------------------ 槓桿檢查(20250724停用)------------------------------------------------------

        //槓桿檢查前的刷新資料庫config

        //[HttpGet]
        //public async Task<IActionResult> LoginAndPostData()
        //{
        //    string result = await RriskConfigUpdataService.LoginAndPostData();

        //    if (result == "更新失敗")
        //    {
        //        return BadRequest("更新失敗");
        //    }

        //    return Ok(result); // 成功回傳
        //}

        // FCA槓桿檢查工具介面
        //public IActionResult Leverage_Check_FCA(string Path, string Date)
        //{
        //    ViewData["Title"] = UniversalService.Get_Tool_Name("Leverage_Check_FCA");
        //    List<string> fileNameList = FCAService.GetFileNameList();
        //    ViewBag.FileNameList = fileNameList;
        //    ExcelViewModel viewModel = new();
        //    if (fileNameList.Count == 0)
        //    {
        //        return View("Leverage_Check/Leverage_Check_FCA");
        //    }
        //    else
        //    {
        //        if (Date != "" && Date != null)
        //        {
        //            viewModel = FCAService.View_Data(Date);
        //            ViewBag.SelectDate = Date;
        //        }
        //        else if (Path == null || Path == "")
        //        {
        //            string fileName = fileNameList.Last();
        //            viewModel = FCAService.View_Data(fileName);
        //            ViewBag.SelectDate = fileName;
        //        }
        //        return View("Leverage_Check/Leverage_Check_FCA", viewModel);
        //    }
        //}

        //// ASIC槓桿檢查工具介面
        //public IActionResult Leverage_Check_ASIC(string Date)
        //{
        //    ViewData["Title"] = UniversalService.Get_Tool_Name("Leverage_Check_ASIC");
        //    List<string> fileNameList = ASICService.GetFileNameList();
        //    ViewBag.FileNameList = fileNameList;
        //    ExcelViewModel viewModel = new();
        //    if (fileNameList.Count == 0)
        //    {
        //        return View("Leverage_Check/Leverage_Check_ASIC");
        //    }
        //    else
        //    {
        //        if (Date != "" && Date != null)
        //        {
        //            viewModel = ASICService.View_Data(Date);
        //            ViewBag.SelectDate = Date;
        //        }
        //        else
        //        {
        //            string fileName = fileNameList.Last();
        //            viewModel = ASICService.View_Data(fileName);
        //            ViewBag.SelectDate = fileName;
        //        }
        //        return View("Leverage_Check/Leverage_Check_ASIC", viewModel);
        //    }
        //}

        // ------------------------------------------------------ Manager 周一比對 ------------------------------------------------------
        // MT4
        public IActionResult Compare_Manager_MT4(string date)
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("Compare_Manager_MT4");
            List<string> fileNameList = Compare_Manager_MT4Service.GetFileNameList();
            ViewBag.FileNameList = fileNameList;
            ExcelViewModel viewModel = new();
            if (fileNameList.Count != 0)
            {
                if (date == "" || date == null)
                {
                    string FileName = fileNameList.Last();
                    viewModel = Compare_Manager_MT4Service.View_Data(FileName);
                    ViewBag.SelectDate = FileName;
                }
                else
                {
                    viewModel = Compare_Manager_MT4Service.View_Data(date);
                    ViewBag.SelectDate = date;
                }
            }
            return View("Compare_Manager/MT4", viewModel);
        }

        [HttpGet]
        public IActionResult Update_Permission()
        {
            int result = Compare_Manager_MT4Service.search_manager_token();

            if (result == 1)
            {
                return BadRequest(1);
            }

            return Ok(result); // 成功回傳
        }

        [HttpGet]
        public IActionResult Update_csv(string data_time)
        {
            int result = Compare_Manager_MT4Service.search_manager_token();

            if (result == 1)
            {
                return BadRequest(1);
            }

            return Ok(result); // 成功回傳
        }



        // MT5
        public IActionResult Compare_Manager_MT5(string date)
        {
            ViewData["Title"] = UniversalService.Get_Tool_Name("Compare_Manager_MT5");
            List<string> fileNameList = Compare_Manager_MT5Service.GetFileNameList();
            ViewBag.FileNameList = fileNameList;
            ViewBag.LastManager = Compare_Manager_MT5Service.GetLastManaager();
            MT5_ViewModel viewModel = new();
            if (fileNameList.Count != 0)
            {
                if (date == "" || date == null)
                {
                    string FileName = fileNameList.Last();
                    viewModel = Compare_Manager_MT5Service.View_Data(FileName);
                    ViewBag.SelectDate = FileName;
                }
                else
                {
                    viewModel = Compare_Manager_MT5Service.View_Data(date);
                    ViewBag.SelectDate = date;
                }
            }
            return View($"Compare_Manager/MT5", viewModel);
        }
    }
}
