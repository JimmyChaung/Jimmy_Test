using Newtonsoft.Json;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static admin_web.Models.DataProduct.Pelican_CompareModel;

namespace admin_web.Services.DataProductService
{
    public class Pelican_CompareService
    {
        private static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Pelican Compare Tool");
        private static readonly string OutputPath = Path.Combine(ToolPath, "output_file");

        // 更新Excel
        public static void UpdateExcel(string data, string file)
        {
            string FileName = Path.Combine(OutputPath,$"pelican_{file}.xlsx");

            var update_data = JsonConvert.DeserializeObject<List<PelicanRecord>>(data);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;


            using (var package = new ExcelPackage(new FileInfo(FileName)))
            {
                var worksheet = package.Workbook.Worksheets[0];

                worksheet.Cells[1, 8].Value = "Note";

                int startRow = 2;

                foreach (var record in update_data)
                {
                    worksheet.Cells[startRow, 1].Value = record.Server ?? string.Empty;
                    worksheet.Cells[startRow, 2].Value = record.TICKET ?? string.Empty;
                    worksheet.Cells[startRow, 3].Value = record.LOGIN ?? string.Empty;
                    worksheet.Cells[startRow, 4].Value = record.OPEN_TIME ?? string.Empty;
                    worksheet.Cells[startRow, 5].Value = record.PROFIT ?? string.Empty;
                    worksheet.Cells[startRow, 6].Value = record.COMMENT ?? string.Empty;
                    worksheet.Cells[startRow, 7].Value = record.Col ?? string.Empty;
                    worksheet.Cells[startRow, 8].Value = record.Note ?? string.Empty;

                    startRow++;
                }

                package.Save();
            }
        }

        // 取得檔案清單
        public static List<string> GetFileNameList()
        {
            List<string> fileNameList = GetFileNames();
            fileNameList = fileNameList
                .Select(file => file.Replace("pelican_", "").Replace(".xlsx", ""))
                .Select(file => DateTime.Parse(file)) // 轉換為DateTime
                .OrderBy(date => date) // 根據日期排序
                .Select(date => date.ToString("yyyy-MM-dd")) // 轉換回字串
                .ToList();
            return fileNameList;
        }

        public static List<string> GetFileNames()
        {
            string[] files = Directory.GetFiles(OutputPath, "pelican_*.xlsx");
            List<string> fileNameList = new();
            foreach (var item in files.Select(Path.GetFileName).ToArray())
            {
                fileNameList.Add(item);
            }
            return fileNameList;
        }

        public static ViewModel View_Data(string FileDate)
        {
            string FullFileName = $"pelican_{FileDate}.xlsx";
            var viewModel = ReadExcel_View(FullFileName);
            return viewModel;
        }

        private static ViewModel ReadExcel_View(string FileName)
        {
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            string filePath = Path.Combine(OutputPath, FileName);
            using var package = new ExcelPackage(new FileInfo(filePath));

            var viewModel = new ViewModel();
            List<string> sheetNames = package.Workbook.Worksheets.Select(ws => ws.Name).ToList();

            foreach (var sheetName in sheetNames)
            {
                var sheet = package.Workbook.Worksheets[sheetName];
                if (sheet == null || sheet.Dimension == null)
                    continue;

                // 欄位名稱
                var headerRow = sheet.Cells[1, 1, 1, sheet.Dimension.End.Column];
                var headerDict = new Dictionary<string, int>();
                foreach (var cell in headerRow)
                {
                    string headerValue = cell.Text;
                    viewModel.HeaderList.Add(headerValue);
                    headerDict[headerValue] = cell.Start.Column;
                }

                // 資料
                for (int row = 2; row <= sheet.Dimension.End.Row; row++)
                {
                    var record = new PelicanRecord();

                    foreach (var property in typeof(PelicanRecord).GetProperties())
                    {
                        var displayName = property.GetCustomAttributes(typeof(System.ComponentModel.DisplayNameAttribute), true)
                                                   .Cast<System.ComponentModel.DisplayNameAttribute>()
                                                   .FirstOrDefault()?.DisplayName;

                        if (displayName != null && headerDict.ContainsKey(displayName))
                        {
                            int colIndex = headerDict[displayName];
                            var cell = sheet.Cells[row, colIndex];
                            var cellValue = cell.Value;

                            if (cellValue != null)
                            {
                                object convertedValue = null;

                                if (property.Name == "OPEN_TIME")
                                {
                                    if (cellValue is DateTime dateTimeValue)
                                    {
                                        // 如果值已經是 DateTime，則格式化為所需的格式
                                        convertedValue = dateTimeValue.ToString("yyyy/MM/dd hh:mm:ss tt");
                                    }
                                    else if (double.TryParse(cellValue.ToString(), out double oaDate))
                                    {
                                        // 如果是 Excel 的內部日期編碼，則將其轉換為 DateTime
                                        convertedValue = DateTime.FromOADate(oaDate).ToString("yyyy/MM/dd hh:mm:ss tt");
                                    }
                                    else
                                    {
                                        // 如果值是字串或其他形式，則直接使用原始文字
                                        convertedValue = cell.Text;
                                    }
                                }
                                else
                                {
                                    convertedValue = Convert.ChangeType(cell.Text, property.PropertyType);
                                }

                                property.SetValue(record, convertedValue);
                            }
                        }
                    }

                    viewModel.DataList.Add(record);
                }
            }

            return viewModel;
        }
    }
}
