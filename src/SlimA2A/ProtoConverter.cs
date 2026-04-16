using System.Text.Json;
using A2A;
using Google.Protobuf.WellKnownTypes;
using PTask = A2a.V1.Task;

namespace SlimA2A;

/// <summary>Converts between a2a-dotnet models and generated <c>A2a.V1</c> protobuf messages.</summary>
public static class ProtoConverter
{
    public static A2a.V1.SendMessageRequest ToProto(SendMessageRequest r)
    {
        var p = new A2a.V1.SendMessageRequest { Request = ToProto(r.Message) };
        if (r.Configuration is { } cfg)
            p.Configuration = ToProto(cfg);
        if (ProtoStructJson.ToStruct(r.Metadata) is { } m)
            p.Metadata = m;
        return p;
    }

    public static SendMessageRequest FromProto(A2a.V1.SendMessageRequest p) =>
        new()
        {
            Message = FromProto(p.Request),
            Configuration = p.Configuration is null ? null : FromProto(p.Configuration),
            Metadata = ProtoStructJson.FromStruct(p.Metadata),
        };

    public static A2a.V1.SendMessageConfiguration ToProto(SendMessageConfiguration c)
    {
        var p = new A2a.V1.SendMessageConfiguration { Blocking = c.Blocking };
        if (c.HistoryLength is { } hl)
            p.HistoryLength = hl;
        p.AcceptedOutputModes.AddRange(c.AcceptedOutputModes ?? []);
        if (c.PushNotificationConfig is { } pn)
            p.PushNotification = ToProto(pn);
        return p;
    }

    public static SendMessageConfiguration FromProto(A2a.V1.SendMessageConfiguration p) =>
        new()
        {
            AcceptedOutputModes = [.. p.AcceptedOutputModes],
            HistoryLength = p.HistoryLength == 0 ? null : p.HistoryLength,
            Blocking = p.Blocking,
            PushNotificationConfig = p.PushNotification is null ? null : FromProto(p.PushNotification),
        };

    public static A2a.V1.Message ToProto(Message m)
    {
        var p = new A2a.V1.Message
        {
            MessageId = m.MessageId,
            Role = (A2a.V1.Role)(int)m.Role,
        };
        if (m.ContextId is { } cx)
            p.ContextId = cx;
        if (m.TaskId is { } tid)
            p.TaskId = tid;
        foreach (var part in m.Parts)
            p.Parts.Add(ToProto(part));
        if (ProtoStructJson.ToStruct(m.Metadata) is { } meta)
            p.Metadata = meta;
        if (m.Extensions is { } ext)
            p.Extensions.AddRange(ext);
        if (m.ReferenceTaskIds is { } r)
            p.ReferenceTaskIds.AddRange(r);
        return p;
    }

    public static Message FromProto(A2a.V1.Message p) =>
        new()
        {
            MessageId = p.MessageId,
            ContextId = string.IsNullOrEmpty(p.ContextId) ? null : p.ContextId,
            TaskId = string.IsNullOrEmpty(p.TaskId) ? null : p.TaskId,
            Role = (Role)(int)p.Role,
            Parts = p.Parts.Select(FromProto).ToList(),
            Metadata = ProtoStructJson.FromStruct(p.Metadata),
            Extensions = p.Extensions.Count == 0 ? null : [.. p.Extensions],
            ReferenceTaskIds = p.ReferenceTaskIds.Count == 0 ? null : [.. p.ReferenceTaskIds],
        };

    public static A2a.V1.Part ToProto(Part part)
    {
        var p = new A2a.V1.Part();
        switch (part.ContentCase)
        {
            case PartContentCase.Text:
                p.Text = part.Text!;
                break;
            case PartContentCase.Raw:
                p.File = new A2a.V1.FilePart
                {
                    FileWithBytes = Google.Protobuf.ByteString.CopyFrom(part.Raw),
                    MimeType = part.MediaType ?? "",
                    Name = part.Filename ?? "",
                };
                break;
            case PartContentCase.Url:
                p.File = new A2a.V1.FilePart
                {
                    FileWithUri = part.Url!,
                    MimeType = part.MediaType ?? "",
                    Name = part.Filename ?? "",
                };
                break;
            case PartContentCase.Data:
                p.Data = new A2a.V1.DataPart { Data = ProtoStructJson.JsonElementToStruct(part.Data!.Value) };
                break;
        }
        if (ProtoStructJson.ToStruct(part.Metadata) is { } pm)
            p.Metadata = pm;
        return p;
    }

    public static Part FromProto(A2a.V1.Part p)
    {
        var part = new Part();
        switch (p.PartCase)
        {
            case A2a.V1.Part.PartOneofCase.Text:
                part.Text = p.Text;
                break;
            case A2a.V1.Part.PartOneofCase.File:
                if (p.File.HasFileWithBytes)
                    part.Raw = p.File.FileWithBytes.ToByteArray();
                else if (p.File.HasFileWithUri)
                    part.Url = p.File.FileWithUri;
                part.MediaType = string.IsNullOrEmpty(p.File.MimeType) ? null : p.File.MimeType;
                part.Filename = string.IsNullOrEmpty(p.File.Name) ? null : p.File.Name;
                break;
            case A2a.V1.Part.PartOneofCase.Data:
                part.Data = ProtoStructJson.StructToJsonElement(p.Data.Data);
                break;
        }
        part.Metadata = ProtoStructJson.FromStruct(p.Metadata);
        return part;
    }

    public static A2a.V1.SendMessageResponse ToProto(SendMessageResponse r)
    {
        var p = new A2a.V1.SendMessageResponse();
        if (r.Task is { } t)
            p.Task = ToProto(t);
        else if (r.Message is { } msg)
            p.Msg = ToProto(msg);
        return p;
    }

    public static SendMessageResponse FromProto(A2a.V1.SendMessageResponse p) =>
        p.PayloadCase switch
        {
            A2a.V1.SendMessageResponse.PayloadOneofCase.Task => new SendMessageResponse { Task = FromProto(p.Task) },
            A2a.V1.SendMessageResponse.PayloadOneofCase.Msg => new SendMessageResponse { Message = FromProto(p.Msg) },
            _ => new SendMessageResponse(),
        };

    public static A2a.V1.StreamResponse ToProtoStream(StreamResponse r)
    {
        var p = new A2a.V1.StreamResponse();
        if (r.Task is { } t)
            p.Task = ToProto(t);
        else if (r.Message is { } m)
            p.Msg = ToProto(m);
        else if (r.StatusUpdate is { } su)
            p.StatusUpdate = ToProto(su);
        else if (r.ArtifactUpdate is { } au)
            p.ArtifactUpdate = ToProto(au);
        return p;
    }

    public static StreamResponse FromProtoStream(A2a.V1.StreamResponse p) =>
        p.PayloadCase switch
        {
            A2a.V1.StreamResponse.PayloadOneofCase.Task => new StreamResponse { Task = FromProto(p.Task) },
            A2a.V1.StreamResponse.PayloadOneofCase.Msg => new StreamResponse { Message = FromProto(p.Msg) },
            A2a.V1.StreamResponse.PayloadOneofCase.StatusUpdate => new StreamResponse { StatusUpdate = FromProto(p.StatusUpdate) },
            A2a.V1.StreamResponse.PayloadOneofCase.ArtifactUpdate => new StreamResponse { ArtifactUpdate = FromProto(p.ArtifactUpdate) },
            _ => new StreamResponse(),
        };

    public static A2a.V1.TaskStatusUpdateEvent ToProto(TaskStatusUpdateEvent e)
    {
        var p = new A2a.V1.TaskStatusUpdateEvent
        {
            TaskId = e.TaskId,
            ContextId = e.ContextId,
            Status = ToProto(e.Status),
        };
        if (ProtoStructJson.ToStruct(e.Metadata) is { } m)
            p.Metadata = m;
        return p;
    }

    public static TaskStatusUpdateEvent FromProto(A2a.V1.TaskStatusUpdateEvent p) =>
        new()
        {
            TaskId = p.TaskId,
            ContextId = p.ContextId,
            Status = FromProto(p.Status),
            Metadata = ProtoStructJson.FromStruct(p.Metadata),
        };

    public static A2a.V1.TaskArtifactUpdateEvent ToProto(TaskArtifactUpdateEvent e)
    {
        var p = new A2a.V1.TaskArtifactUpdateEvent
        {
            TaskId = e.TaskId,
            ContextId = e.ContextId,
            Artifact = ToProto(e.Artifact),
            Append = e.Append,
            LastChunk = e.LastChunk,
        };
        if (ProtoStructJson.ToStruct(e.Metadata) is { } m)
            p.Metadata = m;
        return p;
    }

    public static TaskArtifactUpdateEvent FromProto(A2a.V1.TaskArtifactUpdateEvent p) =>
        new()
        {
            TaskId = p.TaskId,
            ContextId = p.ContextId,
            Artifact = FromProto(p.Artifact),
            Append = p.Append,
            LastChunk = p.LastChunk,
            Metadata = ProtoStructJson.FromStruct(p.Metadata),
        };

    public static A2a.V1.Artifact ToProto(Artifact a)
    {
        var p = new A2a.V1.Artifact { ArtifactId = a.ArtifactId };
        if (a.Name is { } n)
            p.Name = n;
        if (a.Description is { } d)
            p.Description = d;
        foreach (var part in a.Parts)
            p.Parts.Add(ToProto(part));
        if (ProtoStructJson.ToStruct(a.Metadata) is { } m)
            p.Metadata = m;
        if (a.Extensions is { } ex)
            p.Extensions.AddRange(ex);
        return p;
    }

    public static Artifact FromProto(A2a.V1.Artifact p) =>
        new()
        {
            ArtifactId = p.ArtifactId,
            Name = string.IsNullOrEmpty(p.Name) ? null : p.Name,
            Description = string.IsNullOrEmpty(p.Description) ? null : p.Description,
            Parts = p.Parts.Select(FromProto).ToList(),
            Metadata = ProtoStructJson.FromStruct(p.Metadata),
            Extensions = p.Extensions.Count == 0 ? null : [.. p.Extensions],
        };

    public static A2a.V1.TaskStatus ToProto(A2A.TaskStatus s)
    {
        var p = new A2a.V1.TaskStatus { State = (A2a.V1.TaskState)(int)s.State };
        if (s.Message is { } msg)
            p.Update = ToProto(msg);
        if (s.Timestamp is { } ts)
            p.Timestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(ts.UtcDateTime, DateTimeKind.Utc));
        return p;
    }

    public static A2A.TaskStatus FromProto(A2a.V1.TaskStatus p) =>
        new()
        {
            State = (TaskState)(int)p.State,
            Message = p.Update is null ? null : FromProto(p.Update),
            Timestamp = p.Timestamp is null ? null : p.Timestamp.ToDateTimeOffset(),
        };

    public static A2a.V1.Task ToProto(AgentTask t)
    {
        var p = new A2a.V1.Task
        {
            Id = t.Id,
            ContextId = t.ContextId,
            Status = ToProto(t.Status),
        };
        foreach (var a in t.Artifacts ?? [])
            p.Artifacts.Add(ToProto(a));
        foreach (var h in t.History ?? [])
            p.History.Add(ToProto(h));
        if (ProtoStructJson.ToStruct(t.Metadata) is { } m)
            p.Metadata = m;
        return p;
    }

    public static AgentTask FromProto(PTask p) =>
        new()
        {
            Id = p.Id,
            ContextId = p.ContextId,
            Status = FromProto(p.Status),
            Artifacts = p.Artifacts.Count == 0 ? null : p.Artifacts.Select(FromProto).ToList(),
            History = p.History.Count == 0 ? null : p.History.Select(FromProto).ToList(),
            Metadata = ProtoStructJson.FromStruct(p.Metadata),
        };

    public static A2a.V1.GetTaskRequest ToProto(GetTaskRequest r)
    {
        var p = new A2a.V1.GetTaskRequest { Name = TaskResourceNames.ToResourceName(r.Id) };
        if (r.HistoryLength is { } hl)
            p.HistoryLength = hl;
        return p;
    }

    public static GetTaskRequest FromProto(A2a.V1.GetTaskRequest p) =>
        new()
        {
            Id = TaskResourceNames.FromResourceName(p.Name),
            HistoryLength = p.HistoryLength == 0 ? null : p.HistoryLength,
        };

    public static A2a.V1.ListTasksRequest ToProto(ListTasksRequest r)
    {
        var p = new A2a.V1.ListTasksRequest();
        if (r.ContextId is { } c)
            p.ContextId = c;
        if (r.Status is { } st)
            p.Status = (A2a.V1.TaskState)(int)st;
        if (r.PageSize is { } ps)
            p.PageSize = ps;
        if (r.PageToken is { } pt)
            p.PageToken = pt;
        if (r.HistoryLength is { } hl)
            p.HistoryLength = hl;
        if (r.StatusTimestampAfter is { } a)
            p.LastUpdatedTime = Timestamp.FromDateTime(DateTime.SpecifyKind(a.UtcDateTime, DateTimeKind.Utc));
        if (r.IncludeArtifacts is { } ia)
            p.IncludeArtifacts = ia;
        return p;
    }

    public static ListTasksRequest FromProto(A2a.V1.ListTasksRequest p) =>
        new()
        {
            ContextId = string.IsNullOrEmpty(p.ContextId) ? null : p.ContextId,
            Status = p.Status == A2a.V1.TaskState.Unspecified ? null : (TaskState)(int)p.Status,
            PageSize = p.PageSize == 0 ? null : p.PageSize,
            PageToken = string.IsNullOrEmpty(p.PageToken) ? null : p.PageToken,
            HistoryLength = p.HistoryLength == 0 ? null : p.HistoryLength,
            StatusTimestampAfter = p.LastUpdatedTime is null ? null : p.LastUpdatedTime.ToDateTimeOffset(),
            IncludeArtifacts = p.IncludeArtifacts ? true : null,
        };

    public static ListTasksResponse FromProto(A2a.V1.ListTasksResponse p)
    {
        var tasks = p.Tasks.Select(FromProto).ToList();
        return new ListTasksResponse
        {
            Tasks = tasks,
            NextPageToken = p.NextPageToken ?? string.Empty,
            PageSize = tasks.Count,
            TotalSize = p.TotalSize,
        };
    }

    public static A2a.V1.CancelTaskRequest ToProto(CancelTaskRequest r) =>
        new() { Name = TaskResourceNames.ToResourceName(r.Id) };

    public static A2a.V1.TaskSubscriptionRequest ToProto(SubscribeToTaskRequest r) =>
        new() { Name = TaskResourceNames.ToResourceName(r.Id) };

    public static SubscribeToTaskRequest FromProto(A2a.V1.TaskSubscriptionRequest p) =>
        new() { Id = TaskResourceNames.FromResourceName(p.Name) };

    public static A2a.V1.PushNotificationConfig ToProto(PushNotificationConfig c)
    {
        var p = new A2a.V1.PushNotificationConfig { Url = c.Url };
        if (c.Id is { } id)
            p.Id = id;
        if (c.Token is { } t)
            p.Token = t;
        if (c.Authentication is { } a)
            p.Authentication = ToProto(a);
        return p;
    }

    public static PushNotificationConfig FromProto(A2a.V1.PushNotificationConfig p) =>
        new()
        {
            Id = string.IsNullOrEmpty(p.Id) ? null : p.Id,
            Url = p.Url,
            Token = string.IsNullOrEmpty(p.Token) ? null : p.Token,
            Authentication = p.Authentication is null ? null : FromProto(p.Authentication),
        };

    public static A2a.V1.AuthenticationInfo ToProto(AuthenticationInfo a)
    {
        var p = new A2a.V1.AuthenticationInfo { Credentials = a.Credentials ?? "" };
        if (!string.IsNullOrEmpty(a.Scheme))
            p.Schemes.Add(a.Scheme);
        return p;
    }

    public static AuthenticationInfo FromProto(A2a.V1.AuthenticationInfo p) =>
        new()
        {
            Scheme = p.Schemes.Count > 0 ? p.Schemes[0] : string.Empty,
            Credentials = string.IsNullOrEmpty(p.Credentials) ? null : p.Credentials,
        };

    public static A2a.V1.CreateTaskPushNotificationConfigRequest ToProto(CreateTaskPushNotificationConfigRequest r) =>
        new()
        {
            Parent = TaskResourceNames.ToResourceName(r.TaskId),
            ConfigId = r.ConfigId,
            Config = new A2a.V1.TaskPushNotificationConfig { PushNotificationConfig = ToProto(r.Config) },
        };

    public static A2a.V1.GetTaskPushNotificationConfigRequest ToProto(GetTaskPushNotificationConfigRequest r) =>
        new() { Name = TaskResourceNames.PushConfigResourceName(r.TaskId, r.Id) };

    public static A2a.V1.ListTaskPushNotificationConfigRequest ToProto(ListTaskPushNotificationConfigRequest r)
    {
        var p = new A2a.V1.ListTaskPushNotificationConfigRequest
        {
            Parent = TaskResourceNames.ToResourceName(r.TaskId),
        };
        if (r.PageSize is { } ps)
            p.PageSize = ps;
        if (r.PageToken is { } pt)
            p.PageToken = pt;
        return p;
    }

    public static A2a.V1.DeleteTaskPushNotificationConfigRequest ToProto(DeleteTaskPushNotificationConfigRequest r) =>
        new() { Name = TaskResourceNames.PushConfigResourceName(r.TaskId, r.Id) };

    public static TaskPushNotificationConfig FromProto(A2a.V1.TaskPushNotificationConfig p)
    {
        var (taskId, configId) = TaskResourceNames.ParsePushConfigResourceName(p.Name);
        return new TaskPushNotificationConfig
        {
            Id = configId,
            TaskId = taskId,
            PushNotificationConfig = p.PushNotificationConfig is null
                ? new PushNotificationConfig()
                : FromProto(p.PushNotificationConfig),
        };
    }

    public static A2a.V1.TaskPushNotificationConfig ToProtoResource(TaskPushNotificationConfig c) =>
        new()
        {
            Name = TaskResourceNames.PushConfigResourceName(c.TaskId, c.Id),
            PushNotificationConfig = ToProto(c.PushNotificationConfig),
        };

    public static ListTaskPushNotificationConfigResponse FromProto(A2a.V1.ListTaskPushNotificationConfigResponse p) =>
        new()
        {
            Configs = p.Configs.Select(FromProto).ToList(),
            NextPageToken = string.IsNullOrEmpty(p.NextPageToken) ? null : p.NextPageToken,
        };

    public static A2a.V1.AgentCard ToProto(AgentCard c)
    {
        var p = new A2a.V1.AgentCard
        {
            Name = c.Name,
            Description = c.Description,
            Version = c.Version,
            ProtocolVersion = "1.0",
        };
        if (c.DocumentationUrl is { } du)
            p.DocumentationUrl = du;
        if (c.IconUrl is { } iu)
            p.IconUrl = iu;
        if (c.Provider is { } pr)
        {
            p.Provider = new A2a.V1.AgentProvider
            {
                Url = pr.Url,
                Organization = pr.Organization,
            };
        }
        p.Capabilities = ToProto(c.Capabilities);
        foreach (var mode in c.DefaultInputModes)
            p.DefaultInputModes.Add(mode);
        foreach (var mode in c.DefaultOutputModes)
            p.DefaultOutputModes.Add(mode);
        foreach (var sk in c.Skills)
            p.Skills.Add(ToProto(sk));
        if (c.SupportedInterfaces.Count > 0)
        {
            var first = c.SupportedInterfaces[0];
            p.Url = first.Url;
            p.PreferredTransport = first.ProtocolBinding;
            p.ProtocolVersion = first.ProtocolVersion;
            for (var i = 1; i < c.SupportedInterfaces.Count; i++)
            {
                var iface = c.SupportedInterfaces[i];
                p.AdditionalInterfaces.Add(new A2a.V1.AgentInterface
                {
                    Url = iface.Url,
                    Transport = iface.ProtocolBinding,
                });
            }
        }
        return p;
    }

    public static AgentCard FromProto(A2a.V1.AgentCard p)
    {
        var list = new List<AgentInterface>();
        if (!string.IsNullOrEmpty(p.Url))
        {
            list.Add(new AgentInterface
            {
                Url = p.Url,
                ProtocolBinding = string.IsNullOrEmpty(p.PreferredTransport) ? "JSONRPC" : p.PreferredTransport,
                ProtocolVersion = string.IsNullOrEmpty(p.ProtocolVersion) ? "1.0" : p.ProtocolVersion,
            });
        }
        foreach (var ai in p.AdditionalInterfaces)
        {
            list.Add(new AgentInterface
            {
                Url = ai.Url,
                ProtocolBinding = string.IsNullOrEmpty(ai.Transport) ? "JSONRPC" : ai.Transport,
                ProtocolVersion = "1.0",
            });
        }
        if (list.Count == 0)
        {
            list.Add(new AgentInterface { Url = "", ProtocolBinding = "JSONRPC", ProtocolVersion = "1.0" });
        }
        return new AgentCard
        {
            Name = p.Name,
            Description = p.Description,
            Version = p.Version,
            DocumentationUrl = string.IsNullOrEmpty(p.DocumentationUrl) ? null : p.DocumentationUrl,
            IconUrl = string.IsNullOrEmpty(p.IconUrl) ? null : p.IconUrl,
            SupportedInterfaces = list,
            Capabilities = p.Capabilities is null ? new AgentCapabilities() : FromProto(p.Capabilities),
            Provider = p.Provider is null
                ? null
                : new AgentProvider
                {
                    Url = p.Provider.Url,
                    Organization = p.Provider.Organization,
                },
            Skills = p.Skills.Select(FromProto).ToList(),
            DefaultInputModes = [.. p.DefaultInputModes],
            DefaultOutputModes = [.. p.DefaultOutputModes],
        };
    }

    public static A2a.V1.AgentCapabilities ToProto(AgentCapabilities c) =>
        new()
        {
            Streaming = c.Streaming ?? false,
            PushNotifications = c.PushNotifications ?? false,
            StateTransitionHistory = false,
        };

    public static AgentCapabilities FromProto(A2a.V1.AgentCapabilities p) =>
        new()
        {
            Streaming = p.Streaming,
            PushNotifications = p.PushNotifications,
        };

    public static A2a.V1.AgentSkill ToProto(AgentSkill s)
    {
        var p = new A2a.V1.AgentSkill
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
        };
        p.Tags.AddRange(s.Tags);
        if (s.Examples is { } ex)
            p.Examples.AddRange(ex);
        if (s.InputModes is { } im)
            p.InputModes.AddRange(im);
        if (s.OutputModes is { } om)
            p.OutputModes.AddRange(om);
        return p;
    }

    public static AgentSkill FromProto(A2a.V1.AgentSkill p) =>
        new()
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Tags = [.. p.Tags],
            Examples = p.Examples.Count == 0 ? null : [.. p.Examples],
            InputModes = p.InputModes.Count == 0 ? null : [.. p.InputModes],
            OutputModes = p.OutputModes.Count == 0 ? null : [.. p.OutputModes],
        };
}
