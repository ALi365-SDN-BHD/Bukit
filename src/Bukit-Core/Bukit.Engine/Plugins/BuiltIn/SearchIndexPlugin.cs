using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bukit.Engine;
using Bukit.Rendering;

using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;
namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class SearchIndexPlugin : IBukitPlugin, IAfterBuildPlugin
{
    private readonly AppConfig _config;

    internal SearchIndexPlugin(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public string Name => "search-index";
    public string Version => "3.0.0";

    private static string GetSearchJs()
    {
        return string.Concat(
            "(function(){",
            "var input=document.querySelector('.bukit-search-input');",
            "var results=document.querySelector('.bukit-search-results');",
            "var items=[];",
            "var activeIndex=-1;",
            "var searchUrl=input.getAttribute('data-search-url')||'/search.json';",
            "fetch(searchUrl).then(function(r){return r.json();}).then(function(data){items=data;});",
            "function appendHighlighted(parent,text,query){",
            "var value=String(text||'');",
            "if(!query){parent.appendChild(document.createTextNode(value));return;}",
            "var re=new RegExp('('+query.replace(/[.*+?^${}()|[\\]\\\\]/g,'\\\\$&')+')','gi');",
            "var lastIndex=0;var match;",
            "while((match=re.exec(value))!==null){",
            "if(match.index>lastIndex){parent.appendChild(document.createTextNode(value.slice(lastIndex,match.index)));}",
            "var mark=document.createElement('mark');mark.textContent=match[0];parent.appendChild(mark);",
            "lastIndex=re.lastIndex;",
            "}",
            "if(lastIndex<value.length){parent.appendChild(document.createTextNode(value.slice(lastIndex)));}",
            "}",
            "function search(){",
            "var q=input.value.trim().toLowerCase();",
            "results.replaceChildren();activeIndex=-1;",
            "if(!q||items.length===0)return;",
            "var matched=items.map(function(item,i){",
            "var ts=(item.title||'').toLowerCase().indexOf(q)>=0?10:0;",
            "var cs=(item.content||'').toLowerCase().indexOf(q)>=0?1:0;",
            "var w=item.weight||1;",
            "return {item:item,index:i,score:(ts+cs)*w};",
            "}).filter(function(m){return m.score>0;})",
            ".sort(function(a,b){return b.score-a.score;}).slice(0,20);",
            "if(matched.length===0){",
            "var empty=document.createElement('div');empty.className='bukit-search-empty';",
            "empty.textContent='No results found';results.appendChild(empty);return;}",
            "matched.forEach(function(m,i){",
            "var it=m.item;",
            "var d=document.createElement('a');",
            "d.className='bukit-search-item'+(i===0?' active':'');",
            "d.href=it.url;",
            "var strong=document.createElement('strong');appendHighlighted(strong,it.title||'Untitled',q);",
            "d.appendChild(strong);",
            "if(it.snippet){var br=document.createElement('br');d.appendChild(br);",
            "var small=document.createElement('small');appendHighlighted(small,it.snippet,q);d.appendChild(small);}",
            "d.setAttribute('data-index',i);",
            "results.appendChild(d);",
            "});activeIndex=0;",
            "}",
            "input.addEventListener('input',search);",
            "input.addEventListener('keydown',function(e){",
            "var list=results.querySelectorAll('.bukit-search-item');",
            "if(e.key==='ArrowDown'){e.preventDefault();",
            "if(list.length>0){activeIndex=(activeIndex+1)%list.length;updateActive(list);}}",
            "else if(e.key==='ArrowUp'){e.preventDefault();",
            "if(list.length>0){activeIndex=(activeIndex-1+list.length)%list.length;updateActive(list);}}",
            "else if(e.key==='Enter'&&activeIndex>=0&&list[activeIndex])",
            "{e.preventDefault();list[activeIndex].click();}",
            "else if(e.key==='Escape'){results.replaceChildren();activeIndex=-1;}",
            "});",
            "function updateActive(list){",
            "list.forEach(function(el){el.classList.remove('active');});",
            "if(list[activeIndex]){",
            "list[activeIndex].classList.add('active');",
            "list[activeIndex].scrollIntoView({block:'nearest'});",
            "}}",
            "})();"
        );
    }

    public void AfterBuild(BuildContext context)
    {
        var emitSnippet = TryResolveTemplate(context, "search", out var searchTemplate) &&
            TemplateCapabilitiesResolver.SupportsSearchSnippets(searchTemplate, context.LayoutsDir);
        var listRouteGraph = context.Data.TryGetValue(ListRouteGraphBuilder.BuildContextDataKey, out var graphValue) &&
            graphValue is ListRouteGraph graph
                ? graph
                : null;
        var seoModels = context.Data.TryGetValue(BuildContextDataKeys.SeoModels, out var seoModelsValue) &&
            seoModelsValue is IReadOnlyDictionary<string, SeoModel> models
                ? models
                : null;
        SearchIndexBuilder.GenerateSingleSearchIndex(
            context.OutputDir,
            context.BaseUrl,
            _config.Site.SearchIncludeDerived,
            emitSnippet,
            _config.Site.Search.MaxContentLength,
            context.RoutedDocuments,
            context.DerivedDocuments,
            context.SeoIndex,
            context.BodyStore,
            listRouteGraph,
            seoModels);
        WriteSearchUi(_config, context.OutputDir);
    }

    private static bool TryResolveTemplate(BuildContext context, string kind, out string template)
    {
        template = string.Empty;
        try
        {
            template = context.ResolveTemplateKind(kind);
            return true;
        }
        catch (ConfigException)
        {
            return false;
        }
    }

    internal static void WriteSearchUi(AppConfig config, string outputDir)
    {
        var searchConfig = config.Site.Search;
        var ui = searchConfig.Ui?.Trim().ToLowerInvariant();
        if (ui is null or "false" or "off")
        {
            return;
        }

        var placeholder = HtmlEncoder.Default.Encode(searchConfig.PlaceholderText ?? "Search...");
        var theme = searchConfig.UiTheme?.Trim().ToLowerInvariant() is "dark" or "auto" ? searchConfig.UiTheme : "light";

        var isDark = theme == "dark";
        var bg = isDark ? "#1e1e2e" : "#ffffff";
        var border = isDark ? "#45475a" : "#e0e0e0";
        var textColor = isDark ? "#cdd6f4" : "#333333";
        var inputBg = isDark ? "#313244" : "#f5f5f5";
        var hoverBg = isDark ? "#45475a" : "#eeeeee";
        var highlightBg = isDark ? "#585b70" : "#fff9c4";
        var placeholderColor = isDark ? "#6c7086" : "#999999";

        var sb = new StringBuilder();
        sb.AppendLine("<style>");
        sb.AppendLine(".bukit-search{max-width:600px;margin:0 auto;font-family:system-ui,sans-serif;}");
        sb.Append(".bukit-search-input{width:100%;padding:12px 16px;border:1px solid ").Append(border).Append(";border-radius:8px;background:").Append(inputBg).Append(";color:").Append(textColor).AppendLine(";font-size:16px;box-sizing:border-box;outline:none;transition:border-color .2s;}");
        sb.AppendLine(".bukit-search-input:focus{border-color:#7c3aed;}");
        sb.Append(".bukit-search-input::placeholder{color:").Append(placeholderColor).AppendLine(";}");
        sb.AppendLine(".bukit-search-results{margin-top:8px;}");
        sb.Append(".bukit-search-item{display:block;padding:10px 16px;border-radius:6px;color:").Append(textColor).AppendLine(";text-decoration:none;transition:background .15s;cursor:pointer;}");
        sb.Append(".bukit-search-item:hover,.bukit-search-item.active{background:").Append(hoverBg).AppendLine(";}");
        sb.Append(".bukit-search-item mark{background:").Append(highlightBg).AppendLine(";padding:0 2px;border-radius:2px;}");
        sb.Append(".bukit-search-empty{padding:20px;text-align:center;color:").Append(placeholderColor).AppendLine(";}");
        sb.AppendLine("</style>");
        sb.AppendLine("<div class=\"bukit-search\">");
        sb.Append("  <input type=\"search\" class=\"bukit-search-input\" placeholder=\"").Append(placeholder).AppendLine("\" autocomplete=\"off\" />");
        sb.AppendLine("  <div class=\"bukit-search-results\"></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<script>");
        sb.AppendLine(GetSearchJs());
        sb.AppendLine("</script>");

        var uiPath = Path.Combine(outputDir, "bukit-search.html");
        File.WriteAllText(uiPath, sb.ToString(), Encoding.UTF8);
    }
}
