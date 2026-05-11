using admin_web.Models.DataProduct;
using admin_web.Models.Permission;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using static admin_web.Models.DataProduct.New_Pe_setting;
using static admin_web.Models.DataProduct.Pe_setting;

namespace admin_web.Services.DataProductServices
{
    public class Pe_level_Setting
    {

        private readonly ILogger<Pe_setting> _logger;
        private readonly string _connectionString;

        // 透過 DI 注入 ILogger 和 IConfiguration
        public Pe_level_Setting(ILogger<Pe_setting> logger, IConfiguration configuration)
        {
            _logger = logger;
            // 從 appsettings.json 中取得連接字串
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        //會搜巡所有的table的list

        //  pe_Level_Setting.ExecuteSQL_serverform(interests, skills,time_comp);
        public (List<Pe_setting.aggregationconfig>,
              List<Pe_setting.executionprofiles>,
              List<Pe_setting.marketinformation>,
              List<Pe_setting.pricestream>,
              List<Pe_setting.volumeband>) SQL_list_symbol()
        {
            var aggregationconfigList = new List<Pe_setting.aggregationconfig>();
            var executionprofilesList = new List<Pe_setting.executionprofiles>();
            var marketinformationList = new List<Pe_setting.marketinformation>();
            var pricestreamList = new List<Pe_setting.pricestream>();
            var volumebandList = new List<Pe_setting.volumeband>();

            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();

                // 通用查詢方法：query、建構函式、存入目標 list
                void FetchNames<T>(string query, Func<string, T> createItem, List<T> targetList)
                {
                    using var command = new MySqlCommand(query, connection);
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var name = reader.GetString(0);
                        targetList.Add(createItem(name));
                    }
                }
                //pe_setting.volumeband;"
                // 各資料表的查詢
                FetchNames("SELECT DISTINCT Name FROM pe_setting.aggregationconfig;",
                    name => new Pe_setting.aggregationconfig { Name = name },
                    aggregationconfigList);

                FetchNames("SELECT DISTINCT Name FROM pe_setting.executionprofiles;",
                    name => new Pe_setting.executionprofiles { Name = name },
                    executionprofilesList);

                FetchNames("SELECT DISTINCT CONCAT(IFNULL(FeederSource, ''), '-', IFNULL(Symbol, '')) AS Symbol " +
                    " FROM pe_setting.marketinformation WHERE MarketStatus = 1; ",
                    name => new Pe_setting.marketinformation { Symbol = name },
                    marketinformationList);

                //FetchNames("SELECT DISTINCT Symbol FROM pe_setting.pricestream;",
                //    name => new Pe_setting.pricestream { Name = name },
                //    pricestreamList);


                FetchNames("SELECT DISTINCT ps.PriceStreamProfileName AS Symbol " +
                    "FROM pe_setting.PriceStreamProfileConfig AS psc " +
                    "LEFT JOIN " +
                    "pe_setting.PriceStreamProfile AS ps " +
                    "ON psc.PricingStreamProfileId = ps.Id " +
                    "WHERE ps.IsActive = 'true'; ",
                    name => new Pe_setting.pricestream { Symbol = name },
                    pricestreamList);

                FetchNames("select distinct vbc.VolumeBandName as Symbol  from pe_setting.VolumeBandConfiguration vbc " +
                    "left join pe_setting.VolumeBandConfig vbc2 " +
                    "on vbc.Id = vbc2.VolumeBandConfigurationId where vbc.IsActive = 'true'; ",
                    name => new Pe_setting.volumeband { Symbol = name },
                    volumebandList);
            }


            return (aggregationconfigList, executionprofilesList, marketinformationList, pricestreamList, volumebandList);


        }

        //開始調用的function
        public Pe_setting.SymbolSettingViewModel ExecuteSQL_serverform(List<string> table_name, List<string> regions,
            List<string> symbols, string dateTime)
        {

            //先判斷是否為一個，如如果不適一個的話

            var result = new Pe_setting.SymbolSettingViewModel();

            if (table_name == null || table_name.Count == 0)
            {
                Console.WriteLine("table_name 為空");
                return result;
            }

            foreach (var table in table_name)
            {
                switch (table.ToLower())
                {
                    case "aggregationconfig":
                        result.AggregationconfigList ??= new List<aggregationconfig>();
                        result.AggregationconfigList.AddRange(
                            Aggregationconfig_Get(new List<string> { table }, regions, symbols, dateTime)
                        );
                        break;

                    case "executionprofiles":
                        result.ExecutionprofilesList ??= new List<executionprofiles>();
                        result.ExecutionprofilesList.AddRange(
                            Executionprofiles_Get(new List<string> { table }, regions, symbols, dateTime)
                        );
                        break;

                    case "marketinformation":
                        result.MarketinformationList ??= new List<marketinformation>();
                        result.MarketinformationList.AddRange(
                            MarketInformation_Get(new List<string> { table }, regions, symbols, dateTime)
                        );
                        break;

                    case "pricestream":
                        result.PricestreamList ??= new List<pricestream>();
                        result.PricestreamList.AddRange(
                            Pricestream_Get(new List<string> { table }, regions, symbols, dateTime)
                        );
                        break;

                    case "volumeband":
                        result.VolumebandList ??= new List<volumeband>();
                        result.VolumebandList.AddRange(
                            Volumeband_Get(new List<string> { table }, regions, symbols, dateTime)
                        );
                        break;

                    default:
                        Console.WriteLine($"未知的資料表: {table}");
                        break;
                }
            }

            return result;
        }

        //---------------------------------------------------------------------------------------
        public List<Pe_setting.aggregationconfig> Aggregationconfig_Get(List<string> table_name, List<string> regions,
            List<string> symbols, string dateTime)
        {
            List<Pe_setting.aggregationconfig> aggregationconfig_ny_list = new List<Pe_setting.aggregationconfig>();


            string regionFilter = regions != null && regions.Any() ? string.Join("|", regions) : string.Empty;
            string symbolFilter = symbols != null && symbols.Any() ? string.Join("|", symbols) : string.Empty;



            //sql date(要先塞選他需要那些地區)
            using (var connection = new MySqlConnection(_connectionString))
            {

                connection.Open();


                var query = "";

                //判斷是否為ALL
                if (symbols[0] == "ALL")
                {
                    query = @"SELECT * FROM pe_setting.aggregationconfig 
                                WHERE 1=1
                                  AND REGION REGEXP @region
                                  AND INPUT_TIME REGEXP @date;";
                }
                else
                {
                    query = @"SELECT * FROM pe_setting.aggregationconfig 
                                WHERE 1=1
                                  AND REGION REGEXP @region
                                  AND INPUT_TIME REGEXP @date
                                  AND `Name` REGEXP @symbol;";
                }


                using (var command = new MySqlCommand(query, connection))
                {

                    command.Parameters.AddWithValue("@region", regionFilter); // e.g., "NY" or "NY|TY"
                    command.Parameters.AddWithValue("@date", dateTime);

                    if (symbols[0] == "ALL")
                    { }
                    else
                    {
                        command.Parameters.AddWithValue("@symbol", symbolFilter);
                    }

                    int count = Convert.ToInt32(command.ExecuteScalar());

                    using (var reader = command.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            Pe_setting.aggregationconfig pe_aggregationconfig = new Pe_setting.aggregationconfig()
                            {
                                Id = reader.GetNullableString("Id"),
                                Name = reader.GetNullableString("Name"),
                                AggregationType = reader.GetNullableString("AggregationType"),
                                FailoverInterval = reader.GetNullableString("FailoverInterval"),
                                Description = reader.GetNullableString("Description"),
                                FeederSource = reader.GetNullableString("FeederSource"),
                                AssetType = reader.GetNullableString("AssetType"),
                                IsActive = reader.GetNullableString("IsActive"),
                                AggregationRule = reader.GetNullableString("AggregationRule"),
                                UpdatedBy = reader.GetNullableString("UpdatedBy"),
                                UpdatedDate = reader.GetNullableString("UpdatedDate"),
                                CreatedBy = reader.GetNullableString("CreatedBy"),
                                CreatedDate = reader.GetNullableString("CreatedDate"),
                                ClearNudgeWhenStarts = reader.GetNullableString("ClearNudgeWhenStarts"),
                                NudgeCalculationIntervalSeconds = reader.GetNullableString("NudgeCalculationIntervalSeconds"),
                                NudgeCalculationKeepPriceIntervalSeconds = reader.GetNullableString("NudgeCalculationKeepPriceIntervalSeconds"),
                                NudgePersistInCacheMinutes = reader.GetNullableString("NudgePersistInCacheMinutes"),
                                AverageSpread = reader.GetNullableString("AverageSpread"),
                                IgnoreSourceInterval = reader.GetNullableString("IgnoreSourceInterval"),
                                NumberOfDecimalPlaces = reader.GetNullableString("NumberOfDecimalPlaces"),
                                SpreadVolatility = reader.GetNullableString("SpreadVolatility"),
                                MaxFilterInterval = reader.GetNullableString("MaxFilterInterval"),
                                Throttle = reader.GetNullableString("Throttle"),
                                EnableSpreadBand = reader.GetNullableString("EnableSpreadBand"),
                                Sensitivity = reader.GetNullableString("Sensitivity"),
                                TargetSpread = reader.GetNullableString("TargetSpread"),
                                TargetValues = reader.GetNullableString("TargetValues"),
                                Thresholds = reader.GetNullableString("Thresholds"),
                                INPUT_TIME = reader.GetNullableString("INPUT_TIME"),
                                REGION = reader.GetNullableString("REGION")

                            };
                            aggregationconfig_ny_list.Add(pe_aggregationconfig);
                        }
                    }
                }
            }
            return aggregationconfig_ny_list;
        }
        //---------------------------------------------------------------------------------------

        public List<Pe_setting.executionprofiles> Executionprofiles_Get(List<string> table_name, List<string> regions,
    List<string> symbols, string dateTime)
        {
            List<Pe_setting.executionprofiles> executionprofiles_list = new List<Pe_setting.executionprofiles>();

            string regionFilter = regions != null && regions.Any() ? string.Join("|", regions) : string.Empty;
            string symbolFilter = symbols != null && symbols.Any() ? string.Join("|", symbols) : string.Empty;


            //sql date(要先塞選他需要那些地區)
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();

                var query = "";

                //判斷是否為ALL
                if (symbols[0] == "ALL")
                {
                    query = @"SELECT * FROM pe_setting.executionprofiles 
                                WHERE 1=1
                                  AND REGION REGEXP @region
                                  AND INPUT_TIME REGEXP @date;";
                }
                else
                {
                    query = @"SELECT * FROM pe_setting.executionprofiles 
                                WHERE 1=1
                                  AND REGION REGEXP @region
                                  AND INPUT_TIME REGEXP @date 
                                    AND `Name` REGEXP @symbol;";
                }


                using (var command = new MySqlCommand(query, connection))
                {

                    command.Parameters.AddWithValue("@region", regionFilter); // e.g., "NY" or "NY|TY"
                    command.Parameters.AddWithValue("@date", dateTime);

                    if (symbols[0] != "ALL")
                    {
                        command.Parameters.AddWithValue("@symbol", symbolFilter);
                    }

                    int count = Convert.ToInt32(command.ExecuteScalar());
                    using (var reader = command.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            Pe_setting.executionprofiles executionprofiles = new Pe_setting.executionprofiles()
                            {
                                Id = reader.GetNullableString("Id"),
                                Name = reader.GetNullableString("Name"),
                                MinDelay = reader.GetNullableString("MinDelay"),
                                MaxDelay = reader.GetNullableString("MaxDelay"),
                                MinQty = reader.GetNullableString("MinQty"),
                                MaxQty = reader.GetNullableString("MaxQty"),
                                VolumeMultiplier = reader.GetNullableString("VolumeMultiplier"),
                                VolumeModifier = reader.GetNullableString("VolumeModifier"),
                                SpreadMultiplier = reader.GetNullableString("SpreadMultiplier"),
                                SpreadModifier = reader.GetNullableString("SpreadModifier"),
                                BidPositiveSlippage = reader.GetNullableString("BidPositiveSlippage"),
                                BidNegativeSlippage = reader.GetNullableString("BidNegativeSlippage"),
                                UpdatedBy = reader.GetNullableString("UpdatedBy"),
                                UpdatedDate = reader.GetNullableString("UpdatedDate"),
                                CreatedBy = reader.GetNullableString("CreatedBy"),
                                CreatedDate = reader.GetNullableString("CreatedDate"),
                                AskNegativeSlippage = reader.GetNullableString("AskNegativeSlippage"),
                                AskPositiveSlippage = reader.GetNullableString("AskPositiveSlippage"),
                                OutOfBoundMode = reader.GetNullableString("OutOfBoundMode"),
                                SlippageMode = reader.GetNullableString("SlippageMode"),
                                ExecuteAtTOB = reader.GetNullableString("ExecuteAtTOB"),
                                INPUT_TIME = reader.GetNullableString("INPUT_TIME"),
                                REGION = reader.GetNullableString("REGION")

                            };
                            executionprofiles_list.Add(executionprofiles);
                        }
                    }
                }
            }
            return executionprofiles_list;
        }

        //------------------------------------marketinformation---------------------------------------------------
        public List<Pe_setting.marketinformation> MarketInformation_Get(List<string> table_name, List<string> regions,
    List<string> symbols, string dateTime)
        {
            List<Pe_setting.marketinformation> marketinfo_list = new List<Pe_setting.marketinformation>();

            string regionFilter = regions != null && regions.Any() ? string.Join("|", regions) : string.Empty;
            string symbolFilter = symbols != null && symbols.Any() ? string.Join("|", symbols) : string.Empty;

            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();




                //command.Parameters.AddWithValue("@region", regionFilter); // e.g., "NY" or "NY|TY"
                //command.Parameters.AddWithValue("@date", dateTime);


                //if (symbols[0] != "ALL")
                //{
                //    command.Parameters.AddWithValue("@symbol", symbolFilter);
                //}



                var query = "";

                //判斷是否為ALL
                if (symbols[0] == "ALL")
                {
                    query = @"
                            SELECT a.*,b.SymbolGroupName FROM pe_setting.marketinformation a 
                            left join pe_setting.symbolgroups b
                            on a.SymbolGroupId = b.Id
                            and a.REGION = b.REGION
                            and a.INPUT_TIME = b.INPUT_TIME
                            WHERE 1=1
                            AND a.REGION REGEXP @region
                            AND a.INPUT_TIME REGEXP @date
                            AND a.MarketStatus =1 
                            ";
                }
                else
                {
                    query = @"
                           SELECT * FROM (SELECT CONCAT(IFNULL(FeederSource, ''), '-', IFNULL(Symbol, '')) AS Symbol_Name,cc.* ,b.SymbolGroupName
                            FROM pe_setting.marketinformation cc 
                            left join pe_setting.symbolgroups b
                            on cc.SymbolGroupId = b.Id
                            and cc.REGION = b.REGION
                            and cc.INPUT_TIME = b.INPUT_TIME
                            WHERE 1=1 
                            AND cc.REGION REGEXP @region
                            AND cc.INPUT_TIME REGEXP @date
                            AND cc.MarketStatus = 1) a
                            where 1=1 
                            and a.Symbol_Name regexp @symbol;
                            ";
                }
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@region", regionFilter); // e.g., "NY" or "NY|TY"
                    command.Parameters.AddWithValue("@date", dateTime);


                    if (symbols[0] != "ALL")
                    {
                        command.Parameters.AddWithValue("@symbol", symbolFilter);
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var marketinfo = new Pe_setting.marketinformation
                            {
                                Name = reader.GetNullableString("Symbol"),
                                MarketId = reader.GetNullableString("MarketId"),
                                MarketStatus = reader.GetNullableString("MarketStatus"),
                                FeederSource = reader.GetNullableString("FeederSource"),
                                MarketName = reader.GetNullableString("MarketName"),
                                Description = reader.GetNullableString("Description"),
                                Symbol = reader.GetNullableString("Symbol"),
                                PricingDP = reader.GetNullableString("PricingDP"),
                                AmountDP = reader.GetNullableString("AmountDP"),
                                ExchangeName = reader.GetNullableString("ExchangeName"),
                                TradableDays = reader.GetNullableString("TradableDays"),
                                MarketOpenTime = reader.GetNullableString("MarketOpenTime"),
                                MarketCloseTime = reader.GetNullableString("MarketCloseTime"),
                                AssetType = reader.GetNullableString("AssetType"),
                                MarketScope = reader.GetNullableString("MarketScope"),
                                PricingEngine = reader.GetNullableString("PricingEngine"),
                                ExpirationDateTime = reader.GetNullableString("ExpirationDateTime"),
                                RequireVolumeData = reader.GetNullableString("RequireVolumeData"),
                                MarketTimeZone = reader.GetNullableString("MarketTimeZone"),
                                UpdatedBy = reader.GetNullableString("UpdatedBy"),
                                UpdatedDate = reader.GetNullableString("UpdatedDate"),
                                CreatedBy = reader.GetNullableString("CreatedBy"),
                                CreatedDate = reader.GetNullableString("CreatedDate"),
                                SymbolGroupId = reader.GetNullableString("SymbolGroupId"),
                                TickPerSecond = reader.GetNullableString("TickPerSecond"),
                                PriceMultiplier = reader.GetNullableString("PriceMultiplier"),
                                VolumeMultiplier = reader.GetNullableString("VolumeMultiplier"),
                                AskSpread = reader.GetNullableString("AskSpread"),
                                BidSpread = reader.GetNullableString("BidSpread"),
                                INPUT_TIME = reader.GetNullableString("INPUT_TIME"),
                                REGION = reader.GetNullableString("REGION"),
                                SymbolGroupName = reader.GetNullableString("SymbolGroupName"),
                            };

                            marketinfo_list.Add(marketinfo);
                        }
                    }
                }
            }

            return marketinfo_list;
        }
        //----------------------------Pricestream_Get-----------------------------------------------------------
        public List<Pe_setting.pricestream> Pricestream_Get(List<string> table_name, List<string> regions,
    List<string> symbols, string dateTime)
        {
            List<Pe_setting.pricestream> pricestream_list = new List<Pe_setting.pricestream>();

            string regionFilter = regions != null && regions.Any() ? string.Join("|", regions) : string.Empty;

            string symbolFilter = symbols != null && symbols.Any() ? string.Join("|", symbols) : string.Empty;

            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();

                var query = "";

                if (symbols[0] == "ALL")
                {

                    //command.Parameters.AddWithValue("@region", regionFilter); // e.g., "NY" or "NY|TY"
                    //command.Parameters.AddWithValue("@date", dateTime);

                    //if (symbols[0] != "ALL")
                    //{
                    //    command.Parameters.AddWithValue("@symbol", symbolFilter);
                    //}

                    query = @"
                            WITH ranked_data AS (
                            select a.*,b.VolumeBandName from 
                            (SELECT 
                            psc.*,
                            ps.PriceStreamProfileName,
                            CONCAT(IFNULL(ps.PriceStreamProfileName, ''), '-', IFNULL(psc.Symbol, '')) AS Symbol_Name,
                            ROW_NUMBER() OVER (PARTITION BY psc.VolumeBandConfigurationId ORDER BY psc.UpdatedDate DESC, psc.CreatedDate DESC) AS rn
                            FROM 
                            pe_setting.PriceStreamProfileConfig AS psc
                            LEFT JOIN 
                            pe_setting.PriceStreamProfile AS ps
                            ON psc.PricingStreamProfileId = ps.Id
                            WHERE 
                            ps.IsActive = 'true'
                            AND psc.REGION REGEXP @region
                            AND ps.REGION REGEXP  @region
                            AND psc.INPUT_TIME REGEXP @date
                            AND ps.INPUT_TIME REGEXP @date 
                            AND psc.REGION = ps.REGION
                            ) a 
                            left join 
                            pe_setting.volumebandconfiguration b 
                            on a.VolumeBandConfigurationId = b.Id
                            and a.REGION = b.REGION
                            AND b.REGION REGEXP  @region
                            AND b.INPUT_TIME REGEXP @date
                            )
                            SELECT *
                            FROM ranked_data
                            WHERE rn = 1;
                            ";

                }
                else
                {
                    query = @"
                        WITH ranked_data AS (
                        select a.*,b.VolumeBandName from 
                        (SELECT 
                        psc.*,
                        ps.PriceStreamProfileName,
                        CONCAT(IFNULL(ps.PriceStreamProfileName, ''), '-', IFNULL(psc.Symbol, '')) AS Symbol_Name,
                        ROW_NUMBER() OVER (PARTITION BY psc.VolumeBandConfigurationId ORDER BY psc.UpdatedDate DESC, psc.CreatedDate DESC) AS rn
                        FROM 
                        pe_setting.PriceStreamProfileConfig AS psc
                        LEFT JOIN 
                        pe_setting.PriceStreamProfile AS ps
                        ON psc.PricingStreamProfileId = ps.Id
                        WHERE 
                        ps.IsActive = 'true'
                        AND psc.REGION REGEXP @region
                        AND ps.REGION REGEXP  @region
                        AND psc.INPUT_TIME REGEXP @date
                        AND ps.INPUT_TIME REGEXP @date 
                        AND psc.REGION = ps.REGION
                        ) a 
                        left join 
                        pe_setting.volumebandconfiguration b 
                        on a.VolumeBandConfigurationId = b.Id
                        and a.REGION = b.REGION
                        AND b.REGION REGEXP  @region
                        AND b.INPUT_TIME REGEXP @date
                        )
                        SELECT *
                        FROM ranked_data
                        WHERE rn = 1 
                        AND Symbol_Name REGEXP @symbol;
                        ";
                }

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@region", regionFilter); // e.g., "NY" or "NY|TY"
                    command.Parameters.AddWithValue("@date", dateTime);

                    if (symbols[0] != "ALL")
                    {
                        command.Parameters.AddWithValue("@symbol", symbolFilter);
                    }


                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var pricestream = new Pe_setting.pricestream
                            {
                                Name = reader.GetNullableString("Symbol"),
                                PriceStreamProfileName = reader.GetNullableString("PriceStreamProfileName"),
                                MarketId = reader.GetNullableString("MarketId"),
                                Symbol = reader.GetNullableString("Symbol"),
                                PricingStreamProfileId = reader.GetNullableString("PricingStreamProfileId"),
                                PriceFilterGroupIds = reader.GetNullableString("PriceFilterGroupIds"),
                                VolumeBandConfigurationId = reader.GetNullableString("VolumeBandConfigurationId"),
                                MappingName = reader.GetNullableString("MappingName"),
                                ProfileTimeZone = reader.GetNullableString("ProfileTimeZone"),
                                TimeRange = reader.GetNullableString("TimeRange"),
                                CoreSpread = reader.GetNullableString("CoreSpread"),
                                MaxSpread = reader.GetNullableString("MaxSpread"),
                                MinSpread = reader.GetNullableString("MinSpread"),
                                ThrottlePerSecond = reader.GetNullableString("ThrottlePerSecond"),
                                ExpiryInSecond = reader.GetNullableString("ExpiryInSecond"),
                                UpdatedBy = reader.GetNullableString("UpdatedBy"),
                                // UpdatedDate = reader.GetNullableString("UpdatedDate"), // 已註解
                                CreatedBy = reader.GetNullableString("CreatedBy"),
                                // CreatedDate = reader.GetNullableString("CreatedDate"), // 已註解
                                INPUT_TIME = reader.GetNullableString("INPUT_TIME"),
                                REGION = reader.GetNullableString("REGION"),
                                VolumeBandName = reader.GetNullableString("VolumeBandName")
                            };
                            pricestream_list.Add(pricestream);
                        }
                    }
                }
            }
            return pricestream_list;
        }



        //-------------------
        public List<Pe_setting.volumeband> Volumeband_Get(List<string> table_name, List<string> regions,
    List<string> symbols, string dateTime)
        {
            List<Pe_setting.volumeband> volumeband_list = new List<Pe_setting.volumeband>();

            string regionFilter = regions != null && regions.Any() ? string.Join("|", regions) : string.Empty;
            string symbolFilter = symbols != null && symbols.Any() ? string.Join("|", symbols) : string.Empty;

            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();

                var query = "";
                if (symbols[0] == "ALL")
                {

                    query = @"
                    select b.* from 
                    (
                    SELECT distinct VolumeBandConfigurationId,REGION FROM pe_setting.pricestreamprofileconfig
                    where 1=1 
                    AND REGION regexp @region
                    AND INPUT_TIME regexp @date
                    ) a
                    left join 
                    (
                    SELECT  vbcg.TimeZone,
                      vbcg.StartTime,
                      vbcg.EndTime,
                      vbcg.SpreadMode,
                      vbcg.MinSpread,
                      vbcg.MaxSpread,
                      vbcg.FixSpread,
                      vbcg.EnableOverflow,
                      vbcg.ApplyVWAP,
                      vbcg.Multiplier,
                      vbcg.VolumebandLayer,
                      vbcg.VolumeBandConfigurationId,vbc.* FROM pe_setting.VolumeBandConfiguration vbc 
                    inner join pe_setting.VolumeBandConfig vbcg
                    on vbc.Id = vbcg.VolumeBandConfigurationId and vbc.REGION = vbcg.REGION
                    WHERE 1=1 
                    AND vbc.IsActive = 'true'
                    AND vbc.INPUT_TIME regexp @date
                    AND vbcg.INPUT_TIME regexp @date
                    AND vbc.REGION regexp @region
                    AND vbcg.REGION regexp @region
                    ) b
                    on 
                    a.VolumeBandConfigurationId = b.id 
                    AND a.REGION = b.REGION
                    WHERE 1=1 
                    AND b.VolumeBandName IS NOT NULL
                    AND b.VolumeBandName != '';";

                }
                else
                {
                    query = @"
                   select b.* from 
                    (
                    SELECT distinct VolumeBandConfigurationId,REGION FROM pe_setting.pricestreamprofileconfig
                    where 1=1 
                    AND REGION regexp @region
                    AND INPUT_TIME regexp @date
                    ) a
                    left join 
                    (
                    SELECT  vbcg.TimeZone,
                      vbcg.StartTime,
                      vbcg.EndTime,
                      vbcg.SpreadMode,
                      vbcg.MinSpread,
                      vbcg.MaxSpread,
                      vbcg.FixSpread,
                      vbcg.EnableOverflow,
                      vbcg.ApplyVWAP,
                      vbcg.Multiplier,
                      vbcg.VolumebandLayer,
                      vbcg.VolumeBandConfigurationId,vbc.* FROM pe_setting.VolumeBandConfiguration vbc 
                    inner join pe_setting.VolumeBandConfig vbcg
                    on vbc.Id = vbcg.VolumeBandConfigurationId and vbc.REGION = vbcg.REGION
                    WHERE 1=1 
                    AND vbc.IsActive = 'true'
                    AND vbc.INPUT_TIME regexp  @date
                    AND vbcg.INPUT_TIME regexp  @date
                    AND vbc.REGION regexp @region
                    AND vbcg.REGION regexp @region
                    ) b
                    on 
                    a.VolumeBandConfigurationId = b.id 
                    AND a.REGION = b.REGION
                    WHERE 1=1 
                    AND b.VolumeBandName regexp @symbol
                    AND b.VolumeBandName IS NOT NULL
                    AND b.VolumeBandName != '';";
                }

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@region", regionFilter);
                    command.Parameters.AddWithValue("@date", dateTime);

                    if (symbols[0] != "ALL")
                    {
                        command.Parameters.AddWithValue("@symbol", symbolFilter);
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var volumeband = new Pe_setting.volumeband
                            {
                                // Volume Band 基本資料
                                Id = reader.GetNullableString("Id"),
                                VolumeBandName = reader.GetNullableString("VolumeBandName"),
                                Description = reader.GetNullableString("Description"),
                                IsActive = reader.GetNullableString("IsActive"),

                                // Volume Band 建立與更新資訊
                                UpdatedBy = reader.GetNullableString("UpdatedBy"),
                                // UpdatedDate = reader.GetNullableString("UpdatedDate"),
                                CreatedBy = reader.GetNullableString("CreatedBy"),
                                // CreatedDate = reader.GetNullableString("CreatedDate"),
                                InputTime = reader.GetNullableString("INPUT_TIME"),
                                Region = reader.GetNullableString("REGION"),

                                // Spread 設定資訊
                                TimeZone = reader.GetNullableString("TimeZone"),
                                StartTime = reader.GetNullableString("StartTime"),
                                EndTime = reader.GetNullableString("EndTime"),
                                SpreadMode = reader.GetNullableString("SpreadMode"),
                                MinSpread = reader.GetNullableString("MinSpread"),
                                MaxSpread = reader.GetNullableString("MaxSpread"),
                                FixSpread = reader.GetNullableString("FixSpread"),
                                EnableOverflow = reader.GetNullableString("EnableOverflow"),
                                ApplyVWAP = reader.GetNullableString("ApplyVWAP"),
                                Multiplier = reader.GetNullableString("Multiplier"),
                                VolumeBandLayer = reader.GetNullableString("VolumebandLayer"),
                                VolumeBandConfigurationId = reader.GetNullableString("VolumeBandConfigurationId"),

                                // Spread 設定建立與更新資訊
                                UpdatedBy2 = reader.GetNullableString("UpdatedBy"),
                                // UpdatedDate2 = reader.GetNullableString("UpdatedDate"),
                                CreatedBy2 = reader.GetNullableString("CreatedBy"),
                                // CreatedDate2 = reader.GetNullableString("CreatedDate"),
                                InputTime2 = reader.GetNullableString("INPUT_TIME"),
                                Region2 = reader.GetNullableString("REGION")
                            };

                            volumeband_list.Add(volumeband);
                        };

                    }

                }
            }

            return volumeband_list;
        }




        public static List<object> GetRegionComparisonResult<T>(
    List<T> itemList,
    Func<T, string> nameSelector,
    string[] allRegions,
    string[] excludeProps,
    string[] excludeReturnProps
)
        {
            var regionGroups = itemList
                .GroupBy(x => typeof(T).GetProperty("REGION")?.GetValue(x)?.ToString())
                .Where(g => g.Key != null)
                .ToDictionary(
                    g => g.Key!,
                    g => g.ToDictionary(nameSelector, i => i)
                );

            var propertyNames = typeof(T).GetProperties().Select(p => p.Name).ToList();
            var allNames = itemList.Select(nameSelector).Distinct();

            var result = allNames.Select(name =>
            {
                var columns = new Dictionary<string, object>();
                var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                foreach (var region in allRegions)
                {
                    if (regionGroups.TryGetValue(region, out var group) && group.TryGetValue(name, out var regionItem))
                    {
                        var data = new Dictionary<string, object>();
                        foreach (var propName in propertyNames)
                        {
                            var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem);
                            if (!excludeReturnProps.Contains(propName))
                                columns[$"{propName}{region}"] = value;
                            if (!excludeProps.Contains(propName))
                                data[propName] = value;
                        }
                        regionColumnData[region] = data;
                    }
                    else
                    {
                        foreach (var propName in propertyNames)
                        {
                            if (!excludeReturnProps.Contains(propName))
                                columns[$"{propName}{region}"] = null;
                        }
                    }
                }

                bool allRegionsIdentical = regionColumnData.Count > 1 &&
                    regionColumnData.Values
                        .Select(dict => string.Join(";", dict.OrderBy(k => k.Key).Select(kv => $"{kv.Key}:{kv.Value}")))
                        .Distinct()
                        .Count() == 1;

                return allRegionsIdentical ? null : new
                {
                    Name = name,
                    Columns = columns
                };
            }).Where(x => x != null).Cast<object>().ToList();

            return result;
        }

        // 通用方法
        public object ProcessRegionData<T>(List<T> dataList, string[] excludeProps, string[] excludeReturnProps)
        {
            var regionGroups = dataList
                .GroupBy(x => x.GetType().GetProperty("REGION")?.GetValue(x)?.ToString())
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(
                        i => i.GetType().GetProperty("Name")?.GetValue(i)?.ToString(),
                        i => i
                    )
                );

            var allRegions = new[] { "NY", "LD", "TY" };
            var activeRegions = allRegions.ToList();
            var propertyNames = typeof(T).GetProperties().Select(p => p.Name).ToList();

            // 取得所有的名稱
            var allNames = dataList
                .Select(x => x.GetType().GetProperty("Name")?.GetValue(x)?.ToString())
                .Distinct();

            var result = allNames.Select(name =>
            {
                var regionColumnData = new Dictionary<string, Dictionary<string, object>>();

                foreach (var region in activeRegions)
                {
                    if (regionGroups.ContainsKey(region) && regionGroups[region].TryGetValue(name, out var regionItem))
                    {
                        var data = new Dictionary<string, object>();

                        foreach (var propName in propertyNames)
                        {
                            if (!excludeProps.Contains(propName))
                            {
                                var value = regionItem.GetType().GetProperty(propName)?.GetValue(regionItem);

                                // ⭐ 如果是 AggregationRule 且 dataList 來源為 out_.AggregationconfigList，進行 JSON 處理
                                if (typeof(T).Name == "aggregationconfig" && propName == "AggregationRule" && value is string json && !string.IsNullOrWhiteSpace(json))
                                {
                                    try
                                    {
                                        var dictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                                        foreach (var dict in dictList)
                                        {
                                            dict.Remove("SymbolId");
                                        }

                                        var cleanedJson = JsonConvert.SerializeObject(dictList);
                                        data[propName] = cleanedJson;
                                    }
                                    catch
                                    {
                                        data[propName] = value; // fallback
                                    }
                                }
                                else
                                {
                                    data[propName] = value;
                                }
                            }
                        }

                        regionColumnData[region] = data;
                    }
                }

                // 比較欄位的差異
                var diffColumns = new Dictionary<string, object>();

                foreach (var prop in propertyNames)
                {
                    if (excludeReturnProps.Contains(prop)) continue;

                    var valuesByRegion = regionColumnData
                        .Where(r => r.Value.ContainsKey(prop))
                        .ToDictionary(r => r.Key, r => r.Value[prop]);

                    if (valuesByRegion.Values.Distinct().Count() > 1)
                    {
                        foreach (var region in activeRegions)
                        {
                            valuesByRegion.TryGetValue(region, out var value);
                            diffColumns[$"{prop}{region}"] = value;
                        }
                    }
                }

                return diffColumns.Any() ? new
                {
                    Name = name,
                    Columns = diffColumns
                } : null;

            }).Where(x => x != null).ToList<object>();

            return result;
        }



        //因為它不算是類別--------------------------------------------------------------------------
        //NY、LD、TY類型處理
        public static Dictionary<string, List<object>> GroupByRegion(List<object> sourceList)
        {
            var regionGroups = new Dictionary<string, List<object>>();

            foreach (var region in new[] { "NY", "LD", "TY" })
            {
                regionGroups[region] = sourceList
                    .Where(item => GetRegion(item) == region)
                    .ToList();
            }

            return regionGroups;
        }

        private static string GetRegion(object item)
        {
            var prop = item.GetType().GetProperty("REGION");
            return prop?.GetValue(item)?.ToString();
        }

        public List<Dictionary<string, object>> MergeConfigByRegion(List<Pe_setting.aggregationconfig> configs)
        {
            var nyItems = configs.Where(x => x.REGION == "NY").ToList();
            var ldItems = configs.Where(x => x.REGION == "LD").ToList();
            var tyItems = configs.Where(x => x.REGION == "TY").ToList();

            var allNames = nyItems.Select(x => x.Name)
                .Union(ldItems.Select(x => x.Name))
                .Union(tyItems.Select(x => x.Name))
                .Distinct();

            var props = typeof(Pe_setting.aggregationconfig).GetProperties()
                .Where(p => p.Name != "REGION") // 不需要 REGION
                .ToList();

            var result = new List<Dictionary<string, object>>();

            foreach (var name in allNames)
            {
                var row = new Dictionary<string, object>();
                row["Name"] = name;

                var ny = nyItems.FirstOrDefault(x => x.Name == name);
                var ld = ldItems.FirstOrDefault(x => x.Name == name);
                var ty = tyItems.FirstOrDefault(x => x.Name == name);

                foreach (var prop in props)
                {
                    if (prop.Name == "Name") continue;

                    row[$"{prop.Name}(NY)"] = ny != null ? prop.GetValue(ny) : null;
                    row[$"{prop.Name}(LD)"] = ld != null ? prop.GetValue(ld) : null;
                    row[$"{prop.Name}(TY)"] = ty != null ? prop.GetValue(ty) : null;
                }

                result.Add(row);
            }

            return result;
        }



        //new code Benson 202509

        public (List<New_Pe_setting.model_markets_symbol>,
              List<New_Pe_setting.source_markets_symbol>,
              List<New_Pe_setting.price_stream>,
              List<New_Pe_setting.aggregator_markets>,
              List<New_Pe_setting.aggregator_markets_rule>,
              List<New_Pe_setting.execution_profiles>,
              List<New_Pe_setting.volume_band>
            ) New_SQL_list_symbol()
        {
            var model_markets_symbolList = new List<New_Pe_setting.model_markets_symbol>();
            var source_markets_symbolList = new List<New_Pe_setting.source_markets_symbol>();
            var price_streamList = new List<New_Pe_setting.price_stream>();
            var aggregator_marketsList = new List<New_Pe_setting.aggregator_markets>();
            var aggregator_markets_ruleList = new List<New_Pe_setting.aggregator_markets_rule>();
            var execution_profilesList = new List<New_Pe_setting.execution_profiles>();
            var volume_bandList = new List<New_Pe_setting.volume_band>();



            //var volumebandList = new List<Pe_setting.volumeband>();

            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();

                // 通用查詢方法：query、建構函式、存入目標 list
                void FetchNames<T>(string query, Func<string, T> createItem, List<T> targetList)
                {
                    using var command = new MySqlCommand(query, connection);
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var name = reader.IsDBNull(0) ? null : reader.GetString(0);
                        targetList.Add(createItem(name));
                    }
                }

                //取的現在時間
                string endOfDay = DateTime.Now
                .Date
                .AddDays(1)
                .AddSeconds(-1)
                .ToString("yyyy-MM-dd HH:mm:ss");


                //model_markets_symbol
                FetchNames(
                    $@"
                    SELECT distinct Symbol FROM pe_records.ModelMarkets 
                    WHERE 1 = 1 
                    AND IsActive = 'True' 
                    AND INPUT_TIME = '{endOfDay}';
                    ",
                    name => new New_Pe_setting.model_markets_symbol { Symbol = name },
                    model_markets_symbolList);

                //source_markets_symbol
                FetchNames(
                    $@"
                    SELECT distinct CONCAT(FeederSource, '-', Symbol) as `Name` FROM pe_records.SourceMarkets 
                    WHERE 1=1 
                    AND IsActive = 'TRUE' 
                    AND INPUT_TIME = '{endOfDay}';",
                    name => new New_Pe_setting.source_markets_symbol { Name = name },
                    source_markets_symbolList);

                //Price Stream

                FetchNames(
                    $@"
                    SELECT DISTINCT CONCAT(a.Name, '-', a.Symbol) AS Name
                    FROM (
                    SELECT 
                    ps.Name,
                    mm.Symbol,
                    psm.*
                    FROM pe_records.PriceStreams ps
                    LEFT JOIN pe_records.PriceStreamMarkets psm 
                        ON ps.Id = psm.PriceStreamId 
                        AND ps.INPUT_TIME = psm.INPUT_TIME 
                        AND ps.REGION = psm.REGION 
                        AND ps.INPUT_TIME = '{endOfDay}' 
                        AND psm.INPUT_TIME = '{endOfDay}'
                    LEFT JOIN pe_records.ModelMarkets mm 
                        ON psm.ModelMarketId = mm.Id 
                        AND psm.INPUT_TIME = mm.INPUT_TIME 
                        AND psm.REGION = mm.REGION
                        AND psm.INPUT_TIME = '{endOfDay}' 
                        AND mm.INPUT_TIME = '{endOfDay}' 
                    WHERE 1=1 
                        AND ps.IsActive = 'TRUE'
                        AND ps.INPUT_TIME = '{endOfDay}' 
                    ) a
                        ",
                    name => new New_Pe_setting.price_stream { Name = name },
                    price_streamList);

                // Aggregator Markets參數

                FetchNames(
                    $@"
                    SELECT distinct `Name` FROM pe_records.AggregationMarkets
                    WHERE 1=1 
                    AND IsActive = 'TRUE' 
                    AND INPUT_TIME = '{endOfDay}';",
                    name => new New_Pe_setting.aggregator_markets { Name = name },
                    aggregator_marketsList);


                //  Aggregator Markets Rule參數
                FetchNames(
                    $@"
                    SELECT DISTINCT CONCAT(a.Name, '-', a.Priority) AS Name
                    FROM (
                    SELECT 
                    am.Name,
                    amr.Priority,
                    sm.Symbol
                    FROM pe_records.AggregationMarkets am
                    LEFT JOIN pe_records.AggregationMarketRules amr
                        ON am.Id = amr.AggregationMarketId 
                        AND am.INPUT_TIME = amr.INPUT_TIME 
                        AND am.REGION = amr.REGION
                        AND am.INPUT_TIME = '{endOfDay}' 
                        AND amr.INPUT_TIME = '{endOfDay}' 
                    LEFT JOIN pe_records.SourceMarkets sm
                        ON amr.SourceMarketId = sm.Id 
                        AND amr.INPUT_TIME = sm.INPUT_TIME 
                        AND amr.REGION = sm.REGION
                        AND amr.INPUT_TIME = '{endOfDay}' 
                        AND sm.INPUT_TIME = '{endOfDay}' 
                    WHERE 1=1 
                        AND am.IsActive = 'TRUE'
                    ) a
                    WHERE 1=1 
                        AND a.Name IS NOT NULL 
                        AND a.Priority IS NOT NULL;",
                    name => new New_Pe_setting.aggregator_markets_rule { Name = name },
                    aggregator_markets_ruleList);


                //Execution Profile參數

                FetchNames(
                    $@"
                    SELECT distinct `Name` 
                    FROM pe_records.ExecutionProfiles 
                    WHERE 1=1 
                    AND INPUT_TIME = '{endOfDay}' ;
                    ",
                  name => new New_Pe_setting.execution_profiles { Name = name },
                 execution_profilesList);

                //Volume Band參數

                FetchNames(
                    $@"
                    SELECT distinct vb.`Name`
                    FROM pe_records.VolumeBands vb
                    LEFT JOIN pe_records.VolumeBandSchedules vbs
                        ON vb.Id = vbs.VolumeBandId 
                        AND vb.INPUT_TIME = vbs.INPUT_TIME
                        AND vb.INPUT_TIME = '{endOfDay}' 
                        AND vbs.INPUT_TIME = '{endOfDay}' 
                    WHERE 1=1 
                        AND vb.IsActive = 'TRUE'
                         
                        ;",
                  name => new New_Pe_setting.volume_band { Name = name },
                 volume_bandList);


            }

            return (model_markets_symbolList, source_markets_symbolList, price_streamList,
                aggregator_marketsList, aggregator_markets_ruleList,
                execution_profilesList, volume_bandList);

        }



        //  pe_Level_Setting.ExecuteSQL_serverform(interests, skills,time_comp);
        //開始調用的function
        public New_Pe_setting.SymbolSettingViewModel New_ExecuteSQL_serverform(List<string> table_name, List<string> regions,
            List<string> symbols, string dateTime)
        {

            //先判斷是否為一個，如如果不適一個的話

            var result = new New_Pe_setting.SymbolSettingViewModel();

            if (table_name == null || table_name.Count == 0)
            {
                Console.WriteLine("table_name 為空");
                return result;
            }
            Debug.WriteLine(table_name);
            foreach (var table in table_name)
            {
                switch (table.ToLower())
                {
                    case "model_markets_symbol":
                        result.model_markets_symbolList ??= new List<model_markets_symbol>();
                        result.model_markets_symbolList.AddRange(
                            Model_markets_symbol_Get(new List<string> { table }, regions, symbols, dateTime)
                        );
                        break;

                    case "source_markets_symbol":
                        result.source_markets_symbolList ??= new List<source_markets_symbol>();
                        result.source_markets_symbolList.AddRange(
                            Source_markets_symbol_Get(new List<string> { table }, regions, symbols, dateTime)
                        );
                        break;

                    case "price_stream":
                        result.price_streamList ??= new List<price_stream>();
                        result.price_streamList.AddRange(
                           Price_stream_Get(new List<string> { table }, regions, symbols, dateTime)
                        );
                        break;

                    case "aggregator_markets":
                        result.aggregator_marketsList ??= new List<aggregator_markets>();
                        result.aggregator_marketsList.AddRange(
                            Aggregator_markets_Get(new List<string> { table }, regions, symbols, dateTime)
                        );
                        break;


                    case "aggregator_markets_rule":
                        result.aggregator_markets_ruleList ??= new List<aggregator_markets_rule>();
                        result.aggregator_markets_ruleList.AddRange(
                            Aggregator_markets_rule_Get(new List<string> { table }, regions, symbols, dateTime)
                        );
                        break;

                    // new data 
                    case "execution_profiles":
                        result.execution_profilesList ??= new List<execution_profiles>();
                        result.execution_profilesList.AddRange(
                           Execution_profiles_Get(new List<string> { table }, regions, symbols, dateTime)
                        );
                        break;


                    case "volume_band":
                        result.volume_bandList ??= new List<volume_band>();
                        result.volume_bandList.AddRange(
                           Volume_band_Get(new List<string> { table }, regions, symbols, dateTime)
                        );
                        break;

                    default:
                        Console.WriteLine($"未知的資料表: {table}");
                        break;
                }
            }

            return result;
        }

        //-------------------------------------------------



        //未使用泛型
        public List<New_Pe_setting.model_markets_symbol> Model_markets_symbol_Get(List<string> table_name, List<string> regions,
    List<string> symbols, string dateTime)
        {
            List<New_Pe_setting.model_markets_symbol> model_markets_symbolList = new List<New_Pe_setting.model_markets_symbol>();


            string regionFilter = regions != null && regions.Any() ? string.Join("|", regions) : string.Empty;
            string symbolFilter = symbols != null && symbols.Any() ? string.Join("|", symbols) : string.Empty;

            //sql date(要先塞選他需要那些地區)
            using (var connection = new MySqlConnection(_connectionString))
            {

                connection.Open();
                var query = @"
                            SELECT * FROM pe_records.ModelMarkets 
                            WHERE 1 = 1
                            AND IsActive = 'True'
                            AND REGION REGEXP @region 
                            AND INPUT_TIME REGEXP @date 
                            ";

                if (symbols[0] != "ALL")
                {
                    query += "AND `Symbol` = @symbol";
                }


                query += ";";

                using (var command = new MySqlCommand(query, connection))
                {

                    command.Parameters.AddWithValue("@region", regionFilter); // e.g., "NY" or "NY|TY"
                    command.Parameters.AddWithValue("@date", dateTime);

                    if (symbols[0] == "ALL")
                    { }
                    else
                    {
                        command.Parameters.AddWithValue("@symbol", symbolFilter);
                    }

                    int count = Convert.ToInt32(command.ExecuteScalar());

                    using (var reader = command.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            New_Pe_setting.model_markets_symbol model_markets_symbol = new New_Pe_setting.model_markets_symbol()
                            {
                                Id = reader.GetInt32Nullable("Id") ?? 0,  // 如果是主鍵，可用 ?? 0
                                Name = reader.GetNullableString("Name"),
                                FeederSource = reader.GetNullableString("FeederSource"),
                                Symbol = reader.GetNullableString("Symbol"),
                                AssetType = reader.GetNullableString("AssetType"),
                                IsActive = reader.GetBooleanNullable("IsActive") ?? false,
                                PriceDecimalPlaces = reader.GetInt32Nullable("PriceDecimalPlaces"),
                                VolumeDecimalPlaces = reader.GetInt32Nullable("VolumeDecimalPlaces"),
                                SymbolGroupId = reader.GetInt32Nullable("SymbolGroupId"),
                                InputTime = reader.GetDateTimeNullable("INPUT_TIME"),
                                Region = reader.GetNullableString("REGION"),


                            };
                            model_markets_symbolList.Add(model_markets_symbol);
                        }
                    }
                }
            }
            return model_markets_symbolList;
        }



        public List<New_Pe_setting.source_markets_symbol> Source_markets_symbol_Get(List<string> table_name, List<string> regions,
List<string> symbols, string dateTime)
        {
            List<New_Pe_setting.source_markets_symbol> source_markets_symbolList = new List<New_Pe_setting.source_markets_symbol>();


            string regionFilter = regions != null && regions.Any() ? string.Join("|", regions) : string.Empty;
            string symbolFilter = symbols != null && symbols.Any() ? string.Join("|", symbols) : string.Empty;

            //sql date(要先塞選他需要那些地區)
            using (var connection = new MySqlConnection(_connectionString))
            {

                connection.Open();
                var query = @"
                            SELECT * FROM pe_records.SourceMarkets 
                            WHERE 1 = 1
                            AND IsActive = 'True'
                            AND REGION REGEXP @region 
                            AND INPUT_TIME REGEXP @date 
                            ";

                if (symbols[0] != "ALL")
                {
                    query += "AND CONCAT(FeederSource, '-', Symbol) = @symbol";
                }


                query += ";";

                using (var command = new MySqlCommand(query, connection))
                {

                    command.Parameters.AddWithValue("@region", regionFilter); // e.g., "NY" or "NY|TY"
                    command.Parameters.AddWithValue("@date", dateTime);

                    if (symbols[0] == "ALL")
                    { }
                    else
                    {
                        command.Parameters.AddWithValue("@symbol", symbolFilter);
                    }

                    int count = Convert.ToInt32(command.ExecuteScalar());

                    using (var reader = command.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            New_Pe_setting.source_markets_symbol source_markets_symbol = new New_Pe_setting.source_markets_symbol()
                            {
                                Id = reader.GetInt64Nullable("Id") ?? 0,
                                FeederSource = reader.GetNullableString("FeederSource"),
                                Name = reader.GetNullableString("Name"),
                                Symbol = reader.GetNullableString("Symbol"),
                                IsActive = reader.GetBooleanNullable("IsActive") ?? false,
                                AssetType = reader.GetNullableString("AssetType"),
                                TimeZone = reader.GetNullableString("TimeZone"),
                                ThrottlePerSecond = reader.GetInt32Nullable("ThrottlePerSecond") ?? 0,
                                PriceMultiplier = reader.GetDecimalNullable("PriceMultiplier") ?? 1,
                                VolumeMultiplier = reader.GetDecimalNullable("VolumeMultiplier") ?? 1,
                                PriceDecimalPlaces = reader.GetInt32Nullable("PriceDecimalPlaces") ?? 2,
                                BidSpread = reader.GetDecimalNullable("BidSpread") ?? 0,
                                AskSpread = reader.GetDecimalNullable("AskSpread") ?? 0,
                                ExpirationDateTime = reader.GetNullableString("ExpirationDateTime"),
                                RequireVolumeData = reader.GetNullableString("RequireVolumeData"),
                                INPUT_TIME = reader.GetNullableString("INPUT_TIME"),
                                REGION = reader.GetNullableString("REGION")
                            };
                            source_markets_symbolList.Add(source_markets_symbol);
                        }
                    }
                }
            }
            return source_markets_symbolList;
        }

        public List<New_Pe_setting.price_stream> Price_stream_Get(List<string> table_name, List<string> regions,
List<string> symbols, string dateTime)
        {
            List<New_Pe_setting.price_stream> price_streamList = new List<New_Pe_setting.price_stream>();


            string regionFilter = regions != null && regions.Any() ? string.Join("|", regions) : string.Empty;
            string symbolFilter = symbols != null && symbols.Any() ? string.Join("|", symbols) : string.Empty;

            //sql date(要先塞選他需要那些地區)
            using (var connection = new MySqlConnection(_connectionString))
            {

                connection.Open();
                var query = @"
                          SELECT CONCAT(a.Name, '-', a.Symbol) AS 'Name',
                            a.Symbol,
                            a.Id,
                            a.ModelMarketId,
                            a.MappingName,
                            a.CoreSpread,
                            a.MinSpread,
                            a.MaxSpread,
                            a.ThrottlePerSecond,
                            a.MarketDepthValidSeconds,
                            a.VolumeBandId,
                            a.PriceStreamId,
                            a.INPUT_TIME,
                            a.REGION
                            FROM(
                            SELECT
                            ps.Name,
                            mm.Symbol,
                            psm.*
                            FROM pe_records.PriceStreams ps
                            LEFT JOIN pe_records.PriceStreamMarkets psm
                            ON ps.Id = psm.PriceStreamId
                            AND ps.INPUT_TIME = psm.INPUT_TIME
                            AND ps.REGION = psm.REGION
                            LEFT JOIN pe_records.ModelMarkets mm
                            ON psm.ModelMarketId = mm.Id
                            AND psm.INPUT_TIME = mm.INPUT_TIME
                            AND psm.REGION = mm.REGION
                            WHERE 1 = 1
                            AND ps.IsActive = 'TRUE'
                            ) a
                            WHERE 1=1 
                            AND a.REGION REGEXP @region 
                            AND a.INPUT_TIME REGEXP @date 
                            ";

                if (symbols[0] != "ALL")
                {
                    query += "AND CONCAT(a.Name, '-', a.Symbol) = @symbol";
                }


                query += ";";

                using (var command = new MySqlCommand(query, connection))
                {

                    command.Parameters.AddWithValue("@region", regionFilter); // e.g., "NY" or "NY|TY"
                    command.Parameters.AddWithValue("@date", dateTime);

                    if (symbols[0] == "ALL")
                    { }
                    else
                    {
                        command.Parameters.AddWithValue("@symbol", symbolFilter);
                    }

                    //int count = Convert.ToInt32(command.ExecuteScalar());

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try
                            {
                                var price_stream = new New_Pe_setting.price_stream()
                                {
                                    Id = reader.GetInt32Nullable("Id") ?? 0,
                                    Name = reader.GetNullableString("Name"),
                                    Symbol = reader.GetNullableString("Symbol"),
                                    ModelMarketId = reader.GetInt32Nullable("ModelMarketId"),
                                    MappingName = reader.GetNullableString("MappingName"),
                                    CoreSpread = reader.GetInt64Nullable("CoreSpread"),
                                    MinSpread = reader.GetInt32Nullable("MinSpread"),
                                    MaxSpread = reader.GetInt64Nullable("MaxSpread"),
                                    ThrottlePerSecond = reader.GetInt32("ThrottlePerSecond"),
                                    MarketDepthValidSeconds = reader.GetInt32Nullable("MarketDepthValidSeconds"),
                                    VolumeBandId = reader.GetInt32Nullable("VolumeBandId"),
                                    PriceStreamId = reader.GetInt32Nullable("PriceStreamId"),
                                    INPUT_TIME = reader.GetNullableString("INPUT_TIME"),  // 保留 string 避免 DateTime 轉型問題
                                    REGION = reader.GetNullableString("REGION")
                                };

                                price_streamList.Add(price_stream);
                            }
                            catch (Exception ex)
                            {
                                // 如果某欄位讀取失敗，印出錯誤欄位和值
                                Debug.WriteLine("Error reading row: " + ex.Message);
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    object val = reader.IsDBNull(i) ? null : reader[i];
                                    Debug.WriteLine($"Column {reader.GetName(i)} = {val}");
                                }
                                break;
                            }
                        }
                    }
                }
            }
            return price_streamList;
        }


        public List<New_Pe_setting.aggregator_markets> Aggregator_markets_Get(List<string> table_name, List<string> regions,
List<string> symbols, string dateTime)
        {
            List<New_Pe_setting.aggregator_markets> aggregator_marketsList = new List<New_Pe_setting.aggregator_markets>();


            string regionFilter = regions != null && regions.Any() ? string.Join("|", regions) : string.Empty;
            string symbolFilter = symbols != null && symbols.Any() ? string.Join("|", symbols) : string.Empty;

            //sql date(要先塞選他需要那些地區)
            using (var connection = new MySqlConnection(_connectionString))
            {

                connection.Open();
                var query = @"
                        SELECT * FROM pe_records.AggregationMarkets
                        WHERE 1=1 
                        AND IsActive = 'TRUE'
                        AND REGION REGEXP @region 
                        AND INPUT_TIME REGEXP @date 
                            ";

                if (symbols[0] != "ALL")
                {
                    query += "AND `Name` = @symbol";
                }


                query += ";";

                using (var command = new MySqlCommand(query, connection))
                {

                    command.Parameters.AddWithValue("@region", regionFilter); // e.g., "NY" or "NY|TY"
                    command.Parameters.AddWithValue("@date", dateTime);

                    if (symbols[0] == "ALL")
                    { }
                    else
                    {
                        command.Parameters.AddWithValue("@symbol", symbolFilter);
                    }

                    //int count = Convert.ToInt32(command.ExecuteScalar());

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try
                            {
                                var aggregatorMarket = new New_Pe_setting.aggregator_markets()
                                {
                                    Id = reader.GetInt32Nullable("Id") ?? 0,
                                    Name = reader.GetNullableString("Name")?.ToUpperInvariant(),
                                    AssetType = reader.GetNullableString("AssetType"),
                                    AggregationType = reader.GetNullableString("AggregationType"),
                                    IsActive = reader.GetNullableString("IsActive"),
                                    ClearNudgeWhenStarts = reader.GetNullableString("ClearNudgeWhenStarts"),
                                    FailoverInterval = reader.GetInt32Nullable("FailoverInterval"),
                                    NudgeCalculationKeepPriceIntervalSeconds = reader.GetInt32Nullable("NudgeCalculationKeepPriceIntervalSeconds"),
                                    NudgeCalculationIntervalSeconds = reader.GetInt32Nullable("NudgeCalculationIntervalSeconds"),
                                    NudgePersistInCacheMinutes = reader.GetInt32Nullable("NudgePersistInCacheMinutes"),
                                    IgnoreSourceInterval = reader.GetInt32Nullable("IgnoreSourceInterval"),
                                    AverageSpread = reader.GetInt32Nullable("AverageSpread"),
                                    NumberOfDecimalPlaces = reader.GetInt32Nullable("NumberOfDecimalPlaces"),
                                    SpreadVolatility = reader.GetNullableDouble("SpreadVolatility"),
                                    Throttle = reader.GetInt32Nullable("Throttle"),
                                    MaxFilterInterval = reader.GetInt32Nullable("MaxFilterInterval"),
                                    Sensitivity = reader.GetNullableDouble("Sensitivity"),
                                    TargetSpread = reader.GetInt32Nullable("TargetSpread"),
                                    Thresholds = reader.GetInt32Nullable("Thresholds"),
                                    TargetSpreadValues = reader.GetNullableString("TargetSpreadValues"),
                                    EnableSpreadBand = reader.GetNullableString("EnableSpreadBand"),
                                    FailoverInstantly = reader.GetNullableString("FailoverInstantly"),
                                    INPUT_TIME = reader.GetNullableString("INPUT_TIME"), // 保留 string 避免 DateTime 轉型問題
                                    REGION = reader.GetNullableString("REGION")
                                };

                                aggregator_marketsList.Add(aggregatorMarket);
                            }
                            catch (Exception ex)
                            {
                                // 如果某欄位讀取失敗，印出錯誤欄位和值
                                Debug.WriteLine("Error reading row: " + ex.Message);
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    object val = reader.IsDBNull(i) ? null : reader[i];
                                    Debug.WriteLine($"Column {reader.GetName(i)} = {val}");
                                }
                                break;
                            }
                        }
                    }
                }
            }
            return aggregator_marketsList;
        }





        public List<New_Pe_setting.aggregator_markets_rule> Aggregator_markets_rule_Get(List<string> table_name, List<string> regions,
List<string> symbols, string dateTime)
        {
            List<New_Pe_setting.aggregator_markets_rule> aggregator_markets_ruleList = new List<New_Pe_setting.aggregator_markets_rule>();


            string regionFilter = regions != null && regions.Any() ? string.Join("|", regions) : string.Empty;
            string symbolFilter = symbols != null && symbols.Any() ? string.Join("|", symbols) : string.Empty;

            //sql date(要先塞選他需要那些地區)
            using (var connection = new MySqlConnection(_connectionString))
            {

                connection.Open();
                var query = @"
                            SELECT CONCAT(a.Name, '-', a.Priority) AS Name,
                            a.Id,
                            a.AggregationMarketId,
                            a.Priority,
                            a.FeederSource,
                            a.SourceMarketId,
                            a.Weight,
                            a.BidMarkUp,
                            a.AskMarkUp,
                            a.INPUT_TIME,
                            a.REGION,
                            a.Symbol
                            FROM (
                            SELECT 
                            am.Name,
                            amr.*,
                            sm.Symbol
                            FROM pe_records.AggregationMarkets am
                            LEFT JOIN pe_records.AggregationMarketRules amr
                            ON am.Id = amr.AggregationMarketId 
                            AND am.INPUT_TIME = amr.INPUT_TIME 
                            AND am.REGION = amr.REGION
                            LEFT JOIN pe_records.SourceMarkets sm
                            ON amr.SourceMarketId = sm.Id 
                            AND amr.INPUT_TIME = sm.INPUT_TIME 
                            AND amr.REGION = sm.REGION
                            WHERE am.IsActive = 'TRUE'
                            ) a
                            WHERE 1=1 
                            AND a.Name IS NOT NULL 
                            AND a.Priority IS NOT NULL
                            AND a.REGION REGEXP @region 
                            AND a.INPUT_TIME REGEXP @date 
                            ";

                if (symbols[0] != "ALL")
                {
                    query += "AND a.`Name` = @symbol";
                }


                query += ";";

                using (var command = new MySqlCommand(query, connection))
                {

                    command.Parameters.AddWithValue("@region", regionFilter); // e.g., "NY" or "NY|TY"
                    command.Parameters.AddWithValue("@date", dateTime);

                    if (symbols[0] == "ALL")
                    { }
                    else
                    {
                        command.Parameters.AddWithValue("@symbol", symbolFilter);
                    }

                    //int count = Convert.ToInt32(command.ExecuteScalar());

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try
                            {
                                var aggregatorRule = new New_Pe_setting.aggregator_markets_rule()
                                {
                                    Id = reader.GetInt32Nullable("Id") ?? 0,
                                    Name = reader.GetNullableString("Name"),
                                    AggregationMarketId = reader.GetInt32Nullable("AggregationMarketId"),
                                    Priority = reader.GetInt32Nullable("Priority"),
                                    FeederSource = reader.GetNullableString("FeederSource"),
                                    SourceMarketId = reader.GetInt32Nullable("SourceMarketId"),
                                    Weight = reader.GetNullableDouble("Weight"),
                                    BidMarkUp = reader.GetNullableDouble("BidMarkUp"),
                                    AskMarkUp = reader.GetNullableDouble("AskMarkUp"),
                                    Symbol = reader.GetNullableString("Symbol"),
                                    INPUT_TIME = reader.GetNullableString("INPUT_TIME"),  // 保留 string 避免 DateTime 轉型問題
                                    REGION = reader.GetNullableString("REGION")
                                };

                                aggregator_markets_ruleList.Add(aggregatorRule);
                            }
                            catch (Exception ex)
                            {
                                // 如果某欄位讀取失敗，印出錯誤欄位和值
                                Debug.WriteLine("Error reading row: " + ex.Message);
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    object val = reader.IsDBNull(i) ? null : reader[i];
                                    Debug.WriteLine($"Column {reader.GetName(i)} = {val}");
                                }
                                break;
                            }
                        }
                    }
                }
            }
            return aggregator_markets_ruleList;
        }


        public List<New_Pe_setting.execution_profiles> Execution_profiles_Get(List<string> table_name, List<string> regions,
List<string> symbols, string dateTime)
        {
            List<New_Pe_setting.execution_profiles> execution_profilesList = new List<New_Pe_setting.execution_profiles>();


            string regionFilter = regions != null && regions.Any() ? string.Join("|", regions) : string.Empty;
            string symbolFilter = symbols != null && symbols.Any() ? string.Join("|", symbols) : string.Empty;

            //sql date(要先塞選他需要那些地區)
            using (var connection = new MySqlConnection(_connectionString))
            {

                connection.Open();
                var query = @"
                            SELECT * from pe_records.executionprofiles 
                            WHERE 1=1 
                            AND REGION REGEXP @region 
                            AND INPUT_TIME REGEXP @date 
                            ";

                if (symbols[0] != "ALL")
                {
                    query += "AND `Name` = @symbol";
                }


                query += ";";

                using (var command = new MySqlCommand(query, connection))
                {

                    command.Parameters.AddWithValue("@region", regionFilter); // e.g., "NY" or "NY|TY"
                    command.Parameters.AddWithValue("@date", dateTime);

                    if (symbols[0] == "ALL")
                    { }
                    else
                    {
                        command.Parameters.AddWithValue("@symbol", symbolFilter);
                    }

                    //int count = Convert.ToInt32(command.ExecuteScalar());

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try
                            {
                                var execution_profiles = new New_Pe_setting.execution_profiles()
                                {
                                    Id = reader.GetInt32Nullable("Id") ?? 0,
                                    Name = reader.GetNullableString("Name"),
                                    MinDelay = reader.GetInt32Nullable("MinDelay"),
                                    MaxDelay = reader.GetInt32Nullable("MaxDelay"),
                                    MinQty = reader.GetDecimalNullable("MinQty"),
                                    MaxQty = reader.GetDecimalNullable("MaxQty"),
                                    VolumeMultiplier = reader.GetNullableDouble("VolumeMultiplier"),
                                    VolumeModifier = reader.GetNullableDouble("VolumeModifier"),
                                    SpreadMultiplier = reader.GetNullableDouble("SpreadMultiplier"),
                                    SpreadModifier = reader.GetNullableDouble("SpreadModifier"),
                                    BidPositiveSlippage = reader.GetNullableDouble("BidPositiveSlippage"),
                                    BidNegativeSlippage = reader.GetNullableDouble("BidNegativeSlippage"),
                                    UpdatedBy = reader.GetNullableString("UpdatedBy"),
                                    UpdatedDate = reader.GetDateTimeNullable("UpdatedDate"),
                                    CreatedBy = reader.GetNullableString("CreatedBy"),
                                    CreatedDate = reader.GetDateTimeNullable("CreatedDate"),
                                    AskNegativeSlippage = reader.GetNullableDouble("AskNegativeSlippage"),
                                    AskPositiveSlippage = reader.GetNullableDouble("AskPositiveSlippage"),
                                    OutOfBoundMode = reader.GetNullableString("OutOfBoundMode"),
                                    SlippageMode = reader.GetNullableString("SlippageMode"),
                                    ExecuteAtTOB = reader.GetBooleanNullable("ExecuteAtTOB"),
                                    INPUT_TIME = reader.GetNullableString("INPUT_TIME"), // 保留 string 避免 DateTime 轉型問題
                                    REGION = reader.GetNullableString("REGION")
                                };

                                execution_profilesList.Add(execution_profiles);
                            }
                            catch (Exception ex)
                            {
                                // 如果某欄位讀取失敗，印出錯誤欄位和值
                                Debug.WriteLine("Error reading row: " + ex.Message);
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    object val = reader.IsDBNull(i) ? null : reader[i];
                                    Debug.WriteLine($"Column {reader.GetName(i)} = {val}");
                                }
                                break;
                            }
                        }
                    }
                }
            }
            return execution_profilesList;
        }




        public List<New_Pe_setting.volume_band> Volume_band_Get(List<string> table_name, List<string> regions,
List<string> symbols, string dateTime)
        {
            List<New_Pe_setting.volume_band> volume_bandList = new List<New_Pe_setting.volume_band>();


            string regionFilter = regions != null && regions.Any() ? string.Join("|", regions) : string.Empty;
            string symbolFilter = symbols != null && symbols.Any() ? string.Join("|", symbols) : string.Empty;

            //sql date(要先塞選他需要那些地區)
            using (var connection = new MySqlConnection(_connectionString))
            {

                connection.Open();
                var query = @"
                            SELECT a.*
                            FROM 
                            (SELECT vb.Name,vb.Description,vb.IsActive,vbs.*
                            FROM pe_records.VolumeBands vb
                            LEFT JOIN pe_records.VolumeBandSchedules vbs
                            ON vb.Id = vbs.VolumeBandId
                            WHERE 1=1  
                            AND vb.IsActive = 'TRUE'
                            AND vb.INPUT_TIME = vbs.INPUT_TIME 
                            AND vb.REGION = vbs.REGION 
                            ) a 
                            left join 
                            (
                            SELECT distinct VolumeBandId,INPUT_TIME,REGION FROM 
                            pe_records.pricestreammarkets
                            ) b 
                            on a.VolumeBandId = b.VolumeBandId 
                            WHERE 1=1 
                            AND a.INPUT_TIME = b.INPUT_TIME 
                            AND a.REGION = b.REGION
                            AND a.REGION REGEXP @region 
                            AND a.INPUT_TIME REGEXP @date 
                            ";

                if (symbols[0] != "ALL")
                {
                    query += "AND a.`Name` = @symbol";
                }


                query += ";";

                using (var command = new MySqlCommand(query, connection))
                {

                    command.Parameters.AddWithValue("@region", regionFilter); // e.g., "NY" or "NY|TY"
                    command.Parameters.AddWithValue("@date", dateTime);

                    if (symbols[0] == "ALL")
                    { }
                    else
                    {
                        command.Parameters.AddWithValue("@symbol", symbolFilter);
                    }

                    //int count = Convert.ToInt32(command.ExecuteScalar());

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try
                            {
                                var volumeBand = new volume_band()
                                {
                                    // VolumeBands table
                                    Id = reader.GetInt32Nullable("Id") ?? 0,
                                    Name = reader.GetNullableString("Name"),
                                    Description = reader.GetNullableString("Description"),
                                    IsActive = reader.GetNullableString("IsActive"),
                                    INPUT_TIME = reader.GetNullableString("INPUT_TIME"),  // 保留 string 避免 DateTime 轉型問題
                                    REGION = reader.GetNullableString("REGION"),

                                    // VolumeBandSchedules table
                                    VolumeBandId = reader.GetInt32Nullable("VolumeBandId"),
                                    StartTime = reader.GetTimeSpanNullable("StartTime"),
                                    EndTime = reader.GetTimeSpanNullable("EndTime"),
                                    TimeZone = reader.GetNullableString("TimeZone"),
                                    ApplyVWAP = reader.GetNullableString("ApplyVWAP"),
                                    VolumeBandScheduleLayers = reader.GetNullableString("VolumeBandScheduleLayers")
                                };

                                volume_bandList.Add(volumeBand);
                            }
                            catch (Exception ex)
                            {
                                // 如果某欄位讀取失敗，印出錯誤欄位和值
                                Debug.WriteLine("Error reading row: " + ex.Message);
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    object val = reader.IsDBNull(i) ? null : reader[i];
                                    Debug.WriteLine($"Column {reader.GetName(i)} = {val}");
                                }
                                break;
                            }
                        }
                    }
                }
            }
            return volume_bandList;
        }
    }







    // 靜態擴充方法、處理table 
    public static class DataReaderExtensions
    {

        public static decimal? GetDecimalNullable(this IDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? (decimal?)null : reader.GetDecimal(ordinal);
        }
        public static long? GetInt64Nullable(this IDataReader reader, string column)
        {
            return reader.IsDBNull(reader.GetOrdinal(column))
                ? (long?)null
                : reader.GetInt64(reader.GetOrdinal(column));
        }
        public static double? GetNullableDouble(this IDataReader reader, string column)
        {
            return reader.IsDBNull(reader.GetOrdinal(column)) ? (double?)null : reader.GetDouble(reader.GetOrdinal(column));
        }

        public static DateTime? GetDateTimeNullable(this IDataReader reader, string column)
        {
            return reader.IsDBNull(reader.GetOrdinal(column))
                ? (DateTime?)null
                : reader.GetDateTime(reader.GetOrdinal(column));
        }

        public static string GetNullableString(this IDataReader reader, string column)
        {
            if (reader.IsDBNull(reader.GetOrdinal(column)))
            {
                return null;
            }

            var value = reader[column];

            // 直接將所有資料型別轉為字串
            return value?.ToString(); // 若值為 null，直接返回 null
        }

        public static int? GetInt32Nullable(this IDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return null;

            object value = reader.GetValue(ordinal);

            // 如果是字串，處理空字串 / 空白
            if (value is string str)
            {
                if (string.IsNullOrWhiteSpace(str))
                    return null;

                if (int.TryParse(str, out int result))
                    return result;

                return null; // 非數字直接當 null
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return null;
            }
        }


        public static bool? GetBooleanNullable(this IDataReader reader, string column)
        {
            return reader.IsDBNull(reader.GetOrdinal(column))
                ? (bool?)null
                : reader.GetBoolean(reader.GetOrdinal(column));
        }

        //time

        public static TimeSpan? GetTimeSpanNullable(this MySqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return null;

            var str = reader.GetString(ordinal);
            if (string.IsNullOrWhiteSpace(str))
                return null;

            if (TimeSpan.TryParse(str, out var ts))
                return ts;

            return null; // 無法解析的話回傳 null
        }

    }
    //----------------------------restart code--------------------

}
