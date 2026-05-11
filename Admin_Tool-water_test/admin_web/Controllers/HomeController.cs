using admin_web.Models;
using admin_web.Services.HomeServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;

namespace admin_web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private readonly string _connectionString;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public IActionResult Index(string Path)
        {

            //檢查login，是否有seesion

            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Home");
            }
            ViewBag.Username = username;

            if (Path == null || Path == "")
            {
                ViewBag.Homepage_Tool_Main = Tool_Info_Service.GetAll_tool_class();
                ViewBag.Homepage_Tool_Sub = Tool_Info_Service.Homepage_Tool_Sub();
            }
            else
            {
                var ToolList = Tool_Info_Service.Tool_Sub_Sublist(Path);
                ViewBag.ToolMain = ToolList.Where(tool => tool.Type == "Path").FirstOrDefault();
                ViewBag.ToolList = ToolList.Where(tool => tool.Type != "Path").ToList();
            }

            return View();
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            //Test
            username = "123";
            HttpContext.Session.SetString("Username", username);
            return RedirectToAction("Index", "Home");
        }

        //logout

        public IActionResult Logout()
        {
            // 銷毀Session
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Home");
        }

        public IActionResult LoginReset()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        public JsonResult SearchMenu()
        {
            var toolList = Tool_Info_Service.Get_Search_Menu();
            return Json(toolList);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
