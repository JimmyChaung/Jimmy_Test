using System;

namespace admin_web.Models.DataProduct
{
    public class PAMM_pending_desModel
    {
        public class ViewModel
        {
            public string PAMM_Server { get; set; }
            public string Order { get; set; }
            public string Server { get; set; }
            public string Login { get; set; }
            public string Status { get; set; }
            public string CURRENCY { get; set; }
            public string amount { get; set; }
        }

        public class PmRecord
        {
            public string sql_na { get; set; }
            public string host { get; set; }
            public string user { get; set; }
            public string password { get; set; }
            public string mt_server { get; set; }

            // 正確比較的方法
            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }
                var other = (PmRecord)obj;
                return sql_na == other.sql_na && host == other.host && user == other.user && password == other.password && mt_server == other.mt_server;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(sql_na, host, user, password, mt_server);
            }
        }
    }
}
