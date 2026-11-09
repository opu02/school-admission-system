using System.Net;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// Bonus requirement: two separate API instances (like two backend processes on
/// different ports) share one database and must still never overbook.
/// Each instance has its own DI container, DbContext pool and connection pool, so any
/// in-memory lock would NOT protect us here - only the database row lock does.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MultiInstanceTests(ApiFactory factory)
{
    [Fact]
    public async Task Two_api_instances_sharing_one_database_never_overbook()
    {
        // A second, completely separate app instance (own host, DI container and connection
        // pool) with the same database and the same test clock.
        await using var secondInstance = factory.WithWebHostBuilder(_ => { });
        var apiA = new ApiClient(factory.CreateClient());
        var apiB = new ApiClient(secondInstance.CreateClient());

        const int seats = 7;
        var schoolClass = await apiA.CreateClassAsync(seats);

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 40)
            .Select(i => Task.Run(async () =>
            {
                await startGate.Task;
                var api = i % 2 == 0 ? apiA : apiB; // alternate between the two instances
                return await api.HoldAsync(schoolClass.Id, ApiClient.NewStudentId());
            }))
            .ToList();
        startGate.SetResult();
        var responses = await Task.WhenAll(tasks);

        Assert.Equal(seats, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(40 - seats, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        // Both instances report the same, correct numbers.
        var viaA = await apiA.GetClassAsync(schoolClass.Id);
        var viaB = await apiB.GetClassAsync(schoolClass.Id);
        Assert.Equal(seats, viaA.HeldSeats);
        Assert.Equal(viaA, viaB);
    }
}
