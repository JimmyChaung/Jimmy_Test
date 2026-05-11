using System.Collections.Generic;

namespace admin_web.Models.Mtapiuse
{
    public class EditSymbol_Model
    {
        public class sql_record
        {
            public string Server { get; set; }
            public string Symbol { get; set; }
            public string Item { get; set; }
            public string Before { get; set; }
            public string After { get; set; }
            public string UserLogin { get; set; }
            public string Time { get; set; }
        }

        public class log_record
        {
            public string Server { get; set; }
            public string Symbol { get; set; }
            public List<string> Difference { get; set; }
            public string Result { get; set; }
        }

        public class mt4_input
        {
            public string Server { get; set; }
            public string Symbol { get; set; }
            public string Symbol_Description { get; set; }
            public string Symbol_Type { get; set; }
            public string Symbol_Trade { get; set; }
            public string Symbol_StopLevel { get; set; }
            public string Symbol_FreezeLevel { get; set; }
            public string Symbol_LongOnly { get; set; }
            public string Filtration_Level { get; set; }
            public string Filtration_AutomaticLimit { get; set; }
            public string Filtration_Filter { get; set; }
            public string Filtration_IgnoreQuotes { get; set; }
            public string Swap_Enable { get; set; }
        }

        public class mt5_input
        {
            public string Server { get; set; }
            public string Symbol { get; set; }
            public string Common_Description { get; set; }
            public string Common_Exchange { get; set; }
            public string Common_International { get; set; }
            public string Common_ISIN { get; set; }
            public string Common_CFI { get; set; }
            public string Quotes_SoftFiltrationLevel { get; set; }
            public string Quotes_SoftFilter { get; set; }
            public string Quotes_HardFiltrationLevel { get; set; }
            public string Quotes_HardFilter { get; set; }
            public string Trade_VolumesMin { get; set; }
            public string Trade_VolumesStep { get; set; }
            public string Trade_VolumesMax { get; set; }
            public string Trade_StopLevel { get; set; }
            public string Trade_FreezeLevel { get; set; }
            public string Swaps_Enable { get; set; }
        }


        public class Login_Record
        {
            public string Server { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }
        }
    }
}
