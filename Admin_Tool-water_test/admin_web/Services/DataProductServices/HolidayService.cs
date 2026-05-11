using System;
using System.IO;
using System.Linq;

namespace admin_web.Services.DataProductService
{
    public class HolidayService
    {
        private static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Holiday Tool");
        private static readonly string OutputPath = Path.Combine(ToolPath, "output_file");

        public static bool oupput_check()
        {
            bool hasFiles = Directory.Exists(OutputPath) && Directory.GetFiles(OutputPath).Length > 0;
            return hasFiles;
        }

        public static string Get_Oupput_FileName()
        {
            var csvFiles = Directory.GetFiles(OutputPath, "*.csv")
                                .Where(file => !Path.GetFileName(file).Equals("Setting_Error_Log.csv", StringComparison.OrdinalIgnoreCase))
                                .Select(Path.GetFileNameWithoutExtension)
                                .Select(fileName => fileName.Replace("_MT4", "").Replace("_MT5", ""))
                                .Distinct()
                                .ToList();
            return csvFiles.Count == 1 ? csvFiles.First() : string.Empty;
        }
    }
}
