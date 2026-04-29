using Microsoft.EntityFrameworkCore;
using B2B.Payment.Application.Interfaces;
using B2B.Shared.Infrastructure.Persistence;
using PaymentEntity = B2B.Payment.Domain.Entities.Payment;

namespace B2B.Payment.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(PaymentDbContext context)
    : BaseRepository<PaymentEntity, Guid, PaymentDbContext>(context), IPaymentRepository
{
    public async Task<PaymentEntity?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);
}
