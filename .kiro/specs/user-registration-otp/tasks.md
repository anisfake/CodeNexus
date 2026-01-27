# Implementation Plan

- [x] 1. Create OtpVerification entity and database setup






  - [x] 1.1 Create OtpVerification entity in Domain layer

    - Add OtpVerification.cs with all required properties
    - _Requirements: 2.1, 2.2, 2.3_
  - [x] 1.2 Add DbSet and configure OtpVerification in AppDbContext


    - Configure primary key, indexes (unique on Email)
    - _Requirements: 2.1_

  - [x] 1.3 Create and apply migration for OtpVerification table

    - _Requirements: 2.1_

- [ ] 2. Create Application layer interfaces and DTOs
  - [ ] 2.1 Create DTOs for registration flow
    - RegisterRequest, VerifyOtpRequest, ResendOtpRequest, UserResponse
    - _Requirements: 1.1_
  - [ ] 2.2 Create IAuthService interface
    - Define InitiateRegistrationAsync, VerifyOtpAsync, ResendOtpAsync methods
    - _Requirements: 1.1, 3.1, 4.1_
  - [ ] 2.3 Create IEmailService interface
    - Define SendOtpEmailAsync method
    - _Requirements: 2.4_
  - [ ] 2.4 Create Result and Result<T> classes for operation results
    - _Requirements: 1.2, 1.3, 1.4, 1.5_

- [ ] 3. Implement validation logic
  - [ ] 3.1 Create EmailValidator with regex validation
    - _Requirements: 1.2_
  - [ ] 3.2 Write property test for email validation
    - **Property 1: Invalid email format rejection**
    - **Validates: Requirements 1.2**
  - [ ] 3.3 Create PasswordValidator with complexity rules
    - Minimum 8 chars, 1 uppercase, 1 lowercase, 1 number
    - _Requirements: 1.5_
  - [ ] 3.4 Write property test for password validation
    - **Property 2: Password complexity validation**
    - **Validates: Requirements 1.5**

- [ ] 4. Implement OTP generation and hashing
  - [ ] 4.1 Create OtpGenerator service
    - Generate 6-digit random OTP
    - _Requirements: 2.1_
  - [ ] 4.2 Write property test for OTP generation
    - **Property 3: OTP generation format**
    - **Validates: Requirements 2.1**
  - [ ] 4.3 Create OtpHasher service using BCrypt or SHA256
    - Hash and verify OTP
    - _Requirements: 2.2_
  - [ ] 4.4 Write property test for OTP hashing
    - **Property 4: OTP storage security**
    - **Validates: Requirements 2.2**

- [ ] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Implement AuthService
  - [ ] 6.1 Implement InitiateRegistrationAsync
    - Validate input, check uniqueness, generate OTP, save record, send email
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.5_
  - [ ] 6.2 Write property test for OTP expiration
    - **Property 5: OTP expiration setting**
    - **Validates: Requirements 2.3**
  - [ ] 6.3 Implement VerifyOtpAsync
    - Verify OTP, check expiration, create user, cleanup OTP record
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_
  - [ ] 6.4 Write property test for valid OTP verification
    - **Property 6: Valid OTP verification creates user**
    - **Validates: Requirements 3.1**
  - [ ] 6.5 Write property test for invalid OTP rejection
    - **Property 7: Invalid OTP rejection**
    - **Validates: Requirements 3.2**
  - [ ] 6.6 Write property test for OTP cleanup
    - **Property 8: OTP cleanup after verification**
    - **Validates: Requirements 3.5, 5.2**
  - [ ] 6.7 Implement ResendOtpAsync
    - Check rate limit, invalidate old OTP, generate new OTP
    - _Requirements: 4.1, 4.2, 4.3_
  - [ ] 6.8 Write property test for OTP invalidation on resend
    - **Property 9: Previous OTP invalidation on resend**
    - **Validates: Requirements 4.2**

- [ ] 7. Implement EmailService
  - [ ] 7.1 Create EmailService with Gmail SMTP configuration
    - Configure SMTP settings from appsettings.json
    - _Requirements: 2.4_
  - [ ] 7.2 Implement SendOtpEmailAsync with HTML template
    - _Requirements: 2.4_

- [ ] 8. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 9. Create API Controller
  - [ ] 9.1 Create AuthController with register endpoint
    - POST /api/auth/register
    - _Requirements: 1.1_
  - [ ] 9.2 Add verify-otp endpoint
    - POST /api/auth/verify-otp
    - _Requirements: 3.1_
  - [ ] 9.3 Add resend-otp endpoint
    - POST /api/auth/resend-otp
    - _Requirements: 4.1_

- [ ] 10. Configure dependency injection and settings
  - [ ] 10.1 Register services in Program.cs
    - IAuthService, IEmailService, validators
    - _Requirements: 1.1_
  - [ ] 10.2 Add email configuration to appsettings.json
    - SMTP host, port, credentials
    - _Requirements: 2.4_

- [ ] 11. Final Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.
