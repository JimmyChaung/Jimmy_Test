using Microsoft.AspNetCore.Mvc;
using admin_web.Services.DataProductService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data;
using admin_web.Models;
using System.Threading;
using System.Diagnostics;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using admin_web.Models.DataProduct;
using Microsoft.AspNetCore.Http;
using RestSharp;
using System.Text.Json;


namespace admin_web.Controllers
{
    public class ServerHealthController : Controller
    {
        public IActionResult ServerHealth(string brandselect, string serverselect, string time, string endTime = "", string excludeGroup = "", string excludeLogin = "")
        {
            if (serverselect != null & time != null)
            {

                //---------紀錄選擇伺服器再回傳到前端

                ViewBag.selectedbrand = brandselect;
                ViewBag.selectedserver = serverselect;
                ViewBag.excludeGroup = excludeGroup;
                ViewBag.excludeLogin = excludeLogin;

                //---------ServerList-------------------
                var data_serverList = ServerHealth_Service.ExecuteSQL_serverlist();
                var brands = data_serverList.Select(s => s.Brand).Distinct().ToList();
                var serversByBrand = data_serverList
                    .GroupBy(s => s.Brand)
                    .ToDictionary(g => g.Key, g => g.Select(s => s.Server).ToList());
                // 將數據傳遞到 ViewBag
                ViewBag.Brands = brands;
                ViewBag.ServersByBrand = serversByBrand;


                //---------日光節約時間--------
                var dailight = ServerHealth_Service.GetTimeDelta(time);
                ViewBag.daylight = dailight.DAYLIGHT;
                ViewBag.timedelta = dailight.TIMEDELTA;
                //----------POSITION--------------------------

                string adjustedTime_position = ServerHealth_Service.Half_adjust_time(time);

                // 從mysql取得資料
                DataTable data_position = ServerHealth_Service.ExecuteSQL_position(serverselect, adjustedTime_position, endTime , dailight.TIMEDELTA, excludeGroup, excludeLogin);

                //----------Add order-------------------------

                string adjustedTime_addorder = ServerHealth_Service.Half_adjust_time(time);

                // 從mysql取得資料
                DataTable data_addorder = ServerHealth_Service.ExecuteSQL_addorder(serverselect, adjustedTime_addorder, endTime, dailight.TIMEDELTA, excludeGroup, excludeLogin);


                //----------Social trading-------------------------

                string adjustedTime_socialtrading = ServerHealth_Service.Half_adjust_time(time);

                // 從mysql取得資料
                DataTable data_socialtrading = ServerHealth_Service.ExecuteSQL_socialtrading(serverselect, adjustedTime_socialtrading, endTime, dailight.TIMEDELTA, excludeLogin);

                //----------HFT-------------------------

                string adjustedTime_hft = ServerHealth_Service.ten_adjust_time(time);

                // 從mysql取得資料
                DataTable DataList_hft_A = ServerHealth_Service.ExecuteSQL_hft(serverselect, adjustedTime_hft, endTime, "A", dailight.TIMEDELTA, excludeGroup, excludeLogin);

                DataTable DataList_hft_B = ServerHealth_Service.ExecuteSQL_hft(serverselect, adjustedTime_hft, endTime, "B", dailight.TIMEDELTA, excludeGroup, excludeLogin);

                //----------archive-------------------------

                string adjustedTime_archive = ServerHealth_Service.eod_adjust_time(time, dailight.TIMEDELTA);

                // 從mysql取得資料
                DataTable DataList_archive = ServerHealth_Service.ExecuteSQL_archive(serverselect, adjustedTime_archive, endTime, dailight.TIMEDELTA, excludeGroup, excludeLogin);

                //----------group ticket-------------------------

                string adjustedTime_groupticket = ServerHealth_Service.monday_adjust_time(time, dailight.TIMEDELTA);

                // 從mysql取得資料
                DataTable DataList_groupticket = ServerHealth_Service.ExecuteSQL_groupticket(serverselect, adjustedTime_groupticket, endTime, dailight.TIMEDELTA, excludeGroup);

                //----------持倉即時更新-------------------------

                ServerInfo serverInfo  = ServerHealth_Service.GetServerGroup(serverselect);
                ViewBag.SGGroup = serverInfo.SGGroup;
                ViewBag.SQLName = serverInfo.SQLName;
                ViewBag.test1 = adjustedTime_archive;
                ViewBag.test2 = serverselect;
                string connectionString_sg = ServerHealth_Service.GetConnectionInfo(serverInfo.SGGroup);
                ViewBag.connectionString_sg = connectionString_sg;

                // 從mysql取得資料
                DataTable DataList_real_pos = ServerHealth_Service.ExecuteSQL_position_realtime(serverselect);


                //----------loading-------------------------

                //string adjustedTime_groupticket = ServerHealth_Service.monday_adjust_time(time);

                // 從mysql取得資料
                DataTable DataList_loading = ServerHealth_Service.ExecuteSQL_loading(serverselect, time, dailight.TIMEDELTA);

                //DataTable data_cpuUsage = ServerHealth_Service.ExecuteSQL_loading(serverselect, time);

                // 使用 Newtonsoft.Json 將列表序列化為 JSON 格式
                string Data_loading = JsonConvert.SerializeObject(DataList_loading);


                //----------configuration-------------------------

                DataTable DataList_configuration = ServerHealth_Service.ExecuteSQL_configuration(serverselect);
                string Data_configuration = JsonConvert.SerializeObject(DataList_configuration);


                //----------伺服器指標-------------------------
                //DataTable DataList_analysis = ServerHealth_Service.ExecuteSQL_analysis(serverselect, adjustedTime_archive);
                
                Indicators IndicatorValues = ServerHealth_Service.ExecuteSQL_analysis2(serverselect, adjustedTime_archive);
                


                ////----------寫資料進入viewModel-------------------------
                var viewModel = new ServerHealth_ViewModel
                {
                    DataList_position = data_position,
                    DataList_orderadd = data_addorder,
                    DataList_socialtrading = data_socialtrading,
                    DataList_hft_A = DataList_hft_A,
                    DataList_hft_B = DataList_hft_B,
                    DataList_archive = DataList_archive,
                    DataList_groupticket = DataList_groupticket,
                    //DataList_real_pos = DataList_real_pos,
                    DataList_loading = DataList_loading,
                    Data_loading = Data_loading,
                    DataList_configuration = DataList_configuration,
                    Data_configuration = Data_configuration,
                    //DataList_analysis = DataList_analysis,
                    IndicatorValues = IndicatorValues,
                    //ServerList = data_serverList
                };

                // 傳遞 ViewModel
                return View(viewModel);
            }
            else
            {
                //-------------------------------------------------如果沒有資料的基礎頁面------------------------------------------------
                //---------ServerList-------------------
                var data_serverList = ServerHealth_Service.ExecuteSQL_serverlist();
                var brands = data_serverList.Select(s => s.Brand).Distinct().ToList();
                var serversByBrand = data_serverList
                    .GroupBy(s => s.Brand)
                    .ToDictionary(g => g.Key, g => g.Select(s => s.Server).ToList());
                // 將數據傳遞到 ViewBag
                ViewBag.Brands = brands;
                ViewBag.ServersByBrand = serversByBrand;

                //
                string time_m = "2025-10-10";
                var dailight = ServerHealth_Service.GetTimeDelta(time_m);
                ViewBag.daylight = dailight.DAYLIGHT;
                ViewBag.timedelta = dailight.TIMEDELTA;

                return View();
            }

        }

        [HttpPost]
        public IActionResult RefreshPosition(string server, string excludeGroup = "", string excludeLogin = "")
        {
            // 執行 SQL 查詢，取得資料
            DataTable data_position_realtime = ServerHealth_Service.ExecuteSQL_position_realtime(server, excludeGroup, excludeLogin);

            // 將 DataTable 轉換為物件的列表
            var data = data_position_realtime.AsEnumerable()
                .Select(row => data_position_realtime.Columns.Cast<DataColumn>()
                    .ToDictionary(col => col.ColumnName, col => row[col]))
                .ToList();

            return Json(data); // 以 JSON 格式返回資料
        }

        [HttpGet]
        public IActionResult GetFileContent(string fileName)
        {
            try
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "FAQ", fileName);  // 檔案的完整路徑
                var content = System.IO.File.ReadAllText(filePath);  // 讀取檔案內容
                return Content(content);  // 返回檔案內容
            }
            catch (Exception ex)
            {
                return StatusCode(500, "檔案讀取失敗：" + ex.Message);
            }
            
        }

        [HttpPost]
        public IActionResult SaveFileContent(string content, string fileName)
        {
            
            try
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "FAQ", fileName);  // 檔案路徑
                System.IO.File.WriteAllText(filePath, content);  // 寫入新的內容到檔案中
                return Ok("儲存成功");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "儲存失敗：" + ex.Message);
            }
        }

        [HttpPost]
        public IActionResult UploadImage(IFormFile upload)
        {
            try
            {
                if (upload != null && upload.Length > 0)
                {
                    // 確定圖片上傳的資料夾路徑
                    var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth","images");
                    if (!Directory.Exists(uploadFolder))
                    {
                        Directory.CreateDirectory(uploadFolder);
                    }

                    // 生成唯一檔案名稱
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(upload.FileName);
                    var filePath = Path.Combine(uploadFolder, fileName);

                    // 儲存圖片檔案
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        upload.CopyTo(stream);
                    }

                    // 返回圖片的 URL
                    var fileUrl = Url.Content($"~/tools/ServerHealth/images/{fileName}");
                    return Ok(new { url = fileUrl });
                }

                return BadRequest("圖片上傳失敗：?");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"圖片上傳失敗：{ex.Message}");
            }
        }


        //---------------------------------SERVER自動化排查--------------------
        
        public IActionResult Auto_Server_Check(string brandSelect, string serverSelect, string startTime, string endTime)
        {

            if (serverSelect != null & startTime != null & endTime != null)
            {
                Debug.Write(brandSelect);
                Debug.Write(serverSelect);
                Debug.Write(startTime);
                Debug.Write(endTime);

                //---------
                DataTable datatable_position = ServerHealth_Service.ExecuteSQL_position_int(serverSelect);
                int position = 0; // 設定預設值
                if (datatable_position.Rows.Count > 0)
                {
                    position = Convert.ToInt32(datatable_position.Rows[0]["Position"]);
                }

                DataTable datatable_ticket = ServerHealth_Service.ExecuteSQL_ticket_int(serverSelect);
                int ticket = 0; // 設定預設值
                if (datatable_ticket.Rows.Count > 0)
                {
                    ticket = Convert.ToInt32(datatable_ticket.Rows[0]["Ticket"]);
                }

                //-----modify
                
                DataTable datatable_modify = ServerHealth_Service.ExecuteSQL_log_modify(serverSelect, startTime, endTime);
                var DataList_modify_all = new List<DataList_modify>();
                foreach (DataRow row in datatable_modify.Rows)
                {
                    DataList_modify_all.Add(new DataList_modify
                    {
                        //ServerName = row["SERVER_NAME"].ToString(),
                        LOGIN = Convert.ToInt64(row["LOGIN"]),
                        COUNT = Convert.ToInt32(row["COUNT"]),
                        MT_INPUT_TIME = Convert.ToDateTime(row["TIME"])
                    });
                }
                var DataList_modify = DataList_modify_all
                .Where(r => r.COUNT >= 600)
                .ToList();

                int modifyTotalLogin = 0;
                int modifyTotalCount = 0;
                int modifyMax = 0;

                if (DataList_modify_all.Any())
                {
                    modifyTotalLogin = DataList_modify_all.Select(r => r.LOGIN).Distinct().Count();
                    modifyTotalCount = DataList_modify_all.Sum(r => r.COUNT);
                    modifyMax = DataList_modify_all.Max(r => r.COUNT);
                }
                
                //-----close
                DataTable datatable_close = ServerHealth_Service.ExecuteSQL_log_close(serverSelect, startTime, endTime);
                var DataList_close_all = new List<DataList_close>();
                foreach (DataRow row in datatable_close.Rows)
                {
                    DataList_close_all.Add(new DataList_close
                    {
                        //ServerName = row["SERVER_NAME"].ToString(),
                        LOGIN = Convert.ToInt64(row["LOGIN"]),
                        COUNT = Convert.ToInt32(row["COUNT"]),
                        MT_INPUT_TIME = Convert.ToDateTime(row["TIME"])
                    });
                }
                var DataList_close = DataList_close_all
                .Where(r => r.COUNT >= 600)
                .ToList();

                int CloseTotalLogin = 0;
                int CloseTotalCount = 0;

                if (DataList_close_all.Any())
                {
                    CloseTotalLogin = DataList_close_all.Select(r => r.LOGIN).Distinct().Count();
                    CloseTotalCount = DataList_close_all.Sum(r => r.COUNT);
                }

                //-----no money

                DataTable datatable_nomoney = ServerHealth_Service.ExecuteSQL_log_nomoney(serverSelect, startTime, endTime);
                var DataList_nomoney_all = new List<DataList_nomoney>();
                foreach (DataRow row in datatable_nomoney.Rows)
                {
                    DataList_nomoney_all.Add(new DataList_nomoney
                    {
                        //ServerName = row["SERVER_NAME"].ToString(),
                        LOGIN = Convert.ToInt64(row["LOGIN"]),
                        COUNT = Convert.ToInt32(row["COUNT"])
                    });
                }
                var DataList_nomoney = DataList_nomoney_all
                .Where(r => r.COUNT >= 600)
                .ToList();

                int NomoneyTotalLogin = 0;
                int NomoneyTotalCount = 0;

                if (DataList_nomoney_all.Any())
                {
                    NomoneyTotalLogin = DataList_nomoney_all.Select(r => r.LOGIN).Distinct().Count();
                    NomoneyTotalCount = DataList_nomoney_all.Sum(r => r.COUNT);
                }

                //-----filter
                DataTable datatable_filter = ServerHealth_Service.ExecuteSQL_log_filter(serverSelect, startTime, endTime);
                var DataList_filter_all = new List<DataList_filter>();
                foreach (DataRow row in datatable_filter.Rows)
                {
                    DataList_filter_all.Add(new DataList_filter
                    {
                        //ServerName = row["SERVER_NAME"].ToString(),
                        SYMBOL = Convert.ToString(row["SYMBOL"]),
                        COUNT = Convert.ToInt32(row["COUNT"]),
                        //MT_INPUT_TIME = Convert.ToDateTime(row["TIME"])
                    });
                }
                var DataList_filter = DataList_filter_all
                .Where(r => r.COUNT >= 3000)
                .ToList();

                //-----unknown user
                DataTable datatable_user = ServerHealth_Service.ExecuteSQL_log_user(serverSelect, startTime, endTime);
                var DataList_user_all = new List<DataList_unknownuser>();
                foreach (DataRow row in datatable_user.Rows)
                {
                    DataList_user_all.Add(new DataList_unknownuser
                    {
                        //ServerName = row["SERVER_NAME"].ToString(),
                        LOGIN = Convert.ToInt64(row["LOGIN"]),
                        COUNT = Convert.ToInt32(row["COUNT"]),
                        MT_INPUT_TIME = Convert.ToDateTime(row["TIME"])
                    });
                }
                var DataList_user = DataList_user_all
                .Where(r => r.COUNT >= 30)
                .ToList();

                //----- password
                DataTable datatable_password = ServerHealth_Service.ExecuteSQL_log_password(serverSelect, startTime, endTime);
                var DataList_password_all = new List<DataList_invalidpassword>();
                foreach (DataRow row in datatable_password.Rows)
                {
                    DataList_password_all.Add(new DataList_invalidpassword
                    {
                        //ServerName = row["SERVER_NAME"].ToString(),
                        LOGIN = Convert.ToInt64(row["LOGIN"]),
                        COUNT = Convert.ToInt32(row["COUNT"]),
                        MT_INPUT_TIME = Convert.ToDateTime(row["TIME"])
                    });
                }
                var DataList_password = DataList_password_all
                .Where(r => r.COUNT >= 30)
                .ToList();

                //----- api
                DataTable datatable_api = ServerHealth_Service.ExecuteSQL_log_api(serverSelect, startTime, endTime);
                var DataList_api_all = new List<DataList_api>();
                foreach (DataRow row in datatable_api.Rows)
                {
                    DataList_api_all.Add(new DataList_api
                    {
                        //ServerName = row["SERVER_NAME"].ToString(),
                        LOGIN = Convert.ToInt64(row["LOGIN"]),
                        COUNT = Convert.ToInt32(row["COUNT"]),
                        MT_INPUT_TIME = Convert.ToDateTime(row["TIME"])
                    });
                }
                var DataList_api = DataList_api_all
                .Where(r => r.COUNT >= 0)
                .ToList();

                //----- modify sec
                DataTable datatable_modify_sec = ServerHealth_Service.ExecuteSQL_log_modify_sec(serverSelect, startTime, endTime);
                var DataList_modify_sec_all = new List<DataList_modify_sec>();
                foreach (DataRow row in datatable_modify_sec.Rows)
                {
                    DataList_modify_sec_all.Add(new DataList_modify_sec
                    {
                        //ServerName = row["SERVER_NAME"].ToString(),
                        LOGIN = Convert.ToInt64(row["LOGIN"]),
                        TOTAL_COUNT = Convert.ToInt32(row["TOTAL_COUNTS"]),
                        SEC_COUNT = Convert.ToInt32(row["REQUEST_PER_SEC"]),
                        MT_INPUT_TIME = Convert.ToDateTime(row["TIME"])
                    });
                }
                var DataList_modify_sec = DataList_modify_sec_all
                .Where(r => r.TOTAL_COUNT >= 0)
                .ToList();

                //-----memory

                DataTable datatable_memory = ServerHealth_Service.ExecuteSQL_server_memory(serverSelect, startTime, endTime);
                long maxMemory = 0; // 設定預設值
                long minMemory = 0; // 設定預設值
                if (datatable_memory.Rows.Count > 0)
                {
                    maxMemory = datatable_memory.AsEnumerable()
                                    .Max(row => row.Field<long>("Freememory"));
                    minMemory = datatable_memory.AsEnumerable()
                                        .Min(row => row.Field<long>("Freememory"));
                }

                //---------ServerList-------------------
                //伺服器選擇
                var data_serverList = ServerHealth_Service.ExecuteSQL_serverlist_mt4();
                var brands = data_serverList.Select(s => s.Brand).Distinct().ToList();
                var serversByBrand = data_serverList
                    .GroupBy(s => s.Brand)
                    .ToDictionary(g => g.Key, g => g.Select(s => s.Server).ToList());
                ViewBag.Brands = brands;
                ViewBag.ServersByBrand = serversByBrand;


                var viewModel = new Auto_Server_Check_ViewModel
                {
                    IsSearchPerformed = true,
                    ServerName = serverSelect,
                    StartTime = startTime,
                    endTime = endTime,
                    Position_Count = position,
                    Ticket_Count = ticket,

                    
                    DataList_modify = DataList_modify,
                    ModifyTotalLogin = modifyTotalLogin,
                    ModifyTotalCount = modifyTotalCount,
                    ModifyMax = modifyMax,
                    

                    DataList_close = DataList_close,
                    CloseTotalLogin = CloseTotalLogin,
                    CloseTotalCount = CloseTotalCount,

                    DataList_nomoney = DataList_nomoney,
                    NomoneyTotalLogin = NomoneyTotalLogin,
                    NomoneyTotalCount = NomoneyTotalCount,

                    DataList_filter = DataList_filter,

                    DataList_unknownuser = DataList_user,

                    DataList_invalidpassword = DataList_password,

                    DataList_api = DataList_api,

                    DataList_modify_sec = DataList_modify_sec,

                    max_memory = maxMemory,

                    min_memory = minMemory,
                };
                // 傳遞 ViewModel
                return View(viewModel);
            }
            else
            {
                //---------ServerList-------------------
                //伺服器選擇
                var data_serverList = ServerHealth_Service.ExecuteSQL_serverlist_mt4();
                var brands = data_serverList.Select(s => s.Brand).Distinct().ToList();
                var serversByBrand = data_serverList
                    .GroupBy(s => s.Brand)
                    .ToDictionary(g => g.Key, g => g.Select(s => s.Server).ToList());
                ViewBag.Brands = brands;
                ViewBag.ServersByBrand = serversByBrand;

                return View();
            }

        }


        [HttpPost]
        public IActionResult Lark_message([FromBody] JsonElement payload)
        {
            try
            {
                var client = new RestClient("https://open.feishu.cn/open-apis/auth/v3/tenant_access_token/internal");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                var body = "{\"app_id\": \"cli_a72e3c6020ea5010\",\"app_secret\": \"lRGTbrgvjbA00omWKemtrd8K3OTEbrvY\"}";
                request.AddParameter("application/json", body, ParameterType.RequestBody);
                IRestResponse response = client.Execute(request);
                //Console.WriteLine(response.Content);


                var content = response.Content;

                using var doc = JsonDocument.Parse(content);
                string token = doc.RootElement.GetProperty("tenant_access_token").GetString();

                Console.WriteLine("Bearer " + token);

                var client2 = new RestClient("https://open.larksuite.com/open-apis/im/v1/messages?receive_id_type=chat_id");
                client2.Timeout = -1;

                var request2 = new RestRequest(Method.POST);
                request2.AddHeader("Content-Type", "application/json"); //Bearer t-g2064s7MUKENVEBPJUS2O2XZAFBEMDV2YECDAHPM
                request2.AddHeader("Authorization", "Bearer " + token);


                //var contentText =@"VT8 Server 排查 (排查時間02/28 10:00 GMT+2開始)";
                string text = payload.GetProperty("text").GetString();

                // 1. 先把 text 包進 content JSON 字串
                var contentJsonString = System.Text.Json.JsonSerializer.Serialize(new { text = text });

                Debug.WriteLine(contentJsonString);


                // 2. 外層 message 包裝
                var messagePayload = new
                {
                    receive_id = "oc_c978b7dc238c9a72c746adbc145b9a19",
                    msg_type = "text",
                    content = contentJsonString
                };

                // 3. 轉成 JSON 字串作為 request body
                var body2 = System.Text.Json.JsonSerializer.Serialize(messagePayload);

                request2.AddParameter("application/json", body2, ParameterType.RequestBody);
                IRestResponse response2 = client2.Execute(request2);
                Console.WriteLine(response2.Content);
                return Ok("成功");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"失敗：{ex.Message}");
            }

        }

    

    }
}
