using System.ComponentModel.DataAnnotations;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Features.Classes;

/// <summary>Body of <c>POST /classes</c>.</summary>
public sealed class CreateClassRequest
{
    /// <example>Class 6 - Morning Shift</example>
    [Required(ErrorMessage = "Class name is required.")]
    [StringLength(SchoolClass.NameMaxLength, ErrorMessage = "Class name can be at most 100 characters.")]
    public string? Name { get; set; }

    /// <example>120</example>
    [Required(ErrorMessage = "Total seats is required.")]
    [Range(1, SchoolClass.MaxTotalSeats, ErrorMessage = "Total seats must be a whole number between 1 and 100000.")]
    public int? TotalSeats { get; set; }
}

/// <summary>
/// A class with its live seat counts.
/// <c>heldSeats + confirmedSeats + availableSeats == totalSeats</c> always holds.
/// </summary>
public sealed record ClassResponse(
    string Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats,
    DateTimeOffset CreatedAt);
