namespace Bukit.Cli.Commands;

public sealed record WizardPreset
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public CloneTokens Tokens { get; init; } = new();
    public CloneLayoutInfo Layout { get; init; } = new();
    public CloneBehaviors Behaviors { get; init; } = new();
    public string TemplateStyle { get; init; } = "1";

    public static readonly WizardPreset Blog = new()
    {
        Name = "blog",
        Description = "Personal blog with sidebar and tag cloud",
        Tokens = new CloneTokens
        {
            Bg = "#faf8f5",
            Text = "#2d2a26",
            Muted = "#7a7268",
            Primary = "#2563eb",
            PrimaryStrong = "#1d4ed8",
            Accent = "#db2777",
            Radius = "10px",
            ContentMax = "720px",
            FontFamily = "\"Inter\", system-ui, sans-serif",
        },
        Layout = new CloneLayoutInfo
        {
            NavLinks =
            [
                new NavLinkInfo { Label = "Home", Url = "/" },
                new NavLinkInfo { Label = "Blog", Url = "/blog/" },
                new NavLinkInfo { Label = "About", Url = "/pages/about/" },
            ],
        },
        Behaviors = new CloneBehaviors
        {
            CardHoverLift = true,
            DarkModeToggle = true,
            MobileHamburger = true,
            BackToTop = true,
        },
        TemplateStyle = "1",
    };

    public static readonly WizardPreset Docs = new()
    {
        Name = "docs",
        Description = "Documentation site with left navigation",
        Tokens = new CloneTokens
        {
            Bg = "#ffffff",
            Text = "#1a1a2e",
            Muted = "#6b7280",
            Primary = "#4f46e5",
            PrimaryStrong = "#4338ca",
            Accent = "#10b981",
            Radius = "6px",
            ContentMax = "800px",
            WideMax = "1200px",
            FontFamily = "system-ui, sans-serif",
            CodeFontFamily = "\"Fira Code\", \"SFMono-Regular\", Consolas, monospace",
        },
        Layout = new CloneLayoutInfo
        {
            NavLinks =
            [
                new NavLinkInfo { Label = "Docs", Url = "/docs/" },
                new NavLinkInfo { Label = "API", Url = "/api/" },
                new NavLinkInfo { Label = "GitHub", Url = "https://github.com" },
            ],
        },
        Behaviors = new CloneBehaviors
        {
            StickyHeader = true,
            SmoothScroll = true,
            MobileHamburger = true,
        },
        TemplateStyle = "1",
    };

    public static readonly WizardPreset Landing = new()
    {
        Name = "landing",
        Description = "Single-page landing with Hero + Features + CTA",
        Tokens = new CloneTokens
        {
            Bg = "#ffffff",
            Text = "#111827",
            Muted = "#6b7280",
            Primary = "#7c3aed",
            PrimaryStrong = "#6d28d9",
            Accent = "#f59e0b",
            Radius = "12px",
            ContentMax = "960px",
            WideMax = "1200px",
            Shadow = "0 20px 60px rgba(17, 24, 39, 0.08)",
            ContainerPadding = "80px 24px 120px",
            SectionGap = "80px",
            FontFamily = "\"Inter\", system-ui, sans-serif",
        },
        Layout = new CloneLayoutInfo
        {
            HasHeroCta = true,
            HeroCtaText = "Get Started",
            HeroCtaUrl = "/#cta",
            HasFeaturesSection = true,
            HasCTASection = true,
            NavLinks =
            [
                new NavLinkInfo { Label = "Features", Url = "/#features" },
                new NavLinkInfo { Label = "Pricing", Url = "/#pricing" },
                new NavLinkInfo { Label = "Contact", Url = "/#contact" },
            ],
        },
        Behaviors = new CloneBehaviors
        {
            StickyHeader = true,
            ScrollShrinkNav = true,
            SmoothScroll = true,
            AnimateOnScroll = true,
            MobileHamburger = true,
            BackToTop = true,
        },
        TemplateStyle = "1",
    };

    public static readonly WizardPreset Minimal = new()
    {
        Name = "minimal",
        Description = "Ultra-minimal text-only site",
        Tokens = new CloneTokens
        {
            Bg = "#fafafa",
            Text = "#212121",
            Muted = "#757575",
            Primary = "#212121",
            PrimaryStrong = "#000000",
            Accent = "#616161",
            Radius = "4px",
            ContentMax = "680px",
            Border = "#e0e0e0",
            Shadow = "none",
            NavPadding = "12px 20px",
            ContainerPadding = "32px 20px 64px",
            SectionGap = "24px",
            FontFamily = "\"Georgia\", \"Times New Roman\", serif",
        },
        Layout = new CloneLayoutInfo
        {
            NavLinks =
            [
                new NavLinkInfo { Label = "Home", Url = "/" },
                new NavLinkInfo { Label = "Archive", Url = "/archive/" },
            ],
        },
        Behaviors = new CloneBehaviors(),
        TemplateStyle = "3",
    };

    public static readonly WizardPreset Portfolio = new()
    {
        Name = "portfolio",
        Description = "Photo/art portfolio with gallery",
        Tokens = new CloneTokens
        {
            Bg = "#0f0f0f",
            Text = "#e5e5e5",
            Muted = "#9ca3af",
            Primary = "#f0f0f0",
            PrimaryStrong = "#ffffff",
            Accent = "#fbbf24",
            Radius = "0",
            ContentMax = "1200px",
            WideMax = "1400px",
            Border = "#2a2a2a",
            Shadow = "0 0 0 transparent",
            CardShadow = "0 4px 16px rgba(0,0,0,0.4)",
            ContainerPadding = "48px 24px 96px",
            SectionGap = "60px",
            FontFamily = "system-ui, sans-serif",
            HeadingFontFamily = "\"DM Sans\", system-ui, sans-serif",
            Surface = "#1a1a1a",
            SurfaceMuted = "#141414",
        },
        Layout = new CloneLayoutInfo
        {
            NavLinks =
            [
                new NavLinkInfo { Label = "Work", Url = "/" },
                new NavLinkInfo { Label = "About", Url = "/pages/about/" },
                new NavLinkInfo { Label = "Contact", Url = "/pages/contact/" },
            ],
        },
        Behaviors = new CloneBehaviors
        {
            CardHoverLift = true,
            AnimateOnScroll = true,
            MobileHamburger = true,
            SmoothScroll = true,
        },
        TemplateStyle = "1",
    };

    public static readonly IReadOnlyList<WizardPreset> All = new[]
    {
        Blog, Docs, Landing, Minimal, Portfolio
    };
}
