using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Andeca.AssetBookmarks
{
    public sealed class AssetBookmarksWindow : EditorWindow, IHasCustomMenu
    {
        private const string DragDataKey = "Andeca.AssetBookmarks.DraggedRow";
        private const float RowHeight = 18f;
        private const float RowIndent = 14f;
        private const float IconSize = 16f;
        private const float DragStartDistance = 4f;
        private const double NotificationSeconds = 2.5;

        private static readonly List<AssetBookmarksWindow> OpenWindows = new List<AssetBookmarksWindow>();

        [SerializeField] private string _filter = string.Empty;
        [SerializeField] private Vector2 _scroll;
        // Keys of groups whose foldout differs from their default, so new groups need no entry.
        [SerializeField] private List<string> _toggledGroups = new List<string>();

        private readonly List<BookmarkGroupView> _groups = new List<BookmarkGroupView>();
        private readonly List<VisibleGroup> _visibleGroups = new List<VisibleGroup>();
        private readonly List<Rect> _rowRects = new List<Rect>();

        private SearchField _searchField;
        private GUIStyle _missingLabelStyle;
        private bool _rebuildQueued = true;
        private bool _visibleGroupsDirty = true;
        private bool _hasLoadedSceneGroups;
        private string _visibleFilter = string.Empty;
        private RowKey _selectedRow;
        private Vector2 _pressPosition;
        private string _dropGroupKey;
        private float _dropLineY;

        private bool IsFiltering => !string.IsNullOrEmpty(_visibleFilter);

        [MenuItem("Tools/Asset Bookmarks")]
        public static void Open()
        {
            var window = GetWindow<AssetBookmarksWindow>();
            window.minSize = new Vector2(220f, 140f);
        }

        // Routes feedback to an open window when there is one; menu commands can run without it.
        internal static void Report(AddResult result, AssetBookmarksWindow source = null)
        {
            var message = result.GetMessage();
            if (message == null)
                return;

            var window = source != null ? source : OpenWindows.Count > 0 ? OpenWindows[0] : null;
            if (window != null)
                window.ShowNotification(new GUIContent(message), NotificationSeconds);
            else
                Debug.LogWarning($"[Asset Bookmarks] {message}");
        }

        private void OnEnable()
        {
            OpenWindows.Add(this);
            titleContent = new GUIContent("Bookmarks", EditorGUIUtility.FindTexture("Favorite"));
            _searchField = new SearchField();

            BookmarkStore.Changed += QueueRebuild;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.projectChanged += QueueRebuild;

            QueueRebuild();
        }

        private void OnDisable()
        {
            OpenWindows.Remove(this);

            BookmarkStore.Changed -= QueueRebuild;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.projectChanged -= QueueRebuild;
        }

        private void OnSelectionChange() => Repaint();

        private void OnSceneOpened(Scene scene, OpenSceneMode mode) => QueueRebuild();
        private void OnSceneClosed(Scene scene) => QueueRebuild();
        private void OnPlayModeStateChanged(PlayModeStateChange change) => QueueRebuild();

        private void OnHierarchyChanged()
        {
            // Re-resolves objects that were deleted, restored by undo or reloaded. Skipped in Play Mode, where
            // spawning fires this every frame; destroyed targets are still drawn as missing there.
            if (_hasLoadedSceneGroups && !EditorApplication.isPlaying)
                QueueRebuild();
        }

        private void QueueRebuild()
        {
            _rebuildQueued = true;
            Repaint();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            var clearAll = new GUIContent("Clear All Bookmarks");
            if (_groups.Count > 0)
                menu.AddItem(clearAll, false, ClearAllWithConfirmation);
            else
                menu.AddDisabledItem(clearAll);
        }

        private static void ClearAllWithConfirmation()
        {
            if (EditorUtility.DisplayDialog("Clear All Bookmarks",
                    "Remove every bookmark in this project? You can undo this.", "Clear All", "Cancel"))
                BookmarkStore.instance.Clear();
        }

        private void OnGUI()
        {
            var e = Event.current;

            // IMGUI needs the same controls in Layout and the event that follows it, so the drawn model
            // only changes here, never in the middle of a MouseDown or a KeyDown.
            if (e.type == EventType.Layout)
            {
                if (_rebuildQueued)
                    Rebuild();
                if (_visibleGroupsDirty)
                    RefreshVisibleGroups();
            }

            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_groups.Count == 0)
            {
                EditorGUILayout.HelpBox("Drag assets from the Project window or objects from the Hierarchy here, " +
                    "or right-click them and choose Add to Bookmarks.", MessageType.Info);
            }
            else if (_visibleGroups.Count == 0)
            {
                EditorGUILayout.LabelField($"No bookmarks match \"{_visibleFilter}\".", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                foreach (var visibleGroup in _visibleGroups)
                    DrawGroup(visibleGroup);
            }
            EditorGUILayout.EndScrollView();

            HandleWindowDragAndDrop(e);
            HandleDeleteCommand(e);
        }

        private void Rebuild()
        {
            BookmarkGroupBuilder.Build(BookmarkStore.instance, _groups);

            _hasLoadedSceneGroups = _groups.Exists(group => group.Kind == BookmarkGroupKind.LoadedScene);
            _toggledGroups.RemoveAll(key => !_groups.Exists(group => group.Key == key));
            _rebuildQueued = false;
            _visibleGroupsDirty = true;
        }

        private void RefreshVisibleGroups()
        {
            _visibleGroups.Clear();
            _visibleFilter = _filter;

            foreach (var group in _groups)
            {
                if (!IsFiltering)
                {
                    var expanded = group.ExpandedByDefault != _toggledGroups.Contains(group.Key);
                    _visibleGroups.Add(new VisibleGroup(group, group.Rows, expanded, canReorder: true));
                    continue;
                }

                var matches = group.Rows.FindAll(row =>
                    row.Content.text.IndexOf(_visibleFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                if (matches.Count > 0)
                    _visibleGroups.Add(new VisibleGroup(group, matches, expanded: true, canReorder: false));
            }

            _visibleGroupsDirty = false;
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var addContent = new GUIContent(EditorGUIUtility.FindTexture("Toolbar Plus"),
                    "Bookmark the current selection");
                using (new EditorGUI.DisabledScope(Selection.objects.Length == 0))
                {
                    if (GUILayout.Button(addContent, EditorStyles.toolbarButton, GUILayout.Width(28f)))
                        Report(BookmarkStore.instance.Add(Selection.objects), this);
                }

                GUILayout.Space(4f);

                var filter = _searchField.OnToolbarGUI(_filter);
                if (filter != _filter)
                {
                    _filter = filter;
                    _visibleGroupsDirty = true;
                    Repaint();
                }
            }
        }

        private void DrawGroup(VisibleGroup visibleGroup)
        {
            var group = visibleGroup.Group;
            var expanded = EditorGUILayout.BeginFoldoutHeaderGroup(visibleGroup.Expanded, group.Header, null,
                rect => ShowGroupMenu(group, rect));

            if (expanded != visibleGroup.Expanded && !IsFiltering)
                ToggleGroup(group.Key);

            if (visibleGroup.Expanded)
                DrawRows(visibleGroup);

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void ToggleGroup(string key)
        {
            if (!_toggledGroups.Remove(key))
                _toggledGroups.Add(key);
            _visibleGroupsDirty = true;
            Repaint();
        }

        private void DrawRows(VisibleGroup visibleGroup)
        {
            var e = Event.current;
            var group = visibleGroup.Group;
            _rowRects.Clear();

            foreach (var row in visibleGroup.Rows)
            {
                var rect = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
                _rowRects.Add(rect);
                DrawRow(group, row, rect, e);
            }

            if (!visibleGroup.CanReorder || _rowRects.Count == 0)
                return;

            HandleReorder(group, e);

            if (e.type == EventType.Repaint && _dropGroupKey == group.Key)
            {
                var first = _rowRects[0];
                var line = new Rect(first.x + RowIndent, _dropLineY - 1f, first.width - RowIndent, 2f);
                EditorGUI.DrawRect(line, Palette.DropLine);
            }
        }

        private void DrawRow(BookmarkGroupView group, BookmarkRow row, Rect rect, Event e)
        {
            var key = new RowKey(group.Key, row.Id);
            var controlId = GUIUtility.GetControlID(FocusType.Passive, rect);

            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown when e.button == 0 && rect.Contains(e.mousePosition):
                    _selectedRow = key;
                    if (e.clickCount == 2)
                        OpenTarget(row.Target);
                    else
                        SelectAndPing(row.Target);
                    _pressPosition = e.mousePosition;
                    GUIUtility.hotControl = controlId;
                    e.Use();
                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                    if ((e.mousePosition - _pressPosition).sqrMagnitude >= DragStartDistance * DragStartDistance)
                    {
                        GUIUtility.hotControl = 0;
                        StartDrag(key, row);
                    }
                    e.Use();
                    break;

                case EventType.MouseUp when GUIUtility.hotControl == controlId:
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;

                case EventType.ContextClick when rect.Contains(e.mousePosition):
                    _selectedRow = key;
                    ShowRowMenu(group, row);
                    e.Use();
                    break;

                case EventType.Repaint:
                    PaintRow(row, rect, key.Equals(_selectedRow));
                    break;
            }
        }

        private void PaintRow(BookmarkRow row, Rect rect, bool selected)
        {
            if (selected)
                EditorGUI.DrawRect(rect, hasFocus ? Palette.SelectionFocused : Palette.SelectionUnfocused);

            var iconRect = new Rect(rect.x + RowIndent, rect.y + (rect.height - IconSize) * 0.5f, IconSize, IconSize);
            var labelRect = new Rect(iconRect.xMax + 4f, rect.y, rect.xMax - iconRect.xMax - 4f, rect.height);

            // A target destroyed since the last rebuild (e.g. in Play Mode) is drawn as missing right away.
            var state = row.State == BookmarkRowState.Available && row.Target == null
                ? BookmarkRowState.Missing
                : row.State;

            if (row.Icon != null)
            {
                var previousColor = GUI.color;
                if (state != BookmarkRowState.Available)
                    GUI.color = new Color(1f, 1f, 1f, 0.5f);
                GUI.DrawTexture(iconRect, row.Icon, ScaleMode.ScaleToFit);
                GUI.color = previousColor;
            }

            switch (state)
            {
                case BookmarkRowState.Missing:
                    _missingLabelStyle ??= new GUIStyle(EditorStyles.label);
                    _missingLabelStyle.normal.textColor = Palette.MissingText;
                    GUI.Label(labelRect, row.Content, _missingLabelStyle);
                    break;
                case BookmarkRowState.Unavailable:
                    using (new EditorGUI.DisabledScope(true))
                        GUI.Label(labelRect, row.Content, EditorStyles.label);
                    break;
                default:
                    GUI.Label(labelRect, row.Content, EditorStyles.label);
                    break;
            }
        }

        // Every row drag is a real DragAndDrop with the target attached: dropped inside its group it
        // reorders, dropped anywhere else in the Editor it behaves like dragging the asset or object itself.
        private static void StartDrag(RowKey key, BookmarkRow row)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(DragDataKey, key);

            var target = row.Target;
            DragAndDrop.objectReferences = target != null ? new[] { target } : Array.Empty<Object>();
            DragAndDrop.paths = target != null && AssetDatabase.IsMainAsset(target)
                ? new[] { AssetDatabase.GetAssetPath(target) }
                : Array.Empty<string>();

            DragAndDrop.StartDrag(row.Content.text);
        }

        private void HandleReorder(BookmarkGroupView group, Event e)
        {
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)
                return;
            if (!(DragAndDrop.GetGenericData(DragDataKey) is RowKey dragged) || dragged.GroupKey != group.Key)
                return;

            var first = _rowRects[0];
            var last = _rowRects[_rowRects.Count - 1];
            var area = Rect.MinMaxRect(first.xMin, first.yMin, first.xMax, last.yMax);
            if (!area.Contains(e.mousePosition))
                return;

            var insertIndex = 0;
            while (insertIndex < _rowRects.Count && e.mousePosition.y > _rowRects[insertIndex].center.y)
                insertIndex++;

            DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            _dropGroupKey = group.Key;
            _dropLineY = insertIndex < _rowRects.Count ? _rowRects[insertIndex].yMin : last.yMax;

            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                ClearDropIndicator();
                BookmarkStore.instance.Move(group.Key, dragged.Id, insertIndex);
            }
            e.Use();
        }

        private void HandleWindowDragAndDrop(Event e)
        {
            switch (e.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    // A row dragged outside its own group: bookmarks can't move between groups.
                    if (DragAndDrop.GetGenericData(DragDataKey) is RowKey)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                        ClearDropIndicator();
                        e.Use();
                        return;
                    }

                    if (DragAndDrop.objectReferences.Length == 0)
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (e.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        Report(BookmarkStore.instance.Add(DragAndDrop.objectReferences), this);
                    }
                    e.Use();
                    break;

                case EventType.DragExited:
                    ClearDropIndicator();
                    break;
            }
        }

        private void ClearDropIndicator()
        {
            if (_dropGroupKey == null)
                return;
            _dropGroupKey = null;
            Repaint();
        }

        private void HandleDeleteCommand(Event e)
        {
            if (e.type != EventType.ValidateCommand && e.type != EventType.ExecuteCommand)
                return;
            if (e.commandName != "Delete" && e.commandName != "SoftDelete")
                return;
            // Never let Delete in the search field remove a bookmark.
            if (!_selectedRow.IsValid || EditorGUIUtility.editingTextField)
                return;

            if (e.type == EventType.ExecuteCommand)
            {
                BookmarkStore.instance.Remove(_selectedRow.GroupKey, _selectedRow.Id);
                _selectedRow = default;
            }
            e.Use();
        }

        private static void ShowGroupMenu(BookmarkGroupView group, Rect rect)
        {
            var menu = new GenericMenu();
            if (group.Kind == BookmarkGroupKind.UnloadedScene)
            {
                AddOpenSceneItems(menu, group.ScenePath, null);
                menu.AddSeparator(string.Empty);
            }
            menu.AddItem(new GUIContent("Remove All in Group"), false,
                () => BookmarkStore.instance.RemoveGroup(group.Key));
            menu.DropDown(rect);
        }

        private static void ShowRowMenu(BookmarkGroupView group, BookmarkRow row)
        {
            var menu = new GenericMenu();

            if (row.Target != null)
            {
                var target = row.Target;
                menu.AddItem(new GUIContent("Open"), false, () => OpenTarget(target));
                menu.AddSeparator(string.Empty);
            }
            else if (group.Kind == BookmarkGroupKind.UnloadedScene)
            {
                AddOpenSceneItems(menu, group.ScenePath, row.Id);
                menu.AddSeparator(string.Empty);
            }

            menu.AddItem(new GUIContent("Remove"), false, () => BookmarkStore.instance.Remove(group.Key, row.Id));
            menu.AddItem(new GUIContent("Remove All in Group"), false,
                () => BookmarkStore.instance.RemoveGroup(group.Key));
            menu.ShowAsContext();
        }

        private static void AddOpenSceneItems(GenericMenu menu, string scenePath, string idToSelect)
        {
            var open = new GUIContent("Open Scene");
            var openAdditive = new GUIContent("Open Scene Additive");

            if (EditorApplication.isPlaying)
            {
                menu.AddDisabledItem(open);
                menu.AddDisabledItem(openAdditive);
                return;
            }

            menu.AddItem(open, false, () => OpenScene(scenePath, OpenSceneMode.Single, idToSelect));
            menu.AddItem(openAdditive, false, () => OpenScene(scenePath, OpenSceneMode.Additive, idToSelect));
        }

        private static void OpenScene(string scenePath, OpenSceneMode mode, string idToSelect)
        {
            if (mode == OpenSceneMode.Single && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(scenePath, mode);
            if (idToSelect != null)
                SelectAndPing(BookmarkGroupBuilder.Resolve(idToSelect));
        }

        private static void SelectAndPing(Object target)
        {
            if (target == null)
                return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private static void OpenTarget(Object target)
        {
            if (target == null)
                return;

            // Opening a scene or entering Prefab Mode from inside OnGUI breaks the current layout pass.
            EditorApplication.delayCall += () =>
            {
                if (target == null)
                    return;

                if (EditorUtility.IsPersistent(target))
                {
                    AssetDatabase.OpenAsset(target);
                    return;
                }

                // Same as double-clicking in the Hierarchy.
                Selection.activeObject = target;
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                    sceneView.FrameSelected();
            };
        }

        private readonly struct RowKey : IEquatable<RowKey>
        {
            public RowKey(string groupKey, string id)
            {
                GroupKey = groupKey;
                Id = id;
            }

            public string GroupKey { get; }
            public string Id { get; }
            public bool IsValid => Id != null;

            public bool Equals(RowKey other) => GroupKey == other.GroupKey && Id == other.Id;
            public override bool Equals(object obj) => obj is RowKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(GroupKey, Id);
        }

        private sealed class VisibleGroup
        {
            public VisibleGroup(BookmarkGroupView group, List<BookmarkRow> rows, bool expanded, bool canReorder)
            {
                Group = group;
                Rows = rows;
                Expanded = expanded;
                CanReorder = canReorder;
            }

            public BookmarkGroupView Group { get; }
            public List<BookmarkRow> Rows { get; }
            public bool Expanded { get; }
            public bool CanReorder { get; }
        }

        // Approximations of the Editor's own colors, chosen per skin at draw time so a theme switch applies live.
        private static class Palette
        {
            public static Color SelectionFocused => EditorGUIUtility.isProSkin
                ? new Color(0.17f, 0.36f, 0.53f)
                : new Color(0.23f, 0.45f, 0.69f);

            public static Color SelectionUnfocused => EditorGUIUtility.isProSkin
                ? new Color(0.30f, 0.30f, 0.30f)
                : new Color(0.68f, 0.68f, 0.68f);

            public static Color MissingText => EditorGUIUtility.isProSkin
                ? new Color(1f, 0.45f, 0.45f)
                : new Color(0.70f, 0.10f, 0.10f);

            public static Color DropLine => new Color(0.27f, 0.55f, 0.95f);
        }
    }
}
