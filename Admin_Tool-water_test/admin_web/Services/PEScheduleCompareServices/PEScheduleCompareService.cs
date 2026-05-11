using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using OfficeOpenXml;
using System.Diagnostics;
using admin_web.Models;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Npgsql;
using System.Reflection;
using MetaQuotes.MT5CommonAPI;
using MetaQuotes.MT5ManagerAPI;

namespace admin_web.Services.PEScheduleCompareServices
{
    public class PEScheduleCompareService
    {
        private readonly IConfiguration _configuration;

        public PEScheduleCompareService(IConfiguration configuration)
        {
            _configuration = configuration;
        }        

        private static readonly List<ComparisonRule.floatRule> HardcoreRules = new List<ComparisonRule.floatRule>
        {
            new ComparisonRule.floatRule
            {
                ExpectedTimeZone = "America/New_York",
                TimeRanges = new List<TimeRange>
                {
                    new TimeRange{ FromTime = new TimeSpan(18,0,0), ToTime = new TimeSpan(18,5,0)}
                }
            }
        };

        //public async Task<PeScheduleCompareModel> RFRCompare()
        //{
            
        //    File
        //    return;
        //}

        //public async Task<PeScheduleCompareModel> NewsCompare()
        //{
        //    return;
        //}

        public async Task<String> GetPEConnection(string Region)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();

            command.CommandText = @"
                Select * 
                From sql_connect 
                WHERE `Server` = @region;
            ";

            command.Parameters.AddWithValue("@region", Region);

            using var reader = await command.ExecuteReaderAsync();

            string host = reader.GetString("HOST");
            int port = reader.GetInt32("PORT");
            string user = reader.GetString("USER");
            string password = reader.GetString("PASSWORD");
            string database = reader.GetString("Database");

            string result =
                $"Server={host};" +
                $"Port={port};" +
                $"Database={database};" +
                $"Uid={user};" +
                $"Pwd={password};" +
                $"Connection Timeout=120;" +
                $"Default Command Timeout=120;";
            return result;
        }
    }
}
