using admin_web.Models;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static admin_web.Models.FileModel;

namespace admin_web.Services.DataProductService
{
    public class PE_ReportService
    {
        private static readonly string OutputPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "PE Report Tool", "Output");
        
        public static List<FileEntry> GetOutputDirEntries()
        {
            return GetFileSystemEntriesList(Path.Combine(OutputPath));
        }

        // 取得資料夾內的檔案，做分類
        public static List<FileEntry> GetFileSystemEntriesList(string targetPath)
        {
            List<FileEntry> entries = new();

            string FullTargetPath = Path.Combine(OutputPath, targetPath.Replace("Output","").TrimStart('/'));

            if (Directory.Exists(FullTargetPath))
            {
                var directories = Directory.GetDirectories(FullTargetPath);
                foreach (var dir in directories)
                {
                    entries.Add(new FileEntry
                    {
                        Name = Path.GetFileName(dir),
                        Path = dir.Replace(OutputPath, "Output").Replace("\\", "/"),
                        Type = "dir"
                    });
                }

                var files = Directory.GetFiles(FullTargetPath);
                foreach (var file in files)
                {
                    entries.Add(new FileEntry
                    {
                        Name = Path.GetFileName(file),
                        Path = file.Replace(OutputPath, "").Replace("\\", "/"),
                        Type = Path.GetExtension(file).Equals(".xlsx", System.StringComparison.OrdinalIgnoreCase) ? "excel" : "other"
                    });
                }
            }
            return entries;
        }

        // 讀取Excel
        public static ExcelViewModel View_Data(string FilePath)
        {
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            string filePath = Path.Combine(OutputPath, FilePath);
            using var package = new ExcelPackage(new FileInfo(filePath));

            var viewModel = new ExcelViewModel
            {
                Tabs = new List<ExcelViewModel.Tab>()
            };

            List<string> SheetName = package.Workbook.Worksheets.Select(ws => ws.Name).ToList();
            var dateTimeColumns = new List<int>();

            foreach (var item in SheetName)
            {
                var tab = new ExcelViewModel.Tab();
                var allData = package.Workbook.Worksheets[item];
                tab.Name = item;
                if (allData != null)
                {
                    tab.Check = true;
                    if (allData.Dimension != null)
                    {
                        // 記錄 Header 中的日期欄位位置
                        for (int col = 1; col <= allData.Dimension.End.Column; col++)
                        {
                            string headerValue = allData.Cells[1, col].Text;
                            tab.HeaderList.Add(headerValue);

                            if (headerValue.Contains("OPEN_TIME", StringComparison.OrdinalIgnoreCase) ||
                                headerValue.Contains("CLOSE_TIME", StringComparison.OrdinalIgnoreCase) ||
                                headerValue.Contains("MODIFY_TIME", StringComparison.OrdinalIgnoreCase) ||
                                headerValue.Contains("EXPIRATION", StringComparison.OrdinalIgnoreCase))
                            {
                                dateTimeColumns.Add(col); // 記錄日期欄位
                            }
                        }

                        for (int row = 2; row <= allData.Dimension.End.Row; row++)
                        {
                            List<string> data = new();
                            for (int col = 1; col <= allData.Dimension.End.Column; col++)
                            {
                                var cellValue = allData.Cells[row, col].Value;
                                string cellText = cellValue?.ToString() ?? "";

                                // 檢查是否為日期欄位並進行轉換
                                if (dateTimeColumns.Contains(col))
                                {
                                    if (cellValue != null && double.TryParse(cellText, out double oaDate))
                                    {
                                        // 使用 DateTime.FromOADate 轉換為 DateTime 對象
                                        DateTime dateTimeValue = DateTime.FromOADate(oaDate);

                                        // 將 DateTime 轉換為所需的格式
                                        string formattedDateTime = dateTimeValue.ToString("yyyy/MM/dd hh:mm:ss tt");

                                        // 輸出或使用 formattedDateTime
                                        data.Add(formattedDateTime);
                                    }
                                    else
                                    {
                                        data.Add("Invalid Date");
                                    }
                                }
                                else
                                {
                                    data.Add(cellText);
                                }
                            }
                            tab.DataList.Add(data);
                        }
                    }
                }
                viewModel.Tabs.Add(tab);
            }
            return viewModel;
        }

    }
}
