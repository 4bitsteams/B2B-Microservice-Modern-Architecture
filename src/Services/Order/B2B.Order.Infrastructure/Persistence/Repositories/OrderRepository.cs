using B2B.Order.Application.Interfaces;
using B2B.Shared.Infrastructure.Persistence;
using OrderEntity = B2B.Order.Domain.Entities.Order;

namespace B2B.Order.Infrastructure.Persistence.Repositories;

/// <summary>
/// Write repository — uses the scoped OrderDbContext (primary connection, tracking ON).
/// Command handlers use this to persist new orders and state transitions.
/// </summary>
public sealed class OrderRepository(OrderDbContext context)
    : BaseRepository<OrderEntity, Guid, OrderDbContext>(context), IOrderRepository;
