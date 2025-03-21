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

using SonarQube.Client.Models;
using SonarQube.Client.Requests;

namespace SonarQube.Client.Api;

interface IGetIssuesRequest : IRequest<SonarQubeIssue[]>
{
    string ProjectKey { get; set; }

    string Statuses { get; set; }

    /// <summary>
    /// The branch name to fetch.
    /// </summary>
    /// <remarks>If the value is null/empty, the main branch will be fetched</remarks>
    string Branch { get; set; }

    string[] IssueKeys { get; set; }

    string RuleId { get; set; }

    string ComponentKey { get; set; }

    string Languages { get; set; }

    // Update <see cref="V7_20.GetIssuesRequestWrapper"/> when adding properties here.
}
