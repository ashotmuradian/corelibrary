using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LeanCode.CQRS.MassTransitRelay;
using LeanCode.OpenTelemetry;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Serilog;
using Xunit;

namespace LeanCode.AuditLogs.Tests;

public sealed class AuditLogsIntegrationTests : IAsyncLifetime, IDisposable
{
    private const string SomeId = "some_id";
    private const string ActorId = "actor_id";
    private const string TestPath = "/test";
    private const string AuthorizedTestPath = "/authorized-test";
    private static readonly JsonSerializerOptions Options =
        new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = false,
        };

    private readonly IHost host;
    private readonly ITestHarness harness;
    private readonly TestServer server;
    private static readonly TestEntity TestEntity = new() { Id = SomeId };

    public AuditLogsIntegrationTests()
    {
        host = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(cfg =>
                    {
                        cfg.AddOpenTelemetry()
                            .WithTracing(builder =>
                            {
                                builder.AddAspNetCoreInstrumentation();
                            });
                        cfg.AddDbContext<TestDbContext>();
                        cfg.AddTransient<IAuditLogStorage, StubAuditLogStorage>();
                        cfg.AddTransient<AuditLogsPublisher>();
                        cfg.AddMassTransitTestHarness(ConfigureMassTransit);
                        cfg.AddRouting();
                    })
                    .Configure(app =>
                    {
                        app.Audit<TestDbContext>()
                            .UseRouting()
                            .UseEndpoints(e =>
                            {
                                e.MapPost(
                                    TestPath,
                                    (ctx) =>
                                    {
                                        var dbContext = ctx.RequestServices.GetService<TestDbContext>()!;
                                        dbContext.Add(TestEntity);
                                        return Task.CompletedTask;
                                    }
                                );
                                e.MapPost(
                                    AuthorizedTestPath,
                                    (ctx) =>
                                    {
                                        Activity
                                            .Current!
                                            .AddBaggage(IdentityTraceBaggageHelpers.CurrentUserIdKey, ActorId);
                                        var dbContext = ctx.RequestServices.GetService<TestDbContext>()!;
                                        dbContext.Add(TestEntity);
                                        return Task.CompletedTask;
                                    }
                                );
                            });
                        app.Run(ctx =>
                        {
                            return Task.CompletedTask;
                        });
                    });
            })
            .Build();

        server = host.GetTestServer();

        harness = host.Services.GetRequiredService<ITestHarness>();
    }

    private static void ConfigureMassTransit(IBusRegistrationConfigurator cfg)
    {
        cfg.AddConsumersWithDefaultConfiguration(
            new[] { typeof(AuditLogsConsumer).Assembly },
            typeof(TestConsumerDefinition<>)
        );

        cfg.UsingInMemory(
            (ctx, busCfg) =>
            {
                busCfg.ConfigureEndpoints(ctx, new DefaultEndpointNameFormatter("InMemory"));
                busCfg.ConnectBusObservers(ctx);
            }
        );
    }

    [Fact]
    public async Task Ensure_that_audit_log_is_collected_correctly()
    {
        await server.SendAsync(ctx =>
        {
            ctx.Request.Method = "POST";
            ctx.Request.Path = TestPath;
        });

        var messages = new List<IPublishedMessage<AuditLogMessage>>(1);
        await foreach (var m in harness.Published.SelectAsync<AuditLogMessage>())
        {
            messages.Add(m);
        }
        messages
            .Should()
            .ContainSingle()
            .Which
            .Context
            .Message
            .Should()
            .BeEquivalentTo(
                new
                {
                    EntityChanged = new
                    {
                        Ids = new[] { SomeId },
                        Type = typeof(TestEntity).FullName,
                        EntityState = "Added",
                        Changes = JsonSerializer.SerializeToDocument(TestEntity, Options),
                    },
                    ActionName = TestPath,
                },
                opt => opt.ComparingByMembers<JsonElement>()
            )
            .And
            .Subject
            .Should()
            .Match(s => s.As<AuditLogMessage>().SpanId != null && s.As<AuditLogMessage>().TraceId != null);
    }

    [Fact]
    public async Task Ensure_that_audit_log_is_collected_with_actor_id()
    {
        await server.SendAsync(ctx =>
        {
            ctx.Request.Method = "POST";
            ctx.Request.Path = AuthorizedTestPath;
        });

        var messages = new List<IPublishedMessage<AuditLogMessage>>(1);
        await foreach (var m in harness.Published.SelectAsync<AuditLogMessage>())
        {
            messages.Add(m);
        }
        messages
            .Should()
            .ContainSingle()
            .Which
            .Context
            .Message
            .Should()
            .BeEquivalentTo(new { ActorId, }, opt => opt.ComparingByMembers<JsonElement>())
            .And
            .Subject
            .Should()
            .Match(s => s.As<AuditLogMessage>().SpanId != null && s.As<AuditLogMessage>().TraceId != null);
    }

    public async Task InitializeAsync()
    {
        await host.StartAsync();
        await harness.Start();
    }

    public Task DisposeAsync() => host.StopAsync();

    public void Dispose()
    {
        server.Dispose();
        host.Dispose();
    }

    internal sealed class TestConsumerDefinition<TConsumer> : ConsumerDefinition<TConsumer>
        where TConsumer : class, IConsumer
    {
        protected override void ConfigureConsumer(
            IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<TConsumer> consumerConfigurator,
            IRegistrationContext context
        )
        {
            endpointConfigurator.UseAuditLogs<TestDbContext>(context);
        }
    }

    internal sealed class StubAuditLogStorage : IAuditLogStorage
    {
        private readonly ILogger logger = Log.ForContext<StubAuditLogStorage>();

        public Task StoreEventAsync(AuditLogMessage auditLogMessage, CancellationToken cancellationToken)
        {
            logger.Information(
                "StubAuditLog: Changes found {UserId} {ActionName} {Type} {State} {@PrimaryKey} {@EntryChanged} {DateOccurred}",
                auditLogMessage.ActorId,
                auditLogMessage.ActionName,
                auditLogMessage.EntityChanged.Type,
                auditLogMessage.EntityChanged.EntityState,
                auditLogMessage.EntityChanged.Ids.Select(id => id.ToString()).ToList(),
                auditLogMessage.EntityChanged.Changes,
                auditLogMessage.DateOccurred
            );

            return Task.CompletedTask;
        }
    }
}
