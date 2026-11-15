using BunnyLane.Status.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BunnyLane.Status.Tests.Data;

/// <summary>One in-memory SQLite database per test, shared by every context the factory hands out.</summary>
public sealed class TestDb : IDbContextFactory<StatusDb>, IDisposable
{
    private readonly SqliteConnection _connection = new("Filename=:memory:");
    private readonly DbContextOptions<StatusDb> _options;

    public TestDb()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<StatusDb>().UseSqlite(_connection).Options;
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public StatusDb CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
