using System;
using System.ComponentModel.DataAnnotations;

namespace FarmhouseBakery.Models
{
    public class JobPosting
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        // Full-time, Part-time, Seasonal...
        [Required]
        [StringLength(50)]
        public string JobType { get; set; } = string.Empty;

        // Optional, e.g. "$17/hour"
        [StringLength(100)]
        public string? Pay { get; set; }

        [Required]
        [StringLength(4000)]
        public string Description { get; set; } = string.Empty;

        // Hidden postings stay saved but don't show on the Careers page.
        public bool IsActive { get; set; } = true;

        public DateTime PostedDate { get; set; } = DateTime.UtcNow;
    }
}
