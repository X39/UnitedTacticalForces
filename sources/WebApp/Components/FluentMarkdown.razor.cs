using Markdig;
using Markdig.Extensions.GenericAttributes;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.RegularExpressions;

namespace X39.UnitedTacticalForces.WebApp.Components;

public partial class FluentMarkdown : FluentComponentBase
{
    private static readonly MarkdownPipeline MarkdownPipeline = BuildMarkdownPipeline();
    private static readonly IReadOnlyDictionary<string, string> MarkdownIconLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["document"]  = "Document",
            ["extract"]   = "Extraction",
            ["host"]      = "Host",
            ["intel"]     = "Intel",
            ["map"]       = "Map",
            ["medical"]   = "Medical",
            ["modpack"]   = "Mod pack",
            ["objective"] = "Objective",
            ["rally"]     = "Rally point",
            ["radio"]     = "Radio",
            ["slot"]      = "Slot",
            ["stealth"]   = "Stealth",
            ["support"]   = "Support",
            ["time"]      = "Time",
            ["warning"]   = "Warning",
        };
    private static readonly Regex MarkdownIconShortcodeRegex =
        new(@":(?<icon>document|extract|host|intel|map|medical|modpack|objective|rally|radio|slot|stealth|support|time|warning):",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal class MarkdownSectionPreCodeExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline) { }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            var htmlRenderer = renderer as TextRendererBase<HtmlRenderer>;
            if (htmlRenderer == null)
            {
                return;
            }

            var originalCodeBlockRenderer = htmlRenderer.ObjectRenderers.FindExact<CodeBlockRenderer>();
            if (originalCodeBlockRenderer != null)
            {
                htmlRenderer.ObjectRenderers.Remove(originalCodeBlockRenderer);
            }

            htmlRenderer.ObjectRenderers.AddIfNotAlready(
                new MarkdownSectionPreCodeRenderer(
                    new MarkdownSectionPreCodeRendererOptions
                    {
                        PreTagAttributes = "{.snippet .hljs-copy-wrapper}",
                    }
                )
            );
        }
    }

    internal class SafeLinkExtension : IMarkdownExtension
    {
        private static readonly HashSet<string> AllowedUriSchemes = new(StringComparer.OrdinalIgnoreCase)
        {
            Uri.UriSchemeHttp,
            Uri.UriSchemeHttps,
            Uri.UriSchemeMailto,
        };

        public void Setup(MarkdownPipelineBuilder pipeline) { }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is HtmlRenderer htmlRenderer)
            {
                htmlRenderer.LinkRewriter = RewriteUnsafeUrl;
            }
        }

        private static string RewriteUnsafeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            var trimmedUrl = url.Trim();
            return IsSafeUrl(trimmedUrl) ? trimmedUrl : "#";
        }

        private static bool IsSafeUrl(string url)
        {
            if (url.StartsWith("#", StringComparison.Ordinal))
            {
                return true;
            }

            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
            {
                return false;
            }

            return !uri.IsAbsoluteUri || AllowedUriSchemes.Contains(uri.Scheme);
        }
    }

    /// <summary>
    /// Options for MarkdownSectionPreCodeRenderer
    /// </summary>
    internal class MarkdownSectionPreCodeRendererOptions
    {
        /// <summary>
        /// html attributes for Tag element in markdig generic attributes format
        /// </summary>
        public string? PreTagAttributes { get; init; }
        /// <summary>
        /// html attributes for Code element in markdig generic attributes format
        /// </summary>
        public string? CodeTagAttributes { get; init; }
    }

    /// <summary>
    /// Modified version of original markdig CodeBlockRenderer
    /// </summary>
    /// <see href="https://github.com/xoofx/markdig/blob/master/src/Markdig/Renderers/Html/CodeBlockRenderer.cs"/>
    internal class MarkdownSectionPreCodeRenderer : HtmlObjectRenderer<CodeBlock>
    {
        private          HashSet<string>?                       _blocksAsDiv;
        private readonly MarkdownSectionPreCodeRendererOptions? _options;

        public MarkdownSectionPreCodeRenderer(MarkdownSectionPreCodeRendererOptions? options)
        {
            _options = options;
        }

        public bool OutputAttributesOnPre { get; set; }

        /// <summary>
        /// Gets a map of fenced code block infos that should be rendered as div blocks instead of pre/code blocks.
        /// </summary>
        public HashSet<string> BlocksAsDiv => _blocksAsDiv ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        protected override void Write(HtmlRenderer renderer, CodeBlock obj)
        {
            renderer.EnsureLine();

            if (_blocksAsDiv is not null
                && (obj as FencedCodeBlock)?.Info is string info
                && _blocksAsDiv.Contains(info))
            {
                var infoPrefix = (obj.Parser as FencedCodeBlockParser)?.InfoPrefix
                                 ?? FencedCodeBlockParser.DefaultInfoPrefix;

                // We are replacing the HTML attribute `language-mylang` by `mylang` only for a div block
                // NOTE that we are allocating a closure here

                if (renderer.EnableHtmlForBlock)
                {
                    renderer.Write("<div")
                        .WriteAttributes(
                            obj.TryGetAttributes(),
                            cls => cls.StartsWith(infoPrefix, StringComparison.Ordinal)
                                ? cls.Substring(infoPrefix.Length)
                                : cls
                        )
                        .Write('>');
                }

                renderer.WriteLeafRawLines(obj, true, true, true);

                if (renderer.EnableHtmlForBlock)
                {
                    renderer.WriteLine("</div>");
                }
            }
            else
            {
                if (renderer.EnableHtmlForBlock)
                {
                    renderer.Write("<pre");

                    WritePreAttributes(renderer, obj, _options?.PreTagAttributes);

                    renderer.Write("><code");

                    WriteCodeAttributes(renderer, obj, _options?.CodeTagAttributes);

                    renderer.Write('>');
                }

                renderer.WriteLeafRawLines(obj, true, true);

                if (renderer.EnableHtmlForBlock)
                {
                    renderer.WriteLine("</code></pre>");
                }
            }

            renderer.EnsureLine();
        }

        private void WritePreAttributes(HtmlRenderer renderer, CodeBlock obj, string? preGenericAttributes)
        {
            HtmlAttributes? orig = null;

            if (OutputAttributesOnPre)
            {
                orig = obj.TryGetAttributes();
            }

            WriteElementAttributes(renderer, orig, preGenericAttributes);
        }

        private void WriteCodeAttributes(HtmlRenderer renderer, CodeBlock obj, string? codeGenericAttributes)
        {
            HtmlAttributes? orig = null;

            if (!OutputAttributesOnPre)
            {
                orig = obj.TryGetAttributes();
            }

            WriteElementAttributes(renderer, orig, codeGenericAttributes);
        }

        static private void WriteElementAttributes(
            HtmlRenderer renderer,
            HtmlAttributes? fromCodeBlock,
            string? genericAttributes
        )
        {
            // origin code block had no attributes
            fromCodeBlock ??= new HtmlAttributes();

            // append if any additional attributes provided
            var ss = new StringSlice(genericAttributes);
            if (!ss.IsEmpty && GenericAttributesParser.TryParse(ref ss, out var attributes))
            {
                if (fromCodeBlock != null)
                {
                    if (attributes.Classes != null)
                    {
                        foreach (var a in attributes.Classes)
                        {
                            fromCodeBlock.AddClass(a);
                        }
                    }

                    if (attributes.Properties != null)
                    {
                        foreach (var pr in attributes.Properties)
                        {
                            fromCodeBlock.AddProperty(pr.Key, pr.Value!);
                        }
                    }
                }
            }

            //
            renderer.WriteAttributes(fromCodeBlock);
        }
    }


    private IJSObjectReference _jsModule        = default!;
    private bool               _markdownChanged = false;
    private string?            _previousContent;

    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = default!;

    /// <summary>
    /// Gets or sets the Markdown content 
    /// </summary>
    [Parameter]
    public string? Content { get; set; }

    [Parameter]
    public EventCallback OnContentConverted { get; set; }

    public MarkupString HtmlContent { get; private set; }

    protected override void OnInitialized()
    {
        if (Content is null)
            throw new ArgumentException("You need to provide either Content or FromAsset parameter");
    }

    protected override void OnParametersSet()
    {
        if (string.Equals(_previousContent, Content, StringComparison.Ordinal))
            return;
        _previousContent  = Content;
        _markdownChanged = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // import code for highlighting code blocks
            _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                "./Components/FluentMarkdown.razor.js"
            );
        }

        if (firstRender || _markdownChanged)
        {
            _markdownChanged = false;

            // create markup from markdown source
            HtmlContent = await MarkdownToMarkupStringAsync();
            StateHasChanged();

            // notify that content converted from markdown 
            if (OnContentConverted.HasDelegate)
            {
                await OnContentConverted.InvokeAsync();
            }

            await _jsModule.InvokeVoidAsync("highlight");
            await _jsModule.InvokeVoidAsync("addCopyButton");
        }
    }

    /// <summary>
    /// Converts markdown, provided in Content or from markdown file stored as a static asset, to MarkupString for rendering.
    /// </summary>
    /// <returns>MarkupString</returns>
    private Task<MarkupString> MarkdownToMarkupStringAsync() => Task.FromResult(ConvertToMarkupString(Content));

    private static MarkdownPipeline BuildMarkdownPipeline()
    {
        return new MarkdownPipelineBuilder()
            .DisableHtml()
            .UseAlertBlocks()
            .UseAbbreviations()
            .UseAutoIdentifiers()
            .UseCitations()
            .UseDefinitionLists()
            .UseEmphasisExtras()
            .UseFigures()
            .UseFooters()
            .UseFootnotes()
            .UseGridTables()
            .UsePipeTables()
            .UseListExtras()
            .UseTaskLists()
            .UseAutoLinks()
            .Use<SafeLinkExtension>()
            .Use<MarkdownSectionPreCodeExtension>()
            .Build();
    }

    private static MarkupString ConvertToMarkupString(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var html = Markdown.ToHtml(value, MarkdownPipeline);
            html = ReplaceIconShortcodes(html);

            // Raw HTML is disabled and unsafe link schemes are neutralized before rendering as MarkupString.
            return new MarkupString(html);
        }

        return new MarkupString();
    }

    private static string ReplaceIconShortcodes(string html)
        => MarkdownIconShortcodeRegex.Replace(
            html,
            (match) =>
            {
                var icon = match.Groups["icon"].Value.ToLowerInvariant();
                var label = MarkdownIconLabels[icon];
                return
                    $"<span class=\"utf-md-icon utf-md-icon--{icon}\" role=\"img\" aria-label=\"{label}\"></span>";
            }
        );
}
