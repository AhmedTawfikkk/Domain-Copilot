# ADR-0008: Identity Cookie Authentication

- Status: Accepted
- Date: 2026-09-21

## Context

The browser UI is hosted by the same ASP.NET Core API origin. The former
demonstration API-key mechanism did not provide account registration, password
hashing, session lifecycle, or per-user identities.

## Decision

The API uses ASP.NET Core Identity with PostgreSQL-backed user, role, claim, and
token tables. `ApplicationUser` adds a display name and creation timestamp to the
framework identity user.

Registration and sign-in are anonymous API endpoints. Sign-in creates an
HTTP-only, secure, same-site cookie with an eight-hour sliding lifetime. Role
claims are included in the authentication principal and existing Lawyer/Counsel
authorization policies remain the server-side enforcement point.

For the evaluation demo, registration includes a public Lawyer/Counsel selector.
This is intentionally not a production authorization-administration model.

## Consequences

- The UI uses same-origin cookie authentication without storing a credential in
  JavaScript, local storage, or URLs.
- Password hashing, validation, uniqueness, and persistence are owned by the
  Identity framework rather than controller code.
- A production deployment must replace public Counsel registration with an
  invitation or administrator-managed role-assignment process and add account
  recovery, email confirmation, MFA, and audit requirements.
