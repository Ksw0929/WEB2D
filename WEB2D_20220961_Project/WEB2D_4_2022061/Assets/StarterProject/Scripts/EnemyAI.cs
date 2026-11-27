using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyAI : MonoBehaviour
{
    [SerializeField] float moveSpeed = 2.5f, attackRange = 0.9f, attackDamage = 10f, attackCooldown = 1f;
    [SerializeField] int expDrop = 10;
    [SerializeField, Min(0f)] float dissolveDuration = 0.8f;

    Rigidbody2D _rb;
    SpriteRenderer _sr;
    Animator _anim;
    Health _health;
    Material _mat;
    ObjectPool _pool;

    Transform _player;
    float _at;
    Vector2 _dir;

    static readonly int HX = Animator.StringToHash("MoveX"),
                        HY = Animator.StringToHash("MoveY"),
                        HS = Animator.StringToHash("Speed"),
                        HM = Animator.StringToHash("IsMoving");

    static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();
        _anim = GetComponent<Animator>();
        _health = GetComponent<Health>();

        _rb.gravityScale = 0f;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        if (_health != null) _health.onDeath.AddListener(OnDeath);

        _mat = _sr.material;
        _pool = GetComponentInParent<ObjectPool>();
    }

    void OnEnable()
    {
        if (_mat) _mat.SetFloat(DissolveAmountId, 0f);
        if (_rb) _rb.simulated = true;
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) _player = p.transform;
    }

    void Update()
    {
        if (_health != null && _health.IsDead) return;
        if (_player == null) return;

        float dist = Vector2.Distance(transform.position, _player.position);
        if (dist <= attackRange)
        {
            _dir = Vector2.zero;
            Anim(_dir, false);
            _at += Time.deltaTime;
            if (_at >= attackCooldown)
            {
                _at = 0f;
                _player.GetComponent<Health>()?.TakeDamage(attackDamage);
            }
        }
        else
        {
            _dir = ((Vector2)_player.position - (Vector2)transform.position).normalized;
            Anim(_dir, true);
        }
    }

    void FixedUpdate()
    {
        if (_health != null && _health.IsDead) return;
        if (_player == null) return;

        float d = Vector2.Distance(transform.position, _player.position);
        _rb.linearVelocity = d > attackRange ? _dir * moveSpeed : Vector2.zero;
    }

    void Anim(Vector2 d, bool m)
    {
        if (_anim != null)
        {
            _anim.SetFloat(HX, d.x);
            _anim.SetFloat(HY, d.y);
            _anim.SetFloat(HS, m ? d.magnitude : 0f);
            _anim.SetBool(HM, m);
        }
        else if (_sr != null && m && Mathf.Abs(d.x) > 0.05f)
        {
            _sr.flipX = d.x < 0f;
        }
    }

    void OnDeath()
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.simulated = false;
        GameManager.Instance?.AddExp(expDrop);
        StartCoroutine(Dissolve());
    }

    IEnumerator Dissolve()
    {
        for (float elapsed = 0f; elapsed < dissolveDuration; elapsed += Time.deltaTime)
        {
            _mat.SetFloat(DissolveAmountId, elapsed / dissolveDuration);
            yield return null;
        }

        _mat.SetFloat(DissolveAmountId, 1f);

        if (_pool) _pool.Return(gameObject);
        else gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_mat) Destroy(_mat);
    }

    public void SetMoveSpeed(float s) => moveSpeed = s;
    public int ExpDrop => expDrop;
}