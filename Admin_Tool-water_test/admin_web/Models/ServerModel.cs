namespace admin_web.Models
{
    public class ServerModel
    {
        public class ServerRecord
        {
            public string MT { get; set; }
            public string SERVER_NAME { get; set; }
            public string SERVER_PROXY { get; set; }
            public string SERVER_DC { get; set; }
            public string SQL_NAME { get; set; }
            public string SQL_HOST { get; set; }
            public string SQL_USER { get; set; }
            public string SQL_PASSWORD { get; set; }
            public string SQL_PORT { get; set; }
        }

        public class SqlConnectRecord
        {
            public string HOST { get; set; }
            public string USER { get; set; }
            public string PASSWORD { get; set; }
            public string PORT { get; set; }
        }
    }
}
