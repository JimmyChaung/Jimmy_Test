using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;


namespace admin_web.Infrastructure
{
    public class DatabricksConnection
    {

        private readonly string _connectionString;

        public DatabricksConnection(string host, string httpPath, string token)
        {
            _connectionString =
                $"Server={host};" +
                $"Port=443;" +
                $"HTTPPath={httpPath};" +
                $"SSL=1;" +
                $"ThriftTransport=2;" +  // Databricks SQL warehouse 必填
                $"AuthMech=3;" +
                $"UID=token;" +
                $"PWD={token};";

            Debug.WriteLine("Databricks Connection = " + _connectionString);
        }


    }
}
