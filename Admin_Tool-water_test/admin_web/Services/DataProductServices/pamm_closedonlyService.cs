using CsvHelper;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using static admin_web.Models.DataProduct.pamm_closedonlyModel;

namespace admin_web.Services.DataProductService
{
    public class pamm_closedonlyService
    {
        private static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "pamm_closedonly");
        private static readonly string OutputPath = Path.Combine(ToolPath, "out_put");
        private static readonly string PmSetPath = Path.Combine(ToolPath, "setting", "pm_set.csv");

        public static List<string> GetNoColorId()
        {
            var pm_setting = ReadCsv_Pm();
            List<string> no_color_id = new();
            foreach (var item in pm_setting)
            {
                if (item.No_Color_configid != "nan")
                {
                    var ids = item.No_Color_configid.Split(',');

                    foreach (var id in ids)
                    {
                        no_color_id.Add(id.Trim());
                    }
                }
            }
            return no_color_id;
        }

        public static List<string> GetSpecialId()
        {
            var pm_setting = ReadCsv_Pm();
            List<string> special_id = new();
            foreach (var item in pm_setting)
            {
                if (item.Special_Approve_Login != "nan")
                {
                    var ids = item.Special_Approve_Login.Split(',');

                    foreach (var id in ids)
                    {
                        special_id.Add(id.Trim());
                    }
                }
            }
            return special_id;
        }


        public static List<string> GetPammNameList()
        {
            List<string> fileNameList = GetFileNames();
            List<string> PammList = fileNameList
                .Select(fileName => fileName.Split('_')[0]) // 取得pamm部分
                .Distinct()
                .ToList();

            return PammList;
        }

        public static List<string> GetPammDateList(string PammName)
        {
            List<string> fileNameList = GetFileNames();
            List<string> DateList = fileNameList
                .Select(fileName => fileName.Replace(".xlsx",""))
                .Where(fileName => fileName.Contains(PammName))
                .Select(fileName => fileName.Split('_')[1]) // 取得日期部分
                .ToList();

            return DateList;
        }

        public static List<string> GetFileNames()
        {
            string[] files = Directory.GetFiles(OutputPath, "pamm*.xlsx");
            List<string> fileNameList = new();
            foreach (var item in files.Select(Path.GetFileName).ToArray())
            {
                fileNameList.Add(item);
            }
            return fileNameList;
        }

        public static ViewModel View_Data(string PammName, string PammDate)
        {
            string FullFileName = $"{PammName}_{PammDate}.xlsx";
            var viewModel = ReadExcel_View(FullFileName);
            return viewModel;
        }

        // 新增Server資訊
        public static string PmRecords_Add(string sql_na, string host, string user, string password, string mt4_server, string Floating_configid, string No_Color_configid, string Test_Id, string Special_Approve_Login)
        {
            string FileName = PmSetPath;
            var records = ReadCsv_Pm();

            if (records.Any(r => r.sql_na == sql_na))
            {
                return "欲新增的SQL已存在，請確認後再添加。";
            }
            if (string.IsNullOrEmpty(sql_na) || string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(mt4_server))
            {
                return "不可有空白資料，請確認後再添加。";
            }

            var newRecord = new PmRecord
            {
                sql_na = sql_na,
                host = host,
                user = user,
                password = password,
                mt4_server = mt4_server,
                Floating_configid = Floating_configid,
                No_Color_configid = No_Color_configid,
                Test_Id = Test_Id,
                Special_Approve_Login = Special_Approve_Login
            };

            records.Add(newRecord);
            WriteCsv(records, FileName);
            return "新增成功！！！";
        }


        // 修改Server資訊
        public static string PmRecords_Edit(string sql_naBefore, string sql_naAfter, string hostAfter, string userAfter, string passwordAfter, string mt4_serverAfter, string Floating_configidAfter, string No_Color_configidAfter, string Test_IdAfter, string Special_Approve_LoginAfter)
        {
            var records = ReadCsv_Pm(); // 原本的檔案

            if (records.Any(r => r.sql_na == sql_naAfter && sql_naBefore != sql_naAfter))
            {
                return "該SQL資料已存在，請確認後再更新。";
            }

            // 比對符合的行資料，對其進行更新
            var EditRecord = records.FirstOrDefault(r => r.sql_na == sql_naBefore);

            if (EditRecord != null)
            {
                EditRecord.sql_na = sql_naAfter;
                EditRecord.host = hostAfter;
                EditRecord.user = userAfter;
                EditRecord.password = passwordAfter;
                EditRecord.mt4_server = mt4_serverAfter;
                EditRecord.Floating_configid = Floating_configidAfter;
                EditRecord.No_Color_configid = No_Color_configidAfter;
                EditRecord.Test_Id = Test_IdAfter;
                EditRecord.Special_Approve_Login = Special_Approve_LoginAfter;

                WriteCsv(records, PmSetPath);
                return "更新成功！！！";
            }

            return "更新資料時發生錯誤。";
        }


        // 刪除Server資訊
        public static string PmRecords_Delete(string sql_na)
        {
            var records = ReadCsv_Pm();

            // 刪除符合條件的Server資料
            var delRecord = records.FirstOrDefault(r =>
                r.sql_na == sql_na
            );

            if (delRecord != null)
            {
                records.Remove(delRecord);
                WriteCsv(records, PmSetPath);
                return "刪除成功！！！";
            }

            return "刪除資料時發生錯誤。";
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

                // 欄位名稱
                var headerRow = sheet.Cells[1, 1, 1, sheet.Dimension.End.Column];
                var headerDict = new Dictionary<string, int>();
                foreach (var cell in headerRow)
                {
                    string headerValue = cell.Text;
                    tab.HeaderList.Add(headerValue);
                    headerDict[headerValue] = cell.Start.Column;
                }

                // 資料
                for (int row = 2; row <= sheet.Dimension.End.Row; row++)
                {
                    var record = new PammRecord();

                    foreach (var property in typeof(PammRecord).GetProperties())
                    {
                        var displayName = property.GetCustomAttributes(typeof(System.ComponentModel.DisplayNameAttribute), true)
                                                   .Cast<System.ComponentModel.DisplayNameAttribute>()
                                                   .FirstOrDefault()?.DisplayName;

                        if (displayName != null && headerDict.ContainsKey(displayName))
                        {
                            int colIndex = headerDict[displayName];
                            var cellValue = sheet.Cells[row, colIndex].Text;
                            if (!string.IsNullOrEmpty(cellValue))
                            {
                                var convertedValue = Convert.ChangeType(cellValue, property.PropertyType);
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

        // 寫入Server資訊檔案
        private static void WriteCsv(List<PmRecord> records, string FileName)
        {
            using var writer = new StreamWriter(FileName);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(records);
        }

        // 讀取Server設置的資料
        public static List<PmRecord> ReadCsv_Pm()
        {
            string FilePath = PmSetPath;
            if (!File.Exists(FilePath))
            {
                return new List<PmRecord>();
            }
            using var reader = new StreamReader(FilePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<PmRecord>().ToList();
        }
    }
}
