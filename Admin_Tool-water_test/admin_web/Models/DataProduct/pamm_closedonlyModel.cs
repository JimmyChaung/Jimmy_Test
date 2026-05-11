using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace admin_web.Models.DataProduct
{
    public class pamm_closedonlyModel
    {
        public class ViewModel
        {
            public List<Tab> Tabs { get; set; } = new List<Tab>();

            public class Tab
            {
                public string Name { get; set; } // Sheet 名稱
                public List<string> HeaderList { get; set; } = new List<string>(); // 欄位名稱
                public List<PammRecord> DataList { get; set; } = new List<PammRecord>(); // Sheet 內的資料
            }
        }

        public class PammRecord
        {
            [DisplayName("id")]
            public string Id { get; set; }

            [DisplayName("Server_name")]
            public string ServerName { get; set; }

            [DisplayName("sales_name")]
            public string SalesName { get; set; }

            [DisplayName("account_id")]
            public string AccountId { get; set; }

            [DisplayName("name")]
            public string Name { get; set; }

            [DisplayName("currency")]
            public string Currency { get; set; }

            [DisplayName("configuration_id")]
            public string ConfigurationId { get; set; }

            [DisplayName("ProfitOpen")]
            public string ProfitOpen { get; set; }

            [DisplayName("BalancePlatform")]
            public string BalancePlatform { get; set; }

            [DisplayName("InvestorDeposits")]
            public string InvestorDeposits { get; set; }

            [DisplayName("InvestorWithdrawals")]
            public string InvestorWithdrawals { get; set; }

            [DisplayName("Floating")]
            public string Floating { get; set; }

            [DisplayName("PerformanceFeeReceived")]
            public string PerformanceFeeReceived { get; set; }

            [DisplayName("Performance rate")]
            public string PerformanceRate { get; set; }

            [DisplayName("PerformanceFeeReceived(%)")]
            public string PerformanceFeeReceivedPercentage { get; set; }

            [DisplayName("sales_team")]
            public string SalesTeam { get; set; }
        }

        public class PmRecord
        {
            public string sql_na { get; set; }
            public string host { get; set; }
            public string user { get; set; }
            public string password { get; set; }
            public string mt4_server { get; set; }
            public string Floating_configid { get; set; }
            public string No_Color_configid { get; set; }
            public string Test_Id { get; set; }
            public string Special_Approve_Login { get; set; }


            // 正確比較的方法
            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }
                var other = (PmRecord)obj;
                return sql_na == other.sql_na &&
                       host == other.host &&
                       user == other.user &&
                       password == other.password &&
                       mt4_server == other.mt4_server &&
                       Floating_configid == other.Floating_configid &&
                       No_Color_configid == other.No_Color_configid &&
                       Test_Id == other.Test_Id &&
                       Special_Approve_Login == other.Special_Approve_Login;
            }

            public override int GetHashCode()
            {
                var hashCode = new HashCode();
                hashCode.Add(sql_na);
                hashCode.Add(host);
                hashCode.Add(user);
                hashCode.Add(password);
                hashCode.Add(mt4_server);
                hashCode.Add(Floating_configid);
                hashCode.Add(No_Color_configid);
                hashCode.Add(Test_Id);
                hashCode.Add(Special_Approve_Login);
                return hashCode.ToHashCode();
            }
        }
    }
}
