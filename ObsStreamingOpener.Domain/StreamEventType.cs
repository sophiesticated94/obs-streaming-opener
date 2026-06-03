namespace ObsStreamingOpener.Domain;

public enum StreamEventType
{
    ChatMessage = 1,
    Tip = 2,
    AudienceRelationshipStarted = 3,
    AudienceRelationshipEnded = 4,
    AudienceRelationshipRenewed = 5,
    ViewerMilestone = 6,
    System = 100
}
