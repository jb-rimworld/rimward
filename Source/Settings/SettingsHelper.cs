using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWard
{
    internal static class SettingsHelper
    {
        public static void SliderLabeled(this Listing_Standard ls, string label, ref int val, string format, float min = 0f, float max = 100f, string tooltip = null)
        {
            float num = val;
            ls.SliderLabeled(label, ref num, format, min, max, tooltip);
            val = (int)num;
        }

        public static void SliderLabeled(this Listing_Standard ls, string label, ref float val, string format, float min = 0f, float max = 1f, string tooltip = null)
        {
            Rect rect = ls.GetRect(Text.LineHeight);
            Rect rect2 = rect.LeftPart(0.5f).Rounded();
            Rect rect3 = rect.RightPart(0.5f).Rounded().LeftPart(0.9f).Rounded();
            Rect rect4 = rect.RightPart(0.1f).Rounded();
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect2, label);
            float num = Widgets.HorizontalSlider(rect3, val, min, max, true);
            val = num;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(rect4, string.Format(format, val));
            if (!tooltip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }
            Text.Anchor = anchor;
            ls.Gap(ls.verticalSpacing);
        }

        public static void FloatRange(this Listing_Standard ls, string label, ref FloatRange range, float min = 0f, float max = 1f, string tooltip = null, ToStringStyle valueStyle = ToStringStyle.FloatTwo)
        {
            Rect rect = ls.GetRect(Text.LineHeight);
            Rect rect2 = rect.LeftPart(0.7f).Rounded();
            Rect rect3 = rect.RightPart(0.3f).Rounded().LeftPart(0.9f).Rounded();
            rect3.yMin -= 5f;
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect2, label);
            Text.Anchor = TextAnchor.MiddleRight;
            int hashCode = ls.CurHeight.GetHashCode();
            Widgets.FloatRange(rect3, hashCode, ref range, min, max, null, valueStyle);
            if (!tooltip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }
            Text.Anchor = anchor;
            ls.Gap(ls.verticalSpacing);
        }

        public static Rect GetRect(this Listing_Standard listing, float? height = null)
        {
            return listing.GetRect(height ?? Text.LineHeight);
        }

        public static void AddLabeledRadioList(this Listing_Standard listing, string header, string[] labels, ref string val, float? headerHeight = null)
        {
            if (header != string.Empty)
            {
                Widgets.Label(listing.GetRect(headerHeight), header);
            }
            listing.AddRadioList(GenerateLabeledRadioValues(labels), ref val);
        }

        private static void AddRadioList<T>(this Listing_Standard listing, List<LabeledRadioValue<T>> items, ref T val, float? height = null)
        {
            foreach (LabeledRadioValue<T> item in items)
            {
                if (Widgets.RadioButtonLabeled(listing.GetRect(height), item.Label, EqualityComparer<T>.Default.Equals(item.Value, val)))
                {
                    val = item.Value;
                }
            }
        }

        private static List<LabeledRadioValue<string>> GenerateLabeledRadioValues(string[] labels)
        {
            List<LabeledRadioValue<string>> list = new List<LabeledRadioValue<string>>();
            foreach (string text in labels)
            {
                list.Add(new LabeledRadioValue<string>(text, text));
            }
            return list;
        }

        public class LabeledRadioValue<T>
        {
            public string Label;
            public T Value;

            public LabeledRadioValue(string label, T val)
            {
                this.Label = label;
                this.Value = val;
            }
        }
    }
}
