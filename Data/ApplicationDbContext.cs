using FarmhouseBakery.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FarmhouseBakery.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<MenuItem> MenuItems { get; set; }

        public DbSet<JobPosting> JobPostings { get; set; }

        public DbSet<JobApplication> JobApplications { get; set; }

        public DbSet<ActivityLog> ActivityLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Used to count applications per email for the 30-day limit.
            builder.Entity<JobApplication>()
                .HasIndex(x => new { x.Email, x.SubmittedDate });

            // The Activity page shows the newest entries first.
            builder.Entity<ActivityLog>()
                .HasIndex(x => x.Timestamp);
        }
    }
}