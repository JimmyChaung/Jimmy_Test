using System.Collections.Generic;

namespace admin_web.Models
{
    public class ExcelViewModel
    {
        // Excel資料
        public List<Tab> Tabs { get; set; } // 所有以Sheet名稱做紀錄的List
        public class Tab
        {
            public List<string> HeaderList { get; set; } // 欄位名稱
            public List<List<string>> DataList { get; set; } // Sheet內的資料
            public bool Check { get; set; } // 是否有該Sheet名稱
            public string Name { get; set; } // Sheet名稱
            public Tab()
            {
                HeaderList = new List<string>();
                DataList = new List<List<string>>();
            }
        }
    }
}
