using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace admin_web.Services.Querysql
{
    public class QuerydatabicksService
    {

        private readonly string _workspaceUrl;
        private readonly string _patToken;
        private readonly string _warehouseId;

        public QuerydatabicksService(string workspaceUrl, string patToken, string warehouseId)
        {
            _workspaceUrl = workspaceUrl;
            _patToken = patToken;
            _warehouseId = warehouseId;
        }

        // 執行 SQL 查詢，回傳結果 List<Dictionary<string, object>>
        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string sqlQuery)
        {
            Debug.WriteLine(sqlQuery);
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(_workspaceUrl);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _patToken);
            var payload = new
            {
                statement = sqlQuery,
                warehouse_id = _warehouseId
            };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            Debug.WriteLine("Sending SQL query to Databricks...");
            var response = await client.PostAsync("/api/2.0/sql/statements", content);
            var result = await response.Content.ReadAsStringAsync();
            Debug.WriteLine("Result:");
            Debug.WriteLine(result);

            // --------------------20260205 new data ---------------------------- 
            // ❌ 呼叫失敗直接丟例外（很重要）
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Databricks error: {result}");
            }

            var data = new List<Dictionary<string, object>>();

            using var doc = JsonDocument.Parse(result);



            var statementId = doc.RootElement.GetProperty("statement_id").GetString();

            //  輪詢 / 等待結果
            string status;
            JsonElement resultElement;

            do
            {
                await Task.Delay(500); // 每 0.5 秒查一次
                var getResp = await client.GetAsync($"/api/2.0/sql/statements/{statementId}");
                var getResult = await getResp.Content.ReadAsStringAsync();
                using var getDoc = JsonDocument.Parse(getResult);

                var root = getDoc.RootElement;
                status = root.GetProperty("status").GetProperty("state").GetString()!;

                // result 可能還沒出現
            } while (status == "PENDING" || status == "RUNNING");

            // 確認是否 SUCCEEDED
            if (status != "SUCCEEDED")
            {
                throw new Exception($"Databricks SQL failed with status {status}");
            }

            if (!doc.RootElement.TryGetProperty("result", out resultElement))
                return data;

            if (!doc.RootElement.TryGetProperty("manifest", out var manifestElement) ||
                !manifestElement.TryGetProperty("schema", out var schemaElement) ||
                !schemaElement.TryGetProperty("columns", out var columnsElement))
            {
                // 如果 manifest 或 schema 任一不存在 → 回空
                return data;
            }

            Debug.WriteLine(columnsElement);


            // data_array 防呆
            if (!resultElement.TryGetProperty("data_array", out var rows))
            {
                return data;
            }


            var columns = schemaElement.GetProperty("columns");

            rows = resultElement.GetProperty("data_array");



            //// 拿欄位名稱
            var columnNames = new List<string>();
            foreach (var col in columns.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString();
                if (!string.IsNullOrEmpty(name))
                    columnNames.Add(name);
                else
                    columnNames.Add("name" + columnNames.Count); // 避免 null
            }

            // 拿資料
            foreach (var row in rows.EnumerateArray())
            {
                var dict = new Dictionary<string, object>();
                for (int i = 0; i < columnNames.Count; i++)
                {
                    // row[i] 對應欄位 columnNames[i]
                    dict[columnNames[i]] = row[i].ValueKind == JsonValueKind.Null ? null : row[i].ToString();
                }
                data.Add(dict);
            }

            // 回傳
            return data;
        }

        public async Task<List<Dictionary<string, string>>> ExecuteQueryAllDataDownload(string sqlQuery)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(_workspaceUrl);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _patToken);

            var payload = new
            {
                statement = sqlQuery,
                warehouse_id = _warehouseId,
                disposition = "EXTERNAL_LINKS",
                format = "CSV",
                wait_timeout = "0s"
            };

            var postRes = await client.PostAsync(
                "/api/2.0/sql/statements",
                new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            );
            postRes.EnsureSuccessStatusCode();

            var postBody = await postRes.Content.ReadAsStringAsync();
            var postJson = JsonDocument.Parse(postBody).RootElement;
            var statementId = postJson.GetProperty("statement_id").GetString();

            JsonElement root;
            while (true)
            {
                await Task.Delay(2000);

                var pollRes = await client.GetAsync($"/api/2.0/sql/statements/{statementId}");
                pollRes.EnsureSuccessStatusCode();

                var pollBody = await pollRes.Content.ReadAsStringAsync();
                root = JsonDocument.Parse(pollBody).RootElement;

                var state = root.GetProperty("status").GetProperty("state").GetString();

                if (state == "SUCCEEDED") break;
                if (state is "FAILED" or "CANCELED" or "CLOSED")
                    throw new Exception($"查詢失敗，狀態：{state}，訊息：" +
                        root.GetProperty("status").GetProperty("error").GetProperty("message").GetString());
            }

            var columns = root
                .GetProperty("manifest")
                .GetProperty("schema")
                .GetProperty("columns")
                .EnumerateArray()
                .Select(c => c.GetProperty("name").GetString())
                .ToList();

            var allRows = new List<Dictionary<string, string>>();
            var chunkIndex = 0;

            while (true)
            {
                // 取該 chunk 的外部連結
                JsonElement linksElement;
                if (chunkIndex == 0)
                {
                    // 第一頁在初始回應裡
                    linksElement = root.GetProperty("result").GetProperty("external_links");
                }
                else
                {
                    // 後續頁要另外呼叫
                    var chunkRes = await client.GetAsync(
                        $"/api/2.0/sql/statements/{statementId}/result/chunks/{chunkIndex}");
                    chunkRes.EnsureSuccessStatusCode();

                    var chunkBody = await chunkRes.Content.ReadAsStringAsync();
                    linksElement = JsonDocument.Parse(chunkBody).RootElement
                        .GetProperty("external_links");
                }

                foreach (var link in linksElement.EnumerateArray())
                {
                    var url = link.GetProperty("external_link").GetString();
                    var expireTime = link.GetProperty("expiration").GetString();

                    // 下載 CSV（external link 不需要 Bearer token）
                    using var downloadClient = new HttpClient();
                    var csvBytes = await downloadClient.GetByteArrayAsync(url);
                    var csvText = Encoding.UTF8.GetString(csvBytes);

                    // 解析 CSV（第一個 chunk 第一個 link 有 header）
                    var csvRows = ParseCsv(csvText, hasHeader: chunkIndex == 0);
                    foreach (var row in csvRows)
                    {
                        var dict = new Dictionary<string, string>();
                        for (int i = 0; i < columns.Count && i < row.Count; i++)
                            dict[columns[i]] = row[i];
                        allRows.Add(dict);
                    }
                }

                // 確認是否有下一頁
                var nextChunkIndex = root
                    .GetProperty("result")
                    .TryGetProperty("next_chunk_index", out var next)
                    ? next.GetInt32()
                    : (int?)null;

                if (nextChunkIndex == null) break;
                chunkIndex = nextChunkIndex.Value;
            }

            return allRows;
        }

        // ── CSV 解析（處理欄位內有逗號或換行的情況）─────────────
        private List<List<string>> ParseCsv(string csvText, bool hasHeader)
        {
            var result = new List<List<string>>();
            var lines = csvText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var start = hasHeader ? 1 : 0;  // 跳過 header 行

            for (int i = start; i < lines.Length; i++)
            {
                var row = new List<string>();
                var line = lines[i].TrimEnd('\r');
                var inQuote = false;
                var field = new System.Text.StringBuilder();

                foreach (var ch in line)
                {
                    if (ch == '"') { inQuote = !inQuote; }
                    else if (ch == ',' && !inQuote) { row.Add(field.ToString()); field.Clear(); }
                    else { field.Append(ch); }
                }
                row.Add(field.ToString());
                result.Add(row);
            }

            return result;
        }
    }
}
