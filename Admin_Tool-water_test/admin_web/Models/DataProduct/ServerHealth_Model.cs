using System;
using System.Collections.Generic;
using System.Data;

namespace admin_web.Models.DataProduct
{
    public class ServerHealth_ViewModel
    {
        public DataTable DataList_position { get; set; }
        public DataTable DataList_orderadd { get; set; }
        public DataTable DataList_socialtrading { get; set; }
        public DataTable DataList_hft_A { get; set; }
        public DataTable DataList_hft_B { get; set; }
        public DataTable DataList_archive { get; set; }
        public DataTable DataList_groupticket { get; set; }

        public DataTable DataList_real_pos{ get; set; }
        public DataTable DataList_loading { get; set; }
        public DataTable DataList_configuration { get; set; }
        public string Data_configuration { get; set; }
        public string Data_loading { get; set; }
        public DataTable DataList_analysis { get; set; }
        public Indicators IndicatorValues { get; set; }
        //public List<ServerList> ServerList { get; set; } = new List<ServerList>(); 
    }

    public class ServerList
    {
        public string Brand { get; set; }
        public string Server { get; set; }
    }

    public class SG_ConnectionInfo
    {
        public string Host { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string Port { get; set; }
    }

    public class ServerInfo
    {
        public string SGGroup { get; set; }
        public string SQLName { get; set; }
        public string ServerName { get; set; }
    }

    public class LoadingDto
    {
        public double Server { get; set; }
        public string Time { get; set; } // 時間欄位
        public double CPU { get; set; } // CPU 使用率欄位
        public double FreeMemory { get; set; }
        public double NetWork { get; set; }
    }

    public class Indicators
    {
        public string POSITION { get; set; }
        public int TOTAL_USERS { get; set; }
        public int PENDING { get; set; }
        public int SYMBOL { get; set; }
        public int GROUP { get; set; }
        public int ARCHIVE_DELETE { get; set; }
        public int ARCHIVE_INACTIVITY { get; set; }
    }
    public class TimeDelta
    {
        public string DAYLIGHT { get; set; }
        public string TIMEDELTA { get; set; }

    }

    //--------------------自動伺服器檢查
    public class Auto_Server_Check_ViewModel
    {
        public bool IsSearchPerformed { get; set; }

        //----基礎訊息
        public string ServerName { get; set; }
        public string StartTime { get; set; }
        public string endTime { get; set; }

        //-----持倉和總單量
        public int Position_Count { get; set; }
        public int Ticket_Count { get; set; }
        //-----改單
        public List<DataList_modify> DataList_modify { get; set; }
        public int ModifyTotalLogin { get; set; }
        public int ModifyTotalCount { get; set; }
        public int ModifyMax { get; set; }

        //-----關單
        public List<DataList_close> DataList_close { get; set; }
        public int CloseTotalLogin { get; set; }
        public int CloseTotalCount { get; set; }

        //-----no money
        public List<DataList_nomoney> DataList_nomoney { get; set; }
        public int NomoneyTotalLogin { get; set; }
        public int NomoneyTotalCount { get; set; }

        //-----filter 
        public List<DataList_filter> DataList_filter { get; set; }

        //-----user
        public List<DataList_unknownuser> DataList_unknownuser { get; set; }

        //-----password  
        public List<DataList_invalidpassword> DataList_invalidpassword { get; set; }

        //-----api  
        public List<DataList_api> DataList_api { get; set; }

        //----modify sec
        public List<DataList_modify_sec> DataList_modify_sec { get; set; }

        //-----memory
        public long max_memory { get; set; }

        public long min_memory { get; set; }
    }

    public class DataList_modify
    {
        //public string ServerName { get; set; }
        public long LOGIN { get; set; }
        public int COUNT { get; set; }
        public DateTime MT_INPUT_TIME { get; set; }
    }

    public class DataList_close
    {
        //public string ServerName { get; set; }
        public long LOGIN { get; set; }
        public int COUNT { get; set; }
        public DateTime MT_INPUT_TIME { get; set; }
    }

    public class DataList_nomoney
    {
        //public string ServerName { get; set; }
        public long LOGIN { get; set; }
        public int COUNT { get; set; }
    }

    public class DataList_filter
    {
        //public string ServerName { get; set; }
        public string SYMBOL { get; set; }
        public int COUNT { get; set; }
        //public DateTime MT_INPUT_TIME { get; set; }
    }

    public class DataList_unknownuser
    {
        //public string ServerName { get; set; }
        public long LOGIN { get; set; }
        public int COUNT { get; set; }
        public DateTime MT_INPUT_TIME { get; set; }
    }

    public class DataList_invalidpassword
    {
        //public string ServerName { get; set; }
        public long LOGIN { get; set; }
        public int COUNT { get; set; }
        public DateTime MT_INPUT_TIME { get; set; }
    }

    public class DataList_api
    {
        //public string ServerName { get; set; }
        public long LOGIN { get; set; }
        public int COUNT { get; set; }
        public DateTime MT_INPUT_TIME { get; set; }
    }

    public class DataList_modify_sec
    {
        //public string ServerName { get; set; }
        public long LOGIN { get; set; }
        public int TOTAL_COUNT { get; set; }

        public int SEC_COUNT { get; set; }

        public DateTime MT_INPUT_TIME { get; set; }
    }

}


