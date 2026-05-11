using admin_web.Models.FAQ;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace admin_web.Services.FaqServices
{
    public class Default_Service
    {
        // 資料庫config設置
        private static readonly string connectionString = UniversalService.sql_connectionString;

        // 撈取FAQ內容
        public static Dictionary<Faq_Model.Navbar_Main_Model, List<Faq_Model.Navbar_Sub_Model>> GetAll_Faq()
        {
            string sqlcommand = $@"
                    SELECT ToolID, Class, ToolName, ClassName
                    FROM (
                        SELECT DISTINCT cn.ID as `cnID`, n.ID as `nID`, f.ToolID, c.Class, n.ToolName, cn.Name as ClassName
                        FROM admin_tool.faq_content f
                        LEFT JOIN admin_tool.tool_info c ON f.ToolID REGEXP CONCAT('^', c.ToolID)
                        LEFT JOIN admin_tool.tool_info n ON f.ToolID = n.ToolID
                        LEFT JOIN admin_tool.tool_class cn ON c.Class = cn.Class
                        WHERE c.Class IS NOT NULL
                    ) sub
					ORDER BY cnID, nID";

            var toolDictionary = new Dictionary<Faq_Model.Navbar_Main_Model, List<Faq_Model.Navbar_Sub_Model>>();

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                {
                    using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                    {
                        while (sqlreader.Read())
                        {
                            var _class = new Faq_Model.Navbar_Main_Model
                            {
                                ClassID = sqlreader["Class"].ToString(),
                                ClassName = sqlreader["ClassName"].ToString()
                            };

                            var _tool = new Faq_Model.Navbar_Sub_Model
                            {
                                ToolID = sqlreader["ToolID"].ToString(),
                                ToolName = sqlreader["ToolName"].ToString()
                            };

                            if (!toolDictionary.ContainsKey(_class))
                            {
                                toolDictionary[_class] = new List<Faq_Model.Navbar_Sub_Model>();
                            }

                            toolDictionary[_class].Add(_tool);
                        }
                    }
                }
            }
            return toolDictionary;
        }

        // 撈取FAQ內容
        public static (string Name, string Content) Get_Content(string ID)
        {
            string content = string.Empty;
            string name = string.Empty;
            string sqlcommand = $@"
                    SELECT f.*, i.ToolName
                    FROM admin_tool.faq_content f
                    LEFT JOIN(
	                    SELECT i.ToolID, i.ToolName
                        FROM admin_tool.tool_info i
                    ) i on i.ToolID = f.ToolID
                    WHERE f.ToolID = '{ID}'";

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                {
                    using (MySqlDataReader sqlreader = cmd.ExecuteReader())
                    {
                        if (sqlreader.Read())
                        {
                            name = sqlreader["ToolName"].ToString();
                            content = sqlreader["Content"].ToString();
                        }
                    }
                }
            }
            return (name, content);
        }

        // FAQ內容更新
        public static string Update_Faq(string ID, string Content)
        {
            // 取出更新前的FAQ，用於比對圖片差異
            var original_faq = Get_Content(ID);
            var original_faq_content = original_faq.Content;

            // 如果更新的內容不為空且有圖片
            if (Content != null)
            {
                string base64Pattern = @"<img(?<attributes>[^>]+)src=""data:image/(?<type>[^;]+);base64,(?<data>[^""]+)""(?<otherAttributes>[^>]*)>";
                string imagesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "image", "Faq");

                // 防呆: 建立放圖片用的資料夾
                if (!Directory.Exists(imagesDirectory))
                {
                    Directory.CreateDirectory(imagesDirectory);
                }

                // 將img的Base64值做轉換後當作圖片的檔名，將FAQ的來源改抓本地端圖片
                Content = Regex.Replace(Content, base64Pattern, match =>
                {
                    string imageType = match.Groups["type"].Value;
                    string base64Data = match.Groups["data"].Value;

                    byte[] imageBytes = Convert.FromBase64String(base64Data);

                    string fileName = $"{Guid.NewGuid()}.{imageType}";

                    string imagePath = Path.Combine(imagesDirectory, fileName);

                    File.WriteAllBytes(imagePath, imageBytes);

                    string relativeImagePath = $"/image/Faq/{fileName}";

                    string attributes = match.Groups["attributes"].Value;
                    string otherAttributes = match.Groups["otherAttributes"].Value;

                    return $"<img {attributes}src=\"{relativeImagePath}\"{otherAttributes}>";
                });

                Content = Content.Replace("\\","\\\\");
            }

            // 更新資料庫的FAQ內容
            string sqlcommand = $@"
                    UPDATE admin_tool.faq_content 
                    SET `Content` = '{(Content ?? "").Replace("'", "''")}' 
                    WHERE (`ToolID` = '{ID}');";

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
                    {
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // 若資料庫更新成功，將被移除的圖片刪除
                            ProcessFaqImages(original_faq_content, Content);
                            return "SUCCESS";
                        }
                        else
                        {
                            return "沒有資料被更新，請團隊確認後臺是否正確設置";
                        }
                    }
                }
                catch
                {
                    return "更新時發生錯誤";
                }
            }
        }

        public static void ProcessFaqImages(string original_faq_content, string updated_content)
        {
            // 從本地端刪除FAQ被移除的圖片檔(用FAQ的前後內容做比對取出)
            string imagesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "image", "Faq");

            string imgPattern = @"<img[^>]+src=""(?<src>[^""]+)""[^>]*>";

            IEnumerable<string> imagesToDelete;

            HashSet<string> originalImages = ExtractImageSrc(original_faq_content, imgPattern);

            if (updated_content == null)
            {
                imagesToDelete = originalImages;
            }
            else
            {
                HashSet<string> updatedImages = ExtractImageSrc(updated_content, imgPattern);
                imagesToDelete = originalImages.Except(updatedImages);
            }

            foreach (var imageUrl in imagesToDelete)
            {
                if (imageUrl.StartsWith("/image/Faq/"))
                {
                    string fileName = Path.GetFileName(imageUrl);
                    string imagePath = Path.Combine(imagesDirectory, fileName);

                    if (File.Exists(imagePath))
                    {
                        File.Delete(imagePath);
                        Console.WriteLine($"Deleted unused image: {imagePath}");
                    }
                }
            }
        }

        private static HashSet<string> ExtractImageSrc(string content, string pattern)
        {
            var imageSrcList = new HashSet<string>();

            MatchCollection matches = Regex.Matches(content, pattern);

            foreach (Match match in matches)
            {
                string src = match.Groups["src"].Value;
                imageSrcList.Add(src);
            }

            return imageSrcList;
        }
    }
}
