using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using C = Week10Common;
using Object = UnityEngine.Object;

/// <summary>
/// 10주차 측정 — 모두 Play 중에 실행한다. 8주차와 같은 규칙:
///   게임을 멈추고(timeScale 0), 임시 카메라로 대상만 크게 한 번 찍는다. 조건마다 프레임을 기다린다.
///   같은 값이 나오도록 #2 · #3 · #4는 고정 위치에 둔 '측정용 스프라이트'를 쓴다.
///   1-1 #1 지금의 피격 플래시 · 2-2 #2 Lerp 검사 · 3-1 #3 어두운 곳의 플래시 · 4-3 #4 바꾸기 전후 · 5-1 #5 피격 신호 · 5-2 #6 적 20마리 비용
/// </summary>
public static class Week10Measure
{
    // ────────────────────────────────────────────────────────── 프레임 단위 실행기 (8주차와 같다)

    class Step { public Action act; public int wait; }
    static readonly Queue<Step> _steps = new();
    static int _until;
    static Action _onAbort;

    static void Run(Action onAbort)
    {
        _onAbort = onAbort;
        _until = Time.frameCount + 30;   // Play 직후의 불안정한 프레임을 피한다
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Then(Action a, int wait = 6) => _steps.Enqueue(new Step { act = a, wait = wait });

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Halt(); _onAbort?.Invoke(); Debug.LogWarning("[측정] Play가 끝나 중단했다."); return; }
        if (Time.frameCount < _until) return;
        if (_steps.Count == 0) { Halt(); return; }
        var s = _steps.Dequeue();
        try { s.act(); }
        catch (Exception e) { Halt(); _onAbort?.Invoke(); Debug.LogException(e); return; }
        _until = Time.frameCount + s.wait;
    }

    static void Halt() { EditorApplication.update -= Tick; _steps.Clear(); }

    static bool NeedPlay(string title)
    {
        if (EditorApplication.isPlaying && _steps.Count == 0) return false;
        EditorUtility.DisplayDialog(title, _steps.Count > 0 ? "측정이 이미 진행 중입니다." : "Play 중에 실행하세요.", "확인");
        return true;
    }

    /// <summary>게임을 멈추고, 끝나면 되돌린다.</summary>
    class Freeze
    {
        readonly float _ts;
        readonly List<Behaviour> _off = new();
        public Freeze()
        {
            _ts = Time.timeScale; Time.timeScale = 0;
            // 8주차 횃불 깜빡임(CG2Torch)을 끈다 — 멈춘 순간의 깜빡임 값이 남으면 측정이 1~2씩 흔들렸다(테스트에서 확인)
            // 꺼지면 원래 세기로 돌아간다. 8주차 코드에 의존하지 않도록 타입 이름으로 찾는다
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
    static SpriteRenderer Dummy(string name, Vector3 pos, Material mat, string tagFrom = "Enemy")
    {
        Sprite sp = null;
        if (tagFrom == "Player") sp = GameObject.FindWithTag("Player")?.GetComponent<SpriteRenderer>()?.sprite;
        if (sp == null) sp = AssetDatabase.LoadAssetAtPath<GameObject>(C.ENEMY_PREFAB).GetComponent<SpriteRenderer>().sprite;
        var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sortingLayerName = "Entity";
        sr.sharedMaterial = mat;
        return sr;
    }

    /// <summary>intensity = HDR 색 창의 Intensity 칸과 같은 단위 (흰색 × 2^intensity를 색으로 넣는다 · Linear 프로젝트는 넘길 때 선형으로 바뀐다)</summary>
    static void SetFlash(SpriteRenderer sr, float amount, float intensity = float.NaN)
    {
        var mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(mpb);
        mpb.SetFloat("_FlashAmount", amount);
        if (!float.IsNaN(intensity)) { float v = Mathf.Pow(2, intensity); mpb.SetColor("_FlashColor", new Color(v, v, v, 1)); }
        sr.SetPropertyBlock(mpb);
    }

    // ────────────────────────────────────────────────────────── 1-1 측정 #1

    [MenuItem(C.MENU + "1-1. 측정 #1 — 지금의 피격 플래시는 보일까 (Play 중)", priority = 11)]
    public static void Measure1()
    {
        if (NeedPlay("측정 #1")) return;
        var fz = new Freeze();
        SpriteRenderer target = null; Camera cam = null; C.Mask mask = null; Texture2D before = null;
        void End() { if (cam) Object.DestroyImmediate(cam.gameObject); mask?.Free(); fz.End(); }
        Then(() =>
        {
            target = C.NearestEnemy();
            if (target == null) throw new Exception("화면에 적이 없다 — 적이 나온 뒤 다시");
            cam = C.ZoomCamera(target.bounds); mask = new C.Mask(target);
            before = C.Shoot(cam, 320, 320);
            var hr = target.GetComponent<HitReceiver>();
            hr.OnHit();                                    // 맞았다는 신호 — 지금 코드는 SpriteRenderer.color를 흰색으로 바꾼다
        });
        Then(() =>
        {
            var after = C.Shoot(cam, 320, 320);
            var a = C.Measure(before, cam, target, mask); var b = C.Measure(after, cam, target, mask);
            float changed = C.ChangedInside(before, after, cam, target, mask);
            var sb = new StringBuilder("[측정] #1 지금의 피격 플래시 — 가장 가까운 적 · HitReceiver.OnHit() 호출 · 게임 정지\n| 항목 | 맞기 전 | 맞은 직후 |\n|---|---|---|\n");
            sb.AppendLine($"| SpriteRenderer.color | (1, 1, 1) | ({target.color.r:0.##}, {target.color.g:0.##}, {target.color.b:0.##}) |");
            sb.AppendLine($"| 그림 안 평균 밝기 (0~255) | {a.inside:0} | {b.inside:0} |");
            sb.AppendLine($"| 그림 안에서 30 이상 바뀐 픽셀 | — | **{changed:0.0}%** |");
            sb.Append("        읽는 법: SpriteRenderer.color는 원래 색에 곱하는 값이다. 흰색(1, 1, 1)을 곱하면 아무것도 바뀌지 않는다\n        ── 다음 ── Stop → (직접) Sprite Lit Shader Graph 만들기 → 1-2");
            C.Save("m1", sb.ToString()); C.Save("m1_changed", changed.ToString("0.0"));
            C.SavePng(before, C.STORE + "/hit_before.png");
            End();
            Debug.Log(sb.ToString());
        }, 1);
        Run(End);
    }

    // ────────────────────────────────────────────────────────── 2-2 측정 #2

    [MenuItem(C.MENU + "2-2. 측정 #2 — Lerp 계산이 맞을까 (Play 중)", priority = 22)]
    public static void Measure2()
    {
        if (NeedPlay("측정 #2")) return;
        var shader = C.StudentShader();
        if (shader == null) { Debug.LogWarning("[측정] #2 SG_HitFlash가 없다 — 1-2"); return; }
        var fz = new Freeze();
        var light = C.Named(C.ENTITY_LIGHT);
        float li = light ? light.intensity : 1; Color lc = light ? light.color : Color.white;
        // 다른 조명이 닿지 않는 먼 곳 · 캐릭터 전역 조명을 흰색 1로 → 화면 값 ≈ 그래프가 낸 색
        var pos = new Vector3(60, 60, 0);
        var mat = new Material(shader) { hideFlags = HideFlags.DontSave };
        var sr = Dummy("CG2 Measure Sprite", pos, mat);
        var refSr = Dummy("CG2 Measure Ref", pos + new Vector3(5, 0, 0), C.DefaultMaterial());
        refSr.enabled = false;
        Camera cam = null; C.Mask mask = null;
        var shots = new Dictionary<float, C.Region>();
        C.Region refR = default; float outsideBg = 0; Texture2D t1 = null;
        void End()
        {
            if (light) { light.intensity = li; light.color = lc; }
            if (cam) Object.DestroyImmediate(cam.gameObject);
            mask?.Free(); Object.DestroyImmediate(sr.gameObject); Object.DestroyImmediate(refSr.gameObject); Object.DestroyImmediate(mat); fz.End();
        }
        Then(() => { if (light) { light.intensity = 1; light.color = Color.white; } cam = C.ZoomCamera(sr.bounds); mask = new C.Mask(sr); });
        // 기준: 같은 자리에 기본 머티리얼(Sprite-Lit-Default)
        Then(() => { sr.enabled = false; refSr.transform.position = pos; refSr.enabled = true; });
        Then(() => { var t = C.Shoot(cam, 320, 320); refR = C.Measure(t, cam, refSr, mask); Object.DestroyImmediate(t); refSr.enabled = false; sr.enabled = true; }, 1);
        foreach (var a in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
        {
            float k = a;
            Then(() => SetFlash(sr, k, 0f));
            Then(() =>
            {
                var t = C.Shoot(cam, 320, 320);
                shots[k] = C.Measure(t, cam, sr, mask);
                if (k == 1f) t1 = t; else Object.DestroyImmediate(t);
            }, 1);
        }
        Then(() => { sr.enabled = false; });
        Then(() =>
        {
            var bg = C.Shoot(cam, 320, 320);
            outsideBg = C.Measure(bg, cam, sr, mask).outside;
            // 그림 바깥(투명해야 할 곳)이 배경과 달라진 비율
            int w = 320, h = 320; float halfH = cam.orthographicSize; Vector2 c = cam.transform.position; var b = sr.bounds;
            var p1 = t1.GetPixels32(); var p0 = bg.GetPixels32(); int n = 0, ch = 0;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float wx = c.x - halfH + (x + 0.5f) / w * 2 * halfH, wy = c.y - halfH + (y + 0.5f) / h * 2 * halfH;
                if (wx < b.min.x || wx > b.max.x || wy < b.min.y || wy > b.max.y) continue;
                if (mask.Inside((wx - b.min.x) / b.size.x, (wy - b.min.y) / b.size.y)) continue;
                n++; if (Mathf.Abs(C.Gray(p1[y * w + x]) - C.Gray(p0[y * w + x])) >= 30) ch++;
            }
            float outsidePainted = n > 0 ? 100f * ch / n : 0;
            Object.DestroyImmediate(bg); Object.DestroyImmediate(t1);

            float l0 = shots[0].insideLinear, l1 = shots[1f].insideLinear;
            var sb = new StringBuilder("[측정] #2 Lerp 검사 — 측정용 스프라이트 · 다른 조명 없음 · 캐릭터 전역 조명 흰색 1 · FlashColor 흰색\n");
            sb.AppendLine("| 검사 | 값 | 판정 |\n|---|---|---|");
            float diff0 = Mathf.Abs(shots[0].inside - refR.inside);
            sb.AppendLine($"| FlashAmount 0 = 원래 그림인가 (기본 머티리얼과 밝기 차) | {diff0:0.0} | {(diff0 <= 3 ? "✓" : "⚠ _MainTex 연결 · Reference 확인")} |");
            sb.AppendLine($"| 그림 바깥이 칠해진 비율 (FlashAmount 1) | {outsidePainted:0.0}% | {(outsidePainted <= 1 ? "✓" : "⚠ Sample Texture 2D의 A → Alpha 블록 연결")} |");
            sb.AppendLine("| FlashAmount (T) | 화면 값 (0~255) | 측정 (Linear 밝기) | Lerp 계산 | 차이 |\n|---|---|---|---|---|");
            foreach (var kv in shots)
            {
                float expect = Mathf.Lerp(l0, l1, kv.Key);
                sb.AppendLine($"| {kv.Key:0.00} | {kv.Value.inside:0} | {kv.Value.insideLinear:0.000} | {expect:0.000} | {kv.Value.insideLinear - expect:+0.000;-0.000;0.000} |");
            }
            sb.Append("        Linear 밝기 = 픽셀마다 화면 값을 Linear(0~1)로 바꿔 평균한 값. Lerp 계산 = lerp(T=0 값, T=1 값, T). 셰이더는 Linear 값으로 섞는다\n        ── 다음 ── Stop → 3단계");
            C.Save("m2", sb.ToString()); C.Save("m2_outside", outsidePainted.ToString("0.0"));
            End();
            Debug.Log(sb.ToString());
        }, 1);
        Run(End);
    }

    // ────────────────────────────────────────────────────────── 3-1 측정 #3

    static readonly (string label, Vector3 pos)[] SPOTS =
    {
        ("횃불 옆 (밝은 곳)", new Vector3(-3.6f, -0.4f, 0)),
        ("기둥 그늘", new Vector3(-0.6f, 3.6f, 0)),
        ("조명 밖 (어두운 곳)", new Vector3(16f, 10f, 0)),
    };

    [MenuItem(C.MENU + "3-1. 측정 #3 — 어두운 곳에서도 하얄까 (Play 중)", priority = 31)]
    public static void Measure3()
    {
        if (NeedPlay("측정 #3")) return;
        var shader = C.StudentShader();
        if (shader == null) { Debug.LogWarning("[측정] #3 SG_HitFlash가 없다"); return; }
        var fz = new Freeze();
        var mat = new Material(shader) { hideFlags = HideFlags.DontSave };
        var player = GameObject.FindWithTag("Player"); var ppos = player ? player.transform.position : Vector3.zero;
        if (player) player.transform.position = Vector3.zero;   // 랜턴 위치 고정
        var hidden = new List<SpriteRenderer>();
        foreach (var e in Object.FindObjectsByType<SpriteRenderer>()) if (e.CompareTag("Enemy") && e.enabled) { e.enabled = false; hidden.Add(e); }
        var srs = new List<SpriteRenderer>();
        foreach (var s in SPOTS) srs.Add(Dummy("CG2 Measure Sprite", s.pos, mat));
        var rows = new string[SPOTS.Length];
        var vals = new float[SPOTS.Length, 4];
        var ints = new[] { float.NaN, 0f, 1f, 2f };   // 플래시 없음 · 흰색(Intensity 0) · Intensity 1 · Intensity 2
        void End()
        {
            foreach (var s in srs) if (s) Object.DestroyImmediate(s.gameObject);
            foreach (var e in hidden) if (e) e.enabled = true;
            if (player) player.transform.position = ppos;
            Object.DestroyImmediate(mat); fz.End();
        }
        for (int m = 0; m < ints.Length; m++)
        {
            int mi = m;
            Then(() => { foreach (var s in srs) SetFlash(s, float.IsNaN(ints[mi]) ? 0 : 1, float.IsNaN(ints[mi]) ? 0 : ints[mi]); }, 8);
            Then(() =>
            {
                for (int i = 0; i < srs.Count; i++)
                {
                    var cam = C.ZoomCamera(srs[i].bounds); var mask = new C.Mask(srs[i]);
                    var t = C.Shoot(cam, 240, 240);
                    vals[i, mi] = C.Measure(t, cam, srs[i], mask).inside;
                    Object.DestroyImmediate(t); Object.DestroyImmediate(cam.gameObject); mask.Free();
                }
            }, 1);
        }
        Then(() =>
        {
            var sb = new StringBuilder("[측정] #3 어두운 곳의 플래시 — 측정용 적 그림 3곳 · FlashAmount 1 · 그림 안 평균 밝기 (0~255)\n");
            sb.AppendLine("| 자리 | 플래시 없음 | 흰색 (Intensity 0) | Intensity 1 | Intensity 2 |\n|---|---|---|---|---|");
            for (int i = 0; i < SPOTS.Length; i++)
                sb.AppendLine($"| {SPOTS[i].label} | {vals[i, 0]:0} | {vals[i, 1]:0} | {vals[i, 2]:0} | **{vals[i, 3]:0}** |");
            sb.Append("        읽는 법: Sprite Lit은 Base Color에 조명을 곱한다. 흰색(1)도 어두운 곳에서는 회색이 된다. 1보다 큰 HDR 색을 주면 어두운 곳에서도 하얗다\n        ── 다음 ── Stop → 3단계 (직접) FlashColor Mode = HDR · 노멀 맵 노드");
            C.Save("m3", sb.ToString());
            C.Save("m3_dark", $"{vals[2, 1]:0},{vals[2, 3]:0}");
            End();
            Debug.Log(sb.ToString());
        }, 1);
        Run(End);
    }

    // ────────────────────────────────────────────────────────── 4-3 측정 #4

    static Light2D MeasureLight()
    {
        var l = new GameObject("CG2 Measure Light") { hideFlags = HideFlags.DontSave }.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        var so = new SerializedObject(l);
        var p = so.FindProperty("m_ApplyToSortingLayers"); var ls = SortingLayer.layers;
        p.arraySize = ls.Length; for (int i = 0; i < ls.Length; i++) p.GetArrayElementAtIndex(i).intValue = ls[i].id;
        so.FindProperty("m_NormalMapQuality").intValue = (int)Light2D.NormalMapQuality.Accurate;
        so.FindProperty("m_NormalMapDistance").floatValue = 1f;
        so.FindProperty("m_ShadowsEnabled").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        l.pointLightInnerRadius = 9.9f; l.pointLightOuterRadius = 10f; l.intensity = 1f;   // 거리 감쇠 없음 (7주차 측정 조명)
        return l;
    }

    static Vector2 Halves(Texture2D t, Camera cam, SpriteRenderer sr, C.Mask m)
    {
        int w = t.width, h = t.height; float halfH = cam.orthographicSize; Vector2 c = cam.transform.position; var b = sr.bounds;
        var px = t.GetPixels32(); double l = 0, r = 0; int nl = 0, nr = 0;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float wx = c.x - halfH + (x + 0.5f) / w * 2 * halfH, wy = c.y - halfH + (y + 0.5f) / h * 2 * halfH;
            if (wx < b.min.x || wx > b.max.x || wy < b.min.y || wy > b.max.y) continue;
            if (!m.Inside((wx - b.min.x) / b.size.x, (wy - b.min.y) / b.size.y)) continue;
            if (wx < b.center.x) { l += C.Gray(px[y * w + x]); nl++; } else { r += C.Gray(px[y * w + x]); nr++; }
        }
        return new Vector2(nl > 0 ? (float)(l / nl) : 0, nr > 0 ? (float)(r / nr) : 0);
    }

    [MenuItem(C.MENU + "4-3. 측정 #4 — 바꾸기 전후 비용 · 노멀 맵 (Play 중)", priority = 43)]
    public static void Measure4()
    {
        if (NeedPlay("측정 #4")) return;
        var student = C.StudentMaterial();
        if (student == null) { Debug.LogWarning("[측정] #4 M_HitFlash가 없다 — 4-1"); return; }
        var fz = new Freeze();
        var def = C.DefaultMaterial();
        var all = new List<SpriteRenderer>(); var orig = new List<Material>();
        foreach (var s in Object.FindObjectsByType<SpriteRenderer>()) if (s.CompareTag("Enemy") || s.CompareTag("Player")) { all.Add(s); orig.Add(s.sharedMaterial); }
        var cost = new string[2];
        var feel = new float[2];
        SpriteRenderer dummy = null; Light2D ml = null;
        void End()
        {
            for (int i = 0; i < all.Count; i++) if (all[i]) all[i].sharedMaterial = orig[i];
            if (dummy) Object.DestroyImmediate(dummy.gameObject); if (ml) Object.DestroyImmediate(ml.gameObject);
            fz.End();
        }
        var mats = new[] { def, student };
        for (int k = 0; k < 2; k++)
        {
            int kk = k;
            Then(() => { foreach (var s in all) s.sharedMaterial = mats[kk]; }, 30);
            Then(() => cost[kk] = $"| {(kk == 0 ? "Sprite-Lit-Default (전)" : student.name + " (후)")} | {UnityStats.drawCalls} | {UnityStats.setPassCalls} |", 1);
        }
        // 노멀 맵 — 플레이어 그림을 먼 곳에 두고 측정 조명을 왼쪽 · 오른쪽에 둔다(7주차 입체감 점수)
        var pos = new Vector3(-60, 60, 0);
        for (int k = 0; k < 2; k++)
        {
            int kk = k; Vector2 L = default;
            Then(() =>
            {
                if (dummy) Object.DestroyImmediate(dummy.gameObject);
                dummy = Dummy("CG2 Measure Sprite", pos, mats[kk], "Player");
                ml ??= MeasureLight();
                ml.transform.position = pos + new Vector3(-1.3f, 0.3f, 0);
            }, 8);
            Then(() => { var cam = C.ZoomCamera(dummy.bounds); var m = new C.Mask(dummy); var t = C.Shoot(cam, 240, 240); L = Halves(t, cam, dummy, m); Object.DestroyImmediate(t); Object.DestroyImmediate(cam.gameObject); m.Free(); ml.transform.position = pos + new Vector3(1.3f, 0.3f, 0); }, 8);
            Then(() => { var cam = C.ZoomCamera(dummy.bounds); var m = new C.Mask(dummy); var t = C.Shoot(cam, 240, 240); var R = Halves(t, cam, dummy, m); feel[kk] = ((L.x - L.y) - (R.x - R.y)) / 2f; Object.DestroyImmediate(t); Object.DestroyImmediate(cam.gameObject); m.Free(); }, 1);
        }
        Then(() =>
        {
            var sb = new StringBuilder("[측정] #4 머티리얼 바꾸기 전후 — 같은 프레임 · 게임 정지 · Player와 Enemy 전부\n| 머티리얼 | DrawCalls | SetPass |\n|---|---|---|\n");
            foreach (var c in cost) sb.AppendLine(c);
            sb.AppendLine("| 노멀 맵 반응 (7주차 입체감 점수 · 측정 조명 좌우) | 점수 |\n|---|---|");
            sb.AppendLine($"| Sprite-Lit-Default | {feel[0]:0.0} |");
            sb.AppendLine($"| {student.name} | **{feel[1]:0.0}** |");
            sb.Append(feel[1] < feel[0] * 0.5f
                ? "        ⚠ 입체감이 사라졌다 — 그래프에 _NormalMap → Sample Texture 2D(Type Normal) → Normal (Tangent Space) 연결\n"
                : "        ✓ 7주차 노멀 맵 반응이 그대로다\n");
            sb.Append("        ── 다음 ── Stop → 5단계 (직접) HitReceiver 고치기");
            C.Save("m4", sb.ToString()); C.Save("m4_feel", $"{feel[0]:0.0},{feel[1]:0.0}");
            End();
            Debug.Log(sb.ToString());
        }, 1);
        Run(End);
    }

    // ────────────────────────────────────────────────────────── 5-1 측정 #5

    [MenuItem(C.MENU + "5-1. 측정 #5 — 피격 신호가 GPU 플래시로 (Play 중)", priority = 51)]
    public static void Measure5()
    {
        if (NeedPlay("측정 #5")) return;
        var fz = new Freeze();
        SpriteRenderer target = null; Camera cam = null; C.Mask mask = null; Texture2D before = null, peak = null;
        var trace = new List<string>();
        float t0 = 0; int frames = 0; float peakAmount = 0, changed = 0;
        C.Region ra = default, rb = default;
        var mpb = new MaterialPropertyBlock();
        // 스크립트가 값을 넣는 곳은 두 가지다 — 머티리얼 복사본(.material) 또는 MaterialPropertyBlock. 둘 다 읽는다
        float Amount() { target.GetPropertyBlock(mpb); if (!mpb.isEmpty && mpb.HasFloat("_FlashAmount")) return mpb.GetFloat("_FlashAmount"); var sm = target.sharedMaterial; return sm != null && sm.HasProperty("_FlashAmount") ? sm.GetFloat("_FlashAmount") : 0; }
        void End() { if (cam) Object.DestroyImmediate(cam.gameObject); mask?.Free(); fz.End(); }
        Then(() =>
        {
            target = C.NearestEnemy();
            if (target == null) throw new Exception("화면에 적이 없다");
            cam = C.ZoomCamera(target.bounds); mask = new C.Mask(target);
            before = C.Shoot(cam, 320, 320);
            target.GetComponent<HitReceiver>().OnHit();     // 피격 신호 (Health.TakeDamage가 부르는 것과 같다)
            peakAmount = Amount();
            // 신호를 보낸 같은 프레임에 찍는다 — 플래시는 게임이 멈춰도 줄어들어야 하므로 기다리면 안 된다
            // 적이 움직이기 전에 잰다 — 움직인 뒤에 재면 그림 영역이 어긋난다(테스트에서 확인)
            peak = C.Shoot(cam, 320, 320);
            ra = C.Measure(before, cam, target, mask); rb = C.Measure(peak, cam, target, mask);
            changed = C.ChangedInside(before, peak, cam, target, mask);
        }, 3);
        Then(() =>
        {
            // 캡처에 걸린 시간이 섞이지 않게 신호를 한 번 더 보내 처음부터 다시 줄어들게 한다.
            // 게임은 멈춘 그대로다(timeScale 0) — 게임 오버 때처럼 멈춰도 0으로 돌아오는지 본다(테스트: deltaTime을 쓰면 하얀 채로 굳었다)
            target.GetComponent<HitReceiver>().OnHit();
            t0 = Time.realtimeSinceStartup;
            EditorApplication.update += Watch;
        }, 1);
        void Watch()
        {
            if (!EditorApplication.isPlaying || target == null) { EditorApplication.update -= Watch; return; }
            frames++;
            float a = Amount();
            if (trace.Count < 12) trace.Add($"{a:0.00}");
            if (a <= 0.001f || frames > 300) { EditorApplication.update -= Watch; Finish(); }
        }
        void Finish()
        {
            float ms = (Time.realtimeSinceStartup - t0) * 1000f;
            bool returned = Amount() <= 0.001f;
            var a = ra; var b = rb;
            string m1 = C.Load("m1_changed");
            var sb = new StringBuilder("[측정] #5 피격 신호 → GPU 플래시 — 가장 가까운 적 · HitReceiver.OnHit()\n| 항목 | 값 |\n|---|---|\n");
            sb.AppendLine($"| 신호 직후 _FlashAmount | {peakAmount:0.00} |");
            sb.AppendLine($"| 그림 안 평균 밝기 맞기 전 → 직후 | {a.inside:0} → {b.inside:0} |");
            sb.AppendLine($"| 그림 안에서 30 이상 바뀐 픽셀 | **{changed:0.0}%**{(m1 != "" ? $" (측정 #1: {m1}%)" : "")} |");
            sb.AppendLine(returned
                ? $"| 게임이 멈춘 상태에서 0으로 돌아오기까지 | 약 {ms:0} ms · {frames}프레임 (프레임 수는 PC 속도에 따라 다르다) |"
                : $"| 게임이 멈춘 상태에서 0으로 돌아오기까지 | ⚠ 돌아오지 않는다 ({frames}프레임 동안 {Amount():0.00}) — 코루틴에 Time.unscaledDeltaTime |");
            C.Save("m5_returned", returned ? "1" : "0");
            sb.AppendLine($"| 프레임마다 _FlashAmount | {string.Join(" → ", trace)} |");
            sb.Append("        읽는 법: 스크립트는 숫자 하나(_FlashAmount)만 바꾼다. 색을 섞는 계산은 GPU가 픽셀마다 한다\n        ── 다음 ── 5-2");
            C.Save("m5", sb.ToString()); C.Save("m5_changed", changed.ToString("0.0")); C.Save("m5_peak", peakAmount.ToString("0.00"));
            // README용 — 맞기 전 · 맞은 직후 나란히
            var sheet = new Texture2D(640, 320, TextureFormat.RGBA32, false);
            sheet.SetPixels32(0, 0, 320, 320, before.GetPixels32()); sheet.SetPixels32(320, 0, 320, 320, peak.GetPixels32()); sheet.Apply();
            C.SavePng(sheet, C.CAPTURES + "/week10_hit_before_after.png");
            Object.DestroyImmediate(sheet);
            End();
            AssetDatabase.Refresh();
            Debug.Log(sb.ToString());
        }
        Run(End);
    }

    // ────────────────────────────────────────────────────────── 5-2 측정 #6

    [MenuItem(C.MENU + "5-2. 측정 #6 — 적 20마리 · 값 넣는 방법별 비용 (Play 중)", priority = 52)]
    public static void Measure6()
    {
        if (NeedPlay("측정 #6")) return;
        var student = C.StudentMaterial();
        if (student == null) { Debug.LogWarning("[측정] #6 M_HitFlash가 없다"); return; }
        var fz = new Freeze();
        var def = C.DefaultMaterial();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(C.ENEMY_PREFAB);
        var crowd = new List<SpriteRenderer>();
        var hidden = new List<SpriteRenderer>();
        var lights = new List<Light2D>();
        var player = GameObject.FindWithTag("Player"); var ppos = player ? player.transform.position : Vector3.zero;
        int mats0 = Resources.FindObjectsOfTypeAll<Material>().Length;
        var created = new List<Material>();
        void End()
        {
            foreach (var s in crowd) if (s) Object.DestroyImmediate(s.gameObject);
            foreach (var m in created) if (m) Object.DestroyImmediate(m);
            foreach (var s in hidden) if (s) s.enabled = true;
            foreach (var l in lights) if (l) l.enabled = true;
            if (player) player.transform.position = ppos;
            fz.End();
        }
        Then(() =>
        {
            if (player) player.transform.position = Vector3.zero;
            foreach (var s in Object.FindObjectsByType<SpriteRenderer>()) if (s.CompareTag("Enemy") && s.enabled) { s.enabled = false; hidden.Add(s); }
            // 조명 · 그림자 패스가 섞이지 않게 방향 있는 조명은 잠시 끈다 — 스프라이트 그리기 비용만 본다
            foreach (var l in Object.FindObjectsByType<Light2D>()) if (l.lightType != Light2D.LightType.Global && l.enabled) { l.enabled = false; lights.Add(l); }
            for (int i = 0; i < 20; i++)
            {
                var go = new GameObject("CG2 Measure Enemy") { hideFlags = HideFlags.DontSave };
                go.transform.position = new Vector3(-5f + (i / 4) * 2.5f, -3.75f + (i % 4) * 2.5f + ((i / 4) % 2) * 0.6f, 0);
                var s = go.AddComponent<SpriteRenderer>();
                s.sprite = prefab.GetComponent<SpriteRenderer>().sprite; s.sortingLayerName = "Entity";
                crowd.Add(s);
            }
        });
        var labels = new[] { "ⓐ Sprite-Lit-Default 공유", "ⓑ M_HitFlash 공유", "ⓒ 한 마리만 피격 중 (MaterialPropertyBlock)", "ⓓ 20마리 모두 MaterialPropertyBlock", "ⓔ 20마리 모두 .material 복제" };
        var rows = new string[labels.Length];
        for (int k = 0; k < labels.Length; k++)
        {
            int kk = k;
            Then(() =>
            {
                for (int i = 0; i < crowd.Count; i++)
                {
                    var s = crowd[i];
                    s.SetPropertyBlock(null);
                    s.sharedMaterial = kk == 0 ? def : student;
                    if (kk == 2 && i == 0) SetFlash(s, 1);
                    if (kk == 3) SetFlash(s, 0);
                    if (kk == 4) { var m = s.material; m.SetFloat("_FlashAmount", 0); created.Add(m); }   // .material = 그 렌더러만 쓰는 복사본을 만든다
                }
            }, 30);
            Then(() => { int nm = Resources.FindObjectsOfTypeAll<Material>().Length; if (kk == 0) mats0 = nm; rows[kk] = $"| {labels[kk]} | {UnityStats.drawCalls} | {UnityStats.setPassCalls} | {UnityStats.srpBatcherDrawCalls} | {nm - mats0} |"; }, 1);   // 머티리얼 수는 ⓐ 줄을 기준(0)으로 센다
        }
        Then(() =>
        {
            var sb = new StringBuilder("[측정] #6 적 20마리 — 값 넣는 방법별 비용 · 방향 있는 조명 끔 · 게임 정지\n| 방법 | DrawCalls | SetPass | SRP Batcher 호출 | 새로 생긴 머티리얼 |\n|---|---|---|---|---|\n");
            foreach (var r in rows) sb.AppendLine(r);
            sb.Append("        읽는 법: .material은 렌더러마다 머티리얼을 복사한다. MaterialPropertyBlock은 복사 없이 값만 얹는다\n        ── 다음 ── 5-3");
            C.Save("m6", sb.ToString());
            End();
            Debug.Log(sb.ToString());
        }, 1);
        Run(End);
        Debug.Log("[측정] #6 시작 — 약 4초. Unity 창을 앞에 둔다.");
    }
}
