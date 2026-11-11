CREATE TABLE SignupRequests (
    TokenHash TEXT NOT NULL PRIMARY KEY,
    Email TEXT NOT NULL,
    Name TEXT NOT NULL,
    PasswordHash TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    ExpiresAt TEXT NOT NULL
);
CREATE INDEX IX_SignupRequests_Email ON SignupRequests (Email);
CREATE INDEX IX_SignupRequests_ExpiresAt ON SignupRequests (ExpiresAt);
