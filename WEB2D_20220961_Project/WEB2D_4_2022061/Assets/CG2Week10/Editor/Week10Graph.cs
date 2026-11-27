using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using C = Week10Common;

/// <summary>
/// 10주차 1~4단계의 검사 · 적용 메뉴.
///   그래프는 학생이 Shader Graph 창에서 직접 만든다. 이 메뉴들은 만든 결과를 '셰이더가 실제로 가진 속성'으로 검사한다.
///   (Shader.GetPropertyName 같은 공개 API만 쓴다 — 그래프 파일 구조가 바뀌어도 검사는 그대로 동작한다)
/// </summary>
public static class Week10Graph
{
    static string Row(string item, string value, bool ok, string fix) => $"| {item} | {value} | {(ok ? "✓" : "⚠ " + fix)} |\n";

    /// <summary>HDR 색 창의 Intensity 칸 = log2(가장 큰 채널) — 머티리얼에 저장된 값 기준</summary>
    public static float Intensity(Color c) => Mathf.Log(Mathf.Max(c.maxColorComponent, 1e-4f), 2);

    static int Find(Shader s, string name)
    {
        for (int i = 0; i < s.GetPropertyCount(); i++) if (s.GetPropertyName(i) == name) return i;
        return -1;
    }

    static string Names(Shader s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.GetPropertyCount(); i++)
            if ((s.GetPropertyFlags(i) & ShaderPropertyFlags.HideInInspector) == 0) sb.Append(sb.Length > 0 ? ", " : "").Append(s.GetPropertyName(i));
        return sb.ToString();
    }

    [MenuItem(C.MENU + "1-2. 그래프 검사 — 파일 · 대상(Sprite Lit)", priority = 12)]
    public static void CheckFile()
    {
        if (C.Busy("그래프 검사")) return;
        var sb = new StringBuilder("[검증] 1-2 그래프 파일\n| 항목 | 값 | 판정 |\n|---|---|---|\n");
        bool exists = File.Exists(C.GRAPH);
        sb.Append(Row("파일", C.GRAPH, exists, "Assets/CG2Week10/Shaders 에서 Create → Shader Graph → URP → Sprite Lit Shader Graph · 이름 SG_HitFlash"));
        if (exists)
        {
            string text = File.ReadAllText(C.GRAPH);
            string target = text.Contains("UniversalSpriteLitSubTarget") ? "Sprite Lit" : text.Contains("UniversalSpriteUnlitSubTarget") ? "Sprite Unlit" :
                            text.Contains("UniversalSpriteCustomLitSubTarget") ? "Sprite Custom Lit" : "다른 대상";
            sb.Append(Row("Graph Settings → Material", target, target == "Sprite Lit", "Sprite Lit Shader Graph로 다시 만든다 (2D 조명 · 노멀 맵을 받는다)"));
            var s = C.StudentShader();
            sb.Append(Row("컴파일", s == null ? "없음" : ShaderUtil.ShaderHasError(s) ? "오류" : "정상", s != null && !ShaderUtil.ShaderHasError(s), "그래프 창 Save Asset · Console 오류 확인"));
            if (s != null) sb.Append($"        셰이더 이름: {s.name} — 머티리얼의 Shader 목록에서 이 이름으로 찾는다\n");
        }
        sb.Append("        ── 다음 ── (직접) Blackboard 속성 3개 · 노드 연결 → Save Asset → 2-1");
        Debug.Log(sb.ToString());
    }

    [MenuItem(C.MENU + "2-1. 속성 검사 — 이름 · 참조 이름 · 범위", priority = 21)]
    public static void CheckProperties() => Debug.Log(PropertyReport(C.StudentShader(), "2-1", out _));

    public static string PropertyReport(Shader s, string tag, out int warn)
    {
        warn = 0;
        int w = 0;
        var sb = new StringBuilder($"[검증] {tag} 그래프 속성 — 셰이더가 실제로 가진 이름\n| 항목 | 값 | 판정 |\n|---|---|---|\n");
        if (s == null) { warn = 1; return sb.Append("| 셰이더 | 없음 | ⚠ 1-2부터 |\n").ToString(); }
        void W(bool ok) { if (!ok) w++; }
        int tex = Find(s, "_MainTex"), amt = Find(s, "_FlashAmount"), col = Find(s, "_FlashColor"), nrm = Find(s, "_NormalMap");
        W(tex >= 0); sb.Append(Row("스프라이트 그림 _MainTex", tex >= 0 ? "있음" : "없음", tex >= 0, "Texture2D 속성의 Reference를 _MainTex로 (SpriteRenderer가 이 이름으로 그림을 넘긴다)"));
        bool amtOk = amt >= 0 && s.GetPropertyType(amt) == ShaderPropertyType.Range;
        W(amtOk);
        string amtVal = amt < 0 ? "없음" : $"{s.GetPropertyType(amt)} {s.GetPropertyRangeLimits(amt).x:0}~{s.GetPropertyRangeLimits(amt).y:0} · 기본 {s.GetPropertyDefaultFloatValue(amt):0.##}";
        sb.Append(Row("플래시 양 _FlashAmount", amtVal, amtOk, amt < 0 ? "Float 속성의 Reference를 _FlashAmount로 (스크립트가 이 이름으로 부른다)" : "Mode = Slider · Min 0 · Max 1"));
        bool colOk = col >= 0 && s.GetPropertyType(col) == ShaderPropertyType.Color;
        W(colOk);
        sb.Append(Row("플래시 색 _FlashColor", colOk ? "Color" : "없음", colOk, "Color 속성의 Reference를 _FlashColor로"));
        if (colOk)
        {
            bool hdr = (s.GetPropertyFlags(col) & ShaderPropertyFlags.HDR) != 0;
            sb.Append($"| 플래시 색 Mode | {(hdr ? "HDR" : "Default")} | {(hdr ? "✓" : "ℹ 3단계에서 HDR로 바꾼다")} |\n");
        }
        sb.Append($"| 노멀 맵 _NormalMap | {(nrm >= 0 ? "있음" : "없음")} | {(nrm >= 0 ? "✓" : "ℹ 3단계에서 넣는다 — 없으면 7주차 입체감이 사라진다")} |\n");
        sb.Append($"        셰이더 속성 전체: {Names(s)}\n");
        warn = w;
        sb.Append(w == 0 ? "        ── 다음 ── Play → 2-2" : "        ⚠ Reference 칸은 Graph Inspector → Node Settings에서 바꾼다. 이름(Name)과 다른 칸이다");
        return sb.ToString();
    }

    // ────────────────────────────────────────────────────────── 4단계 머티리얼

    [MenuItem(C.MENU + "4-1. 머티리얼 검사 — 셰이더 · 플래시 색 밝기", priority = 41)]
    public static void CheckMaterial()
    {
        var m = C.StudentMaterial();
        var sb = new StringBuilder("[검증] 4-1 머티리얼\n| 항목 | 값 | 판정 |\n|---|---|---|\n");
        sb.Append(Row("파일", C.MATERIAL, m != null, "Assets/CG2Week10/Materials 에서 SG_HitFlash 우클릭 → Create → Material · 이름 M_HitFlash"));
        if (m != null)
        {
            bool sh = m.shader == C.StudentShader();
            sb.Append(Row("Shader", m.shader.name, sh, "Inspector 맨 위 Shader → Shader Graphs/SG_HitFlash"));
            if (m.HasProperty("_FlashColor"))
            {
                var c = m.GetColor("_FlashColor");
                float intensity = Intensity(c);
                sb.Append(Row("Flash Color", $"({c.r:0.##}, {c.g:0.##}, {c.b:0.##}) · Intensity {intensity:0.#}", intensity >= 1.9f, "HDR 색 창에서 흰색 · Intensity 2 (어두운 곳에서도 하얗게 — 측정 #3)"));
            }
            if (m.HasProperty("_FlashAmount")) sb.Append(Row("Flash Amount", $"{m.GetFloat("_FlashAmount"):0.##}", m.GetFloat("_FlashAmount") == 0, "0 (평소에는 원래 그림)"));
        }
        sb.Append("        ── 다음 ── 4-2");
        Debug.Log(sb.ToString());
    }

    [MenuItem(C.MENU + "4-2. 적 · 플레이어에 적용 — 머티리얼 바꾸기", priority = 42)]
    public static void Apply()
    {
        if (C.Busy("적용")) return;
        var m = C.StudentMaterial();
        if (m == null) { Debug.LogWarning("[실습] 4-2 M_HitFlash가 없다 — 4-1"); return; }
        var root = PrefabUtility.LoadPrefabContents(C.ENEMY_PREFAB);
        var esr = root.GetComponent<SpriteRenderer>();
        string before = esr.sharedMaterial ? esr.sharedMaterial.name : "없음";
        esr.sharedMaterial = m;
        PrefabUtility.SaveAsPrefabAsset(root, C.ENEMY_PREFAB);
        PrefabUtility.UnloadPrefabContents(root);
        int scene = 0;
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
            if (sr.CompareTag("Player") || sr.CompareTag("Enemy")) { Undo.RecordObject(sr, "피격 머티리얼"); sr.sharedMaterial = m; scene++; }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[실습] 4-2 머티리얼 적용 — {before} → {m.name}\n| 대상 | 바꾼 수 |\n|---|---|\n| Enemy 프리팹 | 1 |\n| 씬의 Player · Enemy | {scene} |\n" +
                  "        모두 같은 머티리얼 하나를 함께 쓴다(공유). 적마다 다른 값은 5단계에서 스크립트가 넣는다\n        ── 다음 ── Play → 4-3");
    }

    [MenuItem(C.MENU + "되돌리기 — 4단계 머티리얼 (Sprite-Lit-Default로)", priority = 91)]
    public static void RevertMaterial()
    {
        if (C.Busy("되돌리기")) return;
        var d = C.DefaultMaterial();
        var root = PrefabUtility.LoadPrefabContents(C.ENEMY_PREFAB);
        root.GetComponent<SpriteRenderer>().sharedMaterial = d;
        PrefabUtility.SaveAsPrefabAsset(root, C.ENEMY_PREFAB);
        PrefabUtility.UnloadPrefabContents(root);
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
            if (sr.CompareTag("Player") || sr.CompareTag("Enemy")) { Undo.RecordObject(sr, "되돌리기"); sr.sharedMaterial = d; }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[되돌리기] Enemy 프리팹 · Player · Enemy 머티리얼 → Sprite-Lit-Default");
    }

    [MenuItem(C.MENU + "(막혔을 때) 정답 그래프 · 머티리얼 만들기", priority = 95)]
    public static void MakeAnswer()
    {
        if (C.Busy("정답")) return;
        if (File.Exists(C.GRAPH) && !EditorUtility.DisplayDialog("정답 그래프", "SG_HitFlash가 이미 있습니다. 정답으로 덮어쓸까요?", "덮어쓰기", "취소")) return;
        Week10GraphBuilder.Build(C.GRAPH);
        Directory.CreateDirectory(Path.GetDirectoryName(C.MATERIAL));
        var m = C.StudentMaterial();
        if (m == null) { m = new Material(C.StudentShader()); AssetDatabase.CreateAsset(m, C.MATERIAL); }
        else m.shader = C.StudentShader();
        m.SetColor("_FlashColor", new Color(4, 4, 4, 1));   // HDR 색 창의 흰색 · Intensity 2와 같은 값
        m.SetFloat("_FlashAmount", 0);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        Debug.Log("[실습] 정답 그래프 · 머티리얼 — " + C.GRAPH + " · " + C.MATERIAL + "\n        그래프를 더블클릭해 노드 연결을 직접 확인한다");
    }
}
