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
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Threading;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Core.Binding;
using SonarLint.VisualStudio.Infrastructure.VS;
using SonarLint.VisualStudio.Integration;
using Task = System.Threading.Tasks.Task;

namespace SonarLint.VisualStudio.ConnectedMode.Migration.Wizard
{
    public sealed partial class MigrationWizardWindow : DialogWindow, IProgress<MigrationProgress>
    {
        public event EventHandler StartMigration;

        private readonly BoundSonarQubeProject oldBinding;
        private readonly IConnectedModeMigration connectedModeMigration;
        private readonly ILogger logger;
        private readonly IThreadHandling threadHandling;

        private bool dialogResult;
        private bool migrationInProgress;

        private CancellationTokenSource cancellationTokenSource;

        internal MigrationWizardWindow(BoundSonarQubeProject oldBinding, IConnectedModeMigration connectedModeMigration, ILogger logger)
        {
            this.oldBinding = oldBinding;
            this.connectedModeMigration = connectedModeMigration;
            this.logger = logger;
            this.threadHandling = ThreadHandling.Instance;

            cancellationTokenSource = new CancellationTokenSource();

            InitializeComponent();
            this.Closing += OnClosing;
            dialogResult = false;
        }

        private void NavigateToMigrationProgressPage()
        {
            // Changing the first page visibility to Hidden rather than Collapsed so
            // that the size of the dialog does not change.
            // If we used "Collapsed", the size of the dialog would be recalculated as
            // if the first page did not exist.
            StartWindow.Visibility = Visibility.Hidden;
            MigrationProgressWindow.Visibility = Visibility.Visible;

            // Set button states
            btnPage1_Cancel.IsEnabled = false;
            btnPage1_Start.IsEnabled = false;

            finishButton.IsEnabled = false; // disabled until migration finished
        }

        private void OnStartMigration(object sender, RoutedEventArgs e)
        {
            // User has clicked on the "Start" button on the first page
            // -> show page 2
            // -> start the process

            if (migrationInProgress) { return; }
            migrationInProgress = true;

            NavigateToMigrationProgressPage();

            // Disables all closing / cancel buttons, including the 
            // the X in the top-right of the window
            this.IsCloseButtonEnabled = false;

            MigrateAsync().Forget();
        }

        private async Task MigrateAsync()
        {
            try
            {
                await connectedModeMigration.MigrateAsync(oldBinding, this, cancellationTokenSource.Token);
                MigrationFinished();
            }
            catch (OperationCanceledException ex)
            {
                logger.LogVerbose(MigrationStrings.CancelTokenFailure_VerboseLog, ex);
                logger.WriteLine(MigrationStrings.CancelTokenFailure_NormalLog);
            }
            catch (Exception ex) when (!ErrorHandler.IsCriticalException(ex))
            {
                logger.LogVerbose(MigrationStrings.ErrorDuringMigation_VerboseLog, ex);
                logger.WriteLine(MigrationStrings.ErrorDuringMigation_NormalLog, ex.Message);
            }
        }

        private void MigrationFinished()
        {
            migrationInProgress = false;
            this.finishButton.IsEnabled = true;
            this.IsCloseButtonEnabled = true;
            dialogResult = true;
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            cancellationTokenSource.Cancel();
            migrationInProgress = false;
            this.DialogResult = dialogResult;
        }

        void IProgress<MigrationProgress>.Report(MigrationProgress value)
        {
            threadHandling.RunOnUIThreadSync2(() =>
            {
                ListBoxItem item = new ListBoxItem();
                item.Foreground = value.IsWarning ? Brushes.Red : Brushes.Black;
                item.Content = value.Message;
                progressList.Items.Add(item);
            });
        }
    }
}
