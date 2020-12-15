using System;
using System.Threading;
using System.Threading.Tasks;
using FirebaseAdmin;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Grpc.Auth;
using Microsoft.Extensions.Hosting;

namespace LeanCode.Firebase.Firestore
{
    public class FirestoreDatabase : IHostedService
    {
        private readonly FirebaseApp firebaseApp;
        private FirestoreDb? database;

        public FirestoreDb Database => database ?? throw new InvalidOperationException("The database needs to be initialized first.");

        public FirestoreDatabase(FirebaseApp firebaseApp)
        {
            this.firebaseApp = firebaseApp;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var credentials = firebaseApp.Options.Credential;

            var builder = new FirestoreClientBuilder
            {
                ChannelCredentials = credentials.ToChannelCredentials(),
            };
            var client = await builder.BuildAsync(cancellationToken);
            database = FirestoreDb.Create(firebaseApp.Options.ProjectId, client);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
