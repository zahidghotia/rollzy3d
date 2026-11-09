# Rollzy 3D

A 3D ball runner for Android (Unity 6, URP). The road twists, loops and spirals through five locations; levels get longer and faster as you go.

- **Browser preview** (same design, playable now): [web-preview/index.html](web-preview/index.html)
- **Android game** (Unity 6 project): [Rollzy3D/](Rollzy3D/), with the game code in [Rollzy3D/Assets/_Project/](Rollzy3D/Assets/_Project/)

## What's in the game

- **Road:** turns, hills, S-waves, corkscrews, loops and climbing spirals. The ball sticks to the road while the camera rolls with it.
- **Obstacles:** walls, double walls, hurdles, gaps, sliding blocks, a spinning bar, combos, zigzag walls and pistons. Corkscrews, loops and spirals only carry coins, so the player is never surprised by something they can't see.
- **50 levels on a world map**, across Jungle, Mountains, Neon City, Desert and Snow Peaks. Each level earns 1–3 stars.
- **Shop:** 15 balls and 7 trails, unlocked with coins, ads, by reaching a level, the daily reward, or the lucky spin.
- **Daily reward** (7-day streak) and **Lucky Spin** (one free spin a day).
- **Ad placements:**
  - Revive: continue the run.
  - Level complete: triple the coins.
  - After two fails: skip the level.
  - Free coins.
  - Unlocking shop items.
  - Trying a locked ball for one run.
  - Doubling the daily reward.
  - An extra spin.
  - An interstitial every 3 levels.

## Balance

The browser version was tested with a bot that plays like a person: it reacts after a delay and mistimes moves slightly. The bot plays three ways:

- **Perfect:** a check that every level can be finished.
- **Skilled:** about 0.22 s reaction time.
- **Casual:** about 0.32 s reaction time.

| Level | Start speed | Length | Feel |
| --- | --- | --- | --- |
| 1–2 | 47–48 km/h | ~31 s | Normal |
| 3–6 | 50–54 km/h | ~33 s | Hard |
| 7–15 | 56–69 km/h | ~35 s | Expert |
| 16+ | 71 → 94 km/h | ~37–40 s | Insane |

Speed also rises 22% from the start to the finish of each level. All tuning numbers are in `Rollzy3D/Assets/_Project/Scripts/Core/Balance.cs`.

## Build the Android game

The Unity project is already set up in `Rollzy3D/`: Unity 6000.3.28f1 with URP, the game files in `Assets/_Project/`, the gameplay scene created and the Android settings applied.

1. **Open the project.** In Unity Hub, click **Add → Add project from disk** and choose the `Rollzy3D` folder. Open it with Unity 6000.3.28f1.
2. **Play in the editor.** Open `Assets/_Project/Scenes/Gameplay.unity` and press **Play**. A mouse drag is a swipe, and the arrow keys and Space also work.
3. **Test on your phone.** Turn on USB debugging on the phone, plug it in, and click **Rollzy 3D → 4. Build APK for my phone**. The file is saved as `Builds/Rollzy3D.apk`.
4. **Before publishing,** change the package name `com.yourstudio.rollzy3d` under **Project Settings → Player → Android → Other Settings**.

To set up a fresh project instead: create a **Universal 3D (URP)** project, copy `Assets/_Project/` into it, then click **Rollzy 3D → 1. Create Gameplay Scene** and **Rollzy 3D → 2. Apply Android Player Settings**.

### Real ads (AdMob)

Until you do this, the game shows its own **TEST AD** screen. That lets you check every ad placement without an AdMob account.

1. Download the latest `GoogleMobileAds-*.unitypackage` from github.com/googleads/googleads-mobile-unity/releases. Import it with **Assets → Import Package → Custom Package**.
2. Click **Rollzy 3D → 3. Enable AdMob**.
3. Go to **Assets → Google Mobile Ads → Settings** and enter your AdMob **App ID**.
4. The game starts with Google's **test** ad units. Before release, replace them with your own in `Rollzy3D/Assets/_Project/Scripts/Ads/AdProviders.cs`.
5. The consent form for the EEA, UK and Switzerland is built in. Set up the consent message in your AdMob account under **Privacy & messaging**.

### Publish on Google Play

1. Create an upload keystore under **Project Settings → Player → Android → Publishing Settings → Keystore Manager**. Back it up: you need the same keystore for every update.
2. Click **Rollzy 3D → 5. Build AAB for Google Play**. The file is saved as `Builds/Rollzy3D.aab`.
3. In Play Console, upload the AAB, complete the store listing, privacy policy and Data safety form, and declare that the app contains ads.

## Code map

| Folder | What it does |
| --- | --- |
| `Scripts/Core/` | `RollzyGame` (game loop, physics, rewards, ads flow), `Balance`, `Catalog` (locations, balls, trails, prizes), `SwipeInput`, `Rng` |
| `Scripts/Track/` | `TrackPath` (the 3D road curve), `LevelBuilder` (road, obstacles, coins, scenery, finish), `MeshKit` |
| `Scripts/Visual/` | Materials, ball skins, particles, camera, sky and fog blending, shop thumbnails |
| `Scripts/UI/` | `GameUI` (all screens, built with UI Toolkit), `Icons` |
| `Scripts/Ads/` | Test ads and AdMob (with consent) |
| `Scripts/Save/` | `SaveData` (coins, levels, stars, unlocks) |
| `Scripts/Audio/` | Generated sound effects and vibration |
| `Scripts/Editor/` | The **Rollzy 3D** menu: scene setup, Android settings, AdMob switch, builds |
| `UI/` | `Rollzy.uss` (styles) and the UI theme |
