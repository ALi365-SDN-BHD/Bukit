using System.Text.Json.Serialization;

namespace Bukit.Plugins.PathReportPlugin;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PathReport))]
[JsonSerializable(typeof(PathReportFiles))]
[JsonSerializable(typeof(WechatMaterialUploadResult))]
public sealed partial class PathReportJsonContext : JsonSerializerContext
{
}
