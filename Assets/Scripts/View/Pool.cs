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

        public int Count => items.Count;

        // 꺼진 채로 하나 더 만든다 (라운드 중 한꺼번에 Instantiate하지 않게 미리 채울 때)
        public void Grow()
        {
            var it = Object.Instantiate(prefab, parent);
            it.gameObject.name = prefab.name + " " + items.Count;
            it.gameObject.SetActive(false);
            items.Add(it);
        }

        // 씬을 떠날 때 항목을 씬 밖(holder)으로 옮겨 넘긴다. 다음 씬의 같은 풀이 Adopt로 다시 쓴다
        public List<T> Detach(Transform holder)
        {
            var list = new List<T>(items.Count);
            foreach (var it in items)
            {
                if (it == null) continue;
                if (it.gameObject.activeSelf) it.gameObject.SetActive(false);
                it.transform.SetParent(holder, false);
                list.Add(it);
            }
            items.Clear(); used = 0;
            return list;
        }

        public void Adopt(List<T> list)
        {
            if (list == null) return;
            foreach (var it in list)
            {
                if (it == null) continue;
                it.transform.SetParent(parent, false);
                items.Add(it);
            }
        }
    }
}
