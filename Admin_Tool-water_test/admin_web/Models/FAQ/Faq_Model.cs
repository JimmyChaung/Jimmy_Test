using System;

namespace admin_web.Models.FAQ
{
    public class Faq_Model
    {
        public class Navbar_Main_Model
        {
            public string ClassID { get; set; }
            public string ClassName { get; set; }

            public override bool Equals(object obj)
            {
                if (obj is Navbar_Main_Model other)
                {
                    return this.ClassID == other.ClassID && this.ClassName == other.ClassName;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(ClassID, ClassName);
            }
        }

        public class Navbar_Sub_Model
        {
            public string ToolID { get; set; }
            public string ToolName { get; set; }
        }
    }
}
