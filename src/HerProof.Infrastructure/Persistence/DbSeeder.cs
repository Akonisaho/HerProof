using HerProof.Application.Assessments;
using HerProof.Application.Passports;
using HerProof.Application.Verification;
using HerProof.Domain.Assessments;
using HerProof.Domain.Competencies;
using HerProof.Domain.Identity;
using HerProof.Domain.Interviews;
using HerProof.Domain.Opportunities;
using HerProof.Domain.Repositories;
using HerProof.Domain.Verification;
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

            // Runs for the seeded demo account AND whichever real account is being used to rehearse
            // the demo — independent of SeedDemoDataAsync's "already seeded" guard, so it still
            // applies to a database that was seeded before this existed.
            foreach (var email in new[] { "anko@demo.herproof.test", "takalanikonie08@gmail.com", "thandiwe.livedemo@example.test" })
            {
                await EnsureDemoRepositoryAndFirstInterviewAsync(db, email, competencies);
                await SeedSecondDemoInterviewIfNeededAsync(db, email, competencies);
                await SeedDemoPassportIfNeededAsync(db, email, competencies);
            }

            // Every step on the "Not sure where to start?" career roadmap must have a real learning
            // link before a candidate can be sent to take an assessment or submit a repository for it
            // - independent of SeedLearningResources's "already seeded" guard, so it still applies to a
            // database that was seeded before this existed.
            await EnsureAdditionalLearningResourcesAsync(db, competencies);

            // Same reasoning: every roadmap step needs a real, Published assessment she can actually
            // take, not an "ask an admin" dead end. Idempotent (checked by title), so it also reaches
            // a database seeded before this existed.
            await EnsureCoreAssessmentsAsync(db, competencies);
        }
    }

    private static async Task<CandidateProfile?> FindCandidateByEmailAsync(HerProofDbContext db, string email) =>
        await (from cp in db.CandidateProfiles
               join u in db.Users on cp.UserId equals u.Id
               where u.Email == email
               select cp).FirstOrDefaultAsync();

    /// <summary>If this candidate has no repository at all yet, gives her the same showcase
    /// repository + completed first interview Anko has (docs/05 P18's fallback data), so "My
    /// interviews" and "My Passport" aren't empty for whichever account is being used to rehearse.</summary>
    private static async Task EnsureDemoRepositoryAndFirstInterviewAsync(HerProofDbContext db, string email, Dictionary<string, Competency> competencies)
    {
        var candidateProfile = await FindCandidateByEmailAsync(db, email);
        if (candidateProfile is null || await db.Repositories.AnyAsync(r => r.CandidateId == candidateProfile.Id))
        {
            return;
        }

        var juniorCSharpRole = await db.Opportunities.FirstOrDefaultAsync(o => o.Title == "Junior C# Developer");
        if (juniorCSharpRole is null)
        {
            return;
        }

        SeedDemoRepositoryAndInterview(db, candidateProfile, juniorCSharpRole, competencies);
        await db.SaveChangesAsync();
    }

    /// <summary>A real, documented demo verification decision (CLAUDE.md rule 1: evidence before
    /// verification, by an Assessor) attributed to the seeded demo Assessor account, exactly like the
    /// seeded demo interview's evaluations — so "My Passport" has something to show on first login
    /// rather than a confusing blank page.</summary>
    private static async Task SeedDemoPassportIfNeededAsync(HerProofDbContext db, string email, Dictionary<string, Competency> competencies)
    {
        var candidateProfile = await FindCandidateByEmailAsync(db, email);
        var assessorUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "assessor@demo.herproof.test");
        if (candidateProfile is null || assessorUser is null)
        {
            return;
        }

        if (await db.SkillsPassports.AnyAsync(p => p.CandidateId == candidateProfile.Id))
        {
            return;
        }

        var now = DateTime.UtcNow.AddDays(-1);
        var passport = PassportIssuanceService.EnsurePassport(null, candidateProfile.Id, now);
        db.Add(passport);

        foreach (var (competencyName, level) in new[] { ("C#", 2), ("OOP", 2), ("Git", 1) })
        {
            var record = VerificationDecisionService.Create(
                actingUserIsAssessor: true, candidateProfile.Id, competencies[competencyName].Id, assessmentId: Guid.Empty,
                finalLevel: level, assessorUser.Id, VerificationDecision.Verified,
                $"Demo verification: {competencyName} demonstrated in anko-task-tracker and confirmed in interview.", now, supersedesId: null);
            db.Add(record);

            var candidateCompetency = await db.CandidateCompetencies
                .FirstOrDefaultAsync(cc => cc.CandidateId == candidateProfile.Id && cc.CompetencyId == competencies[competencyName].Id);
            if (candidateCompetency is null)
            {
                candidateCompetency = new CandidateCompetency(candidateProfile.Id, competencies[competencyName].Id, declaredLevel: null, now);
                db.Add(candidateCompetency);
            }

            candidateCompetency.ApplyAssessorDecision(CandidateCompetencyStatus.Verified, level, now);

            var slug = competencyName.Replace(" ", "").ToUpperInvariant();
            var credential = PassportIssuanceService.IssueForVerification(passport, record, slug, now);
            if (credential is not null)
            {
                db.Add(credential);
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedSecondDemoInterviewIfNeededAsync(HerProofDbContext db, string email, Dictionary<string, Competency> competencies)
    {
        var candidateProfile = await FindCandidateByEmailAsync(db, email);
        if (candidateProfile is null)
        {
            return;
        }

        var existingInterviewCount = await db.Interviews.CountAsync(i => i.CandidateId == candidateProfile.Id);
        if (existingInterviewCount >= 2)
        {
            return;
        }

        var repository = await db.Repositories.FirstOrDefaultAsync(r => r.CandidateId == candidateProfile.Id && r.RepositoryName == "AspNetCoreDiagnosticScenarios");
        var internshipOpportunity = await db.Opportunities.FirstOrDefaultAsync(o => o.Title == "Software Internship");
        if (repository is null || internshipOpportunity is null)
        {
            return;
        }

        var interview = new Interview(candidateProfile.Id, repository.Id, internshipOpportunity.Id, InterviewMode.Text, "Nolwazi", DateTime.UtcNow.AddDays(-1));

        var gitQuestion = interview.AskQuestion(
            competencies["Git"].Id,
            "Your repository has a clean, incremental commit history. Walk me through how you decide when to make a new commit.",
            "src/TaskService.cs", "1-45", questionOrder: 0);
        var gitTurn = gitQuestion.AddTurn(TurnKind.Question, gitQuestion.QuestionText, DateTime.UtcNow.AddDays(-1).AddMinutes(2));
        gitTurn.RecordAnswer(
            "I commit whenever a single logical change is complete and the build still passes — usually one feature or one fix per commit, so the history reads as a changelog and I can revert just that piece if it turns out wrong.",
            recordingReference: null, durationSeconds: 28);
        gitTurn.ConfirmTranscript(gitTurn.TranscriptRaw!);
        gitTurn.Evaluate(
            Verdict.Understood,
            "Describes atomic, logically-scoped commits tied to a passing build — a real working practice, not a rote definition.",
            "{\"codeClaim\": \"incremental commit history\", \"answerMatches\": true}");

        var problemSolvingQuestion = interview.AskQuestion(
            competencies["Problem-solving"].Id,
            "If a candidate task needed to support recurring due dates, how would you extend TaskService without breaking existing callers?",
            "src/TaskService.cs", "12-18", questionOrder: 1);
        var problemSolvingTurn = problemSolvingQuestion.AddTurn(TurnKind.Question, problemSolvingQuestion.QuestionText, DateTime.UtcNow.AddDays(-1).AddMinutes(5));
        problemSolvingTurn.RecordAnswer(
            "I'd add an optional recurrence parameter with a default of null so existing calls are unaffected, then branch inside CreateAsync to schedule the next occurrence only when it's set.",
            recordingReference: null, durationSeconds: 24);
        problemSolvingTurn.ConfirmTranscript(problemSolvingTurn.TranscriptRaw!);
        problemSolvingTurn.Evaluate(
            Verdict.Understood,
            "Proposes an additive, backward-compatible signature change — a sound approach that doesn't disturb existing behaviour.",
            "{\"codeClaim\": \"CreateAsync extension\", \"answerMatches\": true}");

        interview.Complete(DateTime.UtcNow.AddDays(-1).AddMinutes(8));
        interview.MarkAsDemoData();
        db.Interviews.Add(interview);
        await db.SaveChangesAsync();
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
            // Profile completeness (RequireCompleteCandidateProfileFilter) gates browsing/applying
            // for every Candidate, demo accounts included — without this they'd all bounce to
            // /my-profile the moment the rehearsal tries to browse Opportunities.
            profile.UpdateProfile(
                province: "Gauteng", educationLevel: "Diploma", genderDeclared: "Woman",
                householdIncomeBand: "Low", careerInterests: "Software development",
                gitHubProfile: null, linkedInProfile: null);
            db.CandidateProfiles.Add(profile);
            candidateProfilesByName[name] = profile;
        }

        var recruiterUser = await CreateDemoUserAsync(userManager, "Recruiter", UserRole.Recruiter);
        var assessorUser = await CreateDemoUserAsync(userManager, "Assessor", UserRole.Assessor);
        var partnerUser = await CreateDemoUserAsync(userManager, "BursaryPartner", UserRole.Partner);
        await CreateDemoUserAsync(userManager, "Admin", UserRole.Administrator);

        var employer = new Organisation("Khanyisa Digital", "Technology", "https://khanyisadigital.co.za");
        var bursaryProvider = new Organisation("Thuto Bursary Trust", "Education Funding", "https://thutobursary.org.za");
        var cellCFoundation = new Organisation("Cell C Foundation", "Telecommunications", "https://example.test/cellcfoundation");
        db.Organisations.AddRange(employer, bursaryProvider, cellCFoundation);

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

        var cellCGirlsBursary = new Opportunity(
            cellCFoundation.Id,
            "Cell C Girls Bursary",
            "Covers tuition and a device for young women studying towards a technology qualification.",
            OpportunityType.Bursary,
            "South Africa",
            DateTime.UtcNow.AddDays(90));
        cellCGirlsBursary.RequireCompetency(competencies["Problem-solving"].Id, requiredLevel: 1, isMandatory: false);
        cellCGirlsBursary.AddEligibilityRule(
            EligibilityRuleType.GenderDeclared, EligibilityOperator.Equals, "Woman", isHardFilter: true,
            "This bursary is reserved for women, per the funder's published criteria.");
        cellCGirlsBursary.AddEligibilityRule(
            EligibilityRuleType.AgeRange, EligibilityOperator.Between, "18-25", isHardFilter: false,
            "Priority is given to applicants aged 18-25.");
        cellCGirlsBursary.Publish();

        db.Opportunities.AddRange(juniorCSharpRole, internshipRole, operationsRole, womenInTechBursary, careReturnersBursary, cellCGirlsBursary);

        SeedDemoRepositoryAndInterview(db, candidateProfilesByName["Anko"], juniorCSharpRole, competencies);
        SeedLearningResources(db, competencies);

        await db.SaveChangesAsync();
    }

    /// <summary>HP-032, closed: without these, the gap-to-proof engine's "here's how to close this
    /// gap" recommendation had nothing to show out of the box in the demo data. Real, free resources
    /// (Microsoft Learn/official docs), not placeholder links.</summary>
    private static void SeedLearningResources(HerProofDbContext db, Dictionary<string, Competency> competencies)
    {
        (string CompetencyName, string Title, string Url, string CostType, int TargetLevel)[] resources =
        [
            ("Problem-solving", "Codewars - practice coding kata by difficulty", "https://www.codewars.com/", "Free", 0),
            ("C#", "C# documentation and tutorials", "https://learn.microsoft.com/dotnet/csharp/", "Free", 2),
            ("OOP", "Object-oriented programming in C#", "https://learn.microsoft.com/dotnet/csharp/fundamentals/object-oriented/", "Free", 2),
            ("Testing", "Unit testing C# with xUnit", "https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test", "Free", 1),
            ("Git", "Git and GitHub for beginners", "https://docs.github.com/get-started", "Free", 1),
            ("Problem-solving", "Introduction to algorithmic thinking", "https://www.freecodecamp.org/learn/javascript-algorithms-and-data-structures/", "Free", 1),
            ("Project coordination", "Project management fundamentals", "https://learn.microsoft.com/training/paths/project-management-fundamentals/", "Free", 2),
            ("Resource management", "Planning and managing resources", "https://learn.microsoft.com/training/modules/plan-manage-resources/", "Free", 1),
        ];

        foreach (var (competencyName, title, url, costType, targetLevel) in resources)
        {
            if (!competencies.TryGetValue(competencyName, out var competency))
            {
                continue;
            }

            db.LearningResources.Add(new LearningResource(competency.Id, title, url, costType, targetLevel));
        }
    }

    /// <summary>HP-career-roadmap: fills in the competencies SeedLearningResources left without a
    /// link - every roadmap step needs one real place to learn it before a candidate is sent on to
    /// take an assessment or submit a repository for it. Idempotent (checked per competency+URL, not
    /// gated by the one-time seed guard) so it still reaches a database seeded before this existed.</summary>
    private static async Task EnsureAdditionalLearningResourcesAsync(HerProofDbContext db, Dictionary<string, Competency> competencies)
    {
        (string CompetencyName, string Title, string Url, string CostType, int TargetLevel)[] resources =
        [
            ("API development", "ASP.NET Core web API tutorial", "https://learn.microsoft.com/aspnet/core/web-api/", "Free", 2),
            ("ASP.NET Core", "ASP.NET Core documentation", "https://learn.microsoft.com/aspnet/core/", "Free", 2),
            ("Scheduling and planning", "Project management fundamentals", "https://learn.microsoft.com/training/paths/project-management-fundamentals/", "Free", 1),
            ("Team coordination", "Project management fundamentals", "https://learn.microsoft.com/training/paths/project-management-fundamentals/", "Free", 2),
            ("Business operations", "Project management fundamentals", "https://learn.microsoft.com/training/paths/project-management-fundamentals/", "Free", 1),
            ("SQL", "Transact-SQL language reference", "https://learn.microsoft.com/sql/t-sql/language-reference", "Free", 2),
            ("Error handling", "Exceptions and exception handling in C#", "https://learn.microsoft.com/dotnet/csharp/fundamentals/exceptions/", "Free", 1),
        ];

        foreach (var (competencyName, title, url, costType, targetLevel) in resources)
        {
            if (!competencies.TryGetValue(competencyName, out var competency))
            {
                continue;
            }

            var alreadyExists = await db.LearningResources.AnyAsync(lr => lr.CompetencyId == competency.Id && lr.Url == url);
            if (!alreadyExists)
            {
                db.LearningResources.Add(new LearningResource(competency.Id, title, url, costType, targetLevel));
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>HP-career-roadmap: three real, Published assessments covering every step of the
    /// Software Developer roadmap end to end, so a candidate is never told "ask an admin to add one" -
    /// idempotent (checked by title), so it also reaches a database seeded before this existed.
    /// Created under the demo Assessor account the same way a real Recruiter/Assessor/Partner would
    /// from the Assessments screen - nothing here bypasses that authoring flow, it's just pre-filled.</summary>
    private static async Task EnsureCoreAssessmentsAsync(HerProofDbContext db, Dictionary<string, Competency> competencies)
    {
        var assessorUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "assessor@demo.herproof.test");
        if (assessorUser is null)
        {
            return;
        }

        if (!await db.Assessments.AnyAsync(a => a.Title == "Core C# Practical"))
        {
            var assessment = new Assessment("Core C# Practical", "Shopping cart total and a pagination bug fix - real, build-and-test-checked C#.", "C#", 60, assessorUser.Id);
            CSharpAssessmentQuestionTemplate.ApplyTo(assessment);
            assessment.Questions.First(q => q.FileName == "ShoppingCart.cs").SetHiddenTestCode(
                """
                using System.Collections.Generic;
                using Xunit;

                public class ShoppingCartHiddenTests
                {
                    [Fact]
                    public void NoDiscountWhenTotalIsAtOrBelow500()
                    {
                        var cart = new ShoppingCart();
                        cart.Items.Add(new CartItem { Name = "A", Price = 100m, Quantity = 5 });
                        Assert.Equal(500m, cart.CalculateTotal());
                    }

                    [Fact]
                    public void TenPercentDiscountWhenTotalExceeds500()
                    {
                        var cart = new ShoppingCart();
                        cart.Items.Add(new CartItem { Name = "A", Price = 100m, Quantity = 6 });
                        Assert.Equal(540m, cart.CalculateTotal());
                    }
                }
                """);
            assessment.Questions.First(q => q.FileName == "Paginator.cs").SetHiddenTestCode(
                """
                using System.Collections.Generic;
                using Xunit;

                public class PaginatorHiddenTests
                {
                    private static List<string> Items()
                    {
                        var items = new List<string>();
                        for (var i = 1; i <= 25; i++)
                        {
                            items.Add($"item{i}");
                        }

                        return items;
                    }

                    [Fact]
                    public void Page1ReturnsTheFirstTenItems()
                    {
                        var result = new Paginator().GetPage(Items(), 1, 10);
                        Assert.Equal(10, result.Count);
                        Assert.Equal("item1", result[0]);
                        Assert.Equal("item10", result[9]);
                    }

                    [Fact]
                    public void Page2ReturnsItemsElevenToTwenty()
                    {
                        var result = new Paginator().GetPage(Items(), 2, 10);
                        Assert.Equal("item11", result[0]);
                        Assert.Equal("item20", result[9]);
                    }
                }
                """);
            CoverIfPresent(assessment, competencies, "C#", 1m);
            CoverIfPresent(assessment, competencies, "OOP", 1m);
            CoverIfPresent(assessment, competencies, "Problem-solving", 1m);
            assessment.AddRubricCriterion("Automated build & test", 1m, "Restores, builds, and passes the hidden tests in the sandbox (docs/05 P13).");
            assessment.Publish();
            db.Add(assessment);
        }

        if (!await db.Assessments.AnyAsync(a => a.Title == "Version Control & Reliability Practical"))
        {
            var assessment = new Assessment("Version Control & Reliability Practical", "A commit-history summary and a retry-logic bug fix.", "Practical", 45, assessorUser.Id);
            assessment.AddQuestion(
                1,
                "Implement: Commit log summary by author",
                "Complete `SummarizeByAuthor` on `CommitLog` so it returns, for each author, the number " +
                "of commits they made, across the full commit history.",
                """
                public class Commit
                {
                    public string Author { get; set; } = string.Empty;
                    public string Message { get; set; } = string.Empty;
                }

                public class CommitLog
                {
                    public List<Commit> Commits { get; set; } = new();

                    public Dictionary<string, int> SummarizeByAuthor()
                    {
                        // TODO: implement this method.
                        throw new NotImplementedException();
                    }
                }
                """,
                "CommitLog.cs",
                AssessmentQuestionType.Implement,
                hiddenTestCode: """
                    using System.Collections.Generic;
                    using Xunit;

                    public class CommitLogHiddenTests
                    {
                        [Fact]
                        public void CountsCommitsPerAuthor()
                        {
                            var log = new CommitLog();
                            log.Commits.Add(new Commit { Author = "Lerato", Message = "a" });
                            log.Commits.Add(new Commit { Author = "Lerato", Message = "b" });
                            log.Commits.Add(new Commit { Author = "Naledi", Message = "c" });

                            var summary = log.SummarizeByAuthor();

                            Assert.Equal(2, summary["Lerato"]);
                            Assert.Equal(1, summary["Naledi"]);
                        }
                    }
                    """);
            assessment.AddQuestion(
                2,
                "Fix the bug: retry logic swallows the real exception",
                "`RetryPolicy.Execute` is supposed to retry the given action up to `maxAttempts` times " +
                "and, if every attempt fails, throw the *last* real exception. Right now it always " +
                "throws a generic exception with no useful information. Find the bug and fix it.",
                """
                public class RetryPolicy
                {
                    public void Execute(Action action, int maxAttempts)
                    {
                        for (var attempt = 0; attempt < maxAttempts; attempt++)
                        {
                            try
                            {
                                action();
                                return;
                            }
                            catch
                            {
                                // Bug: the real exception is discarded here.
                            }
                        }

                        throw new InvalidOperationException("All retry attempts failed.");
                    }
                }
                """,
                "RetryPolicy.cs",
                AssessmentQuestionType.FixBug,
                hiddenTestCode: """
                    using System;
                    using Xunit;

                    public class RetryPolicyHiddenTests
                    {
                        [Fact]
                        public void SucceedsWithoutThrowingWhenActionEventuallySucceeds()
                        {
                            var attempts = 0;
                            new RetryPolicy().Execute(() =>
                            {
                                attempts++;
                                if (attempts < 2)
                                {
                                    throw new InvalidOperationException("transient");
                                }
                            }, 3);
                            Assert.Equal(2, attempts);
                        }

                        [Fact]
                        public void ThrowsTheRealLastExceptionWhenAllAttemptsFail()
                        {
                            var ex = Assert.Throws<ArgumentException>(() =>
                                new RetryPolicy().Execute(() => throw new ArgumentException("specific failure"), 3));
                            Assert.Equal("specific failure", ex.Message);
                        }
                    }
                    """);
            CoverIfPresent(assessment, competencies, "Git", 1m);
            CoverIfPresent(assessment, competencies, "Testing", 1m);
            CoverIfPresent(assessment, competencies, "Error handling", 1m);
            assessment.AddRubricCriterion("Automated build & test", 1m, "Restores, builds, and passes the hidden tests in the sandbox (docs/05 P13).");
            assessment.Publish();
            db.Add(assessment);
        }

        if (!await db.Assessments.AnyAsync(a => a.Title == "Web API Practical"))
        {
            var assessment = new Assessment("Web API Practical", "Request validation and a pagination bug in an API-style response.", "Practical", 45, assessorUser.Id);
            assessment.AddQuestion(
                1,
                "Implement: Validate a route parameter",
                "Complete `IsValidId` on `RouteValidator` so it returns true only for a positive integer " +
                "string with no leading zeros (e.g. \"42\" is valid, \"0\", \"-1\", \"04\" and \"abc\" are not).",
                """
                public class RouteValidator
                {
                    public bool IsValidId(string id)
                    {
                        // TODO: implement this method.
                        throw new NotImplementedException();
                    }
                }
                """,
                "RouteValidator.cs",
                AssessmentQuestionType.Implement,
                hiddenTestCode: """
                    using Xunit;

                    public class RouteValidatorHiddenTests
                    {
                        [Theory]
                        [InlineData("42", true)]
                        [InlineData("1", true)]
                        [InlineData("0", false)]
                        [InlineData("-1", false)]
                        [InlineData("04", false)]
                        [InlineData("abc", false)]
                        public void ValidatesIdsCorrectly(string id, bool expected)
                        {
                            Assert.Equal(expected, new RouteValidator().IsValidId(id));
                        }
                    }
                    """);
            assessment.AddQuestion(
                2,
                "Fix the bug: API page response is off by one",
                "`GetPageResponse` is supposed to return items 1-10 for page 1, 11-20 for page 2, and so " +
                "on - the same contract a real paged API endpoint would expose. It has a bug.",
                """
                public class PagedResponse
                {
                    public List<string> GetPageResponse(List<string> items, int pageNumber, int pageSize)
                    {
                        var skip = pageNumber * pageSize;
                        return items.Skip(skip).Take(pageSize).ToList();
                    }
                }
                """,
                "PagedResponse.cs",
                AssessmentQuestionType.FixBug,
                hiddenTestCode: """
                    using System.Collections.Generic;
                    using Xunit;

                    public class PagedResponseHiddenTests
                    {
                        private static List<string> Items()
                        {
                            var items = new List<string>();
                            for (var i = 1; i <= 25; i++)
                            {
                                items.Add($"item{i}");
                            }

                            return items;
                        }

                        [Fact]
                        public void Page1ReturnsTheFirstTenItems()
                        {
                            var result = new PagedResponse().GetPageResponse(Items(), 1, 10);
                            Assert.Equal(10, result.Count);
                            Assert.Equal("item1", result[0]);
                        }

                        [Fact]
                        public void Page2ReturnsItemsElevenToTwenty()
                        {
                            var result = new PagedResponse().GetPageResponse(Items(), 2, 10);
                            Assert.Equal("item11", result[0]);
                        }
                    }
                    """);
            CoverIfPresent(assessment, competencies, "API development", 1m);
            CoverIfPresent(assessment, competencies, "ASP.NET Core", 1m);
            assessment.AddRubricCriterion("Automated build & test", 1m, "Restores, builds, and passes the hidden tests in the sandbox (docs/05 P13).");
            assessment.Publish();
            db.Add(assessment);
        }

        if (!await db.Assessments.AnyAsync(a => a.Title == "Business Systems Practical"))
        {
            var assessment = new Assessment("Business Systems Practical", "Filtering a customer report and fixing a task-deadline scheduler.", "Practical", 45, assessorUser.Id);
            assessment.AddQuestion(
                1,
                "Implement: Filter active customers from a report",
                "Complete `GetActiveCustomers` on `CustomerReport` so it returns only customers whose " +
                "`IsActive` is true, ordered by `SignupDate` ascending - the same shape a real query " +
                "against a customers table would return.",
                """
                public class Customer
                {
                    public string Name { get; set; } = string.Empty;
                    public bool IsActive { get; set; }
                    public DateTime SignupDate { get; set; }
                }

                public class CustomerReport
                {
                    public List<Customer> GetActiveCustomers(List<Customer> customers)
                    {
                        // TODO: implement this method.
                        throw new NotImplementedException();
                    }
                }
                """,
                "CustomerReport.cs",
                AssessmentQuestionType.Implement,
                hiddenTestCode: """
                    using System;
                    using System.Collections.Generic;
                    using Xunit;

                    public class CustomerReportHiddenTests
                    {
                        [Fact]
                        public void ReturnsOnlyActiveCustomersOrderedBySignupDate()
                        {
                            var customers = new List<Customer>
                            {
                                new Customer { Name = "Later", IsActive = true, SignupDate = new DateTime(2024, 2, 1) },
                                new Customer { Name = "Inactive", IsActive = false, SignupDate = new DateTime(2024, 1, 1) },
                                new Customer { Name = "Earlier", IsActive = true, SignupDate = new DateTime(2024, 1, 1) },
                            };

                            var result = new CustomerReport().GetActiveCustomers(customers);

                            Assert.Equal(2, result.Count);
                            Assert.Equal("Earlier", result[0].Name);
                            Assert.Equal("Later", result[1].Name);
                        }
                    }
                    """);
            assessment.AddQuestion(
                2,
                "Fix the bug: task deadline calculator",
                "`DeadlineCalculator.GetDeadline` is supposed to return a task's start date plus its " +
                "duration in business days (skipping Saturday and Sunday). It currently just adds " +
                "calendar days, so deadlines land on weekends. Find the bug and fix it.",
                """
                public class DeadlineCalculator
                {
                    public DateTime GetDeadline(DateTime startDate, int durationBusinessDays)
                    {
                        return startDate.AddDays(durationBusinessDays);
                    }
                }
                """,
                "DeadlineCalculator.cs",
                AssessmentQuestionType.FixBug,
                hiddenTestCode: """
                    using System;
                    using Xunit;

                    public class DeadlineCalculatorHiddenTests
                    {
                        [Fact]
                        public void SkipsWeekendsWhenAddingBusinessDays()
                        {
                            // Monday 2024-01-01 + 5 business days = Monday 2024-01-08 (skips the weekend).
                            var start = new DateTime(2024, 1, 1);
                            var deadline = new DeadlineCalculator().GetDeadline(start, 5);
                            Assert.Equal(new DateTime(2024, 1, 8), deadline);
                            Assert.NotEqual(DayOfWeek.Saturday, deadline.DayOfWeek);
                            Assert.NotEqual(DayOfWeek.Sunday, deadline.DayOfWeek);
                        }
                    }
                    """);
            CoverIfPresent(assessment, competencies, "SQL", 1m);
            CoverIfPresent(assessment, competencies, "Business operations", 1m);
            CoverIfPresent(assessment, competencies, "Project coordination", 1m);
            CoverIfPresent(assessment, competencies, "Scheduling and planning", 1m);
            CoverIfPresent(assessment, competencies, "Team coordination", 1m);
            assessment.AddRubricCriterion("Automated build & test", 1m, "Restores, builds, and passes the hidden tests in the sandbox (docs/05 P13).");
            assessment.Publish();
            db.Add(assessment);
        }

        await db.SaveChangesAsync();
    }

    private static void CoverIfPresent(Assessment assessment, Dictionary<string, Competency> competencies, string competencyName, decimal weight)
    {
        if (competencies.TryGetValue(competencyName, out var competency))
        {
            assessment.CoverCompetency(competency.Id, weight);
        }
    }

    /// <summary>docs/05 P18: a pre-analysed repository and pre-generated interview for Anko, so the
    /// demo has an instant, always-available fallback if the venue network or a live LLM call fails
    /// mid-rehearsal (docs/05 section 5's own fallback table). Both are marked IsDemoData so the UI
    /// shows "Demo data" rather than presenting cached results as freshly computed — CLAUDE.md's own
    /// "never fake results silently".
    ///
    /// The repository itself is a REAL, real public repo (davidfowl/AspNetCoreDiagnosticScenarios),
    /// not a fictional placeholder URL — the completed interview's own Q&A below is still a scripted
    /// "TaskService" narrative purely for display, but starting a brand NEW interview on this seeded
    /// repository re-fetches live content from GitHub, and a fake URL would 404 every time (discovered
    /// the hard way: this previously pointed at "demo-herproof/anko-task-tracker", which doesn't
    /// exist).</summary>
    private static void SeedDemoRepositoryAndInterview(HerProofDbContext db, CandidateProfile anko, Opportunity juniorCSharpRole, Dictionary<string, Competency> competencies)
    {
        var repository = new Repository(
            anko.Id,
            "AspNetCoreDiagnosticScenarios",
            "https://github.com/davidfowl/AspNetCoreDiagnosticScenarios",
            "master",
            "bcc7ca394eeb7113da6b3071f738ee5361b67db3",
            RepositoryVisibility.Public,
            DateTime.UtcNow.AddDays(-2));
        db.Repositories.Add(repository);

        var analysis = new RepositoryAnalysis(repository.Id, DateTime.UtcNow.AddDays(-2));
        analysis.MarkInProgress();

        var csharpFinding = analysis.AddFinding(
            competencies["C#"].Id, "LanguageUsage", "Scenarios/Controllers/AsyncVoidController.cs", "1-15",
            "Demonstrates an async void action method and its exception-handling pitfalls.",
            "public async void Get()",
            "High");
        var oopFinding = analysis.AddFinding(
            competencies["OOP"].Id, "DesignPattern", "Scenarios/PokemonDbContext.cs", "1-15",
            "PokemonDbContext inherits from EF Core's DbContext base class, overriding its configuration.",
            "public class PokemonDbContext : DbContext",
            "High");
        analysis.AddFinding(
            competencies["Error handling"].Id, "ErrorHandling", "Scenarios/Controllers/MemoryLeakController.cs", "1-15",
            "Shows a controller action that leaks memory by holding a static reference with no cleanup path.",
            "public class MemoryLeakController : Controller",
            "Medium");

        analysis.Complete(
            technologiesDetectedJson: "[\"C#\", \"ASP.NET Core\", \"EF Core\"]",
            summary: "A collection of ASP.NET Core diagnostic scenarios: async/await pitfalls, EF Core DbContext usage, and common error-handling mistakes.",
            modelVersion: "demo-seed",
            promptVersion: "demo-seed",
            completedAtUtc: DateTime.UtcNow.AddDays(-2).AddMinutes(3));
        analysis.RecordExecution(restoreOk: true, buildOk: true, testsPassed: 0, testsFailed: 0, logExcerpt: "Restored and built, no tests present.", sandboxImageDigest: "demo-seed");
        analysis.MarkAsDemoData();
        db.RepositoryAnalyses.Add(analysis);

        var interview = new Interview(anko.Id, repository.Id, juniorCSharpRole.Id, Domain.Interviews.InterviewMode.Text, "Nolwazi", DateTime.UtcNow.AddDays(-2).AddMinutes(10));

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
