using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatSyncPlannerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-22T00:00:00Z");

    [Theory]
    [InlineData("draft", "reviewed", true)]
    [InlineData("draft", "verified", true)]
    [InlineData("draft", "approved", true)]
    [InlineData("draft", "published", false)]
    [InlineData("draft", "needs-review", false)]
    [InlineData("publish", "reviewed", false)]
    [InlineData("publish", "verified", true)]
    [InlineData("publish", "approved", true)]
    [InlineData("publish", "published", false)]
    public void Create_UsesTargetSpecificSafeReviewStatusDefaults(
        string target,
        string reviewStatus,
        bool expectedCandidate)
    {
        var plan = WechatSyncPlanner.Create(Context(reviewStatus), Options() with { Target = target }, Now);

        Assert.Equal(expectedCandidate ? 1 : 0, plan.Candidates.Count);
        if (!expectedCandidate)
        {
            Assert.Contains(plan.Exclusions, exclusion =>
                exclusion.Code == "plugin.wechat-sync.reviewStatusDenied" &&
                exclusion.Severity == "warning");
        }
    }

    [Fact]
    public void Create_FailsClosedWhenManifestAndContentReviewStatusDiffer()
    {
        var plan = WechatSyncPlanner.Create(Context("verified", manifestReviewStatus: "approved"), Options(), Now);

        Assert.Empty(plan.Candidates);
        Assert.True(plan.HasErrors);
        Assert.Contains(plan.Exclusions, exclusion =>
            exclusion.Code == "plugin.wechat-sync.reviewStatusMismatch" &&
            exclusion.Severity == "error");
    }

    [Fact]
    public void Create_FailsClosedWhenEitherReviewStatusIsMissing()
    {
        var missingManifest = WechatSyncPlanner.Create(Context("approved", manifestReviewStatus: ""), Options(), Now);
        var missingContent = WechatSyncPlanner.Create(Context("", manifestReviewStatus: "approved"), Options(), Now);

        Assert.All([missingManifest, missingContent], plan =>
        {
            Assert.Empty(plan.Candidates);
            Assert.True(plan.HasErrors);
            Assert.Contains(plan.Exclusions, exclusion =>
                exclusion.Code == "plugin.wechat-sync.reviewStatusMissing");
        });
    }

    [Fact]
    public void Create_ExpiryBoundaryIsInclusiveAndFutureExpiryIsEligible()
    {
        var expired = WechatSyncPlanner.Create(Context("approved", expiresAt: Now), Options(), Now);
        var future = WechatSyncPlanner.Create(Context("approved", expiresAt: Now.AddTicks(1)), Options(), Now);

        Assert.Empty(expired.Candidates);
        Assert.Contains(expired.Exclusions, exclusion =>
            exclusion.Code == "plugin.wechat-sync.contentExpired");
        Assert.Single(future.Candidates);
    }

    [Fact]
    public void Create_ForceDoesNotBypassReviewPolicy()
    {
        var plan = WechatSyncPlanner.Create(
            Context("needs-review"),
            Options() with { Target = "publish", Force = true },
            Now);

        Assert.Empty(plan.Candidates);
        Assert.Contains(plan.Exclusions, exclusion =>
            exclusion.Code == "plugin.wechat-sync.reviewStatusDenied");
    }

    [Fact]
    public void Create_SyncStatusIsPreservedButNotImplicitlyUsedAsReviewAuthorization()
    {
        var plan = WechatSyncPlanner.Create(Context("approved", syncStatus: "failed"), Options(), Now);

        Assert.Single(plan.Candidates);
        Assert.Empty(plan.Exclusions);
    }

    [Fact]
    public void Create_CustomPublishPolicyMustStillBeApplied()
    {
        var options = Options() with
        {
            Target = "publish",
            DraftReviewStatuses = new HashSet<string>(["editorial-approved"], StringComparer.OrdinalIgnoreCase),
            PublishReviewStatuses = new HashSet<string>(["editorial-approved"], StringComparer.OrdinalIgnoreCase)
        };

        var plan = WechatSyncPlanner.Create(Context("EDITORIAL-APPROVED"), options, Now);

        Assert.Single(plan.Candidates);
    }

    [Fact]
    public void Create_FailsClosedWhenDirectCallerUsesInvalidReviewPolicy()
    {
        var options = Options() with
        {
            Target = "publish",
            DraftReviewStatuses = new HashSet<string>(["reviewed"], StringComparer.OrdinalIgnoreCase),
            PublishReviewStatuses = new HashSet<string>(["approved"], StringComparer.OrdinalIgnoreCase)
        };

        var plan = WechatSyncPlanner.Create(Context("approved"), options, Now);

        Assert.Empty(plan.Candidates);
        Assert.True(plan.HasErrors);
        Assert.Contains(plan.Exclusions, exclusion =>
            exclusion.Code == "plugin.wechat-sync.invalidReviewPolicy" &&
            exclusion.Severity == "error");
    }

    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("  ")]
    public void Create_FailsClosedWhenDirectCallerUsesUnsafeReviewStatus(string status)
    {
        var options = Options() with
        {
            DraftReviewStatuses = new HashSet<string>([status], StringComparer.OrdinalIgnoreCase),
            PublishReviewStatuses = new HashSet<string>([status], StringComparer.OrdinalIgnoreCase)
        };

        var plan = WechatSyncPlanner.Create(Context("approved"), options, Now);

        Assert.Empty(plan.Candidates);
        Assert.True(plan.HasErrors);
        Assert.Contains(plan.Exclusions, exclusion =>
            exclusion.Code == "plugin.wechat-sync.invalidReviewPolicy");
    }

    [Fact]
    public void Create_HidesAllEffectiveCandidatesWhenAnySelectedItemHasAnError()
    {
        var valid = Context("approved");
        var invalid = Context("verified", manifestReviewStatus: "approved");
        var context = new WechatSyncContext
        {
            RootDir = valid.RootDir,
            OutputDir = valid.OutputDir,
            BaseUrl = valid.BaseUrl,
            SiteName = valid.SiteName,
            SiteUrl = valid.SiteUrl,
            Logger = valid.Logger,
            Routed = [valid.Routed[0], invalid.Routed[0]]
        };

        var plan = WechatSyncPlanner.Create(context, Options(), Now);

        Assert.True(plan.HasErrors);
        Assert.Empty(plan.Candidates);
        Assert.Contains(plan.Exclusions, exclusion =>
            exclusion.Code == "plugin.wechat-sync.reviewStatusMismatch");
    }

    private static WechatSyncContext Context(
        string reviewStatus,
        string? manifestReviewStatus = null,
        DateTimeOffset? expiresAt = null,
        string syncStatus = "")
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceKey"] = "notion",
            ["sourceId"] = "page-1",
            ["summary"] = "Summary",
            ["manifestReviewStatus"] = manifestReviewStatus ?? reviewStatus,
            ["reviewStatus"] = reviewStatus,
            ["syncStatus"] = syncStatus
        };
        if (expiresAt is { } value)
        {
            metadata["expiresAt"] = value;
        }

        var item = new WechatSyncItem(
            "post-1",
            "Hello",
            "hello",
            Now,
            "<p>Hello</p>",
            metadata,
            new Dictionary<string, WechatSyncField>
            {
                ["type"] = new("text", "post")
            });

        return new WechatSyncContext
        {
            RootDir = Path.GetTempPath(),
            OutputDir = Path.GetTempPath(),
            BaseUrl = "/",
            SiteName = "Bukit",
            SiteUrl = "https://example.com",
            Logger = new ConsoleLogger(LogLevel.Error),
            Routed = [(item, new WechatSyncRoute("/posts/hello/", "posts/hello/index.html", "post"))]
        };
    }

    private static WechatSyncOptions Options()
        => new(
            SourceNames: [],
            ContentTypes: new HashSet<string>(["post"], StringComparer.OrdinalIgnoreCase),
            DefaultTypesWhenMissing: new HashSet<string>(["post"], StringComparer.OrdinalIgnoreCase),
            CacheFile: ".cache/wechat-sync/sync-cache.json",
            MaxAttempts: 1,
            BaseDelayMs: 1,
            BackoffFactor: 1,
            AppIdEnv: "APP_ID",
            AppSecretEnv: "APP_SECRET",
            ForceRetryIgnoreCacheEnv: string.Empty,
            Author: null,
            DefaultThumbMediaId: "thumb-media-id",
            NeedOpenComment: false,
            OnlyFansCanComment: false,
            SiteName: "Bukit",
            SiteUrl: "https://example.com",
            BaseUrl: "/");
}
