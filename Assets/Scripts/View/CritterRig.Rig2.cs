using UnityEngine;

namespace MoreMush
{
    // 리그2 연결 (2026-10-19): spirit_rig2.json에 그 진화 모습(form = 각성 ★)이 있으면 새 리그로 그린다 (가장 먼저).
    //   ★2·★3 사람형 = SpiritRig2 · ★0·★1 버섯 = MushRig. 두 리그는 이 꼬마의 템플릿 슬롯(Root/Rig2 · Root/Mush, MCP로 만든 것)이고,
    //   리그 참조가 비어 있는 곳(Stage 꼬마 프리팹 등)은 예전 진화 리그(CritterRig.Evo)를 그대로 쓴다.
    // 크기 (2026-10-19 사용자: 같은 등급 정령은 키가 같게): 등급(★)마다 한 키로 맞춘다.
    //   등급 키 = 그 등급 모든 모습이 예전 방식(부품 꼬마 키를 ★1에 맞추고 그림 비율 k)으로 그려지던 키의 평균 → 등급끼리 크기 차이는 예전과 같고, 같은 등급은 같은 키.
    // 움직임은 리그 안에서 다 한다 (FarmCritter는 Rig2On이면 몸 통통·기울기를 얹지 않는다).
    public partial class CritterRig
    {
        [Header("리그2 (템플릿 슬롯)")]
        public SpiritRig2 rig2;
        public MushRig mushRig;
        [System.NonSerialized] public string rig2Motion;   // 리그2 동작 이름 (FarmView.RigMotion: 사다리·줄기·폴짝·착지까지). 없으면 motion
        public bool Rig2On { get; private set; }
        bool rig2Headed;
        string rig2Want; float rig2Poke = -99;

        // 진화 리그와 같은 키 맞춤: 배치 표(critter_rig.json)의 갓 꼭대기 ~ 발바닥 (배치 표 단위)
        // 등급 키 (배치 표 단위, 갓 꼭대기~발바닥): 예전 방식 키의 등급 평균. 처음 한 번 계산
        static System.Collections.Generic.Dictionary<int, float> gradeSpan;
        static float GradeSpan(int form)
        {
            if (gradeSpan == null)
            {
                gradeSpan = new System.Collections.Generic.Dictionary<int, float>();
                var sum = new System.Collections.Generic.Dictionary<int, float>(); var n = new System.Collections.Generic.Dictionary<int, int>();
                foreach (var l in Rig2Data.All())
                {
                    if (!LayoutSpanOf(l.kind, out float b, out float t)) continue;
                    var refL = Rig2Data.Look(l.kind + "_1") ?? l;
                    float h = (t - b) / Mathf.Max(0.01f, refL.height * refL.k) * l.k * l.height;   // 예전 방식으로 그려지던 키
                    sum.TryGetValue(l.form, out float s0); n.TryGetValue(l.form, out int n0);
                    sum[l.form] = s0 + h; n[l.form] = n0 + 1;
                }
                foreach (var kv in sum) gradeSpan[kv.Key] = kv.Value / n[kv.Key];
            }
            return gradeSpan.TryGetValue(form, out float v) ? v : 0;
        }

        static bool LayoutSpanOf(string id, out float bottom, out float top)
        {
            var lay = Layout(id);
            bottom = top = 0;
            if (lay == null) return false;
            var capSp = SpriteDB.Get("Characters/Critters/cap_", id); var footSp = SpriteDB.Get("Characters/Critters/foot_", id);
            top = lay.cap.y + (capSp != null ? capSp.bounds.max.y * lay.cap.s : 0.5f);
            bottom = lay.foot.y + (footSp != null ? footSp.bounds.min.y * lay.foot.s : -0.5f);
            return true;
        }

        bool LayoutSpan(string id, out float bottom, out float top)
        {
            var lay = Layout(id);
            bottom = top = 0;
            if (lay == null) return false;
            var capSp = SpriteDB.Get("Characters/Critters/cap_", id); var footSp = SpriteDB.Get("Characters/Critters/foot_", id);
            top = lay.cap.y + (capSp != null ? capSp.bounds.max.y * lay.cap.s : 0.5f);
            bottom = lay.foot.y + (footSp != null ? footSp.bounds.min.y * lay.foot.s : -0.5f);
            return true;
        }

        bool ApplyRig2(string id)
        {
            var l = rig2 != null || mushRig != null ? Rig2Data.Look(id + "_" + form) : null;
            bool headed = l != null && l.form >= 2;
            Rig2On = l != null && (headed ? rig2 != null : mushRig != null) && LayoutSpan(id, out _, out _);
            if (rig2 != null) rig2.gameObject.SetActive(Rig2On && headed);
            if (mushRig != null) mushRig.gameObject.SetActive(Rig2On && !headed);
            if (!Rig2On) return false;
            SetPartsActive(false);
            if (whole != null) whole.gameObject.SetActive(false);
            if (evoRoot != null) evoRoot.gameObject.SetActive(false);
            EvoOn = false;

            LayoutSpan(id, out float bottom, out float top);
            float g = GradeSpan(l.form);
            var refL = Rig2Data.Look(id + "_1") ?? l;
            // ★2·★3(사람형): 몸통이 공용 틀이고 머리는 불 정령 얼굴 크기에 맞춰 놓았다 → 모두 불 정령(fire_★)과 같은 배율 (사용자: 크기·비율을 불에 맞춤)
            var refH = l.form >= 2 ? Rig2Data.Look("fire_" + l.form) ?? l : l;
            float s = g > 0 ? g / Mathf.Max(0.01f, refH.height)                                // 리그 단위 → 배치 표 단위 (등급 키 / 기준 키)
                : (top - bottom) / Mathf.Max(0.01f, refL.height * refL.k) * l.k;              // (등급 키를 못 구하면 예전 방식)
            var holder = headed ? rig2.transform : mushRig.transform;
            holder.localPosition = new Vector3(0, bottom, 0);
            holder.localScale = new Vector3(s, s, 1);
            int order = capSprite.sortingOrder;
            if (headed) { rig2.baseOrder = order; rig2.Apply(l, outfit); }
            else { mushRig.baseOrder = order; mushRig.Apply(l, outfit); }
            if (headed != rig2Headed) rig2Want = null;   // 다른 리그로 바뀌면 동작을 다시 건다
            rig2Headed = headed;
            shownForm = form;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            shownSilhouette = !silhouette;   // 새 그림에도 실루엣 효과를 다시 건다
            return true;
        }

        void AnimateRig2()
        {
            string want = string.IsNullOrEmpty(rig2Motion) ? motion : rig2Motion;
            bool poked = pokeAt != rig2Poke; rig2Poke = pokeAt;
            float dt = Time.deltaTime;
            if (rig2Headed)
            {
                if (want != rig2Want) { rig2Want = want; rig2.Play(want); }
                if (poked) rig2.Poke();
                // 웹 작업장과 같이: 걷는 동안만 이동 속도를 준다 (머리카락이 뒤로 날린다)
                rig2.vel = new Vector2((rig2.motion == "walk" || rig2.motion == "carry") ? rig2.walkInfo.speed * rig2.flip : 0, 0);
                rig2.Step(dt);
            }
            else
            {
                if (want != rig2Want) { rig2Want = want; mushRig.Play(want); }
                if (poked) mushRig.Poke();
                mushRig.vel = new Vector2((mushRig.motion == "walk" || mushRig.motion == "carry") ? mushRig.walkInfo.speed * mushRig.flip : 0, 0);
                mushRig.Step(dt);
            }
        }
    }
}
