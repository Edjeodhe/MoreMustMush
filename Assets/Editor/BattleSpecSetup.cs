using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;

namespace MoreMush.EditorTools
{
    // 전투 기획서 Ver05 반영에 필요한 씬 작업 (다시 실행해도 같은 결과):
    // 트리 노드 뷰를 Defs.NODES에 맞추기, 보스 도전 버튼, 공방 수확기 카드(약초 괭이)·지운 날씨 줄 정리.
    public static class BattleSpecSetup
    {
        [MenuItem("MoreMush/전투 기획 Ver05/씬 맞추기")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[BattleSpecSetup] 플레이 모드를 끄고 실행하세요"); return; }
            if (Find<TreeView>() == null || Find<TreeScreen>() == null || Find<WorkshopPanel>() == null)
            { Debug.LogError("[BattleSpecSetup] Game 씬을 열고 실행하세요"); return; }
            var log = new List<string>();
            SyncTreeNodes(log);
            BossButton(log);
            Workshop(log);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[BattleSpecSetup]\n" + string.Join("\n", log));
        }

        static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        // ===== 트리 노드 =====
        static void SyncTreeNodes(List<string> log)
        {
            var tree = Find<TreeView>();
            var views = tree.content.GetComponentsInChildren<TreeNodeView>(true).ToList();
            foreach (var v in views.Where(v => !NODE.ContainsKey(v.nodeId)).ToList())
            {
                log.Add("노드 삭제: " + v.nodeId);
                views.Remove(v);
                Object.DestroyImmediate(v.gameObject);
            }
            var byId = views.ToDictionary(v => v.nodeId);
            var hubs = TreeLayout.Hubs();
            // 부모가 먼저 놓이도록 정의 순서대로
            foreach (var n in NODES)
            {
                if (byId.ContainsKey(n.id)) continue;
                var tpl = views.First(v => v.Node.sub == n.sub);
                var go = Object.Instantiate(tpl.gameObject, tpl.transform.parent);
                go.name = "Node " + n.id;
                var nv = go.GetComponent<TreeNodeView>();
                nv.nodeId = n.id;
                Vector2 parent = n.parent != null && byId.TryGetValue(n.parent, out var pv) ? pv.TreePos : hubs[n.br];
                var p = FreeSpot(parent, n, views);
                go.transform.localPosition = new Vector3(p.x / Art.PPU, -p.y / Art.PPU, tpl.transform.localPosition.z);
                views.Add(nv); byId[n.id] = nv;
                log.Add($"노드 추가: {n.id} ({p.x:F0}, {p.y:F0})");
            }
        }

        // 부모에서 바깥쪽(가운데에서 멀어지는 쪽)으로, 다른 노드와 겹치지 않는 가장 가까운 자리
        static Vector2 FreeSpot(Vector2 parent, Node n, List<TreeNodeView> views)
        {
            Vector2 outDir = parent.sqrMagnitude > 1 ? parent.normalized : Vector2.up;
            float baseA = Mathf.Atan2(outDir.y, outDir.x);
            for (float d = n.sub ? 100 : 130; d < 700; d += 20)
                for (int k = 0; k <= 12; k++)
                    foreach (int sgn in new[] { 1, -1 })
                    {
                        float a = baseA + sgn * k * 12 * Mathf.Deg2Rad;
                        var p = parent + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                        if (p.magnitude < 220) continue;
                        bool ok = true;
                        foreach (var v in views)
                        {
                            float need = n.sub && v.Node.sub ? 96 : n.sub || v.Node.sub ? 110 : 128;
                            if ((v.TreePos - p).sqrMagnitude < need * need) { ok = false; break; }
                        }
                        if (ok) return p;
                    }
            return parent + outDir * 300;
        }

        // ===== 보스 도전 버튼: '수확하러 가기' 바로 위 =====
        static void BossButton(List<string> log)
        {
            var ts = Find<TreeScreen>();
            var right = ts.transform.Find("Buttons/Right");
            var go0 = right != null ? right.Find("Go") : null;
            if (go0 == null || right.Find("Workshop") == null) { log.Add("보스 버튼 건너뜀: Buttons/Right/Go·Workshop 없음"); return; }
            var existing = ts.transform.Find("BossButton");
            GameObject btn;
            if (existing != null) btn = existing.gameObject;
            else
            {
                btn = Object.Instantiate(right.Find("Workshop").gameObject, ts.transform);
                btn.name = "BossButton";
                log.Add("보스 도전 버튼 추가");
            }
            var rt = (RectTransform)btn.transform;
            var goRt = (RectTransform)go0;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(goRt.sizeDelta.x, 66);
            rt.anchoredPosition = new Vector2(-24 - goRt.sizeDelta.x / 2, 20 + goRt.sizeDelta.y + 12);
            btn.GetComponent<UIAction>().act = "bosstoggle";
            btn.GetComponent<UIAction>().arg = "";
            var text = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            text.text = "보스 도전";
            text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 24;
            var so = new SerializedObject(ts);
            so.FindProperty("bossButtonText").objectReferenceValue = text;
            so.FindProperty("bossButtonFrame").objectReferenceValue = btn.GetComponent<Image>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ===== 공방: 수확기 카드(새 수확기 추가), 지운 날씨 줄 삭제 =====
        static void Workshop(List<string> log)
        {
            var ws = Find<WorkshopPanel>();
            var cards = ws.cards.Where(c => c != null).ToList();
            foreach (var h in HARVESTERS)
            {
                if (cards.Any(c => c.id == h.id)) continue;
                var tpl = cards.Last();
                var go = Object.Instantiate(tpl.gameObject, tpl.transform.parent);
                go.name = h.id;
                // 기본형 다음 자리에 (도감 순서 = HARVESTERS 순서)
                go.transform.SetSiblingIndex(tpl.transform.parent.childCount - 1);
                var c = go.GetComponent<HvCard>(); c.id = h.id;
                cards.Add(c);
                log.Add("수확기 카드 추가: " + h.id);
            }
            cards = HARVESTERS.Select(h => cards.First(c => c.id == h.id)).ToList();
            foreach (var c in cards)
                foreach (var (btn, act) in new[] { (c.unlock, "hvunlock"), (c.toggle, "hvtoggle"), (c.level, "hvlevel") })
                {
                    var ua = btn.GetComponent<UIAction>(); ua.act = act; ua.arg = c.id;
                    EditorUtility.SetDirty(ua);
                }
            for (int i = 0; i < cards.Count; i++) cards[i].transform.SetSiblingIndex(i);
            var rows = ws.weathers.Where(w => w != null).ToList();
            foreach (var r in rows.Where(r => !WEATHER.ContainsKey(r.id)).ToList())
            {
                log.Add("날씨 줄 삭제: " + r.id);
                rows.Remove(r);
                Object.DestroyImmediate(r.gameObject);
            }
            var so = new SerializedObject(ws);
            Set(so.FindProperty("cards"), cards);
            Set(so.FindProperty("weathers"), rows);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set<T>(SerializedProperty arr, List<T> items) where T : Object
        {
            arr.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }
}
