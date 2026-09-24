using Domio.Application.Notifications;

namespace Domio.Web.Models.Notifications;

public sealed class NotificationCenterViewModel
{
    public required NotificationCenterOverview Overview
    {
        get;
        init;
    }

    public string SelectedTab
    {
        get;
        init;
    } = "inbox";
}
