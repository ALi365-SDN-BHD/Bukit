# Design: CloudBase filter environment references

- **Filter value only.** It is sent as a query value, encoded by the client; fields that form the
  request URL keep their identifier rules.
- **Resolve late.** References resolve in `ContentProviderFactory`, so `config check` and the
  config hash keep the reference text; CloudBase has no content cache (`cacheMode: off`), so a
  changed variable always reads fresh rows.
- **Fail closed.** An unset or empty variable is an error, because RLS turns a wrong filter into
  zero rows and a green build.
- **Credentials (decision to confirm).** Option (a): `anon` can read the release being built; the
  exposure window is documented. Option (b), a CI-only credential, would change the D5 credential
  policy and is not implemented.
