using admin_web.Controllers;
using admin_web.Models.Permission;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace admin_web.Services.PermissionServices
{
    public class Permission_Data
    {


        private readonly ILogger<Permission_Data> _logger;
        private readonly string _connectionString;

        // 透過 DI 注入 ILogger 和 IConfiguration
        public Permission_Data(ILogger<Permission_Data> logger, IConfiguration configuration)
        {
            _logger = logger;
            // 從 appsettings.json 中取得連接字串
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        //開始調用的function
        //開始調用的function
        public List<UserModelsList> ExecuteSQL_serverlist()
        {
            List<UserModelsList> userList = new List<UserModelsList>();

            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                var query = "SELECT * FROM admin_tool.web_account;";
                using (var command = new MySqlCommand(query, connection))
                {
                    int count = Convert.ToInt32(command.ExecuteScalar());
                    using (var reader = command.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            UserModelsList user = new UserModelsList()
                            {

                                Login = reader.GetInt32("LOGIN"),
                                Name = reader.GetString("NAME"),
                                Enable = reader.GetInt32("ENABLE"),
                                Create_Time = reader.GetDateTime("CREATE_TIME"),
                                Remark = reader.GetString("REMARK"),
                                F_Permission = reader.GetString("F_PREMISSION"),
                                Email = reader.GetString("EMAIL"),
                                B_Permission = reader.GetString("B_PREMISSION"),
                                Role = reader.GetString("ROLE"),
                                Last_Time = reader.GetDateTime("LAST_TIME"),

                            };

                            userList.Add(user);
                        }
                    }

                }
            }


            Debug.WriteLine(userList);


            return userList;
        }



        //更新資訊
        public int ExecuteSQL_updata(AccountModel accountUpdate)
        {

            //資料是否更新
            var updata_message = 1;

            // 確認接收的資料
            var login = Int32.Parse(accountUpdate.LoginValue);
            var name = accountUpdate.NameValue;
            var role = accountUpdate.RoleValue;
            var email = accountUpdate.EmailValue;
            var lastTime = accountUpdate.LastTimeValue;
            var status = accountUpdate.Status;

            var account_enable = 0;

            if (status == "停用")
            {
                account_enable = 0;
            }
            else
            {
                account_enable = 1;
            }



            List<UserModelsList> userList = new List<UserModelsList>();


            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();


                ///先做查詢
                bool recordExists = false;
                ///安全的寫法，使用參數化查詢
                var query = "SELECT * FROM admin_tool.web_account where 1=1 and LOGIN=@login;";
                using (var command = new MySqlCommand(query, connection))
                {
                    // Add the Login parameter to the query
                    command.Parameters.AddWithValue("@login", login);
                    //// Add the Login parameter to the query
                    //command.Parameters.AddWithValue("@name", name);
                    //// Add the Login parameter to the query
                    //command.Parameters.AddWithValue("@email", email);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            recordExists = true;

                            // （選擇性）可以在這裡讀取數據，以便在更新前需要使用
                            while (reader.Read())
                            {
                                Console.WriteLine($"找到的用戶名稱：{reader["Name"]}");
                            }
                        }
                    }
                }

                //更新updata
                if (recordExists)
                {

                    var updataquery = @"UPDATE admin_tool.web_account 
                        SET 
                        NAME = @name,
                        ROLE = @role,
                        EMAIL = @email,
                        ENABLE = @enable 
                        WHERE 
                        Login = @login;";
                    using (var updateCommand = new MySqlCommand(updataquery, connection))
                    {
                        updateCommand.Parameters.AddWithValue("@login", login);
                        updateCommand.Parameters.AddWithValue("@name", name);
                        updateCommand.Parameters.AddWithValue("@role", role);
                        updateCommand.Parameters.AddWithValue("@email", email);
                        updateCommand.Parameters.AddWithValue("@enable", account_enable);

                        updateCommand.ExecuteNonQuery();
                        Console.WriteLine("用戶紀錄已成功更新。");
                    }
                    updata_message = 0;

                }
                else
                {
                    updata_message = 2;
                    Console.WriteLine("找不到指定的用戶，未執行更新操作。");
                }

                //回傳

                //var query = "SELECT * FROM admin_tool.web_account;";
                //using (var command = new MySqlCommand(query, connection))
                //{
                //    int count = Convert.ToInt32(command.ExecuteScalar());
                //    using (var reader = command.ExecuteReader())
                //    {

                //        while (reader.Read())
                //        {
                //            UserModelsList user = new UserModelsList()
                //            {

                //                Login = reader.GetInt32("LOGIN"),
                //                Name = reader.GetString("NAME"),
                //                //password不用
                //                Enable = reader.GetInt32("ENABLE"),
                //                Create_Time = reader.GetDateTime("CREATE_TIME"),
                //                Remark = reader.GetString("REMARK"),
                //                F_Permission = reader.GetString("F_PREMISSION"),
                //                Email = reader.GetString("EMAIL"),
                //                B_Permission = reader.GetString("B_PREMISSION"),
                //                Role = reader.GetString("ROLE"),
                //                Last_Time = reader.GetDateTime("LAST_TIME"),

                //            };

                //            userList.Add(user);
                //        }
                //    }

                //}
            }

            return updata_message;
        }


        //刪除資訊
        public int ExecuteSQL_delete(int accountId)
        {
            // 撰寫 SQL 刪除語句
            string query = "DELETE FROM admin_tool.web_account  WHERE 1=1 and LOGIN = @LOGIN";

            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@LOGIN", accountId);
                    return command.ExecuteNonQuery(); // 返回受影響的行數
                }
            }
        }


        //  body: JSON.stringify({ AccountLogin: Login, AccountName: Name, AccountEmail: Email, AccountRole: Role }),
        public class AccountAddModel
        {
            public string AccountLogin { get; set; }
            public string AccountName { get; set; }
            public string AccountEmail { get; set; }
            public string AccountRole { get; set; }
        }


        //新增資訊
        public bool AddAccount(AccountModel account)
        {
            string query = "INSERT INTO admin_tool.web_account (" +
                "LOGIN, NAME, PASSWORD, ENABLE, CREATE_TIME, REMARK, F_PREMISSION, EMAIL, B_PREMISSION, ROLE, LAST_TIME)   " +
                "VALUES (@Login, @Name, @Password, @Enable, @create_time , @Remark, @F_PREMISSION, @Email, @B_PREMISSION, @Role, @LAST_TIME)";

            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Login", account.LoginValue);
                    command.Parameters.AddWithValue("@Name", account.NameValue);
                    command.Parameters.AddWithValue("@Email", account.EmailValue);
                    command.Parameters.AddWithValue("@Role", account.RoleValue);
                    //暫時寫死的

                    //時間轉換

                    string dateString = "2024-10-09 00:00:00";
                    DateTime lastTime = DateTime.Parse(dateString);

                    command.Parameters.AddWithValue("@Password", "Aa123456");
                    command.Parameters.AddWithValue("@Enable", 1);
                    command.Parameters.AddWithValue("@create_time", lastTime);
                    command.Parameters.AddWithValue("@Remark", "ADMIN TEST");
                    command.Parameters.AddWithValue("@F_PREMISSION", "[A1,A2,,A4,B1]");
                    command.Parameters.AddWithValue("@B_PREMISSION", 1);
                    command.Parameters.AddWithValue("@LAST_TIME", lastTime);



                    int rowsAffected = command.ExecuteNonQuery(); // 返回受影響的行數
                    return rowsAffected > 0; // 返回是否成功插入
                }
            }
        }



    }
}
