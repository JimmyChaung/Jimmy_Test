using admin_web.Models;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace admin_web.Services.DataProductService
{
    public class Pamm_balanceService
    {
        private static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Pamm_balance");
        private static readonly string OutputPath = Path.Combine(ToolPath, "output");

        // 取得檔案清單
        public static List<string> GetFileNameList()
        {
            List<string> fileNameList = GetFileNames();
            fileNameList = fileNameList
                .Select(file => file.Replace("pamm_balance_", "").Replace(".xlsx", ""))
                .OrderBy(fileName => DateTime.Parse(fileName))
                .ToList();
            return fileNameList;
        }

        public static List<string> GetFileNames()
        {
            string[] files = Directory.GetFiles(OutputPath, "pamm_balance_*.xlsx");
            List<string> fileNameList = new();
            foreach (var item in files.Select(Path.GetFileName).ToArray())
            {
                fileNameList.Add(item);
            }
            return fileNameList;
        }

        public static ExcelViewModel View_Data(string FileName)
        {
            string FullFilePath = Path.Combine(OutputPath, $"pamm_balance_{FileName}.xlsx");
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            using var package = new ExcelPackage(new FileInfo(FullFilePath));

            var viewModel = new ExcelViewModel
            {
                Tabs = new List<ExcelViewModel.Tab>()
            };

            List<string> SheetName = package.Workbook.Worksheets.Select(ws => ws.Name).ToList();
            foreach (var item in SheetName)
            {
                var Tab = new ExcelViewModel.Tab();
                var AllData = package.Workbook.Worksheets[item]; // excel裡的資料
                Tab.Name = item;
                if (AllData != null)
                {
                    Tab.Check = true;
                    if (AllData.Dimension != null)
                    {
                        for (int col = 1; col <= AllData.Dimension.End.Column; col++) // 欄位名稱
                        {
                            string headerValue = AllData.Cells[1, col].Text;
                            Tab.HeaderList.Add(headerValue);
                        }

                        for (int row = 2; row <= AllData.Dimension.End.Row; row++)
                        {
                            List<string> Data = new();
                            for (int col = 1; col <= AllData.Dimension.End.Column; col++)
                            {
                                Data.Add(AllData.Cells[row, col].Value?.ToString() ?? "");
                            }
                            Tab.DataList.Add(Data);
                        }
                    }
                }
                viewModel.Tabs.Add(Tab);
            }
            return viewModel;
        }
    }
}
