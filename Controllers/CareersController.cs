using FarmhouseBakery.Data;
using FarmhouseBakery.Models;
using FarmhouseBakery.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace FarmhouseBakery.Controllers
{
    // Careers page and applying are public; managing jobs and applications is admin-only.
    [Authorize(Roles = "Admin")]
    public class CareersController : Controller
    {
        // A candidate (by email) can apply this many times...
        private const int MaxApplicationsPerEmail = 2;

        // ...within this many days.
        private const int ApplicationLimitDays = 30;

        private const long MaxResumeBytes = 5 * 1024 * 1024;

        private static readonly string[] AllowedResumeExtensions =
            { ".pdf", ".doc", ".docx" };

        private readonly ApplicationDbContext _context;
        private readonly BlobStorageService _blobStorage;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CareersController> _logger;
        private readonly ActivityLogger _activity;

        // Resumes are kept in the PRIVATE "resumes" blob container, so nobody can
        // download them by URL; admins download them through the Resume action.
        public CareersController(
            ApplicationDbContext context,
            BlobStorageService blobStorage,
            EmailService emailService,
            IConfiguration configuration,
            ILogger<CareersController> logger,
            ActivityLogger activity)
        {
            _context = context;
            _blobStorage = blobStorage;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
            _activity = activity;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var jobs = await _context.JobPostings
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.PostedDate)
                .ToListAsync();

            return View(jobs);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var job = await _context.JobPostings.FindAsync(id);

            // Hidden postings can only be previewed by admins.
            if (job == null || (!job.IsActive && !User.IsInRole("Admin")))
            {
                return NotFound();
            }

            return View(new JobDetailsViewModel { Job = job });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        [RequestSizeLimit(MaxResumeBytes + 1024 * 1024)]
        public async Task<IActionResult> Apply(
            int id,
            [Bind(Prefix = "Form")] JobApplicationForm form,
            IFormFile? resume)
        {
            var job = await _context.JobPostings.FindAsync(id);

            if (job == null || !job.IsActive)
            {
                return NotFound();
            }

            // Check the resume file.
            if (resume == null || resume.Length == 0)
            {
                ModelState.AddModelError("resume", "Please attach your resume.");
            }
            else
            {
                var resumeError = await ValidateResume(resume);

                if (resumeError != null)
                {
                    ModelState.AddModelError("resume", resumeError);
                }
            }

            var email = (form.Email ?? "").Trim().ToLowerInvariant();

            // Limit: max 2 applications per email address in the last 30 days.
            if (ModelState.IsValid)
            {
                var since = DateTime.UtcNow.AddDays(-ApplicationLimitDays);

                var recentDates = await _context.JobApplications
                    .Where(x => x.Email == email && x.SubmittedDate >= since)
                    .OrderBy(x => x.SubmittedDate)
                    .Select(x => x.SubmittedDate)
                    .ToListAsync();

                if (recentDates.Count >= MaxApplicationsPerEmail)
                {
                    // The oldest recent application "expires" 30 days after it was sent.
                    var nextAllowed = recentDates[recentDates.Count - MaxApplicationsPerEmail]
                        .AddDays(ApplicationLimitDays)
                        .ToBakeryTime();

                    ModelState.AddModelError("",
                        $"This email address has already applied {MaxApplicationsPerEmail} times in the last " +
                        $"{ApplicationLimitDays} days. You can apply again on {nextAllowed:MMMM d, yyyy}.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View("Details", new JobDetailsViewModel { Job = job, Form = form });
            }

            // Save the resume with a random name in the private "resumes" container.
            var storedName = await _blobStorage.UploadResumeAsync(resume!);

            var application = new JobApplication
            {
                JobPostingId = job.Id,
                FullName = form.FullName.Trim(),
                Email = email,
                Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim(),
                Message = string.IsNullOrWhiteSpace(form.Message) ? null : form.Message.Trim(),
                ResumeFileName = storedName,
                ResumeOriginalName = Path.GetFileName(resume!.FileName),
                SubmittedDate = DateTime.UtcNow
            };

            _context.JobApplications.Add(application);
            await _context.SaveChangesAsync();

            await NotifyBakery(job, application);

            TempData["Applied"] = "Thank you! Your application has been sent. We'll be in touch if your experience is a good fit.";

            return RedirectToAction(nameof(Details), null, new { id = job.Id }, "apply");
        }

        // Returns an error message, or null when the file is a real PDF / Word document.
        private static async Task<string?> ValidateResume(IFormFile resume)
        {
            if (resume.Length > MaxResumeBytes)
            {
                return "Resume must be 5 MB or smaller.";
            }

            var extension = Path.GetExtension(resume.FileName).ToLowerInvariant();

            if (!AllowedResumeExtensions.Contains(extension))
            {
                return "Resume must be a PDF or Word document (.pdf, .doc, .docx).";
            }

            // Check the first bytes so a renamed file can't pass as a document.
            var header = new byte[8];

            using (var stream = resume.OpenReadStream())
            {
                int read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);

                if (read < header.Length)
                {
                    return "The selected file is not a valid resume document.";
                }
            }

            bool isPdf = header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46;   // %PDF
            bool isDocx = header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;  // ZIP (Word .docx)
            bool isDoc = header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0;   // old Word .doc

            bool matches = extension switch
            {
                ".pdf" => isPdf,
                ".docx" => isDocx,
                ".doc" => isDoc,
                _ => false
            };

            return matches ? null : "The selected file is not a valid resume document.";
        }

        // Emails the bakery about a new application. A failed email doesn't stop the application.
        private async Task NotifyBakery(JobPosting job, JobApplication application)
        {
            var bakeryEmail = _configuration["EmailSettings:Email"];

            if (string.IsNullOrWhiteSpace(bakeryEmail))
            {
                return;
            }

            try
            {
                await _emailService.SendEmailAsync(
                    bakeryEmail,
                    $"New job application: {job.Title}",
                    $"A new application was submitted on the Farmhouse Bakery website.\n\n" +
                    $"Job: {job.Title}\n" +
                    $"Name: {application.FullName}\n" +
                    $"Email: {application.Email}\n" +
                    $"Phone: {application.Phone ?? "-"}\n\n" +
                    $"Message:\n{application.Message ?? "-"}\n\n" +
                    $"Log in as admin and open Careers > Applications to download the resume.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send job application notification email.");
            }
        }

        public async Task<IActionResult> Applications(int? jobId)
        {
            var query = _context.JobApplications
                .Include(x => x.JobPosting)
                .AsQueryable();

            if (jobId.HasValue)
            {
                query = query.Where(x => x.JobPostingId == jobId.Value);
            }

            var applications = await query
                .OrderByDescending(x => x.SubmittedDate)
                .ToListAsync();

            ViewData["JobTitle"] = jobId.HasValue
                ? (await _context.JobPostings.FindAsync(jobId.Value))?.Title
                : null;

            return View(applications);
        }

        public async Task<IActionResult> Resume(int id)
        {
            var application = await _context.JobApplications.FindAsync(id);

            if (application == null)
            {
                return NotFound();
            }

            var stream = await _blobStorage.OpenResumeAsync(application.ResumeFileName);

            if (stream == null)
            {
                return NotFound();
            }

            var contentType = Path.GetExtension(application.ResumeFileName) switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/msword"
            };

            // Always download (never display in the browser), using the candidate's file name.
            return File(stream, contentType, application.ResumeOriginalName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteApplication(int id, int? jobId)
        {
            var application = await _context.JobApplications.FindAsync(id);

            if (application == null)
            {
                return NotFound();
            }

            _context.JobApplications.Remove(application);
            await _context.SaveChangesAsync();

            await _blobStorage.DeleteResumeAsync(application.ResumeFileName);
            await _activity.LogAsync(User, "Job application deleted", $"{application.FullName} ({application.Email})");

            TempData["Message"] = "Application deleted.";

            return RedirectToAction(nameof(Applications), new { jobId });
        }

        public async Task<IActionResult> Manage()
        {
            var jobs = await _context.JobPostings
                .OrderByDescending(x => x.PostedDate)
                .ToListAsync();

            return View(jobs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(JobPosting job)
        {
            if (!ModelState.IsValid)
            {
                var jobs = await _context.JobPostings
                    .OrderByDescending(x => x.PostedDate)
                    .ToListAsync();

                return View("Manage", jobs);
            }

            // Saved before SaveChanges, which gives a new posting its Id.
            bool isNew = job.Id == 0;

            if (isNew)
            {
                // ADD
                job.PostedDate = DateTime.UtcNow;
                job.IsActive = true;

                _context.JobPostings.Add(job);
                TempData["Message"] = "Job posting added.";
            }
            else
            {
                // EDIT
                var existingJob = await _context.JobPostings.FindAsync(job.Id);

                if (existingJob == null)
                {
                    return NotFound();
                }

                existingJob.Title = job.Title;
                existingJob.JobType = job.JobType;
                existingJob.Pay = job.Pay;
                existingJob.Description = job.Description;

                TempData["Message"] = "Job posting updated.";
            }

            await _context.SaveChangesAsync();
            await _activity.LogAsync(User, isNew ? "Job posting added" : "Job posting edited", job.Title);

            return RedirectToAction(nameof(Manage));
        }

        // Show or hide a posting on the Careers page without deleting it.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var job = await _context.JobPostings.FindAsync(id);

            if (job == null)
            {
                return NotFound();
            }

            job.IsActive = !job.IsActive;

            await _context.SaveChangesAsync();
            await _activity.LogAsync(User, job.IsActive ? "Job posting shown" : "Job posting hidden", job.Title);

            TempData["Message"] = job.IsActive
                ? $"\"{job.Title}\" is now showing on the Careers page."
                : $"\"{job.Title}\" is now hidden from the Careers page.";

            return RedirectToAction(nameof(Manage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var job = await _context.JobPostings.FindAsync(id);

            if (job == null)
            {
                return NotFound();
            }

            // Its applications are deleted with it (cascade), so remove their resume files too.
            var resumeFiles = await _context.JobApplications
                .Where(x => x.JobPostingId == id)
                .Select(x => x.ResumeFileName)
                .ToListAsync();

            _context.JobPostings.Remove(job);

            await _context.SaveChangesAsync();

            foreach (var resumeFile in resumeFiles)
            {
                await _blobStorage.DeleteResumeAsync(resumeFile);
            }

            await _activity.LogAsync(User, "Job posting deleted",
                $"{job.Title} ({resumeFiles.Count} application(s) removed)");

            TempData["Message"] = "Job posting deleted.";

            return RedirectToAction(nameof(Manage));
        }
    }
}
