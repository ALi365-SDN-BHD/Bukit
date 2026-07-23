using System.Text.Json.Serialization;

namespace Bukit.WechatSyncing;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(SyncCache))]
[JsonSerializable(typeof(WechatDraftAddRequest))]
[JsonSerializable(typeof(WechatPublishSubmitRequest))]
[JsonSerializable(typeof(WechatPublishStatusRequest))]
[JsonSerializable(typeof(WechatPublishStatusResult))]
internal sealed partial class WechatSyncJsonContext : JsonSerializerContext;
