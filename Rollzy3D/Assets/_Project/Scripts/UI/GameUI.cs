using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rollzy
{
    /// <summary>
    /// All menus and in-game overlays, built in code with UI Toolkit and styled by Rollzy.uss.
    /// Screens: home, ready, pause, revive, failed, complete, level map, shop, daily, spin, settings.
    /// </summary>
    public sealed class GameUI
    {
        readonly RollzyGame game;
        readonly VisualElement root;
        readonly Dictionary<string, VisualElement> screens = new Dictionary<string, VisualElement>();
        readonly List<Label> coinLabels = new List<Label>();
        readonly List<VisualElement> padded = new List<VisualElement>();
        string current;
        Rect lastSafe;

        public bool AdOpen { get; private set; }

        // HUD
        VisualElement hud, progressFill, toast;
        Label hudLevel, hudSpeed, hudCoins, toastName;
        // Home
        Label playLevel, worldTag;
        VisualElement dailyDot, spinDot;
        // Ready
        Label readyTitle, readyRoute, chipKmh, chipDiff;
        Button tryBtn;
        string tryId;
        // Revive / failed / complete
        Label reviveNum, failCoins, winCoins, unlockNote;
        VisualElement reviveFill, starsRow;
        Button skipBtn, tripleBtn;
        // Sheets
        ScrollView mapScroll;
        VisualElement shopGrid, daysGrid, wheel;
        Button tabBalls, tabTrails, claim1, claim2, spinFreeBtn, soundT, vibT, resetT;
        Label dailyNote;
        bool shopBalls = true, spinning, resetArmed;
        float wheelRot;
        // Ad + snack
        VisualElement adLayer, adFill;
        Label adKind, adNote, adCountLabel, snack;
        Button adClose;
        IVisualElementScheduledItem snackHide, toastHide;

        public GameUI(RollzyGame game, UIDocument doc, StyleSheet sheet)
        {
            this.game = game;
            root = doc.rootVisualElement;
            if (sheet != null) root.styleSheets.Add(sheet);
            root.AddToClassList("root");
            root.pickingMode = PickingMode.Ignore;

            BuildHud();
            BuildHome();
            BuildReady();
            BuildPause();
            BuildRevive();
            BuildFailed();
            BuildComplete();
            BuildMap();
            BuildShop();
            BuildDaily();
            BuildSpin();
            BuildSettings();
            BuildAd();
            snack = L(root, "", "snack");
            root.schedule.Execute(ApplySafeArea).Every(250);
        }

        // ------------------------------------------------------------ element helpers
        static VisualElement El(VisualElement parent, string classes, bool pick = false)
        {
            var e = new VisualElement();
            if (!string.IsNullOrEmpty(classes)) foreach (var c in classes.Split(' ')) e.AddToClassList(c);
            if (!pick) e.pickingMode = PickingMode.Ignore;
            parent?.Add(e);
            return e;
        }

        static Label L(VisualElement parent, string text, string classes)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var c in classes.Split(' ')) l.AddToClassList(c);
            parent?.Add(l);
            return l;
        }

        static VisualElement Icon(VisualElement parent, string icon, string classes)
        {
            var e = El(parent, "icon " + classes);
            e.style.backgroundImage = new StyleBackground(Icons.Get(icon));
            return e;
        }

        Button B(VisualElement parent, string text, string classes, Action click, bool ad = false, string sub = null)
        {
            var b = new Button();
            b.RemoveFromClassList(Button.ussClassName);
            b.AddToClassList("btn");
            if (!string.IsNullOrEmpty(classes)) foreach (var c in classes.Split(' ')) b.AddToClassList(c);
            b.clicked += () => { game.Sfx.Play(game.Sfx.click); click(); };
            var col = El(b, "btn-col");
            L(col, text, "btn-label");
            if (sub != null) L(col, sub, "btn-sub");
            if (ad)
            {
                var tag = El(b, "adtag");
                Icon(tag, "play", "adtag-icon");
                L(tag, "AD", "adtag-label");
            }
            parent.Add(b);
            return b;
        }

        static void SetLabel(Button b, string text) => b.Q<Label>(className: "btn-label").text = text;

        Button IconBtn(VisualElement parent, string icon, Action click)
        {
            var b = new Button();
            b.RemoveFromClassList(Button.ussClassName);
            b.AddToClassList("iconbtn");
            b.clicked += () => { game.Sfx.Play(game.Sfx.click); click(); };
            Icon(b, icon, "iconbtn-icon");
            parent.Add(b);
            return b;
        }

        VisualElement CoinPill(VisualElement parent, bool withPlus)
        {
            var pill = El(parent, "coinpill" + (withPlus ? "" : " solo"), true);
            El(pill, "gem");
            coinLabels.Add(L(pill, "0", "coin-text"));
            if (withPlus)
            {
                var plus = new Button { text = "+" };
                plus.RemoveFromClassList(Button.ussClassName);
                plus.AddToClassList("plus");
                plus.clicked += () => OpenSheet("shop");
                pill.Add(plus);
            }
            return pill;
        }

        VisualElement Screen(string name, string classes)
        {
            var s = El(root, "screen " + classes, true);
            screens[name] = s;
            padded.Add(s);
            s.style.display = DisplayStyle.None;
            return s;
        }

        VisualElement Sheet(string name, string title)
        {
            var s = Screen(name, "sheet blocker");
            var head = El(s, "sheethead");
            IconBtn(head, "back", ShowHome);
            L(head, title, "sheet-title");
            CoinPill(head, false);
            return s;
        }

        static ScrollView Body(VisualElement sheet)
        {
            var sv = new ScrollView(ScrollViewMode.Vertical) { touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic };
            sv.AddToClassList("body");
            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sv.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            sheet.Add(sv);
            return sv;
        }

        void Show(string name)
        {
            current = name;
            foreach (var kv in screens) kv.Value.style.display = kv.Key == name ? DisplayStyle.Flex : DisplayStyle.None;
            hud.style.display = game.State == GameState.Menu ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void RefreshCoins()
        {
            string t = game.Save.coins.ToString("N0");
            foreach (var l in coinLabels) l.text = t;
        }

        public void Snack(string message)
        {
            snack.text = message;
            snack.AddToClassList("show");
            snackHide?.Pause();
            snackHide = snack.schedule.Execute(() => snack.RemoveFromClassList("show")).StartingIn(1900);
        }

        public void Back()
        {
            if (current != "home") ShowHome();
        }

        /// <summary>True when a screen position is over a button or a menu, so touches there don't steer the ball.</summary>
        public bool IsPointerOverUI(Vector2 screenPos)
        {
            if (AdOpen) return true;
            var panel = root.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, UnityEngine.Screen.height - screenPos.y));
            for (var e = panel.Pick(p); e != null; e = e.parent)
                if (e is Button || e.ClassListContains("blocker")) return true;
            return false;
        }

        void ApplySafeArea()
        {
            var panel = root.panel;
            if (panel == null) return;
            var sa = UnityEngine.Screen.safeArea;
            if (sa == lastSafe) return;
            lastSafe = sa;
            int h = UnityEngine.Screen.height;
            Vector2 tl = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(sa.xMin, h - sa.yMax));
            Vector2 br = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(sa.xMax, h - sa.yMin));
            Vector2 full = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(UnityEngine.Screen.width, h));
            foreach (var e in padded)
            {
                e.style.paddingLeft = tl.x;
                e.style.paddingTop = tl.y;
                e.style.paddingRight = Mathf.Max(0, full.x - br.x);
                e.style.paddingBottom = Mathf.Max(0, full.y - br.y);
            }
        }

        // ------------------------------------------------------------ HUD
        void BuildHud()
        {
            hud = El(root, "hud");
            padded.Add(hud);
            var top = El(hud, "hud-top");
            IconBtn(top, "pause", game.Pause);
            var mid = El(top, "hud-mid");
            hudLevel = L(mid, "LEVEL 1", "hud-level");
            var bar = El(mid, "bar");
            progressFill = El(bar, "bar-fill");
            hudSpeed = L(mid, "0 KM/H", "hud-speed");
            var pill = El(top, "coinpill solo", true);
            El(pill, "gem");
            hudCoins = L(pill, "0", "coin-text");
            toast = El(hud, "toast");
            var toastPill = El(toast, "toast-pill");
            L(toastPill, "NOW ENTERING", "toast-small");
            toastName = L(toastPill, "JUNGLE", "toast-name");
            hud.style.display = DisplayStyle.None;
        }

        public void UpdateHud()
        {
            var lvl = game.Level;
            hudLevel.text = "LEVEL " + lvl.level;
            hudCoins.text = game.RunCoins.ToString();
            progressFill.style.width = Length.Percent(game.Progress * 100);
            hudSpeed.text = Mathf.RoundToInt(game.SpeedKmh) + " KM/H";
        }

        public void ShowLocation(string name)
        {
            toastName.text = name.ToUpperInvariant();
            toast.AddToClassList("show");
            toastHide?.Pause();
            toastHide = toast.schedule.Execute(() => toast.RemoveFromClassList("show")).StartingIn(2200);
        }

        // ------------------------------------------------------------ home
        void BuildHome()
        {
            var s = Screen("home", "home");
            var bar = El(s, "appbar");
            CoinPill(bar, true);
            IconBtn(bar, "settings", () => OpenSheet("settings"));
            var logo = El(s, "logo");
            L(logo, "ROLLZY", "logo-main");
            L(logo, "3D", "logo-badge");
            El(s, "spacer");
            var main = El(s, "home-main");
            worldTag = L(main, "World 1 · Jungle", "world-tag");
            var play = B(main, "PLAY", "play", () => game.StartLevel(game.Save.maxLevel), false, "LEVEL 1");
            playLevel = play.Q<Label>(className: "btn-sub");
            B(main, "+100 coins", "ad", FreeCoins, true);
            var tabs = El(s, "tabs", true);
            Tab(tabs, "Levels", "map", "#2f80ed", () => OpenSheet("map"));
            Tab(tabs, "Balls", "ball", "#ff4d8d", () => OpenSheet("shop"));
            dailyDot = Tab(tabs, "Daily", "gift", "#ff9b3f", () => OpenSheet("daily"));
            spinDot = Tab(tabs, "Spin", "wheel", "#7b4dff", () => OpenSheet("spin"));
        }

        VisualElement Tab(VisualElement parent, string label, string icon, string color, Action click)
        {
            var b = new Button();
            b.RemoveFromClassList(Button.ussClassName);
            b.AddToClassList("tab");
            b.clicked += () => { game.Sfx.Play(game.Sfx.click); click(); };
            var ic = El(b, "tab-icon");
            ic.style.backgroundColor = ColorUtility.TryParseHtmlString(color, out var c) ? c : Color.gray;
            Icon(ic, icon, "tab-glyph");
            L(b, label.ToUpperInvariant(), "tab-label");
            var dot = El(b, "dot");
            dot.style.display = DisplayStyle.None;
            parent.Add(b);
            return dot;
        }

        public void ShowHome()
        {
            RefreshCoins();
            var s = game.Save;
            playLevel.text = "LEVEL " + s.maxLevel;
            int w = Balance.WorldOf(s.maxLevel);
            worldTag.text = $"World {w + 1} · {Catalog.Biomes[w].name}".ToUpperInvariant();
            dailyDot.style.display = DailyStatus(out _) ? DisplayStyle.None : DisplayStyle.Flex;
            spinDot.style.display = s.spinFree == SaveData.Today ? DisplayStyle.None : DisplayStyle.Flex;
            if (game.State != GameState.Menu) { game.GoHome(); return; }
            Show("home");
        }

        void OpenSheet(string name)
        {
            switch (name)
            {
                case "map": RenderMap(); break;
                case "shop": RenderShop(); break;
                case "daily": RenderDaily(); break;
                case "spin": RenderSpin(); break;
                case "settings": RenderSettings(); break;
            }
            RefreshCoins();
            Show(name);
        }

        void FreeCoins()
        {
            game.Ads.ShowRewarded(ok =>
            {
                if (!ok) { Snack("No ad available right now. Try again soon."); return; }
                game.AddCoins(Balance.FreeCoinsReward);
                game.Sfx.Play(game.Sfx.reward);
                Snack($"+{Balance.FreeCoinsReward} coins");
            });
        }

        // ------------------------------------------------------------ in-game overlays
        void BuildReady()
        {
            var s = Screen("ready", "ready");
            s.pickingMode = PickingMode.Ignore;
            readyTitle = L(s, "LEVEL 1", "ready-title");
            readyRoute = L(s, "", "ready-route");
            var chips = El(s, "chips");
            chipKmh = L(chips, "", "chip");
            chipDiff = L(chips, "", "chip");
            L(s, "Swipe left / right to dodge\nSwipe up to jump", "hint");
            tryBtn = B(s, "Try ball", "ad small", () => game.TrySkin(tryId), true);
            L(s, "TAP TO START", "cta");
        }

        public void ShowReady()
        {
            var lvl = game.Level;
            readyTitle.text = "LEVEL " + lvl.level;
            var names = new List<string>();
            foreach (var b in lvl.biomes) names.Add(b.name);
            readyRoute.text = string.Join("  →  ", names);
            chipKmh.text = Mathf.RoundToInt(lvl.baseSpeed * 3.6f) + " km/h";
            string d = Balance.DifficultyLabel(lvl.level);
            chipDiff.text = d.ToUpperInvariant();
            chipDiff.EnableInClassList("hot", d != "Normal");
            tryId = game.TryOffer();
            tryBtn.style.display = tryId == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (tryId != null) SetLabel(tryBtn, "Try " + Catalog.Ball(tryId).name);
            Show("ready");
        }

        public void ShowGameplay() => Show("none");

        void BuildPause()
        {
            var s = Screen("pause", "overlay blocker");
            var card = El(s, "card", true);
            L(card, "PAUSED", "card-title");
            B(card, "Resume", "", game.Resume);
            B(card, "Restart", "ghost", game.Restart);
            B(card, "Home", "ghost", game.GoHome);
        }

        public void ShowPause() => Show("pause");

        void BuildRevive()
        {
            var s = Screen("revive", "overlay blocker");
            var card = El(s, "card", true);
            L(card, "SECOND CHANCE?", "card-title");
            reviveNum = L(card, "5", "big-number");
            var bar = El(card, "bar dark");
            reviveFill = El(bar, "bar-fill violet");
            B(card, "Continue", "ad", game.AcceptRevive, true);
            B(card, "No thanks", "text", game.DeclineRevive);
        }

        public void ShowRevive(int seconds, float fraction)
        {
            if (current != "revive") Show("revive");
            reviveNum.text = seconds.ToString();
            reviveFill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100);
        }

        void BuildFailed()
        {
            var s = Screen("failed", "overlay blocker");
            var card = El(s, "card", true);
            L(card, "OOPS!", "card-title");
            var reward = El(card, "reward");
            El(reward, "gem big");
            failCoins = L(reward, "+0", "reward-text");
            B(card, "Retry", "", game.Restart);
            skipBtn = B(card, "Skip level", "ad", game.SkipLevel, true);
            B(card, "Home", "ghost", game.GoHome);
        }

        public void ShowFailed()
        {
            failCoins.text = "+" + game.LastReward;
            skipBtn.style.display = game.CanSkip ? DisplayStyle.Flex : DisplayStyle.None;
            Show("failed");
        }

        void BuildComplete()
        {
            var s = Screen("complete", "overlay blocker");
            var card = El(s, "card", true);
            L(card, "LEVEL COMPLETE!", "card-title");
            starsRow = El(card, "stars");
            for (int i = 0; i < 3; i++) Icon(starsRow, "star", "star" + (i == 1 ? " middle" : ""));
            var reward = El(card, "reward");
            El(reward, "gem big");
            winCoins = L(reward, "+0", "reward-text");
            unlockNote = L(card, "", "card-note");
            tripleBtn = B(card, "x3 coins", "ad", game.TripleCoins, true);
            B(card, "Next level", "", game.NextLevel);
            B(card, "Home", "text", () => { game.Save.completions++; game.GoHome(); });
        }

        /// <summary>stars = -1 refreshes only the coin line (after the x3 ad).</summary>
        public void ShowComplete(int stars, string note)
        {
            winCoins.text = "+" + game.LastReward;
            if (stars < 0) { tripleBtn.style.display = DisplayStyle.None; return; }
            int i = 0;
            foreach (var star in starsRow.Children()) star.EnableInClassList("on", i++ < stars);
            unlockNote.text = note ?? "";
            unlockNote.style.display = string.IsNullOrEmpty(note) ? DisplayStyle.None : DisplayStyle.Flex;
            tripleBtn.style.display = DisplayStyle.Flex;
            Show("complete");
        }

        // ------------------------------------------------------------ level map
        void BuildMap()
        {
            var s = Sheet("map", "Levels");
            mapScroll = Body(s);
        }

        void RenderMap()
        {
            var save = game.Save;
            var list = mapScroll.contentContainer;
            list.Clear();
            VisualElement currentNode = null;
            int worlds = Balance.MapLevels / Balance.LevelsPerWorld;
            for (int w = 0; w < worlds; w++)
            {
                var bm = Catalog.Biomes[w % Catalog.Biomes.Length];
                int a = w * Balance.LevelsPerWorld + 1, z = a + Balance.LevelsPerWorld - 1;
                var card = El(list, "world");
                card.style.backgroundColor = bm.skyTop;
                card.style.borderBottomColor = bm.obstacle;
                int starsGot = 0;
                for (int l = a; l <= z; l++) starsGot += save.GetStars(l);
                var head = El(card, "world-head");
                L(head, $"World {w + 1} · {bm.name}", "world-title");
                L(head, $"{starsGot}/30 ★", "world-stars");
                var nodes = El(card, "nodes");
                for (int l = a; l <= z; l++)
                {
                    int lvl = l;
                    bool open = l <= save.maxLevel;
                    var node = new Button();
                    node.RemoveFromClassList(Button.ussClassName);
                    node.AddToClassList("node");
                    if (!open) node.AddToClassList("lock");
                    else if (l == save.maxLevel) { node.AddToClassList("cur"); currentNode = node; }
                    if (open)
                    {
                        L(node, l.ToString(), "node-num");
                        var st = El(node, "node-stars");
                        int got = save.GetStars(l);
                        for (int k = 0; k < 3; k++) Icon(st, "star", "mini-star" + (k < got ? " on" : ""));
                        node.clicked += () => { game.Sfx.Play(game.Sfx.click); game.StartLevel(lvl); };
                    }
                    else
                    {
                        Icon(node, "lock", "node-lock");
                        node.SetEnabled(false);
                    }
                    nodes.Add(node);
                }
                if (save.maxLevel < a)
                {
                    var veil = El(card, "veil");
                    L(veil, $"REACH LEVEL {a}", "veil-text");
                }
            }
            L(list, "More worlds coming soon", "note");
            if (currentNode != null) mapScroll.schedule.Execute(() => mapScroll.ScrollTo(currentNode)).StartingIn(50);
        }

        // ------------------------------------------------------------ shop
        void BuildShop()
        {
            var s = Sheet("shop", "Shop");
            var body = Body(s);
            var tabs = El(body.contentContainer, "segtabs");
            tabBalls = new Button(() => { shopBalls = true; RenderShop(); }) { text = "Balls" };
            tabTrails = new Button(() => { shopBalls = false; RenderShop(); }) { text = "Trails" };
            foreach (var t in new[] { tabBalls, tabTrails }) { t.RemoveFromClassList(Button.ussClassName); t.AddToClassList("segtab"); tabs.Add(t); }
            var free = El(body.contentContainer, "freebar");
            var txt = El(free, "freebar-text");
            L(txt, "Free coins", "freebar-title");
            L(txt, "Watch a short ad, get 100", "freebar-sub");
            B(free, "+100", "ad small", FreeCoins, true);
            shopGrid = El(body.contentContainer, "grid");
        }

        void RenderShop()
        {
            var save = game.Save;
            tabBalls.EnableInClassList("on", shopBalls);
            tabTrails.EnableInClassList("on", !shopBalls);
            shopGrid.Clear();
            foreach (var it in shopBalls ? Catalog.Balls : Catalog.Trails)
            {
                bool has = save.Owns(shopBalls, it.id);
                bool on = (shopBalls ? save.ball : save.trail) == it.id;
                var card = new Button();
                card.RemoveFromClassList(Button.ussClassName);
                card.AddToClassList("item");
                if (on) card.AddToClassList("eq");
                var pic = El(card, "item-pic");
                if (shopBalls) pic.style.backgroundImage = new StyleBackground(game.Thumbs.Get(it.id));
                else
                {
                    pic.AddToClassList("swatch");
                    pic.style.backgroundColor = it.swatchA;
                    var inner = El(pic, "swatch-inner");
                    inner.style.backgroundColor = it.swatchB;
                }
                if (!has) Icon(card, "lock", "item-lock");
                L(card, it.name, "item-name");
                var act = El(card, "item-act");
                if (on) { act.AddToClassList("ok"); L(act, "Equipped", "act-label"); }
                else if (has) L(act, "Equip", "act-label dark");
                else switch (it.unlock)
                {
                    case UnlockType.Coins: act.AddToClassList("buy"); El(act, "gem small"); L(act, it.amount.ToString("N0"), "act-label dark"); break;
                    case UnlockType.Ads: act.AddToClassList("adact"); Icon(act, "play", "act-icon"); L(act, $"AD {save.AdCount((shopBalls ? "b:" : "t:") + it.id)}/{it.amount}", "act-label"); break;
                    case UnlockType.Level: L(act, "Level " + it.amount, "act-label dark"); break;
                    case UnlockType.Daily: L(act, "Day 7", "act-label dark"); break;
                    default: L(act, "Spin prize", "act-label dark"); break;
                }
                var item = it;
                card.clicked += () => ShopTap(item, has);
                shopGrid.Add(card);
            }
        }

        void ShopTap(ShopItem it, bool has)
        {
            var save = game.Save;
            bool balls = shopBalls;
            if (has)
            {
                game.Sfx.Play(game.Sfx.click);
                if (balls) game.EquipBall(it.id); else { save.trail = it.id; save.Save(); }
                RenderShop();
                return;
            }
            switch (it.unlock)
            {
                case UnlockType.Coins:
                    if (save.coins < it.amount) { Snack($"You need {(it.amount - save.coins):N0} more coins"); return; }
                    game.AddCoins(-it.amount);
                    Unlock(it, balls);
                    break;
                case UnlockType.Ads:
                    game.Ads.ShowRewarded(ok =>
                    {
                        if (!ok) { Snack("Watch the whole ad to count it."); return; }
                        int n = save.AddAd((balls ? "b:" : "t:") + it.id);
                        if (n >= it.amount) Unlock(it, balls);
                        else { save.Save(); Snack($"{n}/{it.amount} ads watched"); RenderShop(); }
                    });
                    break;
                case UnlockType.Level: Snack($"Finish level {it.amount} to unlock"); break;
                case UnlockType.Daily: Snack("Claim the day 7 daily reward"); break;
                default: Snack("Win it on the Lucky Spin"); break;
            }
        }

        void Unlock(ShopItem it, bool ball)
        {
            var save = game.Save;
            save.Own(ball, it.id);
            if (ball) game.EquipBall(it.id); else save.trail = it.id;
            save.Save();
            game.Sfx.Play(game.Sfx.reward);
            Snack(it.name + " unlocked!");
            RenderShop();
        }

        // ------------------------------------------------------------ daily reward
        void BuildDaily()
        {
            var s = Sheet("daily", "Daily Reward");
            var body = Body(s).contentContainer;
            L(body, "Come back every day. Day 7 unlocks the Smiley ball.", "note");
            daysGrid = El(body, "days");
            var center = El(body, "center");
            claim2 = B(center, "Claim x2", "ad", () => game.Ads.ShowRewarded(ok => { if (ok) ClaimDaily(2); else Snack("Watch the whole ad to double it."); }), true);
            claim1 = B(center, "Claim", "", () => ClaimDaily(1));
            dailyNote = L(center, "Claimed! Next reward tomorrow.", "note");
        }

        /// <summary>Returns true if today's reward was already claimed; day = index of the next reward.</summary>
        bool DailyStatus(out int day)
        {
            var s = game.Save;
            if (s.dailyLast == SaveData.Today) { day = s.dailyStreak; return true; }
            day = s.dailyLast == SaveData.Yesterday ? s.dailyStreak % 7 : 0;
            return false;
        }

        void RenderDaily()
        {
            bool claimed = DailyStatus(out int day);
            daysGrid.Clear();
            for (int i = 0; i < 7; i++)
            {
                var tile = El(daysGrid, "day" + (i == 6 ? " big" : ""));
                bool done = claimed ? (day == 0 || i < day) : i < day;
                if (!claimed && i == day) tile.AddToClassList("today");
                if (done) tile.AddToClassList("done");
                L(tile, "DAY " + (i + 1), "day-label");
                var val = El(tile, "day-value");
                int r = Catalog.Daily[i];
                if (r > 0) { El(val, "gem small"); L(val, r.ToString(), "day-amount"); }
                else { var pic = El(val, "day-pic"); pic.style.backgroundImage = new StyleBackground(game.Thumbs.Get("smile")); }
            }
            claim1.style.display = claim2.style.display = claimed ? DisplayStyle.None : DisplayStyle.Flex;
            dailyNote.style.display = claimed ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void ClaimDaily(int mult)
        {
            if (DailyStatus(out int day)) return;
            var s = game.Save;
            int r = Catalog.Daily[day];
            if (r > 0) { game.AddCoins(r * mult); Snack($"+{r * mult} coins"); }
            else if (!s.Owns(true, "smile")) { s.Own(true, "smile"); Snack("Smiley ball unlocked!"); }
            else { game.AddCoins(500 * mult); Snack($"+{500 * mult} coins"); }
            s.dailyLast = SaveData.Today;
            s.dailyStreak = (day + 1) % 7;
            s.Save();
            game.Sfx.Play(game.Sfx.reward);
            RenderDaily();
        }

        // ------------------------------------------------------------ lucky spin
        void BuildSpin()
        {
            var s = Sheet("spin", "Lucky Spin");
            var body = Body(s).contentContainer;
            var box = El(body, "wheelbox");
            wheel = El(box, "wheel");
            wheel.style.backgroundImage = new StyleBackground(Icons.Wheel(Catalog.Prizes));
            int n = Catalog.Prizes.Length;
            for (int i = 0; i < n; i++)
            {
                var slot = El(wheel, "wheel-slot");
                slot.style.rotate = new Rotate(new Angle((i + 0.5f) * 360f / n, AngleUnit.Degree));
                L(slot, Catalog.Prizes[i].Label, "wheel-label");
            }
            El(box, "hub");
            Icon(box, "pin", "pin");
            var center = El(body, "center");
            spinFreeBtn = B(center, "Free spin", "gold", () => DoSpin(true));
            B(center, "Spin again", "ad", () => { if (!spinning) game.Ads.ShowRewarded(ok => { if (ok) DoSpin(false); }); }, true);
            L(center, "Win coins, the Neon Grid ball or the Sparkle trail.", "note");
        }

        void RenderSpin()
        {
            spinFreeBtn.style.display = game.Save.spinFree == SaveData.Today ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void DoSpin(bool free)
        {
            if (spinning) return;
            spinning = true;
            var save = game.Save;
            if (free) { save.spinFree = SaveData.Today; save.Save(); RenderSpin(); }
            var prizes = Catalog.Prizes;
            float total = 0;
            foreach (var p in prizes) total += p.weight;
            float r = UnityEngine.Random.value * total;
            int idx = 0;
            for (int i = 0; i < prizes.Length; i++) { r -= prizes[i].weight; if (r <= 0) { idx = i; break; } }
            float seg = 360f / prizes.Length;
            float target = (360 - (idx * seg + seg / 2)) % 360;
            wheelRot += 360 * 5 + ((target - (wheelRot % 360)) + 360) % 360;
            wheel.style.rotate = new Rotate(new Angle(wheelRot, AngleUnit.Degree));
            wheel.schedule.Execute(() =>
            {
                spinning = false;
                var p = prizes[idx];
                if (p.coins > 0) { game.AddCoins(p.coins); Snack($"You won {p.coins} coins!"); }
                else if (p.ball != null && !save.Owns(true, p.ball)) { save.Own(true, p.ball); save.Save(); Snack("You won the Neon Grid ball!"); }
                else if (p.trail != null && !save.Owns(false, p.trail)) { save.Own(false, p.trail); save.Save(); Snack("You won the Sparkle trail!"); }
                else { game.AddCoins(300); Snack("You won 300 coins!"); }
                game.Sfx.Play(game.Sfx.reward);
            }).StartingIn(4100);
        }

        // ------------------------------------------------------------ settings
        void BuildSettings()
        {
            var s = Sheet("settings", "Settings");
            var body = Body(s).contentContainer;
            soundT = Row(body, "Sound", () => { game.SetSound(!game.Save.sound); RenderSettings(); });
            vibT = Row(body, "Vibration", () => { game.SetVibration(!game.Save.vibration); RenderSettings(); });
            resetT = Row(body, "Reset progress", () =>
            {
                if (!resetArmed) { resetArmed = true; RenderToggle(resetT, "Tap again", false, true); return; }
                game.ResetProgress();
                Snack("Progress reset");
            });
            privacyRow = Row(body, "Ad privacy choices", () => game.Ads.ShowPrivacyOptions(RenderSettings));
            RenderToggle(privacyRow, "Open", false);
            var policy = Row(body, "Privacy policy", () => Application.OpenURL(PrivacyPolicyUrl));
            RenderToggle(policy, "View", false);
            adsNote = L(body, "", "note");
        }

        public const string PrivacyPolicyUrl = "https://zahidghotia.github.io/rollzy3d/privacy.html";
        Label adsNote;
        Button privacyRow;

        Button Row(VisualElement parent, string label, Action click)
        {
            var row = El(parent, "setrow");
            L(row, label, "setrow-label");
            var t = new Button(click);
            t.RemoveFromClassList(Button.ussClassName);
            t.AddToClassList("toggle");
            row.Add(t);
            return t;
        }

        static void RenderToggle(Button b, string text, bool on, bool danger = false)
        {
            b.text = text;
            b.EnableInClassList("on", on);
            b.EnableInClassList("danger", danger);
        }

        void RenderSettings()
        {
            RenderToggle(soundT, game.Save.sound ? "On" : "Off", game.Save.sound);
            RenderToggle(vibT, game.Save.vibration ? "On" : "Off", game.Save.vibration);
            resetArmed = false;
            RenderToggle(resetT, "Reset", false);
            // Only EEA/UK/CH players need the consent form entry point; hide it everywhere else.
            privacyRow.parent.style.display = game.Ads != null && game.Ads.PrivacyOptionsRequired ? DisplayStyle.Flex : DisplayStyle.None;
            adsNote.text = game.Ads != null && game.Ads.IsReal ? "Ads are served by Google AdMob." : "Test ads are shown until AdMob is enabled.";
        }

        // ------------------------------------------------------------ test ad
        void BuildAd()
        {
            adLayer = El(root, "adlayer blocker", true);
            padded.Add(adLayer);
            var head = El(adLayer, "ad-head");
            L(head, "TEST AD", "ad-badge");
            adClose = new Button();
            adClose.RemoveFromClassList(Button.ussClassName);
            adClose.AddToClassList("ad-count");
            adCountLabel = L(adClose, "5", "ad-count-label");
            head.Add(adClose);
            var creative = El(adLayer, "ad-creative");
            adKind = L(creative, "Rewarded video", "ad-kind");
            adNote = L(creative, "Watch to the end to get your reward", "ad-note");
            var bar = El(adLayer, "ad-bar");
            adFill = El(bar, "ad-fill");
            adLayer.style.display = DisplayStyle.None;
        }

        /// <summary>The in-game stand-in for a real ad (used until AdMob is enabled).</summary>
        public void ShowTestAd(bool rewarded, Action<bool> done)
        {
            AdOpen = true;
            adLayer.style.display = DisplayStyle.Flex;
            adKind.text = rewarded ? "Rewarded video" : "Interstitial";
            adNote.text = rewarded ? "Watch to the end to get your reward" : "Shown between levels";
            float length = rewarded ? 5f : 3f, started = Time.unscaledTime;
            adClose.SetEnabled(false);
            adClose.clickable = null;
            IVisualElementScheduledItem tick = null;
            tick = adLayer.schedule.Execute(() =>
            {
                float t = Time.unscaledTime - started;
                adFill.style.width = Length.Percent(Mathf.Clamp01(t / length) * 100);
                if (t < length) { adCountLabel.text = Mathf.CeilToInt(length - t).ToString(); return; }
                tick.Pause();
                adCountLabel.text = "Close";
                adClose.SetEnabled(true);
                adClose.clickable = new Clickable(() =>
                {
                    adLayer.style.display = DisplayStyle.None;
                    AdOpen = false;
                    done(true);
                });
            }).Every(50);
        }
    }
}
