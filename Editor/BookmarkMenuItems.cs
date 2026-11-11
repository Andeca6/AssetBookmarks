using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;

namespace Andeca.AssetBookmarks
{
    internal static class BookmarkMenuItems
    {
        private const int MenuPriority = 30;

        [MenuItem("Assets/Add to Bookmarks", false, MenuPriority)]
        private static void AddAssets()
        {
            AssetBookmarksWindow.Report(BookmarkStore.instance.Add(Selection.objects.Where(EditorUtility.IsPersistent)));
        }

        [MenuItem("Assets/Add to Bookmarks", true)]
        private static bool CanAddAssets() => Selection.objects.Any(EditorUtility.IsPersistent);

        [MenuItem("GameObject/Add to Bookmarks", false, MenuPriority)]
        private static void AddGameObjects(MenuCommand command)
        {
            // From the Hierarchy context menu this runs once per selected object, each with its own context;
            // from the main menu it runs once without one. Only one call adds the whole selection.
            if (command.context != null && command.context != Selection.activeObject)
                return;

            var sceneObjects = Selection.gameObjects.Where(gameObject => !EditorUtility.IsPersistent(gameObject));
            AssetBookmarksWindow.Report(BookmarkStore.instance.Add(sceneObjects));
        }

        [MenuItem("GameObject/Add to Bookmarks", true)]
        private static bool CanAddGameObjects() =>
            Selection.gameObjects.Any(gameObject => !EditorUtility.IsPersistent(gameObject));

        // Unbound by default so it never collides with a project's shortcuts; assign it in Edit > Shortcuts.
        [Shortcut("Asset Bookmarks/Add Selection")]
        private static void AddSelectionShortcut()
        {
            if (Selection.objects.Length > 0)
                AssetBookmarksWindow.Report(BookmarkStore.instance.Add(Selection.objects));
        }
    }
}
