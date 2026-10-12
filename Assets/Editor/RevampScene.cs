using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.EditorTools
{
    // v3 개편으로 새로 생긴 씬 UI를 만든다. 여러 번 실행해도 결과가 같다 (이미 있으면 연결만 다시 한다).
    // 의뢰 모달은 기록 모달(RecordsPanel)을 복제해 같은 모양을 쓰고, 버튼은 균사 트리 화면의 스킨 버튼을 복제한다.
    public static class RevampScene
    {
        const string QuestPanelPath = "Canvas/Modal/QuestPanel", QuestButtonPath = "Canvas/TreeScreen/Buttons/Right/Quests";
        const float QuestPanelWidth = 860;

        [MenuItem("MoreMush/개편/의뢰 화면 만들기")]
        public static string BuildQuestUi()
        {
            var scene = SceneManager.GetActiveScene();
            var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
            if (flow == null) return "GameFlow 없음 (Game 씬을 여세요)";
            var panel = BuildQuestPanel(scene);
            flow.quests = panel;
            EditorUtility.SetDirty(flow);
            BuildQuestButton(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "의뢰 화면 준비됨";
        }

        static Transform Find(Scene scene, string path)
        {
            var parts = path.Split('/');
            var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == parts[0]);
            return root == null ? null : parts.Length == 1 ? root.transform : root.transform.Find(string.Join("/", parts.Skip(1)));
        }

        static QuestPanel BuildQuestPanel(Scene scene)
        {
            var existing = Find(scene, QuestPanelPath);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                var src = Find(scene, "Canvas/Modal/RecordsPanel");
                go = Object.Instantiate(src.gameObject, src.parent);
                go.name = "QuestPanel";
                go.SetActive(false);
                Object.DestroyImmediate(go.GetComponent<RecordsPanel>());
            }
            var qp = go.GetComponent<QuestPanel>();
            if (qp == null) qp = go.AddComponent<QuestPanel>();   // Unity 객체에는 ?? 를 쓰지 않는다 (가짜 null)
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(QuestPanelWidth, rt.sizeDelta.y);
            float inner = QuestPanelWidth - 72;
            qp.title = go.transform.Find("Title").GetComponent<TMP_Text>();
            SetWidth(qp.title.transform, inner);
            var rows = go.transform.Find("Rows");
            SetWidth(rows, inner);
            var buttonSrc = Find(scene, "Canvas/TreeScreen/Buttons/Right/Skin");
            int b = 0;
            for (int i = 0; i < 9; i++)
            {
                var row = rows.Find("Row" + i);
                SetWidth(row, inner);
                qp.rowLabels[i] = row.Find("Label").GetComponent<TMP_Text>();
                qp.rowValues[i] = row.Find("Value").GetComponent<TMP_Text>();
                var labelLe = qp.rowLabels[i].GetComponent<LayoutElement>();
                if (labelLe != null) labelLe.flexibleWidth = 1;
                if (i == 0 || i == 4 || i == 8) continue;   // 머리줄·경험치 줄에는 버튼이 없다
                var claim = row.Find("Claim");
                if (claim == null)
                {
                    claim = Object.Instantiate(buttonSrc.gameObject, row).transform;
                    claim.name = "Claim";
                }
                var le = claim.GetComponent<LayoutElement>();
                if (le == null) le = claim.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 150; le.minWidth = 150; le.preferredHeight = 40; le.flexibleWidth = 0;
                var cg = claim.GetComponent<CanvasGroup>(); if (cg != null) cg.alpha = 1;
                var act = claim.GetComponent<UIAction>(); act.act = "questclaim"; act.arg = (b < 3 ? "d" : "w") + (b % 3);
                qp.claims[b] = claim.GetComponent<Button>();
                qp.claimTexts[b] = claim.GetComponentInChildren<TMP_Text>(true);
                qp.claimTexts[b].fontSize = Mathf.Min(qp.claimTexts[b].fontSize, 22);
                b++;
            }
            EditorUtility.SetDirty(qp);
            return qp;
        }

        static void BuildQuestButton(Scene scene)
        {
            if (Find(scene, QuestButtonPath) != null) return;
            var src = Find(scene, "Canvas/TreeScreen/Buttons/Right/Skin");
            var go = Object.Instantiate(src.gameObject, src.parent);
            go.name = "Quests";
            go.transform.SetSiblingIndex(src.GetSiblingIndex());
            go.GetComponent<UIAction>().act = "quests";
            go.GetComponent<UIAction>().arg = "";
            go.GetComponentInChildren<TMP_Text>(true).text = "의뢰";
        }

        // ===== 옵션 =====
        const string OptionsPanelPath = "Canvas/Modal/OptionsPanel";
        const float OptionsExtraH = 140, PauseExtraH = 60;

        [MenuItem("MoreMush/개편/옵션 화면 만들기")]
        public static string BuildOptionsUi()
        {
            var scene = SceneManager.GetActiveScene();
            var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
            if (flow == null) return "GameFlow 없음 (Game 씬을 여세요)";
            flow.options = BuildOptionsPanel(scene);
            EditorUtility.SetDirty(flow);
            AddButton(scene, "Canvas/Modal/PausePanel/Mute", "Options", "options", "옵션", PauseExtraH);
            AddButton(scene, "Canvas/TitleScreen/Box/Buttons/Reset", "Options", "options", "옵션", 0);
            AddButton(scene, "Canvas/TreeScreen/Buttons/Left/Records", "Options", "options", "옵션", 0);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "옵션 화면 준비됨";
        }

        // 일시정지 창을 복제한다: 계속하기 → 언어 줄, 끝내기·소리는 그대로, 타이틀로 버튼을 더한다
        static OptionsPanel BuildOptionsPanel(Scene scene)
        {
            var existing = Find(scene, OptionsPanelPath);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                var src = Find(scene, "Canvas/Modal/PausePanel");
                go = Object.Instantiate(src.gameObject, src.parent);
                go.name = "OptionsPanel";
                go.SetActive(false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta += new Vector2(0, OptionsExtraH);
                go.transform.Find("Title").GetComponent<TMP_Text>().text = "옵션";

                var resume = go.transform.Find("Resume");
                var row = new GameObject("LangRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                row.transform.SetParent(go.transform, false);
                row.transform.SetSiblingIndex(resume.GetSiblingIndex());
                ((RectTransform)row.transform).sizeDelta = ((RectTransform)resume).sizeDelta;
                var hlg = row.GetComponent<HorizontalLayoutGroup>();
                hlg.spacing = 12; hlg.childControlWidth = hlg.childControlHeight = true; hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;
                var rle = row.GetComponent<LayoutElement>();
                rle.preferredWidth = ((RectTransform)resume).sizeDelta.x; rle.preferredHeight = 64;
                resume.SetParent(row.transform, false);
                resume.name = "Lang";
                var rleOld = resume.GetComponent<LayoutElement>();
                if (rleOld != null) Object.DestroyImmediate(rleOld);

                var mute = go.transform.Find("Mute");
                var toTitle = Object.Instantiate(mute.gameObject, go.transform);
                toTitle.name = "ToTitle";
                toTitle.transform.SetSiblingIndex(mute.GetSiblingIndex() + 1);
                toTitle.GetComponent<UIAction>().act = "title";
                toTitle.GetComponentInChildren<TMP_Text>(true).text = "타이틀로";
                var opt = go.transform.Find("Options");   // 일시정지 창에 옵션 버튼을 먼저 단 경우 복제본에서 뺀다
                if (opt != null) Object.DestroyImmediate(opt.gameObject);
            }
            var op = go.GetComponent<OptionsPanel>();
            if (op == null) op = go.AddComponent<OptionsPanel>();
            op.langFirst = go.transform.Find("LangRow/Lang").GetComponent<UIAction>();
            op.langFirst.act = "lang"; op.langFirst.arg = "ko";
            op.muteText = go.transform.Find("Mute").GetComponentInChildren<TMP_Text>(true);
            op.endNow = go.transform.Find("EndNow").gameObject;
            op.toTitle = go.transform.Find("ToTitle").gameObject;
            EditorUtility.SetDirty(op);
            return op;
        }

        // src 옆에 같은 모양의 버튼을 하나 더한다 (이미 있으면 그대로). grow만큼 부모 높이를 늘린다
        static void AddButton(Scene scene, string srcPath, string name, string act, string label, float grow)
        {
            var src = Find(scene, srcPath);
            if (src == null || src.parent.Find(name) != null) return;
            var go = Object.Instantiate(src.gameObject, src.parent);
            go.name = name;
            go.transform.SetSiblingIndex(src.GetSiblingIndex() + 1);
            var a = go.GetComponent<UIAction>(); a.act = act; a.arg = "";
            go.GetComponentInChildren<TMP_Text>(true).text = label;
            if (grow > 0) ((RectTransform)src.parent).sizeDelta += new Vector2(0, grow);
        }

        static void SetWidth(Transform t, float w)
        {
            var rt = (RectTransform)t;
            rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
            var le = t.GetComponent<LayoutElement>();
            if (le != null) { le.preferredWidth = w; le.minWidth = Mathf.Min(le.minWidth, w); }
        }
    }
}
