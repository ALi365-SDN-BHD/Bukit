# Design: data-mode files

- **Reuse, do not fork.** The image localization stage already rewrites every document's
  `content.media.fieldKeys`, data documents included, and `SignedUrlIdentity` already gives signed
  URLs stable file names. The only gap was that data mode never signed file fields, so the change is
  to stop skipping resolution when a data source has `fileFields`.
- **One value per field.** The existing contract (a non-string file value becomes the placeholder)
  is kept; each gallery photo is its own row.
- **Preconditions unchanged.** Data mode needs the same `downloadToLocal`, `fieldKeys`,
  `onFailure: placeholder` and host rules, so a signed link never reaches the output.
