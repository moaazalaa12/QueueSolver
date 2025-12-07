using Microsoft.AspNetCore.SignalR;

namespace QueueSolver.Web.Hubs
{
    public class SimulationHub : Hub
    {
        public async Task SendProgress(object snapshot)
        {
            await Clients.All.SendAsync("ReceiveProgress", snapshot);
        }
    }
}
