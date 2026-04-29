using B2B.Discount.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using MediatR;

namespace B2B.Discount.Application.Queries.GetDiscounts;

public sealed class GetDiscountsHandler(
    IReadDiscountRepository discountRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetDiscountsQuery, Result<PagedList<DiscountDto>>>
{
    public async Task<Result<PagedList<DiscountDto>>> Handle(GetDiscountsQuery request, CancellationToken cancellationToken)
    {
        var paged = await discountRepository.GetPagedByTenantAsync(
            currentUser.TenantId, request.Page, request.PageSize, cancellationToken);

        var dtos = paged.Items.Select(d => new DiscountDto(
            d.Id, d.Name, d.Type.ToString(), d.Value, d.IsActive, d.IsAvailable,
            d.StartDate, d.EndDate, d.UsageCount, d.MaxUsageCount)).ToList();

        return PagedList<DiscountDto>.Create(dtos, request.Page, request.PageSize, paged.TotalCount);
    }
}
