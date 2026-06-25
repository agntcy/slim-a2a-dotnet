using System.Text.Json;
using A2A;
using Google.Protobuf.WellKnownTypes;
using PTask = Lf.A2a.V1.Task;

namespace SlimA2A;

/// <summary>Converts between a2a-dotnet models and generated <c>Lf.A2a.V1</c> protobuf messages.</summary>
public static class ProtoConverter
{
    public static Lf.A2a.V1.SendMessageRequest ToProto(SendMessageRequest r)
    {
        var p = new Lf.A2a.V1.SendMessageRequest { Message = ToProto(r.Message) };
        if (r.Configuration is { } cfg)
            p.Configuration = ToProto(cfg);
        if (ProtoStructJson.ToStruct(r.Metadata) is { } m)
            p.Metadata = m;
        return p;
    }

    public static SendMessageRequest FromProto(Lf.A2a.V1.SendMessageRequest p) =>
        new()
        {
            Message = FromProto(p.Message),
            Configuration = p.Configuration is null ? null : FromProto(p.Configuration),
            Metadata = ProtoStructJson.FromStruct(p.Metadata),
        };

    public static Lf.A2a.V1.SendMessageConfiguration ToProto(SendMessageConfiguration c)
    {
        // proto `return_immediately` is the inverse of the model's `Blocking` (default: wait).
        var p = new Lf.A2a.V1.SendMessageConfiguration { ReturnImmediately = !c.Blocking };
        if (c.HistoryLength is { } hl)
            p.HistoryLength = hl;
        p.AcceptedOutputModes.AddRange(c.AcceptedOutputModes ?? []);
        if (c.PushNotificationConfig is { } pn)
            p.TaskPushNotificationConfig = ToProtoPushConfig(pn);
        return p;
    }

    public static SendMessageConfiguration FromProto(Lf.A2a.V1.SendMessageConfiguration p) =>
        new()
        {
            AcceptedOutputModes = [.. p.AcceptedOutputModes],
            HistoryLength = p.HasHistoryLength ? p.HistoryLength : null,
            Blocking = !p.ReturnImmediately,
            PushNotificationConfig = p.TaskPushNotificationConfig is null
                ? null
                : FromProtoPushConfig(p.TaskPushNotificationConfig),
        };

    public static Lf.A2a.V1.Message ToProto(Message m)
    {
        var p = new Lf.A2a.V1.Message
        {
            MessageId = m.MessageId,
            Role = (Lf.A2a.V1.Role)(int)m.Role,
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

    public static Message FromProto(Lf.A2a.V1.Message p) =>
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

    public static Lf.A2a.V1.Part ToProto(Part part)
    {
        // a2a v1.0 flattened Part: file/data contents and media_type/filename live directly on Part.
        var p = new Lf.A2a.V1.Part();
        switch (part.ContentCase)
        {
            case PartContentCase.Text:
                p.Text = part.Text!;
                break;
            case PartContentCase.Raw:
                p.Raw = Google.Protobuf.ByteString.CopyFrom(part.Raw);
                break;
            case PartContentCase.Url:
                p.Url = part.Url!;
                break;
            case PartContentCase.Data:
                p.Data = ProtoStructJson.JsonElementToValue(part.Data!.Value);
                break;
        }
        if (part.MediaType is { } mt)
            p.MediaType = mt;
        if (part.Filename is { } fn)
            p.Filename = fn;
        if (ProtoStructJson.ToStruct(part.Metadata) is { } pm)
            p.Metadata = pm;
        return p;
    }

    public static Part FromProto(Lf.A2a.V1.Part p)
    {
        var part = new Part();
        switch (p.ContentCase)
        {
            case Lf.A2a.V1.Part.ContentOneofCase.Text:
                part.Text = p.Text;
                break;
            case Lf.A2a.V1.Part.ContentOneofCase.Raw:
                part.Raw = p.Raw.ToByteArray();
                break;
            case Lf.A2a.V1.Part.ContentOneofCase.Url:
                part.Url = p.Url;
                break;
            case Lf.A2a.V1.Part.ContentOneofCase.Data:
                part.Data = ProtoStructJson.ValueToJsonElement(p.Data);
                break;
        }
        part.MediaType = string.IsNullOrEmpty(p.MediaType) ? null : p.MediaType;
        part.Filename = string.IsNullOrEmpty(p.Filename) ? null : p.Filename;
        part.Metadata = ProtoStructJson.FromStruct(p.Metadata);
        return part;
    }

    public static Lf.A2a.V1.SendMessageResponse ToProto(SendMessageResponse r)
    {
        var p = new Lf.A2a.V1.SendMessageResponse();
        if (r.Task is { } t)
            p.Task = ToProto(t);
        else if (r.Message is { } msg)
            p.Message = ToProto(msg);
        return p;
    }

    public static SendMessageResponse FromProto(Lf.A2a.V1.SendMessageResponse p) =>
        p.PayloadCase switch
        {
            Lf.A2a.V1.SendMessageResponse.PayloadOneofCase.Task => new SendMessageResponse { Task = FromProto(p.Task) },
            Lf.A2a.V1.SendMessageResponse.PayloadOneofCase.Message => new SendMessageResponse { Message = FromProto(p.Message) },
            _ => new SendMessageResponse(),
        };

    public static Lf.A2a.V1.StreamResponse ToProtoStream(StreamResponse r)
    {
        var p = new Lf.A2a.V1.StreamResponse();
        if (r.Task is { } t)
            p.Task = ToProto(t);
        else if (r.Message is { } m)
            p.Message = ToProto(m);
        else if (r.StatusUpdate is { } su)
            p.StatusUpdate = ToProto(su);
        else if (r.ArtifactUpdate is { } au)
            p.ArtifactUpdate = ToProto(au);
        return p;
    }

    public static StreamResponse FromProtoStream(Lf.A2a.V1.StreamResponse p) =>
        p.PayloadCase switch
        {
            Lf.A2a.V1.StreamResponse.PayloadOneofCase.Task => new StreamResponse { Task = FromProto(p.Task) },
            Lf.A2a.V1.StreamResponse.PayloadOneofCase.Message => new StreamResponse { Message = FromProto(p.Message) },
            Lf.A2a.V1.StreamResponse.PayloadOneofCase.StatusUpdate => new StreamResponse { StatusUpdate = FromProto(p.StatusUpdate) },
            Lf.A2a.V1.StreamResponse.PayloadOneofCase.ArtifactUpdate => new StreamResponse { ArtifactUpdate = FromProto(p.ArtifactUpdate) },
            _ => new StreamResponse(),
        };

    public static Lf.A2a.V1.TaskStatusUpdateEvent ToProto(TaskStatusUpdateEvent e)
    {
        var p = new Lf.A2a.V1.TaskStatusUpdateEvent
        {
            TaskId = e.TaskId,
            ContextId = e.ContextId,
            Status = ToProto(e.Status),
        };
        if (ProtoStructJson.ToStruct(e.Metadata) is { } m)
            p.Metadata = m;
        return p;
    }

    public static TaskStatusUpdateEvent FromProto(Lf.A2a.V1.TaskStatusUpdateEvent p) =>
        new()
        {
            TaskId = p.TaskId,
            ContextId = p.ContextId,
            Status = FromProto(p.Status),
            Metadata = ProtoStructJson.FromStruct(p.Metadata),
        };

    public static Lf.A2a.V1.TaskArtifactUpdateEvent ToProto(TaskArtifactUpdateEvent e)
    {
        var p = new Lf.A2a.V1.TaskArtifactUpdateEvent
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

    public static TaskArtifactUpdateEvent FromProto(Lf.A2a.V1.TaskArtifactUpdateEvent p) =>
        new()
        {
            TaskId = p.TaskId,
            ContextId = p.ContextId,
            Artifact = FromProto(p.Artifact),
            Append = p.Append,
            LastChunk = p.LastChunk,
            Metadata = ProtoStructJson.FromStruct(p.Metadata),
        };

    public static Lf.A2a.V1.Artifact ToProto(Artifact a)
    {
        var p = new Lf.A2a.V1.Artifact { ArtifactId = a.ArtifactId };
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

    public static Artifact FromProto(Lf.A2a.V1.Artifact p) =>
        new()
        {
            ArtifactId = p.ArtifactId,
            Name = string.IsNullOrEmpty(p.Name) ? null : p.Name,
            Description = string.IsNullOrEmpty(p.Description) ? null : p.Description,
            Parts = p.Parts.Select(FromProto).ToList(),
            Metadata = ProtoStructJson.FromStruct(p.Metadata),
            Extensions = p.Extensions.Count == 0 ? null : [.. p.Extensions],
        };

    public static Lf.A2a.V1.TaskStatus ToProto(A2A.TaskStatus s)
    {
        var p = new Lf.A2a.V1.TaskStatus { State = (Lf.A2a.V1.TaskState)(int)s.State };
        if (s.Message is { } msg)
            p.Message = ToProto(msg);
        if (s.Timestamp is { } ts)
            p.Timestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(ts.UtcDateTime, DateTimeKind.Utc));
        return p;
    }

    public static A2A.TaskStatus FromProto(Lf.A2a.V1.TaskStatus p) =>
        new()
        {
            State = (TaskState)(int)p.State,
            Message = p.Message is null ? null : FromProto(p.Message),
            Timestamp = p.Timestamp is null ? null : p.Timestamp.ToDateTimeOffset(),
        };

    public static Lf.A2a.V1.Task ToProto(AgentTask t)
    {
        var p = new Lf.A2a.V1.Task
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

    public static Lf.A2a.V1.GetTaskRequest ToProto(GetTaskRequest r)
    {
        var p = new Lf.A2a.V1.GetTaskRequest { Id = r.Id };
        if (r.HistoryLength is { } hl)
            p.HistoryLength = hl;
        return p;
    }

    public static GetTaskRequest FromProto(Lf.A2a.V1.GetTaskRequest p) =>
        new()
        {
            Id = p.Id,
            HistoryLength = p.HasHistoryLength ? p.HistoryLength : null,
        };

    public static Lf.A2a.V1.ListTasksRequest ToProto(ListTasksRequest r)
    {
        var p = new Lf.A2a.V1.ListTasksRequest();
        if (r.ContextId is { } c)
            p.ContextId = c;
        if (r.Status is { } st)
            p.Status = (Lf.A2a.V1.TaskState)(int)st;
        if (r.PageSize is { } ps)
            p.PageSize = ps;
        if (r.PageToken is { } pt)
            p.PageToken = pt;
        if (r.HistoryLength is { } hl)
            p.HistoryLength = hl;
        if (r.StatusTimestampAfter is { } a)
            p.StatusTimestampAfter = Timestamp.FromDateTime(DateTime.SpecifyKind(a.UtcDateTime, DateTimeKind.Utc));
        if (r.IncludeArtifacts is { } ia)
            p.IncludeArtifacts = ia;
        return p;
    }

    public static ListTasksRequest FromProto(Lf.A2a.V1.ListTasksRequest p) =>
        new()
        {
            ContextId = string.IsNullOrEmpty(p.ContextId) ? null : p.ContextId,
            Status = p.Status == Lf.A2a.V1.TaskState.Unspecified ? null : (TaskState)(int)p.Status,
            PageSize = p.HasPageSize ? p.PageSize : null,
            PageToken = string.IsNullOrEmpty(p.PageToken) ? null : p.PageToken,
            HistoryLength = p.HasHistoryLength ? p.HistoryLength : null,
            StatusTimestampAfter = p.StatusTimestampAfter is null ? null : p.StatusTimestampAfter.ToDateTimeOffset(),
            IncludeArtifacts = p.HasIncludeArtifacts ? p.IncludeArtifacts : null,
        };

    public static ListTasksResponse FromProto(Lf.A2a.V1.ListTasksResponse p)
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

    public static Lf.A2a.V1.CancelTaskRequest ToProto(CancelTaskRequest r) =>
        new() { Id = r.Id };

    public static Lf.A2a.V1.SubscribeToTaskRequest ToProto(SubscribeToTaskRequest r) =>
        new() { Id = r.Id };

    public static SubscribeToTaskRequest FromProto(Lf.A2a.V1.SubscribeToTaskRequest p) =>
        new() { Id = p.Id };

    // a2a v1.0 merged PushNotificationConfig into the flattened TaskPushNotificationConfig message.
    private static Lf.A2a.V1.TaskPushNotificationConfig ToProtoPushConfig(PushNotificationConfig c)
    {
        var p = new Lf.A2a.V1.TaskPushNotificationConfig { Url = c.Url };
        if (c.Id is { } id)
            p.Id = id;
        if (c.Token is { } t)
            p.Token = t;
        if (c.Authentication is { } a)
            p.Authentication = ToProto(a);
        return p;
    }

    private static PushNotificationConfig FromProtoPushConfig(Lf.A2a.V1.TaskPushNotificationConfig p) =>
        new()
        {
            Id = string.IsNullOrEmpty(p.Id) ? null : p.Id,
            Url = p.Url,
            Token = string.IsNullOrEmpty(p.Token) ? null : p.Token,
            Authentication = p.Authentication is null ? null : FromProto(p.Authentication),
        };

    public static Lf.A2a.V1.AuthenticationInfo ToProto(AuthenticationInfo a) =>
        new()
        {
            Scheme = a.Scheme ?? "",
            Credentials = a.Credentials ?? "",
        };

    public static AuthenticationInfo FromProto(Lf.A2a.V1.AuthenticationInfo p) =>
        new()
        {
            Scheme = p.Scheme,
            Credentials = string.IsNullOrEmpty(p.Credentials) ? null : p.Credentials,
        };

    public static Lf.A2a.V1.TaskPushNotificationConfig ToProto(CreateTaskPushNotificationConfigRequest r)
    {
        var p = ToProtoPushConfig(r.Config);
        p.TaskId = r.TaskId;
        p.Id = r.ConfigId;
        return p;
    }

    public static Lf.A2a.V1.GetTaskPushNotificationConfigRequest ToProto(GetTaskPushNotificationConfigRequest r) =>
        new() { TaskId = r.TaskId, Id = r.Id };

    public static Lf.A2a.V1.ListTaskPushNotificationConfigsRequest ToProto(ListTaskPushNotificationConfigRequest r)
    {
        var p = new Lf.A2a.V1.ListTaskPushNotificationConfigsRequest { TaskId = r.TaskId };
        if (r.PageSize is { } ps)
            p.PageSize = ps;
        if (r.PageToken is { } pt)
            p.PageToken = pt;
        return p;
    }

    public static Lf.A2a.V1.DeleteTaskPushNotificationConfigRequest ToProto(DeleteTaskPushNotificationConfigRequest r) =>
        new() { TaskId = r.TaskId, Id = r.Id };

    public static TaskPushNotificationConfig FromProto(Lf.A2a.V1.TaskPushNotificationConfig p) =>
        new()
        {
            Id = p.Id,
            TaskId = p.TaskId,
            PushNotificationConfig = FromProtoPushConfig(p),
        };

    public static Lf.A2a.V1.TaskPushNotificationConfig ToProtoResource(TaskPushNotificationConfig c)
    {
        var p = ToProtoPushConfig(c.PushNotificationConfig);
        p.TaskId = c.TaskId;
        p.Id = c.Id;
        return p;
    }

    public static ListTaskPushNotificationConfigResponse FromProto(Lf.A2a.V1.ListTaskPushNotificationConfigsResponse p) =>
        new()
        {
            Configs = p.Configs.Select(FromProto).ToList(),
            NextPageToken = string.IsNullOrEmpty(p.NextPageToken) ? null : p.NextPageToken,
        };

    public static Lf.A2a.V1.AgentCard ToProto(AgentCard c)
    {
        var p = new Lf.A2a.V1.AgentCard
        {
            Name = c.Name,
            Description = c.Description,
            Version = c.Version,
        };
        if (c.DocumentationUrl is { } du)
            p.DocumentationUrl = du;
        if (c.IconUrl is { } iu)
            p.IconUrl = iu;
        if (c.Provider is { } pr)
        {
            p.Provider = new Lf.A2a.V1.AgentProvider
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
        // a2a v1.0 moved transport details entirely into the repeated supported_interfaces.
        foreach (var iface in c.SupportedInterfaces)
        {
            p.SupportedInterfaces.Add(new Lf.A2a.V1.AgentInterface
            {
                Url = iface.Url,
                ProtocolBinding = iface.ProtocolBinding,
                ProtocolVersion = iface.ProtocolVersion,
            });
        }
        return p;
    }

    public static AgentCard FromProto(Lf.A2a.V1.AgentCard p)
    {
        var list = p.SupportedInterfaces.Select(ai => new AgentInterface
        {
            Url = ai.Url,
            ProtocolBinding = string.IsNullOrEmpty(ai.ProtocolBinding) ? "JSONRPC" : ai.ProtocolBinding,
            ProtocolVersion = string.IsNullOrEmpty(ai.ProtocolVersion) ? "1.0" : ai.ProtocolVersion,
        }).ToList();
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

    public static Lf.A2a.V1.AgentCapabilities ToProto(AgentCapabilities c) =>
        new()
        {
            Streaming = c.Streaming ?? false,
            PushNotifications = c.PushNotifications ?? false,
        };

    public static AgentCapabilities FromProto(Lf.A2a.V1.AgentCapabilities p) =>
        new()
        {
            Streaming = p.Streaming,
            PushNotifications = p.PushNotifications,
        };

    public static Lf.A2a.V1.AgentSkill ToProto(AgentSkill s)
    {
        var p = new Lf.A2a.V1.AgentSkill
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

    public static AgentSkill FromProto(Lf.A2a.V1.AgentSkill p) =>
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
