using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 11주차 메뉴들이 같이 쓰는 도우미 (10주차 Week10Common과 같은 방식 — 10주차 패키지가 없어도 돌도록 따로 둔다).
///   경로 — 학생이 만드는 그래프 · 머티리얼 · 고치는 스크립트
///   Shoot — 대상만 크게 찍는 임시 카메라로 한 번만 그린다
///   Mask  — 스프라이트 원본 PNG의 알파로 '그림 안' 픽셀만 골라 잰다(피벗 · Pixels Per Unit 기준)
/// </summary>
public static class Week11Common
{
    public const string MENU = "CG2 Lab/11주차/";
    public const string ROOT = "Assets/CG2Week11";
    public const string GRAPH = ROOT + "/Shaders/SG_Dissolve.shadergraph";
    public const string MATERIAL = ROOT + "/Materials/M_Dissolve.mat";
    public const string W10_GRAPH = "Assets/CG2Week10/Shaders/SG_HitFlash.shadergraph";
    public const string W10_MATERIAL = "Assets/CG2Week10/Materials/M_HitFlash.mat";
    public const string ENEMY_PREFAB = "Assets/StarterProject/Prefabs/Enemy.prefab";
    public const string ENEMY_AI = "Assets/StarterProject/Scripts/EnemyAI.cs";
    public const string HEALTH = "Assets/StarterProject/Scripts/Health.cs";
    public const string STORE = "Library/CG2Week11";
    public const string CAPTURES = ROOT + "/Captures";
    public const string COMPARE = ROOT + "/Compare";   // 1-2 비교용 그래프(메뉴가 만든다)

    public static void Save(string key, string value) { Directory.CreateDirectory(STORE); File.WriteAllText($"{STORE}/{key}.txt", value); }
    public static string Load(string key) { var p = $"{STORE}/{key}.txt"; return File.Exists(p) ? File.ReadAllText(p) : ""; }

    public static bool Busy(string title)
    {
        if (!EditorApplication.isPlaying) return false;
        EditorUtility.DisplayDialog(title, "Play를 멈춘 뒤 실행하세요.", "확인");
        return true;
    }

    public static Shader StudentShader() => AssetDatabase.LoadAssetAtPath<Shader>(GRAPH);
    public static Material StudentMaterial() => AssetDatabase.LoadAssetAtPath<Material>(MATERIAL);

    /// <summary>bounds를 꽉 채워 찍는 임시 카메라. 메인 카메라 설정(렌더러 · 배경)을 복사한다.</summary>
    public static Camera ZoomCamera(Bounds b, float margin = 1.3f)
    {
        var main = Camera.main;
        var go = new GameObject("CG2 Measure Camera") { hideFlags = HideFlags.DontSave };
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(main);
        go.AddComponent<UniversalAdditionalCameraData>();
        cam.enabled = false;
        cam.orthographicSize = Mathf.Max(b.size.x, b.size.y) * 0.5f * margin;
        cam.transform.position = new Vector3(b.center.x, b.center.y, main.transform.position.z);
        return cam;
    }

    public static Texture2D Shoot(Camera cam, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.aspect = (float)w / h;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;
        var active = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = active;
        rt.Release();
        Object.DestroyImmediate(rt);
        return tex;
    }

    public static float Gray(Color32 c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    /// <summary>스프라이트 원본 PNG의 알파로 화면 픽셀이 그림 안인지 판단한다(10주차와 같다).</summary>
    public class Mask
    {
        readonly Texture2D _t; readonly bool _flip; readonly Rect _r; readonly Vector2 _pivot; readonly float _ppu;
        readonly Transform _tr;
        public Mask(SpriteRenderer sr)
        {
            _t = new Texture2D(2, 2);
            _t.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sr.sprite)));
            _flip = sr.flipX;
            _r = sr.sprite.rect;
            _pivot = sr.sprite.pivot;
            _ppu = sr.sprite.pixelsPerUnit;
            _tr = sr.transform;
        }
        public bool InsideWorld(Vector2 world)
        {
            Vector2 local = _tr.InverseTransformPoint(new Vector3(world.x, world.y, _tr.position.z));
            float px = local.x * _ppu + _pivot.x, py = local.y * _ppu + _pivot.y;
            if (_flip) px = _r.width - px;
            if (px < 0 || py < 0 || px >= _r.width || py >= _r.height) return false;
            return _t.GetPixel((int)(_r.x + px), (int)(_r.y + py)).a >= 0.5f;
        }
        public void Free() => Object.DestroyImmediate(_t);
    }

    /// <summary>찍은 그림의 픽셀 → 월드 좌표</summary>
    public static Vector2 World(Camera cam, int x, int y, int w, int h)
    {
        float halfH = cam.orthographicSize, halfW = halfH * w / h;
        Vector2 c = cam.transform.position;
        return new Vector2(c.x - halfW + (x + 0.5f) / w * 2 * halfW, c.y - halfH + (y + 0.5f) / h * 2 * halfH);
    }

    /// <summary>
    /// 그림 안 픽셀을 셋으로 나눠 센다 — 남음(배경과 다름) · 지워짐(배경과 같음) · 발광(남은 것 중 주황 경계: 빨강이 원래보다 30 이상 밝음).
    /// bg = 대상을 끄고 찍은 배경, orig = 진행도 0으로 찍은 원래 그림.
    /// </summary>
    public struct Count
    {
        public int inside, remain, glow, intact;   // intact = 남은 픽셀 중 원래 색 그대로(차이 6 이하)인 것
        public float opacity;
        public float RemainPct => inside > 0 ? 100f * remain / inside : 0;
        public float GlowPct => inside > 0 ? 100f * glow / inside : 0;
        public float IntactOfRemainPct => remain > 0 ? 100f * intact / remain : 0;
    }

    public static Count Classify(Texture2D shot, Texture2D bg, Texture2D orig, Camera cam, Mask m)
    {
        int w = shot.width, h = shot.height;
        var p = shot.GetPixels32(); var b = bg.GetPixels32(); var o = orig.GetPixels32();
        var r = new Count(); double op = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!m.InsideWorld(World(cam, x, y, w, h))) continue;
                int i = y * w + x; r.inside++;
                int dBg = Mathf.Abs(p[i].r - b[i].r) + Mathf.Abs(p[i].g - b[i].g) + Mathf.Abs(p[i].b - b[i].b);
                int dOrig = Mathf.Abs(o[i].r - b[i].r) + Mathf.Abs(o[i].g - b[i].g) + Mathf.Abs(o[i].b - b[i].b);
                // 불투명도 추정: 배경에서 얼마나 멀어졌나 ÷ 원래 그림이 배경에서 떨어진 거리 (원래 그림 색 그대로 섞일 때만 의미가 있다)
                if (dOrig > 0) op += Mathf.Clamp01((float)dBg / dOrig);
                if (dBg > 6)
                {
                    r.remain++;
                    if (p[i].r - o[i].r >= 30 && p[i].r > p[i].b + 30) r.glow++;
                    if (Mathf.Abs(p[i].r - o[i].r) + Mathf.Abs(p[i].g - o[i].g) + Mathf.Abs(p[i].b - o[i].b) <= 6) r.intact++;
                }
            }
        r.opacity = r.inside > 0 ? (float)(op / r.inside) : 0;
        return r;
    }

    public static void SavePng(Texture2D t, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, t.EncodeToPNG());
    }

    /// <summary>여러 장을 가로로 붙인다(같은 크기).</summary>
    public static Texture2D Strip(params Texture2D[] shots)
    {
        int w = shots[0].width, h = shots[0].height;
        var t = new Texture2D(w * shots.Length, h, TextureFormat.RGBA32, false);
        for (int i = 0; i < shots.Length; i++) t.SetPixels32(i * w, 0, w, h, shots[i].GetPixels32());
        t.Apply();
        return t;
    }

    /// <summary>가장 가까운 살아 있는 적. pooledOnly면 스포너의 풀에서 나온 적만(씬에 미리 저장된 적 6마리는 풀 소속이 아니다).</summary>
    public static SpriteRenderer NearestEnemy(bool aliveOnly = true, bool pooledOnly = false)
    {
        var p = GameObject.FindWithTag("Player");
        SpriteRenderer best = null; float d = float.MaxValue;
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>())
        {
            if (!sr.CompareTag("Enemy") || !sr.gameObject.activeInHierarchy || !sr.enabled) continue;
            if (pooledOnly && sr.GetComponentInParent<ObjectPool>() == null) continue;
            var h = sr.GetComponent<Health>(); if (aliveOnly && h != null && h.IsDead) continue;
            float dd = p ? (sr.transform.position - p.transform.position).sqrMagnitude : 0;
            if (dd < d) { d = dd; best = sr; }
        }
        return best;
    }

    /// <summary>
    /// 측정용 스프라이트를 또렷하게 — 캐릭터 전역 조명(8주차 CG2 Entity Light, 밤이라 0.15)을 흰색 1로 잠깐 바꾼다(10주차 #2와 같다).
    /// 돌려받은 함수를 부르면 원래대로.
    /// </summary>
    public static System.Action Bright()
    {
        Light2D l = null;
        foreach (var x in Object.FindObjectsByType<Light2D>()) if (x.name == "CG2 Entity Light") l = x;
        if (l == null) return () => { };
        float i = l.intensity; Color c = l.color;
        l.intensity = 1; l.color = Color.white;
        return () => { if (l) { l.intensity = i; l.color = c; } };
    }

    /// <summary>렌더러가 지금 쓰는 머티리얼의 _DissolveAmount (Play 중 복사본 포함). 속성이 없으면 NaN.</summary>
    public static float Amount(SpriteRenderer sr)
    {
        var m = sr.sharedMaterial;
        return m != null && m.HasProperty("_DissolveAmount") ? m.GetFloat("_DissolveAmount") : float.NaN;
    }
}
