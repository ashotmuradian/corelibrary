using LeanCode.Components;
using LeanCode.Contracts.Security;
using LeanCode.CQRS.AspNetCore.Registration;
using LeanCode.CQRS.AspNetCore.Serialization;
using LeanCode.CQRS.Security;
using LeanCode.CQRS.Validation;
using LeanCode.ClientsUpdates.Contracts;
using LeanCode.ClientsUpdates.Services;
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

        var objectsSource = new CQRSObjectsRegistrationSource(contractsCatalog, handlersCatalog);

        serviceCollection.AddSingleton(objectsSource);
        serviceCollection.AddCQRSHandlers(objectsSource.Objects);

        serviceCollection.AddSingleton<RoleRegistry>();
        serviceCollection.AddScoped<IHasPermissions, DefaultPermissionAuthorizer>();
        serviceCollection.AddScoped<ICommandValidatorResolver, CommandValidatorResolver>();

        return new CQRSServicesBuilder(serviceCollection);
    }

    public static CQRSServicesBuilder AddClientsUpdates(this IServiceCollection serviceCollection)
    {
        var sp = serviceCollection.BuildServiceProvider();

        var cqrsHandlers =
            sp.GetService<CQRSObjectsRegistrationSource>()
            ?? throw new InvalidOperationException(
                "CQRS services were not registered, make sure you've called IServiceCollection.AddCQRS(...) first."
            );

        var addtionalCQRSObjects = cqrsHandlers.AddAdditionalCQRSObjects(
            TypesCatalog.Of<VersionSupport>(),
            TypesCatalog.Of<VersionSupport>()
        );

        serviceCollection.AddCQRSHandlers(addtionalCQRSObjects);

        serviceCollection.AddTransient<VersionHandler>();

        return new CQRSServicesBuilder(serviceCollection);
    }
}

public class CQRSServicesBuilder
{
    private readonly IServiceCollection services;

    public CQRSServicesBuilder(IServiceCollection services)
    {
        this.services = services;
    }

    public CQRSServicesBuilder WithSerializer(ISerializer serializer)
    {
        services.Replace(new ServiceDescriptor(typeof(ISerializer), serializer));
        return this;
    }
}
