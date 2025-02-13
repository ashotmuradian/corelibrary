using LeanCode.CQRS.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace LeanCode.CQRS.AspNetCore.Registration;

public static class ServiceCollectionRegistrationExtensions
{
    public static IServiceCollection AddCQRSHandlers(
        this IServiceCollection serviceCollection,
        IEnumerable<CQRSObjectMetadata> cqrsObjects
    )
    {
        foreach (var obj in cqrsObjects)
        {
            serviceCollection.AddCQRSHandler(obj);
        }

        return serviceCollection;
    }

    public static void AddCQRSHandler(this IServiceCollection serviceCollection, CQRSObjectMetadata obj)
    {
        serviceCollection.Add(
            new ServiceDescriptor(MakeHandlerInterfaceType(obj), obj.HandlerType, ServiceLifetime.Scoped)
        );

        Type MakeHandlerInterfaceType(CQRSObjectMetadata obj)
        {
            return obj.ObjectKind switch
            {
                CQRSObjectKind.Command => typeof(ICommandHandler<>).MakeGenericType(obj.ObjectType),
                CQRSObjectKind.Query => typeof(IQueryHandler<,>).MakeGenericType(obj.ObjectType, obj.ResultType),
                CQRSObjectKind.Operation
                    => typeof(IOperationHandler<,>).MakeGenericType(obj.ObjectType, obj.ResultType),
                _ => throw new InvalidOperationException("Unexpected object kind"),
            };
        }
    }
}
