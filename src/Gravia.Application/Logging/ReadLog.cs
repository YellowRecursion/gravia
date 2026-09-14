using FluentValidation;
using MediatR;

namespace Gravia.Application.Logging;

public sealed record ReadLogQuery(Guid? JournalId = null, long AfterSequence = 0) : IRequest<LogBatch>;

public sealed class ReadLogQueryValidator : AbstractValidator<ReadLogQuery>
{
    public ReadLogQueryValidator() => RuleFor(x => x.AfterSequence).GreaterThanOrEqualTo(0);
}

public sealed class ReadLogQueryHandler(ILogJournal journal) : IRequestHandler<ReadLogQuery, LogBatch>
{
    public Task<LogBatch> Handle(ReadLogQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(journal.Read(request.JournalId, request.AfterSequence));
    }
}
