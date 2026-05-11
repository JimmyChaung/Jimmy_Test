using System.IO;

namespace admin_web.Services.DataProductService
{
    public class Pelican_Month_FeeService
    {
        private static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "Pelican Month Fee");
        private static readonly string OutputPath = Path.Combine(ToolPath, "output_file");

        // 讀取所有Excel檔案名稱
        public static string GetFileName()
        {
            string[] files = Directory.GetFiles(OutputPath, "*.xlsx");
            string fileName = files.Length > 0 ? Path.GetFileName(files[0]) : null;
            return fileName;
        }
    }
}
