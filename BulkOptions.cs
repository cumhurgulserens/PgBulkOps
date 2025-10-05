namespace PgBulkOps;

public class BulkOptions
{
    /// <summary>
    /// Kaç satırda bir progress bildirimi yapılacağını belirler.
    /// </summary>
    public int BatchSize { get; set; } = 50_000;

    /// <summary>
    /// İlerleme callback'i.
    /// </summary>
    public Action<BulkProgress>? OnProgress { get; set; }

    /// <summary>
    /// Eğer true ise property isimleri snake_case'e dönüştürülür.
    /// </summary>
    public bool UseSnakeCase { get; set; } = true;
}

public record BulkProgress(int Rows);
