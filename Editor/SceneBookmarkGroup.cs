using System;
using System.Collections.Generic;
using UnityEngine;

namespace Andeca.AssetBookmarks
{
    [Serializable]
    internal sealed class SceneBookmarkGroup
    {
        [SerializeField] private string _sceneGuid;
        [SerializeField] private List<Bookmark> _bookmarks = new List<Bookmark>();

        public SceneBookmarkGroup(string sceneGuid)
        {
            _sceneGuid = sceneGuid;
        }

        public string SceneGuid => _sceneGuid;
        public List<Bookmark> Bookmarks => _bookmarks;
    }
}
