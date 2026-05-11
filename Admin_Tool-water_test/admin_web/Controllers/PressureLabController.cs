using admin_web.Services.QuerysqlServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using admin_web.Services.StressTestServices;
using admin_web.Models;
using static admin_web.Services.StressTestServices.StressTestService;
using System.IO;
using static admin_web.Models.StressTest.StressTestModel;
using System.Diagnostics;

namespace admin_web.Controllers
{
    public class PressureLabController : Controller
    {

        private readonly IConfiguration _configuration;
        private readonly IMTExecutorFactory _factory;
        public PressureLabController(IConfiguration configuration)
        {
            _configuration = configuration;
            _factory = new MTExecutorFactory(_configuration);
        }

        // GET: PressureLabController
        public ActionResult Index()
        {
            return View();
        }


        //benson_開發

        [HttpGet]
        public PartialViewResult Dtress_testone_Partial()
        {
            return PartialView("_Dtress_testonePartial");
        }


        [HttpGet]
        public PartialViewResult Dtress_testtwo_Partial()
        {
            return PartialView("_Dtress_testtwoPartial");
        }

        [HttpGet]
        public PartialViewResult Dtress_testreport_Partial()
        {
            return PartialView("_Dtress_testreportPartial");
        }


        //葉面顯示
        // 將資料導入
        [HttpPost]
        public async Task<PartialViewResult> Pressure_post_from([FromBody] StressTestRequest request)
        {
            var tmp = 0;
            try
            {
                //取的現在的時間GMT+3
                var action_time = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).DateTime; ;

                var (docxData, newId) = await CreateMTAccount(request);
                tmp = newId;
                var momery = Get_Respect_Red_Result(request, action_time);
                docxData.UpdatePerformance(momery);
                _factory.FillDocx(newId, docxData);
            } catch(Exception e)
            {
                if (tmp > 0)
                {
                    await _factory.UpdateDBLogStatus(tmp, "Failed");
                }
                _factory.ExecuteLogtoTxt($"Failed CreateMTAccount ID : {tmp}, error : {e.Message}, detail : {e.StackTrace}");

                Debug.WriteLine("error");
            }
            return PartialView("_Dtress_testreportPartial");
        }

        //葉面顯示
        // 將資料導入
        [HttpPost]
        public Respect_Red_Result Get_Respect_Red_Result([FromBody] StressTestRequest request, DateTime action_time)
        {



            Respect_Red_Result out_fi = new Respect_Red_Result();
            //"standard_value"

            if (request.STANDARD_MODE == "cpu_value")
            {
                var t = _factory.Create(request.ServerName);
                var _result = t.ConnectAsync();
                out_fi = t.get_mt_journal(request.STANDARD_MODE ,request.SERVER_HOUR, action_time);
            }

            else
            {
                var t = _factory.Create(request.ServerName);
                var _result = t.ConnectAsync();
                out_fi = t.get_mt_journal(request.STANDARD_MODE, request.SERVER_HOUR, action_time);
                out_fi.Cpu_Average = request.CPU_VALUE;
                out_fi.Memory_Average = request.MEMORY_VALUE;
                out_fi.IOPS_Average = request.DISK_VALUE?? "0";

            }

            return out_fi;
        }


        // Water
        // 拉取 server list 
        [HttpGet]
        public async Task<IActionResult> GetMtServers()
        {
            var querylistServeice = _factory.GetServerNames();
            return Json(querylistServeice);
        }

        // 取得指定 Server 所有 Group
        [HttpPost]
        public async Task<IActionResult> GetSpecifyServerGroups([FromBody] StressTestRequest  request)
        {
            try
            {
                var executor = _factory.Create(request.ServerName);
                await executor.ConnectAsync();
                await executor.GetGroupsAsync();
                var result = executor.GetSymbol();
                executor.Disconnect();

                return Json(new
                {
                    serverName = result.ServerName,
                    serverGroup = result.ServerGroup,
                    serverSymbol = result.ServerSymbol
                });
            } catch(Exception e)
            {
                return Json(new { error = e.Message, detail = e.StackTrace });
            }
        }

        [HttpPost]
        public async Task<(DocxReportData docxData, int newId)> CreateMTAccount([FromBody] StressTestRequest request)
        {
            int newId = 0;
            int Login;
            _factory.ExecuteLogtoTxt($"Started create account Volume : {request.Volume} on {request.ServerName}");
            Login = await _factory.LoginAsync(request.ServerName);
            var executor = _factory.Create(request.ServerName);
            _factory.ExecuteLogtoTxt($"Started CreateMTAccount {request.Volume} on {request.ServerName}");
            await executor.ConnectAsync();

            var result = executor.GetResult();
            if (Login != 0) result.initLogin = Login;
            newId = await _factory.InsertLogToDB(result, request.type, "Running");
            _factory.LoginRecord(newId, request.ServerName, result.initLogin, result.initLogin + request.Volume);

            executor.CreateAccount(request.Volume, request.Group, request.Leverage, request.Comment);
            _factory.ExecuteLogtoTxt($"Success CreateMTAccount account Volume : {request.Volume}, Group : {request.Group}, Leverage : {request.Leverage} on {request.ServerName}");

            executor.Deposit(request.Balance, request.Comment);
            _factory.ExecuteLogtoTxt($"Success Deposit ${request.Balance} on {request.ServerName}");

            var docxData = await executor.OpenTrade(request);
            _factory.ExecuteLogtoTxt($"Success OpenTrade Volume {request.ORDER__LOTS}, Symbol {request.ORDER_SYMBOL}");

            result = executor.GetResult();
            executor.Disconnect();

            await _factory.UpdateDBLogStatus(newId, "Success");
            return (docxData, newId);
        }

        [HttpPost]
        public async Task<IActionResult> DeletSpecifyLog([FromBody] int Id)
        {
            var (serverName, start, end) = await _factory.GetDBSpecifyLog(Id);
            var executor = _factory.Create(serverName);
            await executor.ConnectAsync();
            await executor.DeleteAccount(start, end);
            var result = await _factory.DeleteDBSpecifyLog(Id);
            return Ok(result);
        }

        [HttpGet]
        public IActionResult DownloadDocxFile([FromQuery] int Id)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Pressure_LAB", "Report", $"report_{Id}.docx");
            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                stream.CopyTo(memory);
            }
            memory.Position = 0;
            var contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            return File(memory, contentType, filePath);
        }

        [HttpGet]
        public async Task<IActionResult> GetDBLogRecord()
        {
            var result = await _factory.GetDBLogAllData();
            return Json(result);
        }

        //20260325_updata
        public class StressTestRequest
        {
            // StressTestRequest 的欄位
            public string ServerName { get; set; }
            public string Group { get; set; }
            public int Leverage { get; set; }
            public int Volume { get; set; }
            public double Balance { get; set; }
            public string Comment { get; set; }
            public string type { get; set; }

            // OpenOrderRequest 的欄位
            public string ORDER_SYMBOL { get; set; }
            public double ORDER__LOTS { get; set; }
            public int ORDER__VOLUME { get; set; }
            public double ORDER__PRICE { get; set; } = 0.0;
            public string OPEN_ORDER_COMMENT { get; set; }
            public bool ORDER_CHECK_TP_SL { get; set; }
            public string ORDER_MODEL_TYPE { get; set; }
            public double ORDER_TP_VALUE { get; set; }

            public double ORDER_SL_VALUE { get; set; }

            //20260325壓測標準
            public string STANDARD_MODE { get; set; }

            public string CPU_VALUE { get; set; }

            public string MEMORY_VALUE { get; set; }

            public string DISK_VALUE { get; set; }

            public string SERVER_HOUR { get; set; }
        }
    }
}
