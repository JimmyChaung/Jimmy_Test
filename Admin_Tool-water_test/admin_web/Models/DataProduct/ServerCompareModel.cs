using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;

namespace admin_web.Models.DataProduct
{
    public class NetworkConfig
    {
        public string Name { get; set; }

    }

    public class ServerConfig
    {
        public List<NetworkConfig> ConfigNetwork { get; set; }
    }

    public class JsonRoot
    {
        public List<ServerConfig> Server { get; set; }
    }

    public class ConfigPair
    {
        public string A_Name { get; set; } = "";
        public string B_Name { get; set; } = "";
    }

    public class PairResult
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public string Custom { get; set; }
    }



}
