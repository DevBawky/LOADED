using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LoadedAuthoringCapture
{
    [Serializable] public class Snapshot { public List<Bullet> bullets = new List<Bullet>(); public List<TextEntry> texts = new List<TextEntry>(); }
    [Serializable] public class Bullet { public string guid, path, name, json, hash, icon, iconHash; }
    [Serializable] public class TextEntry { public string key, scope, source, field, ko, en = "", status = "Draft", note; }
    static Snapshot result;
    static string output;
    public static string Main()
    {
        output = Path.GetFullPath("outputs/authoring-20261002");
        Directory.CreateDirectory(output + "/icons");
        result = new Snapshot();
        foreach (var guid in AssetDatabase.FindAssets("t:BulletData", new[] { "Assets" }).OrderBy(x => x))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<BulletData>(path);
            var json = EditorJsonUtility.ToJson(data);
            var bytes = BulletBalanceWorkbook.CaptureIcon(data);
            var iconPath = bytes == null ? "" : output + "/icons/" + guid + ".png";
            if (bytes != null) File.WriteAllBytes(iconPath, bytes);
            result.bullets.Add(new Bullet { guid = guid, path = path, name = data.DisplayName, json = json, hash = Hash(Encoding.UTF8.GetBytes(json)), icon = iconPath, iconHash = bytes == null ? "" : Hash(bytes) });
        }
        foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Scripts", "Assets/Resources", "Assets/Settings" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (obj != null && obj.GetType().Assembly.GetName().Name == "Assembly-CSharp") Scan(obj, path, guid);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root != null) ScanRoot(root, path, guid);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try { foreach (var root in scene.GetRootGameObjects()) ScanRoot(root, path, guid); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        foreach (var path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)) ScanCode(path.Replace('\\', '/'));
        result.texts = result.texts.GroupBy(x => x.key).Select(x => x.First()).OrderBy(x => x.scope).ThenBy(x => x.key).ToList();
        var jsonType = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert");
        var encoded = (string)jsonType.GetMethod("SerializeObject", new[] { typeof(object) }).Invoke(null, new object[] { result });
        File.WriteAllText(output + "/snapshot.json", encoded, new UTF8Encoding(false));
        return "Captured " + result.bullets.Count + " bullets and " + result.texts.Count + " text sites.";
    }
    static void ScanRoot(GameObject root, string path, string guid)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            if (component is TMPro.TMP_Text || component is UnityEngine.UI.Text || component.GetType().Assembly.GetName().Name == "Assembly-CSharp")
                Scan(component, path, guid + "." + GlobalObjectId.GetGlobalObjectIdSlow(component).targetObjectId);
        }
    }
    static void Scan(UnityEngine.Object obj, string path, string id)
    {
        using (var serialized = new SerializedObject(obj))
        {
            var p = serialized.GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.String || string.IsNullOrWhiteSpace(p.stringValue)) continue;
                if (p.name == "m_Name" || Regex.IsMatch(p.name, "(Id|ID|Path|Name)$") && p.name != "displayName" && p.name != "stageName") continue;
                bool textField = Regex.IsMatch(p.name, "(?i)(description|dialogue|outcome|reason|title|label|message|text|displayName|stageName)");
                if (!Regex.IsMatch(p.stringValue, "[가-힣]") && !textField) continue;
                result.texts.Add(new TextEntry { key = "asset." + id + "." + p.propertyPath, scope = obj.GetType().Name, source = path + " :: " + obj.name, field = p.propertyPath, ko = p.stringValue, note = "Serialized text; preserve rich text and placeholders." });
            }
        }
    }
    static void ScanCode(string path) { }
    public static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    public static byte[] Icon(Sprite sprite)
    {
        if (sprite == null) return null;
        var texture = sprite.texture;
        var previous = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Texture2D copy = null;
        try
        {
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            var rect = sprite.rect;
            copy = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(rect, 0, 0); copy.Apply();
            return copy.EncodeToPNG();
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); if (copy != null) UnityEngine.Object.DestroyImmediate(copy); }
    }
}
