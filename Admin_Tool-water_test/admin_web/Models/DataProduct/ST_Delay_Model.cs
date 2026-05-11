using System;
using System.Collections.Generic;

namespace admin_web.Models.DataProduct
{
    public class ST_Delay_Model
    {
        public class ViewModel
        {
            public List<Tab> Tabs { get; set; } = new List<Tab>();

            public class Tab
            {
                public string Name { get; set; } // Sheet 名稱
                public List<TicketRecord> DataList { get; set; } = new List<TicketRecord>(); // Sheet 內的資料
            }
        }

        public class TicketRecord
        {
            public string F_Server { get; set; }
            public string F_TICKET { get; set; }
            public string F_LOGIN { get; set; }
            public string F_SYMBOL { get; set; }
            public string F_VOLUME { get; set; }
            public string F_OPEN_TIME { get; set; }
            public string F_OPEN_PRICE { get; set; }
            public string F_CLOSE_TIME { get; set; }
            public string F_COMMISSION { get; set; }
            public string F_SWAPS { get; set; }
            public string F_CLOSE_PRICE { get; set; }
            public string F_PROFIT { get; set; }
            public string F_COMMENT { get; set; }
            public string P_TICKET { get; set; }
            public string P_Server { get; set; }
            public string P_LOGIN { get; set; }
            public string P_SYMBOL { get; set; }
            public string P_VOLUME { get; set; }
            public string P_OPEN_TIME { get; set; }
            public string P_OPEN_PRICE { get; set; }
            public string P_CLOSE_TIME { get; set; }
            public string P_COMMISSION { get; set; }
            public string P_SWAPS { get; set; }
            public string P_CLOSE_PRICE { get; set; }
            public string P_PROFIT { get; set; }
            public string P_COMMENT { get; set; }
            public string diff_open { get; set; }
            public string diff_close { get; set; }
            public string diff { get; set; }
        }

        public class StRecord
        {
            public string num { get; set; }
            public string server { get; set; }

            // 正確比較的方法
            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }
                var other = (StRecord)obj;
                return num == other.num && server == other.server;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(num, server);
            }
        }
    }
}
