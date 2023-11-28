﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2023 SonarSource SA
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

using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using SonarLint.VisualStudio.Core.Configuration;
using SonarLint.VisualStudio.Core.Transition;

namespace SonarLint.VisualStudio.Integration.Transition
{
    [Export(typeof(IMuteIssuesWindowService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class MuteIssuesWindowService : IMuteIssuesWindowService
    {
        private readonly IConnectedModeFeaturesConfiguration connectedModeFeaturesConfiguration;

        [ImportingConstructor]
        public MuteIssuesWindowService(IConnectedModeFeaturesConfiguration connectedModeFeaturesConfiguration)
        {
            this.connectedModeFeaturesConfiguration = connectedModeFeaturesConfiguration;
        }

        [ExcludeFromCodeCoverage]
        public MuteIssuesWindowResponse Show(string issueKey)
        {
            var dialog = new MuteWindowDialog(connectedModeFeaturesConfiguration.IsAcceptTransitionAvailable());
            dialog.Owner = Application.Current.MainWindow;
            var dialogResult = dialog.ShowDialog();

            return new MuteIssuesWindowResponse
            {
                Result = dialogResult.GetValueOrDefault(),
                IssueTransition = dialog.SelectedIssueTransition.GetValueOrDefault(),
                Comment = dialog.Comment
            };
        }
    }
}
