﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2020 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using SonarLint.VisualStudio.Core.Helpers;
using SonarLint.VisualStudio.IssueVisualization.Editor.LocationTagging;
using SonarLint.VisualStudio.IssueVisualization.Models;
using SonarLint.VisualStudio.IssueVisualization.Security.Models;

namespace SonarLint.VisualStudio.IssueVisualization.Security.Store
{
    internal interface IHotspotsStore
    {
        ReadOnlyObservableCollection<IAnalysisIssueVisualization> GetAll();

        /// <summary>
        /// Adds a given visualization to the list if it does not already exist.
        /// Returns the given visualization, or the existing visualization with the same hotspot key.
        /// </summary>
        IAnalysisIssueVisualization GetOrAdd(IAnalysisIssueVisualization hotspotViz);

        void Remove(IAnalysisIssueVisualization hotspotViz);
    }

    [Export(typeof(IHotspotsStore))]
    [Export(typeof(IIssueLocationStore))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class HotspotsStore : IHotspotsStore, IIssueLocationStore
    {
        private ObservableCollection<IAnalysisIssueVisualization> Hotspots { get; } = new ObservableCollection<IAnalysisIssueVisualization>();

        IAnalysisIssueVisualization IHotspotsStore.GetOrAdd(IAnalysisIssueVisualization hotspotViz)
        {
            var existingHotspot = FindExisting(hotspotViz);

            if (existingHotspot != null)
            {
                return existingHotspot;
            }

            Hotspots.Add(hotspotViz);
            NotifyHotspotChanged(hotspotViz);

            return hotspotViz;
        }

        void IHotspotsStore.Remove(IAnalysisIssueVisualization hotspotViz)
        {
            Hotspots.Remove(hotspotViz);
            NotifyHotspotChanged(hotspotViz);
        }

        private void NotifyHotspotChanged(IAnalysisIssueVisualization changedHotspotViz)
        {
            if (IssuesChanged == null)
            {
                return;
            }

            var hotspotFilePaths = changedHotspotViz
                .GetAllLocations()
                .Select(x => x.CurrentFilePath)
                .Where(x=> !string.IsNullOrEmpty(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            if (hotspotFilePaths.Any())
            {
                IssuesChanged.Invoke(this, new IssuesChangedEventArgs(hotspotFilePaths));
            }
        }

        private IAnalysisIssueVisualization FindExisting(IAnalysisIssueVisualization hotspotViz)
        {
            var key = ((IHotspot)hotspotViz.Issue).HotspotKey;

            return Hotspots.FirstOrDefault(x => ((IHotspot)x.Issue).HotspotKey == key);
        }

        ReadOnlyObservableCollection<IAnalysisIssueVisualization> IHotspotsStore.GetAll()
        {
            return new ReadOnlyObservableCollection<IAnalysisIssueVisualization>(Hotspots);
        }

        public event EventHandler<IssuesChangedEventArgs> IssuesChanged;

        IEnumerable<IAnalysisIssueLocationVisualization> IIssueLocationStore.GetLocations(string filePath)
        {
            var matchingLocations = Hotspots
                .SelectMany(hotspotViz => hotspotViz.GetAllLocations())
                .Where(locationViz => !string.IsNullOrEmpty(locationViz.CurrentFilePath) &&
                                      PathHelper.IsMatchingPath(locationViz.CurrentFilePath, filePath));

            return matchingLocations;
        }

        void IIssueLocationStore.Refresh(IEnumerable<string> affectedFilePaths)
        {
        }
    }
}
