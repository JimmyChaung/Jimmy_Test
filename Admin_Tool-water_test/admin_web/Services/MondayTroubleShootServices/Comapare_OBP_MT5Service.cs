using admin_web.Models;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace admin_web.Services.MondayTroubleShootServices
{
    public class Comapare_OBP_MT5Service
    {
        private static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Compare OBP MT5 Tool");
        private static readonly string OutputPath = Path.Combine(ToolPath, "CheckList");

        public static List<string> GetFileNameList()
        {
            List<string> fileNameList = GetFileNames();
            fileNameList = fileNameList
                .Select(file => file.Replace("check", "").Replace("_MT5.xlsx", ""))
                .Select(file => DateTime.Parse(file)) // 轉換為DateTime
                .OrderBy(date => date) // 根據日期排序
                .Select(date => date.ToString("yyyy-MM-dd")) // 轉換回字串
                .ToList();
            return fileNameList;
        }

        public static List<string> GetFileNames()
        {
            string[] files = Directory.GetFiles(OutputPath, "check*_MT5.xlsx");
            List<string> fileNameList = new();
            foreach (var item in files.Select(Path.GetFileName).ToArray())
            {
                fileNameList.Add(item);
            }
            return fileNameList;
        }

        public static ExcelViewModel View_Data(string FileName)
        {
            string FullFilePath = Path.Combine(OutputPath, $"check{FileName}_MT5.xlsx");
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            using var package = new ExcelPackage(new FileInfo(FullFilePath));

            var viewModel = new ExcelViewModel
            {
                Tabs = new List<ExcelViewModel.Tab>()
            };

            List<string> SheetName = new() { "貨幣別排查", "Gateway檢查", "MAM子組(只讀)排查", "組別有swap 設置排查", "組別無swap 設置排查", "MAM 母子組Balance 與credit 加總排查", "Currency與商品名比對", "CurrencyDigits檢查" };

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
