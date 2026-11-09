using System;
using UnityEngine;
#if ROLLZY_ADMOB
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

namespace Rollzy
{
    public interface IAdProvider
    {
        bool IsReal { get; }
        void Init();
        /// <summary>done(true) only when the player watched to the end and earned the reward.</summary>
        void ShowRewarded(Action<bool> done);
        void ShowInterstitial(Action done);
        /// <summary>True when the player must be able to change ad consent from Settings (UMP rule for EEA/UK/CH).</summary>
        bool PrivacyOptionsRequired { get; }
        void ShowPrivacyOptions(Action done);
    }

    /// <summary>
    /// Shows an in-game "TEST AD" screen with a countdown. Used until the Google Mobile Ads plugin is
    /// imported and enabled (Rollzy 3D → 3. Enable AdMob).
    /// </summary>
    public sealed class TestAdProvider : IAdProvider
    {
        readonly Action<bool, Action<bool>> showOverlay; // (isRewarded, onClosed)

        public TestAdProvider(Action<bool, Action<bool>> showOverlay) { this.showOverlay = showOverlay; }

        public bool IsReal => false;
        public void Init() { }
        public void ShowRewarded(Action<bool> done) => showOverlay(true, done);
        public void ShowInterstitial(Action done) => showOverlay(false, _ => done());
        public bool PrivacyOptionsRequired => false;
        public void ShowPrivacyOptions(Action done) => done();
    }

#if ROLLZY_ADMOB
    /// <summary>
    /// Google AdMob with the UMP consent flow (required for EEA/UK/Switzerland players).
    /// The IDs below are Google's public TEST units. Replace them with your own before release,
    /// and set your AdMob App ID in Assets → Google Mobile Ads → Settings.
    /// </summary>
    public sealed class AdMobProvider : IAdProvider
    {
#if UNITY_IPHONE
        const string RewardedId = "ca-app-pub-3940256099942544/1712485313";
        const string InterstitialId = "ca-app-pub-3940256099942544/4411468910";
#else
        const string RewardedId = "ca-app-pub-3940256099942544/5224354917";
        const string InterstitialId = "ca-app-pub-3940256099942544/1033173712";
#endif

        RewardedAd rewarded;
        InterstitialAd interstitial;
        bool started;

        public bool IsReal => true;

        public void Init()
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            var request = new ConsentRequestParameters();
            ConsentInformation.Update(request, (FormError updateError) =>
            {
                if (updateError != null)
                {
                    Debug.LogWarning("[Rollzy] Consent update failed: " + updateError.Message);
                    StartSdk();
                    return;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired((FormError formError) =>
                {
                    if (formError != null) Debug.LogWarning("[Rollzy] Consent form: " + formError.Message);
                    StartSdk();
                });
            });
        }

        void StartSdk()
        {
            if (started || !ConsentInformation.CanRequestAds()) return;
            started = true;
            MobileAds.Initialize(_ => { LoadRewarded(); LoadInterstitial(); });
        }

        void LoadRewarded()
        {
            RewardedAd.Load(RewardedId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null) { Debug.Log("[Rollzy] Rewarded load failed: " + error); return; }
                rewarded = ad;
            });
        }

        void LoadInterstitial()
        {
            InterstitialAd.Load(InterstitialId, new AdRequest(), (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null) { Debug.Log("[Rollzy] Interstitial load failed: " + error); return; }
                interstitial = ad;
            });
        }

        public void ShowRewarded(Action<bool> done)
        {
            if (rewarded == null || !rewarded.CanShowAd())
            {
                if (started) LoadRewarded();
                done(false);
                return;
            }
            var ad = rewarded;
            rewarded = null;
            bool earned = false;
            ad.OnAdFullScreenContentClosed += () => { ad.Destroy(); LoadRewarded(); done(earned); };
            ad.OnAdFullScreenContentFailed += (AdError e) => { ad.Destroy(); LoadRewarded(); done(false); };
            ad.Show((Reward reward) => earned = true);
        }

        public void ShowInterstitial(Action done)
        {
            if (interstitial == null || !interstitial.CanShowAd())
            {
                if (started) LoadInterstitial();
                done();
                return;
            }
            var ad = interstitial;
            interstitial = null;
            ad.OnAdFullScreenContentClosed += () => { ad.Destroy(); LoadInterstitial(); done(); };
            ad.OnAdFullScreenContentFailed += (AdError e) => { ad.Destroy(); LoadInterstitial(); done(); };
            ad.Show();
        }

        public bool PrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public void ShowPrivacyOptions(Action done)
        {
            ConsentForm.ShowPrivacyOptionsForm((FormError error) =>
            {
                if (error != null) Debug.LogWarning("[Rollzy] Privacy options: " + error.Message);
                StartSdk(); // consent may now allow ads
                done();
            });
        }
    }
#endif
}
