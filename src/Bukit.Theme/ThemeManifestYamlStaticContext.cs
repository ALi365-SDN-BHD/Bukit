using YamlDotNet.Serialization;

namespace Bukit.Theme;

[YamlStaticContext]
[YamlSerializable(typeof(ThemeManifestV2))]
[YamlSerializable(typeof(ThemeCapabilities))]
[YamlSerializable(typeof(ThemeAssetsConfig))]
[YamlSerializable(typeof(ThemePageTemplateDefinition))]
[YamlSerializable(typeof(ThemePageTemplateAccept))]
[YamlSerializable(typeof(ThemeSectionDefinition))]
[YamlSerializable(typeof(ThemeComponentDefinition))]
[YamlSerializable(typeof(ThemeVariantDefinition))]
[YamlSerializable(typeof(ThemeDataBindingDefinition))]
public partial class ThemeManifestYamlStaticContext : YamlDotNet.Serialization.StaticContext
{
}
