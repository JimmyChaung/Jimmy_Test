using System;

namespace admin_web.Models
{
    public class SettingModel
    {
        // Server.csv
        public class ServerRecord
        {
            public string NAME { get; set; } // SQL 名稱
            public string Server { get; set; } // Server 名稱
            
            // 正確比較的方法
            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }
                var other = (ServerRecord)obj;
                return NAME == other.NAME && Server == other.Server;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(NAME, Server);
            }
        }

        // PathConfig.csv
        public class PathRecord
        {
            public string Name_zh { get; set; } // 工具中文名稱
            public string Name_en { get; set; } // 工具英文名稱
            public string Path { get; set; } // 工具存放路徑(.\wwwroot\tools\ + Path)
        }

    }
}
