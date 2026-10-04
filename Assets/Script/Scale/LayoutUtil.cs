using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// BeamLayout, BasketLayout이 에디터에서 오브젝트를 배치할 때 쓰는 공용 함수들.
// 값이 실제로 바뀔 때만 수정하고 저장 대상으로 표시해, 씬이 매 프레임 변경되지 않게 한다.
public static class LayoutUtil
{
    public static void SetLocalPosition(Transform t, Vector3 pos)
    {
        if (t.localPosition == pos) return;
        t.localPosition = pos;
        MarkDirty(t);
    }

    public static void ResetLocalRotation(Transform t)
    {
        if (t.localRotation == Quaternion.identity) return;
        t.localRotation = Quaternion.identity;
        MarkDirty(t);
    }

    // 스프라이트를 세로로 늘려 로컬 y = topY ~ bottomY 구간을 채운다. width가 0 이하면 폭은 유지한다.
    public static void StretchVertical(SpriteRenderer sr, float topY, float bottomY, float width = 0f)
    {
        if (sr == null || sr.sprite == null) return;

        Transform t = sr.transform;
        ResetLocalRotation(t);

        float length = Mathf.Max(0.001f, topY - bottomY);
        Vector2 spriteSize = sr.sprite.bounds.size;
        float finalWidth;

        if (sr.drawMode == SpriteDrawMode.Simple)
        {
            float sx = width > 0f ? width / spriteSize.x : t.localScale.x;
            Vector3 scale = new Vector3(sx, length / spriteSize.y, 1f);
            if (t.localScale != scale) { t.localScale = scale; MarkDirty(t); }
            finalWidth = spriteSize.x * sx;
        }
        else
        {
            Vector2 size = new Vector2(width > 0f ? width : sr.size.x, length);
            if (sr.size != size) { sr.size = size; MarkDirty(sr); }
            if (t.localScale != Vector3.one) { t.localScale = Vector3.one; MarkDirty(t); }
            finalWidth = size.x;
        }

        // 피벗이 가운데가 아니어도 x = 0 중심, bottomY ~ topY에 정확히 맞도록 보정
        Vector2 pivot01 = sr.sprite.pivot / sr.sprite.rect.size;
        SetLocalPosition(t, new Vector3(
            (pivot01.x - 0.5f) * finalWidth,
            bottomY + pivot01.y * length,
            t.localPosition.z));
    }

    // target 아래 렌더러들을 합친 영역을 root 기준 로컬 좌표로 구한다. root의 회전이 0이라고 가정한다.
    public static bool TryGetLocalBounds(Transform root, Transform target, out Bounds local)
    {
        local = default;
        bool found = false;

        foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;

            Vector3 a = root.InverseTransformPoint(r.bounds.min);
            Vector3 b = root.InverseTransformPoint(r.bounds.max);
            Bounds rb = new Bounds((a + b) * 0.5f, Vector3.zero);
            rb.Encapsulate(a);
            rb.Encapsulate(b);

            if (!found) { local = rb; found = true; }
            else local.Encapsulate(rb);
        }
        return found;
    }

    public static void MarkDirty(Object o)
    {
#if UNITY_EDITOR
        EditorUtility.SetDirty(o);
#endif
    }
}
