using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace admin_web.Models.PressureLab
{
    public class MtScriptOneModel
    {
        public string SelectedServer { get; set; }
        public string SelectedGroup { get; set; }

        public int Leverage { get; set; }


        public int login { get; set; }

        public int in_balance {get;set;}

        public int comment { get; set; }



        public List<string> ServerList { get; set; }
        public List<string> GroupList { get; set; }
    }


    public class MtScriptTwoModel
    {
        public string SelectedServer { get; set; }
        public string SelectedGroup { get; set; }

        public int Leverage { get; set; }


        public int login { get; set; }

        public int in_balance { get; set; }

        public int comment { get; set; }



        public List<string> ServerList { get; set; }
        public List<string> GroupList { get; set; }


        public string Symbol { get; set; }
        

        public double Volume { set; get; }

    }


}
