# Asset Bookmarks

An editor window for Unity that keeps the assets and scene objects you are working with one click away,
however deep they sit in the Project window or the Hierarchy.

## Features

- **Bookmark anything:** assets, folders, sub-assets (such as a single sprite from a sprite sheet),
  scenes, prefabs, GameObjects and individual components.
- **Grouped by scene:** scene objects are grouped under the scene they belong to. Groups for scenes that
  aren't loaded stay visible (collapsed) and can open their scene from the context menu.
- **Stable references:** bookmarks survive renames, moves, reparenting and Editor restarts.
- **Click to ping, double-click to open:** a click selects the item and highlights it in the Project
  window or the Hierarchy. A double-click opens it: a scene loads, a prefab enters Prefab Mode, a script
  opens in your IDE, a GameObject is framed in the Scene view.
- **Drag out as a reference:** drag a bookmark into any object field, the Scene view or the Hierarchy,
  exactly as if you were dragging the original item. The Inspector keeps showing the object you are
  editing, so no lock is needed.
- **Notes:** add a short note to any bookmark when its name isn't enough, such as two objects both
  called `Flipper_L`. Notes show next to the name and are included in the filter.
- **Reorder** by dragging within a group, and **filter** by name or note from the toolbar.
- **Undo support** for adding, removing, reordering and clearing.
- **Clear feedback:** missing items keep their last known name, so you can tell what was deleted.

## Requirements

Unity 6000.0 or newer. Tested on Unity 6000.6.

## Installation

In Unity, open **Window > Package Manager**, click **+ > Install package from git URL…** and enter:

```
https://github.com/Andeca6/AssetBookmarks.git
```

To pin a specific version, append its tag, for example `…/AssetBookmarks.git#v1.0.0`.

## Usage

Open the window from **Tools > Asset Bookmarks**.

| To | Do this |
|---|---|
| Add items | Drag them into the window, or right-click them in the Project window or the Hierarchy and choose **Add to Bookmarks**, or select them and press **+** in the toolbar. |
| Add with a shortcut | Assign one to **Asset Bookmarks > Add Selection** in **Edit > Shortcuts**. It ships unbound to avoid conflicts. |
| Select and ping | Click a bookmark. |
| Open | Double-click a bookmark, or right-click it and choose **Open**. |
| Use as a reference | Drag a bookmark onto an object field, the Scene view or the Hierarchy. |
| Add or edit a note | Select a bookmark and press **F2** (**Return** on macOS), or right-click it and choose **Edit Note...**. Press **Enter** to save or **Esc** to cancel. Notes are limited to 64 characters. |
| Reorder | Drag a bookmark up or down within its group. |
| Remove | Select a bookmark and press **Delete**, or right-click it and choose **Remove**. |
| Open a scene that isn't loaded | Right-click its group header or one of its bookmarks and choose **Open Scene** or **Open Scene Additive**. |
| Clear everything | Use **Clear All Bookmarks** in the window's tab menu (⋮). |

## How bookmarks are stored

Bookmarks are saved to `UserSettings/AssetBookmarks.asset` in your project. They are personal to each
user and project, and Unity's standard `.gitignore` already keeps the `UserSettings` folder out of version
control.

Each bookmark is stored as a
[`GlobalObjectId`](https://docs.unity3d.com/ScriptReference/GlobalObjectId.html), Unity's persistent
identity for assets and scene objects, together with its last known name.

## Limitations

- Scene objects can't be bookmarked while their scene is unsaved, while they are open in Prefab Mode,
  or during Play Mode. Objects created at runtime don't exist in the saved scene. Existing bookmarks
  keep working in Play Mode.
- Bookmarks can't be moved between groups, and reordering is disabled while a filter is active.

## License

[MIT](LICENSE.md) © Andeca

## Contributing

This is a personal portfolio project and doesn't accept pull requests. Bug reports are welcome through
Issues.
