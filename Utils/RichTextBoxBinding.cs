using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Xml;

namespace StickyMD.Utils;

public static class RichTextBoxBinding
{
    public static readonly DependencyProperty BoundDocumentProperty = DependencyProperty.RegisterAttached(
        "BoundDocument",
        typeof(FlowDocument),
        typeof(RichTextBoxBinding),
        new PropertyMetadata(null, OnBoundDocumentChanged));

    public static FlowDocument? GetBoundDocument(DependencyObject obj)
    {
        return (FlowDocument?)obj.GetValue(BoundDocumentProperty);
    }

    public static void SetBoundDocument(DependencyObject obj, FlowDocument? value)
    {
        obj.SetValue(BoundDocumentProperty, value);
    }

    private static void OnBoundDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox richTextBox)
        {
            return;
        }

        if (e.NewValue is not FlowDocument document)
        {
            richTextBox.Document = new FlowDocument();
            return;
        }

        richTextBox.Document = CloneDocument(document);
    }

    private static FlowDocument CloneDocument(FlowDocument source)
    {
        var xaml = XamlWriter.Save(source);

        using var stringReader = new StringReader(xaml);
        using var xmlReader = XmlReader.Create(stringReader);

        return (FlowDocument)XamlReader.Load(xmlReader);
    }
}
