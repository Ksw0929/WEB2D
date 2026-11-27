using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using C = Week11Common;

/// <summary>
/// 11주차 코드 메뉴 — 6-2 체크포인트 · (막혔을 때) EnemyAI · Health 정답 · 되돌리기
///   정답 코드는 Editor/Answers/*.cs.txt에 있다. 바꾸기 전 원본은 Backup/*.cs.bytes로 남긴다(10주차와 같은 방식).
/// </summary>
public static class Week11Code
{
    const string BACKUP = C.ROOT + "/Backup";
    const string ANSWERS = C.ROOT + "/Editor/Answers";

    static string Ok(bool ok, string good, string bad) => ok ? "✓ " + good : "⚠ " + bad;

    // ────────────────────────────────────────────────────────── 6-2 체크포인트

    [MenuItem(C.MENU + "6-2. 체크포인트 — 소멸 연출 · Web 빌드 준비", priority = 62)]
    public static void Checkpoint()
    {
        if (C.Busy("체크포인트")) return;
        var sb = new StringBuilder("[검증] 6-2 체크포인트 — 몬스터 사망에 맞춰 디졸브가 0 → 1로 바뀌고, 여러 마리가 서로 섞이지 않는가?\n");
        var s = C.StudentShader(); var m = C.StudentMaterial();
        bool graph = s != null && !ShaderUtil.ShaderHasError(s);
        bool clip = Week11Graph.AlphaClipOn(C.GRAPH);
        bool edge = s != null && s.FindPropertyIndex("_EdgeWidth") >= 0 && s.FindPropertyIndex("_EdgeColor") >= 0;
        sb.AppendLine($"        [그래프]   {Ok(graph, "SG_Dissolve 컴파일", "SG_Dissolve 없음 · 오류 (2-1)")}  {Ok(clip, "Alpha Clipping", "Alpha Clipping 꺼짐 (2단계)")}  {Ok(edge, "경계 속성", "_EdgeWidth · _EdgeColor 없음 (3단계)")}");
        bool matOk = m != null && m.shader == s;
        float flash = matOk && m.HasProperty("_FlashColor") ? m.GetColor("_FlashColor").maxColorComponent : 0;
        float edgeMax = matOk && m.HasProperty("_EdgeColor") ? m.GetColor("_EdgeColor").maxColorComponent : 0;
        sb.AppendLine($"        [머티리얼] {Ok(matOk, "M_Dissolve → SG_Dissolve", "M_Dissolve 없음 · Shader 확인")}  {Ok(flash >= 3.7f, $"FlashColor Intensity {Mathf.Log(Mathf.Max(flash, 1e-4f), 2):0.#}", "FlashColor Intensity 2 (10주차 값)")}  {Ok(edgeMax > 1.01f, $"EdgeColor Intensity {Mathf.Log(Mathf.Max(edgeMax, 1e-4f), 2):0.#}", "EdgeColor가 1보다 밝지 않다 — 어두운 곳에서 안 빛난다 (측정 #3)")}");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(C.ENEMY_PREFAB);
        bool applied = prefab != null && prefab.GetComponent<SpriteRenderer>().sharedMaterial == m && m != null;
        sb.AppendLine($"        [적용]     {Ok(applied, "Enemy 프리팹 = M_Dissolve", "Enemy 프리팹 머티리얼 (4-1)")}");
        var ai = File.Exists(C.ENEMY_AI) ? File.ReadAllText(C.ENEMY_AI) : "";
        var hp = File.Exists(C.HEALTH) ? File.ReadAllText(C.HEALTH) : "";
        bool code1 = ai.Contains("\"_DissolveAmount\"") && ai.Contains("StartCoroutine");
        bool code2 = ai.Contains("simulated = false") || ai.Contains("simulated=false") || ai.Contains(".enabled = false");
        bool code3 = ai.Contains(".Return(");
        bool code4 = hp.Contains("OnEnable");
        sb.AppendLine($"        [코드]     {Ok(code1, "사망 → 코루틴으로 _DissolveAmount", "EnemyAI.OnDeath에서 디졸브 코루틴")}  {Ok(code2, "죽으면 물리 끄기", "죽은 적이 밀리고 총알을 막는다")}  {Ok(code3, "풀 반환", "다 지운 뒤 pool.Return")}  {Ok(code4, "Health 초기화", "풀에서 나온 적이 죽은 채로 나온다 (Health.OnEnable)")}");
        string m4 = C.Load("m4_ok"), dup = C.Load("m5_dup"), copy = C.Load("m5_copy"), m6 = C.Load("m6_ok");
        sb.AppendLine($"        [측정]     {(m4 == "" ? "ℹ #4 안 함" : Ok(m4 == "1", "#4 사망 → 0 → 1 → 풀", "#4 판정 ⚠"))}  {(dup == "" ? "ℹ #5 안 함" : Ok(dup == "1", "#5 중복 사망 1번", $"#5 onDeath {dup}번"))}  {(copy == "" ? "" : Ok(copy == "1", "#5 복사본은 한 마리만 바뀐다", "#5 복사본 결과 확인"))}  {(m6 == "" ? "ℹ #6 안 함" : Ok(m6 == "1", "#6 풀 장부 = 실제", "#6 죽은 적이 풀에 안 돌아간다"))}");
        bool web = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        sb.AppendLine($"        [Web]      {(web ? "ℹ Web 빌드 모듈 설치됨" : "ℹ Web 빌드 모듈 없음 — Unity Hub → Installs → Add modules → Web Build Support")}  {Ok(graph && applied, "셰이더는 프리팹이 참조하므로 빌드에 들어간다", "그래프 · 적용부터")}");
        int warn = sb.ToString().Split('⚠').Length - 1;
        sb.Append(warn == 0 ? "        ✓ 경고 0개 — README에 소멸 전 · 중 · 후 그림(Captures/week11_death_frames.png)과 그래프 · 값을 적고 커밋\n"
                            : $"        ⚠ {warn}개 — 괄호 안 단계로 돌아가 고친다\n");
        sb.Append("        ── Web 확인 ── File → Build Profiles → Web → Build And Run → 브라우저에서 적을 잡아 경계가 빛나며 사라지는지 본다");
        C.Save("checkpoint", sb.ToString());
        if (warn == 0) Debug.Log(sb.ToString()); else Debug.LogWarning(sb.ToString());
    }

    // ────────────────────────────────────────────────────────── 막혔을 때 · 되돌리기

    static void Swap(string target, string answerName)
    {
        Directory.CreateDirectory(BACKUP);
        var bak = $"{BACKUP}/{Path.GetFileName(target)}.bytes";
        if (!File.Exists(bak)) File.Copy(target, bak);   // 처음 바꿀 때의 원본만 남긴다
        File.WriteAllText(target, File.ReadAllText($"{ANSWERS}/{answerName}"));
    }

    [MenuItem(C.MENU + "(막혔을 때) EnemyAI · Health 정답으로 바꾸기", priority = 96)]
    public static void Answer()
    {
        if (C.Busy("정답 코드")) return;
        if (!EditorUtility.DisplayDialog("정답 코드", "EnemyAI.cs와 Health.cs를 11주차 정답으로 바꿉니다.\n원본은 Assets/CG2Week11/Backup에 남습니다.", "바꾸기", "취소")) return;
        Swap(C.ENEMY_AI, "EnemyAI.cs.txt");
        Swap(C.HEALTH, "Health.cs.txt");
        AssetDatabase.Refresh();
        Debug.Log("[막혔을 때] EnemyAI · Health → 11주차 정답. 원본은 Assets/CG2Week11/Backup — 컴파일이 끝나면 Play → 4-2");
    }

    [MenuItem(C.MENU + "되돌리기 — EnemyAI · Health 원본으로", priority = 92)]
    public static void Revert()
    {
        if (C.Busy("되돌리기")) return;
        int n = 0;
        foreach (var t in new[] { C.ENEMY_AI, C.HEALTH })
        {
            var bak = $"{BACKUP}/{Path.GetFileName(t)}.bytes";
            if (!File.Exists(bak)) continue;
            File.Copy(bak, t, true); File.Delete(bak); if (File.Exists(bak + ".meta")) File.Delete(bak + ".meta"); n++;
        }
        AssetDatabase.Refresh();
        Debug.Log(n > 0 ? $"[되돌리기] 원본 {n}개를 되돌렸다" : "[되돌리기] Backup에 원본이 없다 — 정답 메뉴를 쓰지 않았거나 이미 되돌렸다 (직접 고친 코드는 직접 되돌린다)");
    }
}
