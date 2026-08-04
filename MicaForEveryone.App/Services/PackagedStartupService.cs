using MicaForEveryone.CoreUI;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace MicaForEveryone.App.Services;

public class PackagedStartupService : IStartupService
{
    StartupTask? _task;

    public async Task SetStartupEnabledAsync(bool enabled)
    {
        _task ??= await StartupTask.GetAsync(AppIds.StartupTaskName);
        if (enabled)
        {
            await _task.RequestEnableAsync();
        }
        else
        {
            _task.Disable();
        }
    }

    public async Task<bool> GetStartupAvailableAsync()
    {
        _task ??= await StartupTask.GetAsync(AppIds.StartupTaskName);
        return _task.State != StartupTaskState.DisabledByPolicy && _task.State != StartupTaskState.DisabledByUser;
    }

    public async Task<bool> GetStartupEnabledAsync()
    {
        _task ??= await StartupTask.GetAsync(AppIds.StartupTaskName);
        return _task.State == StartupTaskState.Enabled;
    }
}
