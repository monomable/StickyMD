using System.Text;
using System.Windows;
using System.Windows.Documents;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushConverter = System.Windows.Media.BrushConverter;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaFontFamily = System.Windows.Media.FontFamily;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;

namespace StickyMD.Services;

public sealed class MarkdownService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseTaskLists()
        .Build();

    private static readonly MediaBrush CodeBackground = FreezeBrush("#F5E69C");
    private static readonly MediaBrush LinkBrush = FreezeBrush("#1A5FB4");

    public FlowDocument RenderPreview(string markdown)
    {
        return Render(markdown, compact: true, maxBlocks: 2);
    }

    public FlowDocument RenderFull(string markdown)
    {
        return Render(markdown, compact: false, maxBlocks: int.MaxValue);
    }

    private static FlowDocument Render(string markdown, bool compact, int maxBlocks)
    {
        var parsed = Markdown.Parse(markdown ?? string.Empty, Pipeline);

        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            Background = MediaBrushes.Transparent,
            FontFamily = new MediaFontFamily("Segoe UI"),
            FontSize = compact ? 14 : 13,
            LineHeight = compact ? 20 : 18
        };

        var count = 0;
        foreach (var block in parsed)
        {
            foreach (var converted in ConvertBlock(block, compact))
            {
                if (count >= maxBlocks)
                {
                    break;
                }

                document.Blocks.Add(converted);
                count++;
            }

            if (count >= maxBlocks)
            {
                break;
            }
        }

        if (document.Blocks.Count == 0)
        {
            document.Blocks.Add(CreateParagraph(compact: compact));
        }

        return document;
    }

    private static IEnumerable<WpfBlock> ConvertBlock(MdBlock block, bool compact)
    {
        if (ShouldSkipBlock(block))
        {
            yield break;
        }

        switch (block)
        {
            case HeadingBlock heading:
                yield return ConvertHeading(heading, compact);
                yield break;

            case ParagraphBlock paragraph:
                yield return ConvertParagraph(paragraph, compact);
                yield break;

            case ListBlock list:
                yield return ConvertList(list, compact);
                yield break;

            case QuoteBlock quoteBlock:
                foreach (var quoted in ConvertQuote(quoteBlock, compact))
                {
                    yield return quoted;
                }

                yield break;

            case FencedCodeBlock fencedCode:
                yield return ConvertCodeBlock(fencedCode);
                yield break;

            case CodeBlock codeBlock:
                yield return ConvertCodeBlock(codeBlock);
                yield break;

            case ThematicBreakBlock:
                yield return new Paragraph(new Run("- - -"))
                {
                    Margin = new Thickness(0, 4, 0, 4),
                    Foreground = MediaBrushes.Gray
                };
                yield break;

            case ContainerBlock container:
                foreach (var child in container)
                {
                    foreach (var converted in ConvertBlock(child, compact))
                    {
                        yield return converted;
                    }
                }

                yield break;

            default:
            {
                var fallbackText = block.ToString() ?? string.Empty;
                if (LooksLikeExtensionArtifact(fallbackText))
                {
                    yield break;
                }

                yield return CreateParagraph(fallbackText, compact);
                yield break;
            }
        }
    }

    private static IEnumerable<WpfBlock> ConvertQuote(QuoteBlock quoteBlock, bool compact)
    {
        foreach (var child in quoteBlock)
        {
            foreach (var block in ConvertBlock(child, compact))
            {
                block.Margin = new Thickness(14, block.Margin.Top, 0, block.Margin.Bottom);

                if (block is Paragraph paragraph)
                {
                    paragraph.Foreground = FreezeBrush("#555555");
                    paragraph.Inlines.InsertBefore(paragraph.Inlines.FirstInline, new Run("| "));
                }

                yield return block;
            }
        }
    }

    private static Paragraph ConvertHeading(HeadingBlock heading, bool compact)
    {
        var paragraph = CreateParagraph(compact: compact);

        foreach (var inline in ConvertInlineCollection(heading.Inline, InlineStyle.Default))
        {
            paragraph.Inlines.Add(inline);
        }

        paragraph.FontWeight = FontWeights.Bold;
        paragraph.Margin = compact ? new Thickness(0, 4, 0, 4) : new Thickness(0, 8, 0, 6);
        paragraph.FontSize = heading.Level switch
        {
            1 => compact ? 18 : 22,
            2 => compact ? 17 : 20,
            3 => compact ? 16 : 18,
            _ => compact ? 14 : 16
        };

        return paragraph;
    }

    private static Paragraph ConvertParagraph(ParagraphBlock paragraph, bool compact)
    {
        var result = CreateParagraph(compact: compact);

        foreach (var inline in ConvertInlineCollection(paragraph.Inline, InlineStyle.Default))
        {
            result.Inlines.Add(inline);
        }

        return result;
    }

    private static System.Windows.Documents.List ConvertList(ListBlock listBlock, bool compact)
    {
        var isTaskList = IsTaskList(listBlock);

        var list = new System.Windows.Documents.List
        {
            MarkerStyle = isTaskList
                ? TextMarkerStyle.None
                : listBlock.IsOrdered
                    ? TextMarkerStyle.Decimal
                    : TextMarkerStyle.Disc,
            Margin = compact ? new Thickness(0, 2, 0, 2) : new Thickness(0, 4, 0, 4),
            Padding = new Thickness(18, 0, 0, 0)
        };

        foreach (var child in listBlock)
        {
            if (child is not ListItemBlock itemBlock)
            {
                continue;
            }

            var listItem = new ListItem();
            foreach (var nested in itemBlock)
            {
                foreach (var converted in ConvertBlock(nested, compact))
                {
                    listItem.Blocks.Add(converted);
                }
            }

            if (listItem.Blocks.Count == 0)
            {
                listItem.Blocks.Add(CreateParagraph(compact: compact));
            }

            list.ListItems.Add(listItem);
        }

        return list;
    }

    private static WpfBlock ConvertCodeBlock(CodeBlock codeBlock)
    {
        return new Paragraph(new Run(ExtractCode(codeBlock)))
        {
            Margin = new Thickness(0, 6, 0, 6),
            Padding = new Thickness(8, 6, 8, 6),
            FontFamily = new MediaFontFamily("Consolas"),
            FontSize = 12,
            Background = CodeBackground
        };
    }

    private static string ExtractCode(CodeBlock codeBlock)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < codeBlock.Lines.Count; i++)
        {
            var line = codeBlock.Lines.Lines[i];
            builder.Append(line.Slice.ToString());

            if (i < codeBlock.Lines.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static Paragraph CreateParagraph(string text = "", bool compact = false)
    {
        return new Paragraph(new Run(text))
        {
            Margin = compact ? new Thickness(0, 2, 0, 2) : new Thickness(0, 4, 0, 4)
        };
    }

    private static IEnumerable<WpfInline> ConvertInlineCollection(ContainerInline? container, InlineStyle style)
    {
        if (container is null)
        {
            yield break;
        }

        var activeStyle = style;
        var current = container.FirstChild;

        while (current is not null)
        {
            if (current is HtmlInline htmlInline)
            {
                if (TryHandleUnderlineTag(htmlInline.Tag, ref activeStyle))
                {
                    current = current.NextSibling;
                    continue;
                }

                var checkbox = ConvertTaskCheckbox(htmlInline.Tag, activeStyle);
                if (checkbox is not null)
                {
                    yield return checkbox;
                }

                current = current.NextSibling;
                continue;
            }

            foreach (var converted in ConvertInline(current, activeStyle))
            {
                yield return converted;
            }

            current = current.NextSibling;
        }
    }

    private static IEnumerable<WpfInline> ConvertInline(MdInline inline, InlineStyle style)
    {
        if (TryConvertTaskListInline(inline, style, out var taskCheckbox))
        {
            yield return taskCheckbox;
            yield break;
        }

        switch (inline)
        {
            case LiteralInline literal:
            {
                var text = literal.Content.ToString();

                if (TrySplitTaskPrefix(text, out var checkbox, out var rest))
                {
                    yield return CreateStyledRun(checkbox, style);

                    if (!string.IsNullOrEmpty(rest))
                    {
                        yield return CreateStyledRun(rest, style);
                    }

                    yield break;
                }

                yield return CreateStyledRun(text, style);
                yield break;
            }

            case LineBreakInline:
                yield return new LineBreak();
                yield break;

            case CodeInline codeInline:
            {
                var run = CreateStyledRun(codeInline.Content, style);
                run.FontFamily = new MediaFontFamily("Consolas");
                run.Background = CodeBackground;
                yield return run;
                yield break;
            }

            case EmphasisInline emphasisInline:
            {
                var childStyle = style;

                if (emphasisInline.DelimiterChar == '~')
                {
                    childStyle = childStyle with { Strikethrough = true };
                }
                else if (emphasisInline.DelimiterCount >= 2)
                {
                    childStyle = childStyle with { Bold = true };
                }
                else
                {
                    childStyle = childStyle with { Italic = true };
                }

                foreach (var child in ConvertInlineCollection(emphasisInline, childStyle))
                {
                    yield return child;
                }

                yield break;
            }

            case LinkInline linkInline when !linkInline.IsImage:
            {
                var hyperlink = new Hyperlink
                {
                    Foreground = LinkBrush
                };

                if (Uri.TryCreate(linkInline.Url, UriKind.Absolute, out var uri))
                {
                    hyperlink.NavigateUri = uri;
                }

                foreach (var child in ConvertInlineCollection(linkInline, style with { Underline = true }))
                {
                    hyperlink.Inlines.Add(child);
                }

                if (hyperlink.Inlines.Count == 0)
                {
                    hyperlink.Inlines.Add(CreateStyledRun(linkInline.Url ?? string.Empty, style with { Underline = true }));
                }

                yield return hyperlink;
                yield break;
            }

            case ContainerInline containerInline:
                foreach (var child in ConvertInlineCollection(containerInline, style))
                {
                    yield return child;
                }

                yield break;

            default:
            {
                var fallbackText = inline.ToString() ?? string.Empty;
                if (LooksLikeExtensionArtifact(fallbackText))
                {
                    yield break;
                }

                yield return CreateStyledRun(fallbackText, style);
                yield break;
            }
        }
    }

    private static bool IsTaskList(ListBlock listBlock)
    {
        foreach (var child in listBlock)
        {
            if (child is not ListItemBlock item)
            {
                continue;
            }

            if (ListItemContainsTask(item))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ListItemContainsTask(ListItemBlock item)
    {
        foreach (var block in item)
        {
            if (block is not ParagraphBlock paragraph || paragraph.Inline is null)
            {
                continue;
            }

            var inline = paragraph.Inline.FirstChild;
            while (inline is not null)
            {
                if (TryConvertTaskListInline(inline, InlineStyle.Default, out _))
                {
                    return true;
                }

                if (inline is HtmlInline html && ConvertTaskCheckbox(html.Tag, InlineStyle.Default) is not null)
                {
                    return true;
                }

                if (inline is LiteralInline literal &&
                    (literal.Content.ToString().StartsWith("[ ] ", StringComparison.Ordinal) ||
                     literal.Content.ToString().StartsWith("[x] ", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                inline = inline.NextSibling;
            }
        }

        return false;
    }

    private static Run CreateStyledRun(string text, InlineStyle style)
    {
        var run = new Run(text);

        if (style.Bold)
        {
            run.FontWeight = FontWeights.Bold;
        }

        if (style.Italic)
        {
            run.FontStyle = FontStyles.Italic;
        }

        var decorations = new TextDecorationCollection();

        if (style.Underline)
        {
            decorations.Add(TextDecorations.Underline[0]);
        }

        if (style.Strikethrough)
        {
            decorations.Add(TextDecorations.Strikethrough[0]);
        }

        if (decorations.Count > 0)
        {
            run.TextDecorations = decorations;
        }

        return run;
    }

    private static bool TrySplitTaskPrefix(string text, out string checkbox, out string restText)
    {
        checkbox = string.Empty;
        restText = text;

        if (text.StartsWith("[ ] ", StringComparison.Ordinal) ||
            text.StartsWith("- [ ] ", StringComparison.Ordinal) ||
            text.StartsWith("* [ ] ", StringComparison.Ordinal))
        {
            checkbox = "\u2610 ";
            restText = text.StartsWith("[ ] ", StringComparison.Ordinal) ? text[4..] : text[6..];
            return true;
        }

        if (text.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("* [x] ", StringComparison.OrdinalIgnoreCase))
        {
            checkbox = "\u2611 ";
            restText = text.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase) ? text[4..] : text[6..];
            return true;
        }

        return false;
    }

    private static bool TryHandleUnderlineTag(string? htmlTag, ref InlineStyle style)
    {
        if (string.IsNullOrWhiteSpace(htmlTag))
        {
            return false;
        }

        var tag = htmlTag.Trim().ToLowerInvariant();

        if (tag.StartsWith("<u") && !tag.StartsWith("</", StringComparison.Ordinal))
        {
            style = style with { Underline = true };
            return true;
        }

        if (tag.StartsWith("</u", StringComparison.Ordinal))
        {
            style = style with { Underline = false };
            return true;
        }

        return false;
    }

    private static WpfInline? ConvertTaskCheckbox(string? htmlTag, InlineStyle style)
    {
        if (string.IsNullOrWhiteSpace(htmlTag))
        {
            return null;
        }

        var normalized = htmlTag.ToLowerInvariant();

        if (!normalized.Contains("checkbox", StringComparison.Ordinal))
        {
            return null;
        }

        return normalized.Contains("checked", StringComparison.Ordinal)
            ? CreateStyledRun("\u2611 ", style)
            : CreateStyledRun("\u2610 ", style);
    }

    private static bool TryConvertTaskListInline(MdInline inline, InlineStyle style, out WpfInline taskCheckbox)
    {
        taskCheckbox = null!;

        var inlineType = inline.GetType();
        if (!string.Equals(inlineType.FullName, "Markdig.Extensions.TaskLists.TaskList", StringComparison.Ordinal))
        {
            return false;
        }

        var checkedProperty = inlineType.GetProperty("Checked");
        var isChecked = checkedProperty?.PropertyType == typeof(bool) &&
                        checkedProperty.GetValue(inline) is true;

        taskCheckbox = CreateStyledRun(isChecked ? "\u2611 " : "\u2610 ", style);
        return true;
    }

    private static bool ShouldSkipBlock(MdBlock block)
    {
        var fullName = block.GetType().FullName ?? string.Empty;

        return string.Equals(fullName, "Markdig.Extensions.AutoIdentifiers.HeadingLinkReferenceDefinition", StringComparison.Ordinal);
    }

    private static bool LooksLikeExtensionArtifact(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return text.StartsWith("Markdig.Extensions.", StringComparison.Ordinal);
    }

    private static MediaBrush FreezeBrush(string hex)
    {
        var brush = (MediaSolidColorBrush)new MediaBrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private readonly record struct InlineStyle(bool Bold, bool Italic, bool Underline, bool Strikethrough)
    {
        public static InlineStyle Default => new(false, false, false, false);
    }
}