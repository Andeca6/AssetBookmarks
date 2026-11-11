using System.Text;
using UnityEditor;
using UnityEngine;

namespace Andeca.AssetBookmarks
{
    internal static class BookmarkLabels
    {
        public static string GetDisplayName(Object target)
        {
            // Components are named after their GameObject; the type tells several of them apart.
            return target is Component component
                ? $"{component.gameObject.name} ({component.GetType().Name})"
                : target.name;
        }

        public static string GetLocation(Object target)
        {
            if (EditorUtility.IsPersistent(target))
            {
                var path = AssetDatabase.GetAssetPath(target);
                return AssetDatabase.IsMainAsset(target) ? path : $"{path} > {target.name}";
            }

            switch (target)
            {
                case GameObject gameObject:
                    return GetHierarchyPath(gameObject.transform);
                case Component component:
                    return $"{GetHierarchyPath(component.transform)} ({component.GetType().Name})";
                default:
                    return target.name;
            }
        }

        public static Texture GetIcon(Object target)
        {
            if (target is GameObject gameObject && !EditorUtility.IsPersistent(gameObject))
                return PrefabUtility.GetIconForGameObject(gameObject);

            if (EditorUtility.IsPersistent(target) && AssetDatabase.IsMainAsset(target))
                return AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(target));

            return EditorGUIUtility.ObjectContent(target, target.GetType()).image;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var path = new StringBuilder(transform.name);
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                path.Insert(0, '/').Insert(0, parent.name);
            return path.ToString();
        }
    }
}
