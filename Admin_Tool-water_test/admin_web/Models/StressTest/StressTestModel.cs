using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static admin_web.Controllers.PressureLabController;

namespace admin_web.Models.StressTest
{
    public class StressTestModel
    {
        public enum MTServerType { MT4, MT5 }
        public enum OperationType
        {
            CreateAccount,
            Deposit,
            OpenTrade,
            DeleteTrade,
        }
        public class OperationResult
        {
            public OperationType Operation { get; set; }
            public bool IsSuccess { get; set; } = false;
            public string Commnet { get; set; }
        }

        public class ServerBase
        {
            public string ServerName { get; set; }
            public string ServerIp { get; set; }
            public MTServerType MTType { get; set; }
            public string? ServerGroup { get; set; }
            public string? ServerSymbol { get; set; }
            public int? AdminLogin { get; set; }
            public string? Password { get; set; }
            public string? DBConnection { get; set; }
            public string Message { get; set; }
            public int initLogin { get; set; }
            public Dictionary<long?, List<OperationResult>> LoginResults { get; set; } = new Dictionary<long?, List<OperationResult>>();
            public void AddResult(long? Login, OperationType operation, bool isSuccess, string comment)
            {
                if (!LoginResults.ContainsKey(Login))
                    LoginResults[Login] = new List<OperationResult>();
                LoginResults[Login].Add(new OperationResult
                {
                    Operation = operation,
                    IsSuccess = isSuccess,
                    Commnet = comment
                });
            }
            public ServerBase()
            {
                LoginResults= this.LoginResults;
            }
        }
        public class ServerResult<TStatus> : ServerBase
        {
            public TStatus? Status { get; set; }
            public bool IsConnected { get; set; } = false;

            public ServerResult(ServerBase baseData)
            {
                this.ServerName = baseData.ServerName;
                this.ServerIp = baseData.ServerIp;
                this.MTType = baseData.MTType;
                this.ServerGroup = baseData.ServerGroup;
                this.AdminLogin = baseData.AdminLogin;
                this.Password = baseData.Password;
                this.initLogin = baseData.initLogin;
                this.LoginResults = baseData.LoginResults;
            }
        }
        public class DocxReportData
        {
            public string Server { get; set; } = "";
            public string Group { get; set; } = "";
            public string Leverage { get; set; } = "";
            public string LoginCount { get; set; } = "";
            public string Deposit { get; set; } = "";
            public string DepositComment { get; set; } = "";

            public string Symbol { get; set; } = "";
            public string Lots { get; set; } = "";
            public string OrderCount { get; set; } = "";
            public string Price { get; set; } = "";
            public string TP { get; set; } = "";
            public string SL { get; set; } = "";
            public string OrderComment { get; set; } = "";

            public string Cpu_Max { get; set; } = "";
            public string Cpu_Max_Time { get; set; } = "";
            public string Cpu_Average { get; set; } = "";

            public string Memory_Max { get; set; } = "";
            public string Memory_Max_Time { get; set; } = "";
            public string Memory_Average { get; set; } = "";

            public string IOPS_Max { get; set; } = "";
            public string IOPS_Max_Time { get; set; } = "";
            public string IOPS_Average { get; set; } = "";

            public string InstantRequestRate { get; set; } = "";
            public string InstantRequestTime { get; set; } = "";
            public string AverageRequest { get; set; } = "";
            public string FinalSuccessOrders { get; set; } = "";
            public DocxReportData(StressTestRequest d)
            {
                Server = d.ServerName;
                Group = d.Group;
                Leverage = d.Leverage.ToString();
                LoginCount = d.Volume.ToString();
                Deposit = d.Balance.ToString();
                DepositComment = d.Comment;
                InstantRequestRate = "";
                InstantRequestTime = "";
                AverageRequest = "";
                FinalSuccessOrders = "";
                Symbol = d.ORDER_SYMBOL;
                Lots = d.ORDER__LOTS.ToString();
                OrderCount = d.ORDER__VOLUME.ToString();
                Price = d.ORDER__PRICE.ToString();
                TP = d.ORDER_TP_VALUE.ToString();
                SL = d.ORDER_SL_VALUE.ToString();
                OrderComment = d.OPEN_ORDER_COMMENT;
            }
            public void UpdatePerformance(Respect_Red_Result d)
            {
                this.Cpu_Max = d.Cpu_Max;
                this.Cpu_Max_Time = d.Cpu_Max_Time;
                this.Cpu_Average = d.Cpu_Average;
                this.Memory_Max = d.Memory_Max;
                this.Memory_Max_Time = d.Memory_Max_Time;
                this.Memory_Average = d.Memory_Average;
                this.IOPS_Max = d.IOPS_Max;
                this.IOPS_Max_Time = d.IOPS_Max_Time;
                this.IOPS_Average = d.IOPS_Average;
            }
        }
        public class Respect_Red_Result
        {
            public String Server_Start_Time { get; set; }
            public String Server_End_Time { get; set; }
            public String Cpu_Average { get; set; }

            public string Cpu_Max_Time { get; set; }

            public String Cpu_Max { get; set; }

            public String Memory_Average { get; set; }

            public String Memory_Max_Time { get; set; }

            public String Memory_Max { get; set; }
            
            public String IOPS_Max { get; set; }
            public String IOPS_Max_Time { get; set; }
            public String IOPS_Average { get; set; }

            public String Request_Max { get; set; }

            public String Request_Max_time { get; set;  }

            public String Request_Max_Average { get; set; }

        }
    }
}

