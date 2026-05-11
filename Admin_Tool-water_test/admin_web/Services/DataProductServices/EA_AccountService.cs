using Microsoft.VisualBasic.FileIO;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace admin_web.Services.DataProductService
{
    public static class Global
    {
        public static Dictionary<string, EA_AccountService.SqlRecord> server_dict;
    }

    public class EA_AccountService
    {
        private static readonly string connectionString = UniversalService.sql_connectionString;

        // 給前端看得Server表
        public static List<string> Get_ServerList()
        {
            var server_list = new List<string>();

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT SQL_NAME
                    FROM server_health.server_list
                    WHERE 1 = 1
	                    AND MT IS NOT NULL
                        AND SQL_NAME NOT REGEXP 'TEST'
                        AND SERVER_NAME IS NOT NULL
                    ORDER BY SQL_NAME";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            server_list.Add(reader["SQL_NAME"].ToString());
                        }
                    }
                }
            }
            return server_list;
        }

        public class SqlRecord
        {
            public string SQL_HOST { get; set; }
            public string SQL_USER { get; set; }
            public string SQL_PASSWORD { get; set; }
            public string SQL_PORT { get; set; }
        }

        // 撈取所有Server Config
        private static Dictionary<string, SqlRecord> GetAllServerIP()
        {
            var server_dict = new Dictionary<string, SqlRecord>();

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT *
                    FROM server_health.server_list sl
                    LEFT JOIN(
                     SELECT *
                        FROM server_health.sql_connect
                    ) sc on sl.REPLICA = sc.SERVER
                    WHERE 1 = 1
                        AND sl.SQL_NAME != ''
                        AND sl.SERVER_NAME != ''
                        AND sl.PROXY != ''";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string serverName = reader["SQL_NAME"].ToString();

                            var record = new SqlRecord
                            {
                                //MT = reader["MT"].ToString(),
                                //SERVER_PROXY = reader["PROXY"].ToString(),
                                //SERVER_DC = reader["DC"].ToString(),
                                //SQL_NAME = reader["SQL_NAME"].ToString(),
                                SQL_HOST = reader["HOST"].ToString(),
                                SQL_USER = reader["USER"].ToString(),
                                SQL_PASSWORD = reader["PASSWORD"].ToString(),
                                SQL_PORT = reader["PORT"].ToString()
                            };
                            server_dict[serverName] = record;
                        }
                    }
                }
            }

            return server_dict;
        }

        public static Dictionary<string, DataTable> EA(List<string> ServerList, string StartTime, string EndTime)
        {
            List<string> mt4_server = new(ServerList);
            mt4_server.RemoveAll(item => item.ToLower().Contains("mt5"));
            List<string> mt5_server = new(ServerList);
            mt5_server.RemoveAll(item => !item.Contains("mt5"));

            Global.server_dict = GetAllServerIP();

            // 輸出用table
            // SELECT a.LOGIN,count(a.LOGIN) as count,b.GROUP as 'GROUP', '{schema}' as Server ,a.COMMENT
            DataTable sqldata1 = new();
            sqldata1.Columns.Add("LOGIN", typeof(int));
            sqldata1.Columns.Add("count", typeof(long));
            sqldata1.Columns.Add("GROUP", typeof(string));
            sqldata1.Columns.Add("Server", typeof(string));
            sqldata1.Columns.Add("COMMENT", typeof(string));
            // SELECT a.LOGIN,count(a.LOGIN) as count,b.GROUP as 'GROUP', '{schema}' as Server ,a.COMMENT 
            DataTable sqldata = new();
            sqldata.Columns.Add("LOGIN", typeof(int));
            sqldata.Columns.Add("count", typeof(long));
            sqldata.Columns.Add("GROUP", typeof(string));
            sqldata.Columns.Add("Server", typeof(string));
            sqldata.Columns.Add("COMMENT", typeof(string));

            // mt4
            if (mt4_server.Count > 0)
            {
                DataTable sqldata_mt4 = mt4data_sql(mt4_server, StartTime, EndTime);
                foreach (DataRow row in sqldata_mt4.Rows)
                {
                    sqldata1.ImportRow(row);
                }
                DataTable sqldata1_mt4 = mt4data1_sql(mt4_server, StartTime, EndTime);
                foreach (DataRow row in sqldata1_mt4.Rows)
                {
                    sqldata.ImportRow(row);
                }
            }

            // mt5
            if (mt5_server.Count > 0)
            {
                DataTable sqldata_mt5 = mt5data_sql(mt5_server, StartTime, EndTime);
                foreach (DataRow row in sqldata_mt5.Rows)
                {
                    sqldata1.ImportRow(row);
                }
                DataTable sqldata1_mt5 = mt5data1_sql(mt5_server, StartTime, EndTime);
                foreach (DataRow row in sqldata1_mt5.Rows)
                {
                    sqldata.ImportRow(row);
                }
            }

            Dictionary<string, DataTable> Result = new();

            var distinctRows = sqldata.AsEnumerable()
                            .GroupBy(row => new
                            {
                                LOGIN = row.Field<int>("LOGIN"),
                                GROUP = row.Field<string>("GROUP"),
                                Server = row.Field<string>("Server")
                            })
                            .Select(g => g.First())
                            .CopyToDataTable();

            sqldata = distinctRows;

            // All Data Top 20
            DataTable Top20;
            var Top20dData = sqldata.AsEnumerable()
                                        .OrderByDescending(row => row.Field<long>("count"))
                                        .Take(20);
            if (Top20dData.Any())
            {
                Top20 = Top20dData.CopyToDataTable();
                Top20.Columns.Remove("COMMENT");
                Top20.Columns.Add("COMMENT1", typeof(string));
                Top20.Columns.Add("COMMENT2", typeof(string));
                Top20.Columns.Add("COMMENT3", typeof(string));
                foreach (DataRow row in Top20.Rows)
                {
                    int login = Convert.ToInt32(row["LOGIN"]);
                    string server = row["Server"].ToString();

                    var temp = sqldata1.AsEnumerable()
                                        .Where(r => r.Field<int>("LOGIN") == login && r.Field<string>("Server") == server)
                                        .OrderByDescending(r => r.Field<long>("count"))
                                        .Take(3);

                    var comments = temp.Select(r => r.Field<string>("COMMENT")).ToArray();

                    row["COMMENT1"] = comments.Length > 0 ? comments[0] : "<NA>";
                    row["COMMENT2"] = comments.Length > 1 ? comments[1] : "<NA>";
                    row["COMMENT3"] = comments.Length > 2 ? comments[2] : "<NA>";
                }
            }
            else
            {
                Top20 = new DataTable();
                Top20.Columns.Add("LOGIN", typeof(int));
                Top20.Columns.Add("count", typeof(long));
                Top20.Columns.Add("GROUP", typeof(string));
                Top20.Columns.Add("Server", typeof(string));
                Top20.Columns.Add("COMMENT1", typeof(string));
                Top20.Columns.Add("COMMENT2", typeof(string));
                Top20.Columns.Add("COMMENT3", typeof(string));

            }
            Result.Add("$all", Top20);

            // Single Data Top10
            foreach (var item in ServerList)
            {
                DataTable Top10;
                var Top10dData = sqldata.AsEnumerable()
                                            .Where(r => r.Field<string>("Server") == item)
                                            .OrderByDescending(row => row.Field<long>("count"))
                                            .Take(10);

                if (Top10dData.Any())
                {
                    Top10 = Top10dData.CopyToDataTable();
                    Top10.Columns.Remove("COMMENT");
                    Top10.Columns.Add("COMMENT1", typeof(string));
                    Top10.Columns.Add("COMMENT2", typeof(string));
                    Top10.Columns.Add("COMMENT3", typeof(string));

                    foreach (DataRow row in Top10.Rows)
                    {
                        int login = Convert.ToInt32(row["LOGIN"]);
                        string server = row["Server"].ToString();

                        var temp = sqldata1.AsEnumerable()
                                            .Where(r => r.Field<int>("LOGIN") == login && r.Field<string>("Server") == server)
                                            .OrderByDescending(r => r.Field<long>("count"))
                                            .Take(3);

                        var comments = temp.Select(r => r.Field<string>("COMMENT")).ToArray();

                        row["COMMENT1"] = comments.Length > 0 ? comments[0] : "<NA>";
                        row["COMMENT2"] = comments.Length > 1 ? comments[1] : "<NA>";
                        row["COMMENT3"] = comments.Length > 2 ? comments[2] : "<NA>";
                    }
                }
                else
                {
                    Top10 = new DataTable();
                    Top10.Columns.Add("LOGIN", typeof(int));
                    Top10.Columns.Add("count", typeof(long));
                    Top10.Columns.Add("GROUP", typeof(string));
                    Top10.Columns.Add("Server", typeof(string));
                    Top10.Columns.Add("COMMENT1", typeof(string));
                    Top10.Columns.Add("COMMENT2", typeof(string));
                    Top10.Columns.Add("COMMENT3", typeof(string));
                }
                Result.Add($"${item}", Top10);
            }

            return Result;
        }

        public static DataTable mt4data_sql(List<string> server_list, string StartTime, string EndTime)
        {
            DataTable result = new();

            foreach (var schema in server_list)
            {
                DataTable data = new();

                string sqlcommand = $@"
                    SELECT a.LOGIN,count(a.LOGIN) as count,b.GROUP as 'GROUP', '{schema}' as Server ,a.COMMENT 
                    FROM {schema}.mt4_trades a, {schema}.mt4_users b
                    where 1 =1 
	                    AND a.REASON = 1
                        AND a.OPEN_TIME BETWEEN '{StartTime} 00:00:00' AND '{EndTime} 23:59:59' 
                    and a.LOGIN = b.LOGIN 
                    group by a.LOGIN, a.COMMENT
                ";

                var sql_config = Global.server_dict[schema];
                string sql_connect =
                    $"server={sql_config.SQL_HOST};" +
                    $"user={sql_config.SQL_USER};" +
                    $"password={sql_config.SQL_PASSWORD};" +
                    $"port={sql_config.SQL_PORT};" +
                    $"charset=utf8mb4;";

                using (MySqlConnection connection = new MySqlConnection(sql_connect))
                {
                    connection.Open();

                    using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                    {
                        using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                        {
                            data.Load(sqlreader);
                        }
                    }

                    result.BeginLoadData();
                    result.Merge(data);
                    result.AcceptChanges();
                    connection.Close();
                }
            }

            return result;
        }

        public static DataTable mt4data1_sql(List<string> server_list, string StartTime, string EndTime)
        {
            DataTable result = new();

            foreach (var schema in server_list)
            {
                DataTable data = new();

                string sqlcommand = $@"
                    SELECT a.LOGIN,count(a.LOGIN) as count,b.GROUP as 'GROUP', '{schema}' as Server ,a.COMMENT 
                    FROM {schema}.mt4_trades a, {schema}.mt4_users b
                    where 1 =1 
	                    AND a.REASON = 1
                        AND a.OPEN_TIME BETWEEN '{StartTime} 00:00:00' AND '{EndTime} 23:59:59' 
                    and a.LOGIN = b.LOGIN 
                    group by a.LOGIN 
                ";

                var sql_config = Global.server_dict[schema];
                string sql_connect =
                    $"server={sql_config.SQL_HOST};" +
                    $"user={sql_config.SQL_USER};" +
                    $"password={sql_config.SQL_PASSWORD};" +
                    $"port={sql_config.SQL_PORT};" +
                    $"charset=utf8mb4;";

                using (MySqlConnection connection = new MySqlConnection(sql_connect))
                {
                    connection.Open();

                    using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                    {
                        using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                        {
                            data.Load(sqlreader);
                        }
                    }
                    result.BeginLoadData();
                    result.Merge(data);
                    result.AcceptChanges();
                    connection.Close();
                }
            }

            return result;
        }

        public static DataTable mt5data_sql(List<string> ServerList, string StartTime, string EndTime)
        {
            DataTable result = new();

            foreach (var server in ServerList)
            {
                DataTable data = new();

                string sqlcommand = $@"
                    SELECT 
                        b1.LOGIN, b1.count, b2.GROUP AS 'GROUP', '{server}' AS Server, b1.COMMENT
                    FROM
                        (SELECT 
                            a1.login, a2.count, a1.Comment
                        FROM
                            (SELECT 
                            a.login, a.Comment
                        FROM
                            (SELECT 
                            *
                        FROM
                            {server}.mt5_deals
                        WHERE
                            1 = 1 AND Reason = '1' AND Entry = 1) AS a
                        WHERE
                            1 = 1
                                AND a.time BETWEEN '{StartTime} 00:00:00' AND '{EndTime} 23:59:59'
                        GROUP BY a.login , a.COMMENT) AS a1, (SELECT 
                            a.login, COUNT(a.login) AS count
                        FROM
                            (SELECT 
                            *
                        FROM
                            {server}.mt5_deals
                        WHERE
                            1 = 1 AND Reason = '1' AND Entry = 1) AS a
                        WHERE
                            1 = 1
                                AND a.time BETWEEN '{StartTime} 00:00:00' AND '{EndTime} 23:59:59'
                        GROUP BY a.login) AS a2
                        WHERE
                            1 = 1 AND a2.login = a1.login) AS b1,
                        (SELECT 
                            a.login, a.Group
                        FROM
                            {server}.mt5_users AS a) AS b2
                    WHERE
                        1 = 1 AND b1.login = b2.login
                ";

                var sql_config = Global.server_dict[server];
                string sql_connect =
                    $"server={sql_config.SQL_HOST};" +
                    $"user={sql_config.SQL_USER};" +
                    $"password={sql_config.SQL_PASSWORD};" +
                    $"port={sql_config.SQL_PORT};" +
                    $"charset=utf8mb4;";

                using (MySqlConnection connection = new MySqlConnection(sql_connect))
                {
                    connection.Open();

                    using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                    {
                        using (MySqlDataReader sqlreader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
                        {
                            data.Load(sqlreader);
                        }
                        result.BeginLoadData();
                        result.Merge(data);
                        result.AcceptChanges();
                        connection.Close();
                    }

                    connection.Close();
                }
            }

            return result;
        }

        public static DataTable mt5data1_sql(List<string> ServerList, string StartTime, string EndTime)
        {
            DataTable result = new();

            List<string> all_command = new();
            foreach (var server in ServerList)
            {
                DataTable data = new();

                string sqlcommand = $@"
                    SELECT 
                        b1.LOGIN,
                        b1.count,
                        b2.GROUP AS 'GROUP',
                        '{server}' AS Server,
                        b1.COMMENT
                    FROM
                        (SELECT 
                            a1.login, a2.count, a1.Comment
                        FROM
                            (SELECT 
                            a.login, a.Comment
                        FROM
                            (SELECT 
                            *
                        FROM
                            {server}.mt5_deals
                        WHERE
                            1 = 1 AND Reason = '1' AND Entry = 1) AS a
                        WHERE
                            1 = 1
                                AND a.time BETWEEN '{StartTime} 00:00:00' AND '{EndTime} 23:59:59'
                        GROUP BY a.login , a.COMMENT) AS a1, (SELECT 
                            a.login, COUNT(a.login) AS count
                        FROM
                            (SELECT 
                            *
                        FROM
                            {server}.mt5_deals
                        WHERE
                            1 = 1 AND Reason = '1' AND Entry = 1) AS a
                        WHERE
                            1 = 1
                                AND a.time BETWEEN '{StartTime} 00:00:00' AND '{EndTime} 23:59:59'
                        GROUP BY a.login) AS a2
                        WHERE
                            1 = 1 AND a2.login = a1.login) AS b1,
                        (SELECT 
                            a.login, a.Group
                        FROM
                            {server}.mt5_users AS a) AS b2
                    WHERE
                        1 = 1 AND b1.login = b2.login
                ";

                var sql_config = Global.server_dict[server];
                string sql_connect =
                    $"server={sql_config.SQL_HOST};" +
                    $"user={sql_config.SQL_USER};" +
                    $"password={sql_config.SQL_PASSWORD};" +
                    $"port={sql_config.SQL_PORT};" +
                    $"charset=utf8mb4;";

                using (MySqlConnection connection = new MySqlConnection(sql_connect))
                {
                    connection.Open();

                    using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                    {
                        using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                        {
                            data.Load(sqlreader);
                        }
                        result.BeginLoadData();
                        result.Merge(data);
                        result.AcceptChanges();
                        connection.Close();
                    }

                    connection.Close();
                }
            }

            return result;
        }
    }
}