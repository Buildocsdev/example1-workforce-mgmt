using Newtonsoft.Json.Linq;

namespace FormEventHandler;

// Default form handler — inherit this in your plugin's EventHandler classes.
// Override only the events you care about; everything else is handled here or is a no-op.
public class GenericFormHandler : AbstractHandler
{
    public GenericFormHandler() { }
    public GenericFormHandler(HandlerContext context) : base(context) { }

    public override async Task Form_onInit(bool loadFromDb)
    {
        if (loadFromDb)
        {
            await record.LoadRecord();
        }
        else
        {
            record.SetRecordGuid((string?)contextHandlerInstance.Get("guid"));
            record.SetProjectGuid((string?)contextHandlerInstance.Get("projectGuid"));
            if (contextHandlerInstance.Get("formData") is Dictionary<string, object> formData)
                record.SetData(formData);
        }
    }

    public override async Task Form_onCancel()
        => cmd.Event("Cancel");

    public override async Task Form_onAfterDelete()
        => cmd.NotificationMessage(Translate("record.deleted.success"));

    // Called for each row action in a data table widget.
    // action values come from the UI (e.g. "edit", "delete").
    public override async Task Form_onTableRunActionEvent(string action, RowData rowData)
    {
        if (action == "delete")
        {
            DataRecord deleteRecord = new DataRecord();
            deleteRecord.Configure(_dynamoClient!, _dbTableName!, rowData._plugincode, rowData._code, rowData._sk ?? rowData._id);
            await deleteRecord.Delete();

            await Form_onInit(false);

            cmd.NotificationMessage(Translate("record.deleted.success"));
            cmd.SetRedrawScreen();
        }
        else if (action == "edit")
        {
            cmd.ShowRecord(rowData._plugincode, rowData._code, rowData._sk ?? rowData._id, false);
        }
    }

    // Called for batch (multi-row) actions.
    public override async Task Form_onTableRunBatchActionEvent(string action, object[] data)
    {
        if (action == "delete")
        {
            foreach (var item in data)
            {
                BatchRowData? row = item is JObject jObj
                    ? jObj.ToObject<BatchRowData>()
                    : null;

                if (row == null) continue;

                DataRecord deleteRecord = new DataRecord();
                deleteRecord.Configure(_dynamoClient!, _dbTableName!, row._plugincode, row._code, row._sk);
                await deleteRecord.Delete();
            }

            cmd.NotificationMessage(Translate("record.deleted.success"));
            cmd.SetRedrawScreen();
        }
    }    
}
