using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static admin_web.Models.MondayTroubleShoot.Compare_Manager_Model;

namespace admin_web.Services.MondayTroubleShootServices
{
    public class Compare_Manager_MT5Service
    {
        private static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Compare_Manager_MT5");
        private static readonly string ManagerPath = Path.Combine(ToolPath, "Manager History");
        private static readonly string OutputPath = Path.Combine(ToolPath, "Output");

        // 取得最新檔案日期
        public static string GetLastManaager()
        {
            List<string> fileNameList = new();
            string[] files = Directory.GetFiles(ManagerPath, "*.xlsx");
            foreach (var item in files.Select(Path.GetFileName).ToArray())
            {
                fileNameList.Add(item);
            }

            if (fileNameList.Count == 0)
            {
                return null;
            }

            fileNameList = fileNameList
                .Select(file => file.Replace(".xlsx", ""))
                .OrderBy(fileName =>
                {
                    return DateTime.Parse(fileName);
                })
                .ToList();

            return fileNameList.Last();
        }

        // 取得檔案清單
        public static List<string> GetFileNameList()
        {
            List<string> fileNameList = GetFileNames();
            fileNameList = fileNameList
                .Select(file => file.Replace("MT5_Manager_", "").Replace(".xlsx", ""))
                .OrderBy(fileName =>
                {
                    string datePart = fileName.Split('_')[1];
                    return DateTime.Parse(datePart);
                })
                .ToList();
            return fileNameList;
        }

        public static List<string> GetFileNames()
        {
            string[] files = Directory.GetFiles(OutputPath, "MT5_Manager_*.xlsx");
            List<string> fileNameList = new();
            foreach (var item in files.Select(Path.GetFileName).ToArray())
            {
                fileNameList.Add(item);
            }
            return fileNameList;
        }

        public static MT5_ViewModel View_Data(string FileName)
        {
            string FullFilePath = Path.Combine(OutputPath, $"MT5_Manager_{FileName}.xlsx");
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            using var package = new ExcelPackage(new FileInfo(FullFilePath));

            var viewModel = new MT5_ViewModel
            {
                Tabs = new List<MT5_ViewModel.Tab>()
            };

            List<string> SheetNames = package.Workbook.Worksheets.Select(ws => ws.Name).ToList();

            foreach (var sheetName in SheetNames)
            {
                var tab = new MT5_ViewModel.Tab();
                var worksheet = package.Workbook.Worksheets[sheetName];

                if (sheetName.Contains("其他"))
                {
                    continue;
                }

                tab.Name = sheetName;

                if (worksheet != null && worksheet.Dimension != null)
                {
                    Dictionary<string, int> headerDict = new Dictionary<string, int>();
                    for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                    {
                        string headerValue = worksheet.Cells[1, col].Text;
                        if (!string.IsNullOrEmpty(headerValue))
                        {
                            headerDict[headerValue] = col;
                        }
                    }

                    for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                    {
                        var record = new MT5_Tab();

                        foreach (var property in typeof(MT5_Tab).GetProperties())
                        {
                            var propertyName = property.Name;

                            if (headerDict.ContainsKey(propertyName))
                            {
                                int colIndex = headerDict[propertyName];
                                var cell = worksheet.Cells[row, colIndex];
                                var cellValue = cell.Value?.ToString() ?? "";

                                property.SetValue(record, cellValue);
                            }
                        }
                        tab.DataList.Add(record);
                    }

                    viewModel.Tabs.Add(tab);
                }
            }
            return viewModel;
        }
    }
}
