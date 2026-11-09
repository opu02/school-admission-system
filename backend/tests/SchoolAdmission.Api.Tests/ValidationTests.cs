using System.Net;
using System.Net.Http.Json;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ValidationTests(ApiFactory factory)
{
    private readonly ApiClient _api = new(factory.CreateClient());

    [Fact]
    public async Task Create_class_returns_201_and_rejects_bad_or_duplicate_input()
    {
        var name = $"Class 6 - Morning {Guid.NewGuid():N}";
        var created = await _api.CreateClassAsync(120, name);
        Assert.StartsWith("c_", created.Id);
        Assert.Equal((name, 120, 120), (created.Name, created.TotalSeats, created.AvailableSeats));

        await ApiClient.AssertProblemAsync(
            await _api.Http.PostAsJsonAsync("/classes", new { name = $"  {name.ToUpperInvariant()} ", totalSeats = 5 }),
            HttpStatusCode.Conflict, "CLASS_NAME_TAKEN");
        await ApiClient.AssertProblemAsync(
            await _api.Http.PostAsJsonAsync("/classes", new { name = "", totalSeats = 0 }),
            HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        await ApiClient.AssertProblemAsync(
            await _api.Http.GetAsync("/classes/c_missing"),
            HttpStatusCode.NotFound, "CLASS_NOT_FOUND");
    }

    [Theory]
    [InlineData(null, "STUDENT_ID_REQUIRED")]
    [InlineData("12345", "STUDENT_ID_INVALID")]
    [InlineData("2015269123456789A", "STUDENT_ID_INVALID")]
    [InlineData("201526912345678901", "STUDENT_ID_INVALID")]
    public async Task Hold_requires_a_17_digit_student_id(string? studentId, string expectedCode)
    {
        var schoolClass = await _api.CreateClassAsync(totalSeats: 1);

        await ApiClient.AssertProblemAsync(
            await _api.HoldAsync(schoolClass.Id, studentId), HttpStatusCode.BadRequest, expectedCode);
        Assert.Equal(0, (await _api.GetClassAsync(schoolClass.Id)).HeldSeats);
    }
}
