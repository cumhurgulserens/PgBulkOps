																						 PgBulkOps
PgBulkOps is a high-performance .NET library for PostgreSQL bulk operations.
It provides ultra-fast bulk insert and bulk update methods using PostgreSQL’s native binary COPY protocol.

Built on top of Npgsql, PgBulkOps can handle millions of rows in just a few seconds.

✨ Features
Bulk insert with PostgreSQL binary COPY
Bulk update using COPY + temporary table + UPDATE ... FROM join
Optional PascalCase → snake_case column name conversion
Progress callback with configurable batch size
Fully async/await compatible
📦 Installation
dotnet add package PgBulkOps

✨ Quick Start

Entity Definition

public class User
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Country { get; set; } = default!;
    public DateTime? LastLogin { get; set; }
}

Bulk Insert Example

using Npgsql;
using PgBulkOps;

await using var conn = new NpgsqlConnection("Host=localhost;Database=test;Username=postgres;Password=secret");

var rnd = new Random();
var users = Enumerable.Range(1, 100_000)
    .Select(i => new User
    {
        Id = i,
        Name = $"User{i}",
        Email = $"user{i}@example.com",
        Age = rnd.Next(18, 70),
        IsActive = i % 2 == 0,
        Balance = (decimal)(rnd.NextDouble() * 10000),
        CreatedAt = DateTime.UtcNow,
        Country = i % 2 == 0 ? "TR" : "US",
        LastLogin = DateTime.UtcNow.AddMinutes(-rnd.Next(1, 10000))
    });

await conn.BulkInsertAsync(users, "Users", opts =>
{
    opts.BatchSize = 50_000;
    opts.UseSnakeCase = true;
    opts.OnProgress = p => Console.WriteLine($"Inserted {p.Rows} rows...");
});

Bulk Update Example

// Update existing users
foreach (var user in users)
    user.Name = user.Name + "_updated";

await conn.BulkUpdateAsync(users, "Users", "Id", opts =>
{
    opts.BatchSize = 50_000;
    opts.UseSnakeCase = true;
    opts.OnProgress = p => Console.WriteLine($"Updated {p.Rows} rows...");
});

⚡ Performance
Typical throughput (depending on hardware, WAL, and indexes):

Bulk Insert → 300k–400k rows/sec

Bulk Update → 30k–80k rows/sec

In benchmarks, inserting 1,000,000 rows often completes in ~3 seconds.

⚠️ Notes
PostgreSQL user must have INSERT/UPDATE privileges.

PascalCase properties map automatically to snake_case columns if UseSnakeCase = true.

For massive loads, consider disabling indexes and triggers temporarily.

📄 License
MIT License – maintained by Onventus.