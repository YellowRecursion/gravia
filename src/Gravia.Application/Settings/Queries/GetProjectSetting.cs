using FluentValidation;
using Gravia.Application.Projects.Services;
using Gravia.Application.Settings.DTOs;
using MediatR;

namespace Gravia.Application.Settings.Queries;

public sealed record GetProjectSettingQuery(string Id) : IRequest<SettingDto?>;

public sealed class GetProjectSettingQueryValidator : AbstractValidator<GetProjectSettingQuery>
{
    public GetProjectSettingQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}

public sealed class GetProjectSettingQueryHandler
    : IRequestHandler<GetProjectSettingQuery, SettingDto?>
{
    public Task<SettingDto?> Handle(GetProjectSettingQuery request, CancellationToken cancellationToken)
    {
        var setting = ProjectSession.Current?.Settings.Find(request.Id);
        return Task.FromResult(setting?.ToDto());
    }
}
