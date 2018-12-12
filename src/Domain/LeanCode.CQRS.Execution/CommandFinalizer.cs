using System;
using System.Threading.Tasks;
using LeanCode.Pipelines;

namespace LeanCode.CQRS.Execution
{
    public sealed class CommandFinalizer<TAppContext>
        : IPipelineFinalizer<TAppContext, CommandExecutionPayload, CommandResult>
        where TAppContext : IPipelineContext
    {
        private readonly Serilog.ILogger logger = Serilog.Log.ForContext<CommandFinalizer<TAppContext>>();

        private readonly ICommandHandlerResolver<TAppContext> resolver;

        public CommandFinalizer(ICommandHandlerResolver<TAppContext> resolver)
        {
            this.resolver = resolver;
        }

        public async Task<CommandResult> ExecuteAsync(
            TAppContext _, CommandExecutionPayload payload)
        {
            var context = payload.Context;
            var command = payload.Object;

            var commandType = command.GetType();
            var handler = resolver.FindCommandHandler(commandType);
            if (handler is null)
            {
                logger.Fatal(
                    "Cannot find a handler for the command {@Command}",
                    command);
                throw new CommandHandlerNotFoundException(commandType);
            }

            try
            {
                await handler.ExecuteAsync(context, command).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.Error(
                    ex, "Cannot execute command {@Command} because of internal error",
                    command);
                throw;
            }
            logger.Information(
                "Command {@Command} executed successfully", command);
            return CommandResult.Success();
        }
    }
}
