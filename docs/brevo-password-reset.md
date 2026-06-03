# Brevo Password Reset Email Setup

This app sends password reset email through SMTP when these environment variables are configured. Without them, reset requests are still recorded and the login page keeps the same neutral message, but no email is sent.

## Brevo Account Steps

1. Create or sign in to a Brevo account.
2. Add and verify the sender email or domain you want reset emails to come from.
3. Open Brevo account settings, then go to SMTP & API.
4. Open the SMTP tab and generate a new SMTP key.
5. Copy the SMTP login and SMTP key. Use the SMTP key as the password, not a Brevo API key.

## App Configuration

Add these values to `web/.env.local` or to your hosting environment:

```env
APP_BASE_URL=https://your-public-app-url.example
EMAIL_SMTP_HOST=smtp-relay.brevo.com
EMAIL_SMTP_PORT=587
EMAIL_SMTP_USERNAME=your-brevo-smtp-login
EMAIL_SMTP_PASSWORD=your-brevo-smtp-key
EMAIL_FROM_EMAIL=verified-sender@your-domain.example
EMAIL_FROM_NAME=Battery Pass
```

For local testing, `APP_BASE_URL` can be `http://localhost:5186`. For a deployed app, it must be the public HTTPS URL so the reset link in the email opens the right site.

## Test Flow

1. Restart the app after setting environment variables.
2. Open `/login`.
3. Enter an existing user email in the forgot-password form.
4. Confirm the email arrives.
5. Open the link, set a new password, and sign in with it.

If no email arrives, first check that the sender is verified in Brevo, `EMAIL_SMTP_PASSWORD` is an SMTP key, port `587` is allowed by the host, and `APP_BASE_URL` matches the app URL users can reach.
