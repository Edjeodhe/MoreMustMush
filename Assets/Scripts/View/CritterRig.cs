using UnityEngine;

namespace MoreMush
{
    // Cut-out rig for the special mushroom critters (10 kinds share one body; the cap carries the kind).
    // Joint layout lives in the hierarchy (edit it there); this script swaps sprites and animates around
    // the authored rest pose: idle bob, cap wobble, blinking, hop squash.
    public class CritterRig : MonoBehaviour
    {
        public Transform root;                  // squash/scale pivot (feet line)
        public Transform body, cap, eyes, mouth, blush, footL, footR;
        public SpriteRenderer capSprite, eyesSprite, mouthSprite;
        public SpriteRenderer acc, hat;         // skin accessory and job hat (chef · straw), children of the cap
        public string kind = "fire";
        public string skin, hatId;              // worn skin id and hat id (null = none)
        public float baseRadius = 50;           // prototype critter radius the authored pose was built for
        public bool idle = true;
        public float blinkEvery = 3.2f;
        public bool silhouette;

        Vector3 capRest, bodyRest, footLRest, footRRest;
        float blinkT;
        string shownKind;

        SpriteRenderer[] renderers;
        bool shownSilhouette;

        void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            capRest = cap.localPosition; bodyRest = body.localPosition;
            footLRest = footL.localPosition; footRRest = footR.localPosition;
            blinkT = Random.Range(0.5f, blinkEvery);
        }

        public void SetKind(string id)
        {
            kind = id;
            string key = id + "|" + skin + "|" + hatId;
            if (shownKind == key) return;
            shownKind = key;
            Defs.SKIN.TryGetValue(skin ?? "", out var sk);
            var s = SpriteDB.Get("Characters/Critters/" + (sk != null ? "skin_" : "cap_") + id);
            if (s != null) capSprite.sprite = s;
            Place(acc, sk?.acc);
            Place(hat, hatId);
        }

        // Skin + hat for this critter (farm, skin shop, field). Null to take them off.
        public void SetLook(string id, string skinId, string hatName)
        {
            skin = skinId; hatId = hatName;
            SetKind(id);
        }

        // Puts an accessory sprite where Defs.ACC_PLACE says (rig units from the critter center), relative to the cap.
        void Place(SpriteRenderer sr, string name)
        {
            if (sr == null) return;
            sr.gameObject.SetActive(name != null);
            if (name == null) return;
            var sp = SpriteDB.Get("Characters/Critters/Acc/" + name);
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
            if (shownKind != kind + "|" + skin + "|" + hatId) SetKind(kind);
            if (shownSilhouette != silhouette)
            {
                shownSilhouette = silhouette;
                foreach (var sr in renderers) Art.SetFx(sr, 0, silhouette ? 1 : 0);
            }
            if (!idle) return;
            float t = Time.time;
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
    }
}
