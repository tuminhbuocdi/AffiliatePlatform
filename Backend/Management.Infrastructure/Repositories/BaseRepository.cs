using Management.Infrastructure.Db;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Management.Infrastructure.Repositories;

public class BaseRepository : IDisposable
{
    protected readonly DbConnectionFactory _factory;
    private bool _disposed = false;

    protected virtual string ConnectionName => "Default";

    public BaseRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    protected IDbConnection CreateConnection()
        => _factory.Create(ConnectionName);

    protected async Task<T> WithConnectionAsync<T>(Func<IDbConnection, Task<T>> func)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BaseRepository));
        
        using var connection = CreateConnection();
        await ((SqlConnection)connection).OpenAsync();
        return await func(connection);
    }

    protected T WithConnection<T>(Func<IDbConnection, T> func)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BaseRepository));
        
        using var connection = CreateConnection();
        connection.Open();
        return func(connection);
    }

    public virtual void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
