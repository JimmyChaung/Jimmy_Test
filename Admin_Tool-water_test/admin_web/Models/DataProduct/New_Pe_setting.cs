using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace admin_web.Models.DataProduct
{
    public class New_Pe_setting
    {
        public class SymbolSettingViewModel
        {
            public List<model_markets_symbol> model_markets_symbolList { get; set; }

            public List<source_markets_symbol> source_markets_symbolList { get; set; }

            public List<price_stream> price_streamList { get; set; }
            public List<aggregator_markets> aggregator_marketsList { get; set; }

            public List<aggregator_markets_rule> aggregator_markets_ruleList { get; set; }

            public List<execution_profiles> execution_profilesList { get; set; }

            public List<volume_band> volume_bandList { get; set; }



        }



        public class AjaxDataModel
        {
            public List<string> sqlnames { get; set; }
            public List<string> regions { get; set; }
            public List<string> symbols { get; set; }
            public DateTime? times { get; set; }  // 👈 新增這個欄位

        }

        public class model_markets_symbol
        {
            public int Id { get; set; }
            public string? FeederSource { get; set; }
            public string? Name { get; set; }
            public string? Symbol { get; set; }
            public bool IsActive { get; set; }
            public string? AssetType { get; set; }
            public int? PriceDecimalPlaces { get; set; }
            public int? VolumeDecimalPlaces { get; set; }
            public int? SymbolGroupId { get; set; }
            public DateTime? InputTime { get; set; }
            public string? Region { get; set; }



        }

        public class source_markets_symbol
        {
            public long Id { get; set; }                // 主鍵 ID
            public string FeederSource { get; set; }    // 資料來源
            public string Name { get; set; }            // 名稱
            public string Symbol { get; set; }          // 交易代號
            public bool IsActive { get; set; }          // 是否啟用
            public string AssetType { get; set; }       // 資產類型 (可改成 enum)
            public string TimeZone { get; set; }        // 所屬時區
            public int ThrottlePerSecond { get; set; }  // 每秒限流數
            public decimal PriceMultiplier { get; set; }// 價格倍率
            public decimal VolumeMultiplier { get; set; }// 成交量倍率
            public int PriceDecimalPlaces { get; set; } // 價格小數位數
            public decimal BidSpread { get; set; }      // Bid 點差
            public decimal AskSpread { get; set; }      // Ask 點差
            public string ExpirationDateTime { get; set; } // 到期日 (nullable)
            public string RequireVolumeData { get; set; } // 是否需要成交量數據
            public string INPUT_TIME { get; set; }    // 輸入時間
            public string REGION { get; set; }          // 區域
        }

        public class price_stream
        {
            public string Name { get; set; }               // Name
            public string Symbol { get; set; }             // Symbol
            public int Id { get; set; }                    // Id
            public int? ModelMarketId { get; set; }        // ModelMarketId，可為 null
            public string? MappingName { get; set; }        // MappingName
            public long? CoreSpread { get; set; }       // CoreSpread，可為 null
            public int? MinSpread { get; set; }    // 對應 MySQL INT，可為 NULL
            public long? MaxSpread { get; set; }   // 對應 MySQL BIGINT，可為 NULL
            public int? ThrottlePerSecond { get; set; }    // ThrottlePerSecond，可為 null
            public int? MarketDepthValidSeconds { get; set; } // MarketDepthValidSeconds，可為 null
            public int? VolumeBandId { get; set; }         // VolumeBandId，可為 null
            public int? PriceStreamId { get; set; }        // PriceStreamId，可為 null
            public string INPUT_TIME { get; set; }      // INPUT_TIME，可為 null
            public string REGION { get; set; }             // REGION
        }

        public class aggregator_markets
        {
            public int Id { get; set; }                            // Id
            public string Name { get; set; }                       // Name
            public string AssetType { get; set; }                  // AssetType
            public string AggregationType { get; set; }           // AggregationType
            public string IsActive { get; set; }                  // IsActive
            public string ClearNudgeWhenStarts { get; set; }      // ClearNudgeWhenStarts
            public int? FailoverInterval { get; set; }            // FailoverInterval
            public int? NudgeCalculationKeepPriceIntervalSeconds { get; set; } // NudgeCalculationKeepPriceIntervalSeconds
            public int? NudgeCalculationIntervalSeconds { get; set; }          // NudgeCalculationIntervalSeconds
            public int? NudgePersistInCacheMinutes { get; set; }   // NudgePersistInCacheMinutes
            public int? IgnoreSourceInterval { get; set; }         // IgnoreSourceInterval
            public int? AverageSpread { get; set; }                // AverageSpread
            public int? NumberOfDecimalPlaces { get; set; }        // NumberOfDecimalPlaces
            public double? SpreadVolatility { get; set; }          // SpreadVolatility
            public int? Throttle { get; set; }                     // Throttle
            public int? MaxFilterInterval { get; set; }            // MaxFilterInterval
            public double? Sensitivity { get; set; }              // Sensitivity
            public int? TargetSpread { get; set; }                 // TargetSpread
            public int? Thresholds { get; set; }                   // Thresholds
            public string TargetSpreadValues { get; set; }         // TargetSpreadValues
            public string EnableSpreadBand { get; set; }           // EnableSpreadBand
            public string FailoverInstantly { get; set; }          // FailoverInstantly
            public string INPUT_TIME { get; set; }                 // INPUT_TIME
            public string REGION { get; set; }                     // REGION
        }



        public class aggregator_markets_rule
        {
            public string Name { get; set; }                 // Name
            public int Id { get; set; }                      // Id
            public int? AggregationMarketId { get; set; }   // AggregationMarketId，可為 null
            public int? Priority { get; set; }              // Priority，可為 null
            public string FeederSource { get; set; }        // FeederSource
            public int? SourceMarketId { get; set; }        // SourceMarketId，可為 null
            public double? Weight { get; set; }             // Weight，可為 null
            public double? BidMarkUp { get; set; }          // BidMarkUp，可為 null
            public double? AskMarkUp { get; set; }          // AskMarkUp，可為 null
            public string INPUT_TIME { get; set; }          // INPUT_TIME
            public string REGION { get; set; }              // REGION
            public string Symbol { get; set; }              // Symbol
        }



        public class execution_profiles
        {
            public int Id { get; set; }                        // Id
            public string Name { get; set; }                   // Name
            public int? MinDelay { get; set; }                 // MinDelay，可為 null
            public int? MaxDelay { get; set; }                 // MaxDelay，可為 null
            public decimal? MinQty { get; set; }
            public decimal? MaxQty { get; set; }
            public double? VolumeMultiplier { get; set; }
            public double? VolumeModifier { get; set; }
            public double? SpreadMultiplier { get; set; }
            public double? SpreadModifier { get; set; }
            public double? BidPositiveSlippage { get; set; }
            public double? BidNegativeSlippage { get; set; }
            public double? AskNegativeSlippage { get; set; }
            public double? AskPositiveSlippage { get; set; }
            public string? UpdatedBy { get; set; }              // UpdatedBy
            public DateTime? UpdatedDate { get; set; }         // UpdatedDate，可為 null
            public string? CreatedBy { get; set; }              // CreatedBy
            public DateTime? CreatedDate { get; set; }         // CreatedDate，可為 null

            public string? OutOfBoundMode { get; set; }         // OutOfBoundMode
            public string? SlippageMode { get; set; }           // SlippageMode
            public bool? ExecuteAtTOB { get; set; }            // ExecuteAtTOB，可為 null
            public string INPUT_TIME { get; set; }             // INPUT_TIME
            public string REGION { get; set; }                 // REGION

        }



        public class volume_band
        {
            // VolumeBands table
            public int Id { get; set; }                     // Id
            public string Name { get; set; }                // Name
            public string Description { get; set; }         // Description
            public string IsActive { get; set; }            // IsActive
            public string INPUT_TIME { get; set; }          // INPUT_TIME
            public string REGION { get; set; }              // REGION

            // VolumeBandSchedules table
            public int? VolumeBandId { get; set; }          // VolumeBandId，可為 null
            public TimeSpan? StartTime { get; set; }        // StartTime，可為 null
            public TimeSpan? EndTime { get; set; }          // EndTime，可為 null
            public string TimeZone { get; set; }            // TimeZone
            public string ApplyVWAP { get; set; }           // ApplyVWAP
            public string VolumeBandScheduleLayers { get; set; } // VolumeBandScheduleLayers
        }



    }
}


