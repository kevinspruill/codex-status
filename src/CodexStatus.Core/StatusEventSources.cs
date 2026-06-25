namespace CodexStatus.Core;

public interface IStatusEventSource
{
    string BackendName { get; }
}

public sealed class HookStatusEventSource : IStatusEventSource
{
    public string BackendName => "hooks";
}

public sealed class ExecJsonStatusEventSource : IStatusEventSource
{
    public string BackendName => "exec-json";
}

public sealed class AppServerStatusEventSource : IStatusEventSource
{
    public string BackendName => "app-server";
}
