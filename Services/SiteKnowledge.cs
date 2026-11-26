using Microsoft.AspNetCore.Mvc;

namespace RebuildYou.Web.Services;

public record FaqLink(string Text, string Url);

public record FaqEntry(string Id, string Question, string Answer, string[] Keywords, FaqLink[] Links);

/// <summary>
/// Single source of truth for the About-page FAQ, the chatbot and the FAQ part of the site search.
/// Every answer is taken from content that already exists on this website. Where RebuildYOU has not
/// published a fact yet (fees, start dates, opening hours), the answer says so and points to the team.
///
/// To add or change an answer, edit the E(...) lines in Build():
///   E(id, question, answer, keywords, links...)
///   - id        short unique name, letters and dashes only
///   - keywords  words or phrases people might type, separated by |  (all lower case)
/// </summary>
public static class SiteKnowledge
{
    public static List<FaqEntry> Build(IConfiguration cfg, IUrlHelper url)
    {
        FaqEntry E(string id, string question, string answer, string keywords, params FaqLink[] links) =>
            new(id, question, answer, keywords.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), links);

        FaqLink Lk(string text, string action, string controller, object? values = null) =>
            new(text, url.Action(action, controller, values) ?? "/");

        // Contact details come from appsettings.json (Site section), so they are never typed twice.
        var phone = cfg["Site:Phone"] ?? "";
        var phoneDial = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        var email = cfg["Site:Email"] ?? "";
        var address = cfg["Site:Address"] ?? "";
        var whatsapp = cfg["Site:WhatsApp"] ?? "";

        var contactLinks = new List<FaqLink>();
        if (phoneDial.Length > 0) contactLinks.Add(new FaqLink("Call " + phone, "tel:" + phoneDial));
        if (whatsapp.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) contactLinks.Add(new FaqLink("WhatsApp us", whatsapp));
        if (email.Length > 0) contactLinks.Add(new FaqLink("Email us", "mailto:" + email));
        contactLinks.Add(Lk("Contact form", "Contact", "Home"));

        return new List<FaqEntry>
        {
            E("what-is", "What is RebuildYOU Training Centre?",
              "Rebuild You Skills Academy (Pty) Ltd, trading as Rebuild You Training Centre, is a South African skills development and training organisation. We equip people with practical, industry-relevant skills, professional conduct and the confidence to step into the workplace.",
              "about|about us|who are you|what is rebuildyou|what is rebuild you|who is rebuildyou|tell me about rebuildyou|tell me about the centre|training centre|training center|company|organisation|organization|what do you do",
              Lk("About us", "About", "Home")),

            E("services", "What services do you offer?",
              "We offer corporate training (customer service, call centre, workplace communication and professional conduct), skills development (computer skills, administration and reception, hospitality), employment readiness (CV development, interview preparation and job-application support) and consulting (workforce development strategy, training design and skills gap analysis).",
              "what services|your services|services do you|offer|offering|provide|consulting|consultancy|corporate|corporate training|skills development|workforce|skills gap|training design|what do you provide",
              Lk("Training programmes", "Index", "Training"), Lk("For employers", "Employers", "Home")),

            E("programmes", "What training programmes do you offer?",
              "Our training is practical and skills-based. Areas include customer service and call centre training, computer literacy and digital skills, reception and office administration, hospitality, and workplace readiness (CV, interview and job-application support). The Training page lists the current programmes, and each programme page explains what it covers.",
              "programme|course|learn|study|class|curriculum|hospitality|computer|computers|computer skills|digital skills|call centre|call center|reception|receptionist|administration|admin|office administration|customer service|what can i study|what can i learn|training programme|training programmes|which programmes|list of programmes",
              Lk("Training programmes", "Index", "Training")),

            E("fees", "How much do programmes cost?",
              "Programme fees are not published on the website yet. Training fees, where applicable, are confirmed by RebuildYOU, so please contact us for the latest information before you plan around a cost.",
              "fee|cost|price|pricing|payment|afford|affordable|expensive|cheap|charge|tuition|how much|do i pay|have to pay|need to pay|must i pay|free of charge|free training|free course|free programme|programme free|training free|course free|is the training free|are the programmes free|are the courses free",
              Lk("Contact us", "Contact", "Home"), Lk("Training programmes", "Index", "Training")),

            E("dates", "When do programmes start, how long do they run and how are they delivered?",
              "Start dates, durations and delivery details are shared as RebuildYOU confirms them, so they may not be listed yet. Open a programme on the Training page and register your interest (you will need a free account), and the team can use your profile details to contact you about dates. You can also contact us directly.",
              "start|starts|starting|begin|begins|date|dates|duration|how long|intake|next intake|next class|schedule|timetable|register interest|register my interest|my interest|interested in|enrol|enroll|enrolment|enrollment|join a course|join a programme|book a course|online|course online|online course|study online|learn online|in person|classroom|venue|delivery method|how is it delivered|attend|attendance|distance learning|virtual|face to face",
              Lk("Training programmes", "Index", "Training"), Lk("Contact us", "Contact", "Home")),

            E("requirements", "What are the entry requirements?",
              "Entry requirements are shown on each programme page as RebuildYOU confirms them. If a programme does not list them yet, please contact us before you apply.",
              "requirement|requirements|entry requirements|eligible|eligibility|qualify|matric|grade 12|age limit|prerequisite|prerequisites|who can join|who can apply|who can enrol|who can enroll|who is it for|who may apply",
              Lk("Training programmes", "Index", "Training"), Lk("Contact us", "Contact", "Home")),

            E("accreditation", "Do I get a certificate, and are the programmes accredited?",
              "Training is currently delivered as skills-based programmes with certificates of completion. RebuildYOU is working towards accreditation with relevant bodies and will publish accreditation details on the About page once confirmed.",
              "certificate|certificates|certified|certification|accredited|accreditation|accredit|nqf|saqa|seta|recognised|recognized|credits|proof of completion|certificate of completion",
              Lk("About us", "About", "Home")),

            E("free-account", "Is it free to create an account?",
              "Yes. Creating an account on this website does not require payment. Training fees, where applicable, are confirmed by RebuildYOU.",
              "free account|account free|create account|create an account|make an account|open an account|free to register|register free|sign up|signup|register|registration|free profile|create profile|create my profile|profile|membership|new account|join the website|is it free",
              Lk("Create my free profile", "Register", "Account"), Lk("Log in", "Login", "Account")),

            E("no-guarantee", "Does applying guarantee me a job or a place on a programme?",
              "No. Applications are reviewed by the employer or RebuildYOU, and applying through this website does not guarantee employment or a place on a programme.",
              "guarantee|guaranteed|guarantee a job|will i get a job|job guaranteed|placement|placed|promise|assured|chances of getting|will you find me a job|find me a job|get me a job|get me work",
              Lk("Opportunities", "Index", "Opportunities")),

            E("cv-privacy", "Who can see my CV and personal information?",
              "Only you and authorised RebuildYOU staff who need the information to run the service can see your CV, and administrator access to CVs is logged. If you apply to an employer, your application details may be shared with that employer only for that purpose. Passwords are stored as one-way hashes, and CVs are kept in private storage that cannot be reached by a web address. The Privacy Notice has the full details.",
              "who can see|see my cv|see my information|privacy|private|confidential|secure|security|safe|popia|personal information|data protection|my data|my details|is my cv safe|is my information safe|privacy notice|information officer|hash|hashed",
              Lk("Privacy Notice", "Privacy", "Home")),

            E("cv-upload", "Which file types can I upload for my CV?",
              "You can upload a PDF or DOCX file of up to 5 MB. You can delete your CV at any time from your dashboard.",
              "file type|file types|file format|file formats|format|pdf|docx|word document|upload|uploading|file size|5 mb|mb|megabyte|how big|too big|attach cv|attach my cv|upload cv|upload my cv|delete cv|delete my cv|remove cv|cv upload|upload a cv",
              Lk("My CV", "Cv", "Dashboard")),

            E("how-it-works", "How does the platform work?",
              "It takes four steps: 1) create your free account, 2) upload your CV, 3) browse opportunities and training programmes, 4) apply and track your application status. You make one profile and use it for everything.",
              "how does it work|how does this work|how does the platform work|how does the website work|how it works|platform|steps|process|get started|getting started|where do i start|how do i start|how to start|how do i use|how to use|use this site|use the site|use the website|guide|navigate|new here|first time",
              Lk("Create my free profile", "Register", "Account"), Lk("Opportunities", "Index", "Opportunities")),

            E("apply", "How do I apply for an opportunity?",
              "Create a free account, upload your CV, then open an opportunity and follow its application instructions. Each listing shows how to apply, and you can track your applications from your dashboard.",
              "apply|applying|application|applications|how do i apply|how to apply|apply for a job|apply for job|apply online|submit|submit cv|send cv|send my cv|cover note|cover letter|application form|apply for an opportunity",
              Lk("Opportunities", "Index", "Opportunities"), Lk("My applications", "MyApplications", "Dashboard")),

            E("track-application", "How do I track my application?",
              "After you apply, you can follow each application from your dashboard under My Applications. The status moves through Submitted, Under review, Shortlisted, Interview scheduled, Unsuccessful or Withdrawn.",
              "track|tracking|status|application status|check my application|follow my application|progress|updates|heard back|feedback|outcome|shortlisted|unsuccessful|withdraw|withdrawn|under review|interview scheduled|my applications|where is my application|waiting for a response|no response",
              Lk("My applications", "MyApplications", "Dashboard")),

            E("opportunity-types", "What kinds of opportunities can I find?",
              "Listings can be permanent, temporary, part-time or contract jobs, learnerships, internships, work-integrated learning, training programmes and volunteering. Each listing shows the employer, location, deadline and how to apply. Closed listings are hidden unless you choose to include them.",
              "opportunity|opportunities|job|jobs|vacancy|vacancies|learnership|learnerships|internship|internships|intern|work integrated learning|volunteer|volunteering|part time|contract|temporary|permanent|employment|career|careers|position|positions|entry level|first job|find work|find a job|looking for work|looking for a job|job search|search for jobs|what jobs",
              Lk("Opportunities", "Index", "Opportunities"), Lk("Career categories", "Index", "Careers")),

            E("cv-help", "Can you help me with my CV and interview preparation?",
              "Yes. The Get Work-Ready page has free, practical tips on writing a CV, preparing for an interview, your first weeks at work and staying motivated. The Workplace Readiness programme also covers CV preparation, interview preparation, job-application support, professional communication, workplace expectations and career guidance.",
              "cv|resume|curriculum vitae|cv help|write a cv|write my cv|make a cv|build a cv|improve my cv|interview|interviews|interview tips|interview preparation|prepare for an interview|work ready|get work ready|workplace readiness|career guidance|career advice|advice|tips|motivation|motivated|discouraged|stuck|rejection|job application support",
              Lk("Get work-ready tips", "Index", "Resources"), Lk("Workplace readiness programme", "Details", "Training", new { slug = "workplace-readiness" })),

            E("employers", "I am an employer. How can you help me?",
              "Employers can share a vacancy, learnership or internship, ask us to help source and screen candidates, or enquire about workplace training for their team. Send an employer enquiry and we will get back to you. We only share application details with you for the opportunity the person applied for.",
              "employer|employers|hire|hiring|recruit|recruiting|recruitment|recruiter|employees|post a job|post a vacancy|advertise|advertise a job|list a job|we are a company|our company|my company|team training|train my staff|train our staff|staff training|source candidates|screen candidates|partner|partnership|partnerships|corporate clients",
              Lk("Send an employer enquiry", "Contact", "Home", new { subject = "Employer enquiry" }), Lk("For employers", "Employers", "Home")),

            E("contact", "Where are you located and how can I contact you?",
              $"You can find us at {address}, South Africa. Phone: {phone}. Email: {email}. You can also WhatsApp us or send a message through the contact form.",
              "contact|contacts|contact details|contact you|contact us|phone|call us|call you|telephone|phone number|contact number|cell|mobile number|email|e-mail|email address|address|location|located|where are you|where is|find you|directions|visit|come to|walk in|office|whatsapp|whats app|message you|get in touch|reach you|reach out|enquiry|enquire|enquiries|inquiry|ask a question|davidsonville|roodepoort|johannesburg|gauteng",
              contactLinks.ToArray()),

            E("hours", "What are your opening hours?",
              "Opening hours are not listed on the website yet. Please phone or WhatsApp the team to confirm when someone is available before you visit.",
              "opening hours|open hours|hours|office hours|business hours|what time|time do you open|when are you open|are you open|open on|open today|weekend|saturday|sunday|monday|friday|after hours|trading hours|when do you close|what time do you close|closing time",
              contactLinks.ToArray()),

            E("mission", "What is your mission and vision, and how do you teach?",
              "Our vision is to be a recognised leader in practical skills development and workforce readiness training in South Africa. Our mission is to design and deliver high-quality, practical and industry-aligned training that equips individuals and organisations with the competencies needed for workplace success. Programmes simulate real workplace environments so learners gain confidence and competence. Our values are excellence, integrity, empowerment, impact and innovation.",
              "mission|vision|values|goal|goals|purpose|stand for|why do you exist|our story|story|history|founded|established|why rebuildyou|approach|how do you teach|hands on|real workplace|simulate",
              Lk("About us", "About", "Home")),

            E("leadership", "Who runs RebuildYOU?",
              "Lerease Sam is the Chief Executive Officer and Founder, responsible for strategic direction, partnerships, programme development and business growth.",
              "ceo|founder|owner|director|boss|leader|leadership|who runs|who owns|who started|who founded|lerease|management|chief executive|head of",
              Lk("About us", "About", "Home")),

            E("registered", "Is RebuildYOU a registered company?",
              "RebuildYOU is registered as Rebuild You Skills Academy (Pty) Ltd, registration number 2026/224767/07, and trades as Rebuild You Training Centre.",
              "registered|registration number|company registration|registered company|pty|pty ltd|legit|legitimate|legal|real company|trusted|trust|scam|genuine|cipc|reg number|company number",
              Lk("About us", "About", "Home")),

            E("data-rights", "How do I see, correct or delete my personal information or close my account?",
              "You can ask to see, correct or delete your personal information, or withdraw your account, by contacting us using the details on the Contact page. You can also delete your CV at any time from your dashboard.",
              "delete my account|delete account|close my account|close account|remove my account|remove my data|delete my data|delete my information|correct my information|update my details|change my details|withdraw my account|cancel my account|cancel account|unsubscribe|opt out|my rights|erase|erasure|forget me|deactivate",
              Lk("Contact us", "Contact", "Home"), Lk("Privacy Notice", "Privacy", "Home")),

            E("login-help", "I cannot log in or I need help with my password",
              "Passwords need at least 8 characters, including an uppercase letter, a lowercase letter and a number. After 5 incorrect attempts, sign-in is locked for 15 minutes, so wait and then try again. Once you are signed in you can change your password from your account area. If you still cannot get in, contact us and we will help.",
              "password|passwords|passcode|log in|login|logging in|sign in|signin|cannot log in|can't log in|locked|locked out|lockout|forgot|forgotten|forgot password|reset password|reset my password|change password|change my password|username|cannot sign in|can't sign in|wrong password|incorrect password|not working|problem logging in|problem with login|access my account|account locked",
              Lk("Log in", "Login", "Account"), Lk("Contact us", "Contact", "Home")),

            E("social", "Where can I follow RebuildYOU on social media?",
              "Use the social media icons in the Follow Us section at the bottom of every page to visit our official accounts. Each icon opens in a new tab.",
              "social media|socials|facebook|instagram|tiktok|tik tok|linkedin|youtube|follow us|follow you|follow",
              Lk("Contact us", "Contact", "Home")),
        };
    }
}
