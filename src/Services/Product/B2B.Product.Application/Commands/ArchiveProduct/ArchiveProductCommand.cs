using B2B.Shared.Core.CQRS;

namespace B2B.Product.Application.Commands.ArchiveProduct;

public sealed record ArchiveProductCommand(Guid ProductId) : ICommand;
