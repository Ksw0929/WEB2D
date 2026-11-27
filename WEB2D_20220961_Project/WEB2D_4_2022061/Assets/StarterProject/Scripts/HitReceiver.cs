using System.Collections;
using UnityEngine;

/// <summary>
/// Shader Graph의 _FlashAmount를 제어하는 피격 플래시.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HitReceiver : MonoBehaviour
{
    [SerializeField] float flashDuration = 0.12f;

    SpriteRenderer _sr;
    Material _mat;
    Coroutine _co;

    static readonly int AID = Shader.PropertyToID("_FlashAmount");

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _mat = _sr.material;
    }

    public void OnHit()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
        }

        _co = StartCoroutine(Co());
    }

    IEnumerator Co()
    {
        for (float t = 0f; t < flashDuration; t += Time.unscaledDeltaTime)
        {
            SetFlashAmount(1f - t / flashDuration);
            yield return null;
        }

        SetFlashAmount(0f);
    }

    public void SetFlashAmount(float v)
    {
        if (_mat != null)
        {
            _mat.SetFloat(AID, Mathf.Clamp01(v));
        }
    }

    void OnDisable()
    {
        SetFlashAmount(0f);
    }

    void OnDestroy()
    {
        if (_mat != null)
        {
            Destroy(_mat);
        }
    }
}