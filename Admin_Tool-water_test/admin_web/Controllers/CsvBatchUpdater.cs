using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using Microsoft.AspNetCore.Http;
using admin_web.Infrastructure;

namespace admin_web.Controllers
{
    public class CsvBatchController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [RequestSizeLimit(200_000_000)]
        public async Task<IActionResult> Run(IFormFile rules, List<IFormFile> csvFiles)
        {
            if (rules == null || csvFiles == null || csvFiles.Count == 0)
                return Content("請上傳 A.csv 規則檔與 B CSV 檔案");

            var workDir = Path.Combine(Path.GetTempPath(), "CsvBatch_" + Guid.NewGuid().ToString("N"));
            var dir = Path.Combine(workDir, "csv");
            Directory.CreateDirectory(dir);

            string rulesPath = await SaveFile(rules, workDir);

            foreach (var f in csvFiles)
            {
                if (!Path.GetFileName(f.FileName).EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    return Content("CsvBatchUpdater 只能上傳 .csv 檔案：" + f.FileName);

                await SaveFile(f, dir);
            }

            string jarPath = JarDownloader.EnsureJar("CsvBatchUpdater_v2.jar");// @"C:\Users\0384\Desktop\CsvBatchUpdater_v2.jar";

            var args =
                $"-jar \"{jarPath}\" " +
                $"--rules \"{rulesPath}\" " +
                $"--dir \"{dir}\" ";

            var result = await RunJava(args, workDir);

            if (result.exitCode != 0)
                return Content("執行失敗\n\nSTDOUT:\n" + result.stdout + "\n\nSTDERR:\n" + result.stderr);
            // Java 已經跑完，這時候如果有修改，才會產生 .bak
            var bakFiles = Directory.GetFiles(dir, "*.bak", SearchOption.AllDirectories);

            if (bakFiles.Length == 0)
            {
                return Content("沒有任何資料被修改\n\nSTDOUT:\n" + result.stdout);
            }

            string zipPath = Path.Combine(workDir, "CsvBatchResult.zip");
            ZipFile.CreateFromDirectory(dir, zipPath);

            return PhysicalFile(zipPath, "application/zip", "CsvBatchResult.zip");

        }

        private static async Task<string> SaveFile(IFormFile file, string dir)
        {
            string path = Path.Combine(dir, Path.GetFileName(file.FileName));
            using (var s = System.IO.File.Create(path))
                await file.CopyToAsync(s);
            return path;
        }

        private static async Task<(int exitCode, string stdout, string stderr)> RunJava(string args, string workDir)
        {
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

            var p = Process.Start(psi);
            string stdout = await p.StandardOutput.ReadToEndAsync();
            string stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();

            return (p.ExitCode, stdout, stderr);
        }
    }
}