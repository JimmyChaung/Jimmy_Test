using admin_web.Services.DataProductService;
using Coravel.Invocable;
using Coravel.Scheduling.Schedule.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

public class SchedulerManager
{
    public static void ConfigureScheduler(IScheduler scheduler)
    {
        //Task_ConfigDiffRecordsVertical.Configure(scheduler);
        // Task_test.Configure(scheduler);
    }

    // 是否啟用
    public static class SchedulerConfig
    {
        public static bool Enabled_ConfigDiffRecordsVertical { get; set; } = false;
    }

    // 新增排程記得去Startup.cs註冊服務
    public class Task_ConfigDiffRecordsVertical : IInvocable
    {
        public Task Invoke()
        {
            if (SchedulerConfig.Enabled_ConfigDiffRecordsVertical)
            {
                ConfigDiffRecordsService.Postgre2MysqlAsync();
            }

            return Task.CompletedTask;
        }

        public static void Configure(IScheduler scheduler)
        {
            // 每天 02:00 導入資料
            scheduler.Schedule<Task_ConfigDiffRecordsVertical>()
                    .DailyAtHour(2)
                    .Zoned(TimeZoneInfo.Local);
        }
    }

    public class Task_test : IInvocable
    {
        public Task Invoke()
        {
            try
            {
                var logPath = @"E:\log.txt";
                var message = $"任務執行中：{DateTime.Now}\n";
                File.AppendAllText(logPath, message);
            }
            catch (Exception ex)
            {
                var errorMessage = $"[錯誤] {DateTime.Now} - {ex}\n";
                File.AppendAllText(@"E:\log.txt", errorMessage);
            }

            return Task.CompletedTask;
        }

        public static void Configure(IScheduler scheduler)
        {
            scheduler.Schedule<Task_test>()
                    .EveryTenSeconds()
                    .Zoned(TimeZoneInfo.Local);
        }
    }
}
