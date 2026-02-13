using Microsoft.AspNetCore.SignalR;
using Morty.Web.DTOs;

namespace Morty.Web.Hubs;

public class MortyHub : Hub
{
    public async Task JoinProject(int projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"project-{projectId}");
    }

    public async Task LeaveProject(int projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project-{projectId}");
    }

    // Server-side methods to broadcast updates
    public static class Broadcaster
    {
        private static IHubContext<MortyHub>? _hubContext;

        public static void SetHubContext(IHubContext<MortyHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public static async Task NotifyStoryUpdated(StoryDto story)
        {
            if (_hubContext == null) return;
            await _hubContext.Clients.Group($"project-{story.ProjectId}").SendAsync("OnStoryUpdated", story);
        }

        public static async Task NotifyIterationComplete(IterationDto iteration)
        {
            if (_hubContext == null) return;
            await _hubContext.Clients.All.SendAsync("OnIterationComplete", iteration);
        }

        public static async Task NotifyProjectStatsUpdated(int projectId, object stats)
        {
            if (_hubContext == null) return;
            await _hubContext.Clients.Group($"project-{projectId}").SendAsync("OnProjectStatsUpdated", stats);
        }
    }
}
