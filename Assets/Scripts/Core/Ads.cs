using System;
using UnityEngine;
#if UNITY_ANDROID || UNITY_IOS
using GoogleMobileAds.Api;
#endif

namespace MoreMush
{
    // 보상형 광고 (AdMob). App.Awake가 Init을 부르고, 쓰는 쪽은 RewardedReady를 보고 ShowRewarded를 부른다.
    // 광고는 안드로이드·iOS에서만 나온다 — WebGL·PC에서는 RewardedReady가 늘 false, ShowRewarded는 바로 실패로 돌린다.
    // 개발 빌드(Development Build)·에디터는 구글 테스트 광고만 쓴다. 진짜 광고 단위 ID로 테스트하면 계정이 막힐 수 있다.
    public static class Ads
    {
        // AdMob 콘솔에서 만든 진짜 광고 단위 ID (출시 전에 채운다. 비어 있으면 테스트 광고로 돈다)
        const string REWARDED_ANDROID = "";
        const string REWARDED_IOS = "";

        // 구글이 공개한 테스트 광고 단위 ID
        const string TEST_REWARDED_ANDROID = "ca-app-pub-3940256099942544/5224354917";
        const string TEST_REWARDED_IOS = "ca-app-pub-3940256099942544/1712485313";

        static readonly float[] RETRY_SEC = { 5, 15, 30, 60 };   // 불러오기 실패 뒤 다시 시도할 간격 (마지막 값을 계속 쓴다)

        // 보상형 광고를 지금 바로 보여줄 수 있나 (버튼 켜고 끄기에 쓴다)
        public static bool RewardedReady
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS
                return rewarded != null && rewarded.CanShowAd() && !showing;
#else
                return false;
#endif
            }
        }

        // 준비 상태가 바뀌면 알린다 (광고 버튼이 구독해서 켜고 끈다. OnDisable/OnDestroy에서 꼭 -=)
        public static event Action ReadyChanged;

#if UNITY_ANDROID || UNITY_IOS
        static bool inited, loading, showing;
        static int retry;
        static RewardedAd rewarded;

        static string RewardedId
        {
            get
            {
#if UNITY_ANDROID
                string real = REWARDED_ANDROID, test = TEST_REWARDED_ANDROID;
#else
                string real = REWARDED_IOS, test = TEST_REWARDED_IOS;
#endif
                return Debug.isDebugBuild || string.IsNullOrEmpty(real) ? test : real;
            }
        }
#endif

        // 도메인 리로드를 끈 플레이 모드에서도 이전 플레이의 광고·구독을 들고 있지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ReadyChanged = null;
#if UNITY_ANDROID || UNITY_IOS
            inited = loading = showing = false; retry = 0; rewarded = null;
#endif
        }

        public static void Init()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (inited) return;
            inited = true;
            MobileAds.RaiseAdEventsOnUnityMainThread = true;   // 광고 콜백이 메인 스레드로 와야 게임 상태·UI를 만질 수 있다
            MobileAds.Initialize(_ => LoadRewarded());
#endif
        }

        // 보상형 광고를 보여준다. 끝까지 보고 닫으면 onReward, 못 보여줬거나 중간에 닫으면 onFail (둘 다 메인 스레드, 광고가 닫힌 뒤)
        public static void ShowRewarded(Action onReward, Action onFail = null)
        {
#if UNITY_ANDROID || UNITY_IOS
            if (!RewardedReady) { Fail(onFail); return; }
            var ad = rewarded;
            rewarded = null;
            showing = true;
            bool earned = false;
            AudioListener.pause = true;   // 안드로이드는 광고 중에도 게임 소리가 난다

            ad.OnAdFullScreenContentClosed += () => Finish(ad, earned ? onReward : onFail);
            ad.OnAdFullScreenContentFailed += e =>
            {
                Debug.LogWarning("[Ads] 보상형 광고 표시 실패: " + e.GetMessage());
                Finish(ad, onFail);
            };
            try { ad.Show(_ => earned = true); }
            catch (Exception e) { Debug.LogException(e); Finish(ad, onFail); return; }   // 닫힘 이벤트가 안 오니 여기서 풀어 준다 (소리·showing이 묶이지 않게)
            ReadyChanged?.Invoke();
#else
            Fail(onFail);
#endif
        }

        static void Fail(Action onFail)
        {
            try { onFail?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

#if UNITY_ANDROID || UNITY_IOS
        static void Finish(RewardedAd ad, Action then)
        {
            if (!showing) return;   // 닫힘·실패가 둘 다 오는 경우 한 번만
            showing = false;
            AudioListener.pause = false;
            ad.Destroy();
            try { then?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            LoadRewarded();
        }

        static void LoadRewarded()
        {
            if (loading || rewarded != null) return;
            loading = true;
            RewardedAd.Load(RewardedId, new AdRequest(), (ad, error) =>
            {
                loading = false;
                if (error != null || ad == null)
                {
                    float wait = RETRY_SEC[Mathf.Min(retry++, RETRY_SEC.Length - 1)];
                    Debug.LogWarning($"[Ads] 보상형 광고 불러오기 실패 ({wait}초 뒤 다시): {error?.GetMessage()}");
                    if (App.I != null) App.I.StartCoroutine(RetryLater(wait));
                    return;
                }
                retry = 0;
                rewarded = ad;
                ReadyChanged?.Invoke();
            });
        }

        static System.Collections.IEnumerator RetryLater(float sec)
        {
            yield return new WaitForSecondsRealtime(sec);
            LoadRewarded();
        }
#endif
    }
}
