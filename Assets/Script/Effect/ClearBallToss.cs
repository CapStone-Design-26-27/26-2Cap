using System.Collections.Generic;
using UnityEngine;

// 클리어되면 화면 왼쪽 아래와 오른쪽 아래에서 동물들이 저글링하듯 위로 튀어 올랐다가 떨어지기를 반복한다.
// 줌 상태와 상관없이 항상 같은 화면 위치와 크기로 보이며, 다음 스테이지로 넘어가면 사라진다.
public class ClearBallToss : MonoBehaviour
{
    [Header("개수")]
    [Tooltip("한쪽에서 동시에 저글링하는 동물 수")]
    [SerializeField, Min(1)] private int animalsPerSide = 4;
    [Tooltip("클리어 후 동물이 하나씩 나오기 시작하는 간격 (초)")]
    [SerializeField] private float startInterval = 0.18f;

    [Header("움직임 (화면 높이 절반 = 1)")]
    [Tooltip("던지는 위치의 가로 범위. 화면 가장자리 1, 가운데 0")]
    [SerializeField] private Vector2 launchSide = new Vector2(0.75f, 0.95f);
    [Tooltip("튀어 오르는 최고 높이 범위. 화면 아래 -1, 위 1")]
    [SerializeField] private Vector2 peakHeight = new Vector2(-0.15f, 0.35f);
    [Tooltip("한 번 올라갔다 내려오는 시간 범위 (초)")]
    [SerializeField] private Vector2 airTime = new Vector2(1.1f, 1.5f);
    [Tooltip("떨어지는 위치가 던진 위치보다 화면 가운데 쪽으로 들어오는 거리 범위. 화면 가장자리~가운데 = 1")]
    [SerializeField] private Vector2 inwardDrift = new Vector2(0.4f, 0.65f);
    [Tooltip("초당 회전 각도 범위")]
    [SerializeField] private Vector2 spinSpeed = new Vector2(90f, 360f);

    [Header("모양")]
    [Tooltip("동물 크기 범위")]
    [SerializeField] private Vector2 animalSize = new Vector2(0.14f, 0.2f);
    [Tooltip("폭죽 바로 아래에 보이도록 할 정렬 순서")]
    [SerializeField] private int sortingOrder = 190;

    private class Juggler
    {
        public SpriteRenderer renderer;
        public float side;
        public float delay;
        public float timer;
        public float duration;
        public Vector2 start;
        public Vector2 end;
        public float peak;
        public float spin;
        public float size;
        public bool flying;
    }

    private readonly List<Juggler> jugglers = new List<Juggler>();
    private readonly List<Sprite> animals = new List<Sprite>();
    private bool playing;
    private Camera cam;

    private void OnEnable()
    {
        StageManager.OnStageStarted += HandleStageStarted;
    }

    private void OnDisable()
    {
        StageManager.OnStageStarted -= HandleStageStarted;
        Stop();
    }

    private void HandleStageStarted(int stage) => Stop();

    private void LateUpdate()
    {
        bool cleared = ClearFxState.IsCleared && !ClearFxState.IsFailed;
        if (cleared && !playing) Play();
        else if (!cleared && playing) Stop();
        if (!playing) return;

        Camera c = GetCamera();
        if (c == null) return;
        float unit = c.orthographicSize;
        Vector3 camPos = c.transform.position;
        foreach (Juggler j in jugglers)
            Animate(j, c.aspect, unit, camPos);
    }

    // 좌우에 동물들을 배치하고 차례로 던지기 시작한다.
    private void Play()
    {
        playing = true;
        CollectAnimals();
        if (animals.Count == 0) return;

        for (int i = 0; i < animalsPerSide * 2; i++)
        {
            GameObject go = new GameObject("TossAnimal");
            go.transform.SetParent(transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            sr.enabled = false;
            jugglers.Add(new Juggler
            {
                renderer = sr,
                side = i % 2 == 0 ? -1f : 1f,
                delay = (i / 2) * startInterval * 2f + (i % 2) * startInterval,
            });
        }
    }

    // 게임에 등록된 동물 그림을 모은다.
    private void CollectAnimals()
    {
        animals.Clear();
        GameManager game = GameManager.Inst;
        if (game == null) return;
        foreach (GameObject prefab in game.ballList)
        {
            if (prefab == null) continue;
            SpriteRenderer sr = prefab.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null) animals.Add(sr.sprite);
        }
    }

    // 아래에서 던져져 포물선을 그리며 올라갔다 화면 아래로 떨어지면 다시 던진다.
    private void Animate(Juggler j, float aspect, float unit, Vector3 camPos)
    {
        if (j.renderer == null) return;
        if (!j.flying)
        {
            j.delay -= Time.deltaTime;
            if (j.delay > 0f) return;
            Launch(j, aspect);
        }

        j.timer += Time.deltaTime;
        float k = j.timer / j.duration;
        if (k >= 1f)
        {
            Launch(j, aspect);
            k = 0f;
        }

        // 시작과 끝은 화면 아래 밖, 중간에 최고 높이에 닿는 포물선
        Vector2 pos = Vector2.Lerp(j.start, j.end, k);
        float baseY = Mathf.Lerp(j.start.y, j.end.y, k);
        pos.y = baseY + 4f * (j.peak - Mathf.Max(j.start.y, j.end.y)) * k * (1f - k);

        Transform tr = j.renderer.transform;
        tr.position = new Vector3(camPos.x + pos.x * unit, camPos.y + pos.y * unit, 0f);
        float world = j.size * unit / Mathf.Max(j.renderer.sprite.bounds.size.x, j.renderer.sprite.bounds.size.y, 0.0001f);
        tr.localScale = new Vector3(world, world, 1f);
        tr.Rotate(0f, 0f, j.spin * Time.deltaTime);
    }

    // 동물 하나를 새로 골라 화면 아래 모서리에서 위로 던진다.
    private void Launch(Juggler j, float aspect)
    {
        j.flying = true;
        j.timer = 0f;
        j.duration = Random.Range(airTime.x, airTime.y);
        float x = j.side * aspect * Random.Range(launchSide.x, launchSide.y);
        float drift = Random.Range(inwardDrift.x, inwardDrift.y) * aspect;
        j.start = new Vector2(x, -1.25f);
        j.end = new Vector2(x - j.side * drift, -1.25f);
        j.peak = Random.Range(peakHeight.x, peakHeight.y);
        j.spin = Random.Range(spinSpeed.x, spinSpeed.y) * (Random.value < 0.5f ? -1f : 1f);
        j.size = Random.Range(animalSize.x, animalSize.y);
        j.renderer.sprite = animals[Random.Range(0, animals.Count)];
        j.renderer.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        j.renderer.enabled = true;
    }

    // 동물들을 모두 지운다.
    private void Stop()
    {
        playing = false;
        foreach (Juggler j in jugglers)
            if (j.renderer != null) Destroy(j.renderer.gameObject);
        jugglers.Clear();
    }

    private Camera GetCamera()
    {
        if (cam == null) cam = Camera.main;
        return cam;
    }
}
