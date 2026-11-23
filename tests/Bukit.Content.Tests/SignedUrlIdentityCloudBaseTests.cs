using Bukit.Content.Media;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class SignedUrlIdentityCloudBaseTests
{
    [Theory]
    [InlineData("https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/a.png?token=SECRET")]
    [InlineData("https://env-abc.api.intl.tcloudbasegateway.com/v1/storages/object/sign/media/a.png?token=SECRET")]
    [InlineData("https://env-abc.storage.tcloudbase.com/media/a.png?token=SECRET")]
    public void IdentityTarget_DropsCloudBaseToken(string url)
    {
        var uri = new Uri(url);

        Assert.DoesNotContain("token", SignedUrlIdentity.IdentityTarget(uri), StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(
            SignedUrlIdentity.RequestTarget(uri),
            SignedUrlIdentity.IdentityTarget(uri));
    }

    [Fact]
    public void IdentityTarget_OtherHostKeepsToken()
    {
        var uri = new Uri("https://evil.example.com/v1/storages/object/sign/media/a.png?token=SECRET");

        Assert.Equal(SignedUrlIdentity.RequestTarget(uri), SignedUrlIdentity.IdentityTarget(uri));
    }

    [Fact]
    public void IsSignatureExpired_CloudBaseToken_IsNotRead()
    {
        var uri = new Uri("https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/a.png?token=SECRET");

        Assert.False(SignedUrlIdentity.IsSignatureExpired(uri, DateTimeOffset.UtcNow.AddYears(100)));
    }
}
