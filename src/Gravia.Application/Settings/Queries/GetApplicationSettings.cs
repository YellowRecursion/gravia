using Gravia.Application.Settings.DTOs;
using Gravia.Application.Settings.Services;
using MediatR;

namespace Gravia.Application.Settings.Queries;

public sealed record GetApplicationSettingsQuery : IRequest<SettingSummaryDto[]>;

public sealed class GetApplicationSettingsQueryHandler(ApplicationSettings settings)
    : IRequestHandler<GetApplicationSettingsQuery, SettingSummaryDto[]>
{
    public Task<SettingSummaryDto[]> Handle(
        GetApplicationSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var result = settings.Settings
            .OrderBy(setting => setting.Id, StringComparer.Ordinal)
            .Select(setting => setting.ToSummaryDto())
            .ToArray();
        return Task.FromResult(result);
    }
}
