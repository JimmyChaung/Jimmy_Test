using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using OfficeOpenXml;
using System.Diagnostics;
using admin_web.Models;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Npgsql;
using Microsoft.AspNetCore.Http;
using System;
using OfficeOpenXml;

namespace admin_web.Services.PEScheduleCompareServices
{
    public class ExcelService
    {
        public byte[] GetExcelFile()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "PE_Schedule_Compare/SetRegion.xlsx");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"找不到檔案: {filePath}");
            }

            return File.ReadAllBytes(filePath);
        }

        public async Task<string> SetExcelFile(IFormFile file)
        {
            var allowedExtension = new[] { ".xlsx", ".xls" };
            var ext = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtension.Contains(ext))
            {
                throw new Exception("只允許上傳 Excel 檔案");
            }

            try
            {
                string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "PE_Schedule_Compare");
                string filePath = Path.Combine(uploadFolder, "SetRegion.xlsx");

                Directory.CreateDirectory(uploadFolder);

                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    await file.CopyToAsync(stream);
                }

                return filePath;
            } catch(Exception e)
            {
                throw new Exception("上傳失敗");
            }
            
        }
    }
}
