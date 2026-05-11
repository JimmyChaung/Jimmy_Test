using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace admin_web.Controllers
{
    public class CloseToOpenController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [RequestSizeLimit(200_000_000)]
        public async Task<IActionResult> Generate(IFormFile mt4, IFormFile mt5)
        {
            if (mt4 == null || mt5 == null)
                return Content("請上傳 MT4 / MT5 YAML");

            var workDir = Path.Combine(Path.GetTempPath(), "CloseToOpen_" + Guid.NewGuid().ToString("N"));
            var inputDir = Path.Combine(workDir, "input");
            var outputDir = Path.Combine(workDir, "output");

            Directory.CreateDirectory(inputDir);
            Directory.CreateDirectory(outputDir);

            string mt4Path = await SaveFile(mt4, inputDir);
            string mt5Path = await SaveFile(mt5, inputDir);

            string jarPath = JarDownloader.EnsureJar("GenerateCloseToOpenJson.jar");

            if (!System.IO.File.Exists(jarPath))
                return Content("找不到 jar：" + jarPath);

            var args =
                $"-jar \"{jarPath}\" " +
                $"--mt4 \"{mt4Path}\" " +
                $"--mt5 \"{mt5Path}\" " +
                $"--output \"{outputDir}\" ";

            var psi = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workDir
            };

            var process = Process.Start(psi);
            if (process == null)
                return Content("無法啟動 Java process");

            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return Content("產生失敗\n\nSTDOUT:\n" + stdout + "\n\nSTDERR:\n" + stderr);
            }

            var files = Directory.GetFiles(outputDir);
            if (files.Length == 0)
            {
                return Content("Java 執行成功，但沒有產生檔案。\n\nSTDOUT:\n" + stdout);
            }

            string zipPath = Path.Combine(workDir, "CloseToOpenJsonResult.zip");

            if (System.IO.File.Exists(zipPath))
                System.IO.File.Delete(zipPath);

            ZipFile.CreateFromDirectory(outputDir, zipPath);

            return PhysicalFile(zipPath, "application/zip", "CloseToOpenJsonResult.zip");
        }

        private static async Task<string> SaveFile(IFormFile file, string dir)
        {
            string safeFileName = Path.GetFileName(file.FileName);
            string path = Path.Combine(dir, safeFileName);

            using (var stream = System.IO.File.Create(path))
            {
                await file.CopyToAsync(stream);
            }

            return path;
        }
    }
}