using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RebuildYou.Web.Models;

namespace RebuildYou.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<CareerCategory> Categories => Set<CareerCategory>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<CvDocument> CvDocuments => Set<CvDocument>();
    public DbSet<SavedOpportunity> SavedOpportunities => Set<SavedOpportunity>();
    public DbSet<TrainingProgramme> Programmes => Set<TrainingProgramme>();
    public DbSet<ContactEnquiry> Enquiries => Set<ContactEnquiry>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<LoginRecord> LoginRecords => Set<LoginRecord>();
    public DbSet<ProgrammeInterest> ProgrammeInterests => Set<ProgrammeInterest>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        base.OnModelCreating(m);
        m.Entity<ApplicationUser>().HasIndex(u => u.PhoneNumber).IsUnique().HasFilter("[PhoneNumber] IS NOT NULL"); // one mobile number = one profile
        m.Entity<LoginRecord>().HasIndex(l => l.At);
        m.Entity<ProgrammeInterest>().HasKey(i => new { i.UserId, i.ProgrammeId });
        m.Entity<CareerCategory>().HasIndex(c => c.Slug).IsUnique();
        m.Entity<TrainingProgramme>().HasIndex(p => p.Slug).IsUnique();
        m.Entity<Opportunity>().HasIndex(o => new { o.Status, o.Deadline });
        m.Entity<Opportunity>().HasOne(o => o.Category).WithMany(c => c.Opportunities).HasForeignKey(o => o.CareerCategoryId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<JobApplication>().HasIndex(a => new { a.OpportunityId, a.UserId }).IsUnique();
        m.Entity<JobApplication>().HasOne(a => a.Opportunity).WithMany().HasForeignKey(a => a.OpportunityId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<JobApplication>().HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<JobApplication>().HasOne(a => a.Cv).WithMany().HasForeignKey(a => a.CvDocumentId).OnDelete(DeleteBehavior.SetNull);
        m.Entity<CvDocument>().HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SavedOpportunity>().HasKey(s => new { s.UserId, s.OpportunityId });
        m.Entity<SavedOpportunity>().HasOne(s => s.Opportunity).WithMany().HasForeignKey(s => s.OpportunityId).OnDelete(DeleteBehavior.Cascade);
    }
}
