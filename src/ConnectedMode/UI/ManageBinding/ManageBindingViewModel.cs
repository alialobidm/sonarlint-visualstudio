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

using System.Collections.ObjectModel;
using System.Windows;
using SonarLint.VisualStudio.ConnectedMode.Shared;
using SonarLint.VisualStudio.ConnectedMode.UI.ProjectSelection;
using SonarLint.VisualStudio.ConnectedMode.UI.Resources;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Core.Binding;
using SonarLint.VisualStudio.Core.WPF;

namespace SonarLint.VisualStudio.ConnectedMode.UI.ManageBinding;

internal sealed class ManageBindingViewModel(
    IConnectedModeServices connectedModeServices,
    IConnectedModeBindingServices connectedModeBindingServices,
    IConnectedModeUIServices connectedModeUiServices,
    IConnectedModeUIManager connectedModeUiManager,
    IProgressReporterViewModel progressReporterViewModel)
    : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private ServerProject boundProject;
    private ConnectionInfo selectedConnectionInfo;
    private ServerProject selectedProject;
    private SharedBindingConfigModel sharedBindingConfigModel;
    private SolutionInfoModel solutionInfo;
    private bool bindingSucceeded;

    public SolutionInfoModel SolutionInfo
    {
        get => solutionInfo;
        set
        {
            solutionInfo = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsSolutionOpen));
            RaisePropertyChanged(nameof(IsOpenSolutionBound));
            RaisePropertyChanged(nameof(IsOpenSolutionStandalone));
            RaisePropertyChanged(nameof(IsSelectProjectButtonEnabled));
            RaisePropertyChanged(nameof(IsConnectionSelectionEnabled));
            RaisePropertyChanged(nameof(IsExportButtonEnabled));
            RaisePropertyChanged(nameof(IsUseSharedBindingButtonVisible));
        }
    }

    public IProgressReporterViewModel ProgressReporter { get; } = progressReporterViewModel;

    public ServerProject BoundProject
    {
        get => boundProject;
        set
        {
            if (IsSolutionOpen)
            {
                boundProject = value;
            }
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsOpenSolutionBound));
            RaisePropertyChanged(nameof(IsOpenSolutionStandalone));
            RaisePropertyChanged(nameof(IsSelectProjectButtonEnabled));
            RaisePropertyChanged(nameof(IsConnectionSelectionEnabled));
            RaisePropertyChanged(nameof(IsExportButtonEnabled));
            RaisePropertyChanged(nameof(IsUseSharedBindingButtonVisible));
        }
    }

    public ConnectionInfo SelectedConnectionInfo
    {
        get => selectedConnectionInfo;
        set
        {
            if (value == selectedConnectionInfo)
            {
                return;
            }
            selectedConnectionInfo = value;
            SelectedProject = null;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsConnectionSelected));
            RaisePropertyChanged(nameof(IsSelectProjectButtonEnabled));
        }
    }

    public ObservableCollection<ConnectionInfo> Connections { get; } = [];

    public ServerProject SelectedProject
    {
        get => selectedProject;
        set
        {
            selectedProject = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsProjectSelected));
            RaisePropertyChanged(nameof(IsBindButtonEnabled));
        }
    }

    internal SharedBindingConfigModel SharedBindingConfigModel
    {
        get => sharedBindingConfigModel;
        set
        {
            sharedBindingConfigModel = value;
            RaisePropertyChanged(nameof(IsUseSharedBindingButtonVisible));
        }
    }

    public bool BindingSucceeded
    {
        get => bindingSucceeded;
        set
        {
            bindingSucceeded = value;
            RaisePropertyChanged();
        }
    }

    public bool IsSolutionOpen => SolutionInfo is { Name: not null };
    public bool IsOpenSolutionBound => IsSolutionOpen && BoundProject is not null;
    public bool IsOpenSolutionStandalone => IsSolutionOpen && BoundProject is null;
    public bool IsProjectSelected => SelectedProject != null;
    public bool IsConnectionSelected => SelectedConnectionInfo != null;
    public bool IsConnectionSelectionEnabled => !ProgressReporter.IsOperationInProgress && IsOpenSolutionStandalone && Connections.Any();
    public bool IsBindButtonEnabled => IsProjectSelected && !ProgressReporter.IsOperationInProgress;
    public bool IsSelectProjectButtonEnabled => IsConnectionSelected && !ProgressReporter.IsOperationInProgress && IsOpenSolutionStandalone;
    public bool IsUnbindButtonEnabled => !ProgressReporter.IsOperationInProgress;
    public bool IsManageConnectionsButtonEnabled => !ProgressReporter.IsOperationInProgress;
    public bool IsUseSharedBindingButtonEnabled => !ProgressReporter.IsOperationInProgress;
    public bool IsUseSharedBindingButtonVisible => SharedBindingConfigModel != null && IsOpenSolutionStandalone;
    public bool IsExportButtonEnabled => !ProgressReporter.IsOperationInProgress && IsOpenSolutionBound;
    public string ConnectionSelectionCaptionText => Connections.Any() ? UiResources.SelectConnectionToBindDescription : UiResources.NoConnectionExistsLabel;

    public void Dispose() => cancellationTokenSource?.Dispose();

    public async Task InitializeDataAsync()
    {
        var loadData = new TaskToPerformParams<AdapterResponse>(LoadDataAsync, UiResources.LoadingConnectionsText,
            UiResources.LoadingConnectionsFailedText) { AfterProgressUpdated = OnProgressUpdated };
        var loadDataResult = await ProgressReporter.ExecuteTaskWithProgressAsync(loadData);

        var displayBindStatus = new TaskToPerformParams<AdapterResponseWithData<BindingResult>>(DisplayBindStatusAsync, UiResources.FetchingBindingStatusText,
            UiResources.FetchingBindingStatusFailedText) { AfterProgressUpdated = OnProgressUpdated };
        var displayBindStatusResult = await ProgressReporter.ExecuteTaskWithProgressAsync(displayBindStatus, clearPreviousState: false);

        BindingSucceeded = loadDataResult.Success && displayBindStatusResult.Success;
        await UpdateSharedBindingStateAsync();
    }

    private async Task UpdateSharedBindingStateAsync()
    {
        var detectSharedBinding = new TaskToPerformParams<AdapterResponse>(CheckForSharedBindingAsync, UiResources.CheckingForSharedBindingText,
            UiResources.CheckingForSharedBindingFailedText) { AfterProgressUpdated = OnProgressUpdated };
        await ProgressReporter.ExecuteTaskWithProgressAsync(detectSharedBinding, clearPreviousState: false);
    }

    public async Task PerformManualBindingWithProgressAsync()
    {
        var bind = new TaskToPerformParams<AdapterResponseWithData<BindingResult>>(PerformManualBindingAsync, UiResources.BindingInProgressText, UiResources.BindingFailedText)
        {
            AfterProgressUpdated = OnProgressUpdated
        };
        await ProgressReporter.ExecuteTaskWithProgressAsync(bind);
    }

    public async Task<BindingResult> PerformAutomaticBindingWithProgressAsync(AutomaticBindingRequest automaticBinding)
    {
        var bind = new TaskToPerformParams<AdapterResponseWithData<BindingResult>>(() => PerformAutomaticBindingInternalAsync(automaticBinding), UiResources.BindingInProgressText,
            UiResources.BindingFailedText) { AfterProgressUpdated = OnProgressUpdated };
        var result = await ProgressReporter.ExecuteTaskWithProgressAsync(bind);
        return result.ResponseData;
    }

    public async Task UnbindWithProgressAsync()
    {
        var unbind = new TaskToPerformParams<AdapterResponse>(UnbindAsync, UiResources.UnbindingInProgressText, UiResources.UnbindingFailedText) { AfterProgressUpdated = OnProgressUpdated };
        await ProgressReporter.ExecuteTaskWithProgressAsync(unbind);
    }

    public async Task ExportBindingConfigurationWithProgressAsync()
    {
        var export = new TaskToPerformParams<AdapterResponseWithData<string>>(ExportBindingConfigurationAsync, UiResources.ExportingBindingConfigurationProgressText,
            UiResources.ExportBindingConfigurationWarningText) { AfterProgressUpdated = OnProgressUpdated };

        var result = await ProgressReporter.ExecuteTaskWithProgressAsync(export);
        if (result.Success)
        {
            connectedModeUiServices.MessageBox.Show(string.Format(UiResources.ExportBindingConfigurationMessageBoxTextSuccess, result.ResponseData),
                UiResources.ExportBindingConfigurationMessageBoxCaptionSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
            await UpdateSharedBindingStateAsync();
        }
    }

    internal Task<AdapterResponseWithData<string>> ExportBindingConfigurationAsync()
    {
        var connection = SelectedConnectionInfo.GetServerConnectionFromConnectionInfo();
        var sharedBindingConfig = new SharedBindingConfigModel
        {
            ProjectKey = selectedProject.Key,
            Uri = connection.ServerUri,
            Organization = (connection as ServerConnection.SonarCloud)?.OrganizationKey,
            Region = (connection as ServerConnection.SonarCloud)?.Region.Name,
        };

        var savePath = connectedModeBindingServices.SharedBindingConfigProvider.SaveSharedBinding(sharedBindingConfig);

        return Task.FromResult(new AdapterResponseWithData<string>(savePath != null, savePath));
    }

    internal Task<AdapterResponse> CheckForSharedBindingAsync()
    {
        SharedBindingConfigModel = connectedModeBindingServices.SharedBindingConfigProvider.GetSharedBinding();
        return Task.FromResult(new AdapterResponse(true));
    }

    internal void UpdateProgress(string status)
    {
        ProgressReporter.ProgressStatus = status;
        OnProgressUpdated();
    }

    internal void OnProgressUpdated()
    {
        RaisePropertyChanged(nameof(IsBindButtonEnabled));
        RaisePropertyChanged(nameof(IsUnbindButtonEnabled));
        RaisePropertyChanged(nameof(IsUseSharedBindingButtonEnabled));
        RaisePropertyChanged(nameof(IsManageConnectionsButtonEnabled));
        RaisePropertyChanged(nameof(IsSelectProjectButtonEnabled));
        RaisePropertyChanged(nameof(IsConnectionSelectionEnabled));
        RaisePropertyChanged(nameof(IsExportButtonEnabled));
    }

    internal async Task<AdapterResponse> LoadDataAsync()
    {
        var succeeded = false;
        try
        {
            await connectedModeServices.ThreadHandling.RunOnUIThreadAsync(() => succeeded = LoadConnections());
        }
        catch (Exception ex)
        {
            connectedModeServices.Logger.WriteLine(ex.Message);
            succeeded = false;
        }

        return new AdapterResponse(succeeded);
    }

    internal bool LoadConnections()
    {
        Connections.Clear();
        var succeeded = connectedModeServices.ServerConnectionsRepositoryAdapter.TryGetAllConnectionsInfo(out var slCoreConnections);
        slCoreConnections?.ForEach(Connections.Add);

        RaisePropertyChanged(nameof(IsConnectionSelectionEnabled));
        RaisePropertyChanged(nameof(ConnectionSelectionCaptionText));
        return succeeded;
    }

    internal async Task<AdapterResponseWithData<BindingResult>> DisplayBindStatusAsync()
    {
        SolutionInfo = await GetSolutionInfoModelAsync();

        var bindingConfiguration = connectedModeServices.ConfigurationProvider.GetConfiguration();
        if (bindingConfiguration == null || bindingConfiguration.Mode == SonarLintMode.Standalone)
        {
            var successResponse = new AdapterResponseWithData<BindingResult>(true, BindingResult.Success);
            UpdateBoundProjectProperties(null, null);
            return successResponse;
        }

        var boundServerProject = bindingConfiguration.Project;
        var serverConnection = boundServerProject?.ServerConnection;
        if (serverConnection == null)
        {
            return new AdapterResponseWithData<BindingResult>(false, BindingResult.ConnectionNotFound);
        }

        var response = await connectedModeServices.SlCoreConnectionAdapter.GetServerProjectByKeyAsync(serverConnection, boundServerProject.ServerProjectKey);
        // even if the response is not successful, we still want to update the UI with the bound project, because the binding does exist
        var selectedServerProject = response.ResponseData ?? new ServerProject(boundServerProject.ServerProjectKey, boundServerProject.ServerProjectKey);
        UpdateBoundProjectProperties(serverConnection, selectedServerProject);
        var projectRetrieved = response.ResponseData != null;

        return new AdapterResponseWithData<BindingResult>(projectRetrieved, projectRetrieved ? BindingResult.Success : BindingResult.Failed);
    }

    internal async Task<AdapterResponseWithData<BindingResult>> PerformManualBindingAsync()
    {
        if (!connectedModeServices.ServerConnectionsRepositoryAdapter.TryGet(SelectedConnectionInfo, out var serverConnection))
        {
            return new AdapterResponseWithData<BindingResult>(false, BindingResult.ConnectionNotFound);
        }
        var adapterResponse = await BindAsync(serverConnection, SelectedProject?.Key);
        if (adapterResponse.Success)
        {
            connectedModeServices.TelemetryManager.AddedManualBindings();
        }
        return adapterResponse;
    }

    internal async Task<AdapterResponse> UnbindAsync()
    {
        bool succeeded;
        try
        {
            succeeded = connectedModeBindingServices.BindingController.Unbind(SolutionInfo.Name);
            await DisplayBindStatusAsync();
        }
        catch (Exception ex)
        {
            connectedModeServices.Logger.WriteLine(ex.Message);
            succeeded = false;
        }

        return new AdapterResponse(succeeded);
    }

    private async Task<AdapterResponseWithData<BindingResult>> BindAsync(ServerConnection serverConnection, string serverProjectKey)
    {
        try
        {
            var localBindingKey = await connectedModeBindingServices.SolutionInfoProvider.GetSolutionNameAsync();
            var boundServerProject = new BoundServerProject(localBindingKey, serverProjectKey, serverConnection);
            await connectedModeBindingServices.BindingController.BindAsync(boundServerProject, cancellationTokenSource.Token);
            return await DisplayBindStatusAsync();
        }
        catch (Exception ex)
        {
            connectedModeServices.Logger.WriteLine(ConnectedMode.Resources.Binding_Fails, ex.Message);
            return new AdapterResponseWithData<BindingResult>(false, BindingResult.Failed);
        }
    }

    private void UpdateBoundProjectProperties(ServerConnection serverConnection, ServerProject selectedServerProject)
    {
        SelectedConnectionInfo = serverConnection == null ? null : ConnectionInfo.From(serverConnection);
        SelectedProject = selectedServerProject;
        BoundProject = SelectedProject;
    }

    private async Task<SolutionInfoModel> GetSolutionInfoModelAsync()
    {
        var solutionName = await connectedModeBindingServices.SolutionInfoProvider.GetSolutionNameAsync();
        var isFolderWorkspace = await connectedModeBindingServices.SolutionInfoProvider.IsFolderWorkspaceAsync();
        return new SolutionInfoModel(solutionName, isFolderWorkspace ? SolutionType.Folder : SolutionType.Solution);
    }

    internal async Task<AdapterResponseWithData<BindingResult>> PerformAutomaticBindingInternalAsync(AutomaticBindingRequest automaticBinding)
    {
        var serverProjectKey = GetServerProjectKey(automaticBinding);

        if (ValidateAutomaticBindingArguments(automaticBinding, GetServerConnection(automaticBinding), serverProjectKey) is var validationResult and not BindingResult.Success &&
            !(await CreateConnectionIfMissingAsync(validationResult, automaticBinding)))
        {
            return new AdapterResponseWithData<BindingResult>(false, validationResult);
        }

        var serverConnection = GetServerConnection(automaticBinding); // reload connection in case it was created
        var response = await BindAsync(serverConnection, serverProjectKey);
        Telemetry(response, automaticBinding);
        return response;
    }

    private async Task<bool> CreateConnectionIfMissingAsync(BindingResult result, AutomaticBindingRequest automaticBindingRequest)
    {
        if (result != BindingResult.ConnectionNotFound ||
            automaticBindingRequest is not AutomaticBindingRequest.Shared ||
            SharedBindingConfigModel == null)
        {
            return false;
        }

        var connectionInfo = SharedBindingConfigModel.CreateConnectionInfo();
        if (await connectedModeUiManager.ShowTrustConnectionDialogAsync(connectionInfo.GetServerConnectionFromConnectionInfo(), token: null) is not true)
        {
            return false;
        }

        // this is to ensure that the newly added connection is added to the view model properties
        await LoadDataAsync();
        return true;
    }

    private void Telemetry(AdapterResponseWithData<BindingResult> response, AutomaticBindingRequest automaticBinding)
    {
        if (!response.Success)
        {
            return;
        }

        switch (automaticBinding)
        {
            case AutomaticBindingRequest.Assisted { IsFromSharedBinding: true } or AutomaticBindingRequest.Shared:
                connectedModeServices.TelemetryManager.AddedFromSharedBindings();
                break;
            case AutomaticBindingRequest.Assisted:
                connectedModeServices.TelemetryManager.AddedAutomaticBindings();
                break;
        }
    }

    private BindingResult ValidateAutomaticBindingArguments(
        AutomaticBindingRequest automaticBinding,
        ServerConnection serverConnection,
        string serverProjectKey)
    {
        var logContext = new MessageLevelContext
        {
            Context = [ConnectedMode.Resources.ConnectedModeAutomaticBindingLogContext, automaticBinding.TypeName], VerboseContext = [automaticBinding.ToString()]
        };

        if (automaticBinding is AutomaticBindingRequest.Shared && SharedBindingConfigModel == null)
        {
            connectedModeServices.Logger.WriteLine(logContext, ConnectedMode.Resources.AutomaticBinding_ConfigurationNotAvailable);
            return BindingResult.SharedConfigurationNotAvailable;
        }

        if (string.IsNullOrEmpty(serverProjectKey))
        {
            connectedModeServices.Logger.WriteLine(logContext, ConnectedMode.Resources.AutomaticBinding_ProjectKeyNotFound);
            return BindingResult.ProjectKeyNotFound;
        }

        if (serverConnection == null)
        {
            connectedModeServices.Logger.WriteLine(logContext, ConnectedMode.Resources.AutomaticBinding_ConnectionNotFound);
            return BindingResult.ConnectionNotFound;
        }

        return AutomaticBindingCredentialsExists(logContext, serverConnection);
    }

    private ServerConnection GetServerConnection(AutomaticBindingRequest automaticBinding)
    {
        var serverConnectionId = automaticBinding switch
        {
            AutomaticBindingRequest.Assisted assistedBinding => assistedBinding.ServerConnectionId,
            AutomaticBindingRequest.Shared => sharedBindingConfigModel?.CreateConnectionInfo().GetServerIdFromConnectionInfo(),
            _ => null
        };

        return connectedModeServices.ServerConnectionsRepositoryAdapter.TryGet(serverConnectionId, out var serverConnection) ? serverConnection : null;
    }

    private string GetServerProjectKey(AutomaticBindingRequest automaticBinding) =>
        automaticBinding switch
        {
            AutomaticBindingRequest.Assisted assistedBinding => assistedBinding.ServerProjectKey,
            AutomaticBindingRequest.Shared => SharedBindingConfigModel?.ProjectKey,
            _ => null
        };

    private BindingResult AutomaticBindingCredentialsExists(MessageLevelContext logContext, ServerConnection serverConnection)
    {
        if (serverConnection.Credentials != null)
        {
            return BindingResult.Success;
        }

        connectedModeServices.Logger.WriteLine(
            logContext,
            ConnectedMode.Resources.AutomaticBinding_CredentiasNotFound,
            serverConnection.Id);
        connectedModeUiServices.MessageBox.Show(
            UiResources.NotFoundCredentialsForAutomaticBindingMessageBoxText,
            UiResources.NotFoundCredentialsForAutomaticBindingMessageBoxCaption,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return BindingResult.CredentialsNotFound;
    }
}
