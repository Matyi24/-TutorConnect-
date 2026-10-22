using System.Text.Json.Serialization;

namespace TutorConnectApp
{
    // A szerver (vsc/js/main.js) JSON válaszainak alakjai.

    internal class SubjectDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("category")] public string Category { get; set; } = "";
    }

    internal class TutorDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("full_name")] public string FullName { get; set; } = "";
        [JsonPropertyName("email")] public string Email { get; set; } = "";
        [JsonPropertyName("bio")] public string? Bio { get; set; }
        [JsonPropertyName("hourly_rate")] public decimal HourlyRate { get; set; }
        [JsonPropertyName("average_rating")] public decimal AverageRating { get; set; }
        [JsonPropertyName("review_count")] public int ReviewCount { get; set; }
        [JsonPropertyName("subjects")] public string? Subjects { get; set; }

        /// <summary>"id|név|ár;;id|név|ár" formátumú lista.</summary>
        [JsonPropertyName("subject_prices")] public string? SubjectPrices { get; set; }
    }

    internal class StatsDto
    {
        [JsonPropertyName("tutors")] public int Tutors { get; set; }
        [JsonPropertyName("bookings")] public int Bookings { get; set; }
        [JsonPropertyName("satisfaction")] public int Satisfaction { get; set; }
    }

    internal class BookingSummaryDto
    {
        [JsonPropertyName("pending")] public int Pending { get; set; }
        [JsonPropertyName("upcoming")] public int Upcoming { get; set; }
        [JsonPropertyName("cancelled")] public int Cancelled { get; set; }
        [JsonPropertyName("past")] public int Past { get; set; }
    }

    internal class ReviewDto
    {
        [JsonPropertyName("rating")] public int Rating { get; set; }
        [JsonPropertyName("comment")] public string Comment { get; set; } = "";
    }

    /// <summary>A diák profilja az oktatónak (GET /api/students/:id/profile; e-mail cím nélkül).</summary>
    internal class StudentProfileDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("school_level")] public string? SchoolLevel { get; set; }
        [JsonPropertyName("grade")] public string? Grade { get; set; }
        [JsonPropertyName("subjects")] public List<string> Subjects { get; set; } = new();
    }

    /// <summary>Egy értékelés az oktatóról (GET /api/tutors/:id/reviews).</summary>
    internal class ReviewItemDto
    {
        [JsonIgnore]
        public DateTime? CreatedAt => DateTime.TryParse(CreatedAtRaw, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var value)
            ? value
            : null;

        [JsonPropertyName("rating")] public int Rating { get; set; }
        [JsonPropertyName("comment")] public string? Comment { get; set; }
        // az időpont hiányozhat vagy szövegként érkezhet: nyersen olvassuk, és hibatűrően értelmezzük
        [JsonPropertyName("created_at")] public string? CreatedAtRaw { get; set; }
        [JsonPropertyName("reviewer_name")] public string? ReviewerName { get; set; }
    }

    internal class BookingDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "";
        [JsonPropertyName("start_time")] public DateTime StartTime { get; set; }
        [JsonPropertyName("end_time")] public DateTime EndTime { get; set; }
        [JsonPropertyName("price")] public decimal? Price { get; set; }
        [JsonPropertyName("note")] public string? Note { get; set; }
        [JsonPropertyName("as_student")] public bool AsStudent { get; set; }
        [JsonPropertyName("other_id")] public int OtherId { get; set; }
        [JsonPropertyName("other_name")] public string? OtherName { get; set; }
        [JsonPropertyName("subject_name")] public string? SubjectName { get; set; }
        [JsonPropertyName("review")] public ReviewDto? Review { get; set; }
        [JsonPropertyName("can_confirm")] public bool CanConfirm { get; set; }
        [JsonPropertyName("can_reject")] public bool CanReject { get; set; }
        [JsonPropertyName("can_cancel")] public bool CanCancel { get; set; }
    }

    internal class AccountSubjectDto
    {
        [JsonPropertyName("subject_id")] public int SubjectId { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("hourly_rate")] public decimal HourlyRate { get; set; }
    }

    internal class NamedDto
    {
        [JsonPropertyName("subject_id")] public int SubjectId { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
    }

    internal class AccountDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("email")] public string Email { get; set; } = "";
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("bio")] public string Bio { get; set; } = "";
        [JsonPropertyName("subjects")] public List<AccountSubjectDto>? Subjects { get; set; }
        [JsonPropertyName("school_level")] public string? SchoolLevel { get; set; }
        [JsonPropertyName("grade")] public string? Grade { get; set; }
        [JsonPropertyName("learning_subjects")] public List<NamedDto>? LearningSubjects { get; set; }
    }

    internal class SlotDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("start_time")] public DateTime StartTime { get; set; }
        [JsonPropertyName("end_time")] public DateTime EndTime { get; set; }
    }

    internal class SlotSubjectDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("hourly_rate")] public decimal HourlyRate { get; set; }
    }

    internal class AvailabilityDto
    {
        [JsonPropertyName("slots")] public List<SlotDto> Slots { get; set; } = new();
        [JsonPropertyName("subjects")] public List<SlotSubjectDto> Subjects { get; set; } = new();
    }

    /// <summary>Az oktató saját időpontja (GET /api/availability).</summary>
    internal class SlotItemDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("start_time")] public DateTime StartTime { get; set; }
        [JsonPropertyName("end_time")] public DateTime EndTime { get; set; }

        /// <summary>A MySQL tinyint(1) mezőt számként (0/1) adja vissza a szerver.</summary>
        [JsonPropertyName("is_booked")] public int IsBookedValue { get; set; }

        /// <summary>Hány diák jelentkezett erre az időpontra (az oktató közülük választ); régebbi szervernél hiányzik.</summary>
        [JsonPropertyName("requests")] public int Requests { get; set; }

        [JsonIgnore] public bool IsBooked => IsBookedValue != 0;
    }

    internal class AvailabilityResultDto
    {
        [JsonPropertyName("created")] public int Created { get; set; }
        [JsonPropertyName("skipped")] public int Skipped { get; set; }
    }

    /// <summary>A PUT /api/account válasza: a mentett felhasználó adatai.</summary>
    internal class AccountUpdateDto
    {
        [JsonPropertyName("user")] public UpdatedUserDto? User { get; set; }
    }

    internal class UpdatedUserDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("email")] public string Email { get; set; } = "";
        [JsonPropertyName("role")] public string Role { get; set; } = "";
    }

    // ------------------------------------------------------------
    // Chat
    // ------------------------------------------------------------

    internal class ConversationDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("student_id")] public int StudentId { get; set; }
        [JsonPropertyName("tutor_id")] public int TutorId { get; set; }
        [JsonPropertyName("other_user_name")] public string OtherUserName { get; set; } = "";
        [JsonPropertyName("last_message")] public string? LastMessage { get; set; }
        [JsonPropertyName("last_message_time")] public DateTime? LastMessageTime { get; set; }

        /// <summary>A MySQL logikai értéke számként (0/1) érkezik.</summary>
        [JsonPropertyName("last_message_retracted")] public int? LastMessageRetractedValue { get; set; }

        [JsonPropertyName("unread_count")] public int UnreadCount { get; set; }

        [JsonIgnore] public bool LastMessageRetracted => LastMessageRetractedValue == 1;
    }

    internal class MessageDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("conversation_id")] public int ConversationId { get; set; }
        [JsonPropertyName("sender_id")] public int SenderId { get; set; }
        [JsonPropertyName("content")] public string Content { get; set; } = "";
        [JsonPropertyName("is_read")] public int? IsReadValue { get; set; }
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
        [JsonPropertyName("attachment_name")] public string? AttachmentName { get; set; }
        [JsonPropertyName("attachment_type")] public string? AttachmentType { get; set; }
        [JsonPropertyName("attachment_size")] public long? AttachmentSize { get; set; }
        [JsonPropertyName("deleted_at")] public DateTime? DeletedAt { get; set; }
        [JsonPropertyName("edited_at")] public DateTime? EditedAt { get; set; }

        [JsonIgnore] public bool IsRead => IsReadValue == 1;
        [JsonIgnore] public bool IsRetracted => DeletedAt != null;
        [JsonIgnore] public bool HasAttachment => !string.IsNullOrEmpty(AttachmentName);

        /// <summary>Csak az appban él: a küldés állapota (a szerver üzenetei mind elküldöttek).</summary>
        [JsonIgnore] public SendState Send { get; set; } = SendState.Sent;
    }

    /// <summary>Az üzenet küldésének állapota: a saját üzenet azonnal megjelenik, a szerver válaszáig "küldés alatt" áll.</summary>
    internal enum SendState
    {
        Sent,
        Sending,
        Failed
    }
}

