using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using C = Week11Common;
using Object = UnityEngine.Object;

/// <summary>
/// 11주차 측정 · 비교.
///   Play 중: 1-1 #1 지금 적이 사라지는 모습 · 3-1 #3 경계 발광 · 4-2 #4 사망 → 소멸 · 5-1 #5 여러 마리 · 5-2 #6 풀 장부
///   Play 아님: 1-2 일반 Alpha · Alpha Clipping · 디졸브 비교 · 2-2 #2 진행도 곡선 · 6-1 스타일 비교
///   측정용 스프라이트는 다른 조명이 없는 먼 곳(80, 80)에 둔다 — 남은 픽셀 수는 조명과 상관없이 같다.
/// </summary>
public static class Week11Measure
{
    // ────────────────────────────────────────────────────────── 프레임 단위 실행기 (8 · 10주차와 같다)

    class Step { public Func<bool> act; public int wait; }   // act가 false를 돌려주면 같은 단계를 다음 프레임에 다시 한다
    static readonly Queue<Step> _steps = new();
    static int _until;
    static Action _onAbort;

    static Health _guard;   // 게임이 흐르는 측정 동안 플레이어가 죽어 게임 오버(timeScale 0)가 되지 않게 체력을 채운다

    static void Run(Action onAbort, bool guardPlayer = false)
    {
        _onAbort = onAbort;
        _guard = guardPlayer ? GameObject.FindWithTag("Player")?.GetComponent<Health>() : null;
        _until = Time.frameCount + 30;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Then(Action a, int wait = 6) => _steps.Enqueue(new Step { act = () => { a(); return true; }, wait = wait });
    static void Until(Func<bool> a, int wait = 1) => _steps.Enqueue(new Step { act = a, wait = wait });

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Halt(); _onAbort?.Invoke(); Debug.LogWarning("[측정] Play가 끝나 중단했다."); return; }
        if (_guard)
        {
            _guard.Heal(9999);
            // 적을 잡으면 경험치 → 5주차 레벨업 창이 게임을 멈춘다(timeScale 0). 측정 동안만 시간을 계속 흐르게 한다
            if (Time.timeScale == 0 && !(GameManager.Instance != null && GameManager.Instance.IsGameOver)) { Time.timeScale = 1; _pausedByGame = true; }
        }
        if (Time.frameCount < _until) return;
        if (_steps.Count == 0) { Halt(); return; }
        var s = _steps.Peek();
        bool done;
        try { done = s.act(); }
        catch (Exception e) { Halt(); _onAbort?.Invoke(); Debug.LogException(e); return; }
        if (done) { _steps.Dequeue(); _until = Time.frameCount + s.wait; }
    }

    static bool _pausedByGame;

    static void Halt()
    {
        EditorApplication.update -= Tick; _steps.Clear(); _guard = null;
        // 측정 중 레벨업 창이 떴으면 창이 열린 상태 그대로 다시 멈춘다(창에서 고르면 풀린다). 5주차 CG2Pause가 없으면 건너뛴다
        if (_pausedByGame && EditorApplication.isPlaying)
        {
            var pause = Type.GetType("CG2Pause, Assembly-CSharp");
            var paused = pause?.GetProperty("IsPaused", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
            if (paused is bool b && b) Time.timeScale = 0;
        }
        _pausedByGame = false;
    }

    static bool NeedPlay(string title)
    {
        if (EditorApplication.isPlaying && _steps.Count == 0 && GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            EditorUtility.DisplayDialog(title, "게임 오버 상태입니다(게임이 멈춰 있다).\nStop → Play 후 바로 실행하세요.", "확인");
            return true;
        }
        if (EditorApplication.isPlaying && _steps.Count == 0) return false;
        EditorUtility.DisplayDialog(title, _steps.Count > 0 ? "측정이 이미 진행 중입니다." : "Play 중에 실행하세요.", "확인");
        return true;
    }

    /// <summary>게임을 멈추고(timeScale 0), 8주차 횃불 깜빡임을 끈다. 끝나면 되돌린다.</summary>
    class Freeze
    {
        readonly float _ts;
        readonly List<Behaviour> _off = new();
        public Freeze(bool stopTime = true)
        {
            _ts = Time.timeScale; if (stopTime) Time.timeScale = 0;
            foreach (var b in Object.FindObjectsByType<MonoBehaviour>())
                if (b.enabled && b.GetType().Name == "CG2Torch") { b.enabled = false; _off.Add(b); }
        }
        public void End()
        {
            foreach (var b in _off) if (b) b.enabled = true;
            if (EditorApplication.isPlaying) Time.timeScale = _ts;
        }
    }

    /// <summary>측정용 스프라이트 — 적 그림을 고정 위치에 하나 둔다(게임 로직 없음).</summary>
    static SpriteRenderer Dummy(string name, Vector3 pos, Material mat)
    {
        var sp = AssetDatabase.LoadAssetAtPath<GameObject>(C.ENEMY_PREFAB).GetComponent<SpriteRenderer>().sprite;
        var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sortingLayerName = "Entity";
        sr.sharedMaterial = mat;
        return sr;
    }

    /// <summary>HDR 색 창과 같은 단위: 색 × 2^intensity (10주차와 같다)</summary>
    public static Color Hdr(Color c, float intensity) { float k = Mathf.Pow(2, intensity); return new Color(c.r * k, c.g * k, c.b * k, 1); }
    public static readonly Color ORANGE = new Color(1f, 0.42f, 0.08f);

    /// <summary>머티리얼 권장값 — 정답 메뉴 · 체크포인트 기준</summary>
    public static void Defaults(Material m)
    {
        m.SetFloat("_DissolveAmount", 0);
        if (m.HasProperty("_NoiseScale")) m.SetFloat("_NoiseScale", 40);
        if (m.HasProperty("_EdgeWidth")) m.SetFloat("_EdgeWidth", 0.08f);
        if (m.HasProperty("_EdgeColor")) m.SetColor("_EdgeColor", Hdr(ORANGE, 3));
        if (m.HasProperty("_FlashColor")) m.SetColor("_FlashColor", Hdr(Color.white, 2));
        EditorUtility.SetDirty(m);
    }

    static ObjectPool Pool() => Object.FindAnyObjectByType<EnemySpawner>()?.GetComponent<ObjectPool>();
    static int AliveEnemies() => Object.FindObjectsByType<Health>().Count(h => h.CompareTag("Enemy") && h.gameObject.activeInHierarchy && !h.IsDead);

    /// <summary>찍는 동안만 대상 말고 다른 캐릭터를 숨긴다 — 지나가던 적이 사진에 들어오지 않게.</summary>
    static Texture2D ShootAlone(Camera cam, SpriteRenderer target, int size)
    {
        var hidden = new List<SpriteRenderer>();
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>())
            if (sr != target && sr.enabled && (sr.CompareTag("Enemy") || sr.CompareTag("Player") || sr.GetComponent<Projectile>())) { sr.enabled = false; hidden.Add(sr); }
        var t = C.Shoot(cam, size, size);
        foreach (var sr in hidden) sr.enabled = true;
        return t;
    }

    // ────────────────────────────────────────────────────────── 1-1 측정 #1

    [MenuItem(C.MENU + "1-1. 측정 #1 — 지금 적은 어떻게 사라질까 (Play 중)", priority = 11)]
    public static void Measure1()
    {
        if (NeedPlay("측정 #1")) return;
        var fz = new Freeze(false);
        SpriteRenderer target = null; Camera cam = null; C.Mask mask = null; Texture2D orig = null, bg = null;
        var rows = new List<string>(); int deaths = 0; float t0 = 0; int f0 = 0;
        void End() { if (cam) Object.DestroyImmediate(cam.gameObject); mask?.Free(); if (orig) Object.DestroyImmediate(orig); if (bg) Object.DestroyImmediate(bg); fz.End(); }
        Then(() =>
        {
            target = C.NearestEnemy();
            if (target == null) throw new Exception("화면에 적이 없다 — 적이 나온 뒤 다시");
            cam = C.ZoomCamera(target.bounds); mask = new C.Mask(target);
            target.enabled = false; bg = ShootAlone(cam, target, 160); target.enabled = true;
            orig = ShootAlone(cam, target, 160);
            var h = target.GetComponent<Health>();
            h.onDeath.AddListener(() => deaths++);
            t0 = Time.time; f0 = Time.frameCount;
            h.TakeDamage(h.MaxHp);                       // 총알이 마지막 체력을 깎은 것과 같다
        }, 1);
        int k = 0;
        Until(() =>
        {
            var shot = ShootAlone(cam, target, 160);
            var c = C.Classify(shot, bg, orig, cam, mask);
            Object.DestroyImmediate(shot);
            rows.Add($"| {Time.frameCount - f0} | {(Time.time - t0) * 1000:0} ms | {(target.gameObject.activeInHierarchy ? "켜짐" : "꺼짐")} | {c.RemainPct:0.0}% |");
            return ++k >= 3;
        });
        Then(() =>
        {
            var sb = new StringBuilder("[측정] #1 지금 적이 죽는 모습 — 가장 가까운 적 · Health.TakeDamage(최대 체력)\n");
            sb.AppendLine("| 죽은 뒤 프레임 | 시간 | 오브젝트 | 그림 안에 남은 픽셀 |\n|---|---|---|---|");
            sb.AppendLine("| 0 (죽기 직전) | 0 ms | 켜짐 | 100.0% |");
            foreach (var r in rows) sb.AppendLine(r);
            sb.AppendLine($"| onDeath 호출 | {deaths}번 | | |");
            sb.Append("        읽는 법: 죽은 다음 프레임에 그림이 0%가 된다 — 사라지는 과정이 없다. EnemyAI.OnDeath가 바로 SetActive(false)를 한다\n        ── 다음 ── Stop → 1-2");
            C.Save("m1", sb.ToString());
            C.Save("m1_remain", rows.Count > 0 ? rows[0] : "");
            End();
            Debug.Log(sb.ToString());
        }, 1);
        Run(End, true);
    }

    // ────────────────────────────────────────────────────────── 1-2 비교

    static Shader CompareShader(string name, Week11GraphBuilder.Options o)
    {
        var path = $"{C.COMPARE}/{name}.shadergraph";
        if (!File.Exists(path)) Week11GraphBuilder.Build(path, o);
        return AssetDatabase.LoadAssetAtPath<Shader>(path);
    }

    [MenuItem(C.MENU + "1-2. 비교 — 일반 Alpha · Alpha Clipping · 디졸브", priority = 12)]
    public static void Compare()
    {
        if (C.Busy("1-2 비교")) return;
        var kinds = new (string label, Shader sh)[]
        {
            ("ⓐ 일반 Alpha (그림 Alpha × (1 − 진행도))", CompareShader("SG_Compare_Fade", new Week11GraphBuilder.Options { cut = Week11GraphBuilder.Cut.Fade, alphaClip = false, edge = false, remap = false })),
            ("ⓑ Alpha Clipping만 (Alpha = 그림 × 무늬 · 기준 = 진행도)", CompareShader("SG_Compare_ClipOnly", new Week11GraphBuilder.Options { cut = Week11GraphBuilder.Cut.ClipOnly, alphaClip = true, edge = false, remap = false })),
            ("ⓒ 디졸브 (무늬와 진행도를 Step으로 비교 + Alpha Clipping)", CompareShader("SG_Compare_Dissolve", new Week11GraphBuilder.Options { edge = false, remap = false })),
            ("ⓓ ⓒ에서 Alpha Clipping만 끔", CompareShader("SG_Compare_DissolveNoClip", new Week11GraphBuilder.Options { alphaClip = false, edge = false, remap = false })),
        };
        var sb = new StringBuilder("[비교] 1-2 같은 진행도 0.5에서 세 가지 '사라지기' — 측정용 스프라이트 · NoiseScale 40\n");
        sb.AppendLine("| 방식 | 그림 안에 남은 픽셀 | 남은 픽셀 중 원래 색 그대로 |\n|---|---|---|");
        var shots = new List<Texture2D>();
        Texture2D cShot = null, dShot = null;
        var restore = C.Bright();
        foreach (var (label, sh) in kinds)
        {
            var mat = new Material(sh) { hideFlags = HideFlags.DontSave };
            mat.SetFloat("_NoiseScale", 40);
            var sp = AssetDatabase.LoadAssetAtPath<GameObject>(C.ENEMY_PREFAB).GetComponent<SpriteRenderer>().sprite;
            var go = new GameObject("CG2 Compare") { hideFlags = HideFlags.DontSave };
            go.transform.position = new Vector3(80, 80, 0);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.sortingLayerName = "Entity"; sr.sharedMaterial = mat;
            var cam = C.ZoomCamera(sr.bounds); var mask = new C.Mask(sr);
            sr.enabled = false; var bg = C.Shoot(cam, 200, 200); sr.enabled = true;
            // 원래 그림 = 기본 Sprite-Lit-Default로 그린 것 (ⓑ는 진행도 0에서도 반투명이라 자기 자신을 기준으로 삼을 수 없다)
            sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            var orig = C.Shoot(cam, 200, 200);
            sr.sharedMaterial = mat;
            mat.SetFloat("_DissolveAmount", 0.5f);
            var shot = C.Shoot(cam, 200, 200);
            var c = C.Classify(shot, bg, orig, cam, mask);
            sb.AppendLine($"| {label} | {c.RemainPct:0.0}% | {c.IntactOfRemainPct:0.0}% |");
            shots.Add(shot);
            if (label.StartsWith("ⓒ")) cShot = shot; if (label.StartsWith("ⓓ")) dShot = shot;
            mask.Free(); Object.DestroyImmediate(cam.gameObject); Object.DestroyImmediate(go); Object.DestroyImmediate(mat); Object.DestroyImmediate(bg); Object.DestroyImmediate(orig);
        }
        restore();
        int diff = 0; var pc = cShot.GetPixels32(); var pd = dShot.GetPixels32();
        for (int i = 0; i < pc.Length; i++) if (Mathf.Abs(pc[i].r - pd[i].r) + Mathf.Abs(pc[i].g - pd[i].g) + Mathf.Abs(pc[i].b - pd[i].b) > 6) diff++;
        sb.AppendLine($"| ⓒ와 ⓓ의 화면 차이 (픽셀) | {diff}개 | |");
        var strip = C.Strip(shots.Take(3).ToArray());
        C.SavePng(strip, C.CAPTURES + "/week11_compare_alpha_clip_dissolve.png");
        foreach (var t in shots) Object.DestroyImmediate(t); Object.DestroyImmediate(strip);
        AssetDatabase.ImportAsset(C.CAPTURES + "/week11_compare_alpha_clip_dissolve.png");
        sb.Append("        읽는 법: ⓐ는 다 남았지만 반투명 · ⓑ는 일부가 잘렸지만 남은 것도 반투명 · ⓒ는 남은 픽셀이 원래 색 그대로다\n");
        sb.Append($"        그림: {C.CAPTURES}/week11_compare_alpha_clip_dissolve.png (왼쪽부터 ⓐ ⓑ ⓒ)\n        ── 다음 ── (직접) SG_HitFlash 복제 → SG_Dissolve");
        C.Save("compare", sb.ToString());
        Debug.Log(sb.ToString());
    }

    // ────────────────────────────────────────────────────────── 2-2 측정 #2

    [MenuItem(C.MENU + "2-2. 측정 #2 — 진행도만큼 사라질까", priority = 22)]
    public static void Measure2()
    {
        if (C.Busy("측정 #2")) return;
        var m = C.StudentMaterial();
        if (m == null || m.shader != C.StudentShader()) { Debug.LogWarning("[측정] #2 M_Dissolve가 없거나 Shader가 SG_Dissolve가 아니다 — (직접) 머티리얼 만들기"); return; }
        var amounts = Enumerable.Range(0, 11).Select(i => i / 10f).ToArray();
        var r = Week11Graph.RemainAt(m, amounts, out var shots);
        float start = -1, gone = -1;
        for (int i = 0; i < amounts.Length; i++) { if (start < 0 && r[i] < 99f) start = amounts[i]; if (gone < 0 && r[i] <= 0.5f) gone = amounts[i]; }
        var sb = new StringBuilder($"[측정] #2 진행도와 남은 픽셀 — 측정용 스프라이트 · M_Dissolve 값 (NoiseScale {m.GetFloat("_NoiseScale"):0.##})\n");
        sb.AppendLine("| 진행도 (_DissolveAmount) | " + string.Join(" | ", amounts.Select(a => a.ToString("0.0"))) + " |");
        sb.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", amounts.Length)));
        sb.AppendLine("| 그림 안에 남은 픽셀 | " + string.Join(" | ", r.Select(v => v.ToString("0") + "%")) + " |");
        bool even = start >= 0 && start <= 0.2f && gone >= 0.9f;
        sb.AppendLine($"| 움직이는 구간 | 처음 줄어드는 진행도 {start:0.0} · 다 사라지는 진행도 {(gone < 0 ? "—" : gone.ToString("0.0"))} | {(even ? "✓ 0~1 전체에서 고르게 줄어든다" : "⚠ 앞뒤에 화면이 안 바뀌는 구간이 있다 → Remap")} |");
        var strip = C.Strip(shots[0], shots[3], shots[5], shots[7], shots[9]);   // 0 · 0.3 · 0.5 · 0.7 · 0.9
        C.SavePng(strip, C.CAPTURES + "/week11_dissolve_steps.png");
        AssetDatabase.ImportAsset(C.CAPTURES + "/week11_dissolve_steps.png");
        foreach (var t in shots) Object.DestroyImmediate(t); Object.DestroyImmediate(strip);
        if (r[0] < 99f)
            sb.Append("        ⚠ 진행도 0인데 이미 지워진 픽셀이 있다 — Remap의 In Min Max가 기본값 (-1, 1)이면 이렇게 된다. (0, 1)로\n");
        else if (!even)
            sb.Append($"        Remap 권장: In Min Max (0, 1) · Out Min Max ({Mathf.Max(0, start - 0.1f):0.00}, {(gone < 0 ? 1 : gone):0.00}) — 진행도 0~1을 무늬가 실제로 있는 범위에 맞춘다\n");
        sb.Append("        읽는 법: 남은 픽셀 = 무늬 값이 기준보다 큰 픽셀. Simple Noise 값은 0~1 전체가 아니라 가운데에 몰려 있다\n");
        sb.Append($"        그림: {C.CAPTURES}/week11_dissolve_steps.png (진행도 0 · 0.3 · 0.5 · 0.7 · 0.9)\n");
        sb.Append(even ? "        ── 다음 ── 3단계 (직접) 경계 노드" : "        ── 다음 ── (직접) Remap 노드 → Save Asset → 2-2 다시");
        C.Save(even ? "m2_after" : "m2_before", sb.ToString());
        C.Save("m2", sb.ToString());
        Debug.Log(sb.ToString());
    }

    // ────────────────────────────────────────────────────────── 3-1 측정 #3

    static readonly (string label, Vector3 pos)[] SPOTS =
    {
        ("횃불 옆 (밝은 곳)", new Vector3(-3.6f, -0.4f, 0)),
        ("조명 밖 (어두운 곳)", new Vector3(16f, 10f, 0)),
    };

    [MenuItem(C.MENU + "3-1. 측정 #3 — 경계가 빛나며 움직일까 (Play 중)", priority = 31)]
    public static void Measure3()
    {
        if (NeedPlay("측정 #3")) return;
        var src = C.StudentMaterial();
        if (src == null || !src.HasProperty("_EdgeWidth") || !src.HasProperty("_EdgeColor")) { Debug.LogWarning("[측정] #3 M_Dissolve에 _EdgeWidth · _EdgeColor가 없다 — 3단계 (직접) 경계 노드 → Save Asset"); return; }
        var fz = new Freeze();
        var mat = new Material(src) { hideFlags = HideFlags.DontSave };
        mat.SetFloat("_FlashAmount", 0);
        Color userColor = src.GetColor("_EdgeColor"); float userWidth = src.GetFloat("_EdgeWidth");
        var sr = Dummy("CG2 Measure Sprite", new Vector3(80, 80, 0), mat);
        Camera cam = null; C.Mask mask = null; Texture2D bg = null, orig = null;
        var sb = new StringBuilder("[측정] #3 경계 발광 — 측정용 스프라이트 · 게임 정지\n");
        void End() { if (cam) Object.DestroyImmediate(cam.gameObject); mask?.Free(); if (bg) Object.DestroyImmediate(bg); if (orig) Object.DestroyImmediate(orig); Object.DestroyImmediate(sr.gameObject); Object.DestroyImmediate(mat); fz.End(); }
        // 빛나는 픽셀은 '같은 진행도 · 경계 끔(EdgeWidth 0)' 사진과 같은 프레임에 비교한다 — 경계 말고는 다를 것이 없다
        //   (처음에는 진행도 0 사진과 비교했는데, 자리를 옮긴 직후 첫 측정에서 값이 어긋났다 — 개발 테스트)
        C.Count Shot(float amount, float width, Color color, out Texture2D keep) { var c = Shot2(amount, width, color, out keep, out var r); Object.DestroyImmediate(r); return c; }
        C.Count Shot2(float amount, float width, Color color, out Texture2D keep, out Texture2D noEdge)
        {
            mat.SetFloat("_DissolveAmount", amount); mat.SetFloat("_EdgeWidth", 0); mat.SetColor("_EdgeColor", color);
            noEdge = C.Shoot(cam, 200, 200);
            mat.SetFloat("_EdgeWidth", width);
            keep = C.Shoot(cam, 200, 200);
            return C.Classify(keep, bg, noEdge, cam, mask);
        }
        void Prepare(Vector3 pos)
        {
            sr.transform.position = pos;
            if (cam) Object.DestroyImmediate(cam.gameObject); mask?.Free(); if (bg) Object.DestroyImmediate(bg); if (orig) Object.DestroyImmediate(orig);
            cam = C.ZoomCamera(sr.bounds); mask = new C.Mask(sr);
        }
        void Base()
        {
            // 배경 · 원래 그림은 재는 단계와 같은 프레임에 찍는다(앞 프레임에 찍어 두면 값이 어긋났다 — 개발 테스트)
            if (bg) Object.DestroyImmediate(bg); if (orig) Object.DestroyImmediate(orig);
            Object.DestroyImmediate(C.Shoot(cam, 200, 200));   // 버리는 한 장 — 자리를 옮긴 직후의 첫 그리기
            sr.enabled = false; bg = C.Shoot(cam, 200, 200); sr.enabled = true;
            mat.SetFloat("_DissolveAmount", 0); mat.SetFloat("_EdgeWidth", 0); orig = C.Shoot(cam, 200, 200);
        }
        Then(() => Prepare(SPOTS[0].pos));
        // ① 두께 — 진행도 0.5에서 EdgeWidth만 바꾼다
        Then(() =>
        {
            Base();
            sb.AppendLine($"① 두께 — 진행도 0.5 · EdgeColor 주황 Intensity 3 · {SPOTS[0].label}\n| EdgeWidth | 0 | 0.04 | 0.08 | 0.16 |\n|---|---|---|---|---|");
            var g = new[] { 0f, 0.04f, 0.08f, 0.16f }.Select(w => { var c = Shot(0.5f, w, Hdr(ORANGE, 3), out var t); Object.DestroyImmediate(t); return c; }).ToArray();
            sb.AppendLine("| 그림 안에서 빛나는 픽셀 | " + string.Join(" | ", g.Select(c => c.GlowPct.ToString("0.0") + "%")) + " |");
            sb.AppendLine("| 그림 안에 남은 픽셀 | " + string.Join(" | ", g.Select(c => c.RemainPct.ToString("0.0") + "%")) + " |");
        }, 1);
        // ② 움직임 — 진행도가 바뀌면 빛나는 자리가 바뀌는가
        Then(() =>
        {
            Base();
            var c0 = Shot(0f, 0.08f, Hdr(ORANGE, 3), out var t0);
            var c3 = Shot2(0.3f, 0.08f, Hdr(ORANGE, 3), out var t3, out var r3);
            var c6 = Shot2(0.6f, 0.08f, Hdr(ORANGE, 3), out var t6, out var r6);
            // 진행도 0.3에서 빛나던 픽셀이 0.6에서 어떻게 되었나 — 지워졌나 · 아직 빛나나
            var a = t3.GetPixels32(); var b = t6.GetPixels32(); var o3 = r3.GetPixels32(); var o6 = r6.GetPixels32(); var k = bg.GetPixels32();
            int both = 0, g3 = 0, erased = 0;
            bool Visible(Color32 p, Color32 back) => Mathf.Abs(p.r - back.r) + Mathf.Abs(p.g - back.g) + Mathf.Abs(p.b - back.b) > 6;
            bool Glow(Color32 p, Color32 org) => p.r - org.r >= 30 && p.r > p.b + 30;
            for (int i = 0; i < a.Length; i++)
            {
                if (!(Visible(a[i], k[i]) && Glow(a[i], o3[i]))) continue;
                g3++;
                if (!Visible(b[i], k[i])) erased++;
                else if (Glow(b[i], o6[i])) both++;
            }
            Object.DestroyImmediate(r3); Object.DestroyImmediate(r6);
            sb.AppendLine($"② 움직임 — EdgeWidth 0.08\n| 진행도 | 0 | 0.3 | 0.6 |\n|---|---|---|---|");
            sb.AppendLine($"| 빛나는 픽셀 | {c0.GlowPct:0.0}% | {c3.GlowPct:0.0}% | {c6.GlowPct:0.0}% |");
            sb.AppendLine($"| 남은 픽셀 | {c0.RemainPct:0.0}% | {c3.RemainPct:0.0}% | {c6.RemainPct:0.0}% |");
            sb.AppendLine($"| 0.3에서 빛나던 픽셀 → 0.6에서 지워짐 | | | {(g3 > 0 ? 100f * erased / g3 : 0):0.0}% |");
            sb.AppendLine($"| 0.3에서 빛나던 픽셀 → 0.6에서도 빛남 | | | {(g3 > 0 ? 100f * both / g3 : 0):0.0}% |");
            var strip = C.Strip(t0, t3, t6);
            C.SavePng(strip, C.CAPTURES + "/week11_edge_moves.png");
            Object.DestroyImmediate(strip); Object.DestroyImmediate(t0); Object.DestroyImmediate(t3); Object.DestroyImmediate(t6);
        }, 1);
        // ③ 밝기 — 어두운 곳에서 Intensity별 경계 밝기
        foreach (var spot in SPOTS)
        {
            var s = spot;
            Then(() => Prepare(s.pos));
            Then(() =>
            {
                Base();
                if (s.pos == SPOTS[0].pos) sb.AppendLine("③ 밝기 — 진행도 0.4 · EdgeWidth 0.08 · 경계 픽셀의 평균 화면 값(빨강 · 0~255)\n| 자리 | 몸 (원래) | 주황 Intensity 0 | Intensity 1 | Intensity 2 | Intensity 3 |\n|---|---|---|---|---|---|");
                // 경계 자리 = Intensity 3에서 빛나는 픽셀
                Shot2(0.4f, 0.08f, Hdr(ORANGE, 3), out var reference, out var noEdge);
                var rp = reference.GetPixels32(); var op = noEdge.GetPixels32();   // 몸 (원래) = 같은 진행도 · 경계 끔
                var edge = new List<int>();
                for (int i = 0; i < rp.Length; i++) if (rp[i].r - op[i].r >= 30 && rp[i].r > rp[i].b + 30) edge.Add(i);
                Object.DestroyImmediate(reference); Object.DestroyImmediate(noEdge);
                var cells = new List<string>();
                float body = edge.Count > 0 ? (float)edge.Average(i => (double)op[i].r) : 0;
                foreach (var inten in new[] { 0f, 1f, 2f, 3f })
                {
                    Shot(0.4f, 0.08f, Hdr(ORANGE, inten), out var t);
                    var p = t.GetPixels32();
                    cells.Add(edge.Count > 0 ? edge.Average(i => (double)p[i].r).ToString("0") : "—");
                    Object.DestroyImmediate(t);
                }
                sb.AppendLine($"| {s.label} | {body:0} | " + string.Join(" | ", cells) + " |");
            }, 1);
        }
        Then(() =>
        {
            sb.Append("        읽는 법: ① 두께만큼 빛나는 띠가 넓어진다 ② 빛나던 자리는 다음에 지워진다 — 띠가 지워지는 쪽 앞에서 함께 움직인다\n");
            sb.Append("                 ③ Sprite Lit은 Base Color에 조명을 곱한다 — 어두운 곳에서도 빛나 보이려면 Intensity를 올린다(10주차와 같다)\n");
            sb.Append($"        그림: {C.CAPTURES}/week11_edge_moves.png (진행도 0 · 0.3 · 0.6)\n        ── 다음 ── Stop → 4단계");
            C.Save("m3", sb.ToString());
            End();
            AssetDatabase.ImportAsset(C.CAPTURES + "/week11_edge_moves.png");
            Debug.Log(sb.ToString());
        }, 1);
        Run(End);
    }

    // ────────────────────────────────────────────────────────── 4-2 측정 #4

    [MenuItem(C.MENU + "4-2. 측정 #4 — 죽는 순간부터 사라질 때까지 (Play 중)", priority = 42)]
    public static void Measure4()
    {
        if (NeedPlay("측정 #4")) return;
        var fz = new Freeze(false);
        SpriteRenderer target = null; Camera cam = null; C.Mask mask = null; Texture2D orig = null, bg = null;
        var pool = Pool(); int idle0 = 0, active0 = 0; float t0 = 0; int f0 = 0;
        var samples = new List<(int f, float ms, float amount, float remain, bool active, bool sim)>();
        bool hadAmount = false; Rigidbody2D rb = null;
        var frames = new List<Texture2D>();
        void End() { if (cam) Object.DestroyImmediate(cam.gameObject); mask?.Free(); if (orig) Object.DestroyImmediate(orig); if (bg) Object.DestroyImmediate(bg); foreach (var t in frames) if (t) Object.DestroyImmediate(t); fz.End(); }
        Then(() =>
        {
            target = C.NearestEnemy(true, true);
            if (target == null) throw new Exception("스포너가 내보낸 적이 아직 없다 — 2~3초 뒤 다시 (씬에 미리 놓인 적은 풀 소속이 아니다)");
            hadAmount = !float.IsNaN(C.Amount(target));
            if (!hadAmount) throw new Exception("이 적의 머티리얼에 _DissolveAmount가 없다 — 4-1 (M_Dissolve 적용) 먼저");
            rb = target.GetComponent<Rigidbody2D>();
            idle0 = pool ? pool.IdleCount : -1; active0 = pool ? pool.ActiveCount : -1;
            var h = target.GetComponent<Health>();
            h.TakeDamage(h.MaxHp);                     // 체력 0 → 사망
            t0 = Time.time; f0 = Time.frameCount;
        }, 0);
        Then(() =>
        {
            // 사망 다음 프레임: 멈춘 자리에서 배경 · 원래 그림을 찍는다(물리가 꺼져 더 움직이지 않는다)
            cam = C.ZoomCamera(target.bounds); mask = new C.Mask(target);
            target.enabled = false; bg = ShootAlone(cam, target, 160); target.enabled = true;
            var m = target.sharedMaterial; float keep = m.GetFloat("_DissolveAmount"); float flash = m.HasProperty("_FlashAmount") ? m.GetFloat("_FlashAmount") : 0;
            m.SetFloat("_DissolveAmount", 0); if (m.HasProperty("_FlashAmount")) m.SetFloat("_FlashAmount", 0);
            orig = ShootAlone(cam, target, 160);
            m.SetFloat("_DissolveAmount", keep); if (m.HasProperty("_FlashAmount")) m.SetFloat("_FlashAmount", flash);
        }, 0);
        Until(() =>
        {
            bool active = target.gameObject.activeInHierarchy;
            float amount = C.Amount(target);
            float remain = 0;
            if (active)
            {
                var shot = ShootAlone(cam, target, 160);
                remain = C.Classify(shot, bg, orig, cam, mask).RemainPct;
                if (frames.Count < 60) frames.Add(shot); else Object.DestroyImmediate(shot);
            }
            samples.Add((Time.frameCount - f0, (Time.time - t0) * 1000, amount, remain, active, rb.simulated));
            return !active || Time.time - t0 > 5f;
        });
        Then(() =>
        {
            var sb = new StringBuilder("[측정] #4 사망 → 소멸 — 가장 가까운 적 · Health.TakeDamage(최대 체력) · 게임 진행 중\n");
            sb.AppendLine("| 시점 | 시간 | _DissolveAmount | 그림 안에 남은 픽셀 | 오브젝트 | 물리 (Rigidbody2D.simulated) |\n|---|---|---|---|---|---|");
            var alive = samples.Where(s => s.active).ToList();
            var gone = samples.FirstOrDefault(s => !s.active);
            foreach (var q in new[] { 0f, 0.25f, 0.5f, 0.75f })
            {
                if (alive.Count == 0) break;
                var s = alive[Mathf.Clamp(Mathf.RoundToInt(q * (alive.Count - 1)), 0, alive.Count - 1)];
                sb.AppendLine($"| {q * 100:0}% 지점 | {s.ms:0} ms | {s.amount:0.00} | {s.remain:0.0}% | 켜짐 | {(s.sim ? "켜짐" : "꺼짐")} |");
            }
            if (alive.Count > 0) { var s = alive[alive.Count - 1]; sb.AppendLine($"| 꺼지기 직전 | {s.ms:0} ms | {s.amount:0.00} | {s.remain:0.0}% | 켜짐 | {(s.sim ? "켜짐" : "꺼짐")} |"); }
            bool finished = gone.f > 0;
            sb.AppendLine(finished ? $"| 꺼짐 (풀로) | {gone.ms:0} ms | {gone.amount:0.00} | — | 꺼짐 | — |" : "| 5초 안에 꺼지지 않았다 | | | | | |");
            int idle1 = pool ? pool.IdleCount : -1, active1 = pool ? pool.ActiveCount : -1;
            // 개수 차이(idle +1)로 보면 소멸 0.8초 사이 스포너가 하나 꺼내 갈 때 어긋난다 — 이 적이 쉬는 줄에 들어 있는지 직접 본다
            var idleQ = pool ? typeof(ObjectPool).GetField("_idle", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(pool) as Queue<GameObject> : null;
            bool returned = idleQ != null && idleQ.Contains(target.gameObject);
            sb.AppendLine($"| 풀 | 이 적이 풀의 쉬는 줄에 있다: {(returned ? "예" : "**아니오**")} · 쉬는 적 (IdleCount) {idle0} → {idle1} · 사용 중 (ActiveCount) {active0} → {active1} | | | | |");
            if (returned && idle1 != idle0 + 1) sb.AppendLine("| 참고 | 개수가 +1이 아닌 것은 소멸하는 사이 스포너가 적을 꺼냈거나 다른 적이 죽었기 때문이다 — 게임이 계속 돈다 | | | | |");
            float lastAmount = finished ? gone.amount : (alive.Count > 0 ? alive[alive.Count - 1].amount : 0);   // 1을 넣은 같은 프레임에 꺼지므로 꺼진 뒤 머티리얼 값으로 본다
            bool frameSmooth = alive.Count >= 10;
            bool ok = finished && lastAmount >= 0.99f && frameSmooth && alive.Skip(1).All(s => !s.sim) && returned;
            sb.AppendLine($"| 판정 | {(ok ? "✓ 사라지는 과정이 보이고, 다 지운 뒤 풀로 돌아갔다" : "⚠ " + (!frameSmooth ? "과정이 너무 짧다 — 코루틴 확인 · " : "") + (lastAmount < 0.99f ? "1까지 가기 전에 꺼졌다 · " : "") + (alive.Skip(1).Any(s => s.sim) ? "죽은 뒤에도 물리가 켜져 있다 · " : "") + (!returned ? "풀로 돌아가지 않았다(Return)" : ""))} | | | | |");
            // 매 프레임 사진 중 5장을 띠로
            if (frames.Count >= 5)
            {
                var pick = new[] { 0, frames.Count / 4, frames.Count / 2, frames.Count * 3 / 4, frames.Count - 1 }.Select(i => frames[i]).ToArray();
                var strip = C.Strip(pick);
                C.SavePng(strip, C.CAPTURES + "/week11_death_frames.png");
                Object.DestroyImmediate(strip);
            }
            sb.Append($"        읽는 법: 진행도가 게임 시간에 맞춰 0 → 1로 오르는 동안 남은 픽셀이 줄고, 1이 된 뒤에 꺼진다. 물리는 죽는 순간 꺼진다\n");
            sb.Append($"        그림: {C.CAPTURES}/week11_death_frames.png (소멸 0% · 25% · 50% · 75% · 끝)\n        ── 다음 ── 5-1");
            C.Save("m4", sb.ToString()); C.Save("m4_ok", ok ? "1" : "0");
            End();
            AssetDatabase.ImportAsset(C.CAPTURES + "/week11_death_frames.png");
            Debug.Log(sb.ToString());
        }, 1);
        Run(End, true);
    }

    // ────────────────────────────────────────────────────────── 5-1 측정 #5

    [MenuItem(C.MENU + "5-1. 측정 #5 — 여러 마리가 함께 죽으면 (Play 중)", priority = 51)]
    public static void Measure5()
    {
        if (NeedPlay("측정 #5")) return;
        var fz = new Freeze(false);
        var picked = new List<SpriteRenderer>();
        float t0 = 0; int deathsD = 0; int exp0 = 0; Health hd = null;
        var sb = new StringBuilder("[측정] #5 여러 마리 — 가까운 적 4마리 · A 처치 → 0.3초 뒤 B 처치 · C는 그대로 · D는 한 프레임에 세 번 처치\n");
        void End() { fz.End(); }
        Then(() =>
        {
            var p = GameObject.FindWithTag("Player");
            picked = Object.FindObjectsByType<SpriteRenderer>()
                .Where(s => s.CompareTag("Enemy") && s.gameObject.activeInHierarchy && !s.GetComponent<Health>().IsDead && !float.IsNaN(C.Amount(s)))
                .OrderBy(s => p ? (s.transform.position - p.transform.position).sqrMagnitude : 0).Take(4).ToList();
            if (picked.Count < 4) throw new Exception($"적이 {picked.Count}마리뿐이다 — 4마리 이상 나온 뒤 다시 (4-1 적용 후 새로 나온 적이어야 한다)");
            var gm = GameManager.Instance; exp0 = gm ? gm.Score : 0;
            picked[0].GetComponent<Health>().TakeDamage(9999);     // A
            t0 = Time.time;
        }, 0);
        Until(() => Time.time - t0 >= 0.3f);
        Then(() =>
        {
            picked[1].GetComponent<Health>().TakeDamage(9999);     // B
            hd = picked[3].GetComponent<Health>();                  // D — 같은 프레임에 총알 세 발
            hd.onDeath.AddListener(() => deathsD++);
            hd.TakeDamage(9999); hd.TakeDamage(9999); hd.TakeDamage(9999);
        }, 0);
        Until(() => Time.time - t0 >= 0.6f);
        Then(() =>
        {
            sb.AppendLine("① 각자의 진행도 — A가 죽고 0.6초 뒤 같은 프레임에 읽음\n| 적 | 한 일 | _DissolveAmount | 머티리얼 |\n|---|---|---|---|");
            string[] names = { "A", "B", "C", "D" }; string[] did = { "0초에 처치", "0.3초에 처치", "그대로", "0.3초에 처치 (세 번)" };
            var mats = new HashSet<Material>();
            for (int i = 0; i < 4; i++)
            {
                var s = picked[i]; float a = s.gameObject.activeInHierarchy ? C.Amount(s) : 1;
                mats.Add(s.sharedMaterial);
                sb.AppendLine($"| {names[i]} | {did[i]} | {(s.gameObject.activeInHierarchy ? a.ToString("0.00") : "다 사라짐(꺼짐)")} | {s.sharedMaterial.name} |");
            }
            sb.AppendLine($"| 네 마리가 쓰는 머티리얼 | 서로 다른 {mats.Count}개 | | |");
            int exp1 = GameManager.Instance ? GameManager.Instance.Score : 0;
            sb.AppendLine($"② 중복 사망 — D에 한 프레임 처치 3번\n| 항목 | 값 | 판정 |\n|---|---|---|");
            sb.AppendLine($"| D의 onDeath 호출 | {deathsD}번 | {(deathsD == 1 ? "✓" : "⚠")} |");
            sb.AppendLine($"| 오른 점수 (A · B · D 세 마리) | +{exp1 - exp0} | {(exp1 - exp0 == 30 ? "✓ 10 × 3" : "ℹ 다른 적이 함께 죽었을 수 있다")} |");
        }, 0);
        // ③ 공유 머티리얼 함정 — 측정용 스프라이트 5개가 머티리얼 하나를 함께 쓸 때와 각자 복사본일 때
        Then(() =>
        {
            var src = C.StudentMaterial();
            var shared = new Material(src) { hideFlags = HideFlags.DontSave }; shared.SetFloat("_DissolveAmount", 0); shared.SetFloat("_FlashAmount", 0);
            var dummies = Enumerable.Range(0, 5).Select(i => Dummy("CG2 Shared " + i, new Vector3(80 + i * 1.2f, 80, 0), shared)).ToList();
            int CountChanged(Func<int, Material> matOf, Action<Material> change)
            {
                for (int i = 0; i < 5; i++) dummies[i].sharedMaterial = matOf(i);
                var restore = C.Bright();
                // 가로 6칸(1.2 × 5) × 세로 1.2 — 600 × 120 픽셀 한 장에 다섯을 나란히 찍는다
                var cam = C.ZoomCamera(new Bounds(new Vector3(80 + 2.4f, 80, 0), new Vector3(6f, 1.2f, 0)));
                cam.orthographicSize = 0.6f;
                float left = 80 + 2.4f - 3f;                           // 화면 왼쪽 끝의 월드 x (가로 6칸 = 0.6 × 2 × 600/120)
                var before = C.Shoot(cam, 600, 120);
                change(dummies[0].sharedMaterial);                     // "A만 죽었다" — 첫 번째 렌더러의 머티리얼만 바꾼다
                var after = C.Shoot(cam, 600, 120);
                restore();
                var pb = before.GetPixels32(); var pa = after.GetPixels32();
                int n = 0;
                for (int i = 0; i < 5; i++)
                {
                    // i번째 스프라이트 자리(중심 ± 0.5칸)에서 바뀐 픽셀이 있는지
                    float cx = 80 + i * 1.2f;
                    int x0 = Mathf.Clamp((int)((cx - 0.5f - left) / 6f * 600), 0, 599), x1 = Mathf.Clamp((int)((cx + 0.5f - left) / 6f * 600), 0, 599);
                    int changed = 0;
                    for (int y = 0; y < 120; y++) for (int x = x0; x <= x1; x++) { int k = y * 600 + x; if (Mathf.Abs(pb[k].r - pa[k].r) + Mathf.Abs(pb[k].g - pa[k].g) + Mathf.Abs(pb[k].b - pa[k].b) > 30) changed++; }
                    if (changed > 50) n++;
                }
                Object.DestroyImmediate(before); Object.DestroyImmediate(after); Object.DestroyImmediate(cam.gameObject);
                return n;
            }
            int sharedN = CountChanged(i => shared, m => m.SetFloat("_DissolveAmount", 0.5f));
            var copies = Enumerable.Range(0, 5).Select(i => { var c = new Material(src) { hideFlags = HideFlags.DontSave }; c.SetFloat("_DissolveAmount", 0); c.SetFloat("_FlashAmount", 0); return c; }).ToList();
            int copyN = CountChanged(i => copies[i], m => m.SetFloat("_DissolveAmount", 0.5f));
            sb.AppendLine("③ 머티리얼을 함께 쓰면? — 측정용 스프라이트 5개 · 첫 번째의 머티리얼에 진행도 0.5\n| 방식 | 화면이 바뀐 스프라이트 |\n|---|---|");
            sb.AppendLine($"| 다섯이 머티리얼 하나를 함께 씀 (sharedMaterial · 에셋을 직접 바꾼 것과 같다) | {sharedN} / 5 |");
            sb.AppendLine($"| 각자 복사본 (.material — HitReceiver · EnemyAI가 쓰는 방식) | {copyN} / 5 |");
            foreach (var d in dummies) Object.DestroyImmediate(d.gameObject);
            Object.DestroyImmediate(shared); foreach (var c in copies) Object.DestroyImmediate(c);
            sb.Append("        읽는 법: ① 진행도가 적마다 다르다 — 각자 복사본이라 섞이지 않는다 ② 세 번 맞아도 사망은 한 번 ③ 하나를 함께 쓰면 한 마리 값이 모두에게 간다\n        ── 다음 ── 5-2");
            C.Save("m5", sb.ToString()); C.Save("m5_dup", deathsD.ToString()); C.Save("m5_copy", copyN.ToString());
            End();
            Debug.Log(sb.ToString());
        }, 1);
        Run(End, true);
    }

    // ────────────────────────────────────────────────────────── 5-2 측정 #6

    [MenuItem(C.MENU + "5-2. 측정 #6 — 60마리를 잡아도 적이 계속 나올까 (Play 중)", priority = 52)]
    public static void Measure6()
    {
        if (NeedPlay("측정 #6")) return;
        var spawner = Object.FindAnyObjectByType<EnemySpawner>();
        var pool = Pool();
        if (spawner == null || pool == null) { Debug.LogWarning("[측정] #6 EnemySpawner · ObjectPool이 없다 — Play 직후라면 1초 뒤 다시"); return; }
        var spawn = typeof(EnemySpawner).GetMethod("Spawn", BindingFlags.Instance | BindingFlags.NonPublic);
        int maxEnemies = (int)typeof(EnemySpawner).GetField("maxEnemies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner);
        var fz = new Freeze(false);
        var playerHp = GameObject.FindWithTag("Player")?.GetComponent<Health>();
        int kills = 0, made0 = pool.transform.childCount, batch = 0; float wait = 0;
        var reusedDead = new HashSet<Health>();   // 풀에서 나왔는데 이미 죽어 있던 적(같은 적은 한 번만 센다)
        var sb = new StringBuilder($"[측정] #6 풀 장부 — 10마리씩 내보내고 바로 처치 × 6번 (스포너의 Spawn을 직접 부른다 · 최대 {maxEnemies}마리)\n");
        sb.AppendLine("| 처치 수 | 풀 소속 살아 있는 적 (실제) | 풀이 '사용 중'으로 셈 | 쉬는 적 | 만들어진 적 오브젝트 | 스포너가 새 적을 내보낼까 |\n|---|---|---|---|---|---|");
        int PoolAlive() => pool.GetComponentsInChildren<Health>().Count(h => !h.IsDead);   // 풀 소속만 (씬에 미리 놓인 적은 뺀다)
        void Row() => sb.AppendLine($"| {kills} | {PoolAlive()} | {pool.ActiveCount} | {pool.IdleCount} | {pool.transform.childCount} | {(pool.ActiveCount < maxEnemies ? "예" : "**아니오** (사용 중 ≥ 최대)")} |");
        void End() { fz.End(); }
        float start = 0;
        Then(() => start = Time.time, 0);
        Until(() => Time.time - start >= 1.2f);   // 앞 측정에서 타 들어가던 적이 다 사라질 때까지
        Then(() => Row(), 0);
        for (int b = 0; b < 6; b++)
        {
            Then(() =>
            {
                playerHp?.Heal(9999);
                for (int i = 0; i < 10; i++) spawn.Invoke(spawner, null);
            }, 2);
            Then(() =>
            {
                foreach (var h in pool.GetComponentsInChildren<Health>())
                {
                    if (!h.gameObject.activeInHierarchy) continue;
                    if (h.IsDead) { if (!(C.Amount(h.GetComponent<SpriteRenderer>()) > 0.001f)) reusedDead.Add(h); continue; }   // 풀에서 나왔는데 이미 죽은 상태 — 체력이 되돌려지지 않았다
                    h.TakeDamage(9999); kills++;
                }
                wait = Time.time;
            }, 0);
            Until(() => Time.time - wait >= 1.2f);   // 소멸이 끝나 풀로 돌아갈 시간
            Then(() => { batch++; if (batch == 3 || batch == 6) Row(); }, 0);
        }
        Then(() =>
        {
            sb.AppendLine($"| 풀에서 나왔는데 이미 죽어 있던 적 | {reusedDead.Count}마리 | | | | |");
            bool ok = pool.ActiveCount == PoolAlive() && pool.ActiveCount < maxEnemies && reusedDead.Count == 0;
            string why = reusedDead.Count > 0 ? "풀에서 나온 적이 죽은 채다 — Health를 OnEnable에서 되돌린다" : "죽은 적이 풀로 돌아가지 않는다 — 소멸이 끝나면 pool.Return";
            sb.AppendLine($"| 판정 | {(ok ? "✓ 장부와 실제가 같고, 적이 계속 나온다" : "⚠ " + why)} | | | | |");
            sb.Append($"        읽는 법: '사용 중'이 실제 살아 있는 적보다 크면 죽은 적이 장부에 남은 것이다. 최대 {maxEnemies}에 닿으면 SpawnLoop가 더는 적을 내보내지 않는다\n        ── 다음 ── Stop → 6단계");
            C.Save("m6", sb.ToString()); C.Save("m6_ok", ok ? "1" : "0");
            End();
            Debug.Log(sb.ToString());
        }, 0);
        Run(End, true);
    }

    // ────────────────────────────────────────────────────────── 6-1 스타일 비교

    public static readonly (string name, float scale, float width, Color color, float intensity)[] STYLES =
    {
        ("화염", 40, 0.08f, new Color(1f, 0.42f, 0.08f), 3),
        ("마법", 80, 0.15f, new Color(0.55f, 0.2f, 1f), 3),
        ("재 (가는 경계)", 120, 0.03f, new Color(1f, 0.85f, 0.6f), 3),
    };

    [MenuItem(C.MENU + "6-1. 스타일 비교 — 화염 · 마법 · 재", priority = 61)]
    public static void Styles()
    {
        if (C.Busy("6-1 스타일")) return;
        var src = C.StudentMaterial();
        if (src == null || !src.HasProperty("_EdgeColor")) { Debug.LogWarning("[비교] 6-1 M_Dissolve에 경계 속성이 없다 — 3단계"); return; }
        var sb = new StringBuilder("[비교] 6-1 스타일 — 같은 그래프 · 값만 다르게 · 진행도 0.45 · 횃불 옆(게임 조명 그대로)\n| 스타일 | NoiseScale | EdgeWidth | EdgeColor (Intensity) |\n|---|---|---|---|\n");
        var shots = new List<Texture2D>();
        var all = new List<(string, float, float, Color, float)>(STYLES.Select(s => (s.name, s.scale, s.width, s.color, s.intensity)));
        all.Insert(0, ("지금 M_Dissolve", src.GetFloat("_NoiseScale"), src.GetFloat("_EdgeWidth"), src.GetColor("_EdgeColor"), float.NaN));
        foreach (var (name, scale, width, color, inten) in all)
        {
            var m = new Material(src) { hideFlags = HideFlags.DontSave };
            m.SetFloat("_NoiseScale", scale); m.SetFloat("_EdgeWidth", width); m.SetColor("_EdgeColor", float.IsNaN(inten) ? color : Hdr(color, inten)); m.SetFloat("_FlashAmount", 0);
            Week11Graph.RemainAt(m, new[] { 0.45f }, out var s, SPOTS[0].pos, false);   // 게임 조명 아래(횃불 옆)에서 본다
            shots.Add(s[0]);
            Object.DestroyImmediate(m);
            // 색 창 단위로 적는다: 기본 색 · Intensity (저장값 = 기본 색 × 2^Intensity)
            float k = float.IsNaN(inten) ? Mathf.Max(1, color.maxColorComponent) : 1;
            Color baseC = float.IsNaN(inten) ? color / k : color;
            float shown = float.IsNaN(inten) ? Mathf.Log(k, 2) : inten;
            sb.AppendLine($"| {name} | {scale:0.##} | {width:0.###} | ({baseC.r:0.##}, {baseC.g:0.##}, {baseC.b:0.##}) · {shown:0.#} |");
        }
        var strip = C.Strip(shots.ToArray());
        C.SavePng(strip, C.CAPTURES + "/week11_styles.png");
        AssetDatabase.ImportAsset(C.CAPTURES + "/week11_styles.png");
        foreach (var t in shots) Object.DestroyImmediate(t); Object.DestroyImmediate(strip);
        sb.Append($"        그림: {C.CAPTURES}/week11_styles.png (왼쪽부터 지금 · 화염 · 마법 · 재)\n        읽는 법: 그래프는 하나다. 머티리얼 값만 바꿔도 다른 소멸이 된다 — 마음에 드는 값을 M_Dissolve Inspector에 넣는다\n        ── 다음 ── 6-2");
        C.Save("styles", sb.ToString());
        Debug.Log(sb.ToString());
    }
}
