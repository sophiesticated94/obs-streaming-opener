namespace ObsStreamingOpener.Domain;

public enum StreamEventType
{
    ChatMessage = 1,
    Tip = 2,
    AudienceRelationshipStarted = 3,
    AudienceRelationshipEnded = 4,
    AudienceRelationshipRenewed = 5,
    ViewerMilestone = 6,
    ContentPublished = 7,
    LiveBroadcastScheduled = 8,
    LiveBroadcastStarted = 9,
    LiveBroadcastEnded = 10,
    CommentCreated = 11,
    System = 100
}
