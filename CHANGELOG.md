# Changelog

All notable changes to this package are documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package uses
[Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-11-11

### Added

- Asset Bookmarks window (**Tools > Asset Bookmarks**) for Project assets, folders, sub-assets, scenes,
  GameObjects and components.
- Scene objects grouped by scene, including scenes that aren't loaded, which can be opened from the
  context menu.
- Click to select and ping, double-click to open. Selection happens on release, so dragging a bookmark
  keeps the Inspector on the object receiving the reference.
- Drag a bookmark into object fields, the Scene view or the Hierarchy, and drag within a group to reorder.
- Short notes on bookmarks, shown next to the name and editable in place with F2 or the context menu.
- Filter by name or note in the toolbar.
- **Add to Bookmarks** in the Project and Hierarchy context menus, plus an unbound
  **Asset Bookmarks > Add Selection** shortcut.
- Undo support for every change.
- Per-user, per-project storage in `UserSettings/`.
