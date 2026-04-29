using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using PaymentEntity = B2B.Payment.Domain.Entities.Payment;

namespace B2B.Payment.Application.Interfaces;

public interface IPaymentRepository : IRepository<PaymentEntity, Guid>
{
    Task<PaymentEntity?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
}

public interface IReadPaymentRepository : IReadRepository<PaymentEntity, Guid>
{
    Task<PagedList<PaymentEntity>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentEntity>> GetByCustomerAsync(Guid customerId, Guid tenantId, CancellationToken ct = default);
}

public interface IInvoiceRepository : IRepository<B2B.Payment.Domain.Entities.Invoice, Guid>
{
    Task<B2B.Payment.Domain.Entities.Invoice?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<string> GenerateInvoiceNumberAsync(CancellationToken ct = default);
}

public interface IReadInvoiceRepository : IReadRepository<B2B.Payment.Domain.Entities.Invoice, Guid>
{
    Task<PagedList<B2B.Payment.Domain.Entities.Invoice>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<B2B.Payment.Domain.Entities.Invoice>> GetOverdueAsync(Guid tenantId, CancellationToken ct = default);
}
