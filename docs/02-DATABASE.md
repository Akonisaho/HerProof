# HerProof — Database Design

PostgreSQL + EF Core. snake_case table and column names (use `UseSnakeCaseNamingConvention`). UUID primary keys. UTC timestamps.

## Rules
1. **Append-only:** `verification_records`, `assessment_reviews`, `consent_records`, `audit_log`, `integrity_events`, `interview_turns`. No UPDATE or DELETE from application code. Enforce with a DB role that lacks those privileges, or triggers.
2. **Candidate edits never rewrite history.** Profile changes do not touch verified records.
3. **Secrets:** GitHub tokens stored encrypted (ASP.NET Data Protection or KMS-wrapped key). Column holds ciphertext only.
4. **Soft delete only for user-facing content.** Personal data deletion requests go through the retention job.
5. **Every FK has an index.** Unique constraints: user email, passport number, credential identifier, (opportunity_id, competency_id).
6. **Enums** stored as strings with check constraints (readable in audits).

## ERD 1 — Identity, profile, organisations

```mermaid
erDiagram
  USERS ||--o| CANDIDATE_PROFILES : has
  USERS ||--o{ ORGANISATION_MEMBERS : joins
  ORGANISATIONS ||--o{ ORGANISATION_MEMBERS : has
  USERS ||--o{ CONSENT_RECORDS : gives
  USERS ||--o{ AUDIT_LOG : acts
  USERS {
    uuid id PK
    string full_name
    string email UK
    string role
    bool is_active
    datetime created_at
  }
  CANDIDATE_PROFILES {
    uuid id PK
    uuid user_id FK
    string province
    string education_level
    string gender_declared
    string household_income_band
    text career_interests
    string github_profile
    string linkedin_profile
  }
  ORGANISATIONS {
    uuid id PK
    string name
    string industry
    string website
    string verification_status
  }
  ORGANISATION_MEMBERS {
    uuid id PK
    uuid organisation_id FK
    uuid user_id FK
    string organisation_role
  }
  CONSENT_RECORDS {
    uuid id PK
    uuid user_id FK
    string purpose
    string version
    bool granted
    datetime recorded_at
  }
  AUDIT_LOG {
    uuid id PK
    uuid actor_user_id FK
    string action
    string entity_type
    uuid entity_id
    jsonb detail
    datetime at
  }
```

`gender_declared` and `household_income_band` are optional, collected only for bursary eligibility with explicit consent, and never used for AI scoring.

## ERD 2 — Opportunities, bursaries, competencies, gap

```mermaid
erDiagram
  ORGANISATIONS ||--o{ OPPORTUNITIES : posts
  OPPORTUNITIES ||--o{ OPPORTUNITY_COMPETENCIES : requires
  OPPORTUNITIES ||--o{ ELIGIBILITY_RULES : defines
  OPPORTUNITIES ||--o{ APPLICATIONS : receives
  COMPETENCIES ||--o{ OPPORTUNITY_COMPETENCIES : used_in
  COMPETENCIES ||--o{ COMPETENCY_LEVEL_CRITERIA : defines
  CANDIDATE_PROFILES ||--o{ CANDIDATE_COMPETENCIES : holds
  COMPETENCIES ||--o{ CANDIDATE_COMPETENCIES : of
  CANDIDATE_COMPETENCIES ||--o{ COMPETENCY_EVIDENCE : supported_by
  CANDIDATE_PROFILES ||--o{ GAP_REPORTS : receives
  OPPORTUNITIES ||--o{ GAP_REPORTS : for
  GAP_REPORTS ||--o{ GAP_REPORT_ITEMS : contains
  COMPETENCIES ||--o{ LEARNING_RESOURCES : taught_by
  OPPORTUNITIES {
    uuid id PK
    uuid organisation_id FK
    string title
    text description
    string opportunity_type
    string location
    datetime deadline
    string status
  }
  OPPORTUNITY_COMPETENCIES {
    uuid id PK
    uuid opportunity_id FK
    uuid competency_id FK
    int required_level
    bool is_mandatory
  }
  ELIGIBILITY_RULES {
    uuid id PK
    uuid opportunity_id FK
    string rule_type
    string operator
    string value
    bool is_hard_filter
    text explanation
  }
  APPLICATIONS {
    uuid id PK
    uuid opportunity_id FK
    uuid candidate_id FK
    string status
    jsonb shared_credentials
    datetime submitted_at
  }
  COMPETENCIES {
    uuid id PK
    string name
    string category
    string framework_version
    bool is_active
  }
  COMPETENCY_LEVEL_CRITERIA {
    uuid id PK
    uuid competency_id FK
    int level
    text observable_criteria
  }
  CANDIDATE_COMPETENCIES {
    uuid id PK
    uuid candidate_id FK
    uuid competency_id FK
    int declared_level
    int verified_level
    string status
    datetime updated_at
  }
  COMPETENCY_EVIDENCE {
    uuid id PK
    uuid candidate_competency_id FK
    string evidence_type
    string evidence_reference
    text description
    datetime created_at
  }
  GAP_REPORTS {
    uuid id PK
    uuid candidate_id FK
    uuid opportunity_id FK
    datetime generated_at
  }
  GAP_REPORT_ITEMS {
    uuid id PK
    uuid gap_report_id FK
    uuid competency_id FK
    int required_level
    int demonstrated_level
    string evidence_state
    text explanation
    uuid learning_resource_id FK
  }
  LEARNING_RESOURCES {
    uuid id PK
    uuid competency_id FK
    string title
    string url
    string cost_type
    int target_level
  }
```

`evidence_state` is one of: `Demonstrated`, `ClaimedNotAssessed`, `NotDemonstrated`, `NoEvidence`. `demonstrated_level` is nullable. Null means no assessed evidence, never zero.
Eligibility rule results are computed as Met, NotMet or Unknown. Unknown arises when the candidate has not provided the data.

## ERD 3a — Experience and repositories

```mermaid
erDiagram
  CANDIDATE_PROFILES ||--o{ EXPERIENCE_RECORDS : records
  EXPERIENCE_RECORDS ||--o{ EXPERIENCE_COMPETENCIES : suggests
  COMPETENCIES ||--o{ EXPERIENCE_COMPETENCIES : mapped_to
  CANDIDATE_PROFILES ||--o{ GITHUB_CONNECTIONS : connects
  CANDIDATE_PROFILES ||--o{ REPOSITORIES : submits
  REPOSITORIES ||--o{ REPOSITORY_ANALYSES : analysed_by
  REPOSITORY_ANALYSES ||--o{ REPOSITORY_FINDINGS : yields
  REPOSITORY_ANALYSES ||--o| EXECUTION_REPORTS : has
  EXPERIENCE_RECORDS {
    uuid id PK
    uuid candidate_id FK
    string category
    string title
    text description
    date start_date
    date end_date
  }
  EXPERIENCE_COMPETENCIES {
    uuid id PK
    uuid experience_record_id FK
    uuid competency_id FK
    string mapping_status
  }
  GITHUB_CONNECTIONS {
    uuid id PK
    uuid candidate_id FK
    string external_account_id
    bytes encrypted_token
    datetime connected_at
    bool is_active
  }
  REPOSITORIES {
    uuid id PK
    uuid candidate_id FK
    string repository_name
    string repository_url
    string branch
    string commit_sha
    string visibility
    datetime submitted_at
  }
  REPOSITORY_ANALYSES {
    uuid id PK
    uuid repository_id FK
    string status
    jsonb technologies_detected
    text summary
    string model_version
    string prompt_version
    datetime created_at
    datetime completed_at
  }
  REPOSITORY_FINDINGS {
    uuid id PK
    uuid analysis_id FK
    uuid competency_id FK
    string finding_type
    string file_path
    string line_reference
    text description
    text evidence
    string confidence
  }
  EXECUTION_REPORTS {
    uuid id PK
    uuid analysis_id FK
    bool restore_ok
    bool build_ok
    int tests_passed
    int tests_failed
    text log_excerpt
    string sandbox_image_digest
  }
```

## ERD 3b — Interviews

```mermaid
erDiagram
  REPOSITORIES ||--o{ INTERVIEWS : basis_for
  INTERVIEWS ||--o{ INTERVIEW_QUESTIONS : asks
  INTERVIEW_QUESTIONS ||--o{ INTERVIEW_TURNS : contains
  INTERVIEW_TURNS ||--o| INTERVIEW_EVALUATIONS : evaluated_by
  INTERVIEWS {
    uuid id PK
    uuid candidate_id FK
    uuid repository_id FK
    uuid opportunity_id FK
    string mode
    string status
    string interviewer_persona
    datetime started_at
    datetime completed_at
  }
  INTERVIEW_QUESTIONS {
    uuid id PK
    uuid interview_id FK
    uuid competency_id FK
    text question_text
    string snippet_file_path
    string snippet_lines
    int question_order
  }
  INTERVIEW_TURNS {
    uuid id PK
    uuid question_id FK
    int turn_index
    string turn_kind
    text prompt_text
    text transcript_raw
    text transcript_confirmed
    string recording_reference
    int duration_seconds
    datetime created_at
  }
  INTERVIEW_EVALUATIONS {
    uuid id PK
    uuid turn_id FK
    string verdict
    text evidence_summary
    jsonb code_claims_vs_answer
    string reviewer_status
  }
```


`mode` is `Voice` or `Text`. `verdict` is `Understood`, `Partial` or `NotDemonstrated`. `turn_kind` is `Question`, `Probe` or `Clarify`.

## ERD 4 — Assessments, integrity, verification, passport

```mermaid
erDiagram
  ASSESSMENTS ||--o{ ASSESSMENT_COMPETENCIES : covers
  ASSESSMENTS ||--o{ RUBRIC_CRITERIA : scored_by
  ASSESSMENTS ||--o{ ASSESSMENT_SESSIONS : run_as
  ASSESSMENT_SESSIONS ||--o{ INTEGRITY_EVENTS : records
  ASSESSMENT_SESSIONS ||--o| ASSESSMENT_SUBMISSIONS : produces
  ASSESSMENT_SUBMISSIONS ||--o{ ASSESSMENT_REVIEWS : reviewed_in
  CANDIDATE_PROFILES ||--o{ VERIFICATION_RECORDS : receives
  COMPETENCIES ||--o{ VERIFICATION_RECORDS : about
  CANDIDATE_PROFILES ||--|| SKILLS_PASSPORTS : owns
  SKILLS_PASSPORTS ||--o{ PASSPORT_CREDENTIALS : lists
  VERIFICATION_RECORDS ||--o{ PASSPORT_CREDENTIALS : backs
  SKILLS_PASSPORTS ||--o{ PASSPORT_SHARES : shared_by
  ASSESSMENTS {
    uuid id PK
    string title
    text description
    string assessment_type
    int duration_minutes
    uuid created_by FK
    string status
  }
  ASSESSMENT_COMPETENCIES {
    uuid id PK
    uuid assessment_id FK
    uuid competency_id FK
    decimal weight
  }
  RUBRIC_CRITERIA {
    uuid id PK
    uuid assessment_id FK
    string name
    decimal weight
    text descriptor
  }
  ASSESSMENT_SESSIONS {
    uuid id PK
    uuid assessment_id FK
    uuid candidate_id FK
    string status
    datetime consent_at
    datetime started_at
    datetime submitted_at
    bool accommodation_granted
  }
  INTEGRITY_EVENTS {
    uuid id PK
    uuid session_id FK
    string event_type
    datetime occurred_at
    jsonb metadata
    string client_seq
  }
  ASSESSMENT_SUBMISSIONS {
    uuid id PK
    uuid session_id FK
    string submission_reference
    jsonb test_results
    datetime submitted_at
  }
  ASSESSMENT_REVIEWS {
    uuid id PK
    uuid submission_id FK
    uuid assessor_id FK
    jsonb criterion_scores
    text feedback
    string review_status
    datetime reviewed_at
  }
  VERIFICATION_RECORDS {
    uuid id PK
    uuid candidate_id FK
    uuid competency_id FK
    uuid assessment_id FK
    int final_level
    uuid assessor_id FK
    string decision
    text evidence_summary
    datetime created_at
    uuid supersedes_id FK
  }
  SKILLS_PASSPORTS {
    uuid id PK
    uuid candidate_id FK
    string passport_number UK
    string status
    datetime created_at
  }
  PASSPORT_CREDENTIALS {
    uuid id PK
    uuid passport_id FK
    uuid verification_record_id FK
    string credential_identifier UK
    datetime issued_at
    datetime valid_until
    string credential_status
  }
  PASSPORT_SHARES {
    uuid id PK
    uuid passport_id FK
    string token_hash
    jsonb visible_fields
    datetime created_at
    datetime revoked_at
  }
```

`verification_records.decision` is `Verified`, `Developing` or `NotDemonstrated`. A new decision inserts a row with `supersedes_id`, it never edits the old one.
`passport_shares.token_hash` stores a hash of the share token. The raw token appears only in the URL given to the candidate.
`integrity_events.client_seq` is a per-session monotonic counter so the server can detect gaps and duplicates after offline sync.

## Seed data (migrations or startup seeder)
- Competency categories and the five levels (0-4) with observable criteria per competency.
- Software competencies: C#, OOP, SQL, ASP.NET Core, API development, Git, Problem-solving, Testing, Dependency injection, Error handling.
- Non-traditional competencies: Financial management, Scheduling and planning, Project coordination, Business operations, Resource management, Team coordination.
- Learning resources per competency (curated, free where possible).
- Demo data: four candidates, one recruiter, one assessor, one bursary partner, three opportunities, two bursaries.
