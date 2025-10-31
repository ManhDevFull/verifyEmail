CREATE TABLE public.email_verification (
    id              SERIAL PRIMARY KEY,
    email           TEXT NOT NULL,
    otp_hash        TEXT NOT NULL,
    type            TEXT NOT NULL,
    expires_at      TIMESTAMP NOT NULL,
    used            BOOLEAN NOT NULL DEFAULT FALSE,
    attempt_count   INT NOT NULL DEFAULT 0,
    created_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    used_at         TIMESTAMP NULL,
    ip_request      TEXT NULL,
    user_agent      TEXT NULL
);
