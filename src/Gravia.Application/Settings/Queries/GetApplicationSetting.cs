using FluentValidation;
using Gravia.Application.Settings.DTOs;
using Gravia.Application.Settings.Services;
using MediatR;

namespace Gravia.Application.Settings.Queries;

public sealed record GetApplicationSettingQuery(string Id) : IRequest<SettingDto?>;

public sealed class GetApplicationSettingQueryValidator : AbstractValidator<GetApplicationSettingQuery>
{
    public GetApplicationSettingQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}

public sealed class GetApplicationSettingQueryHandler(ApplicationSettings settings)
    : IRequestHandler<GetApplicationSettingQuery, SettingDto?>
{
    public Task<SettingDto?> Handle(GetApplicationSettingQuery request, CancellationToken cancellationToken)
    {
        var setting = settings.Find(request.Id);
        var result = setting?.ToDto();
        return Task.FromResult(result);
    }
}
