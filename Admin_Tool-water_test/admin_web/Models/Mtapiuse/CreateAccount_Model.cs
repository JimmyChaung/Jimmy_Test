namespace admin_web.Models.Mtapiuse
{
    public class CreateAccount_Model
    {
        public class log_record
        {
            // 基本
            public string Server { get; set; }
            public string Login { get; set; }
            public string Group { get; set; }
            public string Leverage { get; set; }
            public string Comment { get; set; }
            public string Email { get; set; }
            // 僅給當前使用者查看，不寫進庫
            public string Password { get; set; }
            public string Investor_Password { get; set; }
            // 執行結果
            public string Result { get; set; }
            // 創建成功紀錄
            public string UserLogin { get; set; }
            public string Time { get; set; }
        }

        public class mt4_input
        {
            public string Server { get; set; }
            public string Login { get; set; }
            public string Name { get; set; }
            public string Group { get; set; }
            public string Leverage { get; set; }
            public string AgentAccount { get; set; }
            public string Taxes { get; set; }
            public string SendReports { get; set; }
            public string Mqid { get; set; }
            public string Status { get; set; }
            public string Id { get; set; }
            public string Comment { get; set; }
            public string EnableChangePassword { get; set; }
            public string EnableReadOnly { get; set; }
            public string EnableOTP { get; set; }
            public string PasswordPhone { get; set; }
            public string Country { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string ZipCode { get; set; }
            public string Address { get; set; }
            public string LeadSource { get; set; }
            public string Phone { get; set; }
            public string Email { get; set; }
        }

        public class mt5_input
        {
            public string Server { get; set; }
            public string Login { get; set; }
            public string Name { get; set; }
            public string Group { get; set; }
            public string Leverage { get; set; }
            public string Company { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }       
            public string Country { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string ZipCode { get; set; }
            public string Address { get; set; }
            public string Comment { get; set; }
        }

        public class Login_Record
        {
            public string Server { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }
        }
    }
}
