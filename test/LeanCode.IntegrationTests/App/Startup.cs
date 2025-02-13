using LeanCode.Components;
using LeanCode.CQRS.AspNetCore;
using LeanCode.CQRS.MassTransitRelay;
using LeanCode.CQRS.MassTransitRelay.LockProviders;
using LeanCode.CQRS.Security;
using LeanCode.CQRS.Validation.Fluent;
using LeanCode.IntegrationTestHelpers;
using LeanCode.Startup.MicrosoftDI;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeanCode.IntegrationTests.App;

public class Startup : LeanStartup
{
    private static readonly TypesCatalog CQRSTypes = TypesCatalog.Of<Startup>();
    private readonly TestDatabaseConfig testDatabaseConfig;

    protected override bool CloseAndFlushLogger { get; }

    public Startup(IConfiguration config)
        : base(config)
    {
        testDatabaseConfig = TestDatabaseConfig.Create();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHostedService<DbContextInitializer<TestDbContext>>();
        services.AddDbContext<TestDbContext>(cfg => testDatabaseConfig.ConfigureDbContext(cfg, Configuration));
        services.AddSingleton<IRoleRegistration, AppRoles>();

        services.AddRouting();
        services.AddCQRS(CQRSTypes, CQRSTypes);
        services.AddFluentValidation(CQRSTypes);
        services.AddCQRSMassTransitIntegration(busCfg =>
        {
            busCfg.AddConsumer<EntityAddedConsumer, EntityAddedConsumerDefinition>();
            busCfg.AddEntityFrameworkOutbox<TestDbContext>(outboxCfg =>
            {
                testDatabaseConfig.ConfigureMassTransitOutbox(outboxCfg);
                outboxCfg.UseBusOutbox();
            });

            busCfg.UsingInMemory(
                (ctx, cfg) =>
                {
                    cfg.ConfigureEndpoints(ctx);
                }
            );
        });

        services.AddAuthentication(TestAuthenticationHandler.SchemeName).AddTestAuthenticationHandler();
    }

    protected override void ConfigureApp(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseEndpoints(
            e =>
                e.MapRemoteCQRS(
                    "/api",
                    cfg =>
                    {
                        cfg.Commands = cmd =>
                            cmd.Secure().Validate().CommitTransaction<TestDbContext>().PublishEvents();
                        cfg.Queries = cmd => cmd.Secure();
                        cfg.Operations = cmd => cmd.Secure();
                    }
                )
        );
    }
}
