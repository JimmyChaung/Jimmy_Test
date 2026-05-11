using admin_web.Infrastructure;
using admin_web.Services.Querysql;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using admin_web.Models;
using admin_web.Services.QuerymysqlServices;
using Microsoft.Extensions.Logging;
using admin_web.Models;
using Microsoft.Extensions.Configuration;
using admin_web.Services.QuerysqlServices;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace admin_web.Controllers
{
    public class QuerysqlController : Controller
    {
        private readonly IConfiguration _configuration;
        private List<MysqlModel.ServerInfoDto> ServerTmp;

        public class SqlRequestDto
        {
            public string Sql { get; set; }

        }

        public QuerysqlController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 主頁面
        public IActionResult Index()
        {
            return View();
        }




        //--------
        // 取得server
        public class DLQueryRequest
        {
            public List<string> Servers { get; set; }

            public List<DLQueryItem> Items { get; set; }
        }

        // 取的參數值
        public class DLQueryItem
        {
            public string Key { get; set; }

            public List<string> Values { get; set; }
        }

        // --------------------------DataLake start ---------------------------------

        [HttpGet]
        public PartialViewResult DataLakePartial()
        {
            return PartialView("_DataLakePartial");
        }

        // 拉取 server list 內容
        [HttpGet]
        public async Task<IActionResult> GetMtServers()
        {
            QuerygetlistServices querygetlistServices = new QuerygetlistServices(_configuration);
            var server_list = await querygetlistServices.GetAllServerList();

            var filteredList = server_list.Where(x => x.Mt.Equals("mt5", StringComparison.OrdinalIgnoreCase))
                        .ToList();
            return Json(filteredList);
        }

        // 測試資料，是固定畫面的test
        [HttpPost]
        public async Task<IActionResult> RunDataLakeQueryAsync([FromBody] DLQueryRequest req)
        {
            if (req == null)
            {
                return Content("沒有收到任何資料！");
            }

            if (req.Servers == null || req.Servers.Count == 0)
            {
                return Content("請至少選擇一個 Server！");
            }

            if (req.Items == null || req.Items.Count == 0)
            {
                return Content("請至少選擇一個查詢項目！");
            }

            // Debug print (可留可刪)
            Console.WriteLine("Servers: " + string.Join(", ", req.Servers));

            foreach (var item in req.Items)
            {
                Console.WriteLine($"Key: {item.Key}");
                Console.WriteLine("Values: " + string.Join(", ", item.Values));
            }

            // TODO: 依照 server 做不同查詢
            // EX:
            foreach (var server in req.Servers)
            {
                switch (server)
                {
                    case "mt4_au":

                        var db = new QuerydatabicksService(
                        "https://dbc-09524e23-f03d.cloud.databricks.com",
                        Environment.GetEnvironmentVariable("DATABRICKS_TOKEN"),
                        "297d76a7383a8221"
                        );

                        string sql = "SELECT * FROM prod_corporate.silver.mt5_orders_history LIMIT 10";
                        var result1 = await db.ExecuteQueryAsync(sql);

                        //foreach (DataRow r in dt.Rows)
                        //{
                        //Console.WriteLine(r[0]);

                        //}


                        //////建立連線
                        ////DatabricksConnection Connection = new DatabricksConnection("dbc-09524e23-f03d.cloud.databricks.com", "", "");
                        //var databricks = new QuerydatabicksService("", "");

                        //Debug.WriteLine($"databricks:{databricks}");

                        //// 同步取得結果
                        //string result_1 = databricks.ExecuteQueryAsync("SELECT * FROM prod_corporate.silver.mt4_users LIMIT 10;").GetAwaiter().GetResult();
                        //Console.WriteLine(result_1);

                        //確認有連線

                        //傳所有key到後端


                        Debug.WriteLine("mt4_au");

                        break;
                    case "mt4_au2":
                        Debug.WriteLine("mt4_au2");
                        break;
                    default:
                        Debug.WriteLine("Unknown server");
                        break;
                }
            }

            string result = "伺服器: " + string.Join(", ", req.Servers) + "\n";

            foreach (var item in req.Items)
            {
                result += $"查詢 {item.Key}: {string.Join(", ", item.Values)}\n";
            }

            return Content(result);
        }



        // 測試資料，彈性查詢的畫面
        [HttpPost]
        public async Task<IActionResult> RunDatalakeQueryByManual([FromBody] SqlRequestDto sqlCode)
        {
            Debug.WriteLine("RundatalakeQueryByManual");

            Debug.WriteLine(sqlCode.Sql.GetType());

            // TODO: 依照 server 做不同查詢
            var db = new QuerydatabicksService(
                                  "https://dbc-09524e23-f03d.cloud.databricks.com",
                                  Environment.GetEnvironmentVariable("DATABRICKS_TOKEN"),
                                  "297d76a7383a8221"
                                  );

            string sql_co = sqlCode.Sql.Replace("\r\n", " ").Replace("\n", " "); 

            //string sql = "SELECT * FROM prod_corporate.silver.mt5_orders_history LIMIT 10";
            var result1 = await db.ExecuteQueryAsync(sql_co);

            Debug.WriteLine(result1);

            return Ok(result1);
        }

        [HttpPost]
        public async Task<IActionResult> DownloadCsv([FromBody] SqlRequestDto request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.Sql))
                    return Json(new { error = "SqlQuery 不能為空" });

                var service = new QuerydatabicksService(
                "https://dbc-09524e23-f03d.cloud.databricks.com",
                Environment.GetEnvironmentVariable("DATABRICKS_TOKEN"),
                "297d76a7383a8221"
                );
                var data = await service.ExecuteQueryAllDataDownload(request.Sql);

                if (!data.Any())
                    return Json(new { error = "查無資料" });

                // 組成 CSV
                var sb = new StringBuilder();
                var columns = data.First().Keys.ToList();

                // Header
                sb.AppendLine(string.Join(",", columns.Select(EscapeCsv)));

                // Rows
                foreach (var row in data)
                    sb.AppendLine(string.Join(",", columns.Select(col => EscapeCsv(row.GetValueOrDefault(col, "")))));

                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                var fileName = $"export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception e)
            {
                return Json(new { error = e.Message, detail = e.StackTrace });
            }
        }

        [HttpGet]
        public async Task<IActionResult> QueryStatus(string statementId)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("DATABRICKS_TOKEN"));

                var res = await client.GetAsync(
                    $"https://dbc-09524e23-f03d.cloud.databricks.com/api/2.0/sql/statements/{statementId}");
                var body = await res.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(body).RootElement;

                var state = json.GetProperty("status").GetProperty("state").GetString();
                return Json(new { statementId, state });
            }
            catch (Exception e)
            {
                return Json(new { error = e.Message });
            }
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }


    // --------------------------DataLake end ---------------------------------


    // MYSQL --------------------------------------------------------------

        [HttpGet]
        public PartialViewResult MysqlPartial()
        {
            return PartialView("_MysqlPartial");
        }

        [HttpGet]
        public async Task<IActionResult> GetMysqlServerList()
        {
            var data = new QuerymysqlService(_configuration);
            var result = await data.GetAllServersWithTables();
            return Json(result);
        }





        [HttpPost]
        public IActionResult RunMysqlQuery(bool enableFlag)
        {
            if (!enableFlag)
                return Content("【MySQL】未授權查詢（checkbox 未勾選）");

            // ---- 這裡放你的 MySQL 查詢邏輯 ----
            Debug.WriteLine("[MySQL] 查詢執行");

            return Content("【MySQL 查詢成功】這是查詢結果");
        }

        [HttpPost]
        public async Task<IActionResult> RunMysqlQueryByManual([FromBody] QueryserverlistModel.ServerWithTables sql)
        {
            Debug.WriteLine(sql.SqlCode);
            var data = new QuerymysqlService(_configuration);
            var result = await data.MysqlQuerySearch(sql.SqlCode);
            return Ok(result);
        }

        // GET: QuerysqlController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: QuerysqlController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: QuerysqlController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: QuerysqlController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: QuerysqlController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: QuerysqlController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: QuerysqlController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
