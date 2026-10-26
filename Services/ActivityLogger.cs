using System.Security.Claims;
using FarmhouseBakery.Data;
using FarmhouseBakery.Models;

namespace FarmhouseBakery.Services
{
    // Writes a line to the change history (Users > Activity).
    // A failure here never stops the actual change from happening.
    public class ActivityLogger
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ActivityLogger> _logger;

        public ActivityLogger(ApplicationDbContext context, ILogger<ActivityLogger> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAsync(ClaimsPrincipal user, string action, string? details = null)
        {
            try
            {
                _context.ActivityLogs.Add(new ActivityLog
                {
                    Timestamp = DateTime.UtcNow,
                    UserEmail = user.FindFirstValue(ClaimTypes.Email)
                                ?? user.Identity?.Name
                                ?? "unknown",
                    Action = action,
                    Details = details?.Length > 500 ? details[..500] : details
                });

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write activity log entry '{Action}'.", action);
            }
        }
    }
}
