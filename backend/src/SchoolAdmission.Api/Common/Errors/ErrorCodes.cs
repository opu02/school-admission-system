namespace SchoolAdmission.Api.Common.Errors;

/// <summary>
/// Machine-readable error codes returned in the "code" field of every error response.
/// The UI maps these to user-friendly messages.
/// </summary>
public static class ErrorCodes
{
    // 400
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string BadRequest = "BAD_REQUEST";
    public const string StudentIdRequired = "STUDENT_ID_REQUIRED";
    public const string StudentIdInvalid = "STUDENT_ID_INVALID";

    // 403
    public const string HoldNotOwned = "HOLD_NOT_OWNED";

    // 404
    public const string ClassNotFound = "CLASS_NOT_FOUND";
    public const string HoldNotFound = "HOLD_NOT_FOUND";

    // 409
    public const string ClassNameTaken = "CLASS_NAME_TAKEN";
    public const string NoSeatsAvailable = "NO_SEATS_AVAILABLE";
    public const string ActiveHoldExists = "ACTIVE_HOLD_EXISTS";
    public const string HoldExpired = "HOLD_EXPIRED";
    public const string HoldCancelled = "HOLD_CANCELLED";
    public const string HoldAlreadyConfirmed = "HOLD_ALREADY_CONFIRMED";

    // 500
    public const string InternalError = "INTERNAL_ERROR";
}
