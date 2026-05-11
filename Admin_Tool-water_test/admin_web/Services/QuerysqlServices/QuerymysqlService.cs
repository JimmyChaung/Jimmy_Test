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
using admin_web.Services.QuerysqlServices;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace admin_web.Services.QuerymysqlServices
{
    public class QuerymysqlService
    {
        private readonly IConfiguration _configuration;

        public QuerymysqlService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 取各 Server 資料
        public async Task<List<QueryserverlistModel.DatabaseServerInfo>> AllServerInfo()
        {
            QuerygetlistServices querygetlistServices = new QuerygetlistServices(_configuration);

            var result = await querygetlistServices.GetAllServerList();
            return result;
        }

        // 執行序方式將特定 Server tablelist 塞入模板
        public async Task<Dictionary<string, List<string>>> GetTableTemplates()
        {
            var allServers = await AllServerInfo();

            var mt4Server = allServers.FirstOrDefault(s => s.SqlName == "enfaureport");

            var mt5Server = allServers.FirstOrDefault(s => s.SqlName == "mt5_vfx_live");

            var templates = new Dictionary<string, List<string>>();

            var tasks = new List<Task>();

            if (mt4Server != null)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var tables = await GetTablesFromServer(mt4Server);
                    lock (templates)
                    {
                        templates["mt4"] = tables;
                    }
                }));
            }

            if (mt5Server != null)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var tables = await GetTablesFromServer(mt5Server);
                    lock (templates)
                    {
                        templates["mt5"] = tables;
                    }
                }));
            }

            await Task.WhenAll(tasks);

            return templates;
        }

        // 取特定 Server tablelist
        private async Task<List<string>> GetTablesFromServer(QueryserverlistModel.DatabaseServerInfo server)
        {
            var tables = new List<string>();

            string serverConn = $"Server={server.Host};" +
                $"Port={server.Port};" +
                $"Database={server.SqlName};" +
                $"Uid={server.User};" +
                $"Pwd={server.Password};" +
                $"Connection Timeout=120;" +
                $"Default Command Timeout=120;";

            using var connection = new MySqlConnection(serverConn);
            try
            {
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = $"SHOW TABLES FROM `{server.SqlName}`;";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get tables from {server.SqlName}: {ex.Message}", ex);
            }

            return tables;
        }

        // 將 GetTableTemplates 回傳資料新增至各 Server
        public async Task<List<QueryserverlistModel.ServerWithTables>> GetAllServersWithTables()
        {
            var allServers = await AllServerInfo();

            var tableTemplates = await GetTableTemplates();

            var result = allServers.Select(server =>
            {
                var tables = tableTemplates.GetValueOrDefault(server.Mt, new List<string>());

                return new QueryserverlistModel.ServerWithTables
                {
                    ServerInfo = server,
                    Tables = tables
                };
            }).ToList();

            return result;
        }

        // 使用者 INPUT 處理
        public async Task<List<Dictionary<string, Object>>> MysqlQuerySearch(string sqlCode)
        {
            var tables = new List<string>();
            var deny = new[]{
                "drop", "delete", "truncate", "alter", "update", "insert"
            };
            if (deny.Any(x => sqlCode.ToLower().Contains(x)))
            {
                throw new Exception("Dangerous SQL not allowed");
            }
            var match = Regex.Match(sqlCode,
                @"from\s+([^\s,]+)",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                throw new Exception($"Query Search failed");
            }

            // Server Name ex. SG01
            string matchString = match.Groups[1].Value;
            // Table Name ex. mt4/5_users
            var tableName = matchString.Split('.')[0];

           var allServerInfo = await AllServerInfo();
           var result = new List<Dictionary<string, Object>>();

           var getSpecifyServer = allServerInfo.FirstOrDefault(p => p.SqlName == tableName);
            string serverConnInfo =
             $"Server={getSpecifyServer.Host};" +
             $"Port={getSpecifyServer.Port};" +
             $"Database={tableName};" +
             $"Uid={getSpecifyServer.User};" +
             $"Pwd={getSpecifyServer.Password};" +
             $"Connection Timeout=120;" +
             $"Default Command Timeout=120;";
            using var connection = new MySqlConnection(serverConnInfo);

            await connection.OpenAsync();

            using var command = connection.CreateCommand();

            command.CommandText = sqlCode;

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, Object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);

                    row[columnName] = value;
                }
                result.Add(row);
            }
            return result;
        }
    }
}
