using CodeNexus.Domain.Enums;
using System;

namespace CodeNexus.Domain.Entities
{
    public class OtpVerification
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string OtpHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int AttemptCount { get; set; }
        public int ResendCount { get; set; }
        public OtpPurpose Purpose { get; set; } = OtpPurpose.Register;
        public DateTime? LastResendAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
