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

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SonarLint.VisualStudio.ConnectedMode.Binding;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Core.Binding;
using SonarLint.VisualStudio.Integration;
using SonarQube.Client;
using SonarQube.Client.Models;

namespace SonarLint.VisualStudio.ConnectedMode.QualityProfiles
{
    internal interface IQualityProfileDownloader
    {
        /// <summary>
        /// Ensures that the Quality Profiles for all supported languages are to date
        /// </summary>
        Task<bool> UpdateAsync(BoundSonarQubeProject boundProject, IProgress<FixedStepsProgress> progress, CancellationToken cancellationToken);
    }

    [Export(typeof(IQualityProfileDownloader))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class QualityProfileDownloader : IQualityProfileDownloader
    {
        private readonly IBindingConfigProvider bindingConfigProvider;
        private readonly ISonarQubeService sonarQubeService;
        private readonly IConfigurationPersister configurationPersister;
        private readonly ISolutionBindingOperation solutionBindingOperation;

        private readonly ILogger logger;

        private readonly IEnumerable<Language> languagesToBind;

        [ImportingConstructor]
        public QualityProfileDownloader(
            ISonarQubeService sonarQubeService,
            IBindingConfigProvider bindingConfigProvider,
            IConfigurationPersister configurationPersister,
            ILogger logger) :
            this(
                sonarQubeService,
                bindingConfigProvider,
                configurationPersister,
                logger,
                new SolutionBindingOperation(),
                languagesToBind: Language.KnownLanguages
                )
        { }

        internal /* for testing */ QualityProfileDownloader(
            ISonarQubeService sonarQubeService,
            IBindingConfigProvider bindingConfigProvider,
            IConfigurationPersister configurationPersister,
            ILogger logger,
            ISolutionBindingOperation solutionBindingOperation,
            IEnumerable<Language> languagesToBind)
        {
            this.bindingConfigProvider = bindingConfigProvider;
            this.sonarQubeService = sonarQubeService;
            this.configurationPersister = configurationPersister;
            this.solutionBindingOperation = solutionBindingOperation;
            this.logger = logger;
            this.languagesToBind = languagesToBind;
        }

        public async Task<bool> UpdateAsync(BoundSonarQubeProject boundProject, IProgress<FixedStepsProgress> progress, CancellationToken cancellationToken)
        {
            // TODO - CancellableJobRunner
            // TODO - threading
            // TODO - skip downloading up to date QPs

            EnsureProfilesExistForAllSupportedLanguages(boundProject);

            var bindingConfigs = new List<IBindingConfig>();

            var languageCount = languagesToBind.Count();
            int currentLanguage = 0;

            foreach (var language in languagesToBind)
            {
                currentLanguage++;

                var progressMessage = string.Format(BindingStrings.DownloadingQualityProfileProgressMessage, language.Name);
                progress?.Report(new FixedStepsProgress(progressMessage, currentLanguage, languageCount));

                var qualityProfileInfo = await TryDownloadQualityProfileAsync(boundProject, language, cancellationToken);

                if (qualityProfileInfo == null)
                {
                    continue; // skip to the next language
                }
                UpdateProfile(boundProject, language, qualityProfileInfo);

                var bindingConfiguration = configurationPersister.Persist(boundProject);

                // Create the binding configuration for the language
                var bindingConfig = await bindingConfigProvider.GetConfigurationAsync(qualityProfileInfo, language, bindingConfiguration, cancellationToken);
                if (bindingConfig == null)
                {
                    logger.WriteLine(string.Format(BindingStrings.SubTextPaddingFormat,
                        string.Format(BindingStrings.FailedToCreateBindingConfigForLanguage, language.Name)));
                    return false;
                }

                bindingConfigs.Add(bindingConfig);

                logger.WriteLine(string.Format(BindingStrings.SubTextPaddingFormat,
                    string.Format(BindingStrings.QualityProfileDownloadSuccessfulMessageFormat, qualityProfileInfo.Name, qualityProfileInfo.Key, language.Name)));
            }

            solutionBindingOperation.SaveRuleConfiguration(bindingConfigs, cancellationToken);

            return true;
        }

        /// <summary>
        /// Ensures that the bound project has a profile entry for every supported language
        /// </summary>
        /// <remarks>If we add support for new language in the future, this method will make sure it's
        /// Quality Profile is fetched next time an update is triggered</remarks>
        private void EnsureProfilesExistForAllSupportedLanguages(BoundSonarQubeProject boundProject)
        {
            if (boundProject.Profiles == null)
            {
                boundProject.Profiles = new Dictionary<Language, ApplicableQualityProfile>();
            }

            foreach (var language in languagesToBind)
            {
                if (!boundProject.Profiles.ContainsKey(language))
                {
                    boundProject.Profiles[language] = new ApplicableQualityProfile
                    {
                        ProfileKey = null,
                        ProfileTimestamp = DateTime.MinValue,
                    };
                }
            }
        }

        private static void UpdateProfile(BoundSonarQubeProject boundSonarQubeProject, Language language, SonarQubeQualityProfile serverProfile)
        {
            boundSonarQubeProject.Profiles[language] = new ApplicableQualityProfile
            {
                ProfileKey = serverProfile.Key, ProfileTimestamp = serverProfile.TimeStamp
            };
        }

        /// <summary>
        /// Attempts to fetch the QP for the specified language.
        /// </summary>
        /// <returns>The QP, or null if the language plugin is not available on the server</returns>
        private async Task<SonarQubeQualityProfile> TryDownloadQualityProfileAsync(BoundSonarQubeProject boundProject, Language language, CancellationToken cancellationToken)
        {
            // There are valid scenarios in which a language plugin will not be available on the server:
            // 1) the CFamily plugin does not ship in Community edition (nor do any other commerical plugins)
            // 2) a recently added language will not be available in older-but-still-supported SQ versions
            //      e.g. the "secrets" language
            // The unavailability of a language should not prevent binding from succeeding.

            // Note: the historical check that plugins meet a minimum version was removed. 
            // See https://github.com/SonarSource/sonarlint-visualstudio/issues/4272

            var qualityProfileInfo = await WebServiceHelper.SafeServiceCallAsync(() =>

            sonarQubeService.GetQualityProfileAsync(
                boundProject.ProjectKey, boundProject.Organization?.Key, language.ServerLanguage, cancellationToken),
                logger);

            if (qualityProfileInfo == null)
            {
                logger.WriteLine(string.Format(BindingStrings.SubTextPaddingFormat,
                   string.Format(BindingStrings.CannotDownloadQualityProfileForLanguage, language.Name)));
                return null;
            }

            return qualityProfileInfo;
        }
    }
}
