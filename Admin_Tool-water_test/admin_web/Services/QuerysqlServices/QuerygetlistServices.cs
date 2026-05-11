using admin_web.Models;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace admin_web.Services.QuerysqlServices
{
    public class QuerygetlistServices
    {
        private readonly IConfiguration _configuration;

        public QuerygetlistServices(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        // 使用者初始獲取資料
        public async Task<List<QueryserverlistModel.DatabaseServerInfo>> GetAllServerList()
        {
            var result = new List<QueryserverlistModel.DatabaseServerInfo>();

            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new MySqlConnection(connStr);

            await connection.OpenAsync();

            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT 
                serlist.*,
                sqlcon.Server,
                sqlcon.HOST,
                sqlcon.USER,
                sqlcon.PASSWORD,
                sqlcon.PORT
                FROM server_health.server_list serlist
                LEFT JOIN server_health.sql_connect sqlcon
                ON serlist.REPLICA = sqlcon.SERVER
                WHERE 1=1  
                AND serlist.REPLICA IS NOT NULL
                AND serlist.SQL_NAME IS NOT NULL
                AND serlist.MT IS NOT NULL
                AND serlist.SERVER_NAME IS NOT NULL 
                AND serlist.DL_SOURCE_SERVER IS NOT NULL
            ";

            Debug.WriteLine("---------1-------");
            using var reader = await command.ExecuteReaderAsync();
            Debug.WriteLine("---------2-------");
            while (await reader.ReadAsync())
            {
                try
                {
                    var item = new QueryserverlistModel.DatabaseServerInfo();
                    item.SqlName = reader.GetString(reader.GetOrdinal("SQL_NAME"));
                    item.Brand = reader.IsDBNull(reader.GetOrdinal("BRAND")) ? null : reader.GetString(reader.GetOrdinal("BRAND"));
                    item.ServerName = reader.GetString(reader.GetOrdinal("SERVER_NAME"));
                    item.Replica = reader.GetString(reader.GetOrdinal("REPLICA"));
                    item.Mt = reader.GetString(reader.GetOrdinal("MT"));
                    item.ItRisk = reader.IsDBNull(reader.GetOrdinal("IT_RISK")) ? null : reader.GetString(reader.GetOrdinal("IT_RISK"));
                    item.Loading = reader.IsDBNull(reader.GetOrdinal("LOADING")) ? null : reader.GetString(reader.GetOrdinal("LOADING"));
                    item.Proxy = reader.IsDBNull(reader.GetOrdinal("PROXY")) ? null : reader.GetString(reader.GetOrdinal("PROXY"));
                    item.Dc = reader.IsDBNull(reader.GetOrdinal("DC")) ? null : reader.GetString(reader.GetOrdinal("DC"));
                    item.Loki = reader.IsDBNull(reader.GetOrdinal("LOKI")) ? null : reader.GetString(reader.GetOrdinal("LOKI"));
                    item.Usc = reader.IsDBNull(reader.GetOrdinal("USC")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("USC"));
                    item.DlSourceServer = reader.IsDBNull(reader.GetOrdinal("DL_SOURCE_SERVER")) ? null : reader.GetString(reader.GetOrdinal("DL_SOURCE_SERVER"));
                    item.Server = reader.IsDBNull(reader.GetOrdinal("SERVER")) ? null : reader.GetString(reader.GetOrdinal("SERVER"));
                    item.Host = reader.IsDBNull(reader.GetOrdinal("HOST")) ? null : reader.GetString(reader.GetOrdinal("HOST"));
                    item.User = reader.IsDBNull(reader.GetOrdinal("USER")) ? null : reader.GetString(reader.GetOrdinal("USER"));
                    item.Password = reader.IsDBNull(reader.GetOrdinal("PASSWORD")) ? null : reader.GetString(reader.GetOrdinal("PASSWORD"));
                    item.Port = reader.IsDBNull(reader.GetOrdinal("PORT")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("PORT"));
                    result.Add(item);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"讀取資料時發生錯誤，REPLICA={reader["REPLICA"]}", ex);
                    Debug.WriteLine(ex.ToString());
                    throw new Exception(
                        $"讀取資料時發生錯誤，REPLICA={reader["REPLICA"]}", ex);
                }
            }
            return result;
        }

    }
}
