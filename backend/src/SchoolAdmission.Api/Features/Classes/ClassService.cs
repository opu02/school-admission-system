using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Common.Errors;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Features.Classes;

public sealed class ClassService(AppDbContext db, TimeProvider time)
{
    public async Task<ClassResponse> CreateAsync(CreateClassRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name!.Trim();
        var normalizedName = SchoolClass.NormalizeName(name);

        if (await db.Classes.AnyAsync(c => c.NormalizedName == normalizedName, cancellationToken))
        {
            throw ClassNameTaken(name);
        }

        var schoolClass = new SchoolClass
        {
            Id = IdGenerator.NewClassId(),
            Name = name,
            NormalizedName = normalizedName,
            TotalSeats = request.TotalSeats!.Value,
            CreatedAt = time.GetUtcNowMs(),
        };

        db.Classes.Add(schoolClass);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two admins created the same name at the same moment; the unique index caught it.
            throw ClassNameTaken(name);
        }

        return new ClassResponse(
            schoolClass.Id,
            schoolClass.Name,
            schoolClass.TotalSeats,
            HeldSeats: 0,
            ConfirmedSeats: 0,
            AvailableSeats: schoolClass.TotalSeats,
            schoolClass.CreatedAt);
    }

    public async Task<IReadOnlyList<ClassResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await QueryWithSeatCounts()
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(ToResponse).ToList();
    }

    public async Task<ClassResponse> GetAsync(string classId, CancellationToken cancellationToken)
    {
        var row = await QueryWithSeatCounts()
            .SingleOrDefaultAsync(c => c.Id == classId, cancellationToken);

        return row is null
            ? throw ApiException.NotFound(ErrorCodes.ClassNotFound, "This class does not exist.")
            : ToResponse(row);
    }

    /// <summary>
    /// Seat counts are computed from the holds table every time, so they are always exact.
    /// A HELD row whose time has passed is NOT counted, even before the background job
    /// marks it EXPIRED - the seat is free the moment the hold expires.
    /// </summary>
    private IQueryable<ClassSeatRow> QueryWithSeatCounts()
    {
        var now = time.GetUtcNow();

        return db.Classes
            .AsNoTracking()
            .Select(c => new ClassSeatRow
            {
                Id = c.Id,
                Name = c.Name,
                TotalSeats = c.TotalSeats,
                CreatedAt = c.CreatedAt,
                HeldSeats = c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now),
                ConfirmedSeats = c.Holds.Count(h => h.Status == HoldStatus.Confirmed),
            });
    }

    private static ClassResponse ToResponse(ClassSeatRow row) =>
        new(
            row.Id,
            row.Name,
            row.TotalSeats,
            row.HeldSeats,
            row.ConfirmedSeats,
            AvailableSeats: Math.Max(0, row.TotalSeats - row.HeldSeats - row.ConfirmedSeats),
            row.CreatedAt);

    private static ApiException ClassNameTaken(string name) =>
        ApiException.Conflict(ErrorCodes.ClassNameTaken, $"A class named \"{name}\" already exists.");

    // Member-init (not constructor) projection so EF Core can still filter/sort on it.
    private sealed class ClassSeatRow
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required int TotalSeats { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required int HeldSeats { get; init; }
        public required int ConfirmedSeats { get; init; }
    }
}
