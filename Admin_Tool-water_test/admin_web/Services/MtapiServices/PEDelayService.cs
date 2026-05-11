using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using admin_web.Models;

namespace admin_web.Services.MtapiServices
{
    public class PEDelayService
    {
        public static List<string> GetFileNames(string folderPath)
        {
            string[] files = Directory.GetFiles(folderPath, "log_DELAY*.xlsx");
            List<string> fileNameList = new();
            foreach (var item in files.Select(Path.GetFileName).ToArray())
            {
                fileNameList.Add(item);
            }
            return fileNameList;
        }

        public static ExcelViewModel View_Data(string filePath)
        {
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            using var package = new ExcelPackage(new FileInfo(filePath));

            var viewModel = new ExcelViewModel
            {
                Tabs = new List<ExcelViewModel.Tab>()
            };

            //List<string> SheetName = new() { "sheet" };
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
        public static List<string> ReadTxtFile(string filePath)
        {
            List<string> lines = new();
            using (StreamReader reader = new(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }
            return lines;
        }
    }

}
