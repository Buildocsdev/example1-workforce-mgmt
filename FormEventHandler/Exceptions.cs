namespace FormEventHandler;

public class ActionTerminationException : Exception
{
    public ActionTerminationException() { }
    public ActionTerminationException(string message) : base(message) { }
}

public class UserWarningException : Exception
{
    public UserWarningException(string message) : base(message) { }
}

public class RecordNotFoundException : Exception
{
    public RecordNotFoundException(string message) : base(message) { }
}
