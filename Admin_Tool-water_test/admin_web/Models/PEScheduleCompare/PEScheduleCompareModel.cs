using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace admin_web.Models
{
    public class PeScheduleCompareModel
    {
        public bool HasResult { get; set; }
        public string Message { get; set; }
        public Dictionary<string, DataTable> RegionTables { get; set; }
        [Required(ErrorMessage = "請選擇檔案")]
        public IFormFile File { get; set; }
    }
    
    public class ComparisonRule
    {
        public List<string> SetRegions { get; set; } = new List<string>();

        public class floatRule
        {
            public string Name { get; set; }
            public string ExpectedTimeZone { get; set; }
            public List<TimeRange> TimeRanges { get; set; } = new List<TimeRange>();
        }
    }

    public class TimeRange
    {
        public TimeSpan FromTime { get; set; }
        public TimeSpan ToTime { get; set; }
    }
}
