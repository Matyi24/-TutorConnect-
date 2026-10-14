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

    internal class BookingDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "";
        [JsonPropertyName("start_time")] public DateTime StartTime { get; set; }
        [JsonPropertyName("end_time")] public DateTime EndTime { get; set; }
        [JsonPropertyName("price")] public decimal? Price { get; set; }
        [JsonPropertyName("note")] public string? Note { get; set; }
        [JsonPropertyName("as_student")] public bool AsStudent { get; set; }
        [JsonPropertyName("other_name")] public string? OtherName { get; set; }
        [JsonPropertyName("subject_name")] public string? SubjectName { get; set; }
        [JsonPropertyName("review")] public ReviewDto? Review { get; set; }
        [JsonPropertyName("can_confirm")] public bool CanConfirm { get; set; }
        [JsonPropertyName("can_reject")] public bool CanReject { get; set; }
        [JsonPropertyName("can_cancel")] public bool CanCancel { get; set; }
    }

    internal class AccountSubjectDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("hourly_rate")] public decimal HourlyRate { get; set; }
    }

    internal class NamedDto
    {
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

        [JsonIgnore] public bool IsBooked => IsBookedValue != 0;
    }

    internal class AvailabilityResultDto
    {
        [JsonPropertyName("created")] public int Created { get; set; }
        [JsonPropertyName("skipped")] public int Skipped { get; set; }
    }
}
