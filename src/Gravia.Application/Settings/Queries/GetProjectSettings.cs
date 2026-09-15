using Gravia.Application.Projects.Services;
using Gravia.Application.Settings.DTOs;
using MediatR;

namespace Gravia.Application.Settings.Queries;

public sealed record GetProjectSettingsQuery : IRequest<SettingSummaryDto[]>;

public sealed class GetProjectSettingsQueryHandler
    : IRequestHandler<GetProjectSettingsQuery, SettingSummaryDto[]>
{
    public Task<SettingSummaryDto[]> Handle(GetProjectSettingsQuery request, CancellationToken cancellationToken)
    {
        var result = ProjectSession.Current?.Settings.Settings
            .OrderBy(setting => setting.Id, StringComparer.Ordinal)
            .Select(setting => setting.ToSummaryDto())
            .ToArray();
        return Task.FromResult(result ?? []);
    }
}
