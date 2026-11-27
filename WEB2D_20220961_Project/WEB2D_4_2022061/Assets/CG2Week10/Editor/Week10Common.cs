using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 10주차 메뉴들이 같이 쓰는 도우미.
///   경로 — 학생이 만드는 그래프 · 머티리얼 위치와 이름을 한곳에 둔다.
///   Shoot — 대상만 크게 찍는 임시 카메라로 한 번만 그린다(8주차와 같은 방식).
///   Mask  — 스프라이트 원본 PNG의 알파로 '그림 안' 픽셀만 골라 잰다.
/// </summary>
public static class Week10Common
{
    public const string MENU = "CG2 Lab/10주차/";
    public const string ROOT = "Assets/CG2Week10";
    public const string GRAPH = ROOT + "/Shaders/SG_HitFlash.shadergraph";
    public const string MATERIAL = ROOT + "/Materials/M_HitFlash.mat";
    public const string ENEMY_PREFAB = "Assets/StarterProject/Prefabs/Enemy.prefab";
    public const string HIT_RECEIVER = "Assets/StarterProject/Scripts/HitReceiver.cs";
    public const string DEFAULT_MAT = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
    public const string STORE = "Library/CG2Week10";
    public const string CAPTURES = ROOT + "/Captures";
    public const string ENTITY_LIGHT = "CG2 Entity Light";

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
    public static Material DefaultMaterial() => AssetDatabase.LoadAssetAtPath<Material>(DEFAULT_MAT);

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
    public static float Linear(float srgb255) => Mathf.GammaToLinearSpace(srgb255 / 255f);

    /// <summary>
    /// 스프라이트 원본 PNG의 알파 — 화면 픽셀이 그림 안인지 판단한다(flipX면 좌우 반전).
    /// SpriteRenderer.bounds는 Tight 메시 기준이라 그림 사각형보다 작다(테스트에서 확인).
    /// 그래서 bounds 비율이 아니라 피벗과 Pixels Per Unit으로 월드 좌표 → 원본 픽셀을 계산한다.
    /// </summary>
    public class Mask
    {
        readonly Texture2D _t; readonly bool _flip; readonly Rect _r; readonly Vector2 _pivot; readonly float _ppu;
        readonly Bounds _b; readonly Transform _tr;
        public Mask(SpriteRenderer sr)
        {
            _t = new Texture2D(2, 2);
            _t.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sr.sprite)));
            _flip = sr.flipX;
            _r = sr.sprite.rect;
            _pivot = sr.sprite.pivot;
            _ppu = sr.sprite.pixelsPerUnit;
            _b = sr.bounds;
            _tr = sr.transform;
        }
        /// <summary>u, v = bounds 안의 비율(0~1). 호출하는 쪽은 bounds 기준으로 넘긴다.</summary>
        public bool Inside(float u, float v)
        {
            var world = new Vector3(_b.min.x + u * _b.size.x, _b.min.y + v * _b.size.y, _tr.position.z);
            Vector2 local = _tr.InverseTransformPoint(world);
            float px = local.x * _ppu + _pivot.x, py = local.y * _ppu + _pivot.y;
            if (_flip) px = _r.width - px;
            if (px < 0 || py < 0 || px >= _r.width || py >= _r.height) return false;
            return _t.GetPixel((int)(_r.x + px), (int)(_r.y + py)).a >= 0.5f;
        }
        public void Free() => Object.DestroyImmediate(_t);
    }

    public struct Region { public float inside, outside, insideLinear; public int nIn, nOut; public Color insideColor; }

    /// <summary>찍은 그림에서 스프라이트 안 · 밖(같은 사각형 안) 평균 밝기와 평균 색.</summary>
    public static Region Measure(Texture2D shot, Camera cam, SpriteRenderer sr, Mask m)
    {
        int w = shot.width, h = shot.height;
        float halfH = cam.orthographicSize, halfW = halfH * w / h;
        Vector2 c = cam.transform.position;
        var b = sr.bounds;
        var px = shot.GetPixels32();
        double si = 0, so = 0, r = 0, g = 0, bl = 0, lin = 0; int ni = 0, no = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float wx = c.x - halfW + (x + 0.5f) / w * 2 * halfW, wy = c.y - halfH + (y + 0.5f) / h * 2 * halfH;
                if (wx < b.min.x || wx > b.max.x || wy < b.min.y || wy > b.max.y) continue;
                var p = px[y * w + x];
                if (m.Inside((wx - b.min.x) / b.size.x, (wy - b.min.y) / b.size.y)) { si += Gray(p); lin += 0.2126 * Linear(p.r) + 0.7152 * Linear(p.g) + 0.0722 * Linear(p.b); r += p.r; g += p.g; bl += p.b; ni++; }
                else { so += Gray(p); no++; }
            }
        return new Region
        {
            inside = ni > 0 ? (float)(si / ni) : 0, insideLinear = ni > 0 ? (float)(lin / ni) : 0, outside = no > 0 ? (float)(so / no) : 0, nIn = ni, nOut = no,
            insideColor = ni > 0 ? new Color((float)(r / ni / 255), (float)(g / ni / 255), (float)(bl / ni / 255)) : Color.black
        };
    }

    /// <summary>두 그림을 비교해 스프라이트 안에서 화면 값이 30 이상 달라진 픽셀 비율(%).</summary>
    public static float ChangedInside(Texture2D a, Texture2D b2, Camera cam, SpriteRenderer sr, Mask m)
    {
        int w = a.width, h = a.height;
        float halfH = cam.orthographicSize, halfW = halfH * w / h;
        Vector2 c = cam.transform.position;
        var bb = sr.bounds;
        var pa = a.GetPixels32(); var pb = b2.GetPixels32();
        int n = 0, ch = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float wx = c.x - halfW + (x + 0.5f) / w * 2 * halfW, wy = c.y - halfH + (y + 0.5f) / h * 2 * halfH;
                if (wx < bb.min.x || wx > bb.max.x || wy < bb.min.y || wy > bb.max.y) continue;
                if (!m.Inside((wx - bb.min.x) / bb.size.x, (wy - bb.min.y) / bb.size.y)) continue;
                n++;
                if (Mathf.Abs(Gray(pa[y * w + x]) - Gray(pb[y * w + x])) >= 30f) ch++;
            }
        return n > 0 ? 100f * ch / n : 0;
    }

    public static void SavePng(Texture2D t, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, t.EncodeToPNG());
    }

    public static SpriteRenderer NearestEnemy()
    {
        var p = GameObject.FindWithTag("Player");
        SpriteRenderer best = null; float d = float.MaxValue;
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>())
        {
            if (!sr.CompareTag("Enemy") || !sr.gameObject.activeInHierarchy || !sr.enabled) continue;
            float dd = p ? (sr.transform.position - p.transform.position).sqrMagnitude : 0;
            if (dd < d) { d = dd; best = sr; }
        }
        return best;
    }

    public static Light2D Named(string name)
    {
        foreach (var l in Object.FindObjectsByType<Light2D>()) if (l.name == name) return l;
        return null;
    }
}
