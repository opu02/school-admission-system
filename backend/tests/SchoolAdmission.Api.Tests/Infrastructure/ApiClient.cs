using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolAdmission.Api.Tests.Infrastructure;

public sealed record ClassDto(
    string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats);

public sealed record HoldDto(
    string HoldId, string ClassId, string ClassName, string StudentId, string Status, DateTimeOffset ExpiresAt);

public sealed record ProblemDto(int Status, string Code, string Detail);

/// <summary>Small typed helper around the API so tests read like the user story.</summary>
public sealed class ApiClient(HttpClient http)
{
    private static int _studentCounter;

    public HttpClient Http => http;

    /// <summary>A unique, valid 17-digit birth registration number.</summary>
    public static string NewStudentId() =>
        (20_150_000_000_000_000L + Interlocked.Increment(ref _studentCounter) + Random.Shared.Next(0, 1_000_000) * 1000L)
        .ToString();

    public async Task<ClassDto> CreateClassAsync(int totalSeats, string? name = null)
    {
        var response = await http.PostAsJsonAsync("/classes",
            new { name = name ?? $"Class {Guid.NewGuid():N}", totalSeats });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClassDto>())!;
    }

    public async Task<ClassDto> GetClassAsync(string classId) =>
        (await http.GetFromJsonAsync<ClassDto>($"/classes/{classId}"))!;

    public Task<HttpResponseMessage> HoldAsync(string classId, string? studentId) =>
        SendAsync(HttpMethod.Post, $"/classes/{classId}/holds", studentId);

    public Task<HttpResponseMessage> ConfirmAsync(string holdId, string? studentId) =>
        SendAsync(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId);

    public Task<HttpResponseMessage> CancelAsync(string holdId, string? studentId) =>
        SendAsync(HttpMethod.Delete, $"/holds/{holdId}", studentId);

    public Task<HttpResponseMessage> GetHoldAsync(string holdId) =>
        SendAsync(HttpMethod.Get, $"/holds/{holdId}", studentId: null);

    public Task<HttpResponseMessage> MyHoldsAsync(string studentId) =>
        SendAsync(HttpMethod.Get, "/me/holds", studentId);

    public async Task<HoldDto> CreateHoldAsync(string classId, string studentId)
    {
        var response = await HoldAsync(classId, studentId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync<HoldDto>(response))!;
    }

    public static async Task<T?> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await ReadAsync<ProblemDto>(response);
        Assert.NotNull(problem);
        Assert.Equal(code, problem!.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? studentId)
    {
        var request = new HttpRequestMessage(method, url);
        if (studentId is not null)
        {
            request.Headers.Add("X-Student-Id", studentId);
        }

        return http.SendAsync(request);
    }
}
