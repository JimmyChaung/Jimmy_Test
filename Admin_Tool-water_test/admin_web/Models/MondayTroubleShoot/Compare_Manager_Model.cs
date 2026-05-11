using System.Collections.Generic;

namespace admin_web.Models.MondayTroubleShoot
{
    public class Compare_Manager_Model
    {
        public class MT5_ViewModel
        {
            public List<Tab> Tabs { get; set; } = new List<Tab>();

            public class Tab
            {
                public string Name { get; set; } // Sheet 名稱
                public List<MT5_Tab> DataList { get; set; } = new List<MT5_Tab>(); // Sheet 內的資料
            }
        }
        public class MT5_Tab
        {
            public string Servers { get; set; }
            public string Login { get; set; }
            public string ENABLE { get; set; }
            public string Name { get; set; }
            public string Mailbox { get; set; }
            public string Server { get; set; }
            public string RequestLimitLogs { get; set; }
            public string RequestLimitReports { get; set; }
            public string Groups { get; set; }
            public string Access { get; set; }
            public string Right_Admin { get; set; }
            public string Right_Manager { get; set; }
            public string Right_Cfg_Servers { get; set; }
            public string Right_Cfg_Access { get; set; }
            public string Right_Cfg_Time { get; set; }
            public string Right_Cfg_Holidays { get; set; }
            public string Right_Cfg_Groups { get; set; }
            public string Right_Cfg_Managers { get; set; }
            public string Right_Cfg_Requests { get; set; }
            public string Right_Cfg_Gateways { get; set; }
            public string Right_Cfg_Plugins { get; set; }
            public string Right_Cfg_Datafeeds { get; set; }
            public string Right_Cfg_Reports { get; set; }
            public string Right_Cfg_Symbols { get; set; }
            public string Right_Cfg_Hst_Sync { get; set; }
            public string Right_Cfg_ECN { get; set; }
            public string Right_Srv_Journals { get; set; }
            public string Right_Srv_Reports { get; set; }
            public string Right_Charts { get; set; }
            public string Right_Email { get; set; }
            public string Right_News { get; set; }
            public string Right_Export { get; set; }
            public string Right_Techsupport { get; set; }
            public string Right_Market { get; set; }
            public string Right_Accountant { get; set; }
            public string Right_Acc_Read { get; set; }
            public string Right_Acc_Details { get; set; }
            public string Right_Acc_Manager { get; set; }
            public string Right_Acc_Delete { get; set; }
            public string Right_Acc_Online { get; set; }
            public string Right_Confirm_Actions { get; set; }
            public string Right_Notifications { get; set; }
            public string Right_Trades_Read { get; set; }
            public string Right_Trades_Manager { get; set; }
            public string Right_Trades_Delete { get; set; }
            public string Right_Trades_Dealer { get; set; }
            public string Right_Trades_Supervisor { get; set; }
            public string Right_Quotes_Raw { get; set; }
            public string Right_Quotes { get; set; }
            public string Right_Symbol_Details { get; set; }
            public string Right_Risk_Manager { get; set; }
            public string Right_Group_Margin { get; set; }
            public string Right_Group_Commission { get; set; }
            public string Right_Reports { get; set; }
            public string Right_Finteza_Access { get; set; }
            public string Right_Finteza_Websites { get; set; }
            public string Right_Finteza_Campaigns { get; set; }
            public string Right_Finteza_Reports { get; set; }
            public string Right_Clients_Access { get; set; }
            public string Right_Clients_Create { get; set; }
            public string Right_Clients_Edit { get; set; }
            public string Right_Clients_Delete { get; set; }
            public string Right_Documents_Access { get; set; }
            public string Right_Documents_Create { get; set; }
            public string Right_Documents_Edit { get; set; }
            public string Right_Documents_Delete { get; set; }
            public string Right_Documents_Files_Add { get; set; }
            public string Right_Documents_Files_Delete { get; set; }
            public string Right_Comments_Access { get; set; }
            public string Right_Comments_Create { get; set; }
            public string Right_Comments_Delete { get; set; }
            public string Right_Cfg_Funds { get; set; }
            public string Right_Cfg_Mails { get; set; }
            public string Right_Cfg_Messengers { get; set; }
            public string Right_Cfg_KYC { get; set; }
            public string Right_Cfg_Automations { get; set; }
            public string Right_Cfg_Allocations { get; set; }
            public string Right_Cfg_VPS { get; set; }
            public string Right_Cfg_Payments { get; set; }
            public string Right_Cfg_Web_Services { get; set; }
            public string Right_Cfg_Corporate { get; set; }
            public string Right_Grp_Details_Commission { get; set; }
            public string Right_Admin_Computer { get; set; }
            public string Right_Acc_Technical { get; set; }
            public string Right_Acc_Tech_Modify { get; set; }
            public string Right_Clients_KYC { get; set; }
            public string Right_Subscriptions_View { get; set; }
            public string Right_Subscriptions_Edit { get; set; }
            public string Right_Payments_Process { get; set; }
            public string Right_Payments_Access { get; set; }
            public string Right_Payments_Edit { get; set; }
            public string Right_Payments_Delete { get; set; }
        }

        public class MT4ServerRecord
        {
            public string Server { get; set; }
            public string Connect { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }
        }

        public class SQLServerRecord
        {
            public string Host { get; set; }
            public int Port { get; set; }
            public string User { get; set; }
            public string Password { get; set; }
            public string DB { get; set; }
            public int TimeZoneAdd { get; set; }
        }
    }
}
