using admin_web.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

namespace admin_web.Services.HomeServices
{
    public class Tool_Info_Service
    {
        // 資料庫config設置
        private static readonly string connectionString = UniversalService.sql_connectionString;

        // 取得tool_class的全部資料
        public static List<Tool_Info_Model.tool_class> GetAll_tool_class()
        {
            var toolList = new List<Tool_Info_Model.tool_class>();

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                string sqlcommand = $@"
                        SELECT * 
                        FROM admin_tool.tool_class
                        order by ID";

                connection.Open();

                using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                {
                    using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                    {
                        while (sqlreader.Read())
                        {
                            var toolClass = new Tool_Info_Model.tool_class
                            {
                                Class = sqlreader["Class"].ToString(),
                                Name = sqlreader["Name"].ToString()
                            };

                            toolList.Add(toolClass);
                        }
                    }
                }
            }
            return toolList;
        }

        // 撈取首頁工具分類(子項)
        public static Dictionary<string, List<Tool_Info_Model.tool_info>> Homepage_Tool_Sub()
        {
            var toolDictionary = new Dictionary<string, List<Tool_Info_Model.tool_info>>();

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                string sqlcommand = $@"
                    SELECT *
                    FROM admin_tool.tool_info i
                    LEFT JOIN admin_tool.tool_class c
	                    on c.Class = i.Class
					LEFT JOIN admin_tool.faq_content f
						on i.ToolID = f.ToolID
                    WHERE i.Class != ''
                    ORDER BY c.ID, i.ID";

                connection.Open();

                using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                {
                    using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                    {
                        while (sqlreader.Read())
                        {
                            var tool = new Tool_Info_Model.tool_info
                            {
                                ToolID = sqlreader["ToolID"].ToString(),
                                ToolName = sqlreader["ToolName"].ToString(),
                                Href = sqlreader["Href"].ToString(),
                                Class = sqlreader["Class"].ToString(),
                                Type = sqlreader["Type"].ToString(),
                                Enable = (int)sqlreader["Enable"],
                                Remark = sqlreader["Remark"].ToString(),
                                Faq_Btn = !string.IsNullOrEmpty(sqlreader["Content"].ToString()) & sqlreader["Type"].ToString() == "Tool"
                            };

                            string className = sqlreader["Class"].ToString();

                            if (!toolDictionary.ContainsKey(className))
                            {
                                toolDictionary[className] = new List<Tool_Info_Model.tool_info>();
                            }

                            toolDictionary[className].Add(tool);
                        }
                    }
                }
            }
            return toolDictionary;
        }

        // 路徑工具下的分類
        public static List<Tool_Info_Model.tool_info> Tool_Sub_Sublist(string Tool)
        {
            var toolList = new List<Tool_Info_Model.tool_info>();

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                string sqlcommand = $@"
                    SELECT *
                    FROM admin_tool.tool_info
                    WHERE 1 = 1
	                    AND ToolID regexp '^{Tool}'";

                connection.Open();

                using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                {
                    using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                    {
                        while (sqlreader.Read())
                        {
                            var tool = new Tool_Info_Model.tool_info
                            {
                                ToolID = sqlreader["ToolID"].ToString(),
                                ToolName = sqlreader["ToolName"].ToString(),
                                Href = sqlreader["Href"].ToString(),
                                Class = sqlreader["Class"].ToString(),
                                Type = sqlreader["Type"].ToString(),
                                Enable = (int)sqlreader["Enable"],
                                Remark = sqlreader["Remark"].ToString()
                            };


                            toolList.Add(tool);
                        }
                    }
                }
            }
            return toolList;
        }

        // ID找工具名稱
        public static string Get_Tool_Name(string ID)
        {
            string _toolName = string.Empty;

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                string sqlcommand = $@"
                        SELECT ToolName
                        FROM admin_tool.tool_info
                        WHERE 1 = 1
                            AND ToolID = '{ID}'
                        ";

                connection.Open();

                using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                {
                    using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                    {
                        while (sqlreader.Read())
                        {
                            _toolName = sqlreader["ToolName"].ToString();
                        }
                    }
                }
            }
            return _toolName;
        }

        // 搜尋功能用的列表
        public static List<object> Get_Search_Menu()
        {
            var menu_list = new List<object>();

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                string sqlcommand = $@"
                    SELECT ToolName, Href
                    FROM admin_tool.tool_info
                    WHERE 1 = 1
	                    AND Type = 'Tool'";

                connection.Open();

                using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                {
                    using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                    {
                        while (sqlreader.Read())
                        {
                            string toolName = sqlreader["ToolName"].ToString();
                            string href = sqlreader["Href"].ToString();

                            menu_list.Add(new { name = toolName, href = href });
                        }
                    }
                }
            }
            return menu_list;
        }
    }
}