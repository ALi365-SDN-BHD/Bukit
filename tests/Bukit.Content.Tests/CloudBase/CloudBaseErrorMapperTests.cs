using Bukit.CloudBase.Transport;
using Bukit.Content.CloudBase;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests.CloudBase;

public sealed class CloudBaseErrorMapperTests
{
    [Theory]
    [InlineData("Unauthorized", DiagnosticCode.CloudBaseAuthFailed)]
    [InlineData("Forbidden", DiagnosticCode.CloudBaseAuthFailed)]
    [InlineData("NotFound", DiagnosticCode.CloudBaseNotFound)]
    [InlineData("RateLimited", DiagnosticCode.CloudBaseRateLimited)]
    [InlineData("RequestFailed", DiagnosticCode.CloudBaseRequestFailed)]
    [InlineData("ResponseInvalid", DiagnosticCode.CloudBaseResponseInvalid)]
    public void Map_ReturnsExpectedDiagnosticCode(string kind, DiagnosticCode expected)
        => Assert.Equal(expected, CloudBaseErrorMapper.Map(Enum.Parse<CloudBaseErrorKind>(kind)));
}
