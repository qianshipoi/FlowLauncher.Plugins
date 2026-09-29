using ClosedXML.Excel;

using FlowLauncher.Plugin.PasswordManager.Models;

using System.IO;

namespace FlowLauncher.Plugin.PasswordManager.Services;

/// <summary>
/// 密码数据导入导出服务
/// </summary>
public sealed class ImportExportService
{
    private readonly PasswordStore _store;
    private readonly string _pluginDirectory;

    public ImportExportService(PasswordStore store, string pluginDirectory)
    {
        _store = store;
        _pluginDirectory = pluginDirectory;
    }

    /// <summary>
    /// 导出为加密的 DB 文件（复制 SQLCipher 加密的数据库和密钥文件）
    /// </summary>
    public void ExportEncryptedDb(string exportPath)
    {
        var srcDb = Path.Combine(_pluginDirectory, "passwords.db");
        var srcKey = Path.Combine(_pluginDirectory, "passwords.dbkey");

        if (!File.Exists(srcDb))
            throw new FileNotFoundException("数据库文件不存在");

        File.Copy(srcDb, exportPath, overwrite: true);

        // 将密钥文件一并复制到同目录下
        if (File.Exists(srcKey))
        {
            var keyPath = Path.ChangeExtension(exportPath, ".dbkey");
            File.Copy(srcKey, keyPath, overwrite: true);
        }
    }

    /// <summary>
    /// 导出为 Excel 文件
    /// </summary>
    public int ExportToExcel(string exportPath)
    {
        var entries = _store.GetAll();
        if (entries.Count == 0)
            throw new InvalidOperationException("没有可导出的数据");

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Passwords");

        // 表头
        ws.Cell(1, 1).Value = "标题";
        ws.Cell(1, 2).Value = "用户名";
        ws.Cell(1, 3).Value = "密码";
        ws.Cell(1, 4).Value = "URL";
        ws.Cell(1, 5).Value = "标签";
        ws.Cell(1, 6).Value = "备注";
        ws.Cell(1, 7).Value = "创建时间";
        ws.Cell(1, 8).Value = "更新时间";

        // 表头样式
        var headerRange = ws.Range(1, 1, 1, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        for (int i = 0; i < entries.Count; i++)
        {
            var row = i + 2;
            var e = entries[i];
            ws.Cell(row, 1).Value = e.Title;
            ws.Cell(row, 2).Value = e.Username;
            ws.Cell(row, 3).Value = PasswordCrypto.Decrypt(e.EncryptedPassword);
            ws.Cell(row, 4).Value = e.Url;
            ws.Cell(row, 5).Value = e.Tags;
            ws.Cell(row, 6).Value = e.Notes;
            ws.Cell(row, 7).Value = e.CreatedAt;
            ws.Cell(row, 8).Value = e.UpdatedAt;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        workbook.SaveAs(exportPath);

        return entries.Count;
    }

    /// <summary>
    /// 导出为 CSV 文件
    /// </summary>
    public int ExportToCsv(string exportPath)
    {
        var entries = _store.GetAll();
        if (entries.Count == 0)
            throw new InvalidOperationException("没有可导出的数据");

        using var writer = new StreamWriter(exportPath, false, new System.Text.UTF8Encoding(true));
        writer.WriteLine("标题,用户名,密码,URL,标签,备注,创建时间,更新时间");

        foreach (var e in entries)
        {
            var password = PasswordCrypto.Decrypt(e.EncryptedPassword);
            writer.WriteLine(string.Join(",",
                CsvEscape(e.Title),
                CsvEscape(e.Username),
                CsvEscape(password),
                CsvEscape(e.Url),
                CsvEscape(e.Tags),
                CsvEscape(e.Notes),
                CsvEscape(e.CreatedAt),
                CsvEscape(e.UpdatedAt)));
        }

        return entries.Count;
    }

    /// <summary>
    /// 从 Excel 文件导入，返回解析后的条目列表
    /// </summary>
    public List<PasswordEntry> ImportFromExcel(string importPath)
    {
        var entries = new List<PasswordEntry>();

        using var workbook = new XLWorkbook(importPath);
        var ws = workbook.Worksheets.First();

        // 从第 2 行开始读取（跳过表头）
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; row <= lastRow; row++)
        {
            var title = ws.Cell(row, 1).GetString().Trim();
            var username = ws.Cell(row, 2).GetString().Trim();
            var password = ws.Cell(row, 3).GetString();
            var url = ws.Cell(row, 4).GetString().Trim();
            var tags = ws.Cell(row, 5).GetString().Trim();
            var notes = ws.Cell(row, 6).GetString().Trim();

            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(username))
                continue;

            entries.Add(new PasswordEntry
            {
                Title = title,
                Username = username,
                EncryptedPassword = PasswordCrypto.Encrypt(password),
                Url = url,
                Tags = tags,
                Notes = notes,
            });
        }

        return entries;
    }

    /// <summary>
    /// 从 CSV 文件导入，返回解析后的条目列表
    /// </summary>
    public List<PasswordEntry> ImportFromCsv(string importPath)
    {
        var entries = new List<PasswordEntry>();

        using var reader = new StreamReader(importPath, System.Text.Encoding.UTF8);

        // 跳过表头
        if (reader.ReadLine() is null)
            return entries;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = CsvParseLine(line);
            if (fields.Count < 3)
                continue;

            var title = fields[0].Trim();
            var username = fields[1].Trim();
            var password = fields[2];
            var url = fields.Count > 3 ? fields[3].Trim() : "";
            var tags = fields.Count > 4 ? fields[4].Trim() : "";
            var notes = fields.Count > 5 ? fields[5].Trim() : "";

            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(username))
                continue;

            entries.Add(new PasswordEntry
            {
                Title = title,
                Username = username,
                EncryptedPassword = PasswordCrypto.Encrypt(password),
                Url = url,
                Tags = tags,
                Notes = notes,
            });
        }

        return entries;
    }

    /// <summary>
    /// 将导入的条目批量写入数据库
    /// </summary>
    public int ImportEntries(List<PasswordEntry> entries)
    {
        int imported = 0;
        foreach (var entry in entries)
        {
            _store.Add(entry);
            imported++;
        }
        return imported;
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";

        return value;
    }

    private static List<string> CsvParseLine(string line)
    {
        var fields = new List<string>();
        int i = 0;
        while (i <= line.Length)
        {
            if (i == line.Length) { fields.Add(""); break; }

            if (line[i] == '"')
            {
                // 引号包裹的字段
                i++;
                var sb = new System.Text.StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                        }
                        else
                        {
                            i++;
                            break;
                        }
                    }
                    else
                    {
                        sb.Append(line[i]);
                        i++;
                    }
                }
                fields.Add(sb.ToString());
                // 跳过逗号
                if (i < line.Length && line[i] == ',') i++;
            }
            else
            {
                // 无引号字段
                int start = i;
                while (i < line.Length && line[i] != ',') i++;
                fields.Add(line[start..i]);
                if (i < line.Length) i++; // 跳过逗号
            }
        }
        return fields;
    }
}
