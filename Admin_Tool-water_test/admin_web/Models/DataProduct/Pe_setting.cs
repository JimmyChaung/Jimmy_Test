using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace admin_web.Models.DataProduct
{
    public class Pe_setting
    {
        public class SymbolSettingViewModel
        {
            public List<aggregationconfig> AggregationconfigList { get; set; }
            public List<executionprofiles> ExecutionprofilesList { get; set; }
            public List<marketinformation> MarketinformationList { get; set; }
            public List<pricestream> PricestreamList { get; set; }
            public List<volumeband> VolumebandList { get; set; }
        }



        public class AjaxDataModel
        {
            public List<string> sqlnames { get; set; }
            public List<string> regions { get; set; }
            public List<string> symbols { get; set; }
            public DateTime? times { get; set; }  // 👈 新增這個欄位

        }

        public class aggregationconfig
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string AggregationType { get; set; }
            public string FailoverInterval { get; set; }
            public string Description { get; set; }
            public string FeederSource { get; set; }
            public string AssetType { get; set; }
            public string IsActive { get; set; }
            public string AggregationRule { get; set; }
            public string UpdatedBy { get; set; }
            public string UpdatedDate { get; set; }
            public string CreatedBy { get; set; }
            public string CreatedDate { get; set; }
            public string ClearNudgeWhenStarts { get; set; }
            public string NudgeCalculationIntervalSeconds { get; set; }
            public string NudgeCalculationKeepPriceIntervalSeconds { get; set; }
            public string NudgePersistInCacheMinutes { get; set; }
            public string AverageSpread { get; set; }
            public string IgnoreSourceInterval { get; set; }
            public string NumberOfDecimalPlaces { get; set; }
            public string SpreadVolatility { get; set; }
            public string MaxFilterInterval { get; set; }
            public string Throttle { get; set; }
            public string EnableSpreadBand { get; set; }
            public string Sensitivity { get; set; }
            public string TargetSpread { get; set; }
            public string TargetValues { get; set; }
            public string Thresholds { get; set; }
            public string INPUT_TIME { get; set; }
            public string REGION { get; set; }
        }

        public class executionprofiles
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string MinDelay { get; set; }
            public string MaxDelay { get; set; }
            public string MinQty { get; set; }
            public string MaxQty { get; set; }
            public string VolumeMultiplier { get; set; }
            public string VolumeModifier { get; set; }
            public string SpreadMultiplier { get; set; }
            public string SpreadModifier { get; set; }
            public string BidPositiveSlippage { get; set; }
            public string BidNegativeSlippage { get; set; }
            public string UpdatedBy { get; set; }
            public string UpdatedDate { get; set; }
            public string CreatedBy { get; set; }
            public string CreatedDate { get; set; }
            public string AskNegativeSlippage { get; set; }
            public string AskPositiveSlippage { get; set; }
            public string OutOfBoundMode { get; set; }
            public string SlippageMode { get; set; }
            public string ExecuteAtTOB { get; set; }
            public string INPUT_TIME { get; set; }
            public string REGION { get; set; }
        }

        public class marketinformation
        {
            public string Name { get; set; }
            public string MarketId { get; set; }
            public string MarketStatus { get; set; }
            public string FeederSource { get; set; }
            public string MarketName { get; set; }
            public string Description { get; set; }
            public string Symbol { get; set; }
            public string PricingDP { get; set; }
            public string AmountDP { get; set; }
            public string ExchangeName { get; set; }
            public string TradableDays { get; set; }
            public string MarketOpenTime { get; set; }
            public string MarketCloseTime { get; set; }
            public string AssetType { get; set; }
            public string MarketScope { get; set; }
            public string PricingEngine { get; set; }
            public string ExpirationDateTime { get; set; }
            public string RequireVolumeData { get; set; }
            public string MarketTimeZone { get; set; }
            public string UpdatedBy { get; set; }
            public string UpdatedDate { get; set; }
            public string CreatedBy { get; set; }
            public string CreatedDate { get; set; }
            public string SymbolGroupId { get; set; }
            public string TickPerSecond { get; set; }
            public string PriceMultiplier { get; set; }
            public string VolumeMultiplier { get; set; }
            public string AskSpread { get; set; }
            public string BidSpread { get; set; }
            public string INPUT_TIME { get; set; }
            public string REGION { get; set; }

            public string SymbolGroupName { get; set; }


        }

        public class pricestream
        {
            public string Name { get; set; }
            public string PriceStreamProfileName { get; set; }
            public string Id { get; set; }
            public string MarketId { get; set; }
            public string Symbol { get; set; }
            public string PricingStreamProfileId { get; set; }
            public string PriceFilterGroupIds { get; set; }
            public string VolumeBandConfigurationId { get; set; }
            public string MappingName { get; set; }
            public string ProfileTimeZone { get; set; }
            public string TimeRange { get; set; }
            public string CoreSpread { get; set; }
            public string UpdatedBy { get; set; }
            public string UpdatedDate { get; set; }
            public string CreatedBy { get; set; }
            public string CreatedDate { get; set; }
            public string ExpiryInSecond { get; set; }
            public string ThrottlePerSecond { get; set; }
            public string MaxSpread { get; set; }
            public string MinSpread { get; set; }
            public string INPUT_TIME { get; set; }
            public string REGION { get; set; }
            public string VolumeBandName { get; set; }


        }

        public class volumeband
        {
            public string Symbol { get; set; }
            public string Id { get; set; }
            public string VolumeBandName { get; set; }
            public string Description { get; set; }
            public string IsActive { get; set; }
            public string UpdatedBy { get; set; }
            public string UpdatedDate { get; set; }
            public string CreatedBy { get; set; }
            public string CreatedDate { get; set; }
            public string InputTime { get; set; }
            public string Region { get; set; }
            public string TimeZone { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public string SpreadMode { get; set; }
            public string MinSpread { get; set; }
            public string MaxSpread { get; set; }
            public string FixSpread { get; set; }
            public string EnableOverflow { get; set; }
            public string ApplyVWAP { get; set; }
            public string Multiplier { get; set; }
            public string VolumeBandLayer { get; set; }
            public string VolumeBandConfigurationId { get; set; }
            public string UpdatedBy2 { get; set; }
            public string UpdatedDate2 { get; set; }
            public string CreatedBy2 { get; set; }
            public string CreatedDate2 { get; set; }
            public string InputTime2 { get; set; }
            public string Region2 { get; set; }
        }


    }
}

