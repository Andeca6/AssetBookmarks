using System.Collections.Generic;
using UnityEngine;

namespace Andeca.AssetBookmarks
{
    internal enum BookmarkGroupKind
    {
        Assets,
        LoadedScene,
        UnloadedScene,
        MissingScene,
    }

    internal enum BookmarkRowState
    {
        Available,
        Missing,
        Unavailable,
    }

    // Immutable snapshots of the store, resolved once per rebuild so drawing never touches
    // the AssetDatabase or the (slow) GlobalObjectId lookups.
    internal sealed class BookmarkRow
    {
        public BookmarkRow(string id, GUIContent content, Texture icon, Object target, BookmarkRowState state)
        {
            Id = id;
            Content = content;
            Icon = icon;
            Target = target;
            State = state;
        }

        public string Id { get; }
        public GUIContent Content { get; }
        public Texture Icon { get; }
        public Object Target { get; }
        public BookmarkRowState State { get; }
    }

    internal sealed class BookmarkGroupView
    {
        public BookmarkGroupView(string key, BookmarkGroupKind kind, string scenePath, GUIContent header,
            List<BookmarkRow> rows)
        {
            Key = key;
            Kind = kind;
            ScenePath = scenePath;
            Header = header;
            Rows = rows;
        }

        public string Key { get; }
        public BookmarkGroupKind Kind { get; }
        public string ScenePath { get; }
        public GUIContent Header { get; }
        public List<BookmarkRow> Rows { get; }

        // Groups that can't be used right now start collapsed to keep the window focused on the open scenes.
        public bool ExpandedByDefault => Kind == BookmarkGroupKind.Assets || Kind == BookmarkGroupKind.LoadedScene;
    }
}
