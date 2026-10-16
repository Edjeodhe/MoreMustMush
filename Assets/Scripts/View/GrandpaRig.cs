using UnityEngine;

namespace MoreMush
{
    // Cut-out rig for the veteran mushroom-hunter grandpa (title, mycelium tree center).
    // Doodle set (2026-10-15 4th pass): separately drawn parts (Characters/Grandpa/Parts, laid out by
    // Tools/art/build_grandpa_rig_doodle.py) on joints — hips, shoulders, neck, chin (beard), hat and wrist (scythe).
    // Older sets only have body + scythe + blink patch; every joint field is optional.
    // Animates around the rest pose: breathing, head bob and tilt, beard and hat follow-through, scythe sway,
    // a wave of the free hand every few seconds (mouth opens), weight shift on the legs, blinking.
    public class GrandpaRig : MonoBehaviour
    {
        public Transform body;                  // feet pivot: breathing squash
        public Transform scythe;                // wrist joint
        public SpriteRenderer eyes;             // closed-eye patch, shown while blinking
        [Header("Joints (doodle parts)")]
        public Transform neck, beard, hat, armBack, armFront, legBack, legFront;
        public SpriteRenderer mouth;            // open mouth, shown while waving
        public float speed = 1;
        public float waveEvery = 7;

        const float SWAY = 1.5f;                // legacy scythe sway (degrees); the cut parts stay seamless up to about 2

        Vector3 bodyRest;
        Quaternion scytheRest, neckRest, beardRest, hatRest, armBackRest, armFrontRest, legBackRest, legFrontRest;
        float blinkT = 2, waveT;

        void Awake()
        {
            bodyRest = body.localScale;
            scytheRest = scythe.localRotation;
            neckRest = Rest(neck); beardRest = Rest(beard); hatRest = Rest(hat);
            armBackRest = Rest(armBack); armFrontRest = Rest(armFront); legBackRest = Rest(legBack); legFrontRest = Rest(legFront);
            waveT = waveEvery * 0.4f;
        }

        static Quaternion Rest(Transform t) => t != null ? t.localRotation : Quaternion.identity;
        static void Rot(Transform t, Quaternion rest, float deg) { if (t != null) t.localRotation = rest * Quaternion.Euler(0, 0, deg); }

        void Update()
        {
            float t = Time.time * speed;
            float breathe = Mathf.Sin(t * 2f);
            body.localScale = new Vector3(bodyRest.x * (1 - breathe * 0.006f), bodyRest.y * (1 + breathe * 0.012f), bodyRest.z);

            bool rigged = neck != null;
            if (!rigged)
            {
                scythe.localRotation = scytheRest * Quaternion.Euler(0, 0, Mathf.Sin(t * 1.3f) * SWAY);
            }
            else
            {
                // wave: three quick swings of the free hand, then rest
                waveT -= Time.deltaTime * speed;
                if (waveT < -1.6f) waveT = waveEvery * Random.Range(0.8f, 1.2f);
                float w = waveT < 0 ? Mathf.Clamp01(Mathf.Min(-waveT * 4, (1.6f + waveT) * 4)) : 0;   // 0..1 envelope
                float head = Mathf.Sin(t * 2f + 0.5f);
                Rot(neck, neckRest, head * 2.5f + w * 4);
                Rot(beard, beardRest, -Mathf.Sin(t * 2f + 0.1f) * 3f - w * 3);
                Rot(hat, hatRest, Mathf.Sin(t * 2f + 1.1f) * 2f + w * 3);
                Rot(armBack, armBackRest, Mathf.Sin(t * 1.6f) * 4 + w * (-115 + Mathf.Sin(t * 14) * 22));
                Rot(armFront, armFrontRest, Mathf.Sin(t * 1.3f + 0.4f) * 3);
                Rot(scythe, scytheRest, Mathf.Sin(t * 1.3f) * 4);
                Rot(legBack, legBackRest, Mathf.Sin(t * 1f) * 1.5f);
                Rot(legFront, legFrontRest, -Mathf.Sin(t * 1f) * 1.5f);
                if (mouth != null) mouth.enabled = w > 0.3f;
            }

            blinkT -= Time.deltaTime;
            eyes.enabled = blinkT < 0.12f;
            if (blinkT <= 0) blinkT = Random.Range(2.5f, 4.5f);
        }
    }
}
