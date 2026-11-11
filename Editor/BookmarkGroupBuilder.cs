using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Andeca.AssetBookmarks
{
    internal static class BookmarkGroupBuilder
    {
        public static void Build(BookmarkStore store, List<BookmarkGroupView> groups)
        {
            groups.Clear();

            if (store.Assets.Count > 0)
                groups.Add(BuildAssetsGroup(store));

            // Loaded scenes first, in Hierarchy order; the rest keep the order they were bookmarked in.
            var loadedSceneOrder = GetLoadedSceneOrder();
            var sceneGroups = store.SceneGroups.OrderBy(group =>
                loadedSceneOrder.TryGetValue(group.SceneGuid, out var order) ? order : int.MaxValue);

            foreach (var group in sceneGroups)
                groups.Add(BuildSceneGroup(store, group, loadedSceneOrder.ContainsKey(group.SceneGuid)));

            store.SaveDisplayNames();
        }

        public static Object Resolve(string id)
        {
            return GlobalObjectId.TryParse(id, out var globalId)
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId)
                : null;
        }

        private static BookmarkGroupView BuildAssetsGroup(BookmarkStore store)
        {
            var bookmarks = store.Assets;
            var targets = Resolve(bookmarks);
            var rows = new List<BookmarkRow>(bookmarks.Count);

            for (var i = 0; i < bookmarks.Count; i++)
            {
                rows.Add(targets[i] != null
                    ? CreateAvailableRow(store, bookmarks[i], targets[i])
                    : CreateMissingRow(bookmarks[i], "The asset no longer exists."));
            }

            var header = new GUIContent($"Assets ({rows.Count})");
            return new BookmarkGroupView(BookmarkStore.AssetsGroupKey, BookmarkGroupKind.Assets, null, header, rows);
        }

        private static BookmarkGroupView BuildSceneGroup(BookmarkStore store, SceneBookmarkGroup group, bool isLoaded)
        {
            var bookmarks = group.Bookmarks;
            var rows = new List<BookmarkRow>(bookmarks.Count);
            var scenePath = AssetDatabase.GUIDToAssetPath(group.SceneGuid);
            var sceneExists = !string.IsNullOrEmpty(scenePath) && AssetDatabase.AssetPathExists(scenePath);
            var sceneName = Path.GetFileNameWithoutExtension(scenePath);

            BookmarkGroupKind kind;
            string title;

            if (!sceneExists)
            {
                kind = BookmarkGroupKind.MissingScene;
                title = $"Missing scene ({bookmarks.Count})";
                foreach (var bookmark in bookmarks)
                    rows.Add(CreateMissingRow(bookmark, "The scene asset no longer exists."));
            }
            else if (isLoaded)
            {
                kind = BookmarkGroupKind.LoadedScene;
                title = $"{sceneName} ({bookmarks.Count})";
                var targets = Resolve(bookmarks);
                for (var i = 0; i < bookmarks.Count; i++)
                {
                    rows.Add(targets[i] != null
                        ? CreateAvailableRow(store, bookmarks[i], targets[i])
                        : CreateMissingRow(bookmarks[i], "The object no longer exists in this scene."));
                }
            }
            else
            {
                kind = BookmarkGroupKind.UnloadedScene;
                title = $"{sceneName} ({bookmarks.Count}) - not loaded";
                var icon = AssetPreview.GetMiniTypeThumbnail(typeof(GameObject));
                foreach (var bookmark in bookmarks)
                {
                    var content = CreateContent(bookmark, bookmark.DisplayName,
                        "Scene not loaded. Right-click to open it.");
                    rows.Add(new BookmarkRow(bookmark.Id, content, CreateNoteContent(bookmark, content), icon, null,
                        BookmarkRowState.Unavailable));
                }
            }

            var header = new GUIContent(title, sceneExists ? scenePath : null);
            return new BookmarkGroupView(group.SceneGuid, kind, sceneExists ? scenePath : null, header, rows);
        }

        private static BookmarkRow CreateAvailableRow(BookmarkStore store, Bookmark bookmark, Object target)
        {
            var name = BookmarkLabels.GetDisplayName(target);
            store.UpdateDisplayName(bookmark, name);
            var content = CreateContent(bookmark, name, BookmarkLabels.GetLocation(target));
            return new BookmarkRow(bookmark.Id, content, CreateNoteContent(bookmark, content),
                BookmarkLabels.GetIcon(target), target, BookmarkRowState.Available);
        }

        private static BookmarkRow CreateMissingRow(Bookmark bookmark, string reason)
        {
            var name = string.IsNullOrEmpty(bookmark.DisplayName) ? "Unknown" : bookmark.DisplayName;
            var content = CreateContent(bookmark, $"{name} (missing)", $"{reason} Right-click to remove it.");
            return new BookmarkRow(bookmark.Id, content, CreateNoteContent(bookmark, content), null, null,
                BookmarkRowState.Missing);
        }

        // The note leads the tooltip too, since a narrow window clips it in the row.
        private static GUIContent CreateContent(Bookmark bookmark, string text, string tooltip)
        {
            var note = bookmark.Note;
            return new GUIContent(text, note.Length == 0 ? tooltip : $"{note}\n{tooltip}");
        }

        private static GUIContent CreateNoteContent(Bookmark bookmark, GUIContent content)
        {
            var note = bookmark.Note;
            return note.Length == 0 ? null : new GUIContent(note, content.tooltip);
        }

        // One batched lookup per group: the per-object variant searches loaded objects on every call.
        private static Object[] Resolve(IReadOnlyList<Bookmark> bookmarks)
        {
            var ids = new GlobalObjectId[bookmarks.Count];
            for (var i = 0; i < ids.Length; i++)
                GlobalObjectId.TryParse(bookmarks[i].Id, out ids[i]);

            var targets = new Object[ids.Length];
            if (ids.Length > 0)
                GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(ids, targets);
            return targets;
        }

        private static Dictionary<string, int> GetLoadedSceneOrder()
        {
            var order = new Dictionary<string, int>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && !string.IsNullOrEmpty(scene.path))
                    order[AssetDatabase.AssetPathToGUID(scene.path)] = i;
            }
            return order;
        }
    }
}
