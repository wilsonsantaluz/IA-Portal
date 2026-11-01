ALTER TABLE LlmUsage ADD COLUMN EstimatedCostNanoUsd INTEGER NULL;
ALTER TABLE LlmUsage ADD COLUMN PriceVersion TEXT NULL;
CREATE UNIQUE INDEX IX_LlmUsage_MessageId_Purpose ON LlmUsage(MessageId, Purpose) WHERE Purpose IN ('chat', 'document');
CREATE INDEX IX_LlmUsage_SessionId_CreatedAt ON LlmUsage(SessionId, CreatedAt);
CREATE INDEX IX_LlmUsage_Purpose_CreatedAt ON LlmUsage(Purpose, CreatedAt);
CREATE INDEX IX_LlmUsage_CreatedAt_Id ON LlmUsage(CreatedAt, Id);
