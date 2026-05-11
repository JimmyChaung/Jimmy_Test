using CsvHelper;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using static admin_web.Models.DataProduct.ST_Delay_Model;

namespace admin_web.Services.DataProductService
{
    public class ST_DelayService
    {
        private static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ST_delay");
        private static readonly string OutputPath = Path.Combine(ToolPath, "output");
        private static readonly string StServerPath = Path.Combine(ToolPath, "st_server.csv");

        public static List<string> GetFileNameList()
        {
            List<string> fileNameList = GetFileNames();

            fileNameList = fileNameList
                .Select(file => file.Replace(".xlsx", ""))
                .Select(file => DateTime.Parse(file)) // 轉換為DateTime
                .OrderBy(date => date) // 根據日期排序
                .Select(date => date.ToString("yyyy-MM-dd")) // 轉換回字串
                .ToList();

            return fileNameList;
        }

        public static List<string> GetFileNames()
        {
            string[] files = Directory.GetFiles(OutputPath, "*.xlsx");
            List<string> fileNameList = new();
            foreach (var item in files.Select(Path.GetFileName).ToArray())
            {
                fileNameList.Add(item);
            }
            return fileNameList;
        }

        public static ViewModel View_Data(string FileDate)
        {
            string FullFileName = $"{FileDate}.xlsx";
            var viewModel = ReadExcel_View(FullFileName);
            return viewModel;
        }

        // 讀取主畫面表格的資料
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

                // 工作表名稱
                var tab = new ViewModel.Tab
                {
                    Name = sheetName
                };

                // 取得欄位名稱
                var headerRow = sheet.Cells[1, 1, 1, sheet.Dimension.End.Column];
                var headerDict = new Dictionary<string, int>();

                foreach (var cell in headerRow)
                {
                    string headerValue = cell.Text.Trim();
                    headerDict[headerValue] = cell.Start.Column;
                }

                // 資料
                for (int row = 2; row <= sheet.Dimension.End.Row; row++)
                {
                    var record = new TicketRecord();

                    foreach (var property in typeof(TicketRecord).GetProperties())
                    {
                        var propertyName = property.Name;

                        if (headerDict.ContainsKey(propertyName))
                        {
                            int colIndex = headerDict[propertyName];
                            var cell = sheet.Cells[row, colIndex];
                            var cellValue = cell.Value;

                            if (cellValue != null)
                            {
                                object convertedValue = null;

                                if (property.Name.Contains("TIME"))
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

                    tab.DataList.Add(record);
                }
                viewModel.Tabs.Add(tab);
            }
            return viewModel;
        }

        // 新增Server資訊
        public static string StConfig_Add(string num, string server)
        {
            string FileName = StServerPath;
            var records = ReadCsv_St();

            if (records.Any(r => r.server == server))
            {
                return "欲新增的Server已存在，請確認後再添加。";
            }
            if (string.IsNullOrEmpty(num) || string.IsNullOrEmpty(server))
            {
                return "不可有空白資料，，請確認後再添加。";
            }
            var newRecord = new StRecord
            {
                num = num,
                server = server,
            };
            records.Add(newRecord);
            WriteCsv(records, FileName);
            return "新增成功！！！";
        }

        // 修改Server資訊
        public static string StConfig_Edit(string numAfter, string serverBefore, string serverAfter)
        {
            var records = ReadCsv_St(); // 原本的檔案

            if (records.Any(r => r.server == serverAfter && serverBefore != serverAfter))
            {
                return "該Server資料已存在，請確認後再更新。";
            }

            // 比對符合的行資料，對其進行更新
            var EditRecord = records.FirstOrDefault(r => r.server == serverBefore);
            if (EditRecord != null)
            {
                EditRecord.num = numAfter;
                EditRecord.server = serverAfter;
                WriteCsv(records, StServerPath);
                return "更新成功！！！";
            }
            return "更新資料時發生錯誤。";
        }

        // 刪除Server資訊
        public static string StConfig_Delete(string server)
        {
            var records = ReadCsv_St();

            // 刪除原有的Server資料
            var delRecord = records.FirstOrDefault(r => r.server == server);
            if (delRecord != null)
            {
                records.Remove(delRecord);
                WriteCsv(records, StServerPath);
                return "刪除成功！！！";
            }
            return "刪除資料時發生錯誤。";
        }

        // 寫入Server資訊檔案
        private static void WriteCsv(List<StRecord> records, string FileName)
        {
            using var writer = new StreamWriter(FileName);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(records);
        }

        // 讀取Server設置的資料
        public static List<StRecord> ReadCsv_St()
        {
            string FilePath = StServerPath;
            if (!File.Exists(FilePath))
            {
                return new List<StRecord>();
            }
            using var reader = new StreamReader(FilePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<StRecord>().ToList();
        }
    }
}
