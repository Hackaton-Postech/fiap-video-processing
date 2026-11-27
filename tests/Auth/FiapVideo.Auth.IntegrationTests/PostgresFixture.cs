using FiapVideo.Auth.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FiapVideo.Auth.IntegrationTests;

// Um único PostgreSQL descartável para todos os testes da coleção. As migrations rodam de verdade.
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CriarContexto();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public AuthDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AuthDbContext>().UseNpgsql(ConnectionString).Options);
}

[CollectionDefinition(Nome)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Nome = "postgres";
}
