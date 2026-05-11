using System.Collections.Generic;

namespace admin_web.Models.Mtapiuse
{
    public class EditGroup_Model
    {
        public class sql_record
        {
            public string Server { get; set; }
            public string Group { get; set; }
            public string Item { get; set; }
            public string Before { get; set; }
            public string After { get; set; }
            public string UserLogin { get; set; }
            public string Time { get; set; }
        }

        public class log_record
        {
            public string Server { get; set; }
            public string Group { get; set; }
            public List<string> Difference { get; set; }
            public string Result { get; set; }
        }

        public class mt4_input
        {
            public string Server { get; set; }
            public string Group { get; set; }
            public string C_SupportPage { get; set; }
            public string P_MaximumSymbols { get; set; }
            public string P_MaximumOrders { get; set; }
            public string P_EnableChargeOfSwaps { get; set; }
            public string P_ProhibitHedgePositions { get; set; }
            public string A_InactivityPeriod { get; set; }
            public string A_MaximumBalance { get; set; }
            public string A_ArchiveDeletedPendingsOlder { get; set; }
            public string M_MarginCallLevel { get; set; }
            public string M_StopOutLevel { get; set; }
            public string M_StopOutSkipHedged { get; set; }
            public string R_Enable { get; set; }
            public string R_SMTPserver { get; set; }
            public string R_SMTPlogin { get; set; }
            public string R_SMTPpassword { get; set; }
            public string R_SupportEmail { get; set; }
            public string R_TemplatesPath { get; set; }
            public string R_CopyReportToSupport { get; set; }
        }

        public class mt5_input
        {            
        }


        public class Login_Record
        {
            public string Server { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }
        }
    }
}
