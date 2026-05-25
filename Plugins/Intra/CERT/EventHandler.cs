using FormEventHandler;

namespace Intra.CERT;

public class EventHandler : GenericFormHandler
{
    public EventHandler() { }
    public EventHandler(HandlerContext context) : base(context) { }

    public override async Task Form_onAfterSave()
        => cmd.SuccessMessage(Translate("record.submit.success"));
}
