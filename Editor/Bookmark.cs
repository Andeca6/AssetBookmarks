using System;
using UnityEngine;

namespace Andeca.AssetBookmarks
{
    // The id is a GlobalObjectId string: one stable identity for assets, sub-assets, folders and
    // scene objects that survives renames, moves and Editor restarts.
    [Serializable]
    internal sealed class Bookmark
    {
        [SerializeField] private string _id;

        // Last known name, so a bookmark stays recognizable while its target can't be resolved
        // (deleted, or its scene isn't loaded).
        [SerializeField] private string _displayName;

        // Optional user text for when the name alone doesn't tell targets apart, e.g. two "Flipper_L".
        [SerializeField] private string _note;

        public const int MaxNoteLength = 64;

        public Bookmark(string id, string displayName)
        {
            _id = id;
            _displayName = displayName;
        }

        public string Id => _id;

        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        // Bookmarks saved before notes existed deserialize with a null note.
        public string Note
        {
            get => _note ?? string.Empty;
            set => _note = value;
        }
    }
}
