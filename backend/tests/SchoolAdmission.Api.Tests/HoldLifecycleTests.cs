using System.Net;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class HoldLifecycleTests(ApiFactory factory)
{
    private readonly ApiClient _api = new(factory.CreateClient());

    [Fact]
    public async Task Hold_then_confirm_follows_the_contract_and_is_idempotent()
    {
        var schoolClass = await _api.CreateClassAsync(totalSeats: 2);
        var studentId = ApiClient.NewStudentId();

        var hold = await _api.CreateHoldAsync(schoolClass.Id, studentId);
        Assert.StartsWith("h_", hold.HoldId);
        Assert.Equal(schoolClass.Id, hold.ClassId);
        Assert.Equal(studentId, hold.StudentId);
        Assert.Equal("HELD", hold.Status);
        var expectedExpiry = factory.Time.GetUtcNow().AddSeconds(ApiFactory.HoldDurationSeconds);
        Assert.True((expectedExpiry - hold.ExpiresAt).Duration() <= TimeSpan.FromSeconds(1),
            $"expiresAt {hold.ExpiresAt:o} should be ~{expectedExpiry:o}");

        var whileHeld = await _api.GetClassAsync(schoolClass.Id);
        Assert.Equal((1, 0, 1), (whileHeld.HeldSeats, whileHeld.ConfirmedSeats, whileHeld.AvailableSeats));

        var first = await _api.ConfirmAsync(hold.HoldId, studentId);
        var second = await _api.ConfirmAsync(hold.HoldId, studentId);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("CONFIRMED", (await ApiClient.ReadAsync<HoldDto>(second))!.Status);

        var afterConfirm = await _api.GetClassAsync(schoolClass.Id);
        Assert.Equal((0, 1, 1), (afterConfirm.HeldSeats, afterConfirm.ConfirmedSeats, afterConfirm.AvailableSeats));

        var mine = await ApiClient.ReadAsync<List<HoldDto>>(await _api.MyHoldsAsync(studentId));
        var listed = Assert.Single(mine!);
        Assert.Equal(schoolClass.Name, listed.ClassName);
        Assert.Equal("CONFIRMED", listed.Status);
    }

    [Fact]
    public async Task Unpaid_hold_expires_by_itself_and_the_seat_is_released()
    {
        var schoolClass = await _api.CreateClassAsync(totalSeats: 1);
        var studentId = ApiClient.NewStudentId();
        var hold = await _api.CreateHoldAsync(schoolClass.Id, studentId);

        // Class is full while the hold is active.
        await ApiClient.AssertProblemAsync(
            await _api.HoldAsync(schoolClass.Id, ApiClient.NewStudentId()), HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE");

        // Time passes, no API call in between.
        factory.Time.Advance(TimeSpan.FromSeconds(ApiFactory.HoldDurationSeconds + 1));

        var status = await ApiClient.ReadAsync<HoldDto>(await _api.GetHoldAsync(hold.HoldId));
        Assert.Equal("EXPIRED", status!.Status);

        var after = await _api.GetClassAsync(schoolClass.Id);
        Assert.Equal((0, 0, 1), (after.HeldSeats, after.ConfirmedSeats, after.AvailableSeats));

        // Paying too late is rejected; the seat is free for others.
        await ApiClient.AssertProblemAsync(
            await _api.ConfirmAsync(hold.HoldId, studentId), HttpStatusCode.Conflict, "HOLD_EXPIRED");
        await _api.CreateHoldAsync(schoolClass.Id, ApiClient.NewStudentId());
    }

    [Fact]
    public async Task Cancel_releases_the_seat_and_only_the_owner_can_act()
    {
        var schoolClass = await _api.CreateClassAsync(totalSeats: 1);
        var owner = ApiClient.NewStudentId();
        var hold = await _api.CreateHoldAsync(schoolClass.Id, owner);

        await ApiClient.AssertProblemAsync(
            await _api.ConfirmAsync(hold.HoldId, ApiClient.NewStudentId()), HttpStatusCode.Forbidden, "HOLD_NOT_OWNED");
        await ApiClient.AssertProblemAsync(
            await _api.CancelAsync(hold.HoldId, ApiClient.NewStudentId()), HttpStatusCode.Forbidden, "HOLD_NOT_OWNED");

        Assert.Equal(HttpStatusCode.NoContent, (await _api.CancelAsync(hold.HoldId, owner)).StatusCode);
        Assert.Equal(1, (await _api.GetClassAsync(schoolClass.Id)).AvailableSeats);

        await ApiClient.AssertProblemAsync(
            await _api.ConfirmAsync(hold.HoldId, owner), HttpStatusCode.Conflict, "HOLD_CANCELLED");

        // After cancelling, the same student may apply again.
        await _api.CreateHoldAsync(schoolClass.Id, owner);
    }
}
