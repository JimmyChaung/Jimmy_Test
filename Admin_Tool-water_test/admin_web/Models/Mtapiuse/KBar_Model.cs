namespace admin_web.Models.Mtapiuse
{
    public class KBar_Model
    {
        public class Edit_Need_Record
        {
            public string OutServer { get; set; }
            public string OutSymbol { get; set; }
            public string InServer { get; set; }
            public string InSymbol { get; set; }
            public string From { get; set; }
            public string To { get; set; }
        }

        public class Del_Record
        {
            public string From { get; set; }
            public string To { get; set; }
            public string Type { get; set; }
            public string Symbol { get; set; }
        }

        public class Login_Record
        {
            public string Login { get; set; }
            public string Password { get; set; }
        }

        // 舊版測試用
        //public class MT4_Server_Record
        //{
        //    public string Server { get; set; }
        //    public string SQL { get; set; }
        //    public string IP { get; set; }
        //    public string Login { get; set; }
        //    public string Password { get; set; }
        //}
        //public class MT5_Server_Record
        //{
        //    public string Server { get; set; }
        //    public string IP { get; set; }
        //    public string Login { get; set; }
        //    public string Password { get; set; }
        //}
    }
}
