//using admin_web.Services.StressTestServices;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Configuration;
//using System;
//using System.Linq;
//using System.Threading.Tasks;
//using static admin_web.Models.StressTest.StressTestModel;
//using static admin_web.Services.StressTestServices.StressTestService;

//namespace admin_web.Controllers
//{
//    public class StressTestController : Controller
//    {
//        private readonly IMTExecutorFactory _factory;

//        public StressTestController()
//        {
//            _factory = new MTExecutorFactory();
//        }

//        [HttpGet]
//        public IActionResult Index() => View();

//        [HttpGet]
//        public IActionResult GetServers()
//        {
//            var servers = StressTestService.AllServerDataset
//                .Select(s => new { s.ServerName, mtType = s.MTType.ToString() });
//            return Json(servers);
//        }

//        [HttpPost]
//        public async Task<IActionResult> GetGroups([FromBody] ServerBase request)
//        {
//            try
//            {
//                if (string.IsNullOrEmpty(request?.ServerName))
//                    return Json(new { error = "ServerName 不能為空" });

//                var executor = _factory.Create(request.ServerName);
//                await executor.ConnectAsync();
//                var result = await executor.GetGroupsAsync();
//                executor.Disconnect();

//                return Json(new
//                {
//                    serverName = result.ServerName,
//                    serverGroup = result.ServerGroup,
//                    mtType = result.MTType.ToString()
//                });
//            }
//            catch (Exception e)
//            {
//                return Json(new { error = e.Message });
//            }
//        }

//        [HttpPost]
//        public async Task<IActionResult> RunStressTest([FromBody] StressTestRequest request)
//        {
//            try
//            {
//                if (request == null)
//                    return Json(new { error = "request 為 null" });

//                if (string.IsNullOrEmpty(request.ServerName))
//                    return Json(new { error = "ServerName 不能為空" });

//                var executor = _factory.Create(request.ServerName);
//                await executor.ConnectAsync();
//                executor.CreateAccount(request.Volume, request.Group, request.Leverage, request.Comment);
//                executor.Deposit(request.Balance, request.Comment);
//                executor.Disconnect();

//                var result = executor.GetResult();

//                return Json(new
//                {
//                    serverName = result.ServerName,
//                    mtType = result.MTType.ToString(),
//                    //loginResults = .Select(kv => new
//                    //{
//                    //    login = kv.Key,
//                    //    results = kv.Value.Select(r => new
//                    //    {
//                    //        operation = r.Operation.ToString(),
//                    //        isSuccess = r.IsSuccess,
//                    //        comment = r.Comment
//                    //    })
//                    //})
//                });
//            }
//            catch (Exception e)
//            {
//                return Json(new { error = e.Message, detail = e.StackTrace });
//            }
//        }
//    }

//    public class StressTestRequest
                    //    {
//        public string ServerName { get; set; }
//        public string Group { get; set; }
//        public int Volume { get; set; }
//        public int Leverage { get; set; }
//        public double Balance { get; set; }
//        public string Comment { get; set; }
//    }
//}