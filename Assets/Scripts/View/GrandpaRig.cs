using UnityEngine;

namespace MoreMush
{
    // Rig for the veteran mushroom-hunter grandpa (title, mycelium tree center), cut from the reference
    // illustration by Tools/art/build_grandpa_rig.py: body, scythe (fist + handle + blade) hinged at the wrist,
    // a head cover that keeps the handle tucked behind the beard and hat brim, and a closed-eye patch.
    // This script animates around the rest pose: breathing, a slow scythe sway and blinking.
    public class GrandpaRig : MonoBehaviour
    {
        public Transform body;                  // feet pivot: breathing squash
        public Transform scythe;                // wrist joint
        public SpriteRenderer eyes;             // closed-eye patch, shown while blinking
        public float speed = 1;

        const float SWAY = 1.5f;                // scythe sway (degrees); the cut parts stay seamless up to about 2

        Vector3 bodyRest;
        Quaternion scytheRest;
        float blinkT = 2;

        void Awake()
        {
            bodyRest = body.localScale;
            scytheRest = scythe.localRotation;
        }

        void Update()
        {
            float t = Time.time * speed;
            float breathe = Mathf.Sin(t * 2f);
            body.localScale = new Vector3(bodyRest.x * (1 - breathe * 0.006f), bodyRest.y * (1 + breathe * 0.012f), bodyRest.z);
            scythe.localRotation = scytheRest * Quaternion.Euler(0, 0, Mathf.Sin(t * 1.3f) * SWAY);

            blinkT -= Time.deltaTime;
            eyes.enabled = blinkT < 0.12f;
            if (blinkT <= 0) blinkT = Random.Range(2.5f, 4.5f);
        }
    }
}
