namespace ReturnLoad.Domain.Administration;

/// <summary>Delivery channel for a notification (§12 domain map; provider chosen later).</summary>
public enum NotificationChannel
{
    Push = 0,
    Sms = 1,
    Email = 2,
    InApp = 3,
}

/// <summary>Delivery lifecycle of a notification.</summary>
public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Read = 3,
}
