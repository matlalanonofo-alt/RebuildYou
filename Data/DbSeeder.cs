using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RebuildYou.Web.Models;

namespace RebuildYou.Web.Data;

public static class DbSeeder
{
    public static async Task RunAsync(IServiceProvider sp, IConfiguration cfg)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync(); else await db.Database.EnsureCreatedAsync();

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in new[] { "Admin", "User" })
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));

        // First administrator: set AdminSeed:Email and AdminSeed:Password in User Secrets / environment variables
        var email = cfg["AdminSeed:Email"]; var pwd = cfg["AdminSeed:Password"];
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(pwd))
        {
            var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
            if (await um.FindByEmailAsync(email) is null)
            {
                var admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FirstName = "Site", Surname = "Administrator", AcceptedPrivacy = true };
                var res = await um.CreateAsync(admin, pwd);
                if (res.Succeeded) await um.AddToRoleAsync(admin, "Admin");
            }
        }

        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new CareerCategory { Name = "Hospitality", Slug = "hospitality", Icon = "bi-cup-hot", SortOrder = 1, Description = "Hotel, restaurant and catering roles, hospitality trainee positions and learnerships." },
                new CareerCategory { Name = "Computer Literacy & Digital Skills", Slug = "digital-skills", Icon = "bi-laptop", SortOrder = 2, Description = "Digital-skills programmes, data capturing, digital administration and entry-level IT support." },
                new CareerCategory { Name = "Call Centre & Customer Service", Slug = "call-centre", Icon = "bi-headset", SortOrder = 3, Description = "Call centre agents, customer service representatives, contact centre trainees and support roles." },
                new CareerCategory { Name = "Reception & Administration", Slug = "reception-admin", Icon = "bi-journal-text", SortOrder = 4, Description = "Receptionists, office administrators, administrative assistants and office support trainees." },
                new CareerCategory { Name = "General Employment", Slug = "general", Icon = "bi-people", SortOrder = 5, Description = "Entry-level jobs, internships, learnerships and work-integrated learning." });
        }
        if (!await db.Programmes.AnyAsync())
        {
            // Descriptions only. Fees, duration, accreditation etc. stay blank until RebuildYOU confirms them.
            db.Programmes.AddRange(
                new TrainingProgramme { Title = "Hospitality Training", Slug = "hospitality-training", Icon = "bi-cup-hot", SortOrder = 1, RelatedCategorySlug = "hospitality",
                    Summary = "Customer service, professional conduct and workplace readiness for hospitality roles.",
                    Description = "This programme helps you prepare for entry-level work in hotels, restaurants, catering and other hospitality businesses. You learn how to welcome and look after guests, work well in a team and behave professionally at work.",
                    WhoIsItFor = "People who want to start a career in hospitality, including first-time job seekers.",
                    SkillsCovered = "Hospitality-related skills\nCustomer service\nProfessional conduct\nWorkplace readiness",
                    WhatYouGain = "Confidence serving customers\nProfessional behaviour at work\nReadiness for hospitality interviews" },
                new TrainingProgramme { Title = "Computer Literacy & Digital Skills", Slug = "computer-skills", Icon = "bi-laptop", SortOrder = 2, RelatedCategorySlug = "digital-skills",
                    Summary = "Build confidence using computers for study and the workplace.",
                    Description = "Learn to use a computer with confidence. We start with the basics and build up to the everyday tools used in offices: typing documents, working with spreadsheets, and using email and the internet.",
                    WhoIsItFor = "Beginners and anyone who wants to feel more comfortable using computers for work or study.",
                    SkillsCovered = "Basic computer skills\nWord processing\nSpreadsheets\nEmail and internet usage\nDigital workplace skills",
                    WhatYouGain = "Confidence on a computer\nSkills for office and admin work\nA stronger CV" },
                new TrainingProgramme { Title = "Call Centre Training", Slug = "call-centre-training", Icon = "bi-headset", SortOrder = 3, RelatedCategorySlug = "call-centre",
                    Summary = "Telephone communication and customer interaction skills for contact centres.",
                    Description = "Prepares you for customer service and contact centre work. You practise speaking clearly and politely on the phone, handling customer questions and behaving professionally in a busy workplace.",
                    WhoIsItFor = "People interested in call centre or customer service jobs.",
                    SkillsCovered = "Customer service\nTelephone communication\nProfessional communication\nWorkplace conduct",
                    WhatYouGain = "Clear phone communication\nConfidence dealing with customers\nProfessional workplace conduct" },
                new TrainingProgramme { Title = "Reception & Office Administration", Slug = "reception-administration", Icon = "bi-journal-text", SortOrder = 4, RelatedCategorySlug = "reception-admin",
                    Summary = "Front-office operations, record keeping and office administration.",
                    Description = "Learn the skills needed to work at a front desk or in an office: greeting visitors, answering calls, keeping records and supporting a team with day-to-day administration.",
                    WhoIsItFor = "People who want to work as receptionists, administrators or office assistants.",
                    SkillsCovered = "Reception and front-office operations\nOffice administration\nRecord keeping\nComputer skills\nProfessional communication",
                    WhatYouGain = "Front-office and reception skills\nRecord keeping\nOffice computer skills" },
                new TrainingProgramme { Title = "Workplace Readiness", Slug = "workplace-readiness", Icon = "bi-briefcase", SortOrder = 5, RelatedCategorySlug = "general",
                    Summary = "CV development, interview preparation and job-application support.",
                    Description = "Everything you need to get ready to apply for work: building a CV, preparing for interviews, applying for jobs and knowing what employers expect.",
                    WhoIsItFor = "Anyone looking for work or their first job.",
                    SkillsCovered = "CV preparation\nInterview preparation\nJob-application support\nProfessional communication\nWorkplace expectations\nCareer guidance",
                    WhatYouGain = "A CV you are proud of\nInterview confidence\nUnderstanding of workplace expectations" });
        }
        await db.SaveChangesAsync();

    }
}
