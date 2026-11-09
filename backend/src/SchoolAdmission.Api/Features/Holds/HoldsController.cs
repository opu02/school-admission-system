using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Common;

namespace SchoolAdmission.Api.Features.Holds;

/// <summary>
/// Admission applications (seat holds). The student is identified by the
/// <c>X-Student-Id</c> header: the 17-digit birth registration number.
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class HoldsController(HoldService holds) : ControllerBase
{
    /// <summary>Apply for a seat: holds one seat for the student until <c>expiresAt</c>.</summary>
    /// <response code="201">The seat is held.</response>
    /// <response code="400">X-Student-Id is missing or not 17 digits.</response>
    /// <response code="404">The class does not exist.</response>
    /// <response code="409">No seats left (NO_SEATS_AVAILABLE) or the student already has an active application (ACTIVE_HOLD_EXISTS).</response>
    [HttpPost("classes/{classId}/holds")]
    [ProducesResponseType(typeof(HoldResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HoldResponse>> Create(
        string classId,
        [FromHeader(Name = StudentIdHeader.Name)] string? studentId,
        CancellationToken cancellationToken)
    {
        var hold = await holds.CreateAsync(classId, StudentIdHeader.Require(studentId), cancellationToken);
        return CreatedAtAction(nameof(Get), new { holdId = hold.HoldId }, hold);
    }

    /// <summary>Get the current status of one application.</summary>
    /// <remarks>X-Student-Id is optional here. If it is sent, it must match the application's student.</remarks>
    /// <response code="403">X-Student-Id was sent and belongs to another student.</response>
    /// <response code="404">The application does not exist.</response>
    [HttpGet("holds/{holdId}")]
    [ProducesResponseType(typeof(HoldResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HoldResponse>> Get(
        string holdId,
        [FromHeader(Name = StudentIdHeader.Name)] string? studentId,
        CancellationToken cancellationToken)
    {
        var checkedStudentId = string.IsNullOrWhiteSpace(studentId) ? null : StudentIdHeader.Require(studentId);
        return Ok(await holds.GetAsync(holdId, checkedStudentId, cancellationToken));
    }

    /// <summary>Pay the admission fee and confirm the admission. Safe to call more than once.</summary>
    /// <response code="403">The application belongs to another student.</response>
    /// <response code="404">The application does not exist.</response>
    /// <response code="409">The application expired (HOLD_EXPIRED) or was cancelled (HOLD_CANCELLED).</response>
    [HttpPost("holds/{holdId}/confirm")]
    [ProducesResponseType(typeof(HoldResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HoldResponse>> Confirm(
        string holdId,
        [FromHeader(Name = StudentIdHeader.Name)] string? studentId,
        CancellationToken cancellationToken) =>
        Ok(await holds.ConfirmAsync(holdId, StudentIdHeader.Require(studentId), cancellationToken));

    /// <summary>Cancel an unpaid application and release its seat.</summary>
    /// <response code="204">Cancelled (also returned if it was already cancelled).</response>
    /// <response code="403">The application belongs to another student.</response>
    /// <response code="404">The application does not exist.</response>
    /// <response code="409">Already confirmed (HOLD_ALREADY_CONFIRMED) or already expired (HOLD_EXPIRED).</response>
    [HttpDelete("holds/{holdId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        string holdId,
        [FromHeader(Name = StudentIdHeader.Name)] string? studentId,
        CancellationToken cancellationToken)
    {
        await holds.CancelAsync(holdId, StudentIdHeader.Require(studentId), cancellationToken);
        return NoContent();
    }

    /// <summary>All applications of the student in X-Student-Id, newest first.</summary>
    [HttpGet("me/holds")]
    [ProducesResponseType(typeof(IReadOnlyList<HoldResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<HoldResponse>>> Mine(
        [FromHeader(Name = StudentIdHeader.Name)] string? studentId,
        CancellationToken cancellationToken) =>
        Ok(await holds.ListForStudentAsync(StudentIdHeader.Require(studentId), cancellationToken));
}
