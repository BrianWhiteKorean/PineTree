using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PineTree.UserControlsOrder
{
    internal static class OrderItemEditorFactory
    {
        private const string LatexBanner = "라텍스/현수막";
        private const string WaterBasedPet = "수성/페트";

        private static readonly Dictionary<string, Func<UserControl>> Factories =
            new Dictionary<string, Func<UserControl>>(StringComparer.Ordinal)
            {
                { "현수막", () => new UcOrder수성_그룹A() },
                { LatexBanner, () => new UcOrder라텍스_그룹A() },
                { "합성지", () => new UcOrder수성_그룹A() },
                { WaterBasedPet, () => new UcOrder수성_그룹B() }
            };

        public static string GetEditorKey(string priceCategory, string categoryName)
        {
            if (priceCategory == "라텍스"
                && (categoryName == "라텍스 현수막" || categoryName == "라텍스현수막" || categoryName == "현수막"))
                return LatexBanner;
            if (categoryName == "페트")
                return priceCategory == "수성" || string.IsNullOrWhiteSpace(priceCategory) ? WaterBasedPet : null;
            if (categoryName == "합성지" && priceCategory != "수성") return null;
            return !string.IsNullOrEmpty(categoryName) && Factories.ContainsKey(categoryName) ? categoryName : null;
        }

        public static UserControl Create(string editorKey)
        {
            return Factories[editorKey]();
        }
    }
}
