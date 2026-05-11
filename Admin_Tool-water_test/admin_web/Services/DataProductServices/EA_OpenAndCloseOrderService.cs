using admin_web.Models;
using CsvHelper;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using static admin_web.Models.DataProduct.Ea_SendMail_Model;

namespace admin_web.Services.DataProductService
{
    public class EA_OpenAndCloseOrderService
    {

        private static readonly string EA_Rank_Path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "EA_OpenAndCloseOrderTool", "EA_Auto", "output", "EA");
        private static readonly string Mail_Config_Path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "EA_OpenAndCloseOrderTool", "EA_Auto", "config", "Mail_Send.csv");
        private static readonly string Mail_Log_Path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "EA_OpenAndCloseOrderTool", "EA_Auto", "MailLog", "MailLog.csv");
        
        public static List<string> GetEaRankList()
        {
            List<string> fileNameList = GetFileNames();
            fileNameList = fileNameList
                .Select(file => file.Replace("EA_rank_new", "").Replace(".xlsx", ""))
                .OrderBy(fileName =>
                {
                    string datePart = fileName.Split('~')[0];
                    return DateTime.Parse(datePart);
                })
                .ToList();

            return fileNameList;
        }

        // 取得EA_Rank資料內容
        public static List<string> GetFileNames()
        {
            string[] files = Directory.GetFiles(EA_Rank_Path, "EA_rank_new*.xlsx");
            List<string> AllFileNameList = new();
            foreach (var item in files.Select(Path.GetFileName).ToArray())
            {
                AllFileNameList.Add(item);
            }
            return AllFileNameList;
        }

        // 讀取Excel
        public static ExcelViewModel View_Data(string FileName)
        {
            string FullFilePath = Path.Combine(EA_Rank_Path, $"EA_rank_new{FileName}.xlsx");
            ExcelPackage.LicenseContext = LicenseContext.Commercial;
            using var package = new ExcelPackage(new FileInfo(FullFilePath));

            var viewModel = new ExcelViewModel
            {
                Tabs = new List<ExcelViewModel.Tab>()
            };

            List<string> SheetName = package.Workbook.Worksheets.Select(ws => ws.Name).ToList();

            foreach (var item in SheetName)
            {
                var tab = new ExcelViewModel.Tab();
                var allData = package.Workbook.Worksheets[item];
                List<int> datetime_list = new();
                tab.Name = item;
                if (allData != null)
                {
                    tab.Check = true;
                    if (allData.Dimension != null)
                    {
                        for (int col = 1; col <= allData.Dimension.End.Column; col++)
                        {
                            string headerValue = allData.Cells[1, col].Text;
                            tab.HeaderList.Add(headerValue);
                        }

                        for (int row = 2; row <= allData.Dimension.End.Row; row++)
                        {
                            List<string> data = new();
                            for (int col = 1; col <= allData.Dimension.End.Column; col++)
                            {
                                data.Add(allData.Cells[row, col].Value?.ToString() ?? "");
                            }
                            tab.DataList.Add(data);
                        }
                    }
                }
                viewModel.Tabs.Add(tab);
            }
            return viewModel;
        }

        // 新增寄件資訊
        public static string MailConfig_Add(string brand, string send_mail, string receive_mail)
        {
            string FileName = Mail_Config_Path;
            var records = ReadCsv(FileName);

            if (records.Any(r => r.brand == brand && r.send_mail == send_mail && r.receive_mail == receive_mail))
            {
                return "欲新增的資料已存在，請確認後再添加。";
            }
            if (string.IsNullOrEmpty(brand) || string.IsNullOrEmpty(send_mail) || string.IsNullOrEmpty(receive_mail))
            {
                return "不可有空白資料，請確認後再添加。";
            }
            var newRecord = new MailRecord
            {
                brand = brand,
                send_mail = send_mail,
                receive_mail = receive_mail
            };
            records.Add(newRecord);
            WriteCsv(records, FileName);
            return "新增成功！！！";
        }

        // 修改寄件資訊
        public static string MailConfig_Edit(string brandBefore, string brandAfter, string send_mailBefore, string send_mailAfter, string receive_mailBefore, string receive_mailAfter)
        {
            string FileName = Mail_Config_Path;
            var records = ReadCsv(FileName); // 原本的檔案

            if (records.Any(r => r.brand == brandAfter && r.send_mail == send_mailAfter && r.receive_mail == receive_mailAfter))
            {
                return "欲更新的資料已存在，請確認後再更新。";
            }

            // 比對符合的行資料，對其進行更新
            var EditRecord = records.FirstOrDefault(r => r.brand == brandBefore && r.send_mail == send_mailBefore && r.receive_mail == receive_mailBefore);
            if (EditRecord != null)
            {
                EditRecord.brand = brandAfter;
                EditRecord.send_mail = send_mailAfter;
                EditRecord.receive_mail = receive_mailAfter;
                WriteCsv(records, Mail_Config_Path);
                return "更新成功！！！";
            }
            return "更新資料時發生錯誤。";
        }

        // 刪除寄件資訊
        public static string MailConfig_Delete(string brand, string send_mail, string receive_mail)
        {

            string FileName = Mail_Config_Path;
            var records = ReadCsv(FileName);

            // 刪除原有的Server資料
            var delRecord = records.FirstOrDefault(r => r.brand == brand && r.send_mail == send_mail && r.receive_mail == receive_mail);
            if (delRecord != null)
            {
                records.Remove(delRecord);
                WriteCsv(records, FileName);
                return "刪除成功！！！";
            }
            return "刪除資料時發生錯誤。";
        }

        // 刷新寄件資訊
        public static List<MailRecord> GetMailRecords()
        {
            var mailRecords = ReadCsv(Mail_Config_Path);
            return mailRecords;
        }

        // 讀取寄件資訊檔案
        public static List<MailRecord> ReadCsv(string FilePath)
        {
            if (!File.Exists(FilePath))
            {
                return new List<MailRecord>();
            }
            using var reader = new StreamReader(FilePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<MailRecord>().ToList();
        }

        // 讀取寄件LOG
        public static List<MailResult> GetMailResults()
        {
            var mailResults = ReadMailLogCsv(Mail_Log_Path); // 返回一個 List<MailResult>
            return mailResults;
        }

        // 讀取寄件LOG檔案
        public static List<MailResult> ReadMailLogCsv(string FilePath)
        {
            if (!File.Exists(FilePath))
            {
                return new List<MailResult>();
            }
            using var reader = new StreamReader(FilePath, Encoding.GetEncoding("Big5"));
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<MailResult>().ToList();
        }

        // 寫入寄件資訊檔案
        private static void WriteCsv(List<MailRecord> records, string FileName)
        {
            using var writer = new StreamWriter(FileName);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(records);
        }
    }
}
