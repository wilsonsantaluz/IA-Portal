CREATE TABLE LlmUsage (
    Id TEXT NOT NULL PRIMARY KEY,
    OwnerUserId INTEGER NOT NULL REFERENCES Users(UserId) ON DELETE RESTRICT,
    SessionId TEXT NOT NULL REFERENCES ChatSession(Id) ON DELETE RESTRICT,
    MessageId INTEGER NOT NULL REFERENCES ChatMessage(Id) ON DELETE RESTRICT,
    Provider TEXT NOT NULL,
    Model TEXT NOT NULL,
    Purpose TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    Status TEXT NOT NULL,
    Source TEXT NOT NULL,
    InputTokens INTEGER NULL,
    OutputTokens INTEGER NULL,
    TotalTokens INTEGER NULL,
    CacheTokens INTEGER NULL,
    DurationMs INTEGER NULL
);
CREATE UNIQUE INDEX IX_LlmUsage_SessionId_Purpose ON LlmUsage(SessionId, Purpose) WHERE Purpose = 'title';
CREATE INDEX IX_LlmUsage_OwnerUserId_CreatedAt ON LlmUsage(OwnerUserId, CreatedAt);
CREATE INDEX IX_LlmUsage_Provider_Model_CreatedAt ON LlmUsage(Provider, Model, CreatedAt);
