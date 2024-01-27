using LeanCode.Components;
using LeanCode.Contracts;
using LeanCode.Contracts.Security;
using LeanCode.CQRS.AspNetCore.Registration;
using LeanCode.CQRS.AspNetCore.Serialization;
using LeanCode.CQRS.Execution;
using LeanCode.CQRS.Security;
using LeanCode.CQRS.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeanCode.CQRS.AspNetCore;

public static class ServiceCollectionCQRSExtensions
{
    public static CQRSServicesBuilder AddCQRS(
        this IServiceCollection serviceCollection,
        TypesCatalog contractsCatalog,
        TypesCatalog handlersCatalog
    )
    {
        serviceCollection.AddSingleton<ISerializer>(_ => new Utf8JsonSerializer(Utf8JsonSerializer.DefaultOptions));

        var objectsSource = new CQRSObjectsRegistrationSource(serviceCollection);
        objectsSource.AddCQRSObjects(contractsCatalog, handlersCatalog);

        serviceCollection.AddSingleton<CQRSMetrics>();
        serviceCollection.AddSingleton(objectsSource);

        serviceCollection.AddSingleton<RoleRegistry>();
        serviceCollection.AddScoped<IHasPermissions, DefaultPermissionAuthorizer>();
        serviceCollection.AddScoped<ICommandValidatorResolver, CommandValidatorResolver>();

        return new CQRSServicesBuilder(serviceCollection, objectsSource);
    }
}

public class CQRSServicesBuilder
{
    public IServiceCollection Services { get; }
    private readonly CQRSObjectsRegistrationSource objectsSource;

    internal CQRSServicesBuilder(IServiceCollection services, CQRSObjectsRegistrationSource objectsSource)
    {
        this.Services = services;
        this.objectsSource = objectsSource;
    }

    public CQRSServicesBuilder WithSerializer(ISerializer serializer)
    {
        Services.Replace(new ServiceDescriptor(typeof(ISerializer), serializer));
        return this;
    }

    public CQRSServicesBuilder AddCQRSObjects(TypesCatalog contractsCatalog, TypesCatalog handlersCatalog)
    {
        objectsSource.AddCQRSObjects(contractsCatalog, handlersCatalog);
        return this;
    }

    public CQRSServicesBuilder AddQuery<TQuery, TResult, THandler>()
        where TQuery : IQuery<TResult>
        where THandler : IQueryHandler<TQuery, TResult>
    {
        objectsSource.AddCQRSObject(new(CQRSObjectKind.Query, typeof(TQuery), typeof(TResult), typeof(THandler)));
        return this;
    }

    public CQRSServicesBuilder AddCommand<TCommand, THandler>()
        where TCommand : ICommand
        where THandler : ICommandHandler<TCommand>
    {
        objectsSource.AddCQRSObject(
            new(CQRSObjectKind.Command, typeof(TCommand), typeof(CommandResult), typeof(THandler))
        );
        return this;
    }

    public CQRSServicesBuilder AddOperation<TOperation, TResult, THandler>()
        where TOperation : IOperation<TResult>
        where THandler : IOperationHandler<TOperation, TResult>
    {
        objectsSource.AddCQRSObject(
            new(CQRSObjectKind.Operation, typeof(TOperation), typeof(TResult), typeof(THandler))
        );
        return this;
    }

    public CQRSServicesBuilder WithLocalCommands(Action<ICQRSApplicationBuilder> configure)
    {
        Services.AddSingleton<Local.ILocalCommandExecutor>(
            s => new Local.MiddlewareBasedLocalCommandExecutor(s, configure)
        );
        return this;
    }
}
