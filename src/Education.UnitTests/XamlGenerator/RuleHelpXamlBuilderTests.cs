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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarLint.VisualStudio.Education.Rule;
using SonarLint.VisualStudio.Education.XamlGenerator;
using SonarLint.VisualStudio.SLCore.Service.Rules.Models;
using SonarLint.VisualStudio.TestInfrastructure;

namespace SonarLint.VisualStudio.Education.UnitTests.XamlGenerator;

[TestClass]
public class RuleHelpXamlBuilderTests
{
    [TestMethod]
    public void MefCtor_CheckExports()
    {
        MefTestHelpers.CheckTypeCanBeImported<RuleHelpXamlBuilder, IRuleHelpXamlBuilder>(
            MefTestHelpers.CreateExport<ISimpleRuleHelpXamlBuilder>(),
            MefTestHelpers.CreateExport<IRichRuleHelpXamlBuilder>());
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Create_ChoosesCorrectLayoutBuilder(bool isExtendedRule)
    {
        var ruleInfoMock = new Mock<IRuleInfo>();
        ruleInfoMock.SetupGet(x => x.RichRuleDescriptionDto).Returns(isExtendedRule
            ? new RuleSplitDescriptionDto("intro", new List<RuleDescriptionTabDto>())
            : null);
        var simpleRuleHelpXamlBuilderMock = new Mock<ISimpleRuleHelpXamlBuilder>();
        var richRuleHelpXamlBuilderMock = new Mock<IRichRuleHelpXamlBuilder>();
        var testSubject = new RuleHelpXamlBuilder(simpleRuleHelpXamlBuilderMock.Object, richRuleHelpXamlBuilderMock.Object);

        testSubject.Create(ruleInfoMock.Object);

        simpleRuleHelpXamlBuilderMock.Verify(x => x.Create(ruleInfoMock.Object), isExtendedRule ? Times.Never : Times.Once);
        richRuleHelpXamlBuilderMock.Verify(x => x.Create(ruleInfoMock.Object), isExtendedRule ? Times.Once : Times.Never);
    }
}
