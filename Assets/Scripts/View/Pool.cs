using System.Collections.Generic;
using UnityEngine;

namespace MoreMush
{
    // Index-based pool: item i follows entity i of a simulation list each frame, the rest are hidden.
    public class Pool<T> where T : Component
    {
        readonly T prefab;
        readonly Transform parent;
        readonly List<T> items = new List<T>();
        int used;

        public Pool(T prefab, Transform parent) { this.prefab = prefab; this.parent = parent; }

        public T Get(int i)
        {
            while (items.Count <= i)
            {
                var it = Object.Instantiate(prefab, parent);
                it.gameObject.name = prefab.name + " " + items.Count;
                items.Add(it);
            }
            var x = items[i];
            if (!x.gameObject.activeSelf) x.gameObject.SetActive(true);
            if (i + 1 > used) used = i + 1;
            return x;
        }

        // Hide every item from index n on.
        public void Trim(int n)
        {
            for (int i = n; i < used && i < items.Count; i++) if (items[i].gameObject.activeSelf) items[i].gameObject.SetActive(false);
            used = n;
        }
    }
}
