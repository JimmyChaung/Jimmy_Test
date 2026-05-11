using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace admin_web.Services.DataProductService
{
    public class CompareGodService
    {
        public static readonly string ToolPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tools", "CompareGod");

        public static string MainProgram(string primaryKey, IReadOnlyList<IFormFile> before_files, IReadOnlyList<IFormFile> after_files)
        {
            try
            {
                // 檢查副檔名
                if (!before_files.All(file => file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) ||
                    !after_files.All(file => file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)))
                {
                    return "請勿上傳非 CSV 的檔案！！！";
                }

                // 讀檔
                var before_data = ReadCsv(before_files);
                var after_data = ReadCsv(after_files);

                // 檢查PK
                var check_before = Check_PK(before_data, primaryKey);
                var check_after = Check_PK(after_data, primaryKey);
                if (!string.IsNullOrEmpty(check_before) || !string.IsNullOrEmpty(check_after))
                {
                    var log = $"\nBefore\n{check_before}\nAfter\n{check_after}";
                    return log;
                }

                // 比對
                var result = CompareData(before_data, after_data, primaryKey);
                var fileName = Log2Excel(result, primaryKey);

                return fileName;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // 讀取CSV
        private static Dictionary<string, List<Dictionary<string, string>>> ReadCsv(IReadOnlyList<IFormFile> files)
        {
            var result = new Dictionary<string, List<Dictionary<string, string>>>();

            foreach (var file in files)
            {
                var records = new List<Dictionary<string, string>>();
                using (var stream = file.OpenReadStream())
                using (var reader = new StreamReader(stream, Encoding.GetEncoding("big5")))
                using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = ",",
                    Quote = '"',
                    BadDataFound = null,
                    DetectColumnCountChanges = true,
                    TrimOptions = TrimOptions.Trim,
                    IgnoreBlankLines = true
                }))
                {
                    csv.Context.TypeConverterCache.RemoveConverter<bool>();
                    csv.Context.TypeConverterCache.RemoveConverter<bool?>();

                    if (csv.Read())
                    {
                        csv.ReadHeader();
                        var headerRow = csv.HeaderRecord;

                        if (headerRow == null)
                        {
                            // 沒有標頭
                            continue;
                        }

                        while (csv.Read())
                        {
                            var row = new Dictionary<string, string>();
                            for (int i = 0; i < csv.HeaderRecord.Length; i++)
                            {
                                string field = csv.GetField(i);
                                row[headerRow[i]] = field.Trim();
                            }
                            records.Add(row);
                        }

                        //while (csv.Read())
                        //{
                        //    var row = new Dictionary<string, string>();
                        //    foreach (var header in headerRow)
                        //    {
                        //        row[header] = csv.GetField(header);
                        //    }
                        //    records.Add(row);
                        //}
                    }
                    else
                    {
                        // 沒有資料
                    }
                }
                var _FileName = Path.GetFileNameWithoutExtension(file.FileName.Replace("_Before", "").Replace("_After", ""));
                result[_FileName] = records;
            }

            return result;
        }

        // 檢查每個檔案欄位
        public static string Check_PK(
        Dictionary<string, List<Dictionary<string, string>>> data,
        string primaryKey)
        {
            var primaryKeyList = primaryKey.Split(',').Select(k => k.Trim()).ToList();

            List<string> missingKeys = new List<string>();
            List<string> duplicateKeys = new List<string>();

            foreach (var file in data.Keys)
            {
                var fileData = data[file];

                // 檢查是否缺少 PK 欄位
                if (fileData.Count > 0)
                {
                    var firstRecord = fileData.First();
                    var missingInRecord = primaryKeyList.Where(pk => !firstRecord.ContainsKey(pk)).ToList();
                    if (missingInRecord.Any())
                    {
                        missingKeys.Add($"檔案：'{file}' 缺少欄位 {string.Join(", ", missingInRecord)}");
                    }
                }

                // 檢查是否有重複的 PK
                var keyCounts = new Dictionary<string, int>();

                foreach (var record in fileData)
                {
                    var keyValues = primaryKeyList.Select(pk => record.ContainsKey(pk) ? record[pk] : "").ToList();
                    string compositeKey = string.Join("_", keyValues);

                    if (!string.IsNullOrEmpty(compositeKey))
                    {
                        if (!keyCounts.ContainsKey(compositeKey))
                        {
                            keyCounts[compositeKey] = 1;
                        }
                        else
                        {
                            keyCounts[compositeKey]++;
                        }
                    }
                }

                foreach (var kvp in keyCounts.Where(kv => kv.Value > 1))
                {
                    duplicateKeys.Add($"檔案：'{file}' PK重複 {kvp.Key} (出現 {kvp.Value} 次)");
                }
            }

            var allErrors = new List<string>();
            if (missingKeys.Any()) allErrors.AddRange(missingKeys);
            if (duplicateKeys.Any()) allErrors.AddRange(duplicateKeys);

            return allErrors.Any() ? string.Join("\n", allErrors) : null;
        }

        // 比對
        public static Dictionary<string, Dictionary<string, List<string>>> CompareData(
        Dictionary<string, List<Dictionary<string, string>>> before_data,
        Dictionary<string, List<Dictionary<string, string>>> after_data,
        string primaryKey)
        {
            var result = new Dictionary<string, Dictionary<string, List<string>>>();
            var primaryKeyList = primaryKey.Split(',').Select(k => k.Trim()).ToList();

            foreach (var file in before_data.Keys)
            {
                try
                {
                    var added = new List<string>();
                    var deleted = new List<string>();
                    var modified = new List<string>();
                    var reordered = new List<string>();
                    var abnormal = new List<string>();

                    // 以before為主，after沒有該檔案就略過
                    if (!after_data.ContainsKey(file)) continue;

                    var beforeRecords = before_data[file];
                    var afterRecords = after_data[file];

                    // 檢查 before 重複 key
                    var beforeGroups = beforeRecords
                        .GroupBy(row => string.Join(",", primaryKeyList.Select(pk => row.ContainsKey(pk) ? row[pk] : "")))
                        .Where(g => g.Count() > 1)
                        .ToList();

                    foreach (var group in beforeGroups)
                    {
                        foreach (var row in group)
                        {
                            var rowData = string.Join(", ", row.Select(kv => $"{kv.Key}: {kv.Value}"));
                            abnormal.Add($"{group.Key},{rowData}");
                        }
                    }

                    beforeRecords = beforeRecords
                        .GroupBy(row => string.Join(",", primaryKeyList.Select(pk => row.ContainsKey(pk) ? row[pk] : "")))
                        .Where(g => g.Count() == 1)
                        .Select(g => g.First())
                        .ToList();

                    // 檢查 after 重複 key
                    var afterGroups = afterRecords
                        .GroupBy(row => string.Join(",", primaryKeyList.Select(pk => row.ContainsKey(pk) ? row[pk] : "")))
                        .Where(g => g.Count() > 1)
                        .ToList();

                    foreach (var group in afterGroups)
                    {
                        foreach (var row in group)
                        {
                            var rowData = string.Join(", ", row.Select(kv => $"{kv.Key}: {kv.Value}"));
                            abnormal.Add($"{group.Key},{rowData}");
                        }
                    }

                    afterRecords = afterRecords
                        .GroupBy(row => string.Join(",", primaryKeyList.Select(pk => row.ContainsKey(pk) ? row[pk] : "")))
                        .Where(g => g.Count() == 1)
                        .Select(g => g.First())
                        .ToList();


                    var beforeDict = beforeRecords.ToDictionary(
                        row => string.Join(",", primaryKeyList.Select(pk => row.ContainsKey(pk) ? row[pk] : "")),
                        row => row
                    );

                    var afterDict = afterRecords.ToDictionary(
                        row => string.Join(",", primaryKeyList.Select(pk => row.ContainsKey(pk) ? row[pk] : "")),
                        row => row
                    );

                    // 紀錄新增
                    foreach (var key in afterDict.Keys)
                    {
                        if (!beforeDict.ContainsKey(key))
                        {
                            // 新增的PrimaryKey
                            var addedRow = string.Join(", ", afterDict[key].Select(kv => $"{kv.Key}: {kv.Value}"));
                            added.Add(addedRow);
                        }
                    }

                    // 紀錄刪除
                    foreach (var key in beforeDict.Keys)
                    {
                        if (!afterDict.ContainsKey(key))
                        {
                            // 消失的PrimaryKey
                            var deletedRow = string.Join(", ", beforeDict[key].Select(kv => $"{kv.Key}: {kv.Value}"));
                            deleted.Add(deletedRow);
                        }
                    }

                    // 紀錄修改
                    foreach (var key in beforeDict.Keys.Intersect(afterDict.Keys))
                    {
                        var beforeRow = beforeDict[key];
                        var afterRow = afterDict[key];

                        var changes = new List<string>();

                        foreach (var header in beforeRow.Keys)
                        {
                            var beforeValue = beforeRow.ContainsKey(header) ? beforeRow[header] : "[NONE]";
                            var afterValue = afterRow.ContainsKey(header) ? afterRow[header] : "[NONE]";

                            if (beforeValue != afterValue)
                            {
                                changes.Add($"{header}: {beforeValue} → {afterValue}");
                            }
                            else
                            {
                                changes.Add($"{header}: {beforeValue}");
                            }
                        }

                        if (changes.Any())
                        {
                            var modifiedRow = string.Join(", ", changes);
                            if (modifiedRow.Contains("→"))
                            {
                                modified.Add(modifiedRow);
                            }
                        }
                    }

                    // 紀錄排序
                    var beforeOrder = beforeRecords
                        .Select((r, index) => new
                        {
                            Row = string.Join(",", primaryKeyList.Select(pk => r[pk])),
                            Index = index + 2
                        })
                        .ToList();

                    var afterOrder = afterRecords
                        .Select((r, index) => new
                        {
                            Row = string.Join(",", primaryKeyList.Select(pk => r[pk])),
                            Index = index + 2
                        })
                        .ToList();

                    foreach (var before in beforeOrder)
                    {
                        var after = afterOrder.FirstOrDefault(a => a.Row == before.Row);
                        if (after != null && after.Index != before.Index)
                        {
                            var beforePK = before.Row.Split(",");
                            var afterPK = after.Row.Split(",");
                            var pkDetails = string.Join(", ", primaryKeyList.Select((pk, idx) => $"{pk}: {beforePK[idx].Trim()}"));
                            reordered.Add($"{pkDetails}, Before: {before.Index}, After: {after.Index}");
                        }
                    }

                    result[file] = new Dictionary<string, List<string>>()
                    {
                        { "新增", added },
                        { "刪除", deleted },
                        { "修改", modified },
                        { "排序", reordered },
                        { "異常", abnormal}
                    };
                }
                catch (Exception ex)
                {
                    result[file] = new Dictionary<string, List<string>>()
                    {
                        { "錯誤", new List<string>(){ $"{ex.Message}" } },
                    };
                }
            }

            return result;
        }

        // 輸出檔案
        public static string Log2Excel(Dictionary<string, Dictionary<string, List<string>>> compareResult, string primaryKeys)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            string fileName = $"compare_result_{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
            string filePath = Path.Combine(ToolPath, fileName);

            // PK 設置拆段
            List<string> primaryKeyList = primaryKeys.Split(',').Select(pk => pk.Trim()).ToList();

            using (var package = new ExcelPackage())
            {
                foreach (var fileEntry in compareResult)
                {
                    string sheetName = fileEntry.Key;
                    var worksheet = package.Workbook.Worksheets.Add(sheetName);
                    var changes = fileEntry.Value;

                    if (changes.ContainsKey("錯誤"))
                    {
                        worksheet.Cells[1, 1].Value = "Error";
                        var errorMessages = changes["錯誤"];

                        if (errorMessages != null && errorMessages.Count > 0)
                        {
                            worksheet.Cells[1, 2].Value = errorMessages[0];
                        }
                        continue;
                    }

                    var allFields = new HashSet<string>();

                    // 記錄欄位名稱
                    foreach (var _change in changes)
                    {
                        var changeType = _change.Value;
                        if (_change.Key == "排序") continue;

                        foreach (var record in changeType)
                        {
                            var fields = record.Split(", ").Select(f => f.Split(": ")[0]);
                            foreach (var field in fields)
                            {
                                allFields.Add(field);
                            }
                        }
                    }

                    // 第一張表: 新增, 刪除, 修改
                    var columns = allFields.ToList();
                    int row = 1;
                    int space_row = -1;
                    //if (changes["新增"].Any() || changes["刪除"].Any() || changes["修改"].Any())
                    if (changes.ContainsKey("新增") || changes.ContainsKey("刪除") || changes.ContainsKey("修改"))
                    {
                        worksheet.Cells[row, 1].Value = "類型";
                        for (int col = 0; col < columns.Count; col++)
                        {
                            worksheet.Cells[row, col + 2].Value = columns[col];
                        }
                        space_row = 1;
                    }

                    row++;

                    // "新增", "刪除", "修改"
                    foreach (var section in changes.Keys)
                    {
                        if (section == "排序") continue;

                        foreach (var record in changes[section])
                        {
                            var fieldValues = record.Split(", ")
                                .Select(f => f.Contains(": ") ? f.Split(": ")[1] : "")
                                .ToList();

                            worksheet.Cells[row, 1].Value = section;

                            for (int col = 0; col < fieldValues.Count; col++)
                            {
                                //worksheet.Cells[row, col + 2].Value = fieldValues[col];
                                var cell = worksheet.Cells[row, col + 2];
                                cell.Value = fieldValues[col];

                                if (fieldValues[col].Contains("→"))
                                {
                                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightPink);
                                }
                            }

                            if (section == "錯誤" || section == "異常")
                            {
                                int colCount = fieldValues.Count + 1;
                                for (int col = 1; col <= colCount; col++)
                                {
                                    var cell = worksheet.Cells[row, col];
                                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightCoral);
                                }
                            }

                            row++;
                        }
                    }

                    // 第二張表: 排序
                    int sortingRow = 1;
                    if (changes.ContainsKey("排序") && changes["排序"].Any())
                    {
                        worksheet.Cells[sortingRow, columns.Count + 2 + space_row].Value = "類型";
                        for (int i = 0; i < primaryKeyList.Count; i++)
                        {
                            worksheet.Cells[sortingRow, columns.Count + 3 + space_row + i].Value = primaryKeyList[i];
                        }
                        worksheet.Cells[sortingRow, columns.Count + 3 + space_row + primaryKeyList.Count].Value = "Before";
                        worksheet.Cells[sortingRow, columns.Count + 4 + space_row + primaryKeyList.Count].Value = "After";

                        sortingRow++;
                        foreach (var change in changes["排序"])
                        {
                            // PK: _, Before: _, After: _
                            var changeDetails = change.Split(", ");

                            worksheet.Cells[sortingRow, columns.Count + 2 + space_row].Value = "排序";

                            for (int i = 0; i < primaryKeyList.Count; i++)
                            {
                                worksheet.Cells[sortingRow, columns.Count + 3 + space_row + i].Value = changeDetails[i].Split(": ")[1];
                            }
                            worksheet.Cells[sortingRow, columns.Count + 3 + space_row + primaryKeyList.Count].Value = changeDetails[1].Split(": ")[1];
                            worksheet.Cells[sortingRow, columns.Count + 4 + space_row + primaryKeyList.Count].Value = changeDetails[2].Split(": ")[1];

                            sortingRow++;
                        }
                    }

                }

                File.WriteAllBytes(filePath, package.GetAsByteArray());
            }

            return fileName;
        }
    }
}