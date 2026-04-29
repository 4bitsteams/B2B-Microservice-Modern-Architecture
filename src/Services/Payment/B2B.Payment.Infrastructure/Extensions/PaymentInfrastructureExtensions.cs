using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using B2B.Payment.Application.Interfaces;
using B2B.Payment.Infrastructure.Persistence;
using B2B.Payment.Infrastructure.Persistence.Repositories;
using B2B.Shared.Infrastructure.Extensions;

namespace B2B.Payment.Infrastructure.Extensions;

public static class PaymentInfrastructureExtensions
{
    public static IServiceCollection AddPaymentInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddPostgresWithReadReplica<PaymentDbContext>(config);

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddSingleton<IReadPaymentRepository, PaymentReadRepository>();
        services.AddSingleton<IReadInvoiceRepository, InvoiceReadRepository>();
        services.AddScoped<B2B.Shared.Core.Interfaces.IUnitOfWork>(sp => sp.GetRequiredService<PaymentDbContext>());

        return services;
    }
}
