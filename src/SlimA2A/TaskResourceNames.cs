namespace SlimA2A;

/// <summary>Maps A2A task identifiers to protobuf resource names (<c>tasks/{task_id}</c>).</summary>
public static class TaskResourceNames
{
    private const string Prefix = "tasks/";

    public static string ToResourceName(string taskId)
    {
        ArgumentException.ThrowIfNullOrEmpty(taskId);
        return taskId.StartsWith(Prefix, StringComparison.Ordinal) ? taskId : Prefix + taskId;
    }

    public static string FromResourceName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return name.StartsWith(Prefix, StringComparison.Ordinal) ? name[Prefix.Length..] : name;
    }

    public static string PushConfigResourceName(string taskId, string configId) =>
        $"{ToResourceName(taskId)}/pushNotificationConfigs/{configId}";

    public static (string TaskId, string ConfigId) ParsePushConfigResourceName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        const string mid = "/pushNotificationConfigs/";
        var i = name.IndexOf(mid, StringComparison.Ordinal);
        if (i < 0)
            throw new ArgumentException("Invalid push notification config resource name.", nameof(name));
        var taskPart = name[..i];
        var configId = name[(i + mid.Length)..];
        return (FromResourceName(taskPart), configId);
    }
}
