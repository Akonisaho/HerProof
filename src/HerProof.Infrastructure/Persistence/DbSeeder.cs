using HerProof.Domain.Competencies;
using HerProof.Domain.Identity;
using HerProof.Domain.Interviews;
using HerProof.Domain.Opportunities;
using HerProof.Domain.Repositories;
using HerProof.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Infrastructure.Persistence;

/// <summary>Seeds the competency framework (always) and, guarded by environment, the hackathon
/// demo dataset from docs/02: four candidates, one recruiter, one assessor, one bursary partner,
/// three opportunities, two bursaries. Fake data only (CLAUDE.md). Idempotent: safe to run every
/// startup in Development.</summary>
public static class DbSeeder
{
    private const string DemoPassword = "Demo#Passw0rd!";

    public static async Task SeedAsync(
        HerProofDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        bool seedDemoData)
    {
        await SeedRolesAsync(roleManager);
        var competencies = await SeedCompetencyFrameworkAsync(db);

        if (seedDemoData)
        {
            await SeedDemoDataAsync(db, userManager, competencies);
        }
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var role in Enum.GetNames<UserRole>())
        {
            if (await roleManager.FindByNameAsync(role) is null)
            {
                await roleManager.CreateAsync(new ApplicationRole(role));
            }
        }
    }

    private static async Task<Dictionary<string, Competency>> SeedCompetencyFrameworkAsync(HerProofDbContext db)
    {
        if (await db.Competencies.AnyAsync())
        {
            return await db.Competencies.ToDictionaryAsync(c => c.Name);
        }

        string[] softwareCompetencies =
        [
            "C#", "OOP", "SQL", "ASP.NET Core", "API development",
            "Git", "Problem-solving", "Testing", "Dependency injection", "Error handling",
        ];

        string[] nonTraditionalCompetencies =
        [
            "Financial management", "Scheduling and planning", "Project coordination",
            "Business operations", "Resource management", "Team coordination",
        ];

        var competencies = new Dictionary<string, Competency>();

        foreach (var name in softwareCompetencies)
        {
            var competency = new Competency(name, CompetencyCategory.Software, "v1");
            AddLevelCriteria(db, competency);
            db.Competencies.Add(competency);
            competencies[name] = competency;
        }

        foreach (var name in nonTraditionalCompetencies)
        {
            var competency = new Competency(name, CompetencyCategory.NonTraditional, "v1");
            AddLevelCriteria(db, competency);
            db.Competencies.Add(competency);
            competencies[name] = competency;
        }

        await db.SaveChangesAsync();
        return competencies;
    }

    private static void AddLevelCriteria(HerProofDbContext db, Competency competency)
    {
        string[] descriptors =
        [
            "No observable evidence yet.",
            "Follows guided examples with support; recognises the concept in others' code.",
            "Applies the concept independently in small, well-defined tasks.",
            "Applies the concept reliably across a real feature, including edge cases.",
            "Teaches or reviews others' use of the concept and improves it at a system level.",
        ];

        for (var level = 0; level <= 4; level++)
        {
            db.CompetencyLevelCriteria.Add(new CompetencyLevelCriterion(competency.Id, level, $"{competency.Name}: {descriptors[level]}"));
        }
    }

    private static async Task SeedDemoDataAsync(HerProofDbContext db, UserManager<ApplicationUser> userManager, Dictionary<string, Competency> competencies)
    {
        if (await db.Organisations.AnyAsync())
        {
            return;
        }

        var candidateNames = new[] { "Lerato", "Keabetswe", "Anko", "Naledi" };
        var candidateProfilesByName = new Dictionary<string, CandidateProfile>();
        foreach (var name in candidateNames)
        {
            var user = await CreateDemoUserAsync(userManager, name, UserRole.Candidate);
            var profile = new CandidateProfile(user.Id);
            db.CandidateProfiles.Add(profile);
            candidateProfilesByName[name] = profile;
        }

        var recruiterUser = await CreateDemoUserAsync(userManager, "Recruiter", UserRole.Recruiter);
        var assessorUser = await CreateDemoUserAsync(userManager, "Assessor", UserRole.Assessor);
        var partnerUser = await CreateDemoUserAsync(userManager, "BursaryPartner", UserRole.Partner);
        await CreateDemoUserAsync(userManager, "Admin", UserRole.Administrator);

        var employer = new Organisation("Demo Software Co.", "Technology", "https://example.test/democo");
        var bursaryProvider = new Organisation("Demo Bursary Trust", "Education Funding", "https://example.test/demotrust");
        db.Organisations.AddRange(employer, bursaryProvider);

        db.OrganisationMembers.Add(new OrganisationMember(employer.Id, recruiterUser.Id, OrganisationRole.Recruiter));
        db.OrganisationMembers.Add(new OrganisationMember(bursaryProvider.Id, partnerUser.Id, OrganisationRole.Partner));
        _ = assessorUser; // Assessors are not tied to an organisation; role alone grants the policy (P03).

        var juniorCSharpRole = new Opportunity(
            employer.Id,
            "Junior C# Developer",
            "Entry-level role building features in an ASP.NET Core product.",
            OpportunityType.Job,
            "Johannesburg, hybrid",
            DateTime.UtcNow.AddDays(30));
        juniorCSharpRole.RequireCompetency(competencies["C#"].Id, requiredLevel: 2, isMandatory: true);
        juniorCSharpRole.RequireCompetency(competencies["OOP"].Id, requiredLevel: 2, isMandatory: true);
        juniorCSharpRole.RequireCompetency(competencies["Testing"].Id, requiredLevel: 1, isMandatory: false);
        juniorCSharpRole.Publish();

        var internshipRole = new Opportunity(
            employer.Id,
            "Software Internship",
            "Six-month internship pairing a junior with a mentor.",
            OpportunityType.Internship,
            "Remote",
            DateTime.UtcNow.AddDays(45));
        internshipRole.RequireCompetency(competencies["Git"].Id, requiredLevel: 1, isMandatory: true);
        internshipRole.RequireCompetency(competencies["Problem-solving"].Id, requiredLevel: 1, isMandatory: true);
        internshipRole.Publish();

        var operationsRole = new Opportunity(
            employer.Id,
            "Operations Coordinator",
            "Beyond Employment pathway: household and community coordination experience welcomed.",
            OpportunityType.Job,
            "Pretoria",
            DateTime.UtcNow.AddDays(21));
        operationsRole.RequireCompetency(competencies["Project coordination"].Id, requiredLevel: 2, isMandatory: true);
        operationsRole.RequireCompetency(competencies["Resource management"].Id, requiredLevel: 1, isMandatory: false);
        operationsRole.Publish();

        var womenInTechBursary = new Opportunity(
            bursaryProvider.Id,
            "Women in Tech Bursary",
            "Covers tuition for a certified software development course.",
            OpportunityType.Bursary,
            "South Africa",
            DateTime.UtcNow.AddDays(60));
        womenInTechBursary.RequireCompetency(competencies["Problem-solving"].Id, requiredLevel: 1, isMandatory: false);
        womenInTechBursary.AddEligibilityRule(
            EligibilityRuleType.GenderDeclared, EligibilityOperator.Equals, "Woman", isHardFilter: true,
            "This bursary is reserved for women, per the funder's published criteria.");
        womenInTechBursary.AddEligibilityRule(
            EligibilityRuleType.HouseholdIncomeBand, EligibilityOperator.In, "Low,Medium", isHardFilter: false,
            "Priority is given to applicants from lower household income bands.");
        womenInTechBursary.Publish();

        var careReturnersBursary = new Opportunity(
            bursaryProvider.Id,
            "Care Returners Bursary",
            "Supports candidates re-entering the workforce after a caregiving period.",
            OpportunityType.Bursary,
            "South Africa",
            DateTime.UtcNow.AddDays(60));
        careReturnersBursary.AddEligibilityRule(
            EligibilityRuleType.EducationLevel, EligibilityOperator.Equals, "Any", isHardFilter: false,
            "Open regardless of prior formal qualification.");
        careReturnersBursary.Publish();

        db.Opportunities.AddRange(juniorCSharpRole, internshipRole, operationsRole, womenInTechBursary, careReturnersBursary);

        SeedDemoRepositoryAndInterview(db, candidateProfilesByName["Anko"], juniorCSharpRole, competencies);

        await db.SaveChangesAsync();
    }

    /// <summary>docs/05 P18: a pre-analysed repository and pre-generated interview for Anko, so the
    /// demo has an instant, always-available fallback if the venue network or a live LLM call fails
    /// mid-rehearsal (docs/05 section 5's own fallback table). Both are marked IsDemoData so the UI
    /// shows "Demo data" rather than presenting cached results as freshly computed — CLAUDE.md's own
    /// "never fake results silently".</summary>
    private static void SeedDemoRepositoryAndInterview(HerProofDbContext db, CandidateProfile anko, Opportunity juniorCSharpRole, Dictionary<string, Competency> competencies)
    {
        var repository = new Repository(
            anko.Id,
            "anko-task-tracker",
            "https://github.com/demo-herproof/anko-task-tracker",
            "main",
            "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2",
            RepositoryVisibility.Public,
            DateTime.UtcNow.AddDays(-2));
        db.Repositories.Add(repository);

        var analysis = new RepositoryAnalysis(repository.Id, DateTime.UtcNow.AddDays(-2));
        analysis.MarkInProgress();

        var csharpFinding = analysis.AddFinding(
            competencies["C#"].Id, "LanguageUsage", "src/TaskService.cs", "12-40",
            "Implements a TaskService class with async CRUD methods over EF Core.",
            "public async Task<Models.TaskItem> CreateAsync(string title, DateTime dueAtUtc)",
            "High");
        var oopFinding = analysis.AddFinding(
            competencies["OOP"].Id, "DesignPattern", "src/TaskService.cs", "5-10",
            "TaskService depends on an injected ITaskRepository interface rather than a concrete type.",
            "public TaskService(ITaskRepository repository)",
            "High");
        analysis.AddFinding(
            competencies["Testing"].Id, "TestCoverage", "tests/TaskServiceTests.cs", "15-45",
            "xUnit tests cover create, complete and overdue-task scenarios.",
            "public async Task CreateAsync_sets_a_due_date()",
            "Medium");

        analysis.Complete(
            technologiesDetectedJson: "[\"C#\", \"ASP.NET Core\", \"EF Core\", \"xUnit\"]",
            summary: "A small task-tracking API: EF Core-backed service layer, interface-based dependencies, xUnit test coverage of the main flows.",
            modelVersion: "demo-seed",
            promptVersion: "demo-seed",
            completedAtUtc: DateTime.UtcNow.AddDays(-2).AddMinutes(3));
        analysis.RecordExecution(restoreOk: true, buildOk: true, testsPassed: 6, testsFailed: 0, logExcerpt: "Restored, built, 6 passed, 0 failed.", sandboxImageDigest: "demo-seed");
        analysis.MarkAsDemoData();
        db.RepositoryAnalyses.Add(analysis);

        var interview = new Interview(anko.Id, repository.Id, juniorCSharpRole.Id, Domain.Interviews.InterviewMode.Text, "Thandi", DateTime.UtcNow.AddDays(-2).AddMinutes(10));

        var csharpQuestion = interview.AskQuestion(
            competencies["C#"].Id,
            "Your TaskService.CreateAsync method is async. Walk me through what happens if two requests try to create a task for the same user at the same time.",
            "src/TaskService.cs", "12-18", questionOrder: 0);
        var csharpTurn = csharpQuestion.AddTurn(Domain.Interviews.TurnKind.Question, csharpQuestion.QuestionText, DateTime.UtcNow.AddDays(-2).AddMinutes(11));
        csharpTurn.RecordAnswer(
            "Each request gets its own DbContext from the DI container, so they don't share state. Both inserts go through, since there's nothing stopping two tasks with the same title - the uniqueness isn't enforced at that level.",
            recordingReference: null, durationSeconds: 45);
        csharpTurn.ConfirmTranscript(csharpTurn.TranscriptRaw!);
        csharpTurn.Evaluate(
            Domain.Interviews.Verdict.Understood,
            "Correctly identifies per-request DbContext scoping and the absence of a uniqueness constraint; matches the actual code.",
            "{\"codeClaim\": \"async CreateAsync with injected DbContext\", \"answerMatches\": true}");

        var oopQuestion = interview.AskQuestion(
            competencies["OOP"].Id,
            "TaskService takes an ITaskRepository in its constructor instead of a concrete class. Why did you design it that way?",
            "src/TaskService.cs", "5-10", questionOrder: 1);
        var oopTurn = oopQuestion.AddTurn(Domain.Interviews.TurnKind.Question, oopQuestion.QuestionText, DateTime.UtcNow.AddDays(-2).AddMinutes(14));
        oopTurn.RecordAnswer(
            "So I can swap in a fake repository for tests without touching a real database, and so the service doesn't know or care where the data actually lives.",
            recordingReference: null, durationSeconds: 30);
        oopTurn.ConfirmTranscript(oopTurn.TranscriptRaw!);
        oopTurn.Evaluate(
            Domain.Interviews.Verdict.Understood,
            "Names both real reasons for depending on an interface (testability, decoupling from storage) — matches the test project's use of a fake repository.",
            "{\"codeClaim\": \"constructor-injected ITaskRepository\", \"answerMatches\": true}");

        interview.Complete(DateTime.UtcNow.AddDays(-2).AddMinutes(16));
        interview.MarkAsDemoData();
        db.Interviews.Add(interview);
        _ = csharpFinding;
        _ = oopFinding;
    }

    private static async Task<ApplicationUser> CreateDemoUserAsync(UserManager<ApplicationUser> userManager, string name, UserRole role)
    {
        var email = $"{name.ToLowerInvariant()}@demo.herproof.test";
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return existing;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = name,
        };

        var result = await userManager.CreateAsync(user, DemoPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed demo user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(user, role.ToString());
        return user;
    }
}
