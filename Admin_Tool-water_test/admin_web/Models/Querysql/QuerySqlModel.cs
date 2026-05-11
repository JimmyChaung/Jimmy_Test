using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;

namespace admin_web.Models
{
    public class DatabrickModel
    {
        public class DatabricksResultRowModel
        {
            public string Column1 { get; set; }
            public string Column2 { get; set; }
            public int Column3 { get; set; }
        }
    }
    public class MysqlModel
    {
        public string MySqlCode { get; set; }
        public enum MTPlatformType
        {
            mt4 = 0,
            mt5 = 1
        }
        public class ServerInfoDto
        {
            public string ServerName { get; set; }
            public string SqlServerName { get; set; }
            public MTPlatformType MTType { get; set; }
            public string REPLICA { get; set; }
            public string Tables { get; set; }
        }

        public class TableSchema
        {
            public MTPlatformType MTType { get; set; }
            public string TableName { get; set; }
        }

    }
}
