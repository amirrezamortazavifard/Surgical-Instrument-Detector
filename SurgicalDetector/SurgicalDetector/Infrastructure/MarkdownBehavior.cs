using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace SurgicalDetector.Infrastructure
{
    public static class MarkdownBehavior
    {
        public static readonly DependencyProperty MarkdownTextProperty =
        DependencyProperty.RegisterAttached(
        "MarkdownText",
        typeof(string),
        typeof(MarkdownBehavior),
        new PropertyMetadata(null, OnMarkdownTextChanged));

        public static string GetMarkdownText(DependencyObject obj)
        {
            return (string)obj.GetValue(MarkdownTextProperty);
        }

        public static void SetMarkdownText(DependencyObject obj, string value)
        {
            obj.SetValue(MarkdownTextProperty, value);
        }

        private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock)
            {
                string text = e.NewValue as string;
                RenderMarkdown(textBlock, text);
            }
        }

        private static void RenderMarkdown(TextBlock textBlock, string text)
        {
            textBlock.Inlines.Clear();
            if (string.IsNullOrEmpty(text)) return;

            bool isRtl = IsRtl(text);
            textBlock.FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            textBlock.TextAlignment = isRtl ? TextAlignment.Justify : TextAlignment.Left;

            var parts = SplitByMarkdown(text);
            foreach (var part in parts)
            {
                Run run = new Run(part.Text);
                if (part.IsBold) run.FontWeight = FontWeights.Bold;
                if (part.IsItalic) run.FontStyle = FontStyles.Italic;
                textBlock.Inlines.Add(run);
            }
        }

        private static bool IsRtl(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (char c in text)
            {
                if (c >= 0x0600 && c <= 0x06FF) return true;
            }
            return false;
        }

        private static List<MarkdownPart> SplitByMarkdown(string text)
        {
            var result = new List<MarkdownPart>();
            string pattern = @"(\*\*(.*?)\*\*)|(\*(.*?)\*)";
            var matches = Regex.Matches(text, pattern);

            int lastIndex = 0;
            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    result.Add(new MarkdownPart { Text = text.Substring(lastIndex, match.Index - lastIndex) });
                }

                if (match.Groups[1].Success)
                {
                    result.Add(new MarkdownPart { Text = match.Groups[2].Value, IsBold = true });
                }
                else if (match.Groups[3].Success)
                {
                    result.Add(new MarkdownPart { Text = match.Groups[4].Value, IsItalic = true });
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                result.Add(new MarkdownPart { Text = text.Substring(lastIndex) });
            }

            return result;
        }

        private class MarkdownPart
        {
            public string Text { get; set; }
            public bool IsBold { get; set; }
            public bool IsItalic { get; set; }
        }
    }
}