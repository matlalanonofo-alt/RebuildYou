using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace RebuildYou.Web.Models;

public class ApplicationUser : IdentityUser
{
    [MaxLength(80)] public string FirstName { get; set; } = "";
    [MaxLength(80)] public string Surname { get; set; } = "";
    [MaxLength(100)] public string? City { get; set; }
    [MaxLength(80)] public string? EducationLevel { get; set; }
    [MaxLength(200)] public string? CareerInterests { get; set; }
    [MaxLength(80)] public string? EmploymentStatus { get; set; }
    [MaxLength(200)] public string? TrainingInterests { get; set; }
    public bool AcceptedPrivacy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public int LoginCount { get; set; }
    [NotMapped] public string FullName => $"{FirstName} {Surname}";
}

public class CareerCategory
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [Required, MaxLength(100)] public string Slug { get; set; } = "";
    [Required, MaxLength(600)] public string Description { get; set; } = "";
    [MaxLength(60)] public string Icon { get; set; } = "bi-briefcase";
    public int SortOrder { get; set; }
    public List<Opportunity> Opportunities { get; set; } = new();
}

public enum OpportunityType { Permanent, Temporary, PartTime, Contract, Learnership, Internship, WorkIntegratedLearning, TrainingProgramme, Volunteering }
public enum ListingStatus { Draft, Published, Closed, Archived }
public enum ApplicationStatus { Submitted, UnderReview, Shortlisted, InterviewScheduled, Unsuccessful, Withdrawn }

public class Opportunity
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [Required, MaxLength(160), Display(Name = "Employer")] public string EmployerName { get; set; } = "";
    [Display(Name = "Career category")] public int CareerCategoryId { get; set; }
    public CareerCategory? Category { get; set; }
    [Required, MaxLength(120)] public string Location { get; set; } = "";
    public OpportunityType Type { get; set; }
    [MaxLength(100), Display(Name = "Salary / stipend (ZAR)")] public string? Salary { get; set; }
    [MaxLength(300)] public string? Qualifications { get; set; }
    [MaxLength(200)] public string? Experience { get; set; }
    [Required, MaxLength(600), Display(Name = "Short description")] public string ShortDescription { get; set; } = "";
    public string? Responsibilities { get; set; }
    [Display(Name = "Required skills"), MaxLength(500)] public string? Skills { get; set; }
    [Display(Name = "Application instructions")] public string? ApplicationInstructions { get; set; }
    [Display(Name = "Documents required"), MaxLength(300)] public string? DocumentsRequired { get; set; }
    [DataType(DataType.Date)] public DateTime Deadline { get; set; } = DateTime.Today.AddDays(30);
    public DateTime DatePublished { get; set; } = DateTime.UtcNow;
    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    [Display(Name = "External application URL"), MaxLength(400), Url] public string? ExternalUrl { get; set; }
    public bool IsDemo { get; set; }
    [NotMapped] public bool IsOpen => Status == ListingStatus.Published && Deadline.Date > DateTime.Today;
}

public class JobApplication
{
    public int Id { get; set; }
    public int OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }
    [Required] public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    public int? CvDocumentId { get; set; }
    public CvDocument? Cv { get; set; }
    [MaxLength(1500)] public string? CoverNote { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? InterviewDate { get; set; }
    [MaxLength(2000)] public string? InternalNotes { get; set; }
}

public class CvDocument
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    [MaxLength(255)] public string OriginalFileName { get; set; } = "";
    [MaxLength(80)] public string StoredFileName { get; set; } = "";
    [MaxLength(100)] public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class SavedOpportunity
{
    public string UserId { get; set; } = "";
    public int OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}

public class TrainingProgramme
{
    public int Id { get; set; }
    [Required, MaxLength(140)] public string Title { get; set; } = "";
    [Required, MaxLength(140)] public string Slug { get; set; } = "";
    [Required, MaxLength(300)] public string Summary { get; set; } = "";
    public string? Description { get; set; }
    [Display(Name = "Skills covered (one per line)")] public string? SkillsCovered { get; set; }
    [Display(Name = "Entry requirements"), MaxLength(400)] public string? EntryRequirements { get; set; }
    [MaxLength(100)] public string? Duration { get; set; }
    [Display(Name = "Delivery method"), MaxLength(100)] public string? DeliveryMethod { get; set; }
    [Display(Name = "Start dates"), MaxLength(200)] public string? StartDates { get; set; }
    [Display(Name = "Fees"), MaxLength(100)] public string? Fees { get; set; }
    [Display(Name = "Accreditation"), MaxLength(300)] public string? Accreditation { get; set; }
    [MaxLength(60)] public string Icon { get; set; } = "bi-mortarboard";
    [Display(Name = "Who is it for?"), MaxLength(400)] public string? WhoIsItFor { get; set; }
    [Display(Name = "What you will gain (one per line)")] public string? WhatYouGain { get; set; }
    [Display(Name = "Related career category (slug, e.g. hospitality)"), MaxLength(100)] public string? RelatedCategorySlug { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool IsArchived { get; set; }
}

public class ContactEnquiry
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(200)] public string Email { get; set; } = "";
    [MaxLength(200)] public string Subject { get; set; } = "";
    [MaxLength(3000)] public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsHandled { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    public string? ActorId { get; set; }
    [MaxLength(120)] public string Action { get; set; } = "";
    [MaxLength(600)] public string? Details { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class ProgrammeInterest
{
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    public int ProgrammeId { get; set; }
    public TrainingProgramme? Programme { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class LoginRecord
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    [MaxLength(200)] public string Email { get; set; } = "";
    [MaxLength(40)] public string Event { get; set; } = "";   // Registered, Login, Failed login, Locked out
    [MaxLength(60)] public string? Ip { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
