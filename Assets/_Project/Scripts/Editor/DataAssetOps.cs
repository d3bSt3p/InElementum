using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace InElementum.Editor
{
    /// <summary>Asset operations shared by the data editor windows and the sample data builder.</summary>
    public static class DataAssetOps
    {
        public static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        public static ScriptableObject Create(Type type, string folder, string baseName)
        {
            EnsureFolder(folder);

            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.asset");
            var asset = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static T Create<T>(string folder, string baseName) where T : ScriptableObject =>
            (T)Create(typeof(T), folder, baseName);

        public static T Duplicate<T>(T source) where T : ScriptableObject
        {
            var sourcePath = AssetDatabase.GetAssetPath(source);
            var path = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            return AssetDatabase.CopyAsset(sourcePath, path) ? AssetDatabase.LoadAssetAtPath<T>(path) : null;
        }

        /// <summary>Moves the asset to the OS trash, so a mistaken delete can be recovered.</summary>
        public static bool Delete(ScriptableObject asset) => AssetDatabase.MoveAssetToTrash(AssetDatabase.GetAssetPath(asset));

        public static List<T> FindAll<T>() where T : ScriptableObject => FindAll(typeof(T)).Cast<T>().ToList();

        public static List<ScriptableObject> FindAll(Type type)
        {
            return AssetDatabase.FindAssets($"t:{type.Name}")
                .Select(guid => AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type))
                .OfType<ScriptableObject>()
                .OrderBy(asset => asset.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
