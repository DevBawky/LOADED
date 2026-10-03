using System.Collections.Generic;
using UnityEngine;

public static class BulletIconPresenter
{
    private readonly struct Key
    {
        public Key(BulletType type, BulletGrade grade, bool preview, int spriteId)
        {
            Type = type;
            Grade = grade;
            Preview = preview;
            SpriteId = spriteId;
        }

        public BulletType Type { get; }
        public BulletGrade Grade { get; }
        public bool Preview { get; }
        public int SpriteId { get; }

        public override int GetHashCode() =>
            ((int)Type * 397) ^ ((int)Grade * 17) ^ (Preview ? 1 : 0)
            ^ SpriteId;

        public override bool Equals(object obj) => obj is Key other
            && Type == other.Type && Grade == other.Grade
            && Preview == other.Preview && SpriteId == other.SpriteId;
    }

    private static readonly Dictionary<Key, Material> Materials =
        new Dictionary<Key, Material>();
    private static Shader shader;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        foreach (Material material in Materials.Values)
            if (material != null) Object.Destroy(material);
        Materials.Clear();
        shader = null;
    }

    public static void Apply(
        UnityEngine.UI.Image image,
        BulletData bullet,
        bool staticPreview = false)
    {
        if (image == null)
            return;

        Sprite sprite = bullet == null ? null : bullet.CylinderIcon;
        image.sprite = sprite;
        image.preserveAspect = true;
        image.enabled = sprite != null;
        image.material = bullet == null || sprite == null
            ? null
            : GetMaterial(bullet.BulletType, bullet.Grade, staticPreview, sprite);
    }

    public static void Apply(
        UnityEngine.UI.Image image,
        BulletInstance bullet,
        bool staticPreview = false)
    {
        Apply(image, bullet == null ? null : bullet.Data, staticPreview);
    }

    private static Material GetMaterial(
        BulletType type,
        BulletGrade grade,
        bool preview,
        Sprite sprite)
    {
        Key key = new Key(type, grade, preview, sprite.GetInstanceID());
        if (Materials.TryGetValue(key, out Material existing)
            && existing != null)
            return existing;

        shader ??= Shader.Find("LOADED/UI/Bullet Line Art");
        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = $"BulletIcon_{type}_{grade}_{sprite.name}_{(preview ? "Preview" : "Live")}",
            hideFlags = HideFlags.HideAndDontSave
        };
        material.SetFloat("_TypeMode", (float)type);
        material.SetFloat("_GradeMode", (float)grade);
        material.SetFloat("_Motion", preview ? 0f : 1f);
        material.SetColor("_GradeColor", BulletData.GetDefaultGradeColor(grade));
        material.SetVector("_SpriteUvRect", GetSpriteUvRect(sprite));
        Materials[key] = material;
        return material;
    }

    private static Vector4 GetSpriteUvRect(Sprite sprite)
    {
        Texture2D texture = sprite.texture;
        Rect rect;
        try
        {
            rect = sprite.textureRect;
        }
        catch (UnityException)
        {
            rect = sprite.rect;
        }

        float width = Mathf.Max(1f, texture.width);
        float height = Mathf.Max(1f, texture.height);
        return new Vector4(
            rect.x / width,
            rect.y / height,
            rect.width / width,
            rect.height / height);
    }
}
