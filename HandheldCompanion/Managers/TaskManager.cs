using Microsoft.Win32.TaskScheduler;
using System;
<<<<<<< HEAD
=======
using System.Security.Principal;
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc

namespace HandheldCompanion.Managers;

public class TaskManager : Manager
{
    private readonly string TaskName;
    private readonly string TaskExecutable;

    // TaskManager vars
    private Task task;
    private TaskDefinition taskDefinition;
    private TaskService taskService;

    public TaskManager(string TaskName, string Executable)
    {
        this.TaskName = TaskName;
        TaskExecutable = Executable;

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
    }

    private void SettingsManager_SettingValueChanged(string name, object value)
    {
        switch (name)
        {
            case "RunAtStartup":
                UpdateTask(Convert.ToBoolean(value));
                break;
        }
    }

    public override void Start()
    {
        taskService = new TaskService();
        task = taskService.FindTask(TaskName);

        try
        {
            if (task is not null)
                taskService.RootFolder.DeleteTask(TaskName);

            taskDefinition = TaskService.Instance.NewTask();
            taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
            taskDefinition.Principal.UserId = WindowsIdentity.GetCurrent().Name;
            taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;
            taskDefinition.Settings.DisallowStartIfOnBatteries = false;
            taskDefinition.Settings.StopIfGoingOnBatteries = false;
            taskDefinition.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            taskDefinition.Settings.Enabled = false;
            taskDefinition.Triggers.Add(new LogonTrigger() { UserId = WindowsIdentity.GetCurrent().Name });
            taskDefinition.Actions.Add(new ExecAction(TaskExecutable));

            task = TaskService.Instance.RootFolder.RegisterTaskDefinition(TaskName, taskDefinition);
            task.Enabled = SettingsManager.GetBoolean("RunAtStartup");
        }
        catch
        {
        }

        base.Start();
    }

    public override void Stop()
    {
        if (!IsInitialized)
            return;

        base.Stop();
    }

    public void UpdateTask(bool value)
    {
        if (task is null)
            return;

        try
        {
            task.Enabled = value;
        }
        catch
        {
        }
    }
}