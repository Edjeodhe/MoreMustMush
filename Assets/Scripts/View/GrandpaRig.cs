using UnityEngine;

namespace MoreMush
{
    // Cut-out rig for the veteran mushroom-hunter grandpa (title, mycelium tree center, ending).
    // Joints are authored in the hierarchy (the sleeves are part of the torso, so the arms hinge at the elbows);
    // this script animates around that rest pose: breathing, scythe arm sway, free arm wave, hat bob,
    // blinking, and sparkly eyes when `sparkle` is on.
    public class GrandpaRig : MonoBehaviour
    {
        public Transform pelvis, torso, head, hat, lowerArmBack, lowerArmFront, legL, legR;
        public SpriteRenderer eyes;
        public bool sparkle;
        public float speed = 1;

        Quaternion labRest, lafRest, headRest;
        Vector3 torsoRest, headPosRest, hatRest;
        float blinkT = 2;

        void Awake()
        {
            torsoRest = torso.localPosition; headPosRest = head.localPosition; hatRest = hat.localPosition;
            labRest = lowerArmBack.localRotation; lafRest = lowerArmFront.localRotation;
            headRest = head.localRotation;
        }

        void Update()
        {
            float t = Time.time * speed;
            float breathe = Mathf.Sin(t * 2f);
            torso.localPosition = torsoRest + new Vector3(0, breathe * 0.012f, 0);
            head.localPosition = headPosRest + new Vector3(0, breathe * 0.008f, 0);
            head.localRotation = headRest * Quaternion.Euler(0, 0, Mathf.Sin(t * 0.9f) * 3);
            hat.localPosition = hatRest + new Vector3(0, Mathf.Max(0, Mathf.Sin(t * 2f - 0.4f)) * 0.012f, 0);
            lowerArmBack.localRotation = labRest * Quaternion.Euler(0, 0, Mathf.Sin(t * 1.3f) * 2);
            lowerArmFront.localRotation = lafRest * Quaternion.Euler(0, 0, Mathf.Sin(t * 3.4f) * 10 + (sparkle ? 12 : 0));

            blinkT -= Time.deltaTime;
            string e = sparkle ? "eyes_sparkle" : blinkT < 0.12f ? "eyes_closed" : "eyes_open";
            var s = SpriteDB.Get("Characters/Grandpa/" + e);
            if (s != null && eyes.sprite != s) eyes.sprite = s;
            if (blinkT <= 0) blinkT = Random.Range(2.5f, 4.5f);
        }
    }
}
