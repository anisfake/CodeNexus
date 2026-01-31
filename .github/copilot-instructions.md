# CodeNexus Copilot Instructions

## Project
- Target framework: .NET 8.
- Architecture/layers:
  - `CodeNexus.API`: ASP.NET Core controllers are thin; call MediatR via `ISender`.
  - `CodeNexus.Application`: CQRS with MediatR; FluentValidation; returns `Result` / `Result<T>`.
  - `CodeNexus.Domain`: entities only (e.g., `OtpVerification`).
  - `CodeNexus.Infrastructure`: EF Core (SQL Server), services (Email/OTP/JWT), dependency injection in `Infrastructure.DependencyInjection`.

## Patterns & conventions (match existing code)
- Commands/Queries are records:
  - `public record XCommand(...) : IRequest<Result>` or `IRequest<Result<T>>`
  - `public record XQuery(...) : IRequest<Result<T>>`
- Handlers: `public class XCommandHandler : IRequestHandler<XCommand, Result>` etc.
- Use async EF Core APIs and thread `CancellationToken` through all calls.
- Business failures return `Result.Failure("ERROR_CODE", "message")`. Do not throw for expected conditions.
- Error codes are `SCREAMING_SNAKE_CASE` strings.

## Validation
- Use FluentValidation `AbstractValidator<T>`.
- Use `.WithMessage(...)` and `.WithErrorCode("SCREAMING_SNAKE_CASE")`.
- Validators handle format/range/null/empty; avoid database/business logic inside validators.

## EF Core
- Queries are read-only and should prefer `.AsNoTracking()`.
- Commands may modify state and call `SaveChangesAsync(ct)`.

## AuthController behavior (DO NOT CHANGE)
- `AuthController.ToActionResult(Result)` maps:
  - `"EMAIL_EXISTS"` or `"USERNAME_EXISTS"` => 409 Conflict
  - `"OTP_RATE_LIMITED"` or `"RESEND_RATE_LIMITED"` => 429 TooManyRequests
  - otherwise => 400 BadRequest
  - success => 200 Ok()
- `AuthController.ToActionResult<T>(Result<T>)` returns `CreatedAtAction(nameof(VerifyOtp), result.Value)` on success. Keep as-is.

## OTP / forgot password conventions
- `OtpVerification` fields include: `Email`, `Username`, `FirstName`, `LastName`, `PasswordHash`, `OtpHash`, `Purpose`, `CreatedAt`, `ExpiresAt`, `AttemptCount`, `ResendCount`, `LastResendAt`.
- Forgot password uses `Purpose = ResetPassword`, `OtpExpirationMinutes = 5`, resend interval = 1 minute.
- OTP generated/hashed via `IOTPService`, email sent via `IEmailService.SendOtpEmailAsync(email, otp, ct)`.

## Non-negotiable constraint: do not rewrite completed features
- Do not refactor, rename, or change behavior of existing completed features unless explicitly requested.
- Do not change existing controller helpers/response behavior, especially anything in `AuthController`.
- Prefer additive changes: add new files/classes/endpoints rather than editing existing ones.
- Preserve existing naming (including typos like `ForgotPasswordCommanHandler`) and folder structure.
- Avoid broad cleanup edits (usings reordering, formatting sweeps, renames) unless required to compile.

## Queries (read-only) without impacting existing behavior
- Implement query endpoints to return `Ok(result.Value)` (inline) or a helper in the NEW controller you add.
- Do NOT reuse `AuthController.ToActionResult<T>` for query endpoints since it returns `CreatedAtAction`.