using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace admin_web.Models.Permission
{
    public class UserModelsList
    {
        public int Login { get; set; }

        public string Name { get; set; }
        public string Password { get; set; }
        public int Enable { get; set; }
        public DateTime Create_Time { get; set; }
        public string Remark { get; set; }
        public string F_Permission { get; set; }
        public string Email { get; set; }
        public string B_Permission { get; set; }
        public string Role { get; set; }

        public DateTime Last_Time { get; set; }

    }


    // 後端傳到前端資料
    public class AccountModel
    {
        public string LoginValue { get; set; }
        public string NameValue { get; set; }
        public string RoleValue { get; set; }
        public string EmailValue { get; set; }
        public string LastTimeValue { get; set; }
        public string Status { get; set; }
    }

}
