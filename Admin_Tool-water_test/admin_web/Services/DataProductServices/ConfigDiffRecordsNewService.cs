using MySql.Data.MySqlClient;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static admin_web.Models.DataProduct.ConfigDiffRecords_Model;

namespace admin_web.Services.DataProductService
{
    public class ConfigDiffRecordsNewService
    {
        private static string sql_connectionString = UniversalService.sql_connectionString;

        // 要導入資料的Table
        // 這邊新增新的Table，排程用的 E:\snap_tool\EA_Postgr2Mysql_New 這邊也要加
        public static List<string> table_list = new() {
            "AggregationMarketRules", "AggregationMarkets", "ExecutionProfiles", "ModelMarketSchedules",
            "ModelMarkets", "OrderRoutingRules", "PriceStreamMarkets", "PriceStreams",
            "SourceMarkets", "TakerGroupPriceStreams", "TakerGroups", "VolumeBandSchedules", "VolumeBands"
        };

        // 給前端選的Table
        public static List<string> select_table_list = new List<string>() {
            "Model Markets Symbol", "Price Stream", "PriceStreamMarkets", "Aggregator Markets",
            "Aggregator Markets Rule", "Volume Band", "Execution Profile", "Order Routing" };

        public static string Get_Today_Refresh_Time()
        {
            try
            {
                var _refreshTime = DateTime.Today.AddHours(2);
                var now = DateTime.Now;

                string _date = now >= _refreshTime
                    ? DateTime.Today.ToString("yyyy-MM-dd")
                    : DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
                string _dateTime = string.Empty;

                using (MySqlConnection connection = new MySqlConnection(sql_connectionString))
                {
                    connection.Open();

                    string query = $@"SELECT Time
                                    FROM admin_tool_log.configdiffrecords_log
                                    WHERE Time REGEXP '{_date}'
                                    GROUP BY TIME
                                    ORDER BY TIME DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                _dateTime = reader["Time"].ToString();
                            }
                        }
                    }
                }
                return _dateTime;
            }
            catch (Exception)
            {
                return "ERROR";
            }

        }

        // 從 Postgre導入現在的資料到 Mysql
        public static async Task<string> Postgre2MysqlAsync()
        {
            var pe_dict = GetAllPeServer();

            // Today Date
            string inputTime = DateTime.Today.ToString("yyyy-MM-dd") + " 23:59:59";
            string logTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Declare All Log Records List
            var all_log = new List<LogRecord>();

            var tasks = pe_dict.Select(async item =>
            {
                var _server = item.Key;
                var _config = item.Value;

                string pg_connectionString = @$"
                    Host={_config.Host};
                    Port={_config.Port};
                    Username={_config.User};
                    Password={_config.Password};
                    Database={_config.Database}";

                using (var myConn = new MySqlConnection(sql_connectionString))
                {
                    await myConn.OpenAsync();

                    foreach (var table_name in table_list)
                    {
                        await Task.Run(async () =>
                        {
                            // Delete today's data
                            try
                            {
                                string deleteSql = $@"
                                        DELETE FROM pe_records.{table_name}
                                        WHERE INPUT_TIME = '{inputTime}' AND REGION = '{_server}'";

                                using (var deleteCmd = new MySqlCommand(deleteSql, myConn))
                                    await deleteCmd.ExecuteNonQueryAsync();
                            }
                            catch (Exception ex)
                            {
                                all_log.Add(new LogRecord()
                                {
                                    Server = _server,
                                    Table = table_name,
                                    Log = $"Failed to delete today data for {table_name}: {ex.Message}",
                                    Time = logTime
                                });
                                return;
                            }

                            // Add after obtaining data
                            try
                            {
                                using (var pgConn = new NpgsqlConnection(pg_connectionString))
                                {
                                    await pgConn.OpenAsync();
                                    var query = $"SELECT * FROM public.\"{table_name}\"";

                                    using (var cmd = new NpgsqlCommand(query, pgConn))
                                    using (var reader = await cmd.ExecuteReaderAsync())
                                    {
                                        var schemaTable = reader.GetSchemaTable();
                                        var columnNames = schemaTable.Rows.Cast<DataRow>()
                                            .Select(r => r["ColumnName"].ToString())
                                            .ToList();

                                        var insertValues = new List<string>();

                                        while (await reader.ReadAsync())
                                        {
                                            var rowValues = columnNames.Select(col =>
                                            {
                                                var val = reader[col];

                                                if (val == DBNull.Value)
                                                {
                                                    return "NULL";
                                                }
                                                else if (val is string[] strArray)
                                                {
                                                    if (strArray.Length == 0)
                                                        return "''";
                                                    else
                                                    {
                                                        var joined = "[" + string.Join(",", strArray.Select(s => s.Replace("'", "''"))) + "]";
                                                        return $"'{joined}'";
                                                    }
                                                }
                                                else if (val is double[] doubleArray)
                                                {
                                                    if (doubleArray.Length == 0)
                                                        return "''";
                                                    else
                                                    {
                                                        var joined = "[" + string.Join(",", doubleArray.Select(f => f.ToString(CultureInfo.InvariantCulture))) + "]";
                                                        return $"'{joined}'";
                                                    }
                                                }
                                                else if (val is float[] floatArray)
                                                {
                                                    if (floatArray.Length == 0)
                                                        return "''";
                                                    else
                                                    {
                                                        var joined = "[" + string.Join(",", floatArray.Select(f => f.ToString(CultureInfo.InvariantCulture))) + "]";
                                                        return $"'{joined}'";
                                                    }
                                                }
                                                else if (val is int[] intArray)
                                                {
                                                    if (intArray.Length == 0)
                                                        return "''";
                                                    else
                                                    {
                                                        var joined = "[" + string.Join(",", intArray) + "]";
                                                        return $"'{joined}'";
                                                    }
                                                }
                                                else
                                                {
                                                    return $"'{val.ToString().Replace("'", "''")}'";
                                                }
                                            }).ToList();

                                            // Additional fields
                                            rowValues.Add($"'{inputTime}'");
                                            rowValues.Add($"'{_server}'");

                                            insertValues.Add($"({string.Join(",", rowValues)})");

                                            // Batch insert of 500 records
                                            if (insertValues.Count >= 500)
                                            {
                                                var finalColumnList = string.Join(",", columnNames) + ", `INPUT_TIME`, `REGION`";
                                                var insertSql = $@"
                                                    INSERT INTO pe_records.{table_name} ({finalColumnList})
                                                    VALUES {string.Join(",", insertValues)};";

                                                using (var insertCmd = new MySqlCommand(insertSql, myConn))
                                                    await insertCmd.ExecuteNonQueryAsync();

                                                insertValues.Clear();
                                            }
                                        }

                                        // The remaining uninserted
                                        if (insertValues.Any())
                                        {
                                            var finalColumnList = string.Join(",", columnNames) + ", `INPUT_TIME`, `REGION`";
                                            var insertSql = $@"
                                                INSERT INTO pe_records.{table_name} ({finalColumnList})
                                                VALUES {string.Join(",", insertValues)};";

                                            using (var insertCmd = new MySqlCommand(insertSql, myConn))
                                                await insertCmd.ExecuteNonQueryAsync();
                                        }
                                    }
                                }

                                // success log 
                                all_log.Add(new LogRecord()
                                {
                                    Server = _server,
                                    Table = table_name,
                                    Log = "SUCCESS",
                                    Time = logTime
                                });
                            }
                            catch (Exception ex)
                            {
                                // erro log
                                all_log.Add(new LogRecord()
                                {
                                    Server = _server,
                                    Table = table_name,
                                    Log = $"Failed to insert for {table_name}: {ex.Message}",
                                    Time = logTime
                                });
                            }
                        });
                    }
                }
            });

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Sort Log order by Server & Table
            all_log = all_log
                    .OrderBy(l => l.Server)
                    .ThenBy(l => l.Table)
                    .ToList();

            // 紀錄 Log
            foreach (var item in all_log)
            {
                using (var myConn = new MySqlConnection(sql_connectionString))
                {
                    myConn.Open();

                    string insertSql = @"
                            INSERT INTO admin_tool_log.configdiffrecords_log 
                            (`Server`, `Table`, `Log`, `Time`) 
                            VALUES (@Server, @Table, @Log, @Time);";

                    using (var cmd = new MySqlCommand(insertSql, myConn))
                    {
                        cmd.Parameters.AddWithValue("@Server", item.Server);
                        cmd.Parameters.AddWithValue("@Table", item.Table);
                        cmd.Parameters.AddWithValue("@Log", item.Log);
                        cmd.Parameters.AddWithValue("@Time", item.Time);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            if (all_log.Any(l => l.Log != "SUCCESS"))
            {
                var failedLogs = all_log
                                .Where(l => l.Log != "SUCCESS")
                                .Select(l => $"{l.Server} {l.Table}")
                                .ToList();

                var errorMessage = "以下資料表存取失敗：\n" + string.Join("\n", failedLogs);
                return errorMessage;
            }
            else
            {
                return "刷新成功";
            }
        }

        // 撈全部 PE Server Config
        public static Dictionary<string, PeServerRecord> GetAllPeServer()
        {
            Dictionary<string, PeServerRecord> dict = new();
            using (MySqlConnection connection = new MySqlConnection(sql_connectionString))
            {
                connection.Open();

                string query = "SELECT * FROM pe_records.sql_connect;";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string serverName = reader["Server"].ToString();

                            var record = new PeServerRecord
                            {
                                Host = reader["Host"].ToString(),
                                User = reader["User"].ToString(),
                                Password = reader["Password"].ToString(),
                                Port = reader["Port"].ToString(),
                                Database = reader["Database"].ToString()
                            };
                            dict[serverName] = record;
                        }
                    }
                }
            }
            return dict;
        }

        // 新版前端
        public static List<ViewRecord> CompareData_NEW(string _From, string _To)
        {
            if (string.IsNullOrEmpty(_From) || string.IsNullOrEmpty(_To))
            {
                return null;
            }

            _From += " 23:59:59";
            _To += " 23:59:59";
            var all_view = new List<ViewRecord>();

            // 忽略欄位
            var ignored_col = new List<string>() { "UpdatedBy", "UpdatedDate", "CreatedBy", "CreatedDate", "INPUT_TIME" };

            // 使用表格
            var tableMappings = new Dictionary<string, TableRecord>
            {
                ["Model Markets Symbol"] = new TableRecord
                {
                    primary_key = "Id",
                    secondary_key = "Symbol",
                    query = @$"
                            SELECT * 
                            FROM pe_records.modelmarkets
                            WHERE INPUT_TIME IN ('{_From}','{_To}')"
                },
                ["Price Stream"] = new TableRecord
                {
                    primary_key = "Id",
                    secondary_key = "Name",
                    query = @$"
                            SELECT * 
                            FROM pe_records.pricestreams
                            WHERE INPUT_TIME IN ('{_From}','{_To}')"
                },
                ["PriceStreamMarkets"] = new TableRecord
                {
                    primary_key = "Id",
                    secondary_key = "Name,Symbol",
                    query = @$"
                            SELECT ps.Name, mm.Symbol, psm.*
                            FROM pe_records.pricestreams ps
                            LEFT JOIN pe_records.pricestreammarkets psm 
                            ON ps.Id = psm.PriceStreamId AND ps.REGION = psm.REGION AND ps.INPUT_TIME = psm.INPUT_TIME
                            LEFT JOIN pe_records.modelmarkets mm 
                            ON psm.ModelMarketId = mm.Id AND psm.REGION = mm.REGION AND psm.INPUT_TIME = mm.INPUT_TIME
                            WHERE mm.INPUT_TIME IN ('{_From}','{_To}')"
                },
                ["Aggregator Markets"] = new TableRecord
                {
                    primary_key = "Id",
                    secondary_key = "Name",
                    query = @$"
                            SELECT * 
                            FROM pe_records.aggregationmarkets
                            WHERE INPUT_TIME IN ('{_From}','{_To}')"
                },
                ["Aggregator Markets Rule"] = new TableRecord
                {
                    primary_key = "Name",
                    secondary_key = "AggregationMarketId,Priority",
                    query = @$"
                            SELECT CONCAT(am.Name, '-',amr.Priority) as Name, sm.FeederSource, sm.Symbol, amr.*
                            FROM pe_records.aggregationmarkets am
                            LEFT JOIN pe_records.aggregationmarketrules amr 
                            ON am.Id = amr.AggregationMarketId AND am.REGION = amr.REGION AND am.INPUT_TIME = amr.INPUT_TIME
                            LEFT JOIN pe_records.sourcemarkets sm 
                            ON amr.SourceMarketId = sm.Id AND amr.REGION = sm.REGION AND amr.INPUT_TIME = sm.INPUT_TIME
                            WHERE 1 = 1
                                AND am.IsActive = 'True'
                                AND sm.INPUT_TIME IN ('{_From}','{_To}')"
                },
                ["Volume Band"] = new TableRecord
                {
                    primary_key = "Id",
                    secondary_key = "Name",
                    query = @$"
                            SELECT *
                            FROM pe_records.volumebands vb
                            LEFT JOIN pe_records.volumebandschedules vbs 
                            ON vb.Id = vbs.VolumeBandId AND vb.REGION = vbs.REGION AND vb.INPUT_TIME = vbs.INPUT_TIME
                            WHERE vbs.INPUT_TIME IN ('{_From}','{_To}')"
                },
                ["Execution Profile"] = new TableRecord
                {
                    primary_key = "Id",
                    secondary_key = "Name",
                    query = @$"
                        SELECT * 
                        FROM pe_records.executionprofiles
                        WHERE INPUT_TIME IN ('{_From}','{_To}')"
                },
                ["Order Routing"] = new TableRecord
                {
                    primary_key = "Id",
                    secondary_key = "Priority",
                    query = @$"
                        SELECT * 
                        FROM pe_records.orderroutingrules
                        WHERE INPUT_TIME IN ('{_From}','{_To}')"
                }
            };

            foreach (var _table in tableMappings)
            {
                var _viewRecord = new ViewRecord();
                var TableName = _table.Key;
                var TableRecord = _table.Value;
                _viewRecord.Name = TableName;

                try
                {
                    // 拆字
                    var keyParts = TableRecord.secondary_key?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();

                    // 撈取資料
                    var allData = new List<Dictionary<string, object>>();
                    using (var conn = new MySqlConnection(sql_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new MySqlCommand(TableRecord.query, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    string colName = reader.GetName(i);
                                    object val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    row[colName] = val;
                                }
                                allData.Add(row);
                            }
                        }
                    }

                    var fromData = allData
                        .Where(d => d.ContainsKey("INPUT_TIME") && d["INPUT_TIME"]?.ToString() == _From)
                        .ToList();

                    var toData = allData
                        .Where(d => d.ContainsKey("INPUT_TIME") && d["INPUT_TIME"]?.ToString() == _To)
                        .ToList();

                    if (fromData.Count() == 0)
                    {
                        throw new Exception($"無 {_From.Replace(" 23:59:59", "")} 的資料紀錄，無法進行比對");
                    }

                    if (toData.Count() == 0)
                    {
                        throw new Exception($"無 {_To.Replace(" 23:59:59", "")} 的資料紀錄，無法進行比對");
                    }

                    // 比對差異
                    List<Dictionary<string, object>> diffResults = new();

                    var keys = new List<string> { TableRecord.primary_key, "REGION" };
                    var fromDict = fromData.ToDictionary(row => string.Join("||", keys.Select(k => row[k].ToString())));
                    var toDict = toData.ToDictionary(row => string.Join("||", keys.Select(k => row[k].ToString())));

                    // 處理 DEL
                    foreach (var key in fromDict.Keys.Except(toDict.Keys))
                    {
                        var row = new Dictionary<string, object>(fromDict[key]);
                        row["Diff"] = "DEL";
                        diffResults.Add(row);
                    }

                    // 處理 ADD
                    foreach (var key in toDict.Keys.Except(fromDict.Keys))
                    {
                        var row = new Dictionary<string, object>(toDict[key]);
                        row["Diff"] = "ADD";
                        diffResults.Add(row);
                    }

                    // 處理 MOD
                    foreach (var key in fromDict.Keys.Intersect(toDict.Keys))
                    {
                        var fromRow = fromDict[key];
                        var toRow = toDict[key];

                        bool isModified = false;
                        var modFrom = new Dictionary<string, object>(fromRow);
                        var modTo = new Dictionary<string, object>(toRow);

                        foreach (var col in fromRow.Keys)
                        {
                            if (keyParts.Contains(col) || ignored_col.Contains(col)) continue;

                            var val1 = fromRow[col]?.ToString() ?? "";
                            var val2 = toRow[col]?.ToString() ?? "";

                            if (val1 != val2)
                            {
                                modFrom[col] = $"←{val1}";
                                modTo[col] = $"→{val2}";
                                isModified = true;
                            }
                        }

                        if (isModified)
                        {
                            modFrom["Diff"] = "MOD";
                            modTo["Diff"] = "MOD";
                            diffResults.Add(modFrom);
                            diffResults.Add(modTo);
                        }
                    }

                    // 如果沒有Diff資料，則回傳一行空的
                    if (diffResults.Count == 0)
                    {
                        var emptyRow = new Dictionary<string, object>();

                        var sample = fromData.FirstOrDefault() ?? toData.FirstOrDefault();
                        if (sample != null)
                        {
                            foreach (var col in sample.Keys)
                            {
                                emptyRow[col] = "";
                            }
                        }

                        emptyRow["Diff"] = "";

                        diffResults.Add(emptyRow);
                    }

                    // 重新排序
                    List<string> fixedOrder = new List<string> {
                        "Diff", "INPUT_TIME", "REGION", TableRecord.primary_key,
                    };
                    if (!string.IsNullOrEmpty(TableRecord.secondary_key))
                    {
                        foreach (var key in keyParts)
                        {
                            fixedOrder.Add(key.Trim());
                        }
                    }

                    var allColumns = diffResults
                        .SelectMany(row => row.Keys)
                        .Distinct()
                        .ToList();

                    var remaining = allColumns.Except(fixedOrder).ToList();
                    var finalOrder = fixedOrder.Concat(remaining).ToList();

                    var reOrderedDiffResults = diffResults.Select(row =>
                    {
                        var newRow = new Dictionary<string, object>();
                        foreach (var col in finalOrder)
                        {
                            row.TryGetValue(col, out var val);
                            newRow[col] = val ?? "";
                        }
                        return newRow;
                    }).ToList();

                    _viewRecord.Status = true;
                    _viewRecord.FixColumn = fixedOrder.Count;
                    _viewRecord.View = reOrderedDiffResults;
                }
                catch (Exception ex)
                {
                    _viewRecord.Status = false;
                    _viewRecord.FixColumn = 0;
                    _viewRecord.View = null;
                    _viewRecord.Error_Log = ex.Message;
                }
                finally
                {
                    all_view.Add(_viewRecord);
                }
            }
            return all_view;
        }
    }
}
