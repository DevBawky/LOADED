using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BulletTypeTextEffect : MonoBehaviour
{
    private const string ShaderResourcePath = "Shaders/BulletTypeText";
    private static readonly int EffectModeId = Shader.PropertyToID(
        "_EffectMode");
    private static readonly int MotionIntensityId = Shader.PropertyToID(
        "_MotionIntensity");

    private TMP_Text targetText;
    private Material originalMaterial;
    private Material runtimeMaterial;
    private BulletType bulletType;
    private float appliedMotionIntensity = -1f;

    public BulletType BulletType => bulletType;

    public static void Apply(TMP_Text text, BulletType type)
    {
        if (text == null)
        {
            return;
        }

        BulletTypeTextEffect effect =
            text.GetComponent<BulletTypeTextEffect>();
        if (effect == null)
        {
            effect = text.gameObject.AddComponent<BulletTypeTextEffect>();
        }

        effect.SetType(type);
    }

    private void Awake()
    {
        targetText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        EnsureMaterial();
        EnsureMaterialAssignment();
        RefreshMaterial(true);
    }

    private void LateUpdate()
    {
        EnsureMaterial();
        EnsureMaterialAssignment();
        RefreshMaterial(false);
    }

    private void OnDestroy()
    {
        if (targetText != null
            && runtimeMaterial != null
            && targetText.fontSharedMaterial == runtimeMaterial)
        {
            targetText.fontSharedMaterial = originalMaterial;
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
    }

    public void SetType(BulletType type)
    {
        bulletType = type;
        EnsureMaterial();
        EnsureMaterialAssignment();
        RefreshMaterial(true);
    }

    private void EnsureMaterial()
    {
        targetText ??= GetComponent<TMP_Text>();
        if (targetText == null || runtimeMaterial != null)
        {
            return;
        }

        Shader shader = Resources.Load<Shader>(ShaderResourcePath);
        if (shader == null)
        {
            shader = Shader.Find("LOADED/UI/Bullet Type Text");
        }

        Material source = targetText.fontSharedMaterial;
        if (shader == null || source == null)
        {
            return;
        }

        originalMaterial = source;
        runtimeMaterial = new Material(shader)
        {
            name = $"{source.name} ({bulletType} Type Text)",
            hideFlags = HideFlags.HideAndDontSave
        };
        runtimeMaterial.CopyPropertiesFromMaterial(source);

        if (source.IsKeywordEnabled("OUTLINE_ON"))
        {
            runtimeMaterial.EnableKeyword("OUTLINE_ON");
        }

        if (source.IsKeywordEnabled("UNITY_UI_ALPHACLIP"))
        {
            runtimeMaterial.EnableKeyword("UNITY_UI_ALPHACLIP");
        }

        targetText.fontSharedMaterial = runtimeMaterial;
        targetText.UpdateMeshPadding();
    }

    private void EnsureMaterialAssignment()
    {
        if (targetText == null || runtimeMaterial == null
            || targetText.fontSharedMaterial == runtimeMaterial)
        {
            return;
        }

        targetText.fontSharedMaterial = runtimeMaterial;
        targetText.UpdateMeshPadding();
    }

    private void RefreshMaterial(bool force)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetFloat(EffectModeId, (float)bulletType);
        float motionIntensity = CombatAccessibilitySettings.PresentationIntensity;
        if (!force
            && Mathf.Approximately(
                appliedMotionIntensity,
                motionIntensity))
        {
            return;
        }

        appliedMotionIntensity = motionIntensity;
        runtimeMaterial.SetFloat(MotionIntensityId, motionIntensity);
    }
}
