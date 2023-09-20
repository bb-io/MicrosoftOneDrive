using Apps.MicrosoftOneDrive.Dtos;

namespace Apps.MicrosoftOneDrive.Webhooks.Payload;

public class EventPayload
{
    public SubscriptionDto Subscription { get; set; }
    public string DeltaToken { get; set; }
}