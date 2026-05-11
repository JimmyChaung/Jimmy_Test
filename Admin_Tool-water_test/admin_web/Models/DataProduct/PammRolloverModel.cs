using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;

namespace admin_web.Models.DataProduct
{
    public class PammRollover_Model
    {
        public string Pamm_name { get; set; }
        public string Base_url { get; set; }
        public string Cfg_ids { get; set; }

    }
    public class FullPammConfig
    {
        [Name("pamm_name")]
        public string Pamm_name { get; set; }

        [Name("base_url")]
        public string Base_url { get; set; }

        [Name("username")]
        public string Username { get; set; }

        [Name("password")]
        public string Password { get; set; }

        [Name("client_id")]
        public string Client_id { get; set; }

        [Name("client_secret")]
        public string Client_secret { get; set; }

        [Name("cfg_ids/All")] // **解決特殊字元問題**
        public string Cfg_ids { get; set; }
    }
}
