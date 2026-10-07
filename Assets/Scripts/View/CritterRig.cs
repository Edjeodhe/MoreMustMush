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
        public string kind = "fire";
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
            if (eyesSprite == null) eyesSprite = eyes.GetComponent<SpriteRenderer>();
            if (mouthSprite == null) mouthSprite = mouth.GetComponent<SpriteRenderer>();
            if (capSprite == null) capSprite = cap.GetComponentInChildren<SpriteRenderer>();
            capRest = cap.localPosition; bodyRest = body.localPosition;
            footLRest = footL.localPosition; footRRest = footR.localPosition;
            blinkT = Random.Range(0.5f, blinkEvery);
        }

        public void SetKind(string id)
        {
            kind = id;
            if (shownKind == id) return;
            shownKind = id;
            var s = SpriteDB.Get("Characters/Critters/cap_" + id);
            if (s != null) capSprite.sprite = s;
        }

        // sx, sy: squash factors (1 = rest); r: prototype radius to display at
        public void Squash(float sx, float sy, float r)
        {
            float k = r / baseRadius;
            root.localScale = new Vector3(sx * k, sy * k, 1);
        }

        void Update()
        {
            if (shownKind != kind) SetKind(kind);
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
