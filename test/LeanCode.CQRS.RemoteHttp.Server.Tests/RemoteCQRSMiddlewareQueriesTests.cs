using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LeanCode.CQRS.RemoteHttp.Server.Tests
{
    public class RemoteCQRSMiddlewareQueriesTests : BaseMiddlewareTests
    {
        public RemoteCQRSMiddlewareQueriesTests()
            : base("query", typeof(SampleRemoteQuery))
        { }

        [Fact]
        public async Task Writes_NotFound_if_query_cannot_be_found()
        {
            var (status, _) = await Invoke("non.Existing.Query");
            Assert.Equal(StatusCodes.Status404NotFound, status);
        }

        [Fact]
        public async Task Writes_BadRequest_if_body_is_malformed()
        {
            var (status, _) = await Invoke(content: "malformed body 123");
            Assert.Equal(StatusCodes.Status400BadRequest, status);
        }

        [Fact]
        public async Task Writes_NotFound_if_trying_to_execute_non_remote_query()
        {
            var (status, _) = await Invoke(typeof(SampleQuery).FullName);
            Assert.Equal(StatusCodes.Status404NotFound, status);
        }

        [Fact]
        public async Task Does_not_call_QueryExecutor_when_executing_non_remote_query()
        {
            var (status, _) = await Invoke(typeof(SampleQuery).FullName);
            Assert.Null(query.LastQuery);
        }

        [Fact]
        public async Task Deserializes_correct_query_object()
        {
            await Invoke(content: "{'Prop': 12}");

            var q = Assert.IsType<SampleRemoteQuery>(query.LastQuery);
            Assert.Equal(12, q.Prop);
        }

        [Fact]
        public async Task Writes_OK_when_query_has_been_executed()
        {
            var (status, _) = await Invoke();
            Assert.Equal(StatusCodes.Status200OK, status);
        }

        [Fact]
        public async Task Writes_result_when_query_has_been_executed()
        {
            var (_, content) = await Invoke();
            Assert.Equal("0", content);
        }
    }

    public class SampleQuery : IQuery<int>
    { }

    public class SampleRemoteQuery : IRemoteQuery<int>
    {
        public int Prop { get; set; }
    }
}
