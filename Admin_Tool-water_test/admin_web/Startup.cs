using admin_web.Services.PermissionServices;
using Coravel;
using Coravel.Scheduling.Schedule.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using static SchedulerManager;
using admin_web.Services.QuerymysqlServices;
using OfficeOpenXml;

namespace admin_web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            // services.AddScoped<Permission_Data>(); // 或 services.AddTransient<Permission_Data>();

            services.AddControllersWithViews();

            // 添加Session支持
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30); // 設置Session過期時間(30min)
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = int.MaxValue;
            });

            services.AddScoped<QuerymysqlService>();

            // 排程: 註冊服務
            //services.AddControllersWithViews();
            //services.AddScheduler();
            //services.AddTransient<Task_ConfigDiffRecordsVertical>();
            // services.AddTransient<Task_test>(); // 測試用
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();


            // 使用Session中間件
            app.UseSession();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Login}/{id?}"
                );
                endpoints.MapDefaultControllerRoute();
            });

            // 啟用排程
            //app.ApplicationServices.UseScheduler(scheduler =>
            //{
            //    SchedulerManager.ConfigureScheduler(scheduler);
            //});

            //lifetime.ApplicationStarted.Register(() =>
            //{
            //    var scheduler = app.ApplicationServices.GetRequiredService<IScheduler>();
            //    SchedulerManager.ConfigureScheduler(scheduler);
            //});
        }
    }
}
