using LeanCode.CodeAnalysis.Tests.TestSamples;
using LeanCode.CQRS.Execution;
using Microsoft.AspNetCore.Http;

namespace LeanCode.CodeAnalysis.Tests.Data;

public class FirstOperationOH : IOperationHandler<FirstOperation, bool>
{
    public Task<bool> ExecuteAsync(HttpContext context, FirstOperation operation) =>
        throw new NotImplementedException();
}

public class MultipleOperationsOH : IOperationHandler<FirstOperation, bool>, IOperationHandler<SecondOperation, bool>
{
    public Task<bool> ExecuteAsync(HttpContext context, FirstOperation operation) =>
        throw new NotImplementedException();

    public Task<bool> ExecuteAsync(HttpContext context, SecondOperation operation) =>
        throw new NotImplementedException();
}
