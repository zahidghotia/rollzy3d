using UnityEngine;

namespace Rollzy
{
    public enum Scenery { Jungle, Mountains, City, Desert, Snow }
    public enum UnlockType { Free, Coins, Ads, Level, Daily, Spin }

    /// <summary>A location the road travels through.</summary>
    public sealed class Biome
    {
        public string name;
        public Scenery scenery;
        public Color skyTop, skyBottom, fog, ground, roadA, roadB, edge, glow, obstacle, hazard;
    }

    public sealed class ShopItem
    {
        public string id, name;
        public UnlockType unlock;
        public int amount; // coins, ads or level depending on unlock type
        public Color swatchA, swatchB; // trails only
    }

    public sealed class Prize
    {
        public int coins;
        public string ball, trail;
        public float weight;
        public Color color;
        public string Label => coins > 0 ? coins.ToString() : ball != null ? "BALL" : "TRAIL";
    }

    public static class Catalog
    {
        static Color H(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        public static readonly Biome[] Biomes =
        {
            new Biome { name = "Jungle", scenery = Scenery.Jungle, skyTop = H("#3fa9ec"), skyBottom = H("#c6f1ff"), fog = H("#bfe9e4"), ground = H("#2f8f46"),
                roadA = H("#fff1d6"), roadB = H("#ffdca3"), edge = H("#8a5a33"), glow = H("#ffd23f"), obstacle = H("#ff4d6d"), hazard = H("#ff9f1c") },
            new Biome { name = "Mountains", scenery = Scenery.Mountains, skyTop = H("#4f73d9"), skyBottom = H("#e6eeff"), fog = H("#d6dff6"), ground = H("#7d9a6a"),
                roadA = H("#f2f4ff"), roadB = H("#dde3ff"), edge = H("#4a5bc4"), glow = H("#7fd8ff"), obstacle = H("#e84855"), hazard = H("#3185fc") },
            new Biome { name = "Neon City", scenery = Scenery.City, skyTop = H("#2d1f6e"), skyBottom = H("#ff9a7a"), fog = H("#d9849a"), ground = H("#35324d"),
                roadA = H("#5a5678"), roadB = H("#4c4869"), edge = H("#2a2645"), glow = H("#00e5ff"), obstacle = H("#ff3d9a"), hazard = H("#ffd400") },
            new Biome { name = "Desert", scenery = Scenery.Desert, skyTop = H("#4fa3ff"), skyBottom = H("#ffe2a8"), fog = H("#ffdca0"), ground = H("#e8b96a"),
                roadA = H("#fff3dc"), roadB = H("#ffdfb0"), edge = H("#c2703d"), glow = H("#ff7a3d"), obstacle = H("#2d7dd2"), hazard = H("#e4572e") },
            new Biome { name = "Snow Peaks", scenery = Scenery.Snow, skyTop = H("#7fa8f5"), skyBottom = H("#f4fbff"), fog = H("#e6f1ff"), ground = H("#eef5ff"),
                roadA = H("#eaf5ff"), roadB = H("#d2e8ff"), edge = H("#5aa9e6"), glow = H("#9b7bff"), obstacle = H("#ff5d8f"), hazard = H("#7b61ff") },
        };

        public static readonly ShopItem[] Balls =
        {
            new ShopItem { id = "classic", name = "Bubblegum", unlock = UnlockType.Free },
            new ShopItem { id = "beach", name = "Beach Ball", unlock = UnlockType.Coins, amount = 300 },
            new ShopItem { id = "soccer", name = "Striker", unlock = UnlockType.Coins, amount = 500 },
            new ShopItem { id = "basket", name = "Hoops", unlock = UnlockType.Ads, amount = 2 },
            new ShopItem { id = "tennis", name = "Ace", unlock = UnlockType.Coins, amount = 800 },
            new ShopItem { id = "melon", name = "Melon", unlock = UnlockType.Ads, amount = 3 },
            new ShopItem { id = "eight", name = "Lucky 8", unlock = UnlockType.Coins, amount = 1200 },
            new ShopItem { id = "earth", name = "Planet", unlock = UnlockType.Level, amount = 10 },
            new ShopItem { id = "smile", name = "Smiley", unlock = UnlockType.Daily },
            new ShopItem { id = "candy", name = "Candy", unlock = UnlockType.Ads, amount = 4 },
            new ShopItem { id = "chrome", name = "Chrome", unlock = UnlockType.Coins, amount = 2000 },
            new ShopItem { id = "neon", name = "Neon Grid", unlock = UnlockType.Spin },
            new ShopItem { id = "lava", name = "Lava", unlock = UnlockType.Ads, amount = 5 },
            new ShopItem { id = "gold", name = "Gold Rush", unlock = UnlockType.Coins, amount = 3000 },
            new ShopItem { id = "galaxy", name = "Galaxy", unlock = UnlockType.Level, amount = 25 },
        };

        public static readonly ShopItem[] Trails =
        {
            new ShopItem { id = "none", name = "None", unlock = UnlockType.Free, swatchA = H("#ece9f7"), swatchB = H("#ddd8f0") },
            new ShopItem { id = "cloud", name = "Cloud", unlock = UnlockType.Free, swatchA = H("#ffffff"), swatchB = H("#cfd8ff") },
            new ShopItem { id = "rainbow", name = "Rainbow", unlock = UnlockType.Coins, amount = 600, swatchA = H("#ff4d4d"), swatchB = H("#3fa9ff") },
            new ShopItem { id = "fire", name = "Fire", unlock = UnlockType.Ads, amount = 3, swatchA = H("#ffd000"), swatchB = H("#ff3d00") },
            new ShopItem { id = "stars", name = "Stardust", unlock = UnlockType.Coins, amount = 1000, swatchA = H("#ffd23f"), swatchB = H("#ff9b3f") },
            new ShopItem { id = "neon", name = "Neon", unlock = UnlockType.Level, amount = 15, swatchA = H("#00e5ff"), swatchB = H("#ff3d9a") },
            new ShopItem { id = "sparkle", name = "Sparkle", unlock = UnlockType.Spin, swatchA = H("#ff4d8d"), swatchB = H("#2fd36b") },
        };

        /// <summary>Daily login rewards; day 7 gives the Smiley ball (or 500 coins if owned).</summary>
        public static readonly int[] Daily = { 50, 75, 100, 150, 200, 300, -1 };

        public static readonly Prize[] Prizes =
        {
            new Prize { coins = 25, weight = 20, color = H("#ff4d8d") },
            new Prize { coins = 100, weight = 10, color = H("#ffc928") },
            new Prize { coins = 50, weight = 18, color = H("#2fd36b") },
            new Prize { ball = "neon", weight = 4, color = H("#00b8d4") },
            new Prize { coins = 25, weight = 20, color = H("#7b4dff") },
            new Prize { coins = 200, weight = 6, color = H("#ff9b3f") },
            new Prize { coins = 50, weight = 18, color = H("#2f80ed") },
            new Prize { trail = "sparkle", weight = 4, color = H("#e83e8c") },
        };

        public static ShopItem Ball(string id) => System.Array.Find(Balls, b => b.id == id) ?? Balls[0];
        public static ShopItem Trail(string id) => System.Array.Find(Trails, t => t.id == id) ?? Trails[0];
    }
}
