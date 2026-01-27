# Requirements Document

## Introduction

Chức năng đăng ký tài khoản người dùng với xác thực OTP qua email. Hệ thống cho phép người dùng đăng ký tài khoản mới, gửi mã OTP xác thực qua Gmail, và hoàn tất đăng ký sau khi xác thực thành công.

## Glossary

- **System**: Hệ thống CodeNexus API
- **User**: Người dùng muốn đăng ký tài khoản
- **OTP (One-Time Password)**: Mã xác thực một lần gồm 6 chữ số
- **Email**: Địa chỉ email Gmail của người dùng
- **Registration Request**: Yêu cầu đăng ký chứa thông tin email, username, password

## Requirements

### Requirement 1

**User Story:** As a user, I want to register a new account with my email, so that I can access the CodeNexus platform.

#### Acceptance Criteria

1. WHEN a user submits registration with email, username, and password THEN the System SHALL validate the input format and uniqueness
2. WHEN the email format is invalid THEN the System SHALL reject the registration and return an error message
3. WHEN the email already exists in the system THEN the System SHALL reject the registration and return an error message
4. WHEN the username already exists in the system THEN the System SHALL reject the registration and return an error message
5. WHEN the password does not meet complexity requirements (minimum 8 characters, at least 1 uppercase, 1 lowercase, 1 number) THEN the System SHALL reject the registration and return an error message

### Requirement 2

**User Story:** As a user, I want to receive an OTP code via email, so that I can verify my email ownership.

#### Acceptance Criteria

1. WHEN registration input is valid THEN the System SHALL generate a 6-digit OTP code
2. WHEN an OTP is generated THEN the System SHALL hash the OTP before storing in the database
3. WHEN an OTP is generated THEN the System SHALL set an expiration time of 5 minutes
4. WHEN an OTP is generated THEN the System SHALL send the OTP to the user email via Gmail SMTP
5. WHEN a user requests a new OTP for the same email within 1 minute THEN the System SHALL reject the request and return a rate limit error

### Requirement 3

**User Story:** As a user, I want to verify my OTP code, so that I can complete my registration.

#### Acceptance Criteria

1. WHEN a user submits a valid OTP within the expiration time THEN the System SHALL create the user account
2. WHEN a user submits an invalid OTP THEN the System SHALL reject verification and return an error message
3. WHEN a user submits an expired OTP THEN the System SHALL reject verification and return an expiration error
4. WHEN a user exceeds 5 failed OTP attempts THEN the System SHALL invalidate the OTP and require a new registration request
5. WHEN verification is successful THEN the System SHALL mark the OTP record as used and delete it

### Requirement 4

**User Story:** As a user, I want to resend OTP if I didn't receive it, so that I can complete verification.

#### Acceptance Criteria

1. WHEN a user requests OTP resend after 1 minute from last send THEN the System SHALL generate and send a new OTP
2. WHEN a new OTP is sent THEN the System SHALL invalidate the previous OTP for that email
3. WHEN a user exceeds 5 resend requests within 1 hour THEN the System SHALL block further resend requests and return a rate limit error

### Requirement 5

**User Story:** As a system administrator, I want OTP records to be cleaned up automatically, so that the database remains efficient.

#### Acceptance Criteria

1. WHEN an OTP record expires THEN the System SHALL allow cleanup processes to remove expired records
2. WHEN an OTP is successfully verified THEN the System SHALL remove the OTP record from the database
