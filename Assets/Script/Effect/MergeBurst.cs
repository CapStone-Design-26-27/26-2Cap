using System.Collections;
using UnityEngine;

// 합쳐진 공 둘레에 손그림 느낌의 지그재그 강조선을 그렸다가 사라지게 한다.
[RequireComponent(typeof(LineRenderer))]
public class MergeBurst : MonoBehaviour
{
    private LineRenderer line;
    private MergeEffectManager.BurstSettings settings;
    private Transform target;
    private float radius;
    private Color baseColor;
    private float spin;

    // 라인 렌더러 기본값을 잡는다. 매니저가 생성 직후 한 번 호출한다.
    public void Init(Material material, int sortingOrder)
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.material = material;
        line.sortingOrder = sortingOrder;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
    }

    // 대상 공을 따라다니며 강조선 연출을 한 번 재생한다.
    public void Play(Transform ball, float ballRadius, Color color, MergeEffectManager.BurstSettings burst)
    {
        settings = burst;
        target = ball;
        radius = ballRadius;
        baseColor = color;
        spin = Random.Range(-settings.spinSpeed, settings.spinSpeed);

        line.positionCount = settings.spikes * 2;
        line.widthMultiplier = radius * settings.widthRatio;

        transform.position = ball.position;
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        gameObject.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(Animate());
    }

    // 공이 떨어지는 동안에도 강조선이 공을 감싸도록 위치만 따라간다.
    private void LateUpdate()
    {
        if (target != null)
            transform.position = target.position;
    }

    // 안쪽과 바깥쪽 반지름을 번갈아 찍어 톱니 모양 링을 만들고, 약간씩 흔들어 손그림처럼 보이게 한다.
    private void Redraw()
    {
        int count = settings.spikes * 2;
        float step = Mathf.PI * 2f / count;

        for (int i = 0; i < count; i++)
        {
            float angle = i * step + Random.Range(-settings.jitter, settings.jitter) * step;
            float mul = (i % 2 == 0) ? settings.innerRadius : settings.outerRadius;
            float r = radius * mul * (1f + Random.Range(-settings.jitter, settings.jitter));
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f));
        }
    }

    // 튀어나오듯 커졌다가 제자리로 줄고, 끝에서 투명해지며 풀로 돌아간다.
    private IEnumerator Animate()
    {
        float t = 0f;
        float nextRedraw = 0f;
        float popEnd = settings.duration * settings.popRatio;

        while (t < settings.duration)
        {
            if (t >= nextRedraw)
            {
                Redraw();
                nextRedraw += settings.redrawInterval;
            }

            float scale = t < popEnd
                ? Mathf.Lerp(settings.startScale, settings.peakScale, EaseOut(t / popEnd))
                : Mathf.Lerp(settings.peakScale, 1f, (t - popEnd) / (settings.duration - popEnd));
            transform.localScale = Vector3.one * scale;
            transform.Rotate(0f, 0f, spin * Time.deltaTime);

            float fadeStart = settings.duration * (1f - settings.fadeRatio);
            Color c = baseColor;
            c.a *= 1f - Mathf.Clamp01((t - fadeStart) / (settings.duration - fadeStart));
            line.startColor = c;
            line.endColor = c;

            t += Time.deltaTime;
            yield return null;
        }

        target = null;
        gameObject.SetActive(false);
    }

    private static float EaseOut(float x)
    {
        return 1f - (1f - x) * (1f - x);
    }
}
