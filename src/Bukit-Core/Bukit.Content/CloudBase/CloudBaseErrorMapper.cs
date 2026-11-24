using Bukit.CloudBase.Transport;
using Bukit.Shared;

namespace Bukit.Content.CloudBase;

internal static class CloudBaseErrorMapper
{
    internal static DiagnosticCode Map(CloudBaseErrorKind kind)
        => kind switch
        {
            CloudBaseErrorKind.Unauthorized or CloudBaseErrorKind.Forbidden => DiagnosticCode.CloudBaseAuthFailed,
            CloudBaseErrorKind.NotFound => DiagnosticCode.CloudBaseNotFound,
            CloudBaseErrorKind.RateLimited => DiagnosticCode.CloudBaseRateLimited,
            CloudBaseErrorKind.ResponseInvalid => DiagnosticCode.CloudBaseResponseInvalid,
            _ => DiagnosticCode.CloudBaseRequestFailed
        };
}
