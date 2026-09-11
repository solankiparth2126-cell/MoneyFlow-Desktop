using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MoneyFlow.Services.ImportExport;

public static class CsvUtility
{
    public static List<List<string>> ParseCsv(string csvContent)
    {
        var records = new List<List<string>>();
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return records;
        }

        using var reader = new StringReader(csvContent);
        var currentRecord = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;
        int ch;

        while ((ch = reader.Read()) != -1)
        {
            char c = (char)ch;

            if (inQuotes)
            {
                if (c == '"')
                {
                    int next = reader.Peek();
                    if (next == '"')
                    {
                        // Escaped quote: "" -> "
                        currentField.Append('"');
                        reader.Read(); // Consume the second quote
                    }
                    else
                    {
                        // End of quoted field
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                }
                else if (c == '\r')
                {
                    if (reader.Peek() == '\n')
                    {
                        reader.Read(); // consume \n
                    }
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    if (currentRecord.Count > 0 && !IsEmptyRecord(currentRecord))
                    {
                        records.Add(currentRecord);
                    }
                    currentRecord = new List<string>();
                }
                else if (c == '\n')
                {
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    if (currentRecord.Count > 0 && !IsEmptyRecord(currentRecord))
                    {
                        records.Add(currentRecord);
                    }
                    currentRecord = new List<string>();
                }
                else
                {
                    currentField.Append(c);
                }
            }
        }

        // Add the last field and record if any
        if (currentField.Length > 0 || currentRecord.Count > 0)
        {
            currentRecord.Add(currentField.ToString());
            if (!IsEmptyRecord(currentRecord))
            {
                records.Add(currentRecord);
            }
        }

        return records;
    }

    private static bool IsEmptyRecord(List<string> record)
    {
        return record.Count == 0 || (record.Count == 1 && string.IsNullOrWhiteSpace(record[0]));
    }

    public static string WriteCsv(IEnumerable<IEnumerable<string>> records)
    {
        var sb = new StringBuilder();
        foreach (var row in records)
        {
            bool first = true;
            foreach (var field in row)
            {
                if (!first) sb.Append(',');
                first = false;

                string val = field ?? string.Empty;
                bool needsQuotes = val.Contains(',') || val.Contains('"') || val.Contains('\n') || val.Contains('\r');
                if (needsQuotes)
                {
                    sb.Append('"');
                    sb.Append(val.Replace("\"", "\"\""));
                    sb.Append('"');
                }
                else
                {
                    sb.Append(val);
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
