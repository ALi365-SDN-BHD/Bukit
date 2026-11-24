namespace Bukit.CloudBase.Transport;

internal enum CloudBaseErrorKind
{
    Unauthorized,
    Forbidden,
    NotFound,
    RateLimited,
    RequestFailed,
    ResponseInvalid
}
