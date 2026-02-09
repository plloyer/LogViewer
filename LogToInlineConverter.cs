using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace LogViewer
{
    public class LogToInlineConverter : IMultiValueConverter
    {
        // Colors - initialized lazily or safely
        private static Brush _colorEconomy;
        private static Brush _colorFood;
        private static Brush _colorMilitary;
        private static Brush _colorReligion;
        private static Brush _colorCoastal;
        private static Brush _colorSpent;
        private static Brush _colorKnights;
        
        private static Brush _colorAutoStart;
        private static Brush _colorSpectator;
        
        static LogToInlineConverter()
        {
            try 
            {
                _colorEconomy = Brushes.Orange;
                _colorFood = Brushes.Yellow;
                _colorMilitary = GetBrushFromHex("#FF6666");
                _colorReligion = GetBrushFromHex("#CC99FF");
                _colorCoastal = GetBrushFromHex("#6699FF");
                _colorSpent = GetBrushFromHex("#E65100"); // Dark-ish orange
                _colorKnights = GetBrushFromHex("#B388FF"); // Semi-light purple
                _colorAutoStart = GetBrushFromHex("#607D8B"); // Muted blue-gray
                _colorSpectator = GetBrushFromHex("#00BCD4"); // Cyan
            }
            catch { }
        }

        private static Brush GetBrushFromHex(string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return Brushes.Gray;
            }
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (values.Length < 1 || values[0] is not string text || string.IsNullOrEmpty(text))
                    return null;

                string stripPrefix = values.Length > 1 ? values[1] as string : null;

                // Strip prefix if present
                if (!string.IsNullOrEmpty(stripPrefix) && text.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Substring(stripPrefix.Length).TrimStart();
                }

                // Simple parsing logic
                bool isError = text.Contains("[Error", StringComparison.OrdinalIgnoreCase);
                bool isWarning = text.Contains("[Warning", StringComparison.OrdinalIgnoreCase);

                if (isError) return new TextBlock { Text = text, Foreground = Brushes.Red };
                if (isWarning) return new TextBlock { Text = text, Foreground = Brushes.Yellow };

                TextBlock tb = new TextBlock();
                string remaining = text;

                // Loop to handle multiple tags at the start of the line
                // e.g. "[Tag1] [Tag2] Message"
                while (!string.IsNullOrEmpty(remaining) && remaining.TrimStart().StartsWith("["))
                {
                    remaining = remaining.TrimStart();
                    int closeIndex = remaining.IndexOf(']');
                    if (closeIndex == -1) break;

                    string tagPart = remaining.Substring(0, closeIndex + 1);
                    Brush tagColor = GetTagColor(tagPart);

                    // If the tag color is default Gray (unknown), and we have already processed some known tags, 
                    // or if it just looks like a tag, we still treat it as a tag to maintain structure.
                    // But maybe we only want to color *known* tags brightly? 
                    // The user wants Economy/Military colored. 
                    // Let's color it.

                    tb.Inlines.Add(new Run(tagPart) { Foreground = tagColor, FontWeight = FontWeights.Bold });
                    tb.Inlines.Add(new Run(" ")); // Add parsed space back visually if needed, or just rely on TrimStart eating it

                    remaining = remaining.Substring(closeIndex + 1);
                }

                // Add the rest of the message
                if (!string.IsNullOrEmpty(remaining))
                {
                     tb.Inlines.Add(new Run(remaining) { Foreground = Brushes.White });
                }
                
                return tb;
            }
            catch
            {
                return null;
            }
        }

        private Brush GetTagColor(string tagWithBrackets)
        {
            string tag = tagWithBrackets.Trim('[', ']');
            
            if (tag.Contains("Economy", StringComparison.OrdinalIgnoreCase)) return _colorEconomy ?? Brushes.Orange;
            if (tag.Contains("Spending", StringComparison.OrdinalIgnoreCase)) return Brushes.LightSalmon; // Light orange-ish
            if (tag.Contains("Food", StringComparison.OrdinalIgnoreCase)) return _colorFood ?? Brushes.Yellow;
            if (tag.Contains("Military", StringComparison.OrdinalIgnoreCase)) return _colorMilitary ?? Brushes.Red;
            if (tag.Contains("Religion", StringComparison.OrdinalIgnoreCase)) return _colorReligion ?? Brushes.Purple;
            if (tag.Contains("Coastal", StringComparison.OrdinalIgnoreCase)) return _colorCoastal ?? Brushes.Blue;
            if (tag.Contains("Diplomacy", StringComparison.OrdinalIgnoreCase)) return Brushes.LimeGreen;
            if (tag.Contains("SPENT", StringComparison.OrdinalIgnoreCase)) return _colorSpent ?? Brushes.DarkOrange;
            if (tag.Contains("Knights", StringComparison.OrdinalIgnoreCase)) return _colorKnights ?? Brushes.MediumPurple;
            if (tag.Contains("AutoStart", StringComparison.OrdinalIgnoreCase)) return _colorAutoStart ?? Brushes.SlateGray;
            if (tag.Contains("Spectator", StringComparison.OrdinalIgnoreCase)) return _colorSpectator ?? Brushes.Cyan;

            return Brushes.Gray;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
