using UnityEngine;

namespace Rollzy
{
    /// <summary>
    /// Every gameplay number in one place. These values match the browser preview, which was
    /// balance-tested with a bot that plays with human reaction times.
    /// </summary>
    public static class Balance
    {
        // Track
        public const int Lanes = 3;
        public const float LaneWidth = 2.5f;
        public const float TrackWidth = Lanes * LaneWidth + 1f;
        public const float TileLength = 4f;
        public const float RunUp = 12f;
        public const float SafeStart = 18f;
        public const float RunOut = 30f;
        public const float PathStep = 0.25f;

        // Ball
        public const float BallRadius = 0.5f;
        public const float Gravity = 9.81f * 2.2f;
        public const float JumpVelocity = 7f;
        public const float CoyoteTime = 0.1f;
        public const float JumpBuffer = 0.15f;

        // Coins & rewards
        public const float CoinY = 0.6f;
        public const float CoinStep = 1.6f;
        public const int LevelCompleteBonus = 50;
        public const int FreeCoinsReward = 100;

        // Progression
        public const int LevelsPerWorld = 10;
        public const int MapLevels = 50;
        public const int InterstitialEveryNLevels = 3;
        public const float SpeedRamp = 0.22f; // speed grows this much from start to finish of a level

        public static float LevelSpeed(int level) => Mathf.Min(13f + (level - 1) * 0.45f, 26f);
        public static float Difficulty(int level) => Mathf.Clamp01((level - 1) / 25f);
        public static float LevelLength(int level) => Mathf.Min(420f + (level - 1) * 25f, 1100f);

        /// <summary>Seconds of travel between obstacle groups. Shrinks as levels get harder.</summary>
        public static float GapTime(float difficulty) => Mathf.Lerp(1.25f, 0.72f, difficulty);

        public static string DifficultyLabel(int level) =>
            level <= 2 ? "Normal" : level <= 6 ? "Hard" : level <= 15 ? "Expert" : "Insane";

        public static int WorldOf(int level) => ((level - 1) / LevelsPerWorld) % Catalog.Biomes.Length;

        /// <summary>Lane 0 is on the left of the screen.</summary>
        public static float LaneX(int lane) => (lane - (Lanes - 1) * 0.5f) * LaneWidth;
    }
}
