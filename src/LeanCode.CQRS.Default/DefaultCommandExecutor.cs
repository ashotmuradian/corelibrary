﻿using System;
using LeanCode.CQRS.Exceptions;
using LeanCode.CQRS.Security;
using LeanCode.CQRS.Validation;

namespace LeanCode.CQRS.Default
{
    public class DefaultCommandExecutor : ICommandExecutor
    {
        private readonly Serilog.ILogger logger = Serilog.Log.ForContext<DefaultCommandExecutor>();

        private readonly ICommandHandlerResolver commandHandlerResolver;
        private readonly IAuthorizationChecker authorizationChecker;
        private readonly ICommandValidatorResolver commandValidatorResolver;

        public DefaultCommandExecutor(ICommandHandlerResolver commandHandlerResolver,
            IAuthorizationChecker authorizationChecker,
            ICommandValidatorResolver commandValidatorResolver)
        {
            this.commandHandlerResolver = commandHandlerResolver;
            this.authorizationChecker = authorizationChecker;
            this.commandValidatorResolver = commandValidatorResolver;
        }

        public CommandResult Execute<TCommand>(TCommand command)
            where TCommand : ICommand
        {
            logger.Verbose("Executing command {@Command}", command);

            AuthorizeCommand(command);
            var failure = ValidateCommand(command);
            if (failure != null)
            {
                return failure;
            }
            RunCommand(command);
            logger.Information("Command {@Command} executed successfully", command);
            return CommandResult.Success();
        }

        private void AuthorizeCommand<TCommand>(TCommand command)
            where TCommand : ICommand
        {
            if (!authorizationChecker.CheckIfAuthorized(command))
            {
                logger.Warning("Command {@Command} not authorized", command);
                throw new InsufficientPermissionException($"User not authorized for {command.GetType()}");
            }
        }

        private CommandResult ValidateCommand<TCommand>(TCommand command)
            where TCommand : ICommand
        {
            var commandValidator = commandValidatorResolver.GetValidator<TCommand>();
            if (commandValidator != null)
            {
                var result = commandValidator.Validate(command);
                if (!result.IsValid)
                {
                    logger.Information("Command {@Command} is not valid", command);
                    return CommandResult.NotValid(result);
                }
            }
            return null;
        }

        private void RunCommand<TCommand>(TCommand command)
            where TCommand : ICommand
        {
            var handler = commandHandlerResolver.FindCommandHandler<TCommand>();
            if (handler == null)
            {
                logger.Fatal("Cannot find a handler for the command {@Command}", command);
                throw new NotSupportedException($"Cannot find a handler for the command of type: {typeof(TCommand)}");
            }

            try
            {
                handler.Execute(command);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Cannot execute command {@Command} because of internal error", command);
                throw;
            }
        }
    }
}
