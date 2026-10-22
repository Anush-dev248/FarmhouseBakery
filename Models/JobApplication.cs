using System;
using System.ComponentModel.DataAnnotations;

namespace FarmhouseBakery.Models
{
    // A candidate's application for a job posting (saved in the database).
    public class JobApplication
    {
        public int Id { get; set; }

        public int JobPostingId { get; set; }

        public JobPosting? JobPosting { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        // Stored lower-case so the "2 per 30 days" limit can't be dodged with capitals.
        [Required]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [StringLength(30)]
        public string? Phone { get; set; }

        [StringLength(2000)]
        public string? Message { get; set; }

        // Blob name (random GUID name) in the private "resumes" blob container.
        [Required]
        [StringLength(100)]
        public string ResumeFileName { get; set; } = string.Empty;

        // The candidate's original file name, used when an admin downloads it.
        [Required]
        [StringLength(255)]
        public string ResumeOriginalName { get; set; } = string.Empty;

        public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;
    }
}
