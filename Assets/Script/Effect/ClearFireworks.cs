using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 스테이지나 튜토리얼 클리어 시 화면 왼쪽 아래와 오른쪽 아래에서 폭죽을 쏘아 올려 터뜨린다.
// 다음 스테이지로 넘어가기 전까지 계속 반복한다.
// 카메라 줌 상태와 상관없이 항상 같은 화면 위치와 크기로 보인다.
public class ClearFireworks : MonoBehaviour
{
    [Header("에셋")]
    [SerializeField] private Sprite sparkSprite;
    [SerializeField] private Sprite ringSprite;
    [SerializeField] private Sprite starSprite;

    [Header("발사")]
    [Tooltip("한 번에 한쪽에서 쏘는 폭죽 개수")]
    [SerializeField, Min(1)] private int shotsPerSide = 3;
    [Tooltip("한 차례 발사가 끝나고 다음 차례까지 쉬는 시간(초)")]
    [SerializeField] private float roundInterval = 0.8f;
    [Tooltip("폭죽 사이 간격(초). 왼쪽, 오른쪽이 번갈아 발사된다.")]
    [SerializeField] private float shotInterval = 0.35f;
    [Tooltip("올라가는 시간 범위(초)")]
    [SerializeField] private Vector2 riseTime = new Vector2(0.45f, 0.65f);
    [Tooltip("터지는 높이 범위. 화면 아래 -1, 위 1")]
    [SerializeField] private Vector2 burstHeight = new Vector2(-0.05f, 0.45f);
    [Tooltip("터지는 가로 위치 범위. 화면 가장자리 1, 가운데 0")]
    [SerializeField] private Vector2 burstSide = new Vector2(0.35f, 0.7f);

    [Header("불꽃")]
    [SerializeField, Min(1)] private int sparksPerBurst = 36;
    [SerializeField] private Vector2 sparkSpeed = new Vector2(0.45f, 0.75f);
    [SerializeField] private Vector2 sparkLife = new Vector2(0.8f, 1.3f);
    [SerializeField] private float sparkSize = 0.05f;
    [SerializeField] private float gravity = 0.55f;
    [SerializeField] private float drag = 2.2f;
    [SerializeField] private float ringSize = 0.55f;

    [Header("색")]
    [SerializeField] private Color[] palette =
    {
        new Color(1.00f, 0.35f, 0.40f),
        new Color(1.00f, 0.75f, 0.20f),
        new Color(0.45f, 0.95f, 0.45f),
        new Color(0.35f, 0.75f, 1.00f),
        new Color(0.80f, 0.45f, 1.00f),
        new Color(1.00f, 0.50f, 0.85f),
    };

    [Tooltip("다른 오브젝트보다 위에 보이도록 할 정렬 순서")]
    [SerializeField] private int sortingOrder = 200;

    private enum Kind { Rocket, Trail, Spark, Ring, Flash }

    private class Particle
    {
        public SpriteRenderer renderer;
        public Kind kind;
        public Vector2 pos;
        public Vector2 vel;
        public Vector2 start;
        public Vector2 end;
        public float age;
        public float life;
        public float size;
        public Color color;
        public float spin;
        public float trailTimer;
    }

    private readonly List<Particle> active = new List<Particle>();
    private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
    private Coroutine showRoutine;
    private Camera cam;

    private void OnEnable()
    {
        StageManager.OnStageCleared += HandleCleared;
        StageManager.OnStageStarted += HandleStarted;
        TutorialManager.OnTutorialCompleted += Play;
    }

    private void OnDisable()
    {
        StageManager.OnStageCleared -= HandleCleared;
        StageManager.OnStageStarted -= HandleStarted;
        TutorialManager.OnTutorialCompleted -= Play;
    }

    private void HandleCleared(int stage) => Play();

    private void HandleStarted(int stage) => StopAll();

    // 좌우 번갈아 폭죽을 발사한다.
    public void Play()
    {
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(Show());
    }

    // 진행 중인 폭죽을 모두 지운다.
    public void StopAll()
    {
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = null;
        for (int i = active.Count - 1; i >= 0; i--) Release(i);
    }

    // 클리어 상태가 이어지는 동안 좌우 번갈아 발사를 반복한다.
    private IEnumerator Show()
    {
        do
        {
            for (int i = 0; i < shotsPerSide * 2; i++)
            {
                Launch(i % 2 == 0 ? -1f : 1f);
                yield return new WaitForSeconds(shotInterval);
            }
            yield return new WaitForSeconds(roundInterval);
        }
        while (ClearFxState.IsCleared && !ClearFxState.IsFailed);
        showRoutine = null;
    }

    // 화면 아래 모서리에서 화면 안쪽 위로 로켓을 쏜다.
    private void Launch(float side)
    {
        Camera c = GetCamera();
        if (c == null) return;
        float aspect = c.aspect;
        Particle p = Spawn(Kind.Rocket, sparkSprite);
        if (p == null) return;
        p.start = new Vector2(side * aspect * 0.95f, -1.05f);
        p.end = new Vector2(side * aspect * Random.Range(burstSide.x, burstSide.y),
            Random.Range(burstHeight.x, burstHeight.y));
        p.pos = p.start;
        p.life = Random.Range(riseTime.x, riseTime.y);
        p.size = sparkSize * 1.4f;
        p.color = palette.Length > 0 ? palette[Random.Range(0, palette.Length)] : Color.white;
    }

    // 로켓이 꼭대기에 닿으면 불꽃, 링, 섬광을 한 번에 퍼뜨린다.
    private void Burst(Vector2 at, Color main)
    {
        Color second = palette.Length > 0 ? palette[Random.Range(0, palette.Length)] : Color.white;
        float offset = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < sparksPerBurst; i++)
        {
            Particle p = Spawn(Kind.Spark, i % 3 == 0 && starSprite != null ? starSprite : sparkSprite);
            if (p == null) return;
            float angle = offset + i * Mathf.PI * 2f / sparksPerBurst + Random.Range(-0.08f, 0.08f);
            float speed = Random.Range(sparkSpeed.x, sparkSpeed.y) * (i % 2 == 0 ? 1f : 0.7f);
            p.pos = at;
            p.vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed * 1.6f;
            p.life = Random.Range(sparkLife.x, sparkLife.y);
            p.size = sparkSize * (p.renderer.sprite == starSprite ? 2.2f : 1f);
            p.color = i % 2 == 0 ? main : second;
            p.spin = Random.Range(-360f, 360f);
        }

        Particle ring = Spawn(Kind.Ring, ringSprite);
        if (ring != null)
        {
            ring.pos = at;
            ring.life = 0.35f;
            ring.size = ringSize;
            ring.color = main;
            ring.spin = Random.Range(-90f, 90f);
        }

        Particle flash = Spawn(Kind.Flash, starSprite != null ? starSprite : sparkSprite);
        if (flash != null)
        {
            flash.pos = at;
            flash.life = 0.25f;
            flash.size = ringSize * 0.6f;
            flash.color = Color.white;
        }
    }

    private void LateUpdate()
    {
        if (active.Count == 0) return;
        Camera c = GetCamera();
        if (c == null) return;
        float dt = Time.deltaTime;
        float unit = c.orthographicSize;
        Vector3 camPos = c.transform.position;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Particle p = active[i];
            p.age += dt;
            float t = Mathf.Clamp01(p.age / p.life);
            float alpha = 1f;
            float scale = p.size;

            switch (p.kind)
            {
                case Kind.Rocket:
                    // 빠르게 올라가다 꼭대기에서 느려진다.
                    float e = 1f - (1f - t) * (1f - t);
                    Vector2 next = Vector2.Lerp(p.start, p.end, e);
                    next.x += Mathf.Sin(t * Mathf.PI * 3f) * 0.01f;
                    p.vel = (next - p.pos) / Mathf.Max(dt, 0.0001f);
                    p.pos = next;
                    p.trailTimer -= dt;
                    if (p.trailTimer <= 0f)
                    {
                        p.trailTimer = 0.025f;
                        SpawnTrail(p.pos, p.color);
                    }
                    if (p.age >= p.life)
                    {
                        Burst(p.pos, p.color);
                        Release(i);
                        continue;
                    }
                    break;
                case Kind.Trail:
                    p.vel.y -= gravity * 0.3f * dt;
                    p.pos += p.vel * dt;
                    alpha = 1f - t;
                    scale = p.size * (1f - t * 0.6f);
                    break;
                case Kind.Spark:
                    p.vel *= Mathf.Exp(-drag * dt);
                    p.vel.y -= gravity * dt;
                    p.pos += p.vel * dt;
                    // 끝무렵 깜빡이며 사라진다.
                    alpha = t < 0.6f ? 1f : (1f - t) / 0.4f * (Mathf.Sin(p.age * 40f) > -0.3f ? 1f : 0.3f);
                    scale = p.size * Mathf.Lerp(1.2f, 0.5f, t);
                    break;
                case Kind.Ring:
                    alpha = 1f - t;
                    scale = p.size * Mathf.Lerp(0.3f, 1f, 1f - (1f - t) * (1f - t));
                    break;
                case Kind.Flash:
                    alpha = 1f - t;
                    scale = p.size * Mathf.Lerp(1.2f, 0.4f, t);
                    break;
            }

            if (p.age >= p.life)
            {
                Release(i);
                continue;
            }

            Transform tr = p.renderer.transform;
            tr.position = new Vector3(camPos.x + p.pos.x * unit, camPos.y + p.pos.y * unit, 0f);
            float world = scale * unit / Mathf.Max(p.renderer.sprite.bounds.size.x, 0.0001f);
            tr.localScale = new Vector3(world, world, 1f);
            if (p.spin != 0f) tr.Rotate(0f, 0f, p.spin * dt);
            Color col = p.color;
            col.a *= alpha;
            p.renderer.color = col;
        }
    }

    private void SpawnTrail(Vector2 at, Color color)
    {
        Particle p = Spawn(Kind.Trail, sparkSprite);
        if (p == null) return;
        p.pos = at + Random.insideUnitCircle * 0.01f;
        p.vel = new Vector2(Random.Range(-0.05f, 0.05f), Random.Range(-0.1f, 0f));
        p.life = Random.Range(0.25f, 0.4f);
        p.size = sparkSize * 0.8f;
        p.color = Color.Lerp(color, Color.white, 0.5f);
    }

    private Particle Spawn(Kind kind, Sprite sprite)
    {
        if (sprite == null) return null;
        SpriteRenderer sr = pool.Count > 0 ? pool.Pop() : CreateRenderer();
        sr.sprite = sprite;
        sr.color = Color.clear;
        sr.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        sr.gameObject.SetActive(true);
        Particle p = new Particle { renderer = sr, kind = kind };
        active.Add(p);
        return p;
    }

    private SpriteRenderer CreateRenderer()
    {
        GameObject go = new GameObject("FireworkParticle");
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    private void Release(int index)
    {
        Particle p = active[index];
        active.RemoveAt(index);
        if (p.renderer == null) return;
        p.renderer.gameObject.SetActive(false);
        pool.Push(p.renderer);
    }

    private Camera GetCamera()
    {
        if (cam == null) cam = Camera.main;
        return cam;
    }
}
