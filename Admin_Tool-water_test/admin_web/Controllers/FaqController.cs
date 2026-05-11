using admin_web.Services.FaqServices;
using Microsoft.AspNetCore.Mvc;

namespace admin_web.Controllers
{
    public class FaqController : Controller
    {
        public IActionResult FaqHomepage(string tool)
        {
            ViewBag.Navbar = Default_Service.GetAll_Faq();
            var result = Default_Service.Get_Content(tool);
            ViewBag.Tool_Id = tool;
            ViewBag.Name = result.Name;
            ViewBag.Content = result.Content;
            return View();
        }

        [HttpPost]
        public JsonResult Update_Faq(string ID, string Content)
        {
            string result = Default_Service.Update_Faq(ID, Content);
            return Json(result);
        }

        [HttpPost]
        public JsonResult ValidatePassword(string password)
        {
            if (EncryptService.FAQ(password))
            {
                return Json("VALID");
            }
            return Json("INVALID");
        }
    }
}
