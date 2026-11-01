assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Plugins/Bukit.Importing/ImportNotionSeedPusher.cs \
  '["dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj", "dotnet test tests/Bukit.Plugin.Import.Tests/Bukit.Plugin.Import.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Importing.Tests/ImportSeedContentWriterTests.cs \
  '["dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncWorkflow.cs \
  '["dotnet test tests/Bukit.Plugin.WechatSync.Tests/Bukit.Plugin.WechatSync.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  guide/dev/plugins.md \
  '["dotnet test tests/Bukit.Plugin.WechatSync.Tests/Bukit.Plugin.WechatSync.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  CHANGELOG.md \
  '["bash scripts/checks/docs/active-links.sh", "bash scripts/checks/docs/size-policy.sh"]' \
  false
