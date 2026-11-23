using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    shared_credentials_json = table.Column<string>(type: "jsonb", nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.id);
                    table.CheckConstraint("ck_candidate_application_status_enum", "\"status\" IN ('Submitted', 'Withdrawn', 'Reviewed')");
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "assessment_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criterion_scores_json = table.Column<string>(type: "jsonb", nullable: false),
                    feedback = table.Column<string>(type: "text", nullable: false),
                    review_status = table.Column<string>(type: "text", nullable: false),
                    reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_reviews", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    assessment_type = table.Column<string>(type: "text", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessments", x => x.id);
                    table.CheckConstraint("ck_assessment_status_enum", "\"status\" IN ('Draft', 'Published', 'Archived')");
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    detail_json = table.Column<string>(type: "jsonb", nullable: false),
                    at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candidate_competencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    declared_level = table.Column<int>(type: "integer", nullable: true),
                    verified_level = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_competencies", x => x.id);
                    table.CheckConstraint("ck_candidate_competency_status_enum", "\"status\" IN ('Claimed', 'EvidenceSubmitted', 'UnderAssessment', 'AssessmentCompleted', 'Verified', 'Developing', 'NotDemonstrated', 'Expired')");
                });

            migrationBuilder.CreateTable(
                name: "candidate_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    province = table.Column<string>(type: "text", nullable: true),
                    education_level = table.Column<string>(type: "text", nullable: true),
                    gender_declared = table.Column<string>(type: "text", nullable: true),
                    household_income_band = table.Column<string>(type: "text", nullable: true),
                    career_interests = table.Column<string>(type: "text", nullable: true),
                    git_hub_profile = table.Column<string>(type: "text", nullable: true),
                    linked_in_profile = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "competencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    framework_version = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competencies", x => x.id);
                    table.CheckConstraint("ck_competency_category_enum", "\"category\" IN ('Software', 'NonTraditional')");
                });

            migrationBuilder.CreateTable(
                name: "competency_level_criteria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    observable_criteria = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competency_level_criteria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "consent_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<string>(type: "text", nullable: false),
                    granted = table.Column<bool>(type: "boolean", nullable: false),
                    recorded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consent_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "execution_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    restore_ok = table.Column<bool>(type: "boolean", nullable: false),
                    build_ok = table.Column<bool>(type: "boolean", nullable: false),
                    tests_passed = table.Column<int>(type: "integer", nullable: false),
                    tests_failed = table.Column<int>(type: "integer", nullable: false),
                    log_excerpt = table.Column<string>(type: "text", nullable: true),
                    sandbox_image_digest = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_execution_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "experience_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_experience_records", x => x.id);
                    table.CheckConstraint("ck_experience_record_category_enum", "\"category\" IN ('Caregiving', 'Household', 'Community', 'Freelance', 'Volunteer', 'Other')");
                });

            migrationBuilder.CreateTable(
                name: "gap_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gap_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "git_hub_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_account_id = table.Column<string>(type: "text", nullable: false),
                    encrypted_token = table.Column<byte[]>(type: "bytea", nullable: false),
                    connected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_git_hub_connections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "interview_evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    turn_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verdict = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    evidence_summary = table.Column<string>(type: "text", nullable: false),
                    code_claims_vs_answer_json = table.Column<string>(type: "jsonb", nullable: false),
                    reviewer_status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interview_evaluations", x => x.id);
                    table.CheckConstraint("ck_interview_evaluation_verdict_enum", "\"verdict\" IN ('Understood', 'Partial', 'NotDemonstrated')");
                });

            migrationBuilder.CreateTable(
                name: "interviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repository_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    interviewer_persona = table.Column<string>(type: "text", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interviews", x => x.id);
                    table.CheckConstraint("ck_interview_mode_enum", "\"mode\" IN ('Voice', 'Text')");
                    table.CheckConstraint("ck_interview_status_enum", "\"status\" IN ('InProgress', 'Completed', 'Abandoned')");
                });

            migrationBuilder.CreateTable(
                name: "learning_resources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    cost_type = table.Column<string>(type: "text", nullable: false),
                    target_level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learning_resources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "opportunities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    opportunity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    location = table.Column<string>(type: "text", nullable: true),
                    deadline_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunities", x => x.id);
                    table.CheckConstraint("ck_opportunity_opportunity_type_enum", "\"opportunity_type\" IN ('Job', 'Internship', 'Learnership', 'Bursary')");
                    table.CheckConstraint("ck_opportunity_status_enum", "\"status\" IN ('Draft', 'Published', 'Closed')");
                });

            migrationBuilder.CreateTable(
                name: "organisation_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organisation_members", x => x.id);
                    table.CheckConstraint("ck_organisation_member_organisation_role_enum", "\"organisation_role\" IN ('Recruiter', 'Partner', 'Administrator')");
                });

            migrationBuilder.CreateTable(
                name: "organisations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    industry = table.Column<string>(type: "text", nullable: true),
                    website = table.Column<string>(type: "text", nullable: true),
                    verification_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organisations", x => x.id);
                    table.CheckConstraint("ck_organisation_verification_status_enum", "\"verification_status\" IN ('Unverified', 'Verified')");
                });

            migrationBuilder.CreateTable(
                name: "repositories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repository_name = table.Column<string>(type: "text", nullable: false),
                    repository_url = table.Column<string>(type: "text", nullable: false),
                    branch = table.Column<string>(type: "text", nullable: false),
                    commit_sha = table.Column<string>(type: "text", nullable: false),
                    visibility = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repositories", x => x.id);
                    table.CheckConstraint("ck_repository_visibility_enum", "\"visibility\" IN ('Public', 'Private')");
                });

            migrationBuilder.CreateTable(
                name: "skills_passports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    passport_number = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skills_passports", x => x.id);
                    table.CheckConstraint("ck_skills_passport_status_enum", "\"status\" IN ('Active', 'Suspended')");
                });

            migrationBuilder.CreateTable(
                name: "verification_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    final_level = table.Column<int>(type: "integer", nullable: true),
                    assessor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    evidence_summary = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_verification_records", x => x.id);
                    table.CheckConstraint("ck_verification_record_decision_enum", "\"decision\" IN ('Verified', 'Developing', 'NotDemonstrated')");
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_user_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_asp_net_user_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assessment_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_reference = table.Column<string>(type: "text", nullable: false),
                    test_results_json = table.Column<string>(type: "jsonb", nullable: true),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    review_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_submissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessment_submissions_assessment_reviews_review_id",
                        column: x => x.review_id,
                        principalTable: "assessment_reviews",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "assessment_competencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weight = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_competencies", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessment_competencies_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rubric_criteria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    weight = table.Column<decimal>(type: "numeric", nullable: false),
                    descriptor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rubric_criteria", x => x.id);
                    table.ForeignKey(
                        name: "fk_rubric_criteria_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "competency_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_type = table.Column<string>(type: "text", nullable: false),
                    evidence_reference = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competency_evidence", x => x.id);
                    table.ForeignKey(
                        name: "fk_competency_evidence_candidate_competencies_candidate_compet",
                        column: x => x.candidate_competency_id,
                        principalTable: "candidate_competencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repository_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    repository_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    technologies_detected_json = table.Column<string>(type: "jsonb", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    model_version = table.Column<string>(type: "text", nullable: true),
                    prompt_version = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    execution_report_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repository_analyses", x => x.id);
                    table.CheckConstraint("ck_repository_analysis_status_enum", "\"status\" IN ('Pending', 'InProgress', 'Completed', 'Failed', 'NeedsRetry')");
                    table.ForeignKey(
                        name: "fk_repository_analyses_execution_reports_execution_report_id",
                        column: x => x.execution_report_id,
                        principalTable: "execution_reports",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "experience_competencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    experience_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_experience_competencies", x => x.id);
                    table.CheckConstraint("ck_experience_competency_mapping_status_enum", "\"mapping_status\" IN ('Suggested', 'Confirmed', 'Rejected')");
                    table.ForeignKey(
                        name: "fk_experience_competencies_experience_records_experience_recor",
                        column: x => x.experience_record_id,
                        principalTable: "experience_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gap_report_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gap_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required_level = table.Column<int>(type: "integer", nullable: false),
                    demonstrated_level = table.Column<int>(type: "integer", nullable: true),
                    evidence_state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false),
                    learning_resource_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gap_report_items", x => x.id);
                    table.CheckConstraint("ck_gap_report_item_evidence_state_enum", "\"evidence_state\" IN ('Demonstrated', 'ClaimedNotAssessed', 'NotDemonstrated', 'NoEvidence')");
                    table.ForeignKey(
                        name: "fk_gap_report_items_gap_reports_gap_report_id",
                        column: x => x.gap_report_id,
                        principalTable: "gap_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "interview_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    interview_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_text = table.Column<string>(type: "text", nullable: false),
                    snippet_file_path = table.Column<string>(type: "text", nullable: true),
                    snippet_lines = table.Column<string>(type: "text", nullable: true),
                    question_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interview_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_interview_questions_interviews_interview_id",
                        column: x => x.interview_id,
                        principalTable: "interviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eligibility_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    is_hard_filter = table.Column<bool>(type: "boolean", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eligibility_rules", x => x.id);
                    table.CheckConstraint("ck_eligibility_rule_operator_enum", "\"operator\" IN ('Equals', 'In', 'GreaterThanOrEqual', 'LessThanOrEqual', 'Between')");
                    table.CheckConstraint("ck_eligibility_rule_rule_type_enum", "\"rule_type\" IN ('Province', 'GenderDeclared', 'EducationLevel', 'HouseholdIncomeBand', 'FieldOfStudy', 'AgeRange')");
                    table.ForeignKey(
                        name: "fk_eligibility_rules_opportunities_opportunity_id",
                        column: x => x.opportunity_id,
                        principalTable: "opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opportunity_competencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required_level = table.Column<int>(type: "integer", nullable: false),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunity_competencies", x => x.id);
                    table.ForeignKey(
                        name: "fk_opportunity_competencies_opportunities_opportunity_id",
                        column: x => x.opportunity_id,
                        principalTable: "opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "passport_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    passport_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verification_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credential_identifier = table.Column<string>(type: "text", nullable: false),
                    issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valid_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    credential_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    skills_passport_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_passport_credentials", x => x.id);
                    table.CheckConstraint("ck_passport_credential_credential_status_enum", "\"credential_status\" IN ('Active', 'Expired', 'Revoked')");
                    table.ForeignKey(
                        name: "fk_passport_credentials_skills_passports_skills_passport_id",
                        column: x => x.skills_passport_id,
                        principalTable: "skills_passports",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "passport_shares",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    passport_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    visible_fields = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    skills_passport_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_passport_shares", x => x.id);
                    table.ForeignKey(
                        name: "fk_passport_shares_skills_passports_skills_passport_id",
                        column: x => x.skills_passport_id,
                        principalTable: "skills_passports",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "assessment_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    consent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accommodation_granted = table.Column<bool>(type: "boolean", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_sessions", x => x.id);
                    table.CheckConstraint("ck_assessment_session_status_enum", "\"status\" IN ('Invited', 'ConsentGiven', 'InProgress', 'Interrupted', 'Submitted', 'TimedOut', 'Evaluated', 'Reviewed')");
                    table.ForeignKey(
                        name: "fk_assessment_sessions_assessment_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "assessment_submissions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "repository_findings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finding_type = table.Column<string>(type: "text", nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: false),
                    line_reference = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    evidence = table.Column<string>(type: "text", nullable: false),
                    confidence = table.Column<string>(type: "text", nullable: false),
                    repository_analysis_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repository_findings", x => x.id);
                    table.ForeignKey(
                        name: "fk_repository_findings_repository_analyses_repository_analysis",
                        column: x => x.repository_analysis_id,
                        principalTable: "repository_analyses",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "interview_turns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turn_index = table.Column<int>(type: "integer", nullable: false),
                    turn_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    prompt_text = table.Column<string>(type: "text", nullable: false),
                    transcript_raw = table.Column<string>(type: "text", nullable: true),
                    transcript_confirmed = table.Column<string>(type: "text", nullable: true),
                    recording_reference = table.Column<string>(type: "text", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    interview_question_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interview_turns", x => x.id);
                    table.CheckConstraint("ck_interview_turn_turn_kind_enum", "\"turn_kind\" IN ('Question', 'Probe', 'Clarify')");
                    table.ForeignKey(
                        name: "fk_interview_turns_interview_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "interview_evaluations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_interview_turns_interview_questions_interview_question_id",
                        column: x => x.interview_question_id,
                        principalTable: "interview_questions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "integrity_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false),
                    client_seq = table.Column<string>(type: "text", nullable: false),
                    assessment_session_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integrity_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_integrity_events_assessment_sessions_assessment_session_id",
                        column: x => x.assessment_session_id,
                        principalTable: "assessment_sessions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_applications_candidate_id",
                table: "applications",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_opportunity_id",
                table: "applications",
                column: "opportunity_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_role_claims_role_id",
                table: "AspNetRoleClaims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_claims_user_id",
                table: "AspNetUserClaims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_logins_user_id",
                table: "AspNetUserLogins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_roles_role_id",
                table: "AspNetUserRoles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assessment_competencies_assessment_id",
                table: "assessment_competencies",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_competencies_competency_id",
                table: "assessment_competencies",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_reviews_assessor_id",
                table: "assessment_reviews",
                column: "assessor_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_reviews_submission_id",
                table: "assessment_reviews",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sessions_assessment_id",
                table: "assessment_sessions",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sessions_candidate_id",
                table: "assessment_sessions",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sessions_submission_id",
                table: "assessment_sessions",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_submissions_review_id",
                table: "assessment_submissions",
                column: "review_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_submissions_session_id",
                table: "assessment_submissions",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_actor_user_id",
                table: "audit_log",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entity_id",
                table: "audit_log",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_competencies_candidate_id",
                table: "candidate_competencies",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_competencies_competency_id",
                table: "candidate_competencies",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_profiles_user_id",
                table: "candidate_profiles",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_competency_evidence_candidate_competency_id",
                table: "competency_evidence",
                column: "candidate_competency_id");

            migrationBuilder.CreateIndex(
                name: "ix_competency_level_criteria_competency_id",
                table: "competency_level_criteria",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "ix_consent_records_user_id",
                table: "consent_records",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_eligibility_rules_opportunity_id",
                table: "eligibility_rules",
                column: "opportunity_id");

            migrationBuilder.CreateIndex(
                name: "ix_execution_reports_analysis_id",
                table: "execution_reports",
                column: "analysis_id");

            migrationBuilder.CreateIndex(
                name: "ix_experience_competencies_competency_id",
                table: "experience_competencies",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "ix_experience_competencies_experience_record_id",
                table: "experience_competencies",
                column: "experience_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_experience_records_candidate_id",
                table: "experience_records",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_gap_report_items_competency_id",
                table: "gap_report_items",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "ix_gap_report_items_gap_report_id",
                table: "gap_report_items",
                column: "gap_report_id");

            migrationBuilder.CreateIndex(
                name: "ix_gap_reports_candidate_id",
                table: "gap_reports",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_gap_reports_opportunity_id",
                table: "gap_reports",
                column: "opportunity_id");

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_connections_candidate_id",
                table: "git_hub_connections",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_integrity_events_assessment_session_id",
                table: "integrity_events",
                column: "assessment_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_integrity_events_session_id",
                table: "integrity_events",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_interview_evaluations_turn_id",
                table: "interview_evaluations",
                column: "turn_id");

            migrationBuilder.CreateIndex(
                name: "ix_interview_questions_competency_id",
                table: "interview_questions",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "ix_interview_questions_interview_id",
                table: "interview_questions",
                column: "interview_id");

            migrationBuilder.CreateIndex(
                name: "ix_interview_turns_evaluation_id",
                table: "interview_turns",
                column: "evaluation_id");

            migrationBuilder.CreateIndex(
                name: "ix_interview_turns_interview_question_id",
                table: "interview_turns",
                column: "interview_question_id");

            migrationBuilder.CreateIndex(
                name: "ix_interview_turns_question_id",
                table: "interview_turns",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_interviews_candidate_id",
                table: "interviews",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_interviews_opportunity_id",
                table: "interviews",
                column: "opportunity_id");

            migrationBuilder.CreateIndex(
                name: "ix_interviews_repository_id",
                table: "interviews",
                column: "repository_id");

            migrationBuilder.CreateIndex(
                name: "ix_learning_resources_competency_id",
                table: "learning_resources",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_organisation_id",
                table: "opportunities",
                column: "organisation_id");

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_competencies_competency_id",
                table: "opportunity_competencies",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_competencies_opportunity_id",
                table: "opportunity_competencies",
                column: "opportunity_id");

            migrationBuilder.CreateIndex(
                name: "ix_organisation_members_organisation_id",
                table: "organisation_members",
                column: "organisation_id");

            migrationBuilder.CreateIndex(
                name: "ix_organisation_members_user_id",
                table: "organisation_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_passport_credentials_passport_id",
                table: "passport_credentials",
                column: "passport_id");

            migrationBuilder.CreateIndex(
                name: "ix_passport_credentials_skills_passport_id",
                table: "passport_credentials",
                column: "skills_passport_id");

            migrationBuilder.CreateIndex(
                name: "ix_passport_credentials_verification_record_id",
                table: "passport_credentials",
                column: "verification_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_passport_shares_passport_id",
                table: "passport_shares",
                column: "passport_id");

            migrationBuilder.CreateIndex(
                name: "ix_passport_shares_skills_passport_id",
                table: "passport_shares",
                column: "skills_passport_id");

            migrationBuilder.CreateIndex(
                name: "ix_repositories_candidate_id",
                table: "repositories",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_repository_analyses_execution_report_id",
                table: "repository_analyses",
                column: "execution_report_id");

            migrationBuilder.CreateIndex(
                name: "ix_repository_analyses_repository_id",
                table: "repository_analyses",
                column: "repository_id");

            migrationBuilder.CreateIndex(
                name: "ix_repository_findings_analysis_id",
                table: "repository_findings",
                column: "analysis_id");

            migrationBuilder.CreateIndex(
                name: "ix_repository_findings_competency_id",
                table: "repository_findings",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "ix_repository_findings_repository_analysis_id",
                table: "repository_findings",
                column: "repository_analysis_id");

            migrationBuilder.CreateIndex(
                name: "ix_rubric_criteria_assessment_id",
                table: "rubric_criteria",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_skills_passports_candidate_id",
                table: "skills_passports",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_verification_records_assessment_id",
                table: "verification_records",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_verification_records_assessor_id",
                table: "verification_records",
                column: "assessor_id");

            migrationBuilder.CreateIndex(
                name: "ix_verification_records_candidate_id",
                table: "verification_records",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_verification_records_competency_id",
                table: "verification_records",
                column: "competency_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "assessment_competencies");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "candidate_profiles");

            migrationBuilder.DropTable(
                name: "competencies");

            migrationBuilder.DropTable(
                name: "competency_evidence");

            migrationBuilder.DropTable(
                name: "competency_level_criteria");

            migrationBuilder.DropTable(
                name: "consent_records");

            migrationBuilder.DropTable(
                name: "eligibility_rules");

            migrationBuilder.DropTable(
                name: "experience_competencies");

            migrationBuilder.DropTable(
                name: "gap_report_items");

            migrationBuilder.DropTable(
                name: "git_hub_connections");

            migrationBuilder.DropTable(
                name: "integrity_events");

            migrationBuilder.DropTable(
                name: "interview_turns");

            migrationBuilder.DropTable(
                name: "learning_resources");

            migrationBuilder.DropTable(
                name: "opportunity_competencies");

            migrationBuilder.DropTable(
                name: "organisation_members");

            migrationBuilder.DropTable(
                name: "organisations");

            migrationBuilder.DropTable(
                name: "passport_credentials");

            migrationBuilder.DropTable(
                name: "passport_shares");

            migrationBuilder.DropTable(
                name: "repositories");

            migrationBuilder.DropTable(
                name: "repository_findings");

            migrationBuilder.DropTable(
                name: "rubric_criteria");

            migrationBuilder.DropTable(
                name: "verification_records");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "candidate_competencies");

            migrationBuilder.DropTable(
                name: "experience_records");

            migrationBuilder.DropTable(
                name: "gap_reports");

            migrationBuilder.DropTable(
                name: "assessment_sessions");

            migrationBuilder.DropTable(
                name: "interview_evaluations");

            migrationBuilder.DropTable(
                name: "interview_questions");

            migrationBuilder.DropTable(
                name: "opportunities");

            migrationBuilder.DropTable(
                name: "skills_passports");

            migrationBuilder.DropTable(
                name: "repository_analyses");

            migrationBuilder.DropTable(
                name: "assessments");

            migrationBuilder.DropTable(
                name: "assessment_submissions");

            migrationBuilder.DropTable(
                name: "interviews");

            migrationBuilder.DropTable(
                name: "execution_reports");

            migrationBuilder.DropTable(
                name: "assessment_reviews");
        }
    }
}
