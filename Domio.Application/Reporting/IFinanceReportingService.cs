namespace Domio.Application.Reporting;

public interface IFinanceReportingService
{
    Task<FinanceReportDashboard> GetDashboardAsync(
        Guid actorUserId,
        FinanceReportQuery query,
        CancellationToken cancellationToken = default);

    Task<FinanceSimulationResult> SimulateAsync(
        Guid actorUserId,
        FinanceSimulationRequest request,
        CancellationToken cancellationToken = default);
}
