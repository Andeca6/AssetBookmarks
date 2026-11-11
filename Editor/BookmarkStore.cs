using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Andeca.AssetBookmarks
{
    /// <summary>
    /// Single source of truth for bookmarks. Saved under UserSettings/, so bookmarks are per project and
    /// per user, and stay out of version control with Unity's standard ignore rules.
    /// </summary>
    [FilePath("UserSettings/AssetBookmarks.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class BookmarkStore : ScriptableSingleton<BookmarkStore>
    {
        public const string AssetsGroupKey = "assets";

        // GlobalObjectId.identifierType: 0 = null, 1 = imported asset, 2 = scene object, 3 = source asset.
        private const int NullIdentifierType = 0;
        private const int SceneObjectIdentifierType = 2;

        [SerializeField] private List<Bookmark> _assets = new List<Bookmark>();
        [SerializeField] private List<SceneBookmarkGroup> _sceneGroups = new List<SceneBookmarkGroup>();

        // Undo restores serialized state without telling anyone. Comparing revisions separates our own
        // undo steps from the rest of the Editor's, so the file is only rewritten when it actually changed.
        [SerializeField] private int _revision;
        [NonSerialized] private int _savedRevision;
        [NonSerialized] private bool _displayNamesDirty;

        public static event Action Changed;

        public IReadOnlyList<Bookmark> Assets => _assets;
        public IReadOnlyList<SceneBookmarkGroup> SceneGroups => _sceneGroups;
        public bool IsEmpty => _assets.Count == 0 && _sceneGroups.Count == 0;

        private void OnEnable()
        {
            _savedRevision = _revision;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        public AddResult Add(IEnumerable<Object> objects)
        {
            var result = new AddResult();
            var undoRecorded = false;

            foreach (var obj in objects)
            {
                if (obj == null)
                    continue;

                var rejection = Validate(obj, out var globalId);
                if (rejection != AddRejection.None)
                {
                    result.Reject(rejection);
                    continue;
                }

                var id = globalId.ToString();
                var isSceneObject = globalId.identifierType == SceneObjectIdentifierType;
                var sceneGuid = isSceneObject ? globalId.assetGUID.ToString() : null;

                var existing = isSceneObject ? FindSceneGroup(sceneGuid)?.Bookmarks : _assets;
                if (existing != null && IndexOf(existing, id) >= 0)
                {
                    result.CountAlreadyBookmarked();
                    continue;
                }

                if (!undoRecorded)
                {
                    Undo.RecordObject(this, "Add Bookmark");
                    undoRecorded = true;
                }

                var target = isSceneObject ? GetOrCreateSceneGroup(sceneGuid).Bookmarks : _assets;
                target.Add(new Bookmark(id, BookmarkLabels.GetDisplayName(obj)));
                result.CountAdded();
            }

            if (result.Added > 0)
                Commit();
            return result;
        }

        public void Remove(string groupKey, string id)
        {
            var bookmarks = GetBookmarks(groupKey);
            var index = bookmarks == null ? -1 : IndexOf(bookmarks, id);
            if (index < 0)
                return;

            Undo.RecordObject(this, "Remove Bookmark");
            bookmarks.RemoveAt(index);
            if (bookmarks.Count == 0 && groupKey != AssetsGroupKey)
                _sceneGroups.RemoveAll(group => group.SceneGuid == groupKey);
            Commit();
        }

        public void RemoveGroup(string groupKey)
        {
            if (groupKey == AssetsGroupKey)
            {
                if (_assets.Count == 0)
                    return;
                Undo.RecordObject(this, "Remove Bookmarks");
                _assets.Clear();
            }
            else
            {
                var group = FindSceneGroup(groupKey);
                if (group == null)
                    return;
                Undo.RecordObject(this, "Remove Bookmarks");
                _sceneGroups.Remove(group);
            }
            Commit();
        }

        /// <param name="insertIndex">Position between rows (0..Count) where the bookmark is dropped.</param>
        public void Move(string groupKey, string id, int insertIndex)
        {
            var bookmarks = GetBookmarks(groupKey);
            var from = bookmarks == null ? -1 : IndexOf(bookmarks, id);
            if (from < 0)
                return;

            // Removing the item first shifts every later slot up by one.
            var to = Mathf.Clamp(insertIndex > from ? insertIndex - 1 : insertIndex, 0, bookmarks.Count - 1);
            if (to == from)
                return;

            Undo.RecordObject(this, "Reorder Bookmarks");
            var bookmark = bookmarks[from];
            bookmarks.RemoveAt(from);
            bookmarks.Insert(to, bookmark);
            Commit();
        }

        public void Clear()
        {
            if (IsEmpty)
                return;
            Undo.RecordObject(this, "Clear Bookmarks");
            _assets.Clear();
            _sceneGroups.Clear();
            Commit();
        }

        // Display names are a cache of the target's current name rather than user intent, so they are
        // refreshed without an undo step or change notification; renames shouldn't flood the undo history.
        public void UpdateDisplayName(Bookmark bookmark, string displayName)
        {
            if (bookmark.DisplayName == displayName)
                return;
            bookmark.DisplayName = displayName;
            _displayNamesDirty = true;
        }

        public void SaveDisplayNames()
        {
            if (!_displayNamesDirty)
                return;
            _displayNamesDirty = false;
            Save(true);
        }

        private static AddRejection Validate(Object obj, out GlobalObjectId globalId)
        {
            globalId = default;

            if (!EditorUtility.IsPersistent(obj))
            {
                var gameObject = obj as GameObject;
                if (gameObject == null && obj is Component component)
                    gameObject = component.gameObject;
                if (gameObject == null)
                    return AddRejection.Unsupported;

                // Prefab Mode and other preview scenes are temporary, so their objects have no lasting identity.
                if (EditorSceneManager.IsPreviewSceneObject(gameObject))
                    return AddRejection.PrefabMode;

                // Objects created at runtime don't exist in the saved scene and could never be resolved again.
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return AddRejection.PlayMode;

                // An unsaved scene has no asset GUID yet, so its ids would break on the first save.
                if (string.IsNullOrEmpty(gameObject.scene.path))
                    return AddRejection.UnsavedScene;
            }

            globalId = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            return globalId.identifierType == NullIdentifierType ? AddRejection.Unsupported : AddRejection.None;
        }

        private void OnUndoRedo()
        {
            if (_revision != _savedRevision)
                Persist();
        }

        private void Commit()
        {
            _revision++;
            Persist();
        }

        private void Persist()
        {
            Save(true);
            _savedRevision = _revision;
            _displayNamesDirty = false;
            Changed?.Invoke();
        }

        private List<Bookmark> GetBookmarks(string groupKey)
        {
            return groupKey == AssetsGroupKey ? _assets : FindSceneGroup(groupKey)?.Bookmarks;
        }

        private SceneBookmarkGroup FindSceneGroup(string sceneGuid)
        {
            foreach (var group in _sceneGroups)
            {
                if (group.SceneGuid == sceneGuid)
                    return group;
            }
            return null;
        }

        private SceneBookmarkGroup GetOrCreateSceneGroup(string sceneGuid)
        {
            var group = FindSceneGroup(sceneGuid);
            if (group != null)
                return group;

            group = new SceneBookmarkGroup(sceneGuid);
            _sceneGroups.Add(group);
            return group;
        }

        private static int IndexOf(List<Bookmark> bookmarks, string id)
        {
            for (var i = 0; i < bookmarks.Count; i++)
            {
                if (bookmarks[i].Id == id)
                    return i;
            }
            return -1;
        }
    }
}
