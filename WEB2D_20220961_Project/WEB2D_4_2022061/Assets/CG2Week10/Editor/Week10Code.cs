using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using C = Week10Common;

/// <summary>
/// 10주차 5단계 — HitReceiver를 Shader Graph에 연결한다.
///   학생은 HitReceiver.cs를 직접 고친다(수업 내용 5단계 코드). 이 파일은 검사 · 체크포인트 · 막혔을 때의 정답 · 원본 복구를 맡는다.
/// </summary>
public static class Week10Code
{
    const string BACKUP = C.ROOT + "/Backup/HitReceiver.cs.bytes";

    public const string ANSWER = @"using System.Collections;
using UnityEngine;

/// <summary>
/// 피격 플래시 — 10주차: Shader Graph의 _FlashAmount 값을 바꾼다.
///   스크립트는 숫자 하나만 넘기고, 색을 섞는 계산(Lerp)은 GPU가 픽셀마다 한다.
///   적마다 플래시 값이 달라야 하므로 이 렌더러만 쓰는 머티리얼 복사본을 한 번 만든다.
///   (URP 공식 문서: SRP 프로젝트에서는 MaterialPropertyBlock 대신 머티리얼을 따로 쓴다)
///   복사본은 직접 만든 것이므로 오브젝트가 사라질 때 직접 지운다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HitReceiver : MonoBehaviour
{
    [SerializeField] float flashDuration = 0.12f;

    static readonly int AID = Shader.PropertyToID(""_FlashAmount"");
    SpriteRenderer _sr;
    Material _mat;
    Coroutine _co;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _mat = _sr.material;   // .material = 이 렌더러만 쓰는 복사본을 만들어 돌려준다 (한 번만 부른다)
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);   // 복사본은 자동으로 지워지지 않는다
    }

    public void OnHit()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Co());
    }

    IEnumerator Co()
    {
        // 1에서 시작해 flashDuration 동안 0으로 줄어든다. unscaledDeltaTime = 게임이 멈춰도(게임 오버 timeScale 0) 흐르는 시간
        for (float t = 0; t < flashDuration; t += Time.unscaledDeltaTime)
        {
            SetFlashAmount(1f - t / flashDuration);
            yield return null;
        }
        SetFlashAmount(0f);
    }

    public void SetFlashAmount(float v)
    {
        if (_mat != null) _mat.SetFloat(AID, Mathf.Clamp01(v));
    }

    void OnDisable() => SetFlashAmount(0f);   // 풀로 돌아간 적이 하얀 채로 다시 나오지 않게
}
";

    [MenuItem(C.MENU + "5-3. 체크포인트 — 그래프 · 머티리얼 · 스크립트 · 측정", priority = 53)]
    public static void Checkpoint()
    {
        int warn = 0;
        string Ok(bool ok, string good, string bad) { if (!ok) warn++; return ok ? "✓ " + good : "⚠ " + bad; }
        var sb = new StringBuilder();
        var s = C.StudentShader();
        Week10Graph.PropertyReport(s, "", out int pw);
        sb.AppendLine($"        [그래프]   {Ok(s != null && !ShaderUtil.ShaderHasError(s), "SG_HitFlash 컴파일", "SG_HitFlash 없음 · 오류")}  {Ok(pw == 0, "_MainTex · _FlashAmount · _FlashColor", "속성 이름 확인 (2-1)")}");
        var m = C.StudentMaterial();
        float inten = m != null && m.HasProperty("_FlashColor") ? Week10Graph.Intensity(m.GetColor("_FlashColor")) : -9;
        sb.AppendLine($"        [머티리얼] {Ok(m != null, "M_HitFlash", "M_HitFlash 없음 (4-1)")}  {Ok(inten >= 1.9f, $"Flash Color Intensity {inten:0.#}", "Flash Color Intensity 2 (측정 #3)")}");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(C.ENEMY_PREFAB);
        var pm = prefab.GetComponent<SpriteRenderer>().sharedMaterial;
        var player = GameObject.FindWithTag("Player")?.GetComponent<SpriteRenderer>();
        // Play 중에는 HitReceiver가 만든 복사본 "M_HitFlash (Instance)"가 붙어 있다 → 같은 셰이더인지로 확인한다
        sb.AppendLine($"        [적용]     {Ok(pm == m && m != null, "Enemy 프리팹", "Enemy 프리팹 머티리얼 (4-2)")}  {Ok(player != null && m != null && player.sharedMaterial != null && player.sharedMaterial.shader == m.shader, "Player", "Player 머티리얼 (4-2)")}");
        string code = File.ReadAllText(C.HIT_RECEIVER);
        bool usesAmount = code.Contains("_FlashAmount") && (code.Contains("SetFloat") || code.Contains("SetPropertyBlock"));
        bool usesMaterial = code.Contains(".material");
        bool destroys = code.Contains("Destroy(");
        bool usesBlock = code.Contains("SetPropertyBlock");
        string how = usesMaterial ? (destroys ? "✓ 머티리얼 복사본 · OnDestroy에서 지움" : "⚠ .material 복사본을 지우지 않는다 — OnDestroy에서 Destroy")
                   : usesBlock ? "ℹ MaterialPropertyBlock — 공식 문서는 SRP에서 권하지 않는다 (측정 #6)" : "";
        if (usesMaterial && !destroys) warn++;
        sb.AppendLine($"        [스크립트] {Ok(usesAmount, "HitReceiver → _FlashAmount", "HitReceiver가 _FlashAmount를 안 쓴다 (5단계)")}  {how}");
        string L(string k) => C.Load(k);
        sb.AppendLine($"        [측정]     {Ok(L("m1") != "", "#1", "#1 없음")}  {Ok(L("m2") != "", "#2", "#2 없음")}  {Ok(L("m3") != "", "#3", "#3 없음")}  {Ok(L("m4") != "", "#4", "#4 없음")}  {Ok(L("m5") != "", "#5", "#5 없음")}  {Ok(L("m6") != "", "#6", "#6 없음")}");
        if (L("m5_changed") != "")
        {
            float.TryParse(L("m5_changed"), out var ch); float.TryParse(L("m1_changed"), out var ch1);
            sb.AppendLine($"                   {Ok(ch >= 50, $"피격 신호 → 그림 {ch:0.0}% 변화 (전: {ch1:0.0}%)", $"플래시가 약하다 — {ch:0.0}%")}");
        }
        if (L("m5_returned") != "")
            sb.AppendLine($"                   {Ok(L("m5_returned") == "1", "게임이 멈춰도 플래시가 0으로 돌아온다", "게임이 멈추면 하얀 채로 굳는다 — 코루틴에 Time.unscaledDeltaTime (측정 #5)")}");
        if (L("m4_feel") != "")
        {
            var f = L("m4_feel").Split(',');
            float.TryParse(f[0], out var f0); float.TryParse(f[1], out var f1);
            sb.AppendLine($"                   {Ok(f1 >= f0 * 0.5f, $"노멀 맵 유지 {f0:0.0} → {f1:0.0}", $"노멀 맵 반응이 사라졌다 {f0:0.0} → {f1:0.0}")}");
        }
        Debug.Log($"[검증] 10주차 체크포인트 — 경고 {warn}개\n{sb}        체크포인트: 인게임 스크립트의 트리거 신호에 의해 GPU 단에서 실시간으로 흰색 깜빡임 피격 연출이 수행되는가?");
    }

    [MenuItem(C.MENU + "(막혔을 때) HitReceiver 정답으로 바꾸기", priority = 96)]
    public static void ApplyAnswer()
    {
        if (C.Busy("정답")) return;
        Directory.CreateDirectory(Path.GetDirectoryName(BACKUP));
        if (!File.Exists(BACKUP)) File.Copy(C.HIT_RECEIVER, BACKUP);
        File.WriteAllText(C.HIT_RECEIVER, ANSWER, new UTF8Encoding(true));
        AssetDatabase.ImportAsset(BACKUP);
        AssetDatabase.ImportAsset(C.HIT_RECEIVER);
        Debug.Log("[실습] HitReceiver 정답 적용 — 원본은 " + BACKUP);
    }

    [MenuItem(C.MENU + "되돌리기 — 5단계 HitReceiver 원본", priority = 92)]
    public static void Revert()
    {
        if (C.Busy("되돌리기")) return;
        if (!File.Exists(BACKUP)) { Debug.LogWarning("[되돌리기] 백업이 없다 — 정답 적용 메뉴를 쓴 적이 없으면 직접 고친 내용을 되돌린다"); return; }
        File.Copy(BACKUP, C.HIT_RECEIVER, true);
        AssetDatabase.ImportAsset(C.HIT_RECEIVER);
        Debug.Log("[되돌리기] HitReceiver 원본 복구");
    }
}
