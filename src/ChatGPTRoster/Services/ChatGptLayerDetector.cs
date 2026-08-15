using System.Windows.Automation;

namespace ChatGPTRoster.Services;

internal static class ChatGptLayerDetector
{
    private static readonly string[] BlockingMenuIds =
    [
        "application-menu-trigger-file-menu",
        "application-menu-trigger-edit-menu"
    ];

    public static ChatGptControlHit HitTest(int x, int y)
    {
        try
        {
            var element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            for (var depth = 0; element is not null && depth < 12; depth++)
            {
                var automationId = element.Current.AutomationId;
                if (automationId.Equals(BlockingMenuIds[0], StringComparison.OrdinalIgnoreCase))
                {
                    return ChatGptControlHit.File;
                }

                if (automationId.Equals(BlockingMenuIds[1], StringComparison.OrdinalIgnoreCase))
                {
                    return ChatGptControlHit.Edit;
                }

                if (automationId.Equals("application-menu-trigger-view-menu", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatGptControlHit.View;
                }

                if (automationId.Equals("application-menu-trigger-help-menu", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatGptControlHit.Help;
                }

                if (automationId.StartsWith("radix-", StringComparison.OrdinalIgnoreCase)
                    && element.Current.Name.Equals("Open profile menu", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatGptControlHit.Profile;
                }

                if (element.Current.Name.Equals("Settings", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatGptControlHit.Settings;
                }

                if (element.Current.Name.StartsWith("Back to ", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatGptControlHit.ReturnToMain;
                }

                element = TreeWalker.ControlViewWalker.GetParent(element);
            }
        }
        catch (ElementNotAvailableException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return ChatGptControlHit.None;
    }

}
