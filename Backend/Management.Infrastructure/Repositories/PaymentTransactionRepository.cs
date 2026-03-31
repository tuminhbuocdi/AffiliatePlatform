using Dapper;
using Management.Domain.Entities;
using Management.Infrastructure.Db;
using System.Data;

namespace Management.Infrastructure.Repositories;

public class PaymentTransactionRepository : BaseRepository
{
    public PaymentTransactionRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<IEnumerable<PaymentTransaction>> GetByUser(Guid userId, int page = 1, int pageSize = 20)
    {
        using var conn = CreateConnection();

        return await conn.QueryAsync<PaymentTransaction>(
            "dbo.Proc_PaymentTransactions_GetByUser",
            new { UserId = userId, Page = page, PageSize = pageSize },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<Guid> Insert(PaymentTransaction tx)
    {
        using var conn = CreateConnection();

        return await conn.ExecuteScalarAsync<Guid>(
            "dbo.Proc_PaymentTransactions_Insert",
            tx,
            commandType: CommandType.StoredProcedure
        );
    }
}
