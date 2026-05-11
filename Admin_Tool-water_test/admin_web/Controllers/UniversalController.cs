using admin_web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace admin_web.Controllers
{
    public class UniversalController : Controller
    {
        // 上傳檔案
        // path: 上傳到該路徑
        [HttpPost]
        public IActionResult UploadFile(IFormFile file, string path, string file_name, bool del_all = false)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            // input 路徑
            var uploadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", path);

            // 檢查input資料夾是否存在
            if (!Directory.Exists(uploadDirectory))
            {
                Directory.CreateDirectory(uploadDirectory);
            }
            else
            {
                if (del_all)
                {
                    // 清除資料夾內所有檔案和子目錄
                    DirectoryInfo di = new DirectoryInfo(uploadDirectory);

                    foreach (FileInfo file_d in di.GetFiles())
                    {
                        file_d.Delete();
                    }

                    foreach (DirectoryInfo dir in di.GetDirectories())
                    {
                        dir.Delete(true);
                    }
                }
                else
                {
                    // 如果 del_all == false，只覆蓋同名檔案，不清空整個資料夾
                    var filePath = Path.Combine(uploadDirectory, file.FileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath); // 刪除同名檔案
                    }
                }
            }

            // 儲存上傳的檔案
            var newFilePath = Path.Combine(uploadDirectory, string.IsNullOrEmpty(file_name) ? file.FileName : file_name);
            using var stream = new FileStream(newFilePath, FileMode.Create);
            file.CopyTo(stream);

            return Ok($"{file.FileName} uploaded successfully.");
        }

        // 執行檔
        // tool_path: 工具的資料夾名稱; output_path: 執行檔輸出的路徑; exe_name: 執行檔檔名
        [HttpPost]
        public IActionResult RunExe(string tool_path, string output_path, string exe_name, bool DeleteAllFile = false)
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
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardOutput = true;
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

        // 打包執行檔輸出的檔案
        // output_path: 執行檔輸出的路徑; zipFileName: 壓縮檔的檔名
        [HttpGet]
        public IActionResult DownloadZip(string output_path, string zipFileName)
        {
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", output_path);
            try
            {
                string tempZipPath = Path.Combine(Path.GetTempPath(), zipFileName);
                ZipFile.CreateFromDirectory(folderPath, tempZipPath);
                byte[] fileBytes = System.IO.File.ReadAllBytes(tempZipPath);
                System.IO.File.Delete(tempZipPath);
                Response.Headers.Add("Content-Disposition", $"attachment; filename={zipFileName}");
                
                return File(fileBytes, "application/zip");
            }
            catch (Exception ex)
            {
                return Content($"Error: {ex.Message}");
            }
        }

        // 下載單一個檔案(不限檔案格式)
        [HttpGet]
        public IActionResult DownloadFile(string output_path, string FileName)
        {
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", output_path);
            string filePath = Path.Combine(folderPath, FileName);
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    var fileContent = System.IO.File.ReadAllBytes(filePath);
                    var contentType = "application/octet-stream"; 
                    return File(fileContent, contentType, FileName);
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

        // 紀錄點擊次數
        [HttpPost]
        public IActionResult Insert_Usage_Log(string Tool, string Remark)
        {
            string connectionString = UniversalService.sql_connectionString;

            var Time = DateTime.Now;

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    string query = @"
                        INSERT INTO `admin_tool_log`.`tool_usage_history` 
                        (`Tool`, `Remark`, `Time`) 
                        VALUES (@Tool, @Remark, @Time)";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Tool", Tool);
                        cmd.Parameters.AddWithValue("@Remark", Remark);
                        cmd.Parameters.AddWithValue("@Time", Time);

                        cmd.ExecuteNonQuery();
                    }
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 給前端看得Server
        [HttpGet]
        public IActionResult Get_All_Server_Config()
        {
            var log = UniversalService.Get_All_Server_Config();
            if (log != null)
            {
                return Ok(new { data = log });
            }
            else
            {
                return BadRequest();
            }
        }
    }
}
