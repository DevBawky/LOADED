using UnityEngine;
using UnityEngine.UI;

public enum EnemyAttackIconType { Melee, Ranged, Throw, Shotgun, Bomb }

// Resolution-independent silhouettes, shared by normal enemies and bosses.
public sealed class EnemyIntentGraphic : MaskableGraphic
{
    private EnemyTurnActionType action;
    private Vector2 direction = Vector2.right;
    private bool attackReady;
    private float emberTime;
    private EnemyAttackIconType attackType;

    internal void SetAttackType(EnemyAttackIconType value)
    {
        if (attackType == value) return;
        attackType = value;
        SetVerticesDirty();
    }

    internal void SetAttackReady(bool ready)
    {
        if (attackReady == ready) return;
        attackReady = ready;
        emberTime = 0f;
        // The background signals readiness; the silhouette stays legible.
        color = Color.white;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (!attackReady || GamePauseController.IsPaused) return;
        emberTime += Time.unscaledDeltaTime;
        SetVerticesDirty();
    }

    public void SetIntent(EnemyTurnActionType value, Vector2 movementDirection)
    {
        Vector2 next = movementDirection.sqrMagnitude > 0.001f
            ? movementDirection.normalized : Vector2.right;
        if (action == value && (next - direction).sqrMagnitude < 0.0001f) return;
        action = value;
        direction = next;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (attackReady)
        {
            for (int i = 0; i < 5; i++)
            {
                float x = -0.3f + i * 0.15f;
                float flicker = (Mathf.Sin(emberTime * 9f + i * 2.4f) + 1f) * 0.5f;
                Triangle(mesh, new Vector2(x - 0.08f, -0.3f),
                    new Vector2(x + 0.08f, -0.3f),
                    new Vector2(x + Mathf.Sin(emberTime * 4f + i) * 0.045f, 0.05f + flicker * 0.34f),
                    new Color(1f, 0.04f, 0f, 0.15f + flicker * 0.18f));
            }
        }
        if (action == EnemyTurnActionType.Move)
        {
            Vector2 side = new Vector2(-direction.y, direction.x);
            Line(mesh, -direction * 0.35f, direction * 0.3f, 0.11f);
            Triangle(mesh, direction * 0.46f, direction * 0.05f + side * 0.27f,
                direction * 0.05f - side * 0.27f);
        }
        else if (action == EnemyTurnActionType.Rotate)
        {
            float sign = direction.x < 0 ? -1f : 1f;
            Vector2 previous = Vector2.zero;
            for (int i = 0; i <= 20; i++)
            {
                float angle = Mathf.Lerp(-150, 100, i / 20f) * Mathf.Deg2Rad;
                Vector2 point = new Vector2(Mathf.Cos(angle) * sign, Mathf.Sin(angle)) * 0.3f;
                if (i > 0) Line(mesh, previous, point, 0.09f);
                previous = point;
            }
            Triangle(mesh, new Vector2(-0.3f * sign, 0.28f),
                new Vector2(0.06f * sign, 0.46f), new Vector2(0.06f * sign, 0.1f));
        }
        else if (action == EnemyTurnActionType.Support)
        {
            Line(mesh, new Vector2(-0.32f, 0f), new Vector2(0.32f, 0f), 0.15f);
            Line(mesh, new Vector2(0f, -0.32f), new Vector2(0f, 0.32f), 0.15f);
        }
        else if (action == EnemyTurnActionType.Fire || action == EnemyTurnActionType.PrepareAttack)
        {
            if (attackType == EnemyAttackIconType.Ranged || attackType == EnemyAttackIconType.Shotgun)
            {
                Line(mesh, new Vector2(-0.3f, 0.16f), new Vector2(0.36f, 0.16f), 0.2f);
                Line(mesh, new Vector2(-0.2f, 0.1f), new Vector2(-0.3f, -0.32f), 0.17f);
                Line(mesh, new Vector2(0.33f, 0.11f), new Vector2(0.43f, 0.11f), 0.09f);
                Line(mesh, new Vector2(-0.11f, -0.14f), new Vector2(0.09f, -0.14f), 0.045f);
                Line(mesh, new Vector2(0.09f, -0.14f), new Vector2(0.12f, 0.08f), 0.045f);
                return;
            }
            if (attackType == EnemyAttackIconType.Throw || attackType == EnemyAttackIconType.Bomb)
            {
                Vector2 center = new Vector2(0.14f, -0.13f);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI / 8f;
                    float b = (i + 1) * Mathf.PI / 8f;
                    Triangle(mesh, center, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.23f,
                        center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 0.23f);
                }
                Line(mesh, new Vector2(0.14f, 0.06f), new Vector2(0.14f, 0.2f), 0.09f);
                Line(mesh, new Vector2(0.14f, 0.2f), new Vector2(0.27f, 0.29f), 0.05f);
                Line(mesh, new Vector2(-0.4f, -0.04f), new Vector2(-0.29f, 0.2f), 0.055f);
                Line(mesh, new Vector2(-0.29f, 0.2f), new Vector2(-0.08f, 0.32f), 0.055f);
                Triangle(mesh, new Vector2(0.05f, 0.36f), new Vector2(-0.14f, 0.41f), new Vector2(-0.09f, 0.22f));
                return;
            }
            Vector2 axis = new Vector2(1, 1).normalized;
            Vector2 side = new Vector2(-axis.y, axis.x);
            Line(mesh, -axis * 0.38f, axis * 0.27f, 0.105f);
            Triangle(mesh, axis * 0.47f, axis * 0.22f + side * 0.08f,
                axis * 0.22f - side * 0.08f);
            Line(mesh, -axis * 0.14f - side * 0.23f, -axis * 0.14f + side * 0.23f, 0.09f);
        }
        else
        {
            Line(mesh, new Vector2(-0.28f, 0.37f), new Vector2(0.28f, 0.37f), 0.09f);
            Line(mesh, new Vector2(-0.28f, -0.37f), new Vector2(0.28f, -0.37f), 0.09f);
            Line(mesh, new Vector2(-0.23f, 0.32f), new Vector2(0.23f, -0.32f), 0.055f);
            Line(mesh, new Vector2(0.23f, 0.32f), new Vector2(-0.23f, -0.32f), 0.055f);
            Triangle(mesh, new Vector2(-0.15f, -0.29f), new Vector2(0.15f, -0.29f), Vector2.zero);
        }
    }

    private void Line(VertexHelper mesh, Vector2 start, Vector2 end, float width)
    {
        Vector2 delta = (end - start).normalized;
        Vector2 side = new Vector2(-delta.y, delta.x) * width * 0.5f;
        Triangle(mesh, start - side, start + side, end + side);
        Triangle(mesh, start - side, end + side, end - side);
    }

    private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color? tint = null)
    {
        Rect rect = GetPixelAdjustedRect();
        float size = Mathf.Min(rect.width, rect.height);
        int index = mesh.currentVertCount;
        mesh.AddVert(rect.center + a * size, tint ?? color, Vector2.zero);
        mesh.AddVert(rect.center + b * size, tint ?? color, Vector2.zero);
        mesh.AddVert(rect.center + c * size, tint ?? color, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
    }
}
