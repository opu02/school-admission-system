using Microsoft.AspNetCore.Mvc;

namespace SchoolAdmission.Api.Features.Classes;

/// <summary>Admission classes (one per grade + shift) and their seat counts.</summary>
[ApiController]
[Route("classes")]
[Produces("application/json")]
public sealed class ClassesController(ClassService classes) : ControllerBase
{
    /// <summary>Create a new class (school admin).</summary>
    /// <response code="201">The class was created.</response>
    /// <response code="400">Name or seat count is missing or invalid.</response>
    /// <response code="409">A class with the same name already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ClassResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClassResponse>> Create(
        [FromBody] CreateClassRequest request, CancellationToken cancellationToken)
    {
        var created = await classes.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { classId = created.Id }, created);
    }

    /// <summary>List all classes with held, confirmed and available seat counts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClassResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await classes.ListAsync(cancellationToken));

    /// <summary>Get the seat status of one class.</summary>
    /// <response code="404">The class does not exist.</response>
    [HttpGet("{classId}")]
    [ProducesResponseType(typeof(ClassResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassResponse>> Get(string classId, CancellationToken cancellationToken) =>
        Ok(await classes.GetAsync(classId, cancellationToken));
}
