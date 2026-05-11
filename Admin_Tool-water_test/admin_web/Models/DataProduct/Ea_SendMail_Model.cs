using System;

namespace admin_web.Models.DataProduct
{
    public class Ea_SendMail_Model
    {
        public class MailRecord
        {
            // Mail_Send.csv
            public string brand { get; set; }
            public string send_mail { get; set; }
            public string receive_mail { get; set; } 

            // 正確比較的方法
            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }
                var other = (MailRecord)obj;
                return brand == other.brand && send_mail == other.send_mail && receive_mail == other.receive_mail;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(brand, send_mail, receive_mail);
            }
        }

        public class MailResult
        {
            public string From { get; set; }
            public string To { get; set; }
            public string Brand { get; set; }
            public string Status { get; set; }
        }
    }
}
