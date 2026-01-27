# Design Document: User Registration with OTP Verification

## Overview

Hệ thống đăng ký người dùng với xác thực OTP qua email. Thiết kế theo Clean Architecture template của Jason Taylor - sử dụng `IApplicationDbContext` trực tiếp thay vì Repository pattern, kết hợp với CQRS pattern (Commands/Queries) qua MediatR.

## Architecture

```mermaid
sequenceDiagram
    participant U as User
    participant API as AuthController
    participant M as MediatR
    participant CH as CommandHandler
    participant DB as IApplicationDbContext
    participant ES as IEmailService

    U->>API: POST /api/auth/register
    API->>M: Send(RegisterCommand)
    M->>CH: Handle(RegisterCommand)
    CH->>DB: Check email/username uniqueness
    CH->>CH: Generate & Hash OTP
    CH->>DB: Add OtpVerification
    CH->>DB: SaveChangesAsync()
    CH->>ES: SendOtpEmailAsync()
    CH-->>M: Result.Success()
    M-->>API: Result
    API-->>U: 200 OK

    U->>API: POST /api/auth/verify-otp
    API->>M: Send(VerifyOtpCommand)
    M->>CH: Handle(VerifyOtpCommand)
    CH->>DB: Get OtpVerification
    CH->>CH: Verify OTP & Expiration
    CH->>DB: Add User
    CH->>DB: Remove OtpVerification
    CH->>DB: SaveChangesAsync()
    CH-->>M: Result<UserDto>
    M-->>API: Result
    API-->>U: 201 Created
```

## Components and Interfaces

### Domain Layer (CodeNexus.Domain)

```csharp
// Entities/OtpVerification.cs
public class OtpVerification
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string OtpHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public int ResendCount { get; set; }
    public DateTime? LastResendAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
```

### Application Layer (CodeNexus.Application)

```csharp
// Common/Interfaces/IApplicationDbContext.cs
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<OtpVerification> OtpVerifications { get; }
    DbSet<Role> Roles { get; }
    // ... other DbSets
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

// Common/Interfaces/IEmailService.cs
public interface IEmailService
{
    Task SendOtpEmailAsync(string email, string otp, CancellationToken cancellationToken = default);
}

// Features/Auth/Commands/Register/RegisterCommand.cs
public record RegisterCommand(string Email, string Username, string Password) : IRequest<Result>;

// Features/Auth/Commands/Register/RegisterCommandHandler.cs
public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    // ...
}

// Features/Auth/Commands/Register/RegisterCommandValidator.cs
public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    // FluentValidation rules
}

// Features/Auth/Commands/VerifyOtp/VerifyOtpCommand.cs
public record VerifyOtpCommand(string Email, string Otp) : IRequest<Result<UserDto>>;

// Features/Auth/Commands/ResendOtp/ResendOtpCommand.cs
public record ResendOtpCommand(string Email) : IRequest<Result>;
```

### Infrastructure Layer (CodeNexus.Infrastructure)

```csharp
// Persistence/AppDbContext.cs - Implements IApplicationDbContext
public class AppDbContext : DbContext, IApplicationDbContext
{
    // DbSets...
}

// Services/EmailService.cs - Gmail SMTP implementation
public class EmailService : IEmailService
{
    // Implementation
}
```

### API Layer (CodeNexus.API)

```csharp
// Controllers/AuthController.cs
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterCommand command)
        => (await _sender.Send(command)).ToActionResult();

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpCommand command)
        => (await _sender.Send(command)).ToActionResult();

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp(ResendOtpCommand command)
        => (await _sender.Send(command)).ToActionResult();
}
```

## Data Models

### OtpVerification Table

| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| Email | nvarchar(256) | User email (unique index) |
| Username | nvarchar(50) | Desired username |
| PasswordHash | nvarchar(256) | Hashed password |
| OtpHash | nvarchar(256) | Hashed OTP |
| ExpiresAt | datetime2 | OTP expiration time |
| AttemptCount | int | Failed verification attempts |
| ResendCount | int | Number of resend requests |
| LastResendAt | datetime2 | Last resend timestamp |
| CreatedAt | datetime2 | Record creation time |

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Invalid email format rejection
*For any* string that does not match email format (regex: ^[^@\s]+@[^@\s]+\.[^@\s]+$), the registration SHALL be rejected with validation error.
**Validates: Requirements 1.2**

### Property 2: Password complexity validation
*For any* password that does not contain at least 8 characters, 1 uppercase, 1 lowercase, and 1 number, the registration SHALL be rejected with validation error.
**Validates: Requirements 1.5**

### Property 3: OTP generation format
*For any* valid registration request, the generated OTP SHALL be exactly 6 numeric digits.
**Validates: Requirements 2.1**

### Property 4: OTP storage security
*For any* generated OTP, the stored value in database SHALL NOT equal the plaintext OTP (must be hashed).
**Validates: Requirements 2.2**

### Property 5: OTP expiration setting
*For any* generated OTP, the ExpiresAt timestamp SHALL be exactly 5 minutes after CreatedAt.
**Validates: Requirements 2.3**

### Property 6: Valid OTP verification creates user
*For any* valid OTP submitted within expiration time, the system SHALL create a user with matching email and username.
**Validates: Requirements 3.1**

### Property 7: Invalid OTP rejection
*For any* OTP that does not match the stored hash, verification SHALL be rejected.
**Validates: Requirements 3.2**

### Property 8: OTP cleanup after verification
*For any* successful OTP verification, the OtpVerification record SHALL be removed from database.
**Validates: Requirements 3.5, 5.2**

### Property 9: Previous OTP invalidation on resend
*For any* OTP resend request, the previous OTP for that email SHALL no longer be valid for verification.
**Validates: Requirements 4.2**

## Error Handling

| Error Code | HTTP Status | Description |
|------------|-------------|-------------|
| INVALID_EMAIL_FORMAT | 400 | Email format validation failed |
| EMAIL_EXISTS | 409 | Email already registered |
| USERNAME_EXISTS | 409 | Username already taken |
| INVALID_PASSWORD | 400 | Password complexity not met |
| OTP_RATE_LIMITED | 429 | Too many OTP requests |
| INVALID_OTP | 400 | OTP verification failed |
| OTP_EXPIRED | 400 | OTP has expired |
| MAX_ATTEMPTS_EXCEEDED | 400 | Too many failed attempts |
| RESEND_RATE_LIMITED | 429 | Too many resend requests |

## Testing Strategy

### Unit Tests
- FluentValidation rules for RegisterCommand
- OTP generation (6 digits)
- OTP hashing and verification
- Expiration time calculation
- Rate limiting logic

### Property-Based Tests (using FsCheck)
- Property 1-9 as defined above

### Integration Tests
- Full registration flow with in-memory database
- OTP verification flow
- Resend OTP flow
