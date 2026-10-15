using UnityEngine;

namespace MoreMush
{
    // OutGame 씬 (세계수). 세계수 월드(FarmView)를 열고 닫는 뼈대 — 성장·꼬마·꾸미기 UI는 배치되면 연결한다.
    public class OutGameController : MonoBehaviour
    {
        [Tooltip("세계수 월드 뷰. 비우면 씬에서 찾는다")]
        public FarmView farm;

        void Start()
        {
            App.EnsureSave();
            if (farm == null) farm = FindFirstObjectByType<FarmView>();
            if (farm != null) farm.Enter();
            else Debug.LogWarning("[OutGame] FarmView가 씬에 없어요 (세계수 월드를 배치하면 연결돼요)");
        }

        // 떠날 때까지의 수확을 보따리에 넣고 저장
        void OnDestroy() { if (farm != null) farm.Flush(); }

        public void ToMainGame() { Snd.Ui(); App.Go(Scenes.MainGame); }
        public void StartRound() { Snd.Ui(); RoundFlow.Request(); }
    }
}
