﻿// Copyright (c) Davide Giacometti. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text.Json;
using Community.PowerToys.Run.Plugin.EdgeFavorite.Models;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.EdgeFavorite.Helpers
{
    public class FavoriteProvider : IFavoriteProvider
    {
        private readonly string _path = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Bookmarks");
        private readonly FileSystemWatcher _watcher;
        private FavoriteItem _root;

        public FavoriteItem Root => _root;

        public FavoriteProvider()
        {
            _root = new FavoriteItem();
            InitFavorites();

            _watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(_path)!,
                Filter = Path.GetFileName(_path),
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite,
            };

            _watcher.Changed += (s, e) => InitFavorites();
            _watcher.EnableRaisingEvents = true;
        }

        private void InitFavorites()
        {
            if (!Path.Exists(_path))
            {
                Log.Warn($"Failed to find bookmarks file {_path}", typeof(FavoriteProvider));
                return;
            }

            using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read);
            using var sr = new StreamReader(fs);
            string json = sr.ReadToEnd();
            var parsed = JsonDocument.Parse(json);
            parsed.RootElement.TryGetProperty("roots", out var rootElement);
            if (rootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var newRoot = new FavoriteItem();
            rootElement.TryGetProperty("bookmark_bar", out var bookmarkBarElement);
            if (bookmarkBarElement.ValueKind == JsonValueKind.Object)
            {
                ProcessFavorites(bookmarkBarElement, newRoot, string.Empty, true);
            }

            rootElement.TryGetProperty("other", out var otherElement);
            if (otherElement.ValueKind == JsonValueKind.Object)
            {
                ProcessFavorites(otherElement, newRoot, string.Empty, newRoot.Childrens.Count == 0);
            }

            _root = newRoot;
        }

        private void ProcessFavorites(JsonElement element, FavoriteItem parent, string path, bool root)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("children", out var children))
            {
                var name = element.GetProperty("name").GetString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    if (!root)
                    {
                        path += $"{(string.IsNullOrWhiteSpace(path) ? string.Empty : "/")}{name}";
                    }

                    var folder = new FavoriteItem(name, null, path, FavoriteType.Folder);

                    if (root)
                    {
                        folder = parent;
                    }
                    else
                    {
                        parent.AddChildren(folder);
                    }

                    if (children.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var child in children.EnumerateArray())
                        {
                            ProcessFavorites(child, folder, path, false);
                        }
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("url", out var url))
            {
                var name = element.GetProperty("name").GetString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    path += $"{(string.IsNullOrWhiteSpace(path) ? string.Empty : "/")}{name}";
                    var favorite = new FavoriteItem(name, url.GetString(), path, FavoriteType.Url);
                    parent.AddChildren(favorite);
                }
            }
        }
    }
}
