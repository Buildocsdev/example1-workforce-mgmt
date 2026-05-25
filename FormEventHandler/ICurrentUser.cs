namespace FormEventHandler;

public interface ICurrentUser
{
    string? GetUserId();
    string? GetTenant();
    string? GetEmail();
}
