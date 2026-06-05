using System.Runtime.InteropServices;
using System.Text;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public class DoctorCommandAppendFileOrWarnTests
{
    [Fact]
    public void AppendFileOrWarn_Should_AppendContent_When_FileExists()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "hello");
            var sb = new StringBuilder();
            DoctorTemplateAnalyzer.AppendFileOrWarn(path, sb);
            Assert.Equal("hello", sb.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AppendFileOrWarn_Should_EmitWarning_When_FileUnreadable()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "data");
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(path, UnixFileMode.None);
            }
            else
            {
                return;
            }

            var sb = new StringBuilder();
            var originalOut = Console.Out;
            using var writer = new StringWriter();
            Console.SetOut(writer);
            try
            {
                DoctorTemplateAnalyzer.AppendFileOrWarn(path, sb);
                var output = writer.ToString();
                Assert.Contains("Failed to read", output);
                Assert.Contains(path, output);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            Assert.Equal(0, sb.Length);
        }
        finally
        {
            try
            {
                if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                File.Delete(path);
            }
            catch { }
        }
    }

    [Fact]
    public void AppendFileOrWarn_Should_EmitWarning_When_FileNotFound()
    {
        var path = Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid() + ".html");
        var sb = new StringBuilder();
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            DoctorTemplateAnalyzer.AppendFileOrWarn(path, sb);
            var output = writer.ToString();
            Assert.Contains("Failed to read", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(0, sb.Length);
    }
}
