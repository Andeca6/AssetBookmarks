namespace Andeca.AssetBookmarks
{
    internal enum AddRejection
    {
        None,
        Unsupported,
        PlayMode,
        PrefabMode,
        UnsavedScene,
    }

    internal sealed class AddResult
    {
        private AddRejection _firstRejection;

        public int Added { get; private set; }
        public int AlreadyBookmarked { get; private set; }
        public int Rejected { get; private set; }

        public void CountAdded() => Added++;
        public void CountAlreadyBookmarked() => AlreadyBookmarked++;

        public void Reject(AddRejection reason)
        {
            if (Rejected == 0)
                _firstRejection = reason;
            Rejected++;
        }

        // A message only when the outcome would otherwise be confusing: silent success is the norm.
        public string GetMessage()
        {
            if (Rejected > 0)
            {
                var reason = DescribeRejection(_firstRejection);
                return Added > 0 ? $"Added {Added}. {reason}" : reason;
            }

            if (Added == 0 && AlreadyBookmarked > 0)
                return "Already bookmarked.";

            return null;
        }

        private static string DescribeRejection(AddRejection reason)
        {
            switch (reason)
            {
                case AddRejection.PlayMode:
                    return "Scene objects can't be bookmarked in Play Mode.";
                case AddRejection.PrefabMode:
                    return "Objects in Prefab Mode can't be bookmarked. Bookmark the prefab asset instead.";
                case AddRejection.UnsavedScene:
                    return "Save the scene before bookmarking its objects.";
                default:
                    return "This object can't be bookmarked.";
            }
        }
    }
}
