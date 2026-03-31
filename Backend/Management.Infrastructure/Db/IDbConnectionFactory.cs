using System.Data;

namespace Management.Infrastructure.Db
{
    public interface IDbConnectionFactory
    {
        IDbConnection Create(string connectionName = "Default");
    }
}
