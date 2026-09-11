using System;
using System.Collections.Generic;

namespace MoneyFlow.Core.DTOs;

public enum ImportEntityType
{
    Ledgers = 1,
    StockItems = 2,
    Vouchers = 3
}

public enum DuplicateAction
{
    Skip = 1,
    Update = 2,
    Reject = 3
}

public enum ImportRowStatus
{
    Valid = 1,
    Duplicate = 2,
    Error = 3
}

public class ImportPreviewRowDto
{
    public int RowNumber { get; set; }
    public ImportRowStatus Status { get; set; }
    public string PrimaryIdentifier { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    // Staged payload fields
    public Dictionary<string, string> RawFields { get; set; } = new();
}

public class ImportPreviewResultDto
{
    public ImportEntityType EntityType { get; set; }
    public int TotalRows { get; set; }
    public int ValidCount { get; set; }
    public int DuplicateCount { get; set; }
    public int ErrorCount { get; set; }
    public List<ImportPreviewRowDto> Rows { get; set; } = new();

    public bool CanExecute => TotalRows > 0 && ErrorCount == 0;
}

public class ImportExecutionResultDto
{
    public bool Success { get; set; }
    public int TotalProcessed { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Messages { get; set; } = new();
}

public enum ExportFormat
{
    Csv = 1,
    Json = 2
}

public class ExportOptionsDto
{
    public ImportEntityType EntityType { get; set; }
    public ExportFormat Format { get; set; } = ExportFormat.Csv;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
