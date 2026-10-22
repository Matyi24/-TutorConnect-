using System.Text.Json.Serialization;

namespace TutorConnectApp
{
    // Az admin végpontok (vsc/js/moderation.js, /api/admin/...) JSON válaszainak alakjai.
    // A számok szövegként is érkezhetnek (MySQL összesítések), ezért mindenhol engedjük az olvasásukat.

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    internal class AdminUserCounts
    {
        [JsonPropertyName("total")] public int Total { get; set; }
        [JsonPropertyName("students")] public int Students { get; set; }
        [JsonPropertyName("tutors")] public int Tutors { get; set; }
        [JsonPropertyName("admins")] public int Admins { get; set; }
        [JsonPropertyName("new_week")] public int NewWeek { get; set; }
        [JsonPropertyName("unverified")] public int Unverified { get; set; }
        [JsonPropertyName("suspended")] public int Suspended { get; set; }
    }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    internal class AdminReviewCounts
    {
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("average")] public double? Average { get; set; }
    }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    internal class AdminReportCounts
    {
        [JsonPropertyName("open")] public int Open { get; set; }
        [JsonPropertyName("total")] public int Total { get; set; }
    }

    internal class AdminLogItem
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("action")] public string Action { get; set; } = "";
        [JsonPropertyName("target_type")] public string? TargetType { get; set; }
        [JsonPropertyName("details")] public string? Details { get; set; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("admin_name")] public string? AdminName { get; set; }
    }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    internal class AdminStatsDto
    {
        [JsonPropertyName("users")] public AdminUserCounts Users { get; set; } = new();
        [JsonPropertyName("bookings")] public Dictionary<string, int> Bookings { get; set; } = new();
        [JsonPropertyName("reviews")] public AdminReviewCounts Reviews { get; set; } = new();
        [JsonPropertyName("reports")] public AdminReportCounts Reports { get; set; } = new();
        [JsonPropertyName("messages")] public int Messages { get; set; }
        [JsonPropertyName("log")] public List<AdminLogItem> Log { get; set; } = new();
    }

    /// <summary>Egy oldalnyi találat az admin listákból.</summary>
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    internal class AdminPage<T>
    {
        [JsonPropertyName("items")] public List<T> Items { get; set; } = new();
        [JsonPropertyName("total")] public int Total { get; set; }
        [JsonPropertyName("page")] public int Page { get; set; } = 1;
        [JsonPropertyName("pageSize")] public int PageSize { get; set; } = 20;
    }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    internal class AdminReport
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("reason")] public string Reason { get; set; } = "";
        [JsonPropertyName("details")] public string? Details { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "";
        [JsonPropertyName("admin_note")] public string? AdminNote { get; set; }
        [JsonPropertyName("message_id")] public int? MessageId { get; set; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("handled_at")] public DateTime? HandledAt { get; set; }
        [JsonPropertyName("reporter_id")] public int ReporterId { get; set; }
        [JsonPropertyName("reporter_name")] public string? ReporterName { get; set; }
        [JsonPropertyName("reported_user_id")] public int ReportedUserId { get; set; }
        [JsonPropertyName("reported_name")] public string? ReportedName { get; set; }
        [JsonPropertyName("reported_role")] public string? ReportedRole { get; set; }
        [JsonPropertyName("reported_suspended")] public DateTime? ReportedSuspended { get; set; }
        [JsonPropertyName("handled_by_name")] public string? HandledByName { get; set; }
        [JsonPropertyName("other_reports")] public int OtherReports { get; set; }
    }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    internal class AdminReportContext
    {
        [JsonPropertyName("sender_id")] public int SenderId { get; set; }
        [JsonPropertyName("content")] public string? Content { get; set; }
        [JsonPropertyName("retracted")] public bool Retracted { get; set; }
        [JsonPropertyName("attachment_name")] public string? AttachmentName { get; set; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("target")] public bool Target { get; set; }
    }

    internal class AdminReportDetail
    {
        [JsonPropertyName("report")] public AdminReport Report { get; set; } = new();
        [JsonPropertyName("context")] public List<AdminReportContext> Context { get; set; } = new();
    }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    internal class AdminUser
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("full_name")] public string FullName { get; set; } = "";
        [JsonPropertyName("email")] public string Email { get; set; } = "";
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("email_verified_at")] public DateTime? EmailVerifiedAt { get; set; }
        [JsonPropertyName("suspended_at")] public DateTime? SuspendedAt { get; set; }
        [JsonPropertyName("suspended_reason")] public string? SuspendedReason { get; set; }
        [JsonPropertyName("deleted_at")] public DateTime? DeletedAt { get; set; }
        [JsonPropertyName("open_reports")] public int OpenReports { get; set; }
        [JsonPropertyName("all_reports")] public int AllReports { get; set; }
    }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    internal class AdminReview
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("rating")] public int Rating { get; set; }
        [JsonPropertyName("comment")] public string? Comment { get; set; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("student_name")] public string? StudentName { get; set; }
        [JsonPropertyName("tutor_name")] public string? TutorName { get; set; }
    }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    internal class AdminBooking
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "";
        [JsonPropertyName("start_time")] public DateTime? StartTime { get; set; }
        [JsonPropertyName("end_time")] public DateTime? EndTime { get; set; }
        [JsonPropertyName("price")] public decimal? Price { get; set; }
        [JsonPropertyName("student_name")] public string? StudentName { get; set; }
        [JsonPropertyName("tutor_name")] public string? TutorName { get; set; }
        [JsonPropertyName("subject_name")] public string? SubjectName { get; set; }
    }
}
