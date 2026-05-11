//using admin_web.Services.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Data;
using MySql.Data.MySqlClient;
using System;
using admin_web.Models.DataProduct;
using System.Linq;
using System.Diagnostics;

namespace admin_web.Services.DataProductService
{
    public class ServerHealth_Service
    {
        private static readonly string connectFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "config", "sql_connect.csv");
        private static readonly string connectionString = GetConnectionStringFromCsv(connectFilePath);

        public static List<ServerList> ExecuteSQL_serverlist()
        {
            List<ServerList> data = new();

            string sqlcommand = $@"SELECT BRAND as 'Brand',SERVER_NAME as 'Server' FROM server_health.server_list where 1=1 and Brand IS NOT NULL";
            
            //讀取連線資訊
            string connect_filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "config", "sql_connect.csv");
            string connectionString = GetConnectionStringFromCsv(connect_filePath);

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                while (sqlreader.Read())
                                {
                                    data.Add(new ServerList
                                    {
                                        Brand = sqlreader["Brand"].ToString(),
                                        Server = sqlreader["Server"].ToString()
                                    });
                                }
                            }
                        }

                    }
                    break;
                }
                catch (Exception )
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                }
                //connection.Close();
            }
            return data;
        }


        public static DataTable ExecuteSQL_position(string server, string time, string endTime, string delta, string excludeGroup = "", string excludeLogin = "")
        {
            DataTable data = new();

            // 根據使用者的輸入
            string groupFilter = string.IsNullOrEmpty(excludeGroup) ? "" : $"AND `GROUP` NOT REGEXP '{excludeGroup}'";
            // 檢查 excludeLogin 是否有值
            string loginFilter = string.IsNullOrEmpty(excludeLogin)
                ? ""
                : $"AND LOGIN NOT IN ({string.Join(",", excludeLogin.Split(',').Select(login => $"'{login.Trim()}'"))})";


            string sqlcommand = $@"SELECT  p.SERVER_NAME as 'Server',p.LOGIN as 'Login',P.GROUP as 'Group',
                            p.POSITION as 'Position',p.Balance as 'Balance',P.INPUT_TIME as 'Input Time',DATE_SUB(INPUT_TIME,INTERVAL {delta} HOUR) as 'Server Time'
                            FROM server_health.position p
                            where 1=1
                            and INPUT_TIME >= DATE_ADD('{time}',INTERVAL {delta} HOUR)
                            and INPUT_TIME <= DATE_ADD('{endTime}',INTERVAL {delta} HOUR)
                            and SERVER_NAME = '{server}'
                            {groupFilter} 
                            {loginFilter}
                            order by position desc
            ";
            //讀取連線資訊
            //string connect_filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "config", "sql_connect.csv");
            //string connectionString = GetConnectionStringFromCsv(connect_filePath);

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception )
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_addorder(string server, string time, string endTime, string delta, string excludeGroup = "", string excludeLogin = "")
        {
            DataTable data = new();
            // 根據使用者的輸入
            string groupFilter = string.IsNullOrEmpty(excludeGroup) ? "" : $"AND `GROUP` NOT REGEXP '{excludeGroup}'";
            // 檢查 excludeLogin 是否有值
            string loginFilter = string.IsNullOrEmpty(excludeLogin)
                ? ""
                : $"AND LOGIN NOT IN ({string.Join(",", excludeLogin.Split(',').Select(login => $"'{login.Trim()}'"))})";

            string sqlcommand = $@"SELECT SERVER_NAME as 'Server',LOGIN as 'Login',a.GROUP as 'Group',a.OLDHISTORY as 'Last Half Hour Order',NEWHISTORY as 'This Half Hour Order',
                            ADDHISTORY as 'Increase',a.BALANCE as 'Balance',INPUT_TIME as 'Input Time',DATE_SUB(INPUT_TIME,INTERVAL {delta} HOUR) as 'Server Time'
                            FROM server_health.order_add a
                            where 1=1
                            and INPUT_TIME >= DATE_ADD('{time}',INTERVAL {delta} HOUR)
                            and INPUT_TIME <= DATE_ADD('{endTime}',INTERVAL {delta} HOUR)
                            and SERVER_NAME = '{server}'
                            {groupFilter} 
                            {loginFilter}
                           ";


            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception )
                {
                    // 例外狀況

                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_socialtrading(string server, string time, string endTime, string delta, string excludeLogin = "")
        {
            DataTable data = new();
            // 檢查 excludeLogin 是否有值
            string loginFilter = string.IsNullOrEmpty(excludeLogin)
                ? ""
                : $"AND LOGIN NOT IN ({string.Join(",", excludeLogin.Split(',').Select(login => $"'{login.Trim()}'"))})";

            string sqlcommand = $@"SELECT SERVER_NAME as 'Server',LOGIN as 'Provider Login',ADD_ORDER as 'Provider Order', F_MATH as 'Follower',P_BALANCE as 'P_Balance',F_BALANCE as 'F_Balance',INPUT_TIME as 'Input Time',DATE_SUB(INPUT_TIME,INTERVAL {delta} HOUR) as 'Server Time'
                            FROM server_health.soical_trading_order a
                            where 1=1
                            and INPUT_TIME >=  DATE_ADD('{time}',INTERVAL {delta} HOUR)
                            and INPUT_TIME <= DATE_ADD('{endTime}',INTERVAL {delta} HOUR)
                            and SERVER_NAME = '{server}'
                            {loginFilter}
                           ";

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();   
            }
            return data;
        }

        

        public static DataTable ExecuteSQL_hft(string server, string time, string endTime, string mode, string delta, string excludeGroup = "", string excludeLogin = "")
        {
            DataTable data = new();
            // 根據使用者的輸入
            string groupFilter = string.IsNullOrEmpty(excludeGroup) ? "" : $"AND `GROUP` NOT REGEXP '{excludeGroup}'";
            // 檢查 excludeLogin 是否有值
            string loginFilter = string.IsNullOrEmpty(excludeLogin)
                ? ""
                : $"AND LOGIN NOT IN ({string.Join(",", excludeLogin.Split(',').Select(login => $"'{login.Trim()}'"))})";

            string typeCondition = mode == "A" ? "TYPE != 'total'" : "TYPE = 'total'";
            string requestPerMinColumn = mode == "B" ? "REQUEST_PER_MIN as 'Max Per Min'," : "";

            string sqlcommand = $@"
                                SELECT SERVER_NAME as 'Server',
                                       LOGIN as 'Login',
                                       t.group as 'Group',
                                       TYPE as 'Type',
                                       COUNT as 'Request Amount',
                                       {requestPerMinColumn}  
                                       t.BALANCE as 'Balance',                                        
                                       INPUT_TIME as 'Input Time',
                                       DATE_SUB(INPUT_TIME, INTERVAL {delta} HOUR) as 'Server Time'                                      
                                FROM server_health.hft t
                                WHERE 1=1
                                  AND INPUT_TIME >= DATE_ADD('{time}', INTERVAL {delta} HOUR)
                                  AND INPUT_TIME <= DATE_ADD('{endTime}', INTERVAL {delta} HOUR)
                                  AND SERVER_NAME = '{server}'
                                  AND {typeCondition}
                                 {groupFilter} 
                                 {loginFilter}
                                ORDER BY COUNT DESC
                                ";


            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception )
                {
                    // 例外狀況
                    
                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_archive(string server, string time, string endTime, string delta, string excludeGroup = "", string excludeLogin = "")
        {
            DataTable data = new();
            // 根據使用者的輸入
            string groupFilter = string.IsNullOrEmpty(excludeGroup) ? "" : $"AND `GROUP` NOT REGEXP '{excludeGroup}'";
            // 檢查 excludeLogin 是否有值
            string loginFilter = string.IsNullOrEmpty(excludeLogin)
                ? ""
                : $"AND LOGIN NOT IN ({string.Join(",", excludeLogin.Split(',').Select(login => $"'{login.Trim()}'"))})";

            string sqlcommand = $@"SELECT SERVER_NAME as 'Server',LOGIN as 'Login',t.GROUP as 'Group',t.TYPE as 'Type',COUNT_TICKET as 'History Order',BALANCE as 'Balance',
                            DATE_FORMAT(DATE(DATE_SUB(INPUT_TIME,INTERVAL {delta} HOUR)),'%Y-%m-%d') as 'Update Date'
                            FROM server_health.archive_pending t
                            where 1=1
                            and INPUT_TIME >= '{time}'
                            and INPUT_TIME <= '{endTime}'
                            and SERVER_NAME = '{server}'
                            {groupFilter} 
                            {loginFilter}
                           ";


            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception )
                {
                    // 例外狀況，錯誤訊息
                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_groupticket(string server, string time, string endTime, string delta, string excludeGroup = "")
        {
            DataTable data = new();

            string groupFilter = string.IsNullOrEmpty(excludeGroup) ? "" : $"AND `GROUP` NOT REGEXP '{excludeGroup}'";

            string sqlcommand = $@"SELECT SERVER_NAME as 'Server',t.group as 'Group',t.COUNT_TICKET as 'History Order',
                            DATE_FORMAT(DATE(DATE_SUB(INPUT_TIME,INTERVAL {delta} HOUR)),'%Y-%m-%d') as 'Update Date'
                            FROM server_health.group_ticket t
                            where 1=1
                            and INPUT_TIME >= '{time}' 
                            and INPUT_TIME <= '{endTime}' 
                            and SERVER_NAME = '{server}'
                            {groupFilter} 
                           ";


            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception )
                {
                    // 例外狀況

                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return data;
        }


        public static DataTable ExecuteSQL_position_realtime(string server, string excludeGroup = "", string excludeLogin = "")
        {
            DataTable data = new();
            Debug.WriteLine(server);
            ServerInfo serverInfo = GetServerGroup(server);
            string sgGroup = serverInfo.SGGroup;
            string sqlName = serverInfo.SQLName;
            string serverName = serverInfo.ServerName;
            string connectionString_sg = GetConnectionInfo(sgGroup);
            //string connectionString_sg2 = $"Server={connectionString_sg.Host};User ID={connectionString_sg.User};Password={connectionString_sg.Password};Port={connectionString_sg.Port};";

            Debug.Write(serverInfo);
            Debug.Write(sqlName);

            string sqlcommand;

            // 根據使用者的輸入
            string groupFilter = string.IsNullOrEmpty(excludeGroup) ? "" : $"AND `GROUP` NOT REGEXP '{excludeGroup}'";
            // 檢查 excludeLogin 是否有值
            string loginFilter = string.IsNullOrEmpty(excludeLogin)
                ? ""
                : $"AND u.LOGIN NOT IN ({string.Join(",", excludeLogin.Split(',').Select(login => $"'{login.Trim()}'"))})";


            if (!sqlName.Contains("mt5"))
            {
                sqlcommand = $@"SELECT 
                            '{serverName}' as 'Server',
                            u.login as 'Login',
                            u.group as 'Group',
                            count(ticket) as 'Position',
                            u.balance as 'Balance'
                            FROM {sqlName}.mt4_trades t, {sqlName}.mt4_users u
                            where 1=1
                            and t.login = u.login
                            and close_time = '1970-01-01 00:00:00'
                            and cmd < 2
                            {groupFilter} 
                            {loginFilter}
                            group by t.login
                            ORDER BY position DESC
                            LIMIT 20;
                           ";
            }
            else
            {
                sqlcommand = $@"SELECT 
                            '{serverName}' as 'Server',
                            t.login as 'Login',
                            u.group as 'Group',
                            count(Position_ID) as 'Position',
                            u.balance as 'Balance'
                            FROM {sqlName}.mt5_positions t, {sqlName}.mt5_users u
                            where 1 = 1
                            and t.login = u.login
                            {groupFilter} 
                            {loginFilter}
                            group by t.login
                            ORDER BY POSITION DESC
                            LIMIT 20;
                            ";
            }

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString_sg))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception )
                {
                    // 處理例外狀況，例如記錄日誌或顯示錯誤訊息
                    //Console.WriteLine($"連接失敗，重新嘗試中: {ex.Message}");
                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_loading(string server, string time,string delta)
        {
            DataTable data = new();


            string sqlcommand = $@"SELECT t.*,DATE_SUB(t.INPUT_TIME,INTERVAL {delta} HOUR) as 'SERVER_TIME' 
                            FROM server_health.loading t
                            WHERE 1=1
                            and t.SERVER_NAME = '{server}'
                            and ( t.INPUT_TIME between DATE_SUB( DATE_ADD('{time}', INTERVAL {delta} HOUR) , INTERVAL 24 HOUR)
                            AND DATE_ADD(DATE_ADD('{time}', INTERVAL {delta} HOUR), INTERVAL 24 HOUR) )
                           ";


            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception )
                {
                    // 例外狀況

                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_configuration(string server)
        {
            DataTable data = new();


            string sqlcommand = $@"SELECT SERVER_NAME,if(REGION='NY',1,0) AS 'NY',if(REGION='LD',1,0) AS 'LD',if(SOCIAL_TRADING='Pelican',1,0) AS 'Pelican',if(SOCIAL_TRADING='Brokeree',1,0) AS 'Brokeree',
                            MTS,PAMM ,MAM  ,BASIC_ACCOUNT ,USC_ACCOUNT ,SPECIAL_GROUP,SPECIAL_CURRENCY 
                            FROM server_health.server_configuration
                            WHERE 1=1
                            and SERVER_NAME = '{server}'
                           ";


            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_analysis(string server,string time)
        {
            DataTable data = new();


            string sqlcommand = $@"SELECT * FROM server_health.server_analysis
                            WHERE 1=1
                            and SERVER_NAME = '{server}'
                            and INPUT_TIME = '{time}'
                           ";


            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return data;
        }

        public static Indicators ExecuteSQL_analysis2(string server , string time)
        {
            Indicators indicators = new Indicators();


            string sqlcommand = $@"SELECT POSITION,TOTAL_USERS,PENDING,SYMBOL,`GROUP`,ARCHIVE_DELETE,ARCHIVE_INACTIVITY
                            FROM server_health.server_analysis
                            WHERE 1=1
                            and SERVER_NAME = '{server}'
                            and INPUT_TIME regexp DATE('{time}')
                           ";


            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (var sqlreader = cmd.ExecuteReader())
                            {
                                if(sqlreader.Read())
                                {
                                    indicators.POSITION = sqlreader["POSITION"].ToString();
                                    indicators.TOTAL_USERS = Convert.ToInt32(sqlreader["TOTAL_USERS"]);
                                    indicators.PENDING = Convert.ToInt32(sqlreader["PENDING"]);
                                    indicators.SYMBOL = Convert.ToInt32(sqlreader["SYMBOL"]);
                                    indicators.GROUP = Convert.ToInt32(sqlreader["GROUP"]);
                                    indicators.ARCHIVE_DELETE = sqlreader["ARCHIVE_DELETE"] != DBNull.Value ? Convert.ToInt32(sqlreader["ARCHIVE_DELETE"]) : 0;
                                    indicators.ARCHIVE_INACTIVITY = sqlreader["ARCHIVE_INACTIVITY"] != DBNull.Value ? Convert.ToInt32(sqlreader["ARCHIVE_INACTIVITY"]) : 0;
                                };
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return indicators;
        }

        private static string GetConnectionStringFromCsv(string filePath)
        {
            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    // 讀取 CSV 第一行作為標題
                    reader.ReadLine();
                    // 讀取第二行內容
                    string line = reader.ReadLine();
                    if (line != null)
                    {
                        var values = line.Split(',');
                        string server = values[0];
                        string user = values[1];
                        string password = values[2];

                        // 使用 CSV 中的資料構建連接字串
                        return $"server={server};user={user};password={password};charset=utf8;";
                    }
                    else
                    {
                        throw new Exception("CSV 文件內容不正確");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"取得資料失敗: {ex.Message}");
                throw;
            }

        }


        public static ServerInfo GetServerGroup(string server)
        {
            ServerInfo serverInfo = new ServerInfo();

            // 根據 server 從資料庫查詢 SGGroup 和 SQL_NAME
            string sqlcommand = $@"SELECT REPLICA, SQL_NAME, SERVER_NAME
                           FROM server_health.server_list 
                           WHERE SERVER_NAME = '{server}'";

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();
                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    // 設定 serverInfo 的屬性
                                    serverInfo.SGGroup = reader["REPLICA"].ToString();
                                    serverInfo.SQLName = reader["SQL_NAME"].ToString();
                                    serverInfo.ServerName = reader["SERVER_NAME"].ToString();
                                }
                            }
                        }
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"取得資料失敗: {ex.Message}");
                    System.Threading.Thread.Sleep(1000);
                }
            }
            
            return serverInfo; // 返回 serverInfo 物件
        }



        public static string GetConnectionInfo(string server)
        {
            string connectionInfo = null;

            // 查詢對應的連線資訊
            string query = $@"SELECT host, user, password,port FROM server_health.sql_connect 
                         WHERE server = '{server}'";

            // 資料庫連線邏輯

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string host = reader["host"].ToString();
                            string user = reader["user"].ToString();
                            string password = reader["password"].ToString();
                            string port = reader["port"].ToString();

                            connectionInfo = $"server={host};user={user};password={password};port={port};charset=utf8;";

                        }
                    }
                }
            }

            return connectionInfo;
        }


        public static TimeDelta GetTimeDelta(string time)
        {

            TimeDelta timedelta = new TimeDelta();

            // 根據 server 從資料庫查詢 SGGroup 和 SQL_NAME
            string sqlcommand = $@"SELECT
                DAYLIGHT,
                CASE
                    WHEN DAYLIGHT < '{time}' THEN AFTER_TIMEDELTA
                    ELSE BEFORE_TIMEDELTA
                END AS TIMEDELTA
                FROM server_health.daylight_saving ";

            //while (true)
            //{
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();
                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    // 設定  的屬性
                                    timedelta.DAYLIGHT = reader["DAYLIGHT"].ToString();
                                    timedelta.TIMEDELTA = reader["TIMEDELTA"].ToString(); //reader["TIMEDELTA"].ToString();
                                }
                            }
                        }
                    }
                    //break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"取得資料失敗: {ex.Message}");
                    System.Threading.Thread.Sleep(1000);
                }
            //}

            return timedelta;
        }


        public static string Half_adjust_time(string inputTime)
        {
            //轉為往下距離最近的30分or整點 如9:59分 return 9:30 , 9:01分return 9:00
            // 先將輸入的字串轉換為 DateTime
            if (DateTime.TryParse(inputTime, out DateTime parsedTime))
            {
                int minutes = parsedTime.Minute;
                int seconds = parsedTime.Second;

                DateTime adjustedTime;

                // 
                if (minutes >= 30)
                {
                    adjustedTime = new DateTime(parsedTime.Year, parsedTime.Month, parsedTime.Day, parsedTime.Hour, 30, 0);
                }
                // 
                else
                {
                    adjustedTime = new DateTime(parsedTime.Year, parsedTime.Month, parsedTime.Day, parsedTime.Hour, 0, 0);
                }


                // 將 DateTime 轉換回字串格式，並返回
                return adjustedTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                // 
                return string.Empty;
            }
        }

        public static string ten_adjust_time(string inputTime)
        {
            // 將輸入的字串轉換為 DateTime
            if (DateTime.TryParse(inputTime, out DateTime parsedTime))
            {
                // 取得最近的 10 分鐘時間點
                int minutes = parsedTime.Minute;
                int adjustedMinutes = (minutes / 10) * 10; 

                DateTime nearestTenMinute = new DateTime(parsedTime.Year, parsedTime.Month, parsedTime.Day, parsedTime.Hour, adjustedMinutes, 0);

                // 再減去 10 分鐘
                DateTime finalTime = nearestTenMinute.AddMinutes(-10);

                // 轉換回字串格式
                return finalTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                // 如果無法，返回空
                return string.Empty;
            }
        }

        public static string eod_adjust_time(string inputTime, string delta)
        {
            // 字串轉換為 DateTime
            if (DateTime.TryParse(inputTime, out DateTime parsedTime))
            {

                int deltaHour = int.TryParse(delta, out int result) ? result : 5;
                // 判斷當前時間是否在當天的 05:00:00 之前
                DateTime currentDayFiveAM = new DateTime(parsedTime.Year, parsedTime.Month, parsedTime.Day, deltaHour, 0, 0);

                if (parsedTime < currentDayFiveAM)
                {
                    // 如果時間小於當天的 05:00:00，回傳前一天的 05:00:00
                    DateTime previousDayFiveAM = currentDayFiveAM.AddDays(-1);
                    return previousDayFiveAM.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    // 回傳當天的 05:00:00
                    return currentDayFiveAM.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
            else
            {
                // 
                return string.Empty;
            }
        }
        public static string monday_adjust_time(string inputTime, string delta)
        {
            // 轉換為 DateTime
            if (DateTime.TryParse(inputTime, out DateTime parsedTime))
            {
                // 默認小時為 5，如果 delta 無法解析，則默認為 5
                int deltaHour = int.TryParse(delta, out int result) ? result : 5;

                // 計算當週的星期一
                int daysToMonday = (int)parsedTime.DayOfWeek - (int)DayOfWeek.Monday;

                // 如果是星期日，需要特殊處理
                if (daysToMonday < 0)
                {
                    daysToMonday += 7;
                }

                // 找到當週的星期一
                DateTime currentMonday = parsedTime.AddDays(-daysToMonday).Date;

                // 當週星期一的 deltaHour 時間
                DateTime currentMondayAtDeltaHour = new DateTime(currentMonday.Year, currentMonday.Month, currentMonday.Day, deltaHour, 0, 0);

                // 如果輸入時間早於當週的星期一 05:00，則返回上一週的星期一
                if (parsedTime < currentMondayAtDeltaHour)
                {
                    DateTime lastMonday = currentMonday.AddDays(-7);
                    return new DateTime(lastMonday.Year, lastMonday.Month, lastMonday.Day, deltaHour, 0, 0)
                        .ToString("yyyy-MM-dd HH:mm:ss");
                }

                // 否則返回當週的星期一
                return currentMondayAtDeltaHour.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                // 如果無法解析，返回空字串
                return string.Empty;
            }
        }



        public static List<ServerList> ExecuteSQL_serverlist_mt4()
        {
            List<ServerList> data = new();

            string sqlcommand = $@"SELECT BRAND as 'Brand',SERVER_NAME as 'Server' FROM server_health.server_list where 1=1 and Brand IS NOT NULL and MT = 'mt4' and SERVER_NAME not regexp 'test|demo' order by brand";

            //讀取連線資訊
            string connect_filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "config", "sql_connect.csv");
            string connectionString = GetConnectionStringFromCsv(connect_filePath);

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                while (sqlreader.Read())
                                {
                                    data.Add(new ServerList
                                    {
                                        Brand = sqlreader["Brand"].ToString(),
                                        Server = sqlreader["Server"].ToString()
                                    });
                                }
                            }
                        }

                    }
                    break;
                }
                catch (Exception)
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                }
                //connection.Close();
            }
            return data;
        }

        //-------------自動伺服器檢查
        public static DataTable ExecuteSQL_log_modify(string server, string startTime, string endTime)
        {
            DataTable data = new();

            string sqlcommand = $@"SELECT LOGIN,sum(COUNT) as 'COUNT' ,  DATE_FORMAT(MT_INPUT_TIME, '%Y-%m-%d %H:00:00')  as 'TIME'
                            FROM server_health.request_modify
                            where 1=1
                            and MT_INPUT_TIME >= '{startTime}'
                            and MT_INPUT_TIME < '{endTime}'
                            and SERVER_NAME = '{server}'
                            GROUP BY LOGIN ,TIME
                            order by COUNT desc
            ";

            Debug.Write(sqlcommand);
            //讀取連線資訊
            //string connect_filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "config", "sql_connect.csv");
            //string connectionString = GetConnectionStringFromCsv(connect_filePath);

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                    Debug.Write("???");
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_log_close(string server, string startTime, string endTime)
        {
            DataTable data = new();

            string sqlcommand = $@"SELECT LOGIN,sum(COUNT) as 'COUNT' ,  DATE_FORMAT(MT_INPUT_TIME, '%Y-%m-%d %H:00:00')  as 'TIME'
                            FROM server_health.request_close
                            where 1=1
                            and MT_INPUT_TIME >= '{startTime}'
                            and MT_INPUT_TIME < '{endTime}'
                            and SERVER_NAME = '{server}'
                            GROUP BY LOGIN ,TIME
                            order by COUNT desc
            ";

            Debug.Write(sqlcommand);
            //讀取連線資訊
            //string connect_filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "config", "sql_connect.csv");
            //string connectionString = GetConnectionStringFromCsv(connect_filePath);

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                    Debug.Write("???");
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_log_nomoney(string server, string startTime, string endTime)
        {
            DataTable data = new();

            string sqlcommand = $@"SELECT LOGIN,sum(t.COUNT) as 'COUNT'
                            FROM server_health.log_no_money t
                            where 1=1
                            and MT_INPUT_TIME >= '{startTime}'
                            and MT_INPUT_TIME < '{endTime}'
                            and SERVER_NAME = '{server}'
                            GROUP BY LOGIN 
                            order by COUNT desc
            ";

            Debug.Write(sqlcommand);
            //讀取連線資訊
            //string connect_filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "config", "sql_connect.csv");
            //string connectionString = GetConnectionStringFromCsv(connect_filePath);

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                    Debug.Write("???");
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_log_filter(string server, string startTime, string endTime)
        {
            DataTable data = new();

            string sqlcommand = $@"SELECT SYMBOL,sum(t.COUNT) as 'COUNT'
                            FROM server_health.log_filter t
                            where 1=1
                            and MT_INPUT_TIME >= '{startTime}'
                            and MT_INPUT_TIME < '{endTime}'
                            and SERVER_NAME = '{server}'
                            GROUP BY SYMBOL
                            order by COUNT desc
            ";

            Debug.Write(sqlcommand);
            //讀取連線資訊
            //string connect_filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "config", "sql_connect.csv");
            //string connectionString = GetConnectionStringFromCsv(connect_filePath);

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                    Debug.Write("???");
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_log_user(string server, string startTime, string endTime)
        {
            DataTable data = new();

            string sqlcommand = $@"SELECT LOGIN,sum(COUNT) as 'COUNT' ,  DATE_FORMAT(MT_INPUT_TIME, '%Y-%m-%d %H:00:00')  as 'TIME'
                            FROM server_health.log_unknown_user
                            where 1=1
                            and MT_INPUT_TIME >= '{startTime}'
                            and MT_INPUT_TIME < '{endTime}'
                            and SERVER_NAME = '{server}'
                            GROUP BY LOGIN ,TIME
                            order by COUNT desc
            ";

            Debug.Write(sqlcommand);
            //讀取連線資訊
            //string connect_filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "config", "sql_connect.csv");
            //string connectionString = GetConnectionStringFromCsv(connect_filePath);

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                    Debug.Write("???");
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_log_password(string server, string startTime, string endTime)
        {
            DataTable data = new();

            string sqlcommand = $@"SELECT LOGIN,sum(COUNT) as 'COUNT' ,  DATE_FORMAT(MT_INPUT_TIME, '%Y-%m-%d %H:00:00')  as 'TIME'
                            FROM server_health.log_invalid_password
                            where 1=1
                            and MT_INPUT_TIME >= '{startTime}'
                            and MT_INPUT_TIME < '{endTime}'
                            and SERVER_NAME = '{server}'
                            GROUP BY LOGIN ,TIME
                            order by COUNT desc
            ";

            Debug.Write(sqlcommand);
            //讀取連線資訊
            //string connect_filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "config", "sql_connect.csv");
            //string connectionString = GetConnectionStringFromCsv(connect_filePath);

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                    Debug.Write("???");
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_log_api(string server, string startTime, string endTime)
        {
            DataTable data = new();

            string sqlcommand = $@"SELECT LOGIN,sum(COUNT) as 'COUNT' ,  DATE_FORMAT(MT_INPUT_TIME, '%Y-%m-%d %H:00:00')  as 'TIME'
                            FROM server_health.log_man_api
                            where 1=1
                            and MT_INPUT_TIME >= '{startTime}'
                            and MT_INPUT_TIME < '{endTime}'
                            and SERVER_NAME = '{server}'
                            GROUP BY LOGIN ,TIME
                            order by COUNT desc
                            limit 5
            ";

            Debug.Write(sqlcommand);
            //讀取連線資訊
            //string connect_filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "config", "sql_connect.csv");
            //string connectionString = GetConnectionStringFromCsv(connect_filePath);

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                    Debug.Write("???");
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_position_int(string server)
        {
            DataTable data = new();
            Debug.WriteLine(server);
            ServerInfo serverInfo = GetServerGroup(server);
            string sgGroup = serverInfo.SGGroup;
            string sqlName = serverInfo.SQLName;
            string serverName = serverInfo.ServerName;
            string connectionString_sg = GetConnectionInfo(sgGroup);
            //string connectionString_sg2 = $"Server={connectionString_sg.Host};User ID={connectionString_sg.User};Password={connectionString_sg.Password};Port={connectionString_sg.Port};";

            Debug.Write(serverInfo);
            Debug.Write(sqlName);

            string sqlcommand = $@"SELECT 
                            count(ticket) as 'Position'
                            FROM {sqlName}.mt4_trades t
                            where 1=1
                            and close_time = '1970-01-01 00:00:00'
                            and cmd < 2
                           ";

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString_sg))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 處理例外狀況，例如記錄日誌或顯示錯誤訊息
                    //Console.WriteLine($"連接失敗，重新嘗試中: {ex.Message}");
                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_ticket_int(string server)
        {
            DataTable data = new();
            Debug.WriteLine(server);
            ServerInfo serverInfo = GetServerGroup(server);
            string sgGroup = serverInfo.SGGroup;
            string sqlName = serverInfo.SQLName;
            string serverName = serverInfo.ServerName;
            string connectionString_sg = GetConnectionInfo(sgGroup);
            //string connectionString_sg2 = $"Server={connectionString_sg.Host};User ID={connectionString_sg.User};Password={connectionString_sg.Password};Port={connectionString_sg.Port};";

            Debug.Write(serverInfo);
            Debug.Write(sqlName);

            string sqlcommand = $@"SELECT 
                            count(*) as 'Ticket'
                            FROM {sqlName}.mt4_trades t
                           ";

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString_sg))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 處理例外狀況，例如記錄日誌或顯示錯誤訊息
                    //Console.WriteLine($"連接失敗，重新嘗試中: {ex.Message}");
                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_log_modify_sec(string server, string startTime, string endTime)
        {
            DataTable data = new();

            string sqlcommand = $@"
                            WITH ranked AS (
                                SELECT
                                    LOGIN,
                                    REQUEST_PER_SEC,
                                    MT_INPUT_TIME AS `TIME`,
                                    ROW_NUMBER() OVER (
                                        PARTITION BY LOGIN
                                        ORDER BY REQUEST_PER_SEC DESC, MT_INPUT_TIME ASC
                                    ) AS rn
                                FROM server_health.request_modify_seconds
                                WHERE 1=1
                                  AND MT_INPUT_TIME >= '{startTime}'
                                  AND MT_INPUT_TIME < '{endTime}'
                                  AND SERVER_NAME = '{server}'
                            ),
                            order_summary AS (
                                SELECT
                                    LOGIN,
                                    SUM(`COUNT`) AS TOTAL_COUNTS
                                FROM server_health.request_modify
                                WHERE 1=1
                                  AND MT_INPUT_TIME >= '{startTime}'
                                  AND MT_INPUT_TIME < '{endTime}'
                                  AND SERVER_NAME = '{server}'
                                GROUP BY LOGIN
                            )
                            SELECT r.LOGIN, 
                                   r.REQUEST_PER_SEC, 
                                   r.`TIME`, 
                                   o.TOTAL_COUNTS
                            FROM ranked r
                            LEFT JOIN order_summary o ON r.LOGIN = o.LOGIN
                            WHERE r.rn = 1
                            ORDER BY r.REQUEST_PER_SEC DESC;

                            

            ";

            Debug.Write(sqlcommand);
            //讀取連線資訊
            //string connect_filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "ServerHealth", "config", "sql_connect.csv");
            //string connectionString = GetConnectionStringFromCsv(connect_filePath);

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 例外狀況
                    System.Threading.Thread.Sleep(1000);
                    Debug.Write("???");
                }

                //connection.Close();
            }
            return data;
        }

        public static DataTable ExecuteSQL_server_memory(string server, string startTime, string endTime)
        {
            DataTable data = new();
            Debug.WriteLine(server);
            ServerInfo serverInfo = Get_IT_ServerName(server);
            //string sgGroup = serverInfo.SGGroup;
            //string sqlName = serverInfo.SQLName;
            string serverName = serverInfo.ServerName;
            string connectionString_sg = GetConnectionInfo("performance");
            //string connectionString_sg2 = $"Server={connectionString_sg.Host};User ID={connectionString_sg.User};Password={connectionString_sg.Password};Port={connectionString_sg.Port};";

            //Debug.Write(serverInfo);
           // Debug.Write(sqlName);

            string sqlcommand = $@" SELECT ServerName,ServerTime,CEILING(FreeMemory/1024) AS `Freememory` 
                                    FROM risk_service.tb_risk_server_performace
                                    WHERE 1=1                                     
                                    and ServerTime >= '{startTime}'
                                    and ServerTime < '{endTime}'
                                    AND ServerName  = '{serverName}'                                   
                           ";

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString_sg))
                    {
                        connection.Open();

                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                            {
                                data.Load(sqlreader);
                            }
                        }

                    }
                    break;

                }
                catch (Exception)
                {
                    // 處理例外狀況，例如記錄日誌或顯示錯誤訊息
                    //Console.WriteLine($"連接失敗，重新嘗試中: {ex.Message}");
                    System.Threading.Thread.Sleep(1000);
                }

                //connection.Close();
            }
            return data;
        }

        public static ServerInfo Get_IT_ServerName(string server)
        {
            ServerInfo serverInfo = new ServerInfo();

            // 根據 server 從資料庫查詢 SGGroup 和 SQL_NAME
            string sqlcommand = $@"SELECT SQL_NAME, SERVER_NAME,LOADING
                           FROM server_health.server_list 
                           WHERE SERVER_NAME = '{server}'";

            while (true)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(connectionString))
                    {
                        connection.Open();
                        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                        {
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    // 設定 serverInfo 的屬性
                                    serverInfo.SGGroup = reader["SQL_NAME"].ToString();     // SQL名稱
                                    serverInfo.SQLName = reader["SERVER_NAME"].ToString();  //實際上是我們的主搜索用伺服器名稱
                                    serverInfo.ServerName = reader["LOADING"].ToString();   //實際上是在IT RC庫內的名稱
                                }
                            }
                        }
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"取得資料失敗: {ex.Message}");
                    System.Threading.Thread.Sleep(1000);
                }
            }

            return serverInfo; // 返回 serverInfo 物件
        }

    }
}