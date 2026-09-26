using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.BuildingBlocks.Email;

public static class EmailErrors
{
    public static readonly Error InvalidId = Error.Problem("Email.InvalidId", "The email id must not be empty.");

    public static readonly Error InvalidRecipient = Error.Problem(
        "Email.InvalidRecipient",
        "The email recipient is not a valid address.");

    public static readonly Error InvalidSubject = Error.Problem(
        "Email.InvalidSubject",
        $"The email subject is required, at most {EmailMessage.MaxSubjectLength} characters, and single-line.");

    public static readonly Error InvalidBody = Error.Problem(
        "Email.InvalidBody",
        "Both email bodies are required, within the size limit, and free of control characters.");
}
