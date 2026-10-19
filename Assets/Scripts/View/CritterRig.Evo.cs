using System.Collections.Generic;
using UnityEngine;

namespace MoreMush
{
    // 진화 정령 리그 (2026-10-16): 각성 ★0~3 = 진화 모습 4가지 (★0 그냥 버섯 → ★3 수인형 버섯 정령).
    // 부품과 관절은 Resources/Rig/evo_rig.json (Tools/art/fit_evo_parts.py가 부품 그림에서 관절을 점으로 찾아 만든다).
    //   ★0·★1: 몸 한 장 = 갓 붙은 대 (갓을 떼지 않는다). ★1은 짧은 팔 한 덩어리씩
    //   ★2·★3: 머리(얼굴·머리카락·갓) · 몸통(옷·다리) · 팔(위팔/아래팔, 팔꿈치)
    // 계층: 몸 ─ 어깨 ─ (기울기·크기) ─ 위팔 · 팔꿈치 ─ 아래팔 / 목 ─ 머리. 먼 쪽 팔(오른팔)은 왼팔을 뒤집어 몸통 뒤에 그린다.
    // 움직임은 목표 각도를 스프링으로 따라가서 팔·팔꿈치·머리가 조금 늦게 따라오고 살짝 넘쳤다 돌아온다.
    // 누르면(pokeAt) 갓이 든 부품(★0·★1 몸, ★2·★3 머리)이 통통 튄다. 표정은 같은 캔버스의 얼굴 층만 갈아 끼운다.
    public partial class CritterRig
    {
        public int form;                         // 진화 모습 0~3 (= 각성 ★). FarmCritter가 정한다
        [System.NonSerialized] public float pokeAt = -99;   // 눌린 시각 (Time.time)

        [System.Serializable] class EvoPart { public string sprite, bare, back; public float x, y; }
        [System.Serializable] class EvoLook
        {
            public string id, kind, face, arm, armUp, armLo, hair, leg, collar;   // collar = 목 프릴 층 (공용 맨몸 틀, 팔 위에 덮어 어깨 관절을 가림)
            public int form;
            public EvoPart body, head;
            public float[] neck, shoulderL, shoulderR, armCenter, armElbow, hairPos, hipL, hipR, legCenter, collarPos;
            public float armScale, armTilt, armRest, height, legTilt, k = 1;   // k = 이 모습을 진화 줄 그림 크기에 맞추는 배율 (자랄수록 커진다)
            public bool HasHead => head != null && !string.IsNullOrEmpty(head.sprite);
            public bool HasArm => !string.IsNullOrEmpty(armUp) || !string.IsNullOrEmpty(arm);
        }
        [System.Serializable] class EvoFile { public EvoLook[] looks; }
        static Dictionary<string, EvoLook> evoLooks;

        static EvoLook EvoOf(string kind, int form)
        {
            if (evoLooks == null)
            {
                evoLooks = new Dictionary<string, EvoLook>();
                var ta = Resources.Load<TextAsset>("Rig/evo_rig");
                if (ta != null) foreach (var l in JsonUtility.FromJson<EvoFile>(ta.text).looks) evoLooks[l.id] = l;
            }
            return evoLooks.TryGetValue(kind + "_" + form, out var v) ? v : null;
        }

        const string EVO_DIR = "Characters/Evo/";
        static Sprite EvoSprite(string name) => string.IsNullOrEmpty(name) ? null : SpriteDB.Get(EVO_DIR, name);

        // 스프링: 목표를 따라가며 살짝 넘쳤다 돌아온다
        struct Spring
        {
            public float x, v;
            public void Step(float target, float k, float c, float dt)
            {
                v += ((target - x) * k - v * c) * dt;
                x += v * dt;
            }
        }

        Transform evoRoot, evoBody, evoHead, evoHeadInner;
        readonly Transform[] evoShoulder = new Transform[2], evoArmInner = new Transform[2], evoElbow = new Transform[2];
        SpriteRenderer evoBodySR, evoBodyFace, evoHeadSR, evoHeadFace, evoHairBack, evoHair, evoCollar;
        readonly Transform[] evoHip = new Transform[2];
        readonly SpriteRenderer[] evoLeg = new SpriteRenderer[2];
        Spring sLegL, sLegR;
        readonly SpriteRenderer[] evoUp = new SpriteRenderer[2], evoLo = new SpriteRenderer[2];
        EvoLook evo;
        Vector3 evoRest; float evoScale, evoHalfH;
        Spring sRaiseL, sRaiseR, sBendL, sBendR, sHead, sBody;
        float shownPoke = -99, pokeT = 99, evoBlinkT = 2;
        int shownForm = -1;
        public bool EvoOn { get; private set; }

        SpriteRenderer EvoSR(string name, Transform parent, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sharedMaterial = capSprite.sharedMaterial;
            sr.sortingLayerID = capSprite.sortingLayerID;
            sr.sortingOrder = capSprite.sortingOrder + order;
            return sr;
        }

        Transform EvoT(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        void BuildEvo()
        {
            evoRoot = EvoT("Evo", footL.parent);          // 갓·발과 같은 부모 = 배치 표 좌표계
            evoBody = EvoT("Body", evoRoot);
            evoBodySR = EvoSR("BodySprite", evoBody, 0);
            evoBodyFace = EvoSR("BodyFace", evoBodySR.transform, 1);
            for (int i = 0; i < 2; i++)
            {
                // i = 0: 보는 쪽 왼팔 · 1: 오른팔 (왼팔을 뒤집음). 그림이 거의 정면이라 두 팔 다 몸통 앞, 어깨 혹 위에 (뒤에 두면 치마에 가려 사라진다)
                evoShoulder[i] = EvoT(i == 0 ? "ShoulderL" : "ShoulderR", evoBody);
                evoArmInner[i] = EvoT("Arm", evoShoulder[i]);
                // 팔은 머리·머리카락보다 앞 (들어 올린 팔이 머리 뒤로 숨어 사라지던 것). 얼굴을 가리지 않게 휘젓는 각도는 옆쪽으로 둔다
                evoUp[i] = EvoSR("Upper", evoArmInner[i], 10);
                evoElbow[i] = EvoT("Elbow", evoArmInner[i]);
                evoLo[i] = EvoSR("Lower", evoElbow[i], 11);
            }
            evoCollar = EvoSR("Collar", evoBody, 12);           // 목 프릴: 팔(10·11)보다 앞, 몸과 같이 흔들린다
            for (int i = 0; i < 2; i++)
            {
                // 다리: 엉덩이 축(치마 밑단 안쪽)에 매달려 몸통 뒤로. 오른다리는 왼다리를 뒤집음
                evoHip[i] = EvoT(i == 0 ? "HipL" : "HipR", evoBody);
                evoLeg[i] = EvoSR("Leg", evoHip[i], -2);
            }
            evoHead = EvoT("Neck", evoRoot);
            evoHeadInner = EvoT("Bounce", evoHead);
            evoHeadSR = EvoSR("HeadSprite", evoHeadInner, 4);
            evoHeadFace = EvoSR("HeadFace", evoHeadSR.transform, 5);
            evoHairBack = EvoSR("HairBack", evoHeadInner, -4);   // (예전) 머리 그림을 턱에서 자른 뒷부분
            evoHair = EvoSR("Hair", evoHead, -6);                 // 뒷머리 부품: 몸통·팔·다리보다 뒤. 머리를 따라 돌되 통통 튀지는 않는다
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            shownSilhouette = !silhouette;   // 새 그림에도 실루엣 효과를 다시 건다
        }

        static Vector3 V(float[] a) => a != null && a.Length >= 2 ? new Vector3(a[0], a[1], 0) : Vector3.zero;

        // 진화 모습이 배치 표와 그림에 다 있으면 리그를 세우고 true. 없으면 예전 방식(통짜·부품)으로
        bool ApplyEvo(string id)
        {
            var l = EvoOf(id, form);
            // ★2·★3 몸통은 bare가 빈칸. 작업복(outfit: pick·climb·carry)이 있으면 몸통·목 프릴을 그 복장으로 (공용 맨몸 틀, kit_preview.py)
            string fit = string.IsNullOrEmpty(outfit) ? "" : "_" + outfit;
            var bodySp = l?.body == null ? null : EvoSprite(l.body.bare) ?? (fit != "" ? EvoSprite(l.body.sprite + fit) : null) ?? EvoSprite(l.body.sprite);
            var lay = Layout(id);
            EvoOn = bodySp != null && lay != null;
            if (!EvoOn) { if (evoRoot != null) evoRoot.gameObject.SetActive(false); return false; }
            if (evoRoot == null) BuildEvo();
            evo = l;
            evoRoot.gameObject.SetActive(true);
            SetPartsActive(false);
            if (whole != null) whole.gameObject.SetActive(false);

            // 크기: 부품 꼬마와 같은 키(갓 꼭대기~발바닥)를 ★1 모습에 맞추고, 다른 모습은 그림 크기 비율대로 (자랄수록 커진다)
            LayoutSpan(id, out float bottom, out float top);
            var refL = EvoOf(id, 1) ?? l;
            evoScale = (top - bottom) / Mathf.Max(0.01f, refL.height * refL.k) * l.k;   // 부품 단위 → 배치 표 단위
            evoHalfH = l.height * 0.5f * evoScale;
            evoRoot.localPosition = evoRest = new Vector3(0, bottom, 0);
            evoRoot.localScale = new Vector3(evoScale, evoScale, 1);

            // 몸: ★2·★3은 목을 축으로 흔들리고(꼬리·치마가 진자처럼), ★0·★1은 발바닥 가운데를 축으로 통통 튄다
            bool head = l.HasHead;
            var pivot = head ? V(l.neck) : Vector3.zero;
            evoBody.localPosition = pivot;
            evoBodySR.sprite = bodySp;
            evoBodySR.transform.localPosition = new Vector3(l.body.x, l.body.y, 0) - pivot;
            evoBodyFace.sprite = head ? null : EvoSprite(l.face + "normal");
            evoBodyFace.transform.localPosition = Vector3.zero;

            evoCollar.sprite = (fit != "" && !string.IsNullOrEmpty(l.collar) ? EvoSprite(l.collar + fit) : null) ?? EvoSprite(l.collar);
            evoCollar.transform.localPosition = V(l.collarPos) - pivot;

            // 팔
            for (int i = 0; i < 2; i++)
            {
                evoShoulder[i].gameObject.SetActive(l.HasArm);
                if (!l.HasArm) continue;
                var sh = V(i == 0 ? l.shoulderL : l.shoulderR) - pivot;
                evoShoulder[i].localPosition = sh;
                evoShoulder[i].localScale = new Vector3(i == 0 ? 1 : -1, 1, 1);
                evoArmInner[i].localScale = new Vector3(l.armScale, l.armScale, 1);
                bool split = !string.IsNullOrEmpty(l.armUp);
                evoUp[i].sprite = EvoSprite(split ? l.armUp : l.arm);
                evoUp[i].transform.localPosition = V(l.armCenter);
                evoElbow[i].localPosition = V(l.armElbow);
                evoLo[i].sprite = split ? EvoSprite(l.armLo) : null;
                evoLo[i].transform.localPosition = V(l.armCenter) - V(l.armElbow);
            }

            // 다리
            bool legs = !string.IsNullOrEmpty(l.leg);
            for (int i = 0; i < 2; i++)
            {
                evoHip[i].gameObject.SetActive(legs);
                if (!legs) continue;
                evoHip[i].localPosition = V(i == 0 ? l.hipL : l.hipR) - pivot;
                evoHip[i].localScale = new Vector3(i == 0 ? 1 : -1, 1, 1);
                evoLeg[i].sprite = EvoSprite(l.leg);
                evoLeg[i].transform.localPosition = V(l.legCenter);
            }

            // 머리 (★2·★3): 목에 붙고, 갓 통통은 목을 축으로 머리를 눌렀다 늘인다
            evoHead.gameObject.SetActive(head);
            if (head)
            {
                evoHead.localPosition = V(l.neck);
                evoHeadSR.sprite = EvoSprite(l.head.bare) ?? EvoSprite(l.head.sprite);
                evoHeadSR.transform.localPosition = new Vector3(l.head.x, l.head.y, 0) - V(l.neck);
                evoHeadFace.sprite = EvoSprite(l.face + "normal");
                evoHeadFace.transform.localPosition = Vector3.zero;
                evoHairBack.sprite = EvoSprite(l.head.back);
                evoHairBack.transform.localPosition = evoHeadSR.transform.localPosition;
                evoHair.sprite = EvoSprite(l.hair);
                evoHair.transform.localPosition = V(l.hairPos) - V(l.neck);
            }
            shownForm = form;
            return true;
        }

        // 지금 동작에 맞는 표정 (얼굴 층 이름 끝)
        string EvoFace(bool blink, bool fidgetShut)
        {
            float sincePoke = Time.time - pokeAt;
            switch (motion)
            {
                case "held": return (int)(Time.time * 2.2f) % 3 == 2 ? "effort" : "surprise";   // 버둥대며 놀람·안간힘
                case "fall": return "surprise";
                case "climb": return blink ? "blink" : "effort";
            }
            if (sincePoke < 0.25f) return "surprise";
            if (sincePoke < 1.1f) return "happy";
            switch (motion)
            {
                case "work": return blink ? "blink" : "effort";
                case "carry": return blink ? "blink" : "tired";
                case "heart": return "love";
                case "surprise": case "spin": return "surprise";
                case "jump": case "dance": return "happy";
                case "sleep": return "blink";
            }
            if (fidget == Fidget.Wave || fidget == Fidget.Hop) return "happy";   // 웃으며 손 흔들기 · 폴짝
            return blink || fidgetShut ? "blink" : "normal";
        }

        void AnimateEvo(float t)
        {
            var l = evo;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float raiseL = 8, raiseR = 8, bendL = 14, bendR = 14, headRot = 0, bodyRot = 0, lean = 0, bob = 0, stretch = 0, hop = 0;
            float a0 = 0, a1 = 0, capRot = 0, flail = 0; bool shut = false;
            float legL = 0, legR = 0;                                // 다리 앞뒤 흔들기 (도, + = 바깥/앞으로)
            float liftL = 0, liftR = 0, foreL = 1, foreR = 1;        // 걷기: 발 들기 (키 비율) · 앞으로 나간 팔이 짧아 보이는 정도 (원근)
            bool walking = false;
            if (motion != "idle") fidget = Fidget.None;
            switch (motion)
            {
                case "walk":
                {
                    // 정면 그림의 걷기 (2026-10-16 사용자: 팔이 건들건들 → 사실적으로. NKK 사람 리그 참고):
                    // 팔·다리는 반대 위상, 몸은 한 걸음에 두 번 통통 (다리가 엇갈릴 때 가장 높이). 정면이라 앞뒤 흔들기는 옆으로 벌리지 않고
                    // 앞으로 나간 팔 = 팔꿈치 살짝 굽힘 + 짧아 보임, 앞으로 나간 다리 = 발을 살짝 든다. 옆 흔들림은 아주 작게
                    walking = true;
                    float ph = t * 8.5f + phase, s = Mathf.Sin(ph), fwdL = Mathf.Max(0, s), fwdR = Mathf.Max(0, -s);
                    raiseL = 7 + s * 4; raiseR = 7 - s * 4;
                    bendL = 10 + fwdL * 22; bendR = 10 + fwdR * 22;
                    foreL = 1 - fwdL * 0.12f; foreR = 1 - fwdR * 0.12f;
                    liftL = fwdR * 0.035f; liftR = fwdL * 0.035f;        // 왼팔이 앞일 때 오른발이 앞으로 (대각선)
                    legL = -fwdR * 4; legR = -fwdL * 4;                  // 든 발은 살짝 안쪽으로 (몸 밑으로 디딤)
                    bodyRot = s * 1.5f; headRot = -s * 1.2f;
                    bob = Mathf.Abs(Mathf.Cos(ph)) * 0.1f * evoHalfH;   // 키의 약 5%
                    break;
                }
                case "carry":
                    raiseL = raiseR = 32; bendL = bendR = 115;               // 짐끈을 쥔 손
                    lean = -6 + Mathf.Sin(t * 9) * 2; headRot = -4; bob = Mathf.Abs(Mathf.Cos(t * 9)) * 0.03f;
                    break;
                case "work":
                {
                    float w = t * 12;
                    raiseL = 60 + Mathf.Sin(w) * 40; bendL = 50 + Mathf.Sin(w + 1) * 35;   // 번갈아 손을 뻗어 딴다
                    raiseR = 60 + Mathf.Sin(w + Mathf.PI) * 40; bendR = 50 + Mathf.Sin(w + Mathf.PI + 1) * 35;
                    headRot = Mathf.Sin(w) * 4; bodyRot = Mathf.Sin(w) * 2;
                    break;
                }
                case "jump": case "spin": case "surprise":
                    raiseL = raiseR = 150 + Mathf.Sin(t * 20) * 8; bendL = bendR = 15; break;
                case "dance":
                {
                    float d = Mathf.Sin(t * 12);
                    raiseL = 100 + d * 50; raiseR = 100 - d * 50; bendL = 40 + d * 30; bendR = 40 - d * 30;
                    bodyRot = d * 8; headRot = d * 10;
                    legL = Mathf.Max(0, d) * 28; legR = Mathf.Max(0, -d) * 28;
                    break;
                }
                case "held":
                {
                    // 버둥버둥: 팔을 번갈아 휘젓고 몸을 비튼다 (두 팔이 엇갈리게, 팔꿈치는 반 박자 늦게)
                    float w = t * 17;
                    raiseL = 72 + Mathf.Sin(w) * 45; raiseR = 72 + Mathf.Sin(w + 2.4f) * 45;
                    bendL = 35 + Mathf.Sin(w + 1.3f) * 40; bendR = 35 + Mathf.Sin(w + 3.7f) * 40;
                    bodyRot = Mathf.Sin(t * 11) * 9; headRot = Mathf.Sin(t * 13 + 1) * 10;
                    flail = 0.07f * Mathf.Sin(t * 21);           // 팔 없는 ★0도 꿈틀꿈틀
                    legL = 18 + Mathf.Sin(t * 19) * 32; legR = 18 + Mathf.Sin(t * 19 + 2.1f) * 32;   // 발을 동동 구른다
                    break;
                }
                case "fall":
                    raiseL = raiseR = 165 + Mathf.Sin(t * 30) * 6; bendL = bendR = 10; headRot = Mathf.Sin(t * 25) * 4;
                    legL = legR = 22; break;
                case "climb":
                {
                    // 줄기를 타고 오른다: 한 손씩 위로 뻗어 잡고 끌어올림
                    float c = Mathf.Sin(t * 8);
                    raiseL = 150 + c * 25; raiseR = 150 - c * 25;
                    bendL = 30 + Mathf.Max(0, -c) * 70; bendR = 30 + Mathf.Max(0, c) * 70;
                    bodyRot = c * 4; headRot = -c * 3; bob = Mathf.Abs(c) * 0.03f;
                    flail = 0.03f * c;
                    legL = 10 + Mathf.Max(0, c) * 35; legR = 10 + Mathf.Max(0, -c) * 35;   // 손과 엇갈려 발을 딛는다
                    break;
                }
                case "heart":
                    raiseL = raiseR = 35; bendL = bendR = 125; headRot = Mathf.Sin(t * 6) * 6; break;   // 가슴 앞에 모은 손
                case "sleep":
                    raiseL = raiseR = 3; bendL = bendR = 8; headRot = 14 + Mathf.Sin(t * 1.2f) * 2; bodyRot = 4; break;
                default:
                {
                    // 숨쉬기 + 가끔 하는 짧은 행동 (손 흔들기·기지개·두리번·폴짝·하품)
                    float br = Mathf.Sin(t * 2.1f);
                    a0 = 8 + br * 3; a1 = 8 - br * 3;
                    Fidgets(ref a0, ref a1, ref capRot, ref stretch, ref hop, ref lean, ref shut);
                    raiseL = a0; raiseR = a1; headRot = capRot * 0.8f + Mathf.Sin(t * 1.1f + 0.5f) * 2;
                    bendL = 14 + br * 4 + (fidget == Fidget.Wave && fidgetSide < 0 ? 30 + Mathf.Sin(fidgetT * 16) * 25 : 0);
                    bendR = 14 - br * 4 + (fidget == Fidget.Wave && fidgetSide > 0 ? 30 + Mathf.Sin(fidgetT * 16) * 25 : 0);
                    if (fidget == Fidget.Yawn) bendL = 130;          // 하품: 손을 입으로
                    bodyRot = Mathf.Sin(t * 1.3f) * 2; bob = br * 0.015f;
                    break;
                }
            }
            // 스프링: 어깨·머리는 단단하게, 팔꿈치는 조금 늦게 (따라 흔들림)
            // 감쇠 c = 2√k 면 넘치지 않고 따라온다 (걷기·대기·나르기: 팔이 출렁이지 않게). 버둥·춤처럼 일부러 흔드는 동작만 덜 감쇠
            float damp = walking || motion == "idle" || motion == "carry" ? 1f : 0.62f;
            sRaiseL.Step(raiseL, 260, 32 * damp, dt); sRaiseR.Step(raiseR, 260, 32 * damp, dt);
            sBendL.Step(bendL, 200, 28 * damp, dt); sBendR.Step(bendR, 200, 28 * damp, dt);
            sBody.Step(bodyRot + lean, 160, 25 * damp, dt);
            sHead.Step(headRot, 120, 22 * damp, dt);
            sLegL.Step(legL, 220, 30 * damp, dt); sLegR.Step(legR, 220, 30 * damp, dt);

            // 갓 통통: 누른 순간부터 줄어드는 떨림 (가로로 퍼지면 세로로 눌린다)
            if (pokeAt != shownPoke) { shownPoke = pokeAt; pokeT = 0; }
            pokeT += dt;
            float jig = (pokeT < 1.2f ? 0.2f * Mathf.Exp(-5f * pokeT) * Mathf.Sin(pokeT * 26) : 0) + flail;

            evoRoot.localPosition = evoRest + new Vector3(0, hop + bob + evoHalfH * stretch, 0);
            evoRoot.localScale = new Vector3(evoScale * (1 - stretch * 0.6f), evoScale * (1 + stretch), 1);
            evoBody.localRotation = Quaternion.Euler(0, 0, sBody.x);
            bool head = l.HasHead;
            if (!head) evoBody.localScale = new Vector3(1 + jig, 1 - jig, 1);   // ★0·★1: 갓 붙은 몸이 발바닥 축으로 통통
            else
            {
                evoBody.localScale = Vector3.one;
                evoHead.localRotation = Quaternion.Euler(0, 0, sBody.x * 0.5f + sHead.x);
                evoHeadInner.localScale = new Vector3(1 + jig, 1 - jig, 1);     // ★2·★3: 갓 쓴 머리가 목 축으로 통통
            }
            if (!string.IsNullOrEmpty(l.leg))
            {
                // 왼다리: 바깥(앞)으로 = 시계 방향. 오른다리는 뒤집힌 공간이라 같은 값이 거울처럼
                evoHip[0].localRotation = Quaternion.Euler(0, 0, l.legTilt - sLegL.x - sBody.x);
                evoHip[1].localRotation = Quaternion.Euler(0, 0, l.legTilt - sLegR.x + sBody.x);   // 몸이 기울어도 발은 땅을 향한다
                evoLeg[0].transform.localPosition = V(l.legCenter) + new Vector3(0, liftL * l.height, 0);
                evoLeg[1].transform.localPosition = V(l.legCenter) + new Vector3(0, liftR * l.height, 0);
            }
            if (l.HasArm)
            {
                // 왼팔: 바깥으로 벌림 = 시계 방향. 오른팔은 뒤집힌 공간이라 같은 값이 거울처럼 움직인다
                evoArmInner[0].localRotation = Quaternion.Euler(0, 0, l.armTilt - l.armRest - sRaiseL.x);
                evoArmInner[1].localRotation = Quaternion.Euler(0, 0, l.armTilt - l.armRest - sRaiseR.x);
                evoElbow[0].localRotation = Quaternion.Euler(0, 0, sBendL.x);
                evoElbow[1].localRotation = Quaternion.Euler(0, 0, sBendR.x);
                evoArmInner[0].localScale = new Vector3(l.armScale, l.armScale * foreL, 1);   // 앞으로 나간 팔은 짧아 보인다
                evoArmInner[1].localScale = new Vector3(l.armScale, l.armScale * foreR, 1);
            }

            evoBlinkT -= dt;
            bool blink = evoBlinkT < 0.12f;
            if (evoBlinkT <= 0) evoBlinkT = blinkEvery * Random.Range(0.7f, 1.3f);
            var face = EvoSprite(l.face + EvoFace(blink, shut)) ?? EvoSprite(l.face + "normal");
            var fsr = head ? evoHeadFace : evoBodyFace;
            if (fsr.sprite != face) fsr.sprite = face;
        }
    }
}
