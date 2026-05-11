using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace admin_web.Models
{
    public class QueryserverlistModel
    {

        [Table("DatabaseServerInfo")] // 合併後的表名
        public class DatabaseServerInfo
        {
            // ---------- Database Info ----------
            [Key]
            [StringLength(45)]
            public string SqlName { get; set; }

            [StringLength(45)]
            public string Brand { get; set; }

            [StringLength(45)]
            public string ServerName { get; set; }

            [StringLength(45)]
            public string Replica { get; set; }

            [StringLength(45)]
            public string Mt { get; set; }

            
            [StringLength(45)]
            public string ItRisk { get; set; }

            [StringLength(45)]
            public string Loading { get; set; }

            [StringLength(45)]
            public string Proxy { get; set; }

            [StringLength(45)]
            public string Dc { get; set; }


  
            [StringLength(45)]
            public string Loki { get; set; }

            public int? Usc { get; set; }

            [StringLength(45)]
            public string DlSourceServer { get; set; }

            // ---------- Server Info ----------
            [StringLength(45)]
            public string Server { get; set; }

            [StringLength(100)]
            public string Host { get; set; }

            [StringLength(45)]
            public string User { get; set; }

            [StringLength(100)]
            public string Password { get; set; }

            public int? Port { get; set; } // nullable 避免 NULL
        }

        public class ServerWithTables
        {
            public string SqlCode { get; set; }
            public DatabaseServerInfo ServerInfo { get; set; }
            public List<string> Tables { get; set; }
        }
    }

}
