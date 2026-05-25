namespace FormEventHandler;

/// <summary>
/// Named subclass of <see cref="Cmd"/> used by <see cref="RequestContext"/>.
/// Having a distinct type lets the host platform inject or replace the command
/// provider via DI without coupling to <see cref="Cmd"/> directly.
/// </summary>
public class FECommandProvider : Cmd { }
