using ReadyToGoTravel.Booking.Providers;

namespace ReadyToGoTravel.Booking.SupplierIntegrations.LiteApi;

public sealed class LiteApiFixtureSettlementProvider : ISupplierSettlementProvider
{
    public Task<SupplierSettlementInstruction> CreateInstructionAsync(SupplierSettlementPlan plan,
        CancellationToken cancellationToken = default) => Task.FromResult(new SupplierSettlementInstruction(
        $"settlement_{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{plan.ProviderBinding}|{plan.PaymentReference}")))[..24]}",
        plan.ProviderBinding, plan.Amount, plan.Currency));
}
