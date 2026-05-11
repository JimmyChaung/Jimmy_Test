using System.Collections.Generic;

namespace admin_web.Models.Mtapiuse
{
    public class EditAccount_Model
    {
        public class sql_record
        {
            public string Server { get; set; }
            public int Login { get; set; }
            public string Item { get; set; }
            public string Before { get; set; }
            public string After { get; set; }
            public string UserLogin { get; set; }
            public string Time { get; set; }
        }
        public class log_record
        {
            public string Server { get; set; }
            public string Login { get; set; }
            public List<string> Difference { get; set; }
            public string Result { get; set; }
        }

        public class mt4_input
        {
            public string Server { get; set; }
            public string Login { get; set; }
            public string Enable { get; set; }
            public string Name { get; set; }
            public string Color { get; set; }
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
            public string Group { get; set; }
            public string Rights { get; set; }
            public string Leverage { get; set; }
            public string Name { get; set; }
            public string Color { get; set; }
            //public string FirstName { get; set; }
            //public string MiddleName { get; set; }
            //public string LastName { get; set; }
            public string Company { get; set; }
            //public string Registration { get; set; }
            public string ID_number { get; set; }
            public string Status { get; set; }
            public string Lead_campaign { get; set; }
            public string Lead_source { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Country { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Zip_code { get; set; }
            public string Address { get; set; }
            public string Comment { get; set; }
            public string Bank_Account { get; set; }
            public string Agent_account { get; set; }
            public string LimitPositionsValue { get; set; }
        }


        public class Login_Record
        {
            public string Server { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }
        }
    }
}
