using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollzy
{
    /// <summary>
    /// Everything the player owns and has unlocked, stored as JSON in PlayerPrefs.
    /// Fine for launch; move to a server or signed file if cheating becomes a problem.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        const string Key = "rollzy.save.v2";

        public int coins;
        public int maxLevel = 1;
        public List<int> stars = new List<int>();
        public List<string> balls = new List<string> { "classic" };
        public List<string> trails = new List<string> { "none", "cloud" };
        public string ball = "classic";
        public string trail = "none";
        public List<string> adKeys = new List<string>();
        public List<int> adCounts = new List<int>();
        public string dailyLast = "";
        public int dailyStreak;
        public string spinFree = "";
        public int completions;
        public bool sound = true;
        public bool vibration = true;

        public static string Today => DateTime.Now.ToString("yyyy-MM-dd");
        public static string Yesterday => DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");

        public int GetStars(int level) => level >= 0 && level < stars.Count ? stars[level] : 0;

        public void SetStars(int level, int value)
        {
            while (stars.Count <= level) stars.Add(0);
            stars[level] = Mathf.Max(stars[level], value);
        }

        public int AdCount(string key)
        {
            int i = adKeys.IndexOf(key);
            return i < 0 ? 0 : adCounts[i];
        }

        public int AddAd(string key)
        {
            int i = adKeys.IndexOf(key);
            if (i < 0) { adKeys.Add(key); adCounts.Add(1); return 1; }
            return ++adCounts[i];
        }

        public bool Owns(bool isBall, string id) => (isBall ? balls : trails).Contains(id);

        public void Own(bool isBall, string id)
        {
            var list = isBall ? balls : trails;
            if (!list.Contains(id)) list.Add(id);
        }

        public static SaveData Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var data = JsonUtility.FromJson<SaveData>(json);
                    if (data != null) return data;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Rollzy] Save data unreadable, starting fresh: " + e.Message);
            }
            return new SaveData();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
