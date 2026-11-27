using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 11주차 — 디졸브(소멸) Shader Graph를 코드로 만든다.
///   수업에서는 학생이 10주차 SG_HitFlash를 복제해 그래프 창에서 직접 노드를 잇는다. 이 코드는 두 가지에만 쓴다.
///   ① 막힌 학생용 정답 그래프  ② 비교 · 함정 실험용 그래프(일반 Alpha 페이드 · Alpha Clipping 끔 · 경계 없음)
///   10주차 Week10GraphBuilder와 같은 방식(Shader Graph 내부 API · 리플렉션). Unity 6.6 · Shader Graph 17.6에서 확인했다.
/// </summary>
public static class Week11GraphBuilder
{
    // Dissolve = 노이즈와 진행도를 Step으로 비교해 픽셀을 지운다(정답)
    // Fade     = 그림 전체를 고르게 투명하게 — '일반 Alpha' 비교용
    // ClipOnly = Alpha에 노이즈를 곱하고 Alpha Clip Threshold에 진행도 — 'Alpha Clipping만' 비교용(남은 픽셀이 반투명)
    public enum Cut { Dissolve, Fade, ClipOnly }

    public class Options
    {
        public Cut cut = Cut.Dissolve;
        public bool alphaClip = true;      // Graph Settings → Alpha Clipping
        public bool edge = true;           // 3단계 경계 발광
        public bool uvNode = true;         // UV 노드를 Simple Noise에 명시적으로 연결
        public bool remap = true;          // 2단계 끝: 진행도 0~1을 노이즈가 실제로 있는 범위로 늘인다
        public Vector2 remapOut = new Vector2(0.10f, 0.70f);
        public Vector2 remapIn = new Vector2(0f, 1f);   // 함정 실험: 기본값 (-1, 1)을 그대로 두면
        public float posterize = 0;        // 6단계 응용 C(디지털 분해): UV를 이 칸 수로 끊어 무늬를 네모 칸 단위로 (0이면 안 씀)
    }

    static void SetSlot(object node, int id, object value)
    {
        var ms = T(SG, "UnityEditor.ShaderGraph.MaterialSlot");
        var mi = node.GetType().GetMethods(ALL).First(m => m.Name == "FindSlot" && m.IsGenericMethod).MakeGenericMethod(ms);
        var slot = mi.Invoke(node, new object[] { id });
        slot.GetType().GetProperty("value", ALL).SetValue(slot, value);
    }

    const BindingFlags ALL = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Assembly SG => typeof(UnityEditor.ShaderGraph.Internal.ColorShaderProperty).Assembly;
    static Assembly URP => Type.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget, Unity.RenderPipelines.Universal.Editor", true).Assembly;
    static Type T(Assembly a, string n) => a.GetType(n) ?? throw new Exception("타입 없음: " + n);

    static object Call(object o, string m, params object[] args)
    {
        var t = o as Type ?? o.GetType();
        var target = o is Type ? null : o;
        foreach (var mi in t.GetMethods(ALL).Where(x => x.Name == m && x.GetParameters().Length >= args.Length))
        {
            var ps = mi.GetParameters();
            var full = new object[ps.Length];
            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (i < args.Length) { if (args[i] != null && !ps[i].ParameterType.IsInstanceOfType(args[i]) && !ps[i].ParameterType.IsArray) { ok = false; break; } full[i] = args[i]; }
                else if (ps[i].HasDefaultValue) full[i] = ps[i].DefaultValue;
                else { ok = false; break; }
            }
            if (ok) return mi.Invoke(target, full);
        }
        throw new Exception($"메서드 없음: {t.Name}.{m}");
    }

    static object Get(object o, string n)
    {
        var t = o as Type ?? o.GetType(); var target = o is Type ? null : o;
        var p = t.GetProperty(n, ALL); if (p != null) return p.GetValue(target);
        var f = t.GetField(n, ALL); if (f != null) return f.GetValue(target);
        throw new Exception($"멤버 없음: {t.Name}.{n}");
    }

    static void Set(object o, string n, object v)
    {
        var t = o.GetType();
        var p = t.GetProperty(n, ALL); if (p != null && p.CanWrite) { p.SetValue(o, v); return; }
        var f = t.GetField(n, ALL); if (f != null) { f.SetValue(o, v); return; }
        throw new Exception($"쓸 수 없음: {t.Name}.{n}");
    }

    static object New(string shortName) => Activator.CreateInstance(T(SG, "UnityEditor.ShaderGraph.Internal." + shortName), true);
    static object Block(Type owner, string sub, string field) => Get(owner.GetNestedType(sub, ALL), field);

    /// <summary>URP 메뉴 'Sprite Lit Shader Graph'와 같은 빈 그래프. alphaClip이면 Alpha Clip Threshold 블록까지.</summary>
    static object NewSpriteLitGraph(bool alphaClip)
    {
        var target = Activator.CreateInstance(T(URP, "UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget"));
        Call(target, "TrySetActiveSubTarget", T(URP, "UnityEditor.Rendering.Universal.ShaderGraph.UniversalSpriteLitSubTarget"));
        Set(target, "alphaClip", alphaClip);
        var bf = T(SG, "UnityEditor.ShaderGraph.BlockFields");
        var ubf = T(URP, "UnityEditor.Rendering.Universal.ShaderGraph.UniversalBlockFields");
        var descType = T(SG, "UnityEditor.ShaderGraph.BlockFieldDescriptor");
        var list = new System.Collections.Generic.List<object>
        {
            Block(bf, "VertexDescription", "Position"), Block(bf, "VertexDescription", "Normal"), Block(bf, "VertexDescription", "Tangent"),
            Block(bf, "SurfaceDescription", "BaseColor"), Block(ubf, "SurfaceDescription", "SpriteMask"),
            Block(bf, "SurfaceDescription", "NormalTS"), Block(bf, "SurfaceDescription", "Alpha"),
        };
        if (alphaClip) list.Add(Block(bf, "SurfaceDescription", "AlphaClipThreshold"));
        var blocks = Array.CreateInstance(descType, list.Count);
        for (int i = 0; i < list.Count; i++) blocks.SetValue(list[i], i);
        var targets = Array.CreateInstance(T(SG, "UnityEditor.ShaderGraph.Target"), 1);
        targets.SetValue(target, 0);

        var graph = Activator.CreateInstance(T(SG, "UnityEditor.ShaderGraph.GraphData"), true);
        Call(graph, "AddContexts");
        Call(graph, "InitializeOutputs", targets, blocks);
        Call(graph, "AddCategory", Call(T(SG, "UnityEditor.ShaderGraph.CategoryData"), "DefaultCategory", new object[] { null }));
        Set(graph, "path", "Shader Graphs");
        return graph;
    }

    static void Place(object node, float x, float y)
    {
        var ds = Get(node, "drawState");
        Set(ds, "position", new Rect(x, y, 0, 0));
        Set(ds, "expanded", true);
        Set(node, "drawState", ds);
    }

    static object AddNode(object graph, string type, float x, float y)
    {
        var n = Activator.CreateInstance(T(SG, "UnityEditor.ShaderGraph." + type), true);
        Place(n, x, y);
        Call(graph, "AddNode", n);
        return n;
    }

    static object AddProperty(object graph, object prop, string display, float x, float y)
    {
        Set(prop, "displayName", display);
        Set(prop, "overrideReferenceName", "_" + display);   // 학생이 이름만 쓰면 자동으로 생기는 Reference와 같다
        Call(graph, "AddGraphInput", prop);
        var node = Activator.CreateInstance(T(SG, "UnityEditor.ShaderGraph.PropertyNode"), true);
        Place(node, x, y);
        Call(graph, "AddNode", node);
        Set(node, "property", prop);
        return node;
    }

    static object Slider(string name, float min, float max, float value)
    {
        var p = New("Vector1ShaderProperty");
        Set(p, "floatType", UnityEditor.ShaderGraph.Internal.FloatType.Slider);
        Set(p, "rangeValues", new Vector2(min, max));
        Set(p, "value", value);
        return p;
    }

    static void Link(object graph, object from, int fromSlot, object to, int toSlot) =>
        Call(graph, "Connect", Call(from, "GetSlotReference", fromSlot), Call(to, "GetSlotReference", toSlot));

    static object BlockNode(object graph, string fieldName)
    {
        var ctx = Get(graph, "fragmentContext");
        foreach (var r in (IEnumerable)Get(ctx, "blocks"))
        {
            var b = Get(r, "value");
            var d = Get(b, "descriptor"); if ((string)Get(d, "tag") + "." + (string)Get(d, "name") == fieldName) return b;
        }
        throw new Exception("블록 없음: " + fieldName);
    }

    public static string Build(string path, Options o = null)
    {
        o ??= new Options();
        var graph = NewSpriteLitGraph(o.alphaClip);

        // ── 10주차 그대로: 그림 · 노멀 맵 · 피격 플래시 ───────────────────────────────
        var mainTex = AddProperty(graph, New("Texture2DShaderProperty"), "MainTex", -1500, -60);
        var nProp = New("Texture2DShaderProperty");
        Set(nProp, "defaultType", UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty.DefaultType.NormalMap);
        var nTex = AddProperty(graph, nProp, "NormalMap", -1500, 520);
        var colorProp = New("ColorShaderProperty"); Set(colorProp, "value", Color.white);
        Set(colorProp, "colorMode", UnityEditor.ShaderGraph.Internal.ColorMode.HDR);
        var flashColor = AddProperty(graph, colorProp, "FlashColor", -900, 120);
        var flashAmount = AddProperty(graph, Slider("FlashAmount", 0, 1, 0), "FlashAmount", -900, 180);

        var sample = AddNode(graph, "SampleTexture2DNode", -1300, -120);
        Link(graph, mainTex, 0, sample, 1);
        var flash = AddNode(graph, "LerpNode", -700, -40);
        Link(graph, sample, 0, flash, 0);
        Link(graph, flashColor, 0, flash, 1);
        Link(graph, flashAmount, 0, flash, 2);

        var nSample = AddNode(graph, "SampleTexture2DNode", -1300, 520);
        Set(nSample, "textureType", Enum.Parse(T(SG, "UnityEditor.ShaderGraph.TextureType"), "Normal"));
        Link(graph, nTex, 0, nSample, 1);
        Link(graph, nSample, 0, BlockNode(graph, "SurfaceDescription.NormalTS"), 0);

        // ── 11주차: 디졸브 ────────────────────────────────────────────────────────
        var amount = AddProperty(graph, Slider("DissolveAmount", 0, 1, 0), "DissolveAmount", -1100, 260);
        object baseColorOut = flash; int baseColorSlot = 3;
        object alphaSrc; int alphaSlot;

        if (o.cut == Cut.Fade)
        {
            // 비교용 '일반 Alpha': 그림 Alpha × (1 − 진행도) — 모든 픽셀이 같이 옅어진다
            var inv = AddNode(graph, "OneMinusNode", -700, 300);
            Link(graph, amount, 0, inv, 0);
            var mulA = AddNode(graph, "MultiplyNode", -450, 260);
            Link(graph, sample, 7, mulA, 0); Link(graph, inv, 1, mulA, 1);
            alphaSrc = mulA; alphaSlot = 2;
        }
        else if (o.cut == Cut.ClipOnly)
        {
            // 비교용 'Alpha Clipping만': Alpha = 그림 Alpha × 무늬 · Alpha Clip Threshold = 진행도
            //   무늬 < 진행도인 픽셀은 잘려 나가지만, 남은 픽셀의 Alpha가 무늬 값(0.2~0.7)이라 반투명하다
            var scale = New("Vector1ShaderProperty"); Set(scale, "value", 40f);
            var noiseScale = AddProperty(graph, scale, "NoiseScale", -1300, 300);
            var noise = AddNode(graph, "NoiseNode", -1050, 340);
            Link(graph, noiseScale, 0, noise, 1);
            var mulA = AddNode(graph, "MultiplyNode", -450, 260);
            Link(graph, sample, 7, mulA, 0); Link(graph, noise, 2, mulA, 1);
            Link(graph, amount, 0, BlockNode(graph, "SurfaceDescription.AlphaClipThreshold"), 0);
            alphaSrc = mulA; alphaSlot = 2;
        }
        else
        {
            // ① 소멸 패턴: UV → Simple Noise(Scale = NoiseScale) → 0~1 무늬
            var scale = New("Vector1ShaderProperty"); Set(scale, "value", 40f);
            var noiseScale = AddProperty(graph, scale, "NoiseScale", -1300, 300);
            var noise = AddNode(graph, "NoiseNode", -1050, 340);
            if (o.uvNode)
            {
                var uv = AddNode(graph, "UVNode", -1300, 360);
                if (o.posterize > 0)
                {
                    var post = AddNode(graph, "PosterizeNode", -1180, 380);
                    Link(graph, uv, 0, post, 0);
                    SetSlot(post, 1, new Vector4(o.posterize, o.posterize, o.posterize, o.posterize));
                    Link(graph, post, 2, noise, 0);
                }
                else Link(graph, uv, 0, noise, 0);
            }
            Link(graph, noiseScale, 0, noise, 1);

            // ② 진행도 비교: Step(Edge = 기준값, In = 무늬) → 무늬 ≥ 기준값이면 1(남김) · 아니면 0(지움)
            //    기준값 = DissolveAmount를 Remap으로 노이즈가 실제로 있는 범위(약 0.1~0.7)에 맞춘 값
            object thr = amount; int thrSlot = 0;
            if (o.remap)
            {
                var remap = AddNode(graph, "RemapNode", -900, 240);
                Link(graph, amount, 0, remap, 0);
                SetSlot(remap, 1, o.remapIn);
                SetSlot(remap, 2, o.remapOut);
                thr = remap; thrSlot = 3;
            }
            var step = AddNode(graph, "StepNode", -700, 300);
            Link(graph, thr, thrSlot, step, 0); Link(graph, noise, 2, step, 1);
            var mulA = AddNode(graph, "MultiplyNode", -450, 260);
            Link(graph, sample, 7, mulA, 0); Link(graph, step, 2, mulA, 1);
            alphaSrc = mulA; alphaSlot = 2;

            if (o.edge)
            {
                // ③ 경계 마스크: 무늬 − 진행도 = '지워지기까지 남은 거리'. 0 근처(곧 지워질 곳)만 1
                //    Smoothstep(0, EdgeWidth, 거리) → One Minus → 경계에서 1, EdgeWidth만큼 안쪽에서 0
                var width = AddProperty(graph, Slider("EdgeWidth", 0, 0.3f, 0.08f), "EdgeWidth", -1100, 460);
                var eProp = New("ColorShaderProperty"); Set(eProp, "value", new Color(1f, 0.42f, 0.08f));
                Set(eProp, "colorMode", UnityEditor.ShaderGraph.Internal.ColorMode.HDR);
                var edgeColor = AddProperty(graph, eProp, "EdgeColor", -700, 120);
                var sub = AddNode(graph, "SubtractNode", -900, 420);
                Link(graph, noise, 2, sub, 0); Link(graph, thr, thrSlot, sub, 1);
                var smooth = AddNode(graph, "SmoothstepNode", -700, 440);
                Link(graph, width, 0, smooth, 1); Link(graph, sub, 2, smooth, 2);   // Edge1 = 0(기본값) · Edge2 = EdgeWidth
                var edge = AddNode(graph, "OneMinusNode", -500, 460);
                Link(graph, smooth, 3, edge, 0);
                var burn = AddNode(graph, "LerpNode", -300, 0);
                Link(graph, flash, 3, burn, 0); Link(graph, edgeColor, 0, burn, 1); Link(graph, edge, 1, burn, 2);
                baseColorOut = burn; baseColorSlot = 3;
            }
        }

        Link(graph, baseColorOut, baseColorSlot, BlockNode(graph, "SurfaceDescription.BaseColor"), 0);
        Link(graph, alphaSrc, alphaSlot, BlockNode(graph, "SurfaceDescription.Alpha"), 0);
        // Alpha Clip Threshold 블록은 기본값 0.5 그대로 — Alpha가 0.5보다 작은 픽셀은 그리지 않는다

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        Call(T(SG, "UnityEditor.ShaderGraph.FileUtilities"), "WriteShaderGraphToDisk", path, graph);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return path;
    }
}
