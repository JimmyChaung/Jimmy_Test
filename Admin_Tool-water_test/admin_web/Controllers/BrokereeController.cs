using admin_web.Models.Brokeree;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace admin_web.Controllers
{
    public class BrokereeController : Controller
    {

        private readonly IConfiguration _configuration;

        public BrokereeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public IActionResult OP_UserReset_password()
        {
            string connectionString = _configuration.GetConnectionString("OP_TOOL_Connection");
            var users = new List<Op_userinfo>();

            using (var conn = new MySqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    string sql = "SELECT * FROM pamm_permissions.pm_login where 1=1 and pm_user regexp 'op|keree';";

                    //string sql = "SELECT * FROM pamm_permissions.pm_login where 1=1 and pm_user not regexp 'op';";
                    //string sql = "SELECT * FROM pamm_permissions.pm_login;";

                    using (var cmd = new MySqlCommand(sql, conn))

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new Op_userinfo
                            {
                                Id = reader.GetInt32("Id"),
                                fa_user = reader.IsDBNull(reader.GetOrdinal("fa_user")) ? "" : reader.GetString("fa_user"),
                                fa_pass = reader.IsDBNull(reader.GetOrdinal("fa_pass")) ? "" : reader.GetString("fa_pass"),
                                pm_user = reader.IsDBNull(reader.GetOrdinal("pm_user")) ? "" : reader.GetString("pm_user"),
                                pm_pass = reader.IsDBNull(reader.GetOrdinal("pm_pass")) ? "" : reader.GetString("pm_pass"),
                                server_name = reader.IsDBNull(reader.GetOrdinal("server_name")) ? "" : reader.GetString("server_name"),
                                server_url = reader.IsDBNull(reader.GetOrdinal("server_url")) ? "" : reader.GetString("server_url"),
                                serverport = reader.IsDBNull(reader.GetOrdinal("serverport")) ? "" : reader.GetString("serverport"),
                                op_url = reader.IsDBNull(reader.GetOrdinal("op_url")) ? "" : reader.GetString("op_url"),
                                config_set = reader.IsDBNull(reader.GetOrdinal("configration_list")) ? "" : reader.GetString("configration_list"),
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "發生錯誤！" + ex.Message;
                    return View(users);
                }
            }

            if (!users.Any())
            {
                TempData["ErrorMessage"] = "未找到使用者！";
            }

            return View(users);
        }

        [HttpPost]
        public IActionResult UpdateUserPassword(List<Op_userinfo> users, int? updateUserId)
        {
            string connectionString = _configuration.GetConnectionString("OP_TOOL_Connection");
            string user_id = updateUserId.HasValue ? users.FirstOrDefault(u => u.Id == updateUserId)?.fa_user ?? "未知" : "批量修改";

            using (var conn = new MySqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    string sql = "UPDATE pamm_permissions.pm_login SET fa_pass = @fa_pass, configration_list = @config_set WHERE Id = @Id;";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        foreach (var user in users.Where(u => !updateUserId.HasValue || u.Id == updateUserId.Value))
                        {
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@fa_pass", user.fa_pass);
                            cmd.Parameters.AddWithValue("@config_set", user.config_set);
                            cmd.Parameters.AddWithValue("@Id", user.Id);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    TempData["SuccessMessage"] = $"{user_id} 更新成功！";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "發生錯誤：" + ex.Message;
                }
            }

            return RedirectToAction("OP_UserReset_password");
        }
    }
}
