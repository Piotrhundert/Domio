namespace Domio.Application.Diagnostics;

public interface IDatabaseDiagnosticsService
{
    Task<DatabaseDiagnosticsResult> GetAsync(
        CancellationToken cancellationToken = default);
}
