using HerProof.Domain.Assessments;
using HerProof.Domain.Audit;
using HerProof.Domain.Competencies;
using HerProof.Domain.Consent;
using HerProof.Domain.Experience;
using HerProof.Domain.Identity;
using HerProof.Domain.Interviews;
using HerProof.Domain.Opportunities;
using HerProof.Domain.Partnerships;
using HerProof.Domain.Passports;
using HerProof.Domain.Repositories;
using HerProof.Domain.Verification;
using HerProof.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Infrastructure.Persistence;

public class HerProofDbContext(DbContextOptions<HerProofDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    // Identity / People
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<OrganisationMember> OrganisationMembers => Set<OrganisationMember>();

    // Consent / Audit
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    // Partnerships
    public DbSet<PartnershipInquiry> PartnershipInquiries => Set<PartnershipInquiry>();

    // Opportunities
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<OpportunityCompetency> OpportunityCompetencies => Set<OpportunityCompetency>();
    public DbSet<EligibilityRule> EligibilityRules => Set<EligibilityRule>();
    public DbSet<CandidateApplication> Applications => Set<CandidateApplication>();

    // Competency framework / gap
    public DbSet<Competency> Competencies => Set<Competency>();
    public DbSet<CompetencyLevelCriterion> CompetencyLevelCriteria => Set<CompetencyLevelCriterion>();
    public DbSet<CandidateCompetency> CandidateCompetencies => Set<CandidateCompetency>();
    public DbSet<CompetencyEvidence> CompetencyEvidence => Set<CompetencyEvidence>();
    public DbSet<LearningResource> LearningResources => Set<LearningResource>();
    public DbSet<LearningProgress> LearningProgresses => Set<LearningProgress>();
    public DbSet<GapReport> GapReports => Set<GapReport>();
    public DbSet<GapReportItem> GapReportItems => Set<GapReportItem>();

    // Beyond Employment
    public DbSet<ExperienceRecord> ExperienceRecords => Set<ExperienceRecord>();
    public DbSet<ExperienceCompetency> ExperienceCompetencies => Set<ExperienceCompetency>();

    // Repository ingestion and analysis
    public DbSet<GitHubConnection> GitHubConnections => Set<GitHubConnection>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<RepositoryAnalysis> RepositoryAnalyses => Set<RepositoryAnalysis>();
    public DbSet<RepositoryFinding> RepositoryFindings => Set<RepositoryFinding>();
    public DbSet<ExecutionReport> ExecutionReports => Set<ExecutionReport>();

    // Interviews
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<InterviewQuestion> InterviewQuestions => Set<InterviewQuestion>();
    public DbSet<InterviewTurn> InterviewTurns => Set<InterviewTurn>();
    public DbSet<InterviewEvaluation> InterviewEvaluations => Set<InterviewEvaluation>();

    // Assessments
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentCompetency> AssessmentCompetencies => Set<AssessmentCompetency>();
    public DbSet<RubricCriterion> RubricCriteria => Set<RubricCriterion>();
    public DbSet<AssessmentQuestion> AssessmentQuestions => Set<AssessmentQuestion>();
    public DbSet<AssessmentSession> AssessmentSessions => Set<AssessmentSession>();
    public DbSet<IntegrityEvent> IntegrityEvents => Set<IntegrityEvent>();
    public DbSet<AssessmentSubmission> AssessmentSubmissions => Set<AssessmentSubmission>();
    public DbSet<AssessmentReview> AssessmentReviews => Set<AssessmentReview>();

    // Verification and passport
    public DbSet<VerificationRecord> VerificationRecords => Set<VerificationRecord>();
    public DbSet<SkillsPassport> SkillsPassports => Set<SkillsPassport>();
    public DbSet<PassportCredential> PassportCredentials => Set<PassportCredential>();
    public DbSet<PassportShare> PassportShares => Set<PassportShare>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HerProofDbContext).Assembly);

        ModelConventions.Apply(modelBuilder);
    }
}
