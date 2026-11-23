using System.Text;
using System.Text.Json;
using Bukit.CloudBase.Transport;
using Xunit;

namespace Bukit.CloudBase.Tests;

public sealed class CloudBaseCredentialsTests
{
    [Fact]
    public void DecodeRole_Anon_IsNotAdminRisk()
    {
        var role = CloudBaseCredentials.DecodeRole(Jwt(new { role = "anon" }));

        Assert.Equal("anon", role.Role);
        Assert.False(role.IsUnparseable);
        Assert.False(role.IsAdminRisk);
    }

    [Fact]
    public void DecodeRole_ServiceRole_IsAdminRisk()
    {
        var role = CloudBaseCredentials.DecodeRole(Jwt(new { role = "service_role" }));

        Assert.Equal("service_role", role.Role);
        Assert.True(role.IsAdminRisk);
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("only.two")]
    [InlineData("")]
    public void DecodeRole_WrongShape_IsAdminRisk(string token)
    {
        var role = CloudBaseCredentials.DecodeRole(token);

        Assert.Null(role.Role);
        Assert.True(role.IsUnparseable);
        Assert.True(role.IsAdminRisk);
    }

    [Fact]
    public void DecodeRole_MissingRole_IsAdminRisk()
    {
        var role = CloudBaseCredentials.DecodeRole(Jwt(new { sub = "x" }));

        Assert.Null(role.Role);
        Assert.True(role.IsAdminRisk);
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("bm90LWpzb24")]
    public void DecodeRole_MalformedPayload_IsAdminRisk(string payload)
    {
        var token = $"header.{payload}.signature";

        var role = CloudBaseCredentials.DecodeRole(token);

        Assert.Null(role.Role);
        Assert.True(role.IsAdminRisk);
    }

    [Fact]
    public void DecodeRole_Base64UrlPayload_IsDecoded()
    {
        var payload = Base64Url(Encoding.UTF8.GetBytes("""{"role":"anon","sub":"user"}"""));

        var role = CloudBaseCredentials.DecodeRole($"header.{payload}.signature");

        Assert.Equal("anon", role.Role);
    }

    internal static string Jwt(object payload)
        => $"header.{Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload))}.signature";

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
