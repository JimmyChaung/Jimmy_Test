using MySql.Data.MySqlClient;
using System.Collections.Generic;
using static admin_web.Models.ServerModel;

namespace admin_web.Services
{
    public class UniversalService
    {
        public static readonly string sql_connectionString =
            $"server=192.168.1.37;" +
            $"user=tpadmin;" +
            $"password=Vs47D2RLwMLAL3VvoDtuwSs9;" +
            $"port=3306;" +
            $"charset=utf8;";

        // 給前端看得Server表
        public static List<ServerRecord> Get_All_Server_Config()
        {
            var server_list = new List<ServerRecord>();
            using (MySqlConnection connection = new MySqlConnection(sql_connectionString))
            {
                connection.Open();

                string query = @$"
                    SELECT sl.*, sc.*
                    FROM server_health.server_list sl
                    LEFT JOIN server_health.sql_connect sc
                        on sl.REPLICA = sc.SERVER
                    WHERE 1 = 1 
                        AND sl.SQL_NAME != ''
                        AND sl.SERVER_NAME != ''
                        AND sl.PROXY != ''
                    ORDER BY sl.SERVER_NAME";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var record = new ServerRecord
                            {
                                SERVER_NAME = reader["SERVER_NAME"].ToString(),
                                SQL_NAME = reader["SQL_NAME"].ToString(),
                                SERVER_PROXY = reader["PROXY"].ToString(),
                                SERVER_DC = reader["DC"].ToString(),
                                MT = reader["MT"].ToString(),
                            };

                            server_list.Add(record);
                        }
                    }
                }
            }
            return server_list;
        }

        // 取得工具名稱
        public static string Get_Tool_Name(string toolID)
        {
            using (MySqlConnection connection = new MySqlConnection(sql_connectionString))
            {
                connection.Open();

                string query = @$"
                    SELECT * 
                    FROM admin_tool.tool_info
                    WHERE ToolID = '{toolID}'";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader["ToolName"].ToString();
                        }
                        else
                        {
                            return "Invalid Tool Name";
                        }
                    }
                }
            }
        }

        // 撈取所有Server Config
        public static Dictionary<string, ServerRecord> GetAllServerIP(int mt = 0)
        {
            string mt_set = mt == 0 ? "" : $"AND sl.MT = 'mt{mt}'";
            Dictionary<string, ServerRecord> dict = new();
            using (MySqlConnection connection = new MySqlConnection(sql_connectionString))
            {
                connection.Open();

                string query = @$"
                    SELECT *
                    FROM server_health.server_list sl
                    LEFT JOIN(
                     SELECT *
                        FROM server_health.sql_connect
                    ) sc on sl.REPLICA = sc.SERVER
                    WHERE 1 = 1
                        AND sl.SQL_NAME != ''
                        AND sl.SERVER_NAME != ''
                        AND sl.PROXY != ''
                        {mt_set}";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string serverName = reader["SERVER_NAME"].ToString();

                            var record = new ServerRecord
                            {
                                MT = reader["MT"].ToString(),
                                SERVER_PROXY = reader["PROXY"].ToString(),
                                SERVER_DC = reader["DC"].ToString(),
                                SQL_NAME = reader["SQL_NAME"].ToString(),
                                SQL_HOST = reader["HOST"].ToString(),
                                SQL_USER = reader["USER"].ToString(),
                                SQL_PASSWORD = reader["PASSWORD"].ToString(),
                                SQL_PORT = reader["PORT"].ToString()
                            };
                            dict[serverName] = record;
                        }
                    }
                }
            }
            return dict;
        }
    }
}
