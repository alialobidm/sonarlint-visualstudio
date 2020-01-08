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
using System.Text.RegularExpressions;
using System.Windows.Threading;
using FluentAssertions;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarLint.VisualStudio.Integration.UnitTests.Binding
{
    [TestClass]
    public class ProjectSystemFilterTests
    {
        private ConfigurableServiceProvider serviceProvider;
        private ConfigurableVsProjectSystemHelper projectSystem;

        [TestInitialize]
        public void TestInitialize()
        {
            this.serviceProvider = new ConfigurableServiceProvider();

            this.projectSystem = new ConfigurableVsProjectSystemHelper(this.serviceProvider);
            this.serviceProvider.RegisterService(typeof(IProjectSystemHelper), this.projectSystem);

            var host = new ConfigurableHost(this.serviceProvider, Dispatcher.CurrentDispatcher);

            var propertyManager = new ProjectPropertyManager(host);
            var mefExports = MefTestHelpers.CreateExport<IProjectPropertyManager>(propertyManager);
            var mefModel = ConfigurableComponentModel.CreateWithExports(mefExports);
            this.serviceProvider.RegisterService(typeof(SComponentModel), mefModel);
        }

        #region Tests

        [TestMethod]
        public void Ctor_ArgChecks()
        {
            Exceptions.Expect<ArgumentNullException>(() => new ProjectSystemFilter(null));
        }

        [TestMethod]
        public void IsAccepted_ArgumentChecks()
        {
            // Arrange
            var testSubject = this.CreateTestSubject();

            // Test case 1: null
            // Act + Assert
            Exceptions.Expect<ArgumentNullException>(() => testSubject.IsAccepted(null));

            // Test case 2: project is not a IVsHierarchy
            // Arrange
            this.projectSystem.SimulateIVsHierarchyFailure = true;

            // Act + Assert
            Exceptions.Expect<ArgumentException>(() => testSubject.IsAccepted(new ProjectMock("harry.proj")));
        }

        [TestMethod]
        public void IsAccepted_SupportedProject_ProjectExcludedViaProjectProperty()
        {
            IsAccepted_SupportedProject_ProjectExcludedViaProjectProperty_Impl(ProjectSystemHelper.CSharpCoreProjectKind);
            IsAccepted_SupportedProject_ProjectExcludedViaProjectProperty_Impl(ProjectSystemHelper.CSharpProjectKind);

            IsAccepted_SupportedProject_ProjectExcludedViaProjectProperty_Impl(ProjectSystemHelper.VbCoreProjectKind);
            IsAccepted_SupportedProject_ProjectExcludedViaProjectProperty_Impl(ProjectSystemHelper.VbProjectKind);
        }

        private void IsAccepted_SupportedProject_ProjectExcludedViaProjectProperty_Impl(string projectTypeGuid)
        {
            // Arrange
            var testSubject = this.CreateTestSubject();
            var project = new ProjectMock("supported.proj");
            project.ProjectKind = projectTypeGuid;
            project.SetBuildProperty(Constants.SonarQubeTestProjectBuildPropertyKey, "False"); // Should not matter

            // Test case 1: missing property-> is accepted
            // Act
            var result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeTrue("Project with missing property SonarQubeExclude should be accepted");

            // Test case 2: property non-bool -> is accepted
            // Arrange
            project.SetBuildProperty(Constants.SonarQubeExcludeBuildPropertyKey, string.Empty);

            // Act
            result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeTrue("Project with non-bool property SonarQubeExclude should be accepted");

            // Test case 3: property non-bool, non-empty -> is accepted
            // Arrange
            project.SetBuildProperty(Constants.SonarQubeExcludeBuildPropertyKey, "abc");

            // Act
            result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeTrue("Project with non-bool property SonarQubeExclude should be accepted");

            // Test case 4: property true -> not accepted
            // Arrange
            project.SetBuildProperty(Constants.SonarQubeExcludeBuildPropertyKey, "true");

            // Act
            result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeFalse("Project with property SonarQubeExclude=false should NOT be accepted");

            // Test case 5: property false -> is accepted
            // Arrange
            project.SetBuildProperty(Constants.SonarQubeExcludeBuildPropertyKey, "false");

            // Act
            result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeTrue("Project with property SonarQubeExclude=true should be accepted");
        }

        [TestMethod]
        public void IsAccepted_SupportedNotExcludedProject_TestProjectExcludedViaProjectProperty()
        {
            IsAccepted_SupportedNotExcludedProject_TestProjectExcludedViaProjectProperty_Impl(ProjectSystemHelper.CSharpCoreProjectKind);
            IsAccepted_SupportedNotExcludedProject_TestProjectExcludedViaProjectProperty_Impl(ProjectSystemHelper.CSharpProjectKind);

            IsAccepted_SupportedNotExcludedProject_TestProjectExcludedViaProjectProperty_Impl(ProjectSystemHelper.VbCoreProjectKind);
            IsAccepted_SupportedNotExcludedProject_TestProjectExcludedViaProjectProperty_Impl(ProjectSystemHelper.VbProjectKind);
        }

        private void IsAccepted_SupportedNotExcludedProject_TestProjectExcludedViaProjectProperty_Impl(string projectTypeGuid)
        {
            // Arrange
            var testSubject = this.CreateTestSubject();

            var project = new LegacyProjectMock("supported.proj");
            project.ProjectKind = projectTypeGuid;
            project.SetBuildProperty(Constants.SonarQubeExcludeBuildPropertyKey, "false"); // Should evaluate test projects even if false

            // Test case 1: missing property -> accepted
            // Act
            var result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeTrue("Project with missing property SonarQubeTestProject should be accepted");

            // Test case 2: empty -> accepted
            // Arrange
            project.SetBuildProperty(Constants.SonarQubeTestProjectBuildPropertyKey, string.Empty);

            // Act
            result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeTrue("Project with non-bool property SonarQubeTestProject should be accepted");

            // Test case 3: non-bool, non-empty -> treat as false -> is accepted
            // Arrange
            project.SetBuildProperty(Constants.SonarQubeTestProjectBuildPropertyKey, "123");

            // Act
            result = testSubject.IsAccepted(project);
            result.Should().BeTrue();

            // Test case 4: property true -> not accepted
            // Arrange
            project.SetBuildProperty(Constants.SonarQubeTestProjectBuildPropertyKey, "true");

            // Act
            result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeFalse("Project with property SonarQubeTestProject=false should NOT be accepted");

            // Test case 5: property false -> is accepted
            // Arrange
            project.SetBuildProperty(Constants.SonarQubeTestProjectBuildPropertyKey, "false");

            // Act
            result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeTrue("Project with property SonarQubeTestProject=true should be accepted");
        }

        [TestMethod]
        public void IsAccepted_SupportedNotExcludedProject_IsKnownTestProject()
        {
            IsAccepted_SupportedNotExcludedProject_IsKnownTestProject_Impl(ProjectSystemHelper.CSharpCoreProjectKind);
            IsAccepted_SupportedNotExcludedProject_IsKnownTestProject_Impl(ProjectSystemHelper.CSharpProjectKind);

            IsAccepted_SupportedNotExcludedProject_IsKnownTestProject_Impl(ProjectSystemHelper.VbCoreProjectKind);
            IsAccepted_SupportedNotExcludedProject_IsKnownTestProject_Impl(ProjectSystemHelper.VbProjectKind);
        }

        private void IsAccepted_SupportedNotExcludedProject_IsKnownTestProject_Impl(string projectTypeGuid)
        {
            // Arrange
            var testSubject = this.CreateTestSubject();

            var project = new LegacyProjectMock("knownproject.xxx");
            project.ProjectKind = projectTypeGuid;
            project.SetBuildProperty(Constants.SonarQubeExcludeBuildPropertyKey, "false"); // Should evaluate test projects even if false

            // Case 1: Test not test project kind, test project exclude not set
            project.SetBuildProperty(Constants.SonarQubeTestProjectBuildPropertyKey, ""); // Should not continue with evaluation if has boolean value

            // Act
            bool result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeTrue("Project not a known test project");

            // Case 2: Test project kind, test project exclude not set
            project.SetTestProject();

            // Act
            result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeFalse("Project of known test project type should NOT be accepted");

            // Case 3: SonarQubeTestProjectBuildPropertyKey == false, should take precedence over project kind condition
            project.SetBuildProperty(Constants.SonarQubeTestProjectBuildPropertyKey, "false");
            project.ClearProjectKind();

            // Act
            result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeTrue("Should be accepted since test project is explicitly not-excluded");

            // Case 4: SonarQubeTestProjectBuildPropertyKey == true, should take precedence over project kind condition
            project.SetBuildProperty(Constants.SonarQubeTestProjectBuildPropertyKey, "true");
            project.ClearProjectKind();

            // Act
            result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeFalse("Should not be accepted since test project is excluded");
        }

        [TestMethod]
        public void IsAccepted_SupportedNotExcludedProject_NotExcludedTestProject_EvaluateRegex()
        {
            IsAccepted_SupportedNotExcludedProject_NotExcludedTestProject_EvaluateRegex_Impl(ProjectSystemHelper.CSharpCoreProjectKind);
            IsAccepted_SupportedNotExcludedProject_NotExcludedTestProject_EvaluateRegex_Impl(ProjectSystemHelper.CSharpProjectKind);

            IsAccepted_SupportedNotExcludedProject_NotExcludedTestProject_EvaluateRegex_Impl(ProjectSystemHelper.VbCoreProjectKind);
            IsAccepted_SupportedNotExcludedProject_NotExcludedTestProject_EvaluateRegex_Impl(ProjectSystemHelper.VbProjectKind);
        }

        private void IsAccepted_SupportedNotExcludedProject_NotExcludedTestProject_EvaluateRegex_Impl(string projectTypeGuid)
        {
            // Arrange
            var testSubject = this.CreateTestSubject();
            var project = new LegacyProjectMock("foobarfoobar.foo");
            project.ProjectKind = projectTypeGuid;

            // Case 1: Regex match
            testSubject.SetTestRegex(new Regex(".*barfoo.*", RegexOptions.None, TimeSpan.FromSeconds(1)));

            // Act
            var result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeFalse("Project with name that matches test regex should NOT be accepted");

            // Case 2: Regex doesn't match
            testSubject.SetTestRegex(new Regex(".*notfound.*", RegexOptions.None, TimeSpan.FromSeconds(1)));

            // Act
            result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeTrue("Project with name that does not match test regex should be accepted");

            // Case 3: SonarQubeTestProjectBuildPropertyKey == false, should take precedence over regex condition
            project.SetBuildProperty(Constants.SonarQubeTestProjectBuildPropertyKey, "false");

            // Act
            result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeTrue("Should be accepted since test project is explicitly not-excluded");

            // Case 4: SonarQubeTestProjectBuildPropertyKey == true, should take precedence over regex condition
            project.SetBuildProperty(Constants.SonarQubeTestProjectBuildPropertyKey, "true");
            project.ClearProjectKind();

            // Act
            result = testSubject.IsAccepted(project);

            // Assert
            result.Should().BeFalse("Should not be accepted since test project is excluded");
        }

        [TestMethod]
        public void SetTestRegex_ArgCheck()
        {
            // Arrange
            ProjectSystemFilter testSubject = this.CreateTestSubject();

            // Act + Assert
            Exceptions.Expect<ArgumentNullException>(() => testSubject.SetTestRegex(null));
        }

        [TestMethod]
        public void IsAccepted_UnrecognisedProjectType_ReturnsFalse()
        {
            // Arrange
            var testSubject = this.CreateTestSubject();
            var project = new ProjectMock("unsupported.vcxproj");
            project.SetBuildProperty(Constants.SonarQubeTestProjectBuildPropertyKey, "False"); // Should not matter

            // Act and Assert
            testSubject.IsAccepted(project).Should().BeFalse();
        }

        [TestMethod]
        public void IsAccepted_SharedProject_ReturnsFalse()
        {
            // Arrange
            var testSubject = this.CreateTestSubject();

            var project = new ProjectMock("shared1.shproj");
            project.SetCSProjectKind();

            project = new ProjectMock("shared1.SHPROJ");
            project.SetCSProjectKind();

            // Act and Assert
            testSubject.IsAccepted(project).Should().BeFalse();
        }

        #endregion Tests

        #region Helpers

        private ProjectSystemFilter CreateTestSubject()
        {
            var host = new ConfigurableHost(this.serviceProvider, Dispatcher.CurrentDispatcher);
            return new ProjectSystemFilter(host);
        }

        #endregion Helpers
    }
}
