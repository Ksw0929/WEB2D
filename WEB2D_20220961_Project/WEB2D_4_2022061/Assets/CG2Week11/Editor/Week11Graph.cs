using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using C = Week11Common;
using Object = UnityEngine.Object;

/// <summary>
/// 11주차 그래프 · 머티리얼 메뉴
///   2-1 디졸브 그래프 검사(속성 · Alpha Clipping · 진행도 0과 1을 실제로 그려 본다)
///   4-1 적 프리팹에 M_Dissolve 적용 · 되돌리기 · (막혔을 때) 정답 그래프
/// </summary>
public static class Week11Graph
{
    static string Row(string item, string value, bool ok, string fix) => $"| {item} | {value} | {(ok ? "✓" : "⚠ " + fix)} |\n";

    static bool HasProp(Shader s, string name, out int index)
    {
        index = s.FindPropertyIndex(name);
        return index >= 0;
    }

    /// <summary>그래프 파일(JSON)에서 Graph Settings → Alpha Clipping 체크 여부를 읽는다.</summary>
    public static bool AlphaClipOn(string graphPath)
    {
        if (!File.Exists(graphPath)) return false;
        var text = File.ReadAllText(graphPath);
        return text.Contains("\"m_AlphaClip\": true");
    }

    /// <summary>머티리얼을 측정용 스프라이트에 입혀 그린다 — 진행도마다 그림 안에 남은 픽셀 %.</summary>
    public static float[] RemainAt(Material src, float[] amounts, out Texture2D[] shots) => RemainAt(src, amounts, out shots, new Vector3(80, 80, 0), true);

    /// <summary>pos · bright: 게임 조명 아래에서 보려면 실제 자리에 두고 bright = false</summary>
    public static float[] RemainAt(Material src, float[] amounts, out Texture2D[] shots, Vector3 pos, bool bright)
    {
        var sp = AssetDatabase.LoadAssetAtPath<GameObject>(C.ENEMY_PREFAB).GetComponent<SpriteRenderer>().sprite;
        var go = new GameObject("CG2 Check Sprite") { hideFlags = HideFlags.DontSave };
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp; sr.sortingLayerName = "Entity";
        var mat = new Material(src) { hideFlags = HideFlags.DontSave };
        if (mat.HasProperty("_FlashAmount")) mat.SetFloat("_FlashAmount", 0);
        sr.sharedMaterial = mat;
        var cam = C.ZoomCamera(sr.bounds);
        var mask = new C.Mask(sr);
        var result = new float[amounts.Length];
        shots = new Texture2D[amounts.Length];
        var restore = bright ? C.Bright() : () => { };
        try
        {
            sr.enabled = false; var bg = C.Shoot(cam, 200, 200); sr.enabled = true;
            mat.SetFloat("_DissolveAmount", 0); var orig = C.Shoot(cam, 200, 200);
            for (int i = 0; i < amounts.Length; i++)
            {
                mat.SetFloat("_DissolveAmount", amounts[i]);
                shots[i] = C.Shoot(cam, 200, 200);
                result[i] = C.Classify(shots[i], bg, orig, cam, mask).RemainPct;
            }
            Object.DestroyImmediate(bg); Object.DestroyImmediate(orig);
        }
        finally
        {
            restore();
            mask.Free(); Object.DestroyImmediate(cam.gameObject); Object.DestroyImmediate(go); Object.DestroyImmediate(mat);
        }
        return result;
    }

    // ────────────────────────────────────────────────────────── 2-1

    [MenuItem(C.MENU + "2-1. 디졸브 그래프 검사 — 속성 · Alpha Clipping · 진행도 0과 1", priority = 21)]
    public static void CheckGraph()
    {
        if (C.Busy("2-1 그래프 검사")) return;
        var sb = new StringBuilder("[검증] 2-1 디졸브 그래프\n| 항목 | 값 | 판정 |\n|---|---|---|\n");
        var s = C.StudentShader();
        bool exists = s != null;
        sb.Append(Row("파일", C.GRAPH, exists, "SG_HitFlash 선택 → Ctrl+D → 이름 SG_Dissolve → Shaders 폴더로"));
        if (!exists) { Debug.LogWarning(sb.ToString()); return; }
        bool err = ShaderUtil.ShaderHasError(s);
        sb.Append(Row("컴파일", err ? "오류" : "정상", !err, "그래프 창 빨간 노드 확인 → Save Asset"));
        sb.Append(Row("셰이더 이름", s.name, s.name == "Shader Graphs/SG_Dissolve", "파일 이름을 SG_Dissolve로"));

        // 10주차에서 물려받은 속성 — 지워지면 피격 플래시 · 노멀 맵이 끊긴다
        foreach (var n in new[] { "_MainTex", "_NormalMap", "_FlashAmount", "_FlashColor" })
            sb.Append(Row($"10주차 속성 {n}", HasProp(s, n, out _) ? "있음" : "없음", HasProp(s, n, out _), "SG_HitFlash를 복제해서 시작한다"));

        bool hasAmount = HasProp(s, "_DissolveAmount", out int ai);
        string amountInfo = hasAmount ? (s.GetPropertyType(ai) == UnityEngine.Rendering.ShaderPropertyType.Range ? $"Slider {s.GetPropertyRangeLimits(ai).x:0.##}~{s.GetPropertyRangeLimits(ai).y:0.##}" : s.GetPropertyType(ai).ToString()) : "없음";
        sb.Append(Row("_DissolveAmount (진행도)", amountInfo, hasAmount && amountInfo.StartsWith("Slider 0~1"), "Blackboard + Float · 이름 DissolveAmount · Mode Slider · Min 0 · Max 1"));
        bool hasScale = HasProp(s, "_NoiseScale", out int si);
        sb.Append(Row("_NoiseScale (무늬 크기)", hasScale ? s.GetPropertyDefaultFloatValue(si).ToString("0.##") : "없음", hasScale, "Blackboard + Float · 이름 NoiseScale · Default 40"));
        bool clip = AlphaClipOn(C.GRAPH);
        sb.Append(Row("Graph Settings → Alpha Clipping", clip ? "켜짐" : "꺼짐", clip, "Graph Inspector → Graph Settings → Alpha Clipping 체크 → Save Asset"));

        // 3단계를 했으면 경계 속성까지 본다
        bool hasW = HasProp(s, "_EdgeWidth", out _), hasC = HasProp(s, "_EdgeColor", out int ci);
        if (hasW || hasC)
        {
            sb.Append(Row("_EdgeWidth (경계 두께)", hasW ? "있음" : "없음", hasW, "Blackboard + Float · 이름 EdgeWidth · Slider 0~0.3 · Default 0.08"));
            bool hdr = hasC && (s.GetPropertyFlags(ci) & UnityEngine.Rendering.ShaderPropertyFlags.HDR) != 0;
            sb.Append(Row("_EdgeColor (경계 색 · HDR)", hasC ? (hdr ? "HDR" : "일반 색") : "없음", hdr, "Graph Inspector → Mode = HDR"));
        }

        // 실제로 그려 본다 — 이름이 맞아도 선이 틀리면 여기서 걸린다
        if (hasAmount && !err)
        {
            var src = new Material(s) { hideFlags = HideFlags.DontSave };
            var m = C.StudentMaterial();
            if (m != null && m.shader == s) src.CopyPropertiesFromMaterial(m);   // 머티리얼이 있으면 그 값(NoiseScale 등)으로
            var r = RemainAt(src, new[] { 0f, 0.5f, 1f }, out var shots);
            foreach (var t in shots) Object.DestroyImmediate(t);
            Object.DestroyImmediate(src);
            sb.Append(Row("진행도 0 → 그림 안에 남은 픽셀", $"{r[0]:0.0}%", r[0] >= 99f, "0이면 그대로여야 한다 — Step의 Edge · In 순서, Alpha 연결 확인"));
            sb.Append(Row("진행도 0.5", $"{r[1]:0.0}%", r[1] > 1f && r[1] < 99f, "무늬가 안 생긴다 — Simple Noise → Step In 연결 확인"));
            sb.Append(Row("진행도 1 → 남은 픽셀", $"{r[2]:0.0}%", r[2] <= 0.5f, "1이면 다 지워져야 한다 — Multiply 결과 → Alpha 블록 연결 확인"));
        }
        int warn = sb.ToString().Split('⚠').Length - 1;
        sb.Append(warn == 0 ? "        읽는 법: 속성 이름(Reference)이 맞고, 실제로 그려 보니 진행도 0에서 그대로 · 1에서 모두 사라진다\n"
                            : $"        ⚠ {warn}개 — 위 표의 오른쪽 칸대로 고친 뒤 Save Asset → 2-1 다시\n");
        sb.Append(hasW ? "        ── 다음 ── Play → 3-1" : "        ── 다음 ── (직접) 머티리얼 M_Dissolve → 2-2");
        C.Save("check21", sb.ToString());
        if (warn == 0) Debug.Log(sb.ToString()); else Debug.LogWarning(sb.ToString());
    }

    // ────────────────────────────────────────────────────────── 4-1

    [MenuItem(C.MENU + "4-1. 적에 M_Dissolve 적용 — 프리팹 · 씬의 적", priority = 41)]
    public static void Apply()
    {
        if (C.Busy("4-1 적용")) return;
        var m = C.StudentMaterial();
        if (m == null) { Debug.LogWarning($"[실습] 4-1 {C.MATERIAL}가 없다 — 2단계 (직접) 머티리얼 만들기"); return; }
        if (m.shader != C.StudentShader()) { Debug.LogWarning($"[실습] 4-1 M_Dissolve의 Shader가 {m.shader.name}이다 — Inspector 맨 위 Shader → Shader Graphs/SG_Dissolve"); return; }
        int scene = 0; string before = "", reset = "";
        // 2 · 3단계에서 Inspector 슬라이더를 밀어 둔 채면 적이 구멍 난 채로 나온다 — 0으로 되돌린다
        foreach (var p in new[] { "_DissolveAmount", "_FlashAmount" })
            if (m.HasProperty(p) && m.GetFloat(p) > 0) { reset += $" {p} {m.GetFloat(p):0.##} → 0"; m.SetFloat(p, 0); EditorUtility.SetDirty(m); }
        if (reset != "") AssetDatabase.SaveAssets();
        var root = PrefabUtility.LoadPrefabContents(C.ENEMY_PREFAB);
        var psr = root.GetComponent<SpriteRenderer>();
        before = psr.sharedMaterial ? psr.sharedMaterial.name : "없음";
        psr.sharedMaterial = m;
        PrefabUtility.SaveAsPrefabAsset(root, C.ENEMY_PREFAB);
        PrefabUtility.UnloadPrefabContents(root);
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
        {
            if (!sr.CompareTag("Enemy") || sr.sharedMaterial == m) continue;
            Undo.RecordObject(sr, "M_Dissolve 적용"); sr.sharedMaterial = m; scene++;
            PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
        }
        if (scene > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        var msg = $"[실습] 4-1 적 머티리얼 — {before} → M_Dissolve  |  Enemy 프리팹 1 · 씬의 적 {scene}  (플레이어는 M_HitFlash 그대로)\n" +
                  (reset != "" ? $"        ℹ 머티리얼에 밀어 둔 값을 되돌렸다:{reset}\n" : "") +
                  "        읽는 법: M_Dissolve는 SG_HitFlash를 복제한 그래프라 피격 플래시 · 노멀 맵도 그대로 한다\n        ── 다음 ── Ctrl+S → (직접) EnemyAI · Health 고치기 → Play → 4-2";
        C.Save("apply41", msg);
        Debug.Log(msg);
    }

    [MenuItem(C.MENU + "되돌리기 — 적 머티리얼을 M_HitFlash로", priority = 91)]
    public static void Revert()
    {
        if (C.Busy("되돌리기")) return;
        var back = AssetDatabase.LoadAssetAtPath<Material>(C.W10_MATERIAL);
        if (back == null) { Debug.LogWarning("[되돌리기] M_HitFlash가 없다 (10주차)"); return; }
        var root = PrefabUtility.LoadPrefabContents(C.ENEMY_PREFAB);
        root.GetComponent<SpriteRenderer>().sharedMaterial = back;
        PrefabUtility.SaveAsPrefabAsset(root, C.ENEMY_PREFAB);
        PrefabUtility.UnloadPrefabContents(root);
        int n = 0;
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
            if (sr.CompareTag("Enemy") && sr.sharedMaterial != back) { Undo.RecordObject(sr, "되돌리기"); sr.sharedMaterial = back; n++; }
        if (n > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[되돌리기] 적 머티리얼 → M_HitFlash  |  프리팹 1 · 씬 {n} — Ctrl+S로 씬 저장");
    }

    // ────────────────────────────────────────────────────────── 막혔을 때

    [MenuItem(C.MENU + "(막혔을 때) 정답 그래프 SG_Dissolve 만들기", priority = 95)]
    public static void Answer()
    {
        if (C.Busy("정답 그래프")) return;
        if (File.Exists(C.GRAPH) && !EditorUtility.DisplayDialog("정답 그래프", "SG_Dissolve가 이미 있습니다. 정답으로 덮어쓸까요?\n(머티리얼 M_Dissolve의 연결은 유지됩니다)", "덮어쓰기", "취소")) return;
        Week11GraphBuilder.Build(C.GRAPH);
        var m = C.StudentMaterial();
        if (m == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(C.MATERIAL));
            m = new Material(C.StudentShader());
            AssetDatabase.CreateAsset(m, C.MATERIAL);
            Week11Measure.Defaults(m);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[막혔을 때] 정답 그래프 SG_Dissolve · 머티리얼 M_Dissolve — 그래프를 열어 노드 연결을 자기 것과 비교한다 → 2-1");
    }
}
