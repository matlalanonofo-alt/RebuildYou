using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
namespace RebuildYou.Web.Models;

public static class Lists
{
    public static readonly string[] Education = { "Below Grade 12", "Grade 12 / Matric", "Certificate / Diploma", "Degree", "Postgraduate" };
    public static readonly string[] Employment = { "Unemployed", "Employed", "Self-employed", "Student", "Other" };
}

public class RegisterVM
{
    [Required, MaxLength(80), Display(Name = "First name")] public string FirstName { get; set; } = "";
    [Required, MaxLength(80)] public string Surname { get; set; } = "";
    [Required, EmailAddress, Display(Name = "Email address")] public string Email { get; set; } = "";
    [Required, Phone, Display(Name = "Mobile number")] public string Mobile { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    public bool AcceptPrivacy { get; set; }
}

public class LoginVM
{
    [Required, EmailAddress, Display(Name = "Email address")] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    [Display(Name = "Keep me signed in")] public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class ProfileVM
{
    [Required, MaxLength(80), Display(Name = "First name")] public string FirstName { get; set; } = "";
    [Required, MaxLength(80)] public string Surname { get; set; } = "";
    [Required, Phone, Display(Name = "Mobile number")] public string Mobile { get; set; } = "";
    [MaxLength(100), Display(Name = "Area or city")] public string? City { get; set; }
    [Display(Name = "Highest education level")] public string? EducationLevel { get; set; }
    [Display(Name = "Employment status")] public string? EmploymentStatus { get; set; }
    [Display(Name = "Career interests")] public string? CareerInterests { get; set; }
    [Display(Name = "Training interests")] public string? TrainingInterests { get; set; }
}

public class ChangePasswordVM
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")] public string Current { get; set; } = "";
    [Required, DataType(DataType.Password), Display(Name = "New password")] public string New { get; set; } = "";
    [Required, DataType(DataType.Password), Compare("New", ErrorMessage = "Passwords do not match."), Display(Name = "Confirm new password")] public string Confirm { get; set; } = "";
}

public class ContactVM
{
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [Required, EmailAddress, MaxLength(200), Display(Name = "Email address")] public string Email { get; set; } = "";
    [Required, MaxLength(200)] public string Subject { get; set; } = "";
    [Required, MaxLength(3000)] public string Message { get; set; } = "";
    public string? Website { get; set; } // honeypot - must stay empty
}

public class HomeVM
{
    public List<TrainingProgramme> Programmes { get; set; } = new();
    public List<CareerCategory> Categories { get; set; } = new();
    public List<Opportunity> Latest { get; set; } = new();
}

public class OppListVM
{
    public List<Opportunity> Items { get; set; } = new();
    public List<CareerCategory> Categories { get; set; } = new();
    public string? Q { get; set; }
    public int? CategoryId { get; set; }
    public string? Location { get; set; }
    public OpportunityType? Type { get; set; }
    public bool IncludeClosed { get; set; }
    public int Page { get; set; } = 1;
    public int Pages { get; set; } = 1;
}

public class DashboardVM
{
    public ApplicationUser User { get; set; } = null!;
    public int Completion { get; set; }
    public CvDocument? Cv { get; set; }
    public List<JobApplication> Applications { get; set; } = new();
    public List<Opportunity> Saved { get; set; } = new();
    public List<Opportunity> Recommended { get; set; } = new();
    public List<TrainingProgramme> Programmes { get; set; } = new();
}

public class ApplyVM
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever, Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever] public Opportunity Opportunity { get; set; } = null!;
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever, Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever] public List<CvDocument> Cvs { get; set; } = new();
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever, Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever] public ApplicationUser User { get; set; } = null!;
    public int? CvId { get; set; }
    [MaxLength(1500)] public string? CoverNote { get; set; }
    public bool Confirm { get; set; }
    public IFormFile? MatricResultsFile { get; set; }
    public IFormFile? IdentityDocumentFile { get; set; }
    public IFormFile? ProofOfAddressFile { get; set; }
    public IFormFile? CertificatesFile { get; set; }
    public IFormFile? AdditionalDocumentFile { get; set; }
    public List<ApplicationFileVM> Documents { get; set; } = new();
}

public class ApplicationFileVM
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
}

public class AdminStatsVM
{
    public int Users, ActiveUsers, Cvs, Published, Open, Expired, Applications, AwaitingReview, Shortlisted, Programmes, Enquiries, LoginsToday;
    public List<(string Name, int Count)> ByCategory { get; set; } = new();
    public List<(string Name, int Count)> ByStatus { get; set; } = new();
    public List<AuditLog> Recent { get; set; } = new();
}

public class UserDetailVM
{
    public ApplicationUser User { get; set; } = null!;
    public bool IsAdmin { get; set; }
    public List<CvDocument> Cvs { get; set; } = new();
    public List<JobApplication> Applications { get; set; } = new();
    public List<ProgrammeInterest> Interests { get; set; } = new();
    public List<LoginRecord> Logins { get; set; } = new();
}
