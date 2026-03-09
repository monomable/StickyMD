using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;

namespace StickyMD.Utils;

public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseTaskLists()
        .Build();

    private static readonly Brush CodeBackground = CreateFrozenBrush("#F5E69C");
    private static readonly Brush TextBrush = CreateFrozenBrush("#222222");

    public static FlowDocument Render(string markdown, bool compact = false, int maxBlocks = int.MaxValue)
    {
        var parsed = Markdown.Parse(markdown ?? string.Empty, Pipeline);

        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left,
            Background = Brushes.Transparent,
            Foreground = TextBrush,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = compact ? 14 : 13,
            LineHeight = compact ? 20 : 18
        };

        var convertedCount = 0;
        foreach (var block in parsed)
        {
            foreach (var converted in ConvertBlock(block, compact))
            {
                if (convertedCount >= maxBlocks)
                {
                    break;
                }

                document.Blocks.Add(converted);
                convertedCount++;
            }

            if (convertedCount >= maxBlocks)
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
                    Foreground = Brushes.Gray
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
                yield return CreateParagraph(block.ToString() ?? string.Empty, compact);
                yield break;
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
        paragraph.Margin = compact
            ? new Thickness(0, 4, 0, 4)
            : new Thickness(0, 8, 0, 6);

        paragraph.FontSize = heading.Level switch
        {
            1 => compact ? 18 : 22,
            2 => compact ? 17 : 20,
            3 => compact ? 16 : 18,
            4 => compact ? 15 : 16,
            5 => compact ? 14 : 15,
            _ => compact ? 14 : 14
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
        var list = new System.Windows.Documents.List
        {
            MarkerStyle = listBlock.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = compact ? new Thickness(0, 2, 0, 2) : new Thickness(0, 4, 0, 4),
            Padding = new Thickness(18, 0, 0, 0)
        };

        foreach (var child in listBlock)
        {
            if (child is not ListItemBlock listItemBlock)
            {
                continue;
            }

            var listItem = new ListItem();

            foreach (var itemBlock in listItemBlock)
            {
                foreach (var converted in ConvertBlock(itemBlock, compact))
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
        var code = ExtractCode(codeBlock);

        return new Paragraph(new Run(code))
        {
            Margin = new Thickness(0, 6, 0, 6),
            Padding = new Thickness(8, 6, 8, 6),
            FontFamily = new FontFamily("Consolas"),
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
            Margin = compact
                ? new Thickness(0, 2, 0, 2)
                : new Thickness(0, 4, 0, 4)
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
        switch (inline)
        {
            case LiteralInline literal:
            {
                var text = literal.Content.ToString();

                if (TrySplitTaskPrefix(text, out var checkbox, out var restText))
                {
                    yield return CreateStyledRun(checkbox, style);

                    if (!string.IsNullOrEmpty(restText))
                    {
                        yield return CreateStyledRun(restText, style);
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
                run.FontFamily = new FontFamily("Consolas");
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
                var hyperlink = new Hyperlink();

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

                hyperlink.Foreground = CreateFrozenBrush("#1A5FB4");
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
                yield return CreateStyledRun(inline.ToString() ?? string.Empty, style);
                yield break;
        }
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

        if (text.StartsWith("[ ] ", StringComparison.Ordinal))
        {
            checkbox = "☐ ";
            restText = text[4..];
            return true;
        }

        if (text.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase))
        {
            checkbox = "☑ ";
            restText = text[4..];
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

        var normalized = htmlTag.Trim().ToLowerInvariant();

        if (normalized.StartsWith("<u") && !normalized.StartsWith("</"))
        {
            style = style with { Underline = true };
            return true;
        }

        if (normalized.StartsWith("</u"))
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

        if (!normalized.Contains("checkbox"))
        {
            return null;
        }

        return normalized.Contains("checked")
            ? CreateStyledRun("☑ ", style)
            : CreateStyledRun("☐ ", style);
    }

    private static Brush CreateFrozenBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private readonly record struct InlineStyle(
        bool Bold,
        bool Italic,
        bool Underline,
        bool Strikethrough)
    {
        public static InlineStyle Default => new(false, false, false, false);
    }
}
