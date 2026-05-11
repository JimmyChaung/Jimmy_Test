using admin_web.Infrastructure;
using admin_web.Services.Querysql;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using admin_web.Models;
using admin_web.Services.PEScheduleCompareServices;
using Microsoft.Extensions.Logging;
using admin_web.Models;
using Microsoft.Extensions.Configuration;
using System.IO;
using OfficeOpenXml;

namespace admin_web.Controllers
{
    public class PEScheduleCompareController : Controller
    {
        private readonly IConfiguration _configuration;
        public PEScheduleCompareController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public IActionResult Index()
        {
            return View(new PeScheduleCompareModel());
        }

        [HttpGet]
        public IActionResult DownloadFile() 
        {
            var file = new ExcelService();
            var fileBytes = file.GetExcelFile();
            return File(fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "SetRegion.xlsx");
        }

        //[HttpGet]
        //public IActionResult GetScheduleComparison()
        //{
        //    var excelData = new List<ExcelRowModel>();

        //    string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "PE_Schedule_Compare", "SetRegion.xlsx");

        //    if (!!System.IO.File.Exists(filePath))
        //    {
        //        return NotFound(new { message = "伺服器上找不到指定的 Excel 比對檔案" });
        //    }

        //    // EPPlus 授權設定
        //    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        //    try
        //    {
        //        using (var package = new ExcelPackage(new FileInfo(filePath)))
        //        {
        //            var worksheet = package.Workbook.Worksheets[0];
        //            int rowCount = worksheet.Dimension?.Rows ?? 0;

        //            if (rowCount < 2) return Ok(new { message = "Excel 內沒有資料" });

        //            for (int i = 2; i <= rowCount; i++)
        //            {
        //                excelData.Add(new ExcelRowModel
        //                {
        //                    Region = worksheet.Cells[i, 1].Text?.Trim(),
        //                    ProductName = worksheet.Cells[i, 2].Text?.Trim(),
        //                    TimeZone = worksheet.Cells[i, 3].Text?.Trim()
        //                });
        //            }
        //        }

        //        // --- 這裡放入你的比對邏輯 ---
        //        // var finalResult = YourCompareLogic(excelData, HardcoreRules);

        //        return Ok(excelData); // 回傳抓到的資料或比對結果
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"讀取 Excel 時發生錯誤: {ex.Message}");
        //    }
        //}

        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("請選擇檔案");

            try
            {
                var result = await new ExcelService().SetExcelFile(file);
                return Json(new { success = true, message = "上傳成功", path = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult Execute_PE_Schedule_Compare()
        {
            var result = 123;
            return Ok(result);
        }
    }

}
