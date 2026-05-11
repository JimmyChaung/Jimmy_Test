using System.Collections.Generic;

namespace admin_web.Models.Mtapiuse
{
    public class CloseOrder_Model
    {
        public class log_record
        {
            public string Server { get; set; }
            public string Ticket { get; set; }
            public string Result { get; set; }
            public string UserLogin { get; set; }
            public string Time { get; set; }
        }

        public class input
        {
            public string Server { get; set; }
            public string Ticket { get; set; }
            public string Volume { get; set; }
            public string Price { get; set; }
        }

        public class Login_Record
        {
            public string Server { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }
        }
    }
}