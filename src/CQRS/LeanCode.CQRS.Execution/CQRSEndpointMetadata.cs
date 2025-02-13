using Microsoft.AspNetCore.Http;

namespace LeanCode.CQRS.Execution;

public delegate Task<object?> ObjectExecutor(HttpContext httpContext, CQRSRequestPayload payload);

public class CQRSEndpointMetadata
{
    public CQRSObjectMetadata ObjectMetadata { get; }
    public ObjectExecutor ObjectExecutor { get; }

    public CQRSEndpointMetadata(CQRSObjectMetadata objectMetadata, ObjectExecutor objectExecutor)
    {
        ObjectMetadata = objectMetadata;
        ObjectExecutor = objectExecutor;
    }
}
