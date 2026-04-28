using B2B.Shared.Core.CQRS;

namespace B2B.Product.Application.Commands.AdjustStock;

public sealed record AdjustStockCommand(
    Guid ProductId,
    int Quantity,
    string Reason) : ICommand;
