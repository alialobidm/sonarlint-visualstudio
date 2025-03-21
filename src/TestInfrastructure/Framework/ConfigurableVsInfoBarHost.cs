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

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.Shell.Interop;

namespace SonarLint.VisualStudio.TestInfrastructure
{
    public class ConfigurableVsInfoBarHost : IVsInfoBarHost
    {
        private readonly List<IVsUIElement> elements = new List<IVsUIElement>();

        #region IVsInfoBarHost

        void IVsInfoBarHost.AddInfoBar(IVsUIElement uiElement)
        {
            this.elements.Contains(uiElement).Should().BeFalse();
            this.elements.Add(uiElement);
        }

        void IVsInfoBarHost.RemoveInfoBar(IVsUIElement uiElement)
        {
            this.elements.Contains(uiElement).Should().BeTrue();
            this.elements.Remove(uiElement);
        }

        #endregion IVsInfoBarHost

        #region Test helpers

        public void AssertInfoBars(int expectedNumberOfInfoBars)
        {
            this.elements.Should().HaveCount(expectedNumberOfInfoBars, "Unexpected number of info bars");
        }

        public IEnumerable<ConfigurableVsInfoBarUIElement> MockedElements
        {
            get
            {
                return this.elements.OfType<ConfigurableVsInfoBarUIElement>();
            }
        }

        #endregion Test helpers
    }
}
