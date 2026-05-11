using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using Microsoft.AspNetCore.Hosting;
using OfficeOpenXml;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.IO.Compression;

namespace admin_web.Controllers
{
    public class YamlController : Controller
    {
        private readonly string dynamicJar = Path.Combine(
    Directory.GetCurrentDirectory(),
    "ExternalTools",
    "DynamicLeverageYamlGenerator.jar"
);
        private readonly string jsonJar = Path.Combine(
    Directory.GetCurrentDirectory(),
    "ExternalTools",
    "GenerateCloseToOpenJson.jar"
); //@"C:\Users\0384\Desktop\GenerateCloseToOpenJson.jar";

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // 第一步：上傳檔案，先產生 close_to_open.json / symbol_groups_config.json
        [HttpPost]
        [RequestSizeLimit(200_000_000)]
        public async Task<IActionResult> PrepareJson(
            IFormFile excel,
            IFormFile mt4,
            IFormFile mt5,
            IFormFile excludedGroups,
            IFormFile? excludedProducts,
            string mode,
            bool enableExcluded = false
        )
        {
            if (excel == null || mt4 == null || mt5 == null || excludedGroups == null)
                return Content("缺少必要檔案");

            if (enableExcluded && excludedProducts == null)
                return Content("已啟用排除商品清單，但沒有上傳排除商品 Excel");

            var jobId = Guid.NewGuid().ToString("N");
            var workDir = Path.Combine(Path.GetTempPath(), "YamlGen_" + jobId);
            var inputDir = Path.Combine(workDir, "input");
            var jsonDir = Path.Combine(workDir, "json");
            var outputDir = Path.Combine(workDir, "output");

            Directory.CreateDirectory(inputDir);
            Directory.CreateDirectory(jsonDir);
            Directory.CreateDirectory(outputDir);

            string excelPath = await SaveFileAs(excel, inputDir, "main.xlsx");
            string mt4Path = await SaveFileAs(mt4, inputDir, "mt4.yaml");
            string mt5Path = await SaveFileAs(mt5, inputDir, "mt5.yaml");
            string excludedGroupsPath = await SaveFileAs(excludedGroups, inputDir, "excluded_groups.xlsx");

            if (enableExcluded && excludedProducts != null)
                await SaveFileAs(excludedProducts, inputDir, "excluded_products.xlsx");

            // 這兩個驗證方法用你前面加的 ValidateMainExcel / ValidateExcludedGroupsExcel
            var mainExcelError = ValidateMainExcel(excelPath);
            if (mainExcelError != null) return Content(mainExcelError);

            var excludedGroupsError = ValidateExcludedGroupsExcel(excludedGroupsPath);
            if (excludedGroupsError != null) return Content(excludedGroupsError);

            if (!System.IO.File.Exists(jsonJar))
                return Content("找不到 GenerateCloseToOpenJson.jar：" + jsonJar);

            var jsonArgs =
                $"-jar \"{jsonJar}\" " +
                $"--mt4 \"{mt4Path}\" " +
                $"--mt5 \"{mt5Path}\" " +
                $"--output \"{jsonDir}\" ";

            var jsonResult = await RunJava(jsonArgs, workDir);

            if (jsonResult.ExitCode != 0)
            {
                return Content("產生 JSON 失敗\n\nSTDOUT:\n" + jsonResult.Stdout + "\n\nSTDERR:\n" + jsonResult.Stderr);
            }

            string closeToOpenPath = Path.Combine(jsonDir, "close_to_open.json");
            string symbolGroupsPath = Path.Combine(jsonDir, "symbol_groups_config.json");

            if (!System.IO.File.Exists(closeToOpenPath) || !System.IO.File.Exists(symbolGroupsPath))
                return Content("JSON jar 執行完成，但沒有產生 close_to_open.json / symbol_groups_config.json");

            var model = new DynamicYamlEditModel
            {
                JobId = jobId,
                Mode = mode,
                EnableExcluded = enableExcluded,
                CloseToOpenJson = await System.IO.File.ReadAllTextAsync(closeToOpenPath),
                SymbolGroupsConfigJson = await System.IO.File.ReadAllTextAsync(symbolGroupsPath)
            };

            return View("EditJson", model);
        }

        // 第二步：使用者編輯 JSON 後，再產生 YAML
        [HttpPost]
        [RequestSizeLimit(200_000_000)]
        public async Task<IActionResult> GenerateFromEditedJson(
            string jobId,
            string mode,
            bool enableExcluded,
            string closeToOpenJson,
            string symbolGroupsConfigJson
        )
        {
            var workDir = Path.Combine(Path.GetTempPath(), "YamlGen_" + jobId);
            var inputDir = Path.Combine(workDir, "input");
            var jsonDir = Path.Combine(workDir, "json");
            var outputDir = Path.Combine(workDir, "output");

            string excelPath = Path.Combine(inputDir, "main.xlsx");
            string mt4Path = Path.Combine(inputDir, "mt4.yaml");
            string mt5Path = Path.Combine(inputDir, "mt5.yaml");
            string excludedGroupsPath = Path.Combine(inputDir, "excluded_groups.xlsx");
            string excludedProductsPath = Path.Combine(inputDir, "excluded_products.xlsx");

            Directory.CreateDirectory(jsonDir);
            Directory.CreateDirectory(outputDir);

            string closeToOpenPath = Path.Combine(jsonDir, "close_to_open.json");
            string symbolGroupsPath = Path.Combine(jsonDir, "symbol_groups_config.json");

            await System.IO.File.WriteAllTextAsync(closeToOpenPath, closeToOpenJson ?? "");
            await System.IO.File.WriteAllTextAsync(symbolGroupsPath, symbolGroupsConfigJson ?? "");

            if (!System.IO.File.Exists(dynamicJar))
                return Content("找不到 DynamicLeverageYamlGenerator.jar：" + dynamicJar);

            var args =
                $"-jar \"{dynamicJar}\" " +
                $"--input \"{excelPath}\" " +
                $"--mt4 \"{mt4Path}\" " +
                $"--mt5 \"{mt5Path}\" " +
                $"--c2o \"{closeToOpenPath}\" " +
                $"--symbol \"{symbolGroupsPath}\" " +
                $"--excludedGroups \"{excludedGroupsPath}\" " +
                $"--output \"{outputDir}\" " +
                $"--mode \"{mode}\" " +
                $"--mt4Lev \"200\" " +
                $"--mt5Lev \"200\" " +
                $"--enableExcluded \"{enableExcluded.ToString().ToLower()}\" ";

            if (enableExcluded)
                args += $"--excludedProducts \"{excludedProductsPath}\" ";

            var result = await RunJava(args, workDir);

            if (result.ExitCode != 0)
                return Content("產生 YAML 失敗\n\nSTDOUT:\n" + result.Stdout + "\n\nSTDERR:\n" + result.Stderr);

            var files = Directory.GetFiles(outputDir);
            if (files.Length == 0)
                return Content("Java 執行成功，但沒有產生 YAML。\n\nSTDOUT:\n" + result.Stdout);

            string zipPath = Path.Combine(workDir, "DynamicLeverageYamlResult.zip");

            if (System.IO.File.Exists(zipPath))
                System.IO.File.Delete(zipPath);

            ZipFile.CreateFromDirectory(outputDir, zipPath);

            return PhysicalFile(zipPath, "application/zip", "DynamicLeverageYamlResult.zip");
        }

        private static async Task<string> SaveFileAs(IFormFile file, string dir, string fileName)
        {
            string path = Path.Combine(dir, fileName);
            using (var stream = System.IO.File.Create(path))
                await file.CopyToAsync(stream);
            return path;
        }

        private static async Task<(int ExitCode, string Stdout, string Stderr)> RunJava(string args, string workDir)
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
            if (p == null)
                return (-1, "", "無法啟動 Java process");

            string stdout = await p.StandardOutput.ReadToEndAsync();
            string stderr = await p.StandardError.ReadToEndAsync();

            await p.WaitForExitAsync();

            return (p.ExitCode, stdout, stderr);
        }

        private static string? ValidateMainExcel(string path)
        {
            using var package = new ExcelPackage(new FileInfo(path));

            string[] validSheets = { "Bybit", "FX&Gold", "Stocks" };

            var foundSheets = validSheets
                .Where(x => package.Workbook.Worksheets[x] != null)
                .ToList();

            if (!foundSheets.Any())
                return "主 Excel 格式錯誤：至少要有 Bybit、FX&Gold、Stocks 其中一個分頁。";

            string[] requiredHeaders =
            {
        "Date",
        "Day",
        "Time",
        "News Event",
        "Currency",
        "Start Time",
        "End Time",
        "Affected Symbol Type"
    };

            foreach (var sheetName in foundSheets)
            {
                var ws = package.Workbook.Worksheets[sheetName];

                bool hasAnyData = false;
                for (int r = 2; r <= ws.Dimension.End.Row; r++)
                {
                    if (!string.IsNullOrWhiteSpace(ws.Cells[r, 1].Text))
                    {
                        hasAnyData = true;
                        break;
                    }
                }

                if (!hasAnyData)
                    continue;

                for (int c = 1; c <= requiredHeaders.Length; c++)
                {
                    string actual = NormalizeHeader(ws.Cells[1, c].Text);
                    string expected = NormalizeHeader(requiredHeaders[c - 1]);

                    // 🔥 特殊處理 Time 欄
                    if (expected == "TIME")
                    {
                        if (!actual.StartsWith("TIME"))
                        {
                            return $"主 Excel 格式錯誤：分頁 {sheetName} 第 {c} 欄需為 Time（可包含時區），目前是 [{ws.Cells[1, c].Text}]。";
                        }
                    }
                    else
                    {
                        if (actual != expected)
                        {
                            return $"主 Excel 格式錯誤：分頁 {sheetName} 第 {c} 欄應為 [{requiredHeaders[c - 1]}]，目前是 [{ws.Cells[1, c].Text}]。";
                        }
                    }
                }

                return null;
            }

            return "主 Excel 格式錯誤：Bybit、FX&Gold、Stocks 分頁都沒有有效資料。";
        }

        private static string? ValidateExcludedGroupsExcel(string path)
        {
            using var package = new ExcelPackage(new FileInfo(path));

            bool hasMt4Sheet = false;
            bool hasMt5Sheet = false;
            bool mt4HasGroupColumn = false;
            bool mt5HasGroupColumn = false;
            bool mt4HasValidGroup = false;
            bool mt5HasValidGroup = false;

            foreach (var ws in package.Workbook.Worksheets)
            {
                string sheetName = (ws.Name ?? "").ToUpperInvariant();

                bool isMt4 = sheetName.Contains("MT4");
                bool isMt5 = sheetName.Contains("MT5");

                if (!isMt4 && !isMt5)
                    continue;

                if (isMt4) hasMt4Sheet = true;
                if (isMt5) hasMt5Sheet = true;

                int groupCol = -1;
                int maxCol = ws.Dimension?.End.Column ?? 0;

                for (int c = 1; c <= maxCol; c++)
                {
                    string header = (ws.Cells[1, c].Text ?? "").Trim().ToUpperInvariant();

                    if ((header.Contains("GROUP") || header.Contains("組別"))
                        && !header.Contains("SERVER")
                        && !header.Contains("NOTE")
                        && !header.Contains("備註")
                        && !header.Contains("說明"))
                    {
                        groupCol = c;
                        break;
                    }
                }

                if (groupCol < 0)
                    continue;

                if (isMt4) mt4HasGroupColumn = true;
                if (isMt5) mt5HasGroupColumn = true;

                int maxRow = ws.Dimension?.End.Row ?? 0;

                for (int r = 2; r <= maxRow; r++)
                {
                    string val = (ws.Cells[r, groupCol].Text ?? "").Trim();

                    if (isMt4 && IsRealMt4Group(val))
                    {
                        mt4HasValidGroup = true;
                    }

                    if (isMt5 && IsRealMt5GroupPattern(val))
                    {
                        mt5HasValidGroup = true;
                    }
                }
            }

            if (!hasMt4Sheet)
                return "排除組別 Excel 格式錯誤：找不到名稱包含 MT4 的分頁。";

            if (!hasMt5Sheet)
                return "排除組別 Excel 格式錯誤：找不到名稱包含 MT5 的分頁。";

            if (!mt4HasGroupColumn)
                return "排除組別 Excel 格式錯誤：MT4 分頁找不到 Group / 組別 欄位。";

            if (!mt5HasGroupColumn)
                return "排除組別 Excel 格式錯誤：MT5 分頁找不到 Group / 組別 欄位。";

            if (!mt4HasValidGroup)
                return "排除組別 Excel 格式錯誤：MT4 分頁 Group 欄底下沒有有效組別。";

            if (!mt5HasValidGroup)
                return "排除組別 Excel 格式錯誤：MT5 分頁沒有有效 Group pattern，內容需包含 \\ 或 *。";

            return null;
        }

        private static string NormalizeHeader(string s)
        {
            return (s ?? "")
                .Replace("\uFEFF", "")
                .Replace('\u00A0', ' ')
                .Trim()
                .Replace("  ", " ")
                .ToUpperInvariant();
        }

        private static bool IsRealMt4Group(string s)
        {
            string v = (s ?? "").Trim();
            if (string.IsNullOrWhiteSpace(v))
                return false;

            string upper = v.ToUpperInvariant();

            if (upper.Contains("SERVER")
                || upper.Contains("備註")
                || upper.Contains("NOTE")
                || upper.Contains("說明")
                || upper.Contains("字段")
                || upper.Contains("只抓")
                || upper.Contains("排除")
                || upper.Contains("名單"))
            {
                return false;
            }

            // MT4：只要 Group 欄底下有值即可
            return true;
        }

        private static bool IsRealMt5GroupPattern(string s)
        {
            string v = (s ?? "").Trim();
            if (string.IsNullOrWhiteSpace(v))
                return false;

            string upper = v.ToUpperInvariant();

            if (upper.Contains("SERVER")
                || upper.Contains("備註")
                || upper.Contains("NOTE")
                || upper.Contains("說明")
                || upper.Contains("字段")
                || upper.Contains("只抓")
                || upper.Contains("排除")
                || upper.Contains("名單"))
            {
                return false;
            }

            // MT5：通常需要 VIG_Hedge\M_xxx 或萬用字元
            return v.Contains("\\") || v.Contains("*");
        }

        // ValidateMainExcel / ValidateExcludedGroupsExcel 放你前面那兩個驗證方法
    }

    public class DynamicYamlEditModel
    {
        public string JobId { get; set; } = "";
        public string Mode { get; set; } = "NEW";
        public bool EnableExcluded { get; set; }
        public string CloseToOpenJson { get; set; } = "";
        public string SymbolGroupsConfigJson { get; set; } = "";
    }
}