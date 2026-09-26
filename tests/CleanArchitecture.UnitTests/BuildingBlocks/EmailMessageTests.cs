using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.UnitTests.BuildingBlocks;

public sealed class EmailMessageTests
{
    private static Result<EmailMessage> Create(
        string recipient = "user@example.com",
        string subject = "Subject",
        string text = "Text",
        string html = "<p>Html</p>",
        Guid? id = null) =>
        EmailMessage.Create(id ?? Guid.NewGuid(), recipient, subject, text, html);

    [Fact]
    public void Create_Should_Succeed_ForValidMessageWithNonAsciiContent()
    {
        Result<EmailMessage> result = Create(subject: "مرحباً بك", text: "نص عربي\r\nسطر ثانٍ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Subject.ShouldBe("مرحباً بك");
    }

    [Fact]
    public void Create_Should_Fail_WhenIdIsEmpty() =>
        Create(id: Guid.Empty).Error.ShouldBe(EmailErrors.InvalidId);

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("user@example.com\r\nBcc: attacker@example.com")]
    [InlineData("Name <user@example.com>")]
    public void Create_Should_Fail_WhenRecipientIsInvalid(string recipient) =>
        Create(recipient: recipient).Error.ShouldBe(EmailErrors.InvalidRecipient);

    [Theory]
    [InlineData("")]
    [InlineData("Line one\r\nBcc: attacker@example.com")]
    public void Create_Should_Fail_WhenSubjectIsInvalid(string subject) =>
        Create(subject: subject).Error.ShouldBe(EmailErrors.InvalidSubject);

    [Fact]
    public void Create_Should_Fail_WhenSubjectIsTooLong() =>
        Create(subject: new string('a', EmailMessage.MaxSubjectLength + 1)).Error.ShouldBe(EmailErrors.InvalidSubject);

    [Fact]
    public void Create_Should_Fail_WhenBodiesExceedSizeLimit() =>
        Create(text: new string('a', EmailMessage.MaxCombinedBodyLength)).Error.ShouldBe(EmailErrors.InvalidBody);

    [Fact]
    public void Create_Should_Fail_WhenBodyContainsControlCharacters() =>
        Create(text: "bad\0value").Error.ShouldBe(EmailErrors.InvalidBody);
}
