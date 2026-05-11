using System;
using System.Collections.Generic;

namespace admin_web.Models.DataProduct
{
    public class ConfigDiffRecords_Model
    {
        public class PeServerRecord
        {
            public string Server { get; set; }
            public string Host { get; set; }
            public string User { get; set; }
            public string Password { get; set; }
            public string Port { get; set; }
            public string Database { get; set; }
        }

        public class LogRecord
        {
            public string Server { get; set; }
            public string Table { get; set; }
            public string Log { get; set; }
            public string Time { get; set; }
        }

        public class TableRecord
        {
            public string primary_key { get; set; }
            public string secondary_key { get; set; }
            public string query { get; set; }
        }

        public class ViewRecord
        {
            public string Name { get; set; }
            public List<Dictionary<string, object>> View { get; set; }
            public bool Status { get; set; }
            public int FixColumn { get; set; }
            public string Error_Log { get; set; }
        }
    }
}
