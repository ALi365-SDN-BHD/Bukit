using Bukit.CloudBase.Models;

namespace Bukit.Content.CloudBase;

/// <summary>
/// Signs CloudBase file objects of one bucket: the storage API online, or the recorded identities
/// of an offline snapshot.
/// </summary>
internal delegate Task<IReadOnlyList<CloudBaseSignedObject>> CloudBaseSigner(IReadOnlyList<string> objectNames, CancellationToken cancellationToken);
