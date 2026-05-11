using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace admin_web.Models.DataProduct
{
    public class Pelican_CompareModel
    {
        public class ViewModel
        {
            public List<string> HeaderList { get; set; } = new List<string>(); // 欄位名稱
            public List<PelicanRecord> DataList { get; set; } = new List<PelicanRecord>(); // Sheet 內的資料
        }

        public class PelicanRecord
        {
            [DisplayName("Server")]
            public string Server { get; set; }

            [DisplayName("TICKET")]
            public string TICKET { get; set; }

            [DisplayName("LOGIN")]
            public string LOGIN { get; set; }

            [DisplayName("OPEN_TIME")]
            public string OPEN_TIME { get; set; }

            [DisplayName("PROFIT")]
            public string PROFIT { get; set; }

            [DisplayName("COMMENT")]
            public string COMMENT { get; set; }

            [DisplayName("Col")]
            public string Col { get; set; }

            [DisplayName("Note")]
            public string Note { get; set; }
        }
    }
}
