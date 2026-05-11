using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace admin_web.Controllers
{
    public class CsvAuditController : Controller
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
                return Content("請上傳 A.csv 與 Before/After 或 .bak/.csv 檔案");

            var workDir = Path.Combine(Path.GetTempPath(), "CsvAudit_" + Guid.NewGuid().ToString("N"));
            var dir = Path.Combine(workDir, "csv");
            Directory.CreateDirectory(dir);

            string rulesPath = await SaveFile(rules, workDir);

            foreach (var f in csvFiles)
            {
                if (!Path.GetFileName(f.FileName).EndsWith(".csv", StringComparison.OrdinalIgnoreCase)|| !Path.GetFileName(f.FileName).EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                    return Content("CsvBatchUpdater 只能上傳 .csv或.bak 檔案：" + f.FileName);

                await SaveFile(f, dir);
            }

            string outPath = Path.Combine(dir, "report_check_visual.xlsx");
            string jarPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "ExternalTools",
    "CsvChangeAuditTool_v2.jar"
);// @"C:\Users\0384\Desktop\CsvChangeAuditTool_v2.jar";

            var args =
                $"-jar \"{jarPath}\" " +
                $"--rules \"{rulesPath}\" " +
                $"--dir \"{dir}\" " +
                $"--output \"{outPath}\" ";

            var result = await RunJava(args, workDir);

            if (result.exitCode != 0)
                return Content("執行失敗\n\nSTDOUT:\n" + result.stdout + "\n\nSTDERR:\n" + result.stderr);

            if (!System.IO.File.Exists(outPath))
                return Content("Java 執行完成，但沒有產生 report_check_visual.xlsx\n\nSTDOUT:\n" + result.stdout);

            return PhysicalFile(outPath,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "report_check_visual.xlsx");
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