using System.ComponentModel.DataAnnotations;

namespace FarmhouseBakery.Models
{
    // What the candidate types into the Apply form.
    public class JobApplicationForm
    {
        [Required(ErrorMessage = "Please enter your full name.")]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Phone]
        [StringLength(30)]
        [Display(Name = "Phone (optional)")]
        public string? Phone { get; set; }

        [StringLength(2000)]
        [Display(Name = "Message (optional)")]
        public string? Message { get; set; }
    }

    // Everything the job Details page needs: the job and the Apply form.
    public class JobDetailsViewModel
    {
        public JobPosting Job { get; set; } = new JobPosting();

        public JobApplicationForm Form { get; set; } = new JobApplicationForm();
    }
}
