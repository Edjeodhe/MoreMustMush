using System.Collections.Generic;
using UnityEngine;

namespace MoreMush
{
    // Cut-out rig for the special mushroom critters.
    // Per-kind sets (Doodle, Dream 2026-10-15): every kind has its own parts — cap, body with face (Dream: a round
    // cylinder that goes into a socket cut in the cap's gills, plus the same body with the eyes closed; Doodle: a blink
    // patch), one arm blob in front of the body and one foot blob (each mirrored for the other side) — laid out per kind
    // by Resources/Rig/critter_rig.json (Tools/art/build_critter_rig.py stacks the parts by the body structure).
    // Older art sets share one body and swap only the cap; their pose is the one authored in the hierarchy.
    // Animates around the rest pose: cap wobble, arm swing, stepping feet, blinking, idle fidgets, hop squash.
    // 통짜 그림 (2026-10-16): Characters/Kids/<id>[_pick|_climb|_carry|_skin]가 있으면 부품을 모두 끄고 갓이 붙은 한 장만 그린다.
    // 크기는 그 꼬마의 부품 배치(갓 꼭대기~발바닥)에 맞추고, 움직임은 그 한 장을 기울이고 늘이고 띄운다. 복장(outfit)은 FarmCritter가 정한다.
    // 진화 정령 (2026-10-16): Resources/Rig/evo_rig.json에 그 진화 모습(form = 각성 ★)이 있으면 관절 리그로 그린다 (CritterRig.Evo.cs, 가장 먼저).
    public partial class CritterRig : MonoBehaviour
    {
        public Transform root;                  // squash/scale pivot (feet line)
        public Transform body, cap, eyes, mouth, blush, footL, footR;
        public SpriteRenderer capSprite, eyesSprite, mouthSprite;
        public SpriteRenderer acc, hat;         // skin accessory and job hat (chef · straw), children of the cap
        [Header("Per-kind parts (doodle set)")]
        public SpriteRenderer bodySprite, blinkSprite;
        public Transform armL, armR;            // shoulder joints; their child sprite hangs from the joint
        public SpriteRenderer armLSprite, armRSprite, footLSprite, footRSprite;
        public string kind = "fire";
        public string skin, hatId;              // worn skin id and hat id (null = none)
        public string outfit;                   // 일할 때 복장: pick · climb · carry (null = 평상복·스킨)
        public string motion = "idle";          // idle · walk · work · jump · spin · dance · heart · sleep · surprise (FarmCritter sets it)
        public float baseRadius = 50;           // prototype critter radius the authored pose was built for
        public bool idle = true;
        public float blinkEvery = 3.2f;
        public bool silhouette;

        Vector3 capRest, bodyRest, bodyPartRest, bodyPartScale, armLRest, armRRest, footLRest, footRRest;
        float blinkT, phase;
        // idle fidgets: every few seconds one short action (wave · stretch · look · hop · yawn)
        enum Fidget { None, Wave, Stretch, Look, Hop, Yawn }
        Fidget fidget;
        float fidgetT, fidgetLen, fidgetSide = 1, bodyHalfH;
        Sprite bodyOpen, bodyClosed;
        string shownKind, shownSkin, shownHat, shownOutfit;   // 지금 그려진 모습 (문자열을 이어 붙여 비교하지 않고 칸별로 비교)
        SpriteRenderer whole;                    // 통짜 그림 (없으면 만들고, 쓰지 않을 땐 끈다)
        Vector3 wholeRest; float wholeScale, wholeHalfH;
        public bool WholeOn { get; private set; }
        bool shownAny, perKind;

        SpriteRenderer[] renderers;
        bool shownSilhouette;

        void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            capRest = cap.localPosition; bodyRest = body.localPosition;
            footLRest = footL.localPosition; footRRest = footR.localPosition;
            blinkT = Random.Range(0.5f, blinkEvery);
            phase = Random.Range(0f, 10f);
            fidgetT = -Random.Range(1.5f, 6f);
        }

        public void SetKind(string id)
        {
            kind = id;
            if (!LookDirty) return;
            shownAny = true; shownKind = id; shownSkin = skin; shownHat = hatId; shownOutfit = outfit;
            SetPartsActive(true);
            Defs.SKIN.TryGetValue(skin ?? "", out var sk);
            var s = SpriteDB.Get(sk != null ? "Characters/Critters/skin_" : "Characters/Critters/cap_", id);
            if (s != null) capSprite.sprite = s;
            ApplyParts(id);
            Place(acc, sk?.acc);
            Place(hat, hatId);
            if (ApplyEvo(id)) return;              // 진화 정령 리그 (CritterRig.Evo.cs)
            ApplyWhole(id, sk != null);
        }

        bool LookDirty => !shownAny || shownKind != kind || shownSkin != skin || shownHat != hatId || shownOutfit != outfit || shownForm != form;

        // ===== 통짜 그림 =====
        static string WholeKey(string id, string outfit, bool skinned) => "Characters/Kids/" + id + (outfit != null ? "_" + outfit : skinned ? "_skin" : "");

        void ApplyWhole(string id, bool skinned)
        {
            var sp = SpriteDB.Get(WholeKey(id, outfit, skinned)) ?? SpriteDB.Get(WholeKey(id, null, skinned)) ?? SpriteDB.Get(WholeKey(id, null, false));
            var l = Layout(id);
            WholeOn = sp != null && l != null;
            if (!WholeOn) { if (whole != null) whole.enabled = false; return; }
            if (whole == null)
            {
                whole = new GameObject("Whole").AddComponent<SpriteRenderer>();
                whole.transform.SetParent(footL.parent, false);   // 갓·발과 같은 부모 = 배치 표 좌표계
                whole.sharedMaterial = capSprite.sharedMaterial;
                whole.sortingLayerID = capSprite.sortingLayerID;
                whole.sortingOrder = capSprite.sortingOrder;
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
                shownSilhouette = !silhouette;   // 새 그림에도 실루엣 효과를 다시 건다
            }
            whole.sprite = sp; whole.enabled = true;
            // 부품 꼬마와 같은 키: 갓 꼭대기 ~ 발바닥 (배치 표 rig 단위)
            var capSp = SpriteDB.Get("Characters/Critters/cap_", id); var footSp = SpriteDB.Get("Characters/Critters/foot_", id);
            float top = l.cap.y + (capSp != null ? capSp.bounds.max.y * l.cap.s : 0.5f);
            float bottom = l.foot.y + (footSp != null ? footSp.bounds.min.y * l.foot.s : -0.5f);
            wholeScale = (top - bottom) / sp.bounds.size.y;
            wholeHalfH = sp.bounds.extents.y * wholeScale;
            wholeRest = new Vector3(0, bottom - sp.bounds.min.y * wholeScale, 0);
            whole.transform.localPosition = wholeRest;
            whole.transform.localScale = new Vector3(wholeScale, wholeScale, 1);
            SetPartsActive(false);
        }

        void SetPartsActive(bool on)
        {
            foreach (var t in new[] { cap, body, armL, armR, footL, footR, bodySprite != null ? bodySprite.transform : null }) if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);   // bodySprite: 진화 리그일 때 남아 보이던 예전 몸 (Root/BodyPart)
            if (!on && whole != null) whole.gameObject.SetActive(true);
            if (on && whole != null) whole.enabled = false;
        }

        // Skin + hat for this critter (farm, skin shop, field). Null to take them off.
        public void SetLook(string id, string skinId, string hatName)
        {
            skin = skinId; hatId = hatName;
            SetKind(id);
        }

        // ===== per-kind parts =====
        [System.Serializable] public class Part { public float x, y, s; }
        [System.Serializable] public class KindLayout { public string id; public Part cap, body, blink, arm, foot; public float shoulderX, shoulderY, capJointY; }
        [System.Serializable] class LayoutFile { public KindLayout[] kinds; }
        static Dictionary<string, KindLayout> layouts;

        public static KindLayout Layout(string id)
        {
            if (layouts == null)
            {
                layouts = new Dictionary<string, KindLayout>();
                var ta = Resources.Load<TextAsset>("Rig/critter_rig");
                if (ta != null) foreach (var k in JsonUtility.FromJson<LayoutFile>(ta.text).kinds) layouts[k.id] = k;
            }
            return layouts.TryGetValue(id, out var l) ? l : null;
        }

        void ApplyParts(string id)
        {
            var l = Layout(id);
            // a skin that wears its accessory on the body (scarf, sunglasses) swaps the whole body part: body_<id>_skin
            string bodyId = skin != null && SpriteDB.Get("Characters/Critters/body_", id + "_skin") != null ? id + "_skin" : id;
            var bodySp = SpriteDB.Get("Characters/Critters/body_", bodyId);
            perKind = l != null && bodySp != null && bodySprite != null;
            // the shared-body parts of the older sets
            foreach (var t in new[] { eyes, mouth, blush }) if (t != null) t.gameObject.SetActive(!perKind);
            var legacyBody = body.GetComponent<SpriteRenderer>();
            if (legacyBody != null && legacyBody != bodySprite) legacyBody.enabled = !perKind;
            foreach (var sr in new[] { bodySprite, blinkSprite, armLSprite, armRSprite }) if (sr != null) sr.gameObject.SetActive(perKind);
            if (!perKind) return;

            bodySprite.sprite = bodyOpen = bodySp;
            Set(bodySprite.transform, l.body, 1);
            bodyPartRest = bodySprite.transform.localPosition;
            bodyPartScale = bodySprite.transform.localScale;
            // the skin body may sit on a wider canvas (accessory overhang): measure the base body so the squash pivot stays
            bodyHalfH = (SpriteDB.Get("Characters/Critters/body_", id) ?? bodySp).bounds.extents.y * l.body.s;
            // eyes closed: a second whole body drawn with the eyes shut (aligned to the open one) is swapped in;
            // older sets lay a blink patch, a child of the body part, over the face instead
            bodyClosed = SpriteDB.Get("Characters/Critters/body_", bodyId + "_closed");
            blinkSprite.sprite = bodyClosed != null ? null : SpriteDB.Get("Characters/Critters/blink_", id);
            blinkSprite.transform.localPosition = new Vector3((l.blink.x - l.body.x) / l.body.s, (l.blink.y - l.body.y) / l.body.s, 0);
            blinkSprite.transform.localScale = new Vector3(l.blink.s / l.body.s, l.blink.s / l.body.s, 1);
            blinkSprite.enabled = false;

            // cap joint sits on the rim so the wobble pivots there
            cap.localPosition = capRest = new Vector3(l.cap.x, l.capJointY, 0);
            capSprite.transform.localPosition = new Vector3(0, l.cap.y - l.capJointY, 0);
            capSprite.transform.localScale = new Vector3(l.cap.s, l.cap.s, 1);

            // arm: layout gives the LEFT arm sprite center and its shoulder joint; the right arm mirrors it
            var armSp = SpriteDB.Get("Characters/Critters/arm_", id);
            armLSprite.sprite = armRSprite.sprite = armSp;
            // stubby arms in front of the cylinder body (user pick 2026-10-15), under the cap
            armLSprite.sortingOrder = armRSprite.sortingOrder = bodySprite.sortingOrder + 1;
            var off = new Vector3(l.arm.x - l.shoulderX, l.arm.y - l.shoulderY, 0);
            armL.localPosition = new Vector3(l.shoulderX, l.shoulderY, 0);
            armR.localPosition = new Vector3(-l.shoulderX, l.shoulderY, 0);
            armLRest = armL.localPosition; armRRest = armR.localPosition;
            armLSprite.transform.localPosition = off;
            armRSprite.transform.localPosition = new Vector3(-off.x, off.y, 0);
            armLSprite.transform.localScale = new Vector3(l.arm.s, l.arm.s, 1);
            armRSprite.transform.localScale = new Vector3(-l.arm.s, l.arm.s, 1);

            var footSp = SpriteDB.Get("Characters/Critters/foot_", id);
            footLSprite.sprite = footRSprite.sprite = footSp;
            footL.localPosition = footLRest = new Vector3(-Mathf.Abs(l.foot.x), l.foot.y, 0);
            footR.localPosition = footRRest = new Vector3(Mathf.Abs(l.foot.x), l.foot.y, 0);
            footL.localScale = new Vector3(l.foot.s, l.foot.s, 1);
            footR.localScale = new Vector3(-l.foot.s, l.foot.s, 1);
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        static void Set(Transform t, Part p, float sx)
        {
            t.localPosition = new Vector3(p.x, p.y, 0);
            t.localScale = new Vector3(p.s * sx, p.s, 1);
        }

        // Puts an accessory sprite where Defs.ACC_PLACE says (rig units from the critter center), relative to the cap.
        void Place(SpriteRenderer sr, string name)
        {
            if (sr == null) return;
            sr.gameObject.SetActive(name != null);
            if (name == null) return;
            var sp = SpriteDB.Get("Characters/Critters/Acc/", name);
            sr.sprite = sp;
            if (sp == null || !Defs.ACC_PLACE.TryGetValue(name, out var p)) return;
            float s = p.w / sp.bounds.size.x;
            sr.transform.localPosition = new Vector3(p.x, p.y - capRest.y, 0);
            sr.transform.localScale = new Vector3(s, s, 1);
            sr.sortingOrder = capSprite.sortingOrder + (p.back ? -1 : 1);
        }

        // sx, sy: squash factors (1 = rest); r: prototype radius to display at
        public void Squash(float sx, float sy, float r)
        {
            float k = r / baseRadius;
            root.localScale = new Vector3(sx * k, sy * k, 1);
        }

        void Update()
        {
            if (LookDirty) SetKind(kind);
            if (shownSilhouette != silhouette)
            {
                shownSilhouette = silhouette;
                foreach (var sr in renderers) Art.SetFx(sr, 0, silhouette ? 1 : 0);
            }
            if (!idle) return;
            float t = Time.time + phase;
            if (EvoOn) { AnimateEvo(t); return; }
            if (WholeOn) { AnimateWhole(t); return; }
            if (perKind) { Animate(t); return; }

            body.localPosition = bodyRest + new Vector3(0, Mathf.Sin(t * 4) * 0.02f, 0);
            cap.localPosition = capRest + new Vector3(0, Mathf.Sin(t * 4 + 0.6f) * 0.03f, 0);
            cap.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 2.3f) * 4);
            footL.localPosition = footLRest + new Vector3(0, Mathf.Max(0, Mathf.Sin(t * 8)) * 0.03f, 0);
            footR.localPosition = footRRest + new Vector3(0, Mathf.Max(0, -Mathf.Sin(t * 8)) * 0.03f, 0);
            blinkT -= Time.deltaTime;
            bool closed = blinkT < 0.12f;
            var want = SpriteDB.Get(closed ? "Characters/Critters/eyes_closed" : "Characters/Critters/eyes_open");
            if (want != null && eyesSprite.sprite != want) eyesSprite.sprite = want;
            if (blinkT <= 0) blinkT = blinkEvery * Random.Range(0.7f, 1.3f);
        }

        // Per-kind motion: angles in degrees, arm angle 0 = hanging down, positive = raised outward.
        // stretch > 0 makes the body part taller and thinner around the feet line (idle fidgets only).
        void Animate(float t)
        {
            float bob = 0, capRot = Mathf.Sin(t * 2.3f) * 4, capLift = 0, armL0 = 8 + Mathf.Sin(t * 3) * 6, armR0 = 8 + Mathf.Sin(t * 3 + 1.2f) * 6;
            float stepL = 0, stepR = 0, strideL = 0, strideR = 0, stretch = 0, hop = 0, lean = 0;
            bool eyesShut = false;
            if (motion != "idle") fidget = Fidget.None;
            switch (motion)
            {
                case "walk":
                {
                    float w = t * 11, s = Mathf.Sin(w);
                    bob = Mathf.Abs(Mathf.Cos(w)) * 0.02f;
                    armL0 = 14 + s * 30; armR0 = 14 - s * 30;
                    stepL = Mathf.Max(0, s) * 0.05f; stepR = Mathf.Max(0, -s) * 0.05f;
                    strideL = s * 0.03f; strideR = -s * 0.03f;
                    capRot = Mathf.Sin(w * 2) * 5; capLift = Mathf.Abs(Mathf.Sin(w)) * 0.015f;
                    lean = s * 2.5f;
                    break;
                }
                case "work":
                {
                    float w = t * 14;
                    armL0 = 70 + Mathf.Sin(w) * 35; armR0 = 70 + Mathf.Sin(w + Mathf.PI) * 35;
                    capRot = Mathf.Sin(w) * 6; bob = Mathf.Abs(Mathf.Sin(w)) * 0.015f;
                    break;
                }
                case "jump": case "spin": case "surprise":
                    armL0 = armR0 = 140 + Mathf.Sin(t * 20) * 10; capLift = 0.03f; break;
                case "dance":
                    armL0 = 90 + Mathf.Sin(t * 14) * 60; armR0 = 90 - Mathf.Sin(t * 14) * 60; capRot = Mathf.Sin(t * 14) * 10; break;
                case "heart":
                    armL0 = armR0 = 40 + Mathf.Sin(t * 16) * 10; eyesShut = true; break;
                case "sleep":
                    armL0 = armR0 = 4; capRot = Mathf.Sin(t * 1.2f) * 3; eyesShut = true; break;
                default:
                    // breathing and the walk hop are whole-body squash in FarmCritter; the rig adds part motion only
                    Fidgets(ref armL0, ref armR0, ref capRot, ref stretch, ref hop, ref lean, ref eyesShut);
                    break;
            }
            // body stretches from the feet line: its center rises by half the extra height, the cap by all of it
            float grow = bodyHalfH * stretch;
            bodySprite.transform.localPosition = bodyPartRest + new Vector3(0, bob + hop + grow, 0);
            bodySprite.transform.localScale = new Vector3(bodyPartScale.x * (1 - stretch * 0.6f), bodyPartScale.y * (1 + stretch), 1);
            bodySprite.transform.localRotation = Quaternion.Euler(0, 0, lean);
            cap.localPosition = capRest + new Vector3(-lean * 0.004f, bob + hop + grow * 2 + capLift + Mathf.Sin(t * 4 + 0.6f) * 0.008f, 0);
            cap.localRotation = Quaternion.Euler(0, 0, capRot + lean);
            armL.localPosition = armLRest + new Vector3(0, bob + hop + grow, 0);
            armR.localPosition = armRRest + new Vector3(0, bob + hop + grow, 0);
            armL.localRotation = Quaternion.Euler(0, 0, -armL0);
            armR.localRotation = Quaternion.Euler(0, 0, armR0);
            footL.localPosition = footLRest + new Vector3(strideL, stepL + hop, 0);
            footR.localPosition = footRRest + new Vector3(strideR, stepR + hop, 0);

            blinkT -= Time.deltaTime;
            bool shut = eyesShut || blinkT < 0.12f;
            if (bodyClosed != null) { var want = shut ? bodyClosed : bodyOpen; if (bodySprite.sprite != want) bodySprite.sprite = want; }
            else blinkSprite.enabled = shut;
            if (blinkT <= 0) blinkT = blinkEvery * Random.Range(0.7f, 1.3f);
        }

        // 통짜 그림 움직임: 한 장을 기울이고(lean) 발바닥 기준으로 늘이고(stretch) 띄운다(hop). 통통 튀기·눌리기는 FarmCritter가 한다
        void AnimateWhole(float t)
        {
            float lean = 0, stretch = 0, hop = 0, a0 = 0, a1 = 0, rot = 0; bool shut = false;
            if (motion != "idle") fidget = Fidget.None;
            switch (motion)
            {
                case "walk": lean = Mathf.Sin(t * 11) * 3; break;
                case "carry": lean = -6 + Mathf.Sin(t * 11) * 2; stretch = -0.03f; break;   // 짐을 지고 앞으로 숙인다
                case "work": lean = Mathf.Sin(t * 14) * 5; stretch = Mathf.Sin(t * 28) * 0.03f; break;
                case "dance": lean = Mathf.Sin(t * 14) * 10; break;
                case "sleep": lean = 6 + Mathf.Sin(t * 1.2f) * 2; break;
                case "jump": case "spin": case "surprise": case "heart": break;
                default: Fidgets(ref a0, ref a1, ref rot, ref stretch, ref hop, ref lean, ref shut); lean += rot * 0.4f; break;
            }
            whole.transform.localPosition = wholeRest + new Vector3(0, hop + wholeHalfH * stretch, 0);
            whole.transform.localScale = new Vector3(wholeScale * (1 - stretch * 0.6f), wholeScale * (1 + stretch), 1);
            whole.transform.localRotation = Quaternion.Euler(0, 0, lean);
        }

        // One short idle action every few seconds; e = 0 → 1 → 0 envelope so it eases in and out of the rest pose.
        void Fidgets(ref float armL0, ref float armR0, ref float capRot, ref float stretch, ref float hop, ref float lean, ref bool eyesShut)
        {
            fidgetT += Time.deltaTime;
            if (fidget == Fidget.None)
            {
                if (fidgetT < 0) return;
                fidget = (Fidget)Random.Range(1, 6);
                fidgetLen = fidget == Fidget.Hop ? 0.55f : fidget == Fidget.Wave ? 1.4f : 1.2f;
                fidgetSide = Random.value < 0.5f ? -1 : 1;
                fidgetT = 0;
            }
            float p = Mathf.Clamp01(fidgetT / fidgetLen), e = Mathf.Sin(p * Mathf.PI);
            e = e * e * (3 - 2 * e);
            switch (fidget)
            {
                case Fidget.Wave:
                    if (fidgetSide > 0) armR0 = Mathf.Lerp(armR0, 135 + Mathf.Sin(fidgetT * 16) * 22, e);
                    else armL0 = Mathf.Lerp(armL0, 135 + Mathf.Sin(fidgetT * 16) * 22, e);
                    capRot += -fidgetSide * 6 * e; lean = -fidgetSide * 2 * e;
                    break;
                case Fidget.Stretch:
                    armL0 = Mathf.Lerp(armL0, 160, e); armR0 = Mathf.Lerp(armR0, 160, e);
                    stretch += 0.07f * e; eyesShut = e > 0.4f;
                    break;
                case Fidget.Look:
                    lean = fidgetSide * 5 * Mathf.Sin(p * Mathf.PI * 2) * Mathf.Sin(p * Mathf.PI);
                    capRot += lean * 1.6f;
                    break;
                case Fidget.Hop:
                    // crouch → jump → land squash
                    hop = Mathf.Max(0, Mathf.Sin((p - 0.2f) / 0.6f * Mathf.PI)) * (p > 0.2f && p < 0.8f ? 0.09f : 0);
                    stretch += p < 0.2f ? -0.08f * Mathf.Sin(p / 0.2f * Mathf.PI) : p > 0.8f ? -0.06f * Mathf.Sin((p - 0.8f) / 0.2f * Mathf.PI) : 0.05f * e;
                    armL0 = armR0 = Mathf.Lerp(armL0, 70, hop / 0.09f);
                    break;
                case Fidget.Yawn:
                    stretch += 0.03f * e; capRot += Mathf.Sin(p * Mathf.PI * 2) * 3; eyesShut = e > 0.3f;
                    armL0 = Mathf.Lerp(armL0, 40, e);
                    break;
            }
            if (p >= 1) { fidget = Fidget.None; fidgetT = -Random.Range(3f, 7f); }
        }
    }
}
