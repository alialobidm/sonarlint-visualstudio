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

using NSubstitute.ReturnsExtensions;
using SonarLint.VisualStudio.CFamily;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Core.CFamily;
using SonarLint.VisualStudio.Integration.Vsix.CFamily.VcxProject;

namespace SonarLint.VisualStudio.Integration.UnitTests.CFamily.VcxProject;

[TestClass]
public class VCXCompilationDatabaseProviderTests
{
    private const string CDFile = "cdfilevalue";
    private const string CDDirectory = "cddirectoryvalue";
    private const string CDCommand = "cdcommandvalue";
    private const string EnvInclude = "envincludevalue";
    private const string SourceFilePath = "some path";
    private IVCXCompilationDatabaseStorage storage;
    private IFileConfigProvider fileConfigProvider;
    private IEnvironmentVariableProvider envVarProvider;
    private ILogger logger;

    [TestInitialize]
    public void TestInitialize()
    {
        storage = Substitute.For<IVCXCompilationDatabaseStorage>();
        fileConfigProvider = Substitute.For<IFileConfigProvider>();
        envVarProvider = Substitute.For<IEnvironmentVariableProvider>();
        envVarProvider.GetAll().Returns([]);
        logger = Substitute.For<ILogger>();
    }

    [TestMethod]
    public void MefCtor_CheckIsExported()
    {
        envVarProvider.GetAll().Returns([]);
        MefTestHelpers.CheckTypeCanBeImported<VCXCompilationDatabaseProvider, IVCXCompilationDatabaseProvider>(
            MefTestHelpers.CreateExport<IVCXCompilationDatabaseStorage>(),
            MefTestHelpers.CreateExport<IEnvironmentVariableProvider>(envVarProvider),
            MefTestHelpers.CreateExport<IFileConfigProvider>(),
            MefTestHelpers.CreateExport<ILogger>());
    }

    [TestMethod]
    public void MefCtor_CheckIsSingleton() => MefTestHelpers.CheckIsSingletonMefComponent<VCXCompilationDatabaseProvider>();

    [TestMethod]
    public void CreateOrNull_NoFileConfig_ReturnsNull()
    {
        fileConfigProvider.Get(SourceFilePath).ReturnsNull();
        var testSubject = new VCXCompilationDatabaseProvider(
            storage,
            envVarProvider,
            fileConfigProvider,
            logger);

        testSubject.CreateOrNull(SourceFilePath).Should().BeNull();

        storage.DidNotReceiveWithAnyArgs().CreateDatabase(default, default, default, default);
    }

    [TestMethod]
    public void CreateOrNull_FileConfig_CantStore_ReturnsNull()
    {
        var fileConfig = GetFileConfig();
        fileConfigProvider.Get(SourceFilePath).Returns(fileConfig);
        storage.CreateDatabase(default, default, default, default).ReturnsNullForAnyArgs();
        var testSubject = new VCXCompilationDatabaseProvider(
            storage,
            envVarProvider,
            fileConfigProvider,
            logger);

        testSubject.CreateOrNull(SourceFilePath).Should().BeNull();

        storage.Received().CreateDatabase(CDFile, CDDirectory, CDCommand, Arg.Any<IEnumerable<string>>());
    }

    [TestMethod]
    public void CreateOrNull_FileConfig_StoresAndReturnsHandle()
    {
        var fileConfig = GetFileConfig();
        fileConfigProvider.Get(SourceFilePath).Returns(fileConfig);
        var compilationDatabaseHandle = Substitute.For<ICompilationDatabaseHandle>();
        storage.CreateDatabase(CDFile, CDDirectory, CDCommand, Arg.Any<IEnumerable<string>>()).Returns(compilationDatabaseHandle);
        var testSubject = new VCXCompilationDatabaseProvider(
            storage,
            envVarProvider,
            fileConfigProvider,
            logger);

        testSubject.CreateOrNull(SourceFilePath).Should().Be(compilationDatabaseHandle);
    }

    [TestMethod]
    public void CreateOrNull_NoEnvIncludeInFileConfig_UsesStatic()
    {
        var fileConfig = GetFileConfig(null);
        fileConfigProvider.Get(SourceFilePath).Returns(fileConfig);
        envVarProvider.GetAll().Returns([("Var1", "Value1"), ("INCLUDE", "static"), ("Var2", "Value2")]);
        var testSubject = new VCXCompilationDatabaseProvider(
            storage,
            envVarProvider,
            fileConfigProvider,
            logger);

        testSubject.CreateOrNull(SourceFilePath);

        storage.Received(1).CreateDatabase(CDFile, CDDirectory, CDCommand, Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new [] { "Var1=Value1", "INCLUDE=static", "Var2=Value2" })));
    }

    [TestMethod]
    public void CreateOrNull_FileConfigHasEnvInclude_UsesDynamic()
    {
        var fileConfig = GetFileConfig(EnvInclude);
        fileConfigProvider.Get(SourceFilePath).Returns(fileConfig);
        envVarProvider.GetAll().Returns([("Var1", "Value1"), ("INCLUDE", "static"), ("Var2", "Value2")]);
        var testSubject = new VCXCompilationDatabaseProvider(
            storage,
            envVarProvider,
            fileConfigProvider,
            logger);

        testSubject.CreateOrNull(SourceFilePath);

        storage.Received(1).CreateDatabase(CDFile, CDDirectory, CDCommand, Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new [] { "Var1=Value1", "Var2=Value2", $"INCLUDE={EnvInclude}"})));
        logger.Received(1).LogVerbose($"[VCXCompilationDatabaseProvider] Overwriting the value of environment variable \"INCLUDE\". Old value: \"static\", new value: \"{EnvInclude}\"");
    }

    [TestMethod]
    public void CreateOrNull_NoStaticInclude_UsesDynamic()
    {
        var fileConfig = GetFileConfig(EnvInclude);
        fileConfigProvider.Get(SourceFilePath).Returns(fileConfig);
        envVarProvider.GetAll().Returns([("Var1", "Value1"), ("Var2", "Value2")]);
        var testSubject = new VCXCompilationDatabaseProvider(
            storage,
            envVarProvider,
            fileConfigProvider,
            logger);

        testSubject.CreateOrNull(SourceFilePath);

        storage.Received(1).CreateDatabase(CDFile, CDDirectory, CDCommand, Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new [] { "Var1=Value1", "Var2=Value2", $"INCLUDE={EnvInclude}"})));
        logger.Received(1).LogVerbose($"[VCXCompilationDatabaseProvider] Setting environment variable \"INCLUDE\". Value: \"{EnvInclude}\"");
    }

    [TestMethod]
    public void CreateOrNull_StaticEnvVarsAreCached()
    {
        var fileConfig = GetFileConfig();
        fileConfigProvider.Get(SourceFilePath).Returns(fileConfig);
        envVarProvider.GetAll().Returns([("Var1", "Value1"), ("Var2", "Value2")]);
        var testSubject = new VCXCompilationDatabaseProvider(
            storage,
            envVarProvider,
            fileConfigProvider,
            logger);

        testSubject.CreateOrNull(SourceFilePath);
        testSubject.CreateOrNull(SourceFilePath);
        testSubject.CreateOrNull(SourceFilePath);

        envVarProvider.Received(1).GetAll();
    }

    [TestMethod]
    public void CreateOrNull_EnvVarsContainHeaderPropertyForHeaderFiles()
    {
        var fileConfig = GetFileConfig(EnvInclude, true);
        fileConfigProvider.Get(SourceFilePath).Returns(fileConfig);
        envVarProvider.GetAll().Returns([("Var1", "Value1"), ("Var2", "Value2")]);
        var testSubject = new VCXCompilationDatabaseProvider(
            storage,
            envVarProvider,
            fileConfigProvider,
            logger);

        testSubject.CreateOrNull(SourceFilePath);

        var expectedEnv = new[] { "Var1=Value1", "Var2=Value2", $"INCLUDE={EnvInclude}", "SONAR_CFAMILY_CAPTURE_PROPERTY_isHeaderFile=true" };
        storage.Received(1).CreateDatabase(CDFile, CDDirectory, CDCommand, Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(expectedEnv)));
        logger.Received(1).LogVerbose($"[VCXCompilationDatabaseProvider] Setting environment variable \"INCLUDE\". Value: \"{EnvInclude}\"");
        logger.Received(1).LogVerbose($"[VCXCompilationDatabaseProvider] Setting environment variable \"SONAR_CFAMILY_CAPTURE_PROPERTY_isHeaderFile\". Value: \"true\"");
    }

    private IFileConfig GetFileConfig(string envInclude = EnvInclude, bool isHeader = false)
    {
        var fileConfig = Substitute.For<IFileConfig>();
        fileConfig.CDFile.Returns(CDFile);
        fileConfig.CDDirectory.Returns(CDDirectory);
        fileConfig.CDCommand.Returns(CDCommand);
        fileConfig.EnvInclude.Returns(envInclude);
        fileConfig.IsHeaderFile.Returns(isHeader);
        return fileConfig;
    }
}
