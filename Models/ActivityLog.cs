using System;
using System.ComponentModel.DataAnnotations;

namespace FarmhouseBakery.Models
{
    // One line in the change history: who did what, and when.
    public class ActivityLog
    {
        public int Id { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(256)]
        public string UserEmail { get; set; } = string.Empty;

        // e.g. "Menu item added", "Job posting deleted"
        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        // e.g. the item name or job title
        [StringLength(500)]
        public string? Details { get; set; }
    }
}
