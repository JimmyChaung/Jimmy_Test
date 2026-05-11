using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace admin_web.Models.Mtapiuse
{
    public class DisplayAccountCreate_Model
    {
        public class Input_Record
        {
            public string Brand { get; set; }
            public string Server { get; set; }
            public string Login { get; set; }
            public string Name { get; set; }
            public string Group { get; set; }
            public string Email { get; set; }
            public string Leverage { get; set; }
            public string User_Comment { get; set; }
            public string Balance { get; set; }
            public string Balance_Comment { get; set; }
            public string Sales { get; set; }

            [DisplayName("申請人")]
            public string Applicant { get; set; }
            public string Department { get; set; }
            public string Approval { get; set; }
            public string Expired { get; set; }
        }

        public class Login_Record
        {
            public string Login { get; set; }
            public string Password { get; set; }
        }

        public class NextLogin_Record
        {
            public string BRAND { get; set; }
            public string SERVER { get; set; }
            public long LOGIN { get; set; }
        }

        public class Log_Record
        {
            public string Server { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }
            public string Investor_Password { get; set; }
            public string Name { get; set; }
            public string Group { get; set; }
            public string Email { get; set; }
            public string Leverage { get; set; }
            public string User_Comment { get; set; }
            public string Balance { get; set; }
            public string Balance_Comment { get; set; }
            public string Status { get; set; }
        }

        public class Sql_Log_Record
        {
            public string MT4or5 { get; set; }
            public string Server { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }
            public string Investor_Password { get; set; }
            public string Name { get; set; }
            public string Group { get; set; }
            public string Email { get; set; }
            public string Leverage { get; set; }
            public string User_Comment { get; set; }
            public string Balance { get; set; }
            public string Balance_Comment { get; set; }

            [DisplayName("建立人")]
            public string Created_by { get; set; }
            public string Sales { get; set; }

            [DisplayName("申請人")]
            public string Applicant { get; set; }
            public string Department { get; set; }

            [DisplayName("創建日期")]
            public string Date_Create { get; set; }

            [DisplayName("檢查期限")]
            public string Date_Limit { get; set; }
            public string Approval { get; set; }
            public string Time { get; set; }
        }

        public class LogViewModel
        {
            public List<string> Headers { get; set; } = new List<string>();
            public List<Log_Record> Records { get; set; } = new List<Log_Record>();
        }

        public class RuleRecord
        {
            [DisplayName("出入金Comment")]
            public string Deposite_Comment { get; set; }
            [DisplayName("帳號數上限")]
            public string Max_Account { get; set; }
            [DisplayName("資金池上限")]
            public string Max_Amount { get; set; }
            [DisplayName("入金上限")]
            public string Max_Deposite { get; set; }
            [DisplayName("帳號號段")]
            public string Account_Segment { get; set; }
            [DisplayName("過期天數")]
            public string Expired { get; set; }
            [DisplayName("客戶信息")]
            public string Account_Comment { get; set; }
            [DisplayName("MT4組別")]
            public List<string> MT4_Group { get; set; }
            [DisplayName("MT5組別")]
            public List<string> MT5_Group { get; set; }
        }

        public class CapitalRecord
        {
            public string BRAND { get; set; }
            public string TESR_ACCOUNT { get; set; }
            public string TESR_BALANCE { get; set; }
            public string UPDATE_TIME { get; set; }
        }

        //-------------------------------------------------------------------------------------------
        //mt5 data
        public class Mt5_Out_Insert_Cop
        {
            public string Server { get; set; }
            public int Login { get; set; }
            public string Group { get; set; }

            public string Group2 { get; set; }
            public string Name { get; set; }
            public Double Balance { get; set; }
            public string Ccy { get; set; }
            public string Type { get; set; }
            public string Brand { get; set; }
            public int Enable { get; set; }

            public string Comment { get; set; }
        }

        //-------------------------------------------------------------------------------------------
        //mt4 data
        public class Mt4_Out_Insert_Cop
        {
            public string Server { get; set; }
            public int Enable { get; set; }
            public int Login { get; set; }
            public string Group { get; set; }
            public string Name { get; set; }
            public Double Balance { get; set; }
            public string Ccy { get; set; }
            public string Type { get; set; }
            public string Brand { get; set; }
            public string Comment { get; set; }
        }

        //eod price
        public class Eod_price_Insert_Cop
        {
            public DateTime To_data { get; set; }
            public string Ccy { get; set; }
            public double To_Usd { get; set; }
            public double To_Ccy { get; set; }
        }
    }
}
