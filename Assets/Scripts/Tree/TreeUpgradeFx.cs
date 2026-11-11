using UnityEngine;

namespace MoreMush
{
    // Growth effect art lives under Resources/Art/FX/Growth.
    public class TreeUpgradeFx : MonoBehaviour
    {
        public static readonly Color Gold = new Color(1f, 0.75f, 0.24f, 1f);
        public const float Duration = 0.5f;
        const string Root = "Art/FX/Growth/";
        static Sprite arrowSprite;
        static readonly Sprite[] sparkleSprites = new Sprite[3];
        readonly SpriteRenderer[] sparks = new SpriteRenderer[8];
        readonly Vector3[] positions = new Vector3[8];
        readonly float[] delays = new float[8], sizes = new float[8];
        SpriteRenderer arrow;
        float elapsed, radius;
        System.Random random;

        public static void Play(TreeNodeView node)
        {
            var pool = node.transform.parent.GetComponent<TreeUpgradeFxPool>();
            if (pool != null) pool.Enqueue(node);
        }

        public static void Prepare(TreeNodeView node)
        {
            var parent = node.transform.parent;
            if (parent.GetComponent<TreeUpgradeFxPool>() != null) return;
            parent.gameObject.AddComponent<TreeUpgradeFxPool>().Initialize(node);
        }

        internal void Initialize(TreeNodeView node)
        {
            random = new System.Random(); // Cosmetic randomness must not consume gameplay Random state.
            if (arrowSprite == null) arrowSprite = Resources.Load<Sprite>(Root + "level_up_arrow");
            arrow = Make("Arrow", arrowSprite, node.body, 20);
            for (int i = 0; i < sparks.Length; i++)
            {
                int variant = i % 3;
                if (sparkleSprites[variant] == null)
                    sparkleSprites[variant] = Resources.Load<Sprite>(Root + "sparkle_0" + (variant + 1));
                sparks[i] = Make("Sparkle", sparkleSprites[variant], node.body, 21);
            }
            gameObject.SetActive(false);
        }

        internal void Restart(TreeNodeView node)
        {
            elapsed = 0;
            radius = TreeNodeView.Radius(node.Node);
            transform.localPosition = node.transform.localPosition;
            Art.FitPx(arrow, radius * 1.8f * 1.2f * 1.5f);
            for (int i = 0; i < sparks.Length; i++)
            {
                float angle = Next() * Mathf.PI * 2f;
                float distance = radius * Mathf.Lerp(0.65f, 1.7f, Next());
                positions[i] = new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0) / Art.PPU;
                delays[i] = Mathf.Lerp(0.04f, 0.35f, Next());
                sizes[i] = Mathf.Lerp(8f, 15f, Next()) * 2f * 2f;
                sparks[i].transform.localRotation = Quaternion.Euler(0, 0, Next() * 90f);
            }
            Render(0);
            gameObject.SetActive(true);
        }

        float Next() => (float)random.NextDouble();
        SpriteRenderer Make(string name, Sprite sprite, SpriteRenderer source, int order)
        {
            var go = new GameObject(name);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder + order;
            sr.color = new Color(1, 1, 1, 0);
            return sr;
        }

        void LateUpdate()
        {
            elapsed += Time.unscaledDeltaTime;
            Render(elapsed);
            if (elapsed >= Duration) gameObject.SetActive(false);
        }

        // Compress the original 0.7-second choreography to Duration, including sparkle delays.
        void Render(float time)
        {
            float t = Mathf.Clamp01(time / Duration);
            time = t * 0.7f;
            // Slow start, fast middle, gentle stop; retain the same travel distance.
            float rise = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
            float alpha = t <= 0.8f
                ? Mathf.SmoothStep(0, 1, t / 0.8f)
                : 1f - Mathf.SmoothStep(0, 1, (t - 0.8f) / 0.2f);
            arrow.color = new Color(1, 1, 1, alpha);
            arrow.transform.localPosition = new Vector3(0, (radius * 0.15f + radius * (2.5f - 0.15f) * 0.5f * rise) / Art.PPU, -0.1f);
            for (int i = 0; i < sparks.Length; i++)
            {
                float age = (time - delays[i]) / 0.3f;
                float a = age <= 0 || age >= 1 ? 0 : Mathf.Sin(age * Mathf.PI);
                sparks[i].color = new Color(1, 1, 1, a);
                sparks[i].transform.localPosition = positions[i] + Vector3.up * Mathf.Clamp01(age) * radius * 0.35f / Art.PPU;
                Art.FitPx(sparks[i], sizes[i] * (0.6f + a * 0.4f));
            }
        }
    }

    // Bounded, scene-owned pool: clicks enqueue only; visual work runs after Update.
    internal class TreeUpgradeFxPool : MonoBehaviour
    {
        readonly TreeUpgradeFx[] effects = new TreeUpgradeFx[12];
        readonly TreeNodeView[] pending = new TreeNodeView[12];
        int count, next;

        internal void Initialize(TreeNodeView template)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                var go = new GameObject("Upgrade Feedback");
                go.layer = template.gameObject.layer;
                go.transform.SetParent(transform, false);
                effects[i] = go.AddComponent<TreeUpgradeFx>();
                effects[i].Initialize(template);
            }
        }

        internal void Enqueue(TreeNodeView node)
        {
            if (count < pending.Length) pending[count++] = node;
            else pending[pending.Length - 1] = node; // Only excess visuals are coalesced, never purchases.
        }

        void LateUpdate()
        {
            for (int i = 0; i < count; i++)
            {
                if (pending[i] != null)
                {
                    effects[next].Restart(pending[i]);
                    next = (next + 1) % effects.Length;
                }
                pending[i] = null;
            }
            count = 0;
        }

        void OnDisable() { System.Array.Clear(pending, 0, pending.Length); count = 0; }
    }
}
