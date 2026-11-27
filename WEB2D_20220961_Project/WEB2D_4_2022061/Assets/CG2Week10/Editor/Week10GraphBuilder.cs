using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 10주차 — 피격 플래시 Shader Graph를 코드로 만든다.
///   수업에서는 학생이 그래프 창에서 직접 노드를 잇는다. 이 코드는 두 가지에만 쓴다.
///   ① 막힌 학생용 정답 그래프  ② 함정 실험용 그래프(Alpha 미연결 · 참조 이름 오타 · 노멀 맵 없음)
///   Shader Graph의 그래프 편집 API는 공개 API가 아니다(internal). 그래서 리플렉션으로 호출한다.
///   Unity 6.6 · Shader Graph 17.6에서 확인했다. 버전이 바뀌면 이 파일만 고친다.
/// </summary>
public static class Week10GraphBuilder
{
    public class Options
    {
        public bool connectAlpha = true;
        public bool connectNormal = true;
        public string mainTexRef = "_MainTex";
        public string amountRef = "_FlashAmount";
        public string colorRef = "_FlashColor";
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

    /// <summary>URP 메뉴 'Sprite Lit Shader Graph'와 같은 빈 그래프(대상 · 블록)를 만든다.</summary>
    static object NewSpriteLitGraph()
    {
        var target = Activator.CreateInstance(T(URP, "UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget"));
        Call(target, "TrySetActiveSubTarget", T(URP, "UnityEditor.Rendering.Universal.ShaderGraph.UniversalSpriteLitSubTarget"));
        var bf = T(SG, "UnityEditor.ShaderGraph.BlockFields");
        var ubf = T(URP, "UnityEditor.Rendering.Universal.ShaderGraph.UniversalBlockFields");
        var descType = T(SG, "UnityEditor.ShaderGraph.BlockFieldDescriptor");
        var blocks = Array.CreateInstance(descType, 7);
        blocks.SetValue(Block(bf, "VertexDescription", "Position"), 0);
        blocks.SetValue(Block(bf, "VertexDescription", "Normal"), 1);
        blocks.SetValue(Block(bf, "VertexDescription", "Tangent"), 2);
        blocks.SetValue(Block(bf, "SurfaceDescription", "BaseColor"), 3);
        blocks.SetValue(Block(ubf, "SurfaceDescription", "SpriteMask"), 4);
        blocks.SetValue(Block(bf, "SurfaceDescription", "NormalTS"), 5);
        blocks.SetValue(Block(bf, "SurfaceDescription", "Alpha"), 6);
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
        var n = Activator.CreateInstance(T(SG, type), true);
        Place(n, x, y);
        Call(graph, "AddNode", n);
        return n;
    }

    static object AddProperty(object graph, object prop, string display, string reference, float x, float y)
    {
        Set(prop, "displayName", display);
        Set(prop, "overrideReferenceName", reference);
        Call(graph, "AddGraphInput", prop);
        var node = Activator.CreateInstance(T(SG, "UnityEditor.ShaderGraph.PropertyNode"), true);
        Place(node, x, y);
        Call(graph, "AddNode", node);
        Set(node, "property", prop);
        return node;
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
        var graph = NewSpriteLitGraph();

        // 속성 — Blackboard에 생기는 칸. 참조 이름(Reference)이 셰이더 · 스크립트가 부르는 이름이다
        var texProp = New("Texture2DShaderProperty");
        var mainTex = AddProperty(graph, texProp, "MainTex", o.mainTexRef, -1100, -40);
        var colorProp = New("ColorShaderProperty"); Set(colorProp, "value", Color.white);
        Set(colorProp, "colorMode", UnityEditor.ShaderGraph.Internal.ColorMode.HDR);   // 3단계: 밝기를 1보다 크게 줄 수 있게
        var flashColor = AddProperty(graph, colorProp, "FlashColor", o.colorRef, -560, 180);
        var amountProp = New("Vector1ShaderProperty");
        Set(amountProp, "floatType", UnityEditor.ShaderGraph.Internal.FloatType.Slider);
        Set(amountProp, "rangeValues", new Vector2(0, 1));
        Set(amountProp, "value", 0f);
        var flashAmount = AddProperty(graph, amountProp, "FlashAmount", o.amountRef, -560, 260);

        var sample = AddNode(graph, "UnityEditor.ShaderGraph.SampleTexture2DNode", -900, -100);
        Link(graph, mainTex, 0, sample, 1);                            // 텍스처 → Sample Texture 2D
        var lerp = AddNode(graph, "UnityEditor.ShaderGraph.LerpNode", -360, 40);
        Link(graph, sample, 0, lerp, 0);                               // RGBA → A
        Link(graph, flashColor, 0, lerp, 1);                           // FlashColor → B
        Link(graph, flashAmount, 0, lerp, 2);                          // FlashAmount → T
        Link(graph, lerp, 3, BlockNode(graph, "SurfaceDescription.BaseColor"), 0);
        if (o.connectAlpha) Link(graph, sample, 7, BlockNode(graph, "SurfaceDescription.Alpha"), 0);

        if (o.connectNormal)
        {
            // 7주차 노멀 맵 — 스프라이트의 보조 텍스처 '_NormalMap'을 받아 Normal(Tangent Space) 블록으로
            var nProp = New("Texture2DShaderProperty");
            Set(nProp, "defaultType", UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty.DefaultType.NormalMap);
            var nTex = AddProperty(graph, nProp, "NormalMap", "_NormalMap", -1100, 260);
            var nSample = AddNode(graph, "UnityEditor.ShaderGraph.SampleTexture2DNode", -900, 320);
            Set(nSample, "textureType", Enum.Parse(T(SG, "UnityEditor.ShaderGraph.TextureType"), "Normal"));
            Link(graph, nTex, 0, nSample, 1);
            Link(graph, nSample, 0, BlockNode(graph, "SurfaceDescription.NormalTS"), 0);
        }

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        Call(T(SG, "UnityEditor.ShaderGraph.FileUtilities"), "WriteShaderGraphToDisk", path, graph);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return path;
    }
}
