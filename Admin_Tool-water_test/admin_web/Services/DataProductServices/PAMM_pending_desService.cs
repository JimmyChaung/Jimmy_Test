using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using static admin_web.Models.DataProduct.PAMM_pending_desModel;

namespace admin_web.Services.DataProductService
{
    public class PAMM_pending_desService
    {
        private static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "PAMM_pending_des");
        private static readonly string OutputPath = Path.Combine(ToolPath, "out_put");
        private static readonly string MtServerPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "PAMM_pending_des", "setting", "pm_set.csv");

        // 將欲展示的CSV傳到前端
        public static List<string> GetFileNameList()
        {
            List<string> fileNameList = GetFileNames();

            fileNameList = fileNameList
                .Select(file => file.Replace("allPAMM_", "").Replace(".csv", ""))
                .Select(file => DateTime.Parse(file)) // 轉換為DateTime
                .OrderBy(date => date) // 根據日期排序
                .Select(date => date.ToString("yyyy-MM-dd")) // 轉換回字串
                .ToList();

            return fileNameList;
        }

        public static List<string> GetFileNames()
        {
            string[] files = Directory.GetFiles(OutputPath, "allPAMM_*.csv");
            List<string> fileNameList = new();
            foreach (var item in files.Select(Path.GetFileName).ToArray())
            {
                fileNameList.Add(item);
            }
            return fileNameList;
        }

        public static List<ViewModel> View_Data(string FileName)
        {
            string FullFileName = $"allPAMM_{FileName}.csv";
            var viewModel = ReadCsv_View(Path.Combine(ToolPath, "out_put", FullFileName));
            return viewModel;
        }

        // 新增Server資訊
        public static string ServerConfig_Add(string sql_na, string host, string user, string password, string mt_server)
        {
            string FileName = MtServerPath;
            var records = ReadCsv_Pm();

            if (records.Any(r => r.sql_na == sql_na))
            {
                return "欲新增的SQL已存在，請確認後再添加。";
            }
            if (string.IsNullOrEmpty(sql_na) || string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(password))
            {
                return "不可有空白資料，，請確認後再添加。";
            }
            var newRecord = new PmRecord
            {
                sql_na = sql_na,
                host = host,
                user = user,
                password = password,
                mt_server = mt_server
            };
            records.Add(newRecord);
            WriteCsv(records, FileName);
            return "新增成功！！！";
        }

        // 修改Server資訊
        public static string ServerConfig_Edit(string sql_naBefore, string sql_naAfter, string hostBefore, string hostAfter, string userBefore, string userAfter, string passwordBefore, string passwordAfter, string mt_serverBefore, string mt_serverAfter)
        {
            var records = ReadCsv_Pm(); // 原本的檔案

            if (records.Any(r => r.sql_na == sql_naAfter && sql_naBefore != sql_naAfter))
            {
                return "該SQL資料已存在，請確認後再更新。";
            }

            // 比對符合的行資料，對其進行更新
            var EditRecord = records.FirstOrDefault(r => r.sql_na == sql_naBefore && r.host == hostBefore && r.user == userBefore && r.password == passwordBefore && r.mt_server == mt_serverBefore);
            if (EditRecord != null)
            {
                EditRecord.sql_na = sql_naAfter;
                EditRecord.host = hostAfter;
                EditRecord.user = userAfter;
                EditRecord.password = passwordAfter;
                EditRecord.mt_server = mt_serverAfter;
                WriteCsv(records, MtServerPath);
                return "更新成功！！！";
            }
            return "更新資料時發生錯誤。";
        }

        // 刪除Server資訊
        public static string ServerConfig_Delete(string sql_na, string host, string user, string password, string mt_server)
        {
            var records = ReadCsv_Pm();

            // 刪除原有的Server資料
            var delRecord = records.FirstOrDefault(r => r.sql_na == sql_na && r.host == host && r.user == user && r.password == password && r.mt_server == mt_server);
            if (delRecord != null)
            {
                records.Remove(delRecord);
                WriteCsv(records, MtServerPath);
                return "刪除成功！！！";
            }
            return "刪除資料時發生錯誤。";
        }

        // 寫入Server資訊檔案
        private static void WriteCsv(List<PmRecord> records, string FileName)
        {
            using var writer = new StreamWriter(FileName);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(records);
        }

        // 讀取主畫面表格的資料
        private static List<ViewModel> ReadCsv_View(string filePath)
        {
            var viewModelList = new List<ViewModel>();

            if (!File.Exists(filePath))
            {
                return viewModelList;
            }

            using (var reader = new StreamReader(filePath))
            {
                string headerLine = reader.ReadLine();
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    var values = line.Split(',');

                    var viewModel = new ViewModel
                    {
                        PAMM_Server = values[0],
                        Order = values[1],
                        Server = values[2],
                        Login = values[3],
                        Status = values[4],
                        CURRENCY = values[5],
                        amount = values[6]
                    };

                    viewModelList.Add(viewModel);
                }
            }

            return viewModelList;
        }

        // 讀取Server設置的資料
        public static List<PmRecord> ReadCsv_Pm()
        {
            string FilePath = MtServerPath;
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
