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

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Core.Analysis;
using SonarLint.VisualStudio.IssueVisualization.Models;
using SonarLint.VisualStudio.IssueVisualization.OpenInIde;
using SonarLint.VisualStudio.SLCore.Common.Helpers;
using SonarLint.VisualStudio.SLCore.Listener.Visualization.Models;
using SonarLint.VisualStudio.TestInfrastructure;

namespace SonarLint.VisualStudio.IssueVisualization.UnitTests.OpenInIDE;

[TestClass]
public class OpenInIdeConverterImplementationTests
{
    [TestMethod]
    public void MefCtor_CheckIsExported()
    {
        MefTestHelpers.CheckTypeCanBeImported<OpenInIdeConverterImplementation, IOpenInIdeConverterImplementation>(
            MefTestHelpers.CreateExport<IAnalysisIssueVisualizationConverter>(),
            MefTestHelpers.CreateExport<ILogger>());
    }

    [TestMethod]
    public void MefCtor_CheckIsSingleton()
    {
        MefTestHelpers.CheckIsSingletonMefComponent<OpenInIdeConverterImplementation>();
    }

    [DataTestMethod]
    [DataRow(typeof(NullReferenceException), false)]
    [DataRow(typeof(ArgumentException), false)]
    [DataRow(typeof(DivideByZeroException), true)]
    public void TryConvertIssue_DtoConverterThrows_ExceptionHandledDependingOnSeverity(Type exceptionType, bool isCriticalException)
    {
        var dtoToIssueConverter = Substitute.For<IOpenInIdeIssueToAnalysisIssueConverter<IOpenInIdeIssue>>();
        var exception = Activator.CreateInstance(exceptionType) as Exception;
        var testSubject = CreateTestSubject( out _, out var logger);
        dtoToIssueConverter.Convert(default, default).ThrowsForAnyArgs(exception);

        Func<bool> act = () => testSubject.TryConvert(default, default, dtoToIssueConverter, out _);

        if (isCriticalException)
        {
            act.Should().Throw<Exception>();
        }
        else
        {
            act.Should().NotThrow();
            act().Should().BeFalse();
            logger.AssertPartialOutputStringExists("[Open in IDE] Unable to convert issue data:");
        }
    }

    [DataTestMethod]
    [DataRow(typeof(NullReferenceException), false)]
    [DataRow(typeof(ArgumentException), false)]
    [DataRow(typeof(DivideByZeroException), true)]
    public void TryConvertIssue_VisualisationConverterThrows_ExceptionHandledDependingOnSeverity(Type exceptionType, bool isCriticalException)
    {
        var exception = Activator.CreateInstance(exceptionType) as Exception;
        var dtoToIssueConverter = Substitute.For<IOpenInIdeIssueToAnalysisIssueConverter<IOpenInIdeIssue>>();
        var testSubject = CreateTestSubject(out var visualizationConverter, out var logger);
        var (rootPath, dto, analysisIssueBase) = SetUpDtoConverter(dtoToIssueConverter);
        visualizationConverter.Convert(analysisIssueBase).Throws(exception);

        Func<bool> act = () => testSubject.TryConvert(dto, rootPath, dtoToIssueConverter, out _);

        if (isCriticalException)
        {
            act.Should().Throw<Exception>();
        }
        else
        {
            act.Should().NotThrow();
            act().Should().BeFalse();
            logger.AssertPartialOutputStringExists("[Open in IDE] Unable to convert issue data:");
        }
    }

    [TestMethod]
    public void TryConvertIssue_VisualisationConverterThrows_ExceptionHandledDependingOnSeverity()
    {
        var dtoToIssueConverter = Substitute.For<IOpenInIdeIssueToAnalysisIssueConverter<IOpenInIdeIssue>>();
        var testSubject = CreateTestSubject(out var visualizationConverter, out var logger);
        var (rootPath, dto, analysisIssueBase) = SetUpDtoConverter(dtoToIssueConverter);
        var analysisIssueVisualization = Substitute.For<IAnalysisIssueVisualization>();
        visualizationConverter.Convert(analysisIssueBase).Returns(analysisIssueVisualization);

        testSubject.TryConvert(dto, rootPath, dtoToIssueConverter, out var visualization).Should().BeTrue();

        visualization.Should().BeSameAs(analysisIssueVisualization);
        logger.AssertNoOutputMessages();
    }

    private static (string rootPath, IOpenInIdeIssue issueDetail, IAnalysisIssueBase analysisIssueBase) SetUpDtoConverter(IOpenInIdeIssueToAnalysisIssueConverter<IOpenInIdeIssue> converter)
    {
        const string rootPath = "rootpath";
        var issueDetail = Substitute.For<IOpenInIdeIssue>();
        var analysisIssueBase = Substitute.For<IAnalysisIssueBase>();
        converter.Convert(issueDetail, rootPath).Returns(analysisIssueBase);
        return (rootPath, issueDetail, analysisIssueBase);
    }

    private OpenInIdeConverterImplementation CreateTestSubject(out IAnalysisIssueVisualizationConverter issueToVisualizationConverter, out TestLogger logger)
    {
        issueToVisualizationConverter = Substitute.For<IAnalysisIssueVisualizationConverter>();
        logger = new();
        return new(issueToVisualizationConverter, logger);
    }
}
