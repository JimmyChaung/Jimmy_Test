using admin_web.Models.Permission;
using admin_web.Services.PermissionServices;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static admin_web.Services.PermissionServices.Permission_Data;

namespace admin_web.Controllers
{
    public class PermissionController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        private readonly Permission_Data _permissionData;

        // 注入 Permission_Data
        public PermissionController(Permission_Data permissionData)
        {
            _permissionData = permissionData;
        }


        //get
        [HttpGet]
        public IActionResult Account_management()
        {
            // 呼叫 ExecuteSQL_serverlist 方法
            List<UserModelsList> serverList = _permissionData.ExecuteSQL_serverlist();

            return PartialView("Account_management", serverList);
        }




        [HttpPost]
        public IActionResult Account_management([FromBody] AccountModel accountUpdate)
        {
            //        body: JSON.stringify({ loginValue, nameValue, roleValue, emailValue, lastTimeValue, status })

            // 呼叫 ExecuteSQL_serverlist 方法
            var updata_status = _permissionData.ExecuteSQL_updata(accountUpdate);


            Debug.WriteLine($"--------{updata_status}-----------");

            // 呼叫 ExecuteSQL_serverlist 方法
            List<UserModelsList> serverList = _permissionData.ExecuteSQL_serverlist();
            return PartialView("Account_management", serverList);

        }



        //刪除
        // Post - 刪除帳戶
        [HttpPost]
        public IActionResult DeleteAccount([FromBody] int LoginID)
        {
            // 呼叫 ExecuteSQL_delete 方法刪除帳戶
            var deleteStatus = _permissionData.ExecuteSQL_delete(LoginID);

            Debug.WriteLine($"--------Delete Status: {deleteStatus}-----------");




            // 重新載入列表
            List<UserModelsList> serverList = _permissionData.ExecuteSQL_serverlist();
            return PartialView("Account_management", serverList);
        }


        //新增帳號
        [HttpPost]
        public IActionResult AddAccount([FromBody] AccountModel AddModel)
        {
            //  body: JSON.stringify({ AccountLogin: Login, AccountName: Name, AccountEmail: Email, AccountRole: Role }),


            if (AddModel == null || string.IsNullOrWhiteSpace(AddModel.LoginValue) ||
                string.IsNullOrWhiteSpace(AddModel.NameValue) ||
                string.IsNullOrWhiteSpace(AddModel.EmailValue) ||
                string.IsNullOrWhiteSpace(AddModel.RoleValue))
            {
                return BadRequest("請完整填寫資料！");
            }

            // 新增帳號到資料庫
            bool isAdded = _permissionData.AddAccount(AddModel); // 假設 AddAccount 是資料庫操作方法

            if (isAdded)
            {
                // 返回更新後的部分視圖
                List<UserModelsList> serverList = _permissionData.ExecuteSQL_serverlist();
                return PartialView("Account_management", serverList);
            }
            else
            {
                return StatusCode(500, "新增帳號失敗！");
            }
        }










        public IActionResult AnotherPage()
        {
            return PartialView("AnotherPage"); // 另一個部分視圖
        }
    }
}
