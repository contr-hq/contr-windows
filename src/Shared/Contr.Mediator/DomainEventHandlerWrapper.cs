using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Contr.Mediator;

internal abstract class DomainEventHandlerWrapper
{
    public abstract Task Handle(
        IDomainEvent domainEvent, IServiceProvider services, CancellationToken cancellationToken);
}

internal sealed class DomainEventHandlerWrapperImpl<TDomainEvent> : DomainEventHandlerWrapper
    where TDomainEvent : IDomainEvent
{
    public override async Task Handle(
        IDomainEvent domainEvent, IServiceProvider services, CancellationToken cancellationToken)
    {
        var handlers = services.GetServices<IDomainEventHandler<TDomainEvent>>();
        foreach (var handler in handlers)
        {
            await handler.Handle((TDomainEvent)domainEvent, cancellationToken);
        }
    }
}
