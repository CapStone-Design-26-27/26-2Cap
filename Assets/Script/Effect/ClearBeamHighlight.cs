using System.Collections.Generic;
using UnityEngine;

// 공이 담긴 채 수평으로 균형을 이룬 저울대를 무지개색으로 반짝이게 한다. 균형이 풀리면 원래대로 돌아간다.
// 클리어 판정 중이거나 클리어되면 모든 저울대를 강조하고, 둘레에 만화풍 집중선과 빛 번짐을 더한다.
public class ClearBeamHighlight : MonoBehaviour
{
    [Header("에셋")]
    [Tooltip("저울대를 무지개색으로 빛나게 하는 셰이더")]
    [SerializeField] private Shader shineShader;
    [SerializeField] private Sprite glowSprite;
    [Tooltip("번갈아 보여 손그림처럼 떨리게 할 집중선 프레임")]
    [SerializeField] private Sprite[] raySprites;
    [SerializeField] private Sprite sparkleSprite;

    [Header("균형 판정")]
    [Tooltip("이 시간 동안 계속 균형이면 반짝이기 시작한다 (초)")]
    [SerializeField] private float balanceHoldTime = 0.3f;
    [Tooltip("이보다 빠르게 흔들리는 중이면 균형으로 보지 않는다 (도/초)")]
    [SerializeField] private float maxSwingSpeed = 6f;
    [Tooltip("평소 균형일 때도 집중선과 빛 번짐을 보여 준다")]
    [SerializeField] private bool showRaysWhenBalanced = false;

    [Header("무지개색")]
    [Tooltip("초당 색상환 회전 수")]
    [SerializeField] private float hueSpeed = 0.2f;

    [Header("빛 번짐")]
    [Tooltip("저울대 바깥으로 번지는 두께 (월드 단위)")]
    [SerializeField] private float glowPadding = 0.6f;
    [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.85f;
    [SerializeField] private float pulseSpeed = 6f;

    [Header("집중선")]
    [Tooltip("집중선 이미지에서 비어 있는 안쪽 영역의 반폭/반높이 비율")]
    [SerializeField] private Vector2 rayInnerHalfSize = new Vector2(0.40f, 0.175f);
    [Tooltip("저울대와 집중선 안쪽 사이 간격 (월드 단위)")]
    [SerializeField] private float rayGap = 0.35f;
    [SerializeField] private float rayFrameInterval = 0.09f;
    [SerializeField] private float rayPulse = 0.05f;
    [SerializeField, Range(0f, 1f)] private float raySaturation = 0.3f;

    [Header("반짝이")]
    [SerializeField, Min(0)] private int sparklesPerBeam = 6;
    [SerializeField] private float sparkleSize = 1.2f;
    [SerializeField] private Vector2 sparkleLife = new Vector2(0.35f, 0.7f);

    [Header("나타남/사라짐")]
    [SerializeField] private float fadeInTime = 0.25f;
    [SerializeField] private float fadeOutTime = 0.3f;

    private static readonly int FillId = Shader.PropertyToID("_Fill");
    private static readonly int HueSpeedId = Shader.PropertyToID("_HueSpeed");

    private class Sparkle
    {
        public SpriteRenderer renderer;
        public float age;
        public float life;
        public float spin;
        public Vector3 offset;
    }

    private class BeamFx
    {
        public ScaleBeam beam;
        public SpriteRenderer rod;
        public Vector2 rodSize;
        public Vector3 rodCenter;
        public readonly List<SpriteRenderer> shined = new List<SpriteRenderer>();
        public readonly List<Material> originalMaterials = new List<Material>();
        public bool shining;
        public SpriteRenderer glow;
        public SpriteRenderer rays;
        public readonly List<Sparkle> sparkles = new List<Sparkle>();
        public float phase;
        public float intensity;
        public float rayIntensity;
        public float balancedTime;
        public float lastAngle;
    }

    private readonly List<BeamFx> beams = new List<BeamFx>();
    private readonly List<GameObject> created = new List<GameObject>();
    private MaterialPropertyBlock block;
    private Material shineMaterial;
    private ScaleSystem builtFor;
    private float rayTimer;
    private int rayFrame;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (shineShader != null)
        {
            shineMaterial = new Material(shineShader) { name = "ClearShine (Runtime)" };
            shineMaterial.SetFloat(HueSpeedId, hueSpeed);
        }
    }

    private void LateUpdate()
    {
        if (builtFor != ScaleSystem.Inst) Build();
        if (beams.Count == 0) return;

        rayTimer += Time.deltaTime;
        if (rayTimer >= rayFrameInterval)
        {
            rayTimer = 0f;
            rayFrame++;
        }

        bool clearing = ClearFxState.IsClearing;
        foreach (BeamFx fx in beams)
        {
            if (fx.rod == null || fx.beam == null) continue;
            bool balanced = UpdateBalance(fx);
            bool on = clearing || balanced;
            bool raysOn = clearing || (balanced && showRaysWhenBalanced);
            fx.intensity = Fade(fx.intensity, on);
            fx.rayIntensity = Fade(fx.rayIntensity, raysOn);
            UpdateBeam(fx);
        }
    }

    private float Fade(float current, bool on)
    {
        float time = on ? fadeInTime : fadeOutTime;
        return Mathf.MoveTowards(current, on ? 1f : 0f, Time.deltaTime / Mathf.Max(time, 0.01f));
    }

    private void OnDestroy()
    {
        foreach (BeamFx fx in beams) SetShining(fx, false);
        if (shineMaterial != null) Destroy(shineMaterial);
    }

    // 저울대에 공이 담겨 있고, 수평이며, 흔들림이 멈춘 상태가 잠시 이어지면 균형으로 본다.
    private bool UpdateBalance(BeamFx fx)
    {
        float angle = fx.beam.CurrentAngle;
        float swing = Time.deltaTime > 0f ? Mathf.Abs(Mathf.DeltaAngle(fx.lastAngle, angle)) / Time.deltaTime : 0f;
        fx.lastAngle = angle;

        bool level = !ClearFxState.IsFailed && Mathf.Abs(angle) <= ClearFxState.LevelTolerance && swing <= maxSwingSpeed && HasBalls(fx.beam);

        fx.balancedTime = level ? fx.balancedTime + Time.deltaTime : 0f;
        return fx.balancedTime >= balanceHoldTime;
    }

    // 저울대 아래 매달린 바구니 중 공이 든 것이 있는지
    private static bool HasBalls(ScaleBeam beam)
    {
        foreach (ScaleBeam.Attachment a in beam.Attachments)
        {
            if (a.node is Basket basket)
            {
                if (basket.Sensor != null && basket.Sensor.TotalWeight > 0.0001f) return true;
            }
            else if (a.node is ScaleBeam child && HasBalls(child))
            {
                return true;
            }
        }
        return false;
    }

    // 현재 맵의 저울대마다 강조용 오브젝트를 만든다. 맵이 바뀌면 다시 만든다.
    private void Build()
    {
        foreach (BeamFx fx in beams) SetShining(fx, false);
        foreach (GameObject go in created)
            if (go != null) Destroy(go);
        created.Clear();
        beams.Clear();
        builtFor = ScaleSystem.Inst;
        if (builtFor == null) return;

        foreach (HangingNode node in builtFor.AllNodes)
        {
            if (!(node is ScaleBeam beam)) continue;
            BeamFx fx = new BeamFx { beam = beam, phase = beams.Count * 0.17f, lastAngle = beam.CurrentAngle };

            float bestWidth = -1f;
            foreach (SpriteRenderer sr in beam.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sprite == null || sr.GetComponentInParent<HangingNode>() != beam) continue;
                if (!sr.enabled || Mathf.Max(sr.color.r, sr.color.g, sr.color.b) < 0.05f) continue;
                fx.shined.Add(sr);
                fx.originalMaterials.Add(sr.sharedMaterial);

                Vector2 size = GetLocalSize(sr);
                float width = size.x * Mathf.Abs(sr.transform.lossyScale.x);
                if (width > bestWidth)
                {
                    bestWidth = width;
                    fx.rod = sr;
                    fx.rodSize = size;
                    fx.rodCenter = sr.drawMode == SpriteDrawMode.Simple ? sr.sprite.bounds.center : Vector3.zero;
                }
            }
            if (fx.rod == null) continue;

            fx.glow = CreateChild("ClearGlow", fx.rod, glowSprite, fx.rod.sortingOrder - 1);
            if (fx.glow != null) fx.glow.drawMode = SpriteDrawMode.Sliced;

            Sprite firstRay = raySprites != null && raySprites.Length > 0 ? raySprites[0] : null;
            fx.rays = CreateChild("ClearRays", fx.rod, firstRay, fx.rod.sortingOrder - 2);

            if (sparkleSprite != null)
            {
                for (int i = 0; i < sparklesPerBeam; i++)
                {
                    SpriteRenderer sr = CreateChild("ClearSparkle", fx.rod, sparkleSprite, fx.rod.sortingOrder + 1);
                    Sparkle sparkle = new Sparkle { renderer = sr };
                    Respawn(fx, sparkle);
                    sparkle.age = Random.Range(0f, sparkle.life);
                    fx.sparkles.Add(sparkle);
                }
            }

            beams.Add(fx);
        }
    }

    private static Vector2 GetLocalSize(SpriteRenderer sr)
    {
        return sr.drawMode == SpriteDrawMode.Simple ? (Vector2)sr.sprite.bounds.size : sr.size;
    }

    // 강조용 스프라이트를 하나 만든다.
    private SpriteRenderer CreateChild(string objectName, SpriteRenderer parent, Sprite sprite, int order)
    {
        if (sprite == null) return null;
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        created.Add(go);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = parent.sortingLayerID;
        sr.sortingOrder = order;
        sr.enabled = false;
        return sr;
    }

    // 저울대 본체를 무지개색 반짝임으로 바꾸거나 원래 모습으로 되돌린다.
    private void SetShining(BeamFx fx, bool shining)
    {
        if (fx.shining == shining || shineMaterial == null) return;
        fx.shining = shining;
        for (int i = 0; i < fx.shined.Count; i++)
        {
            SpriteRenderer sr = fx.shined[i];
            if (sr == null) continue;
            sr.sharedMaterial = shining ? shineMaterial : fx.originalMaterials[i];
        }
    }

    private void UpdateBeam(BeamFx fx)
    {
        float t = Time.time;
        Transform rodTr = fx.rod.transform;
        Vector3 lossy = rodTr.lossyScale;
        Vector2 rodWorld = new Vector2(fx.rodSize.x * Mathf.Abs(lossy.x), fx.rodSize.y * Mathf.Abs(lossy.y));
        Vector3 center = rodTr.TransformPoint(fx.rodCenter);

        // 저울대 본체를 무지개색으로 빛나게 한다.
        SetShining(fx, fx.intensity > 0f);
        if (fx.shining)
        {
            foreach (SpriteRenderer sr in fx.shined)
            {
                if (sr == null) continue;
                sr.GetPropertyBlock(block);
                block.SetFloat(FillId, fx.intensity);
                sr.SetPropertyBlock(block);
            }
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(t * pulseSpeed + fx.phase * 10f);
        bool raysVisible = fx.rayIntensity > 0f;

        if (fx.glow != null)
        {
            fx.glow.enabled = raysVisible;
            if (raysVisible)
            {
                fx.glow.transform.SetPositionAndRotation(center, rodTr.rotation);
                fx.glow.size = rodWorld + Vector2.one * glowPadding * 2f;
                Color c = Color.HSVToRGB(Mathf.Repeat(t * hueSpeed + fx.phase + 0.15f, 1f), 0.85f, 1f);
                c.a = glowAlpha * fx.rayIntensity * Mathf.Lerp(0.6f, 1f, pulse);
                fx.glow.color = c;
            }
        }

        if (fx.rays != null)
        {
            fx.rays.enabled = raysVisible;
            if (raysVisible)
            {
                if (raySprites.Length > 0) fx.rays.sprite = raySprites[rayFrame % raySprites.Length];
                Vector2 spriteSize = fx.rays.sprite.bounds.size;
                float halfW = rodWorld.x * 0.5f + rayGap;
                float halfH = rodWorld.y * 0.5f + rayGap;
                float pop = Mathf.Lerp(0.85f, 1f, fx.rayIntensity) * (1f + rayPulse * pulse);
                fx.rays.transform.SetPositionAndRotation(center, rodTr.rotation);
                fx.rays.transform.localScale = new Vector3(
                    halfW / (rayInnerHalfSize.x * spriteSize.x) * pop,
                    halfH / (rayInnerHalfSize.y * spriteSize.y) * pop, 1f);
                Color c = Color.HSVToRGB(Mathf.Repeat(t * hueSpeed + fx.phase + 0.5f, 1f), raySaturation, 1f);
                c.a = fx.rayIntensity;
                fx.rays.color = c;
            }
        }

        foreach (Sparkle s in fx.sparkles)
            UpdateSparkle(fx, s, rodTr);
    }

    // 반짝이가 저울대 위 임의 위치에서 커졌다 작아지며 반복해서 나타난다.
    private void UpdateSparkle(BeamFx fx, Sparkle s, Transform rodTr)
    {
        if (s.renderer == null) return;
        bool visible = fx.intensity > 0f;
        s.renderer.enabled = visible;
        if (!visible) return;

        s.age += Time.deltaTime;
        if (s.age >= s.life) Respawn(fx, s);

        float k = Mathf.Sin(Mathf.Clamp01(s.age / s.life) * Mathf.PI);
        float size = sparkleSize * k / Mathf.Max(s.renderer.sprite.bounds.size.x, 0.0001f);
        s.renderer.transform.position = rodTr.TransformPoint(s.offset);
        s.renderer.transform.localScale = new Vector3(size, size, 1f);
        s.renderer.transform.Rotate(0f, 0f, s.spin * Time.deltaTime);
        Color c = Color.HSVToRGB(Mathf.Repeat(Time.time * hueSpeed + fx.phase + 0.3f, 1f), 0.25f, 1f);
        c.a = fx.intensity;
        s.renderer.color = c;
    }

    private void Respawn(BeamFx fx, Sparkle s)
    {
        s.age = 0f;
        s.life = Random.Range(sparkleLife.x, sparkleLife.y);
        s.spin = Random.Range(-180f, 180f);
        float x = Random.Range(-0.5f, 0.5f) * fx.rodSize.x;
        float y = Random.Range(-0.5f, 0.5f) * fx.rodSize.y * 2.5f;
        s.offset = fx.rodCenter + new Vector3(x, y, 0f);
        s.renderer.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
    }
}
