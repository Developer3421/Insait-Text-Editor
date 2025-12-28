using System;
using Avalonia.Controls;

namespace InsaitTextEditor.Scripts.Text
{
    /// <summary>
    /// A set of methods for editing the selected text in an Avalonia TextBox (plain text).
    /// </summary>
    public static partial class SelectionEditingScript
    {
        private const string BoldStart = "**";
        private const string BoldEnd   = "**";
        private const string ItalicStart = "_";
        private const string ItalicEnd   = "_";
        private const string UnderStart = "__";
        private const string UnderEnd   = "__";

        public static void ToggleBold(TextBox editor)      => WrapOrUnwrapSelection(editor, BoldStart,   BoldEnd);
        public static void ToggleItalic(TextBox editor)    => WrapOrUnwrapSelection(editor, ItalicStart, ItalicEnd);
        public static void ToggleUnderline(TextBox editor) => WrapOrUnwrapSelection(editor, UnderStart,  UnderEnd);

        public static void IncreaseFontSize(TextBox editor, double step = 1.0, double max = 72.0)
        {
            if (editor is null) return;
            editor.FontSize = Math.Clamp(editor.FontSize + step, 1.0, max);
        }

        public static void DecreaseFontSize(TextBox editor, double step = 1.0, double min = 8.0)
        {
            if (editor is null) return;
            editor.FontSize = Math.Clamp(editor.FontSize - step, min, 999.0);
        }

        public static void ClearFormatting(TextBox editor)
        {
            if (editor is null) return;

            var text = editor.Text ?? string.Empty;
            GetOrderedSelection(editor, out var selStart, out var selEnd);
            if (selEnd <= selStart) return;

            StripSurroundingMarkers(ref text, ref selStart, ref selEnd, BoldStart,   BoldEnd);
            StripSurroundingMarkers(ref text, ref selStart, ref selEnd, ItalicStart, ItalicEnd);
            StripSurroundingMarkers(ref text, ref selStart, ref selEnd, UnderStart,  UnderEnd);

            var selected = text.Substring(selStart, selEnd - selStart)
                .Replace(BoldStart, string.Empty).Replace(BoldEnd, string.Empty)
                .Replace(ItalicStart, string.Empty).Replace(ItalicEnd, string.Empty)
                .Replace(UnderStart, string.Empty).Replace(UnderEnd, string.Empty);

            text = text[..selStart] + selected + text[selEnd..];

            editor.Text = text;
            editor.SelectionStart = selStart;
            editor.SelectionEnd = selStart + selected.Length;
        }

        private static void WrapOrUnwrapSelection(TextBox editor, string startMarker, string endMarker)
        {
            if (editor is null) return;

            var text = editor.Text ?? string.Empty;
            GetOrderedSelection(editor, out var selStart, out var selEnd);
            var hasSelection = selEnd > selStart;

            if (!hasSelection)
            {
                var insertion = startMarker + endMarker;
                text = text.Insert(selStart, insertion);
                editor.Text = text;

                var caret = selStart + startMarker.Length;
                editor.SelectionStart = caret;
                editor.SelectionEnd = caret;
                return;
            }

            if (IsSurroundedByMarkers(text, selStart, selEnd, startMarker, endMarker))
            {
                var before = text[..(selStart - startMarker.Length)];
                var inside = text.Substring(selStart, selEnd - selStart);
                var after  = text[(selEnd + endMarker.Length)..];

                text = before + inside + after;

                var newStart = selStart - startMarker.Length;
                var newEnd   = selEnd   - startMarker.Length;

                editor.Text = text;
                editor.SelectionStart = newStart;
                editor.SelectionEnd = newEnd;
                return;
            }

            var selected = text.Substring(selStart, selEnd - selStart);
            if (selected.StartsWith(startMarker, StringComparison.Ordinal) &&
                selected.EndsWith(endMarker, StringComparison.Ordinal) &&
                selected.Length >= startMarker.Length + endMarker.Length)
            {
                var inner = selected.Substring(startMarker.Length, selected.Length - startMarker.Length - endMarker.Length);
                text = text[..selStart] + inner + text[selEnd..];

                editor.Text = text;
                editor.SelectionStart = selStart;
                editor.SelectionEnd = selStart + inner.Length;
                return;
            }

            var wrapped = startMarker + selected + endMarker;
            text = text[..selStart] + wrapped + text[selEnd..];

            editor.Text = text;
            editor.SelectionStart = selStart + startMarker.Length;
            editor.SelectionEnd = editor.SelectionStart + selected.Length;
        }

        private static void GetOrderedSelection(TextBox editor, out int selStart, out int selEnd)
        {
            var a = editor.SelectionStart;
            var b = editor.SelectionEnd;
            var len = (editor.Text ?? string.Empty).Length;

            selStart = Math.Clamp(Math.Min(a, b), 0, len);
            selEnd   = Math.Clamp(Math.Max(a, b), 0, len);
        }

        private static bool IsSurroundedByMarkers(string text, int selStart, int selEnd, string startMarker, string endMarker)
        {
            var leftOk  = selStart >= startMarker.Length &&
                          text.Substring(selStart - startMarker.Length, startMarker.Length) == startMarker;
            var rightOk = selEnd + endMarker.Length <= text.Length &&
                          text.Substring(selEnd, endMarker.Length) == endMarker;
            return leftOk && rightOk;
        }

        private static void StripSurroundingMarkers(ref string text, ref int selStart, ref int selEnd, string startMarker, string endMarker)
        {
            var leftOk  = selStart >= startMarker.Length &&
                          text.Substring(selStart - startMarker.Length, startMarker.Length) == startMarker;
            var rightOk = selEnd + endMarker.Length <= text.Length &&
                          text.Substring(selEnd, endMarker.Length) == endMarker;

            if (!leftOk || !rightOk) return;

            text = text.Remove(selEnd, endMarker.Length);
            text = text.Remove(selStart - startMarker.Length, startMarker.Length);

            selStart -= startMarker.Length;
            selEnd   -= (startMarker.Length + endMarker.Length);
        }
    }
}