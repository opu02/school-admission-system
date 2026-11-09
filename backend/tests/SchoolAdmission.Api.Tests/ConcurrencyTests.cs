using System.Net;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// The core business rule: held + confirmed must NEVER exceed totalSeats,
/// even when many parents press "Apply" at the same moment.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ConcurrencyTests(ApiFactory factory)
{
    private readonly ApiClient _api = new(factory.CreateClient());

    [Fact]
    public async Task Many_parallel_applications_never_overbook_a_class()
    {
        const int seats = 10;
        const int applicants = 60;
        var schoolClass = await _api.CreateClassAsync(seats);

        // Release every request at the same instant.
        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, applicants)
            .Select(_ => ApiClient.NewStudentId())
            .Select(studentId => Task.Run(async () =>
            {
                await startGate.Task;
                return await _api.HoldAsync(schoolClass.Id, studentId);
            }))
            .ToList();
        startGate.SetResult();
        var responses = await Task.WhenAll(tasks);

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rejected = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();

        Assert.Equal(seats, created);
        Assert.Equal(applicants - seats, rejected.Count);
        foreach (var response in rejected)
        {
            await ApiClient.AssertProblemAsync(response, HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE");
        }

        var after = await _api.GetClassAsync(schoolClass.Id);
        Assert.Equal(seats, after.HeldSeats);
        Assert.Equal(0, after.AvailableSeats);
        Assert.True(after.HeldSeats + after.ConfirmedSeats <= after.TotalSeats);
    }

    [Fact]
    public async Task Same_student_applying_many_times_in_parallel_gets_only_one_seat()
    {
        var schoolClass = await _api.CreateClassAsync(totalSeats: 5);
        var studentId = ApiClient.NewStudentId();

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 15)
            .Select(_ => Task.Run(async () =>
            {
                await startGate.Task;
                return await _api.HoldAsync(schoolClass.Id, studentId);
            }))
            .ToList();
        startGate.SetResult();
        var responses = await Task.WhenAll(tasks);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        foreach (var response in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            await ApiClient.AssertProblemAsync(response, HttpStatusCode.Conflict, "ACTIVE_HOLD_EXISTS");
        }

        Assert.Equal(1, (await _api.GetClassAsync(schoolClass.Id)).HeldSeats);
    }

    [Fact]
    public async Task Parallel_confirms_of_the_same_hold_count_the_seat_only_once()
    {
        var schoolClass = await _api.CreateClassAsync(totalSeats: 3);
        var studentId = ApiClient.NewStudentId();
        var hold = await _api.CreateHoldAsync(schoolClass.Id, studentId);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => _api.ConfirmAsync(hold.HoldId, studentId)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var after = await _api.GetClassAsync(schoolClass.Id);
        Assert.Equal(1, after.ConfirmedSeats);
        Assert.Equal(0, after.HeldSeats);
        Assert.Equal(2, after.AvailableSeats);
    }
}
