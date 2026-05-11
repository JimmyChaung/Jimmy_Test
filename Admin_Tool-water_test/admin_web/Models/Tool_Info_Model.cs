

namespace admin_web.Models
{
    public class Tool_Info_Model
    {
        public class tool_info
        {
            public int ID { get; set; }
            public string ToolID { get; set; }
            public string ToolName { get; set; }
            public string Href { get; set; }
            public string Class { get; set; }
            public int Enable { get; set; }
            public string Type { get; set; }
            public string Remark { get; set; }
            public bool Faq_Btn { get; set; }
        }

        public class tool_class
        {
            public int ID { get; set; }
            public string Class { get; set; }
            public string Name { get; set; }
        }
    }
}
