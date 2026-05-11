using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace admin_web.Models.Brokeree
{
    public class Op_userinfo
    {
        public int Id { set; get; }

        public string fa_user { set; get; }

        public string fa_pass { set; get; }

        public string pm_user { set; get; }

        public string pm_pass { set; get; }

        public string server_name { set; get; }

        public string server_url { set; get; }

        public string serverport { set; get; }

        public string op_url { set; get; }
        public string config_set { set; get; }

    }
}
