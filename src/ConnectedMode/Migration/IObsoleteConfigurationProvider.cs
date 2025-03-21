﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2025 SonarSource SA
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

using SonarLint.VisualStudio.ConnectedMode.Binding;

namespace SonarLint.VisualStudio.ConnectedMode.Migration
{
    /// <summary>
    /// Service to return the configuration for "old" Connected Mode settings i.e. pre-unintrusive Connected Mode
    /// </summary>
    /// <remarks>This service is only used by the settings migration and cleanup components to help users migrate
    /// to the new Connected Mode settings format and locations. It and all of the implementing classes can be
    /// dropped at some point in the future i.e. once we think users have had enough opportunity to move to the
    /// new format.
    /// See https://github.com/SonarSource/sonarlint-visualstudio/issues/4171
    /// </remarks>
    internal interface IObsoleteConfigurationProvider
    {
        /// <summary>
        /// Returns the binding configuration for the current solution
        /// </summary>
        LegacyBindingConfiguration GetConfiguration();
    }
}
