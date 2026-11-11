CREATE TABLE ActionAuditEvents (
    Id INTEGER NOT NULL CONSTRAINT PK_ActionAuditEvents PRIMARY KEY AUTOINCREMENT,
    CreatedAt TEXT NOT NULL,
    OrganizationId INTEGER NULL,
    ActorUserId INTEGER NULL,
    ActorName TEXT NULL,
    Action TEXT NOT NULL,
    Operation TEXT NOT NULL,
    Area TEXT NOT NULL,
    Method TEXT NOT NULL,
    Route TEXT NOT NULL,
    TargetId TEXT NULL,
    Outcome TEXT NOT NULL,
    StatusCode INTEGER NOT NULL,
    DurationMs INTEGER NOT NULL,
    RequestId TEXT NOT NULL,
    RemoteAddress TEXT NULL
);
CREATE INDEX IX_ActionAuditEvents_CreatedAt ON ActionAuditEvents (CreatedAt);
CREATE INDEX IX_ActionAuditEvents_OrganizationId_Id ON ActionAuditEvents (OrganizationId,Id);
CREATE INDEX IX_ActionAuditEvents_ActorUserId_Id ON ActionAuditEvents (ActorUserId,Id);
CREATE TRIGGER ActionAuditEvents_NoUpdate BEFORE UPDATE ON ActionAuditEvents
BEGIN SELECT RAISE(ABORT,'Audit records are append-only'); END;
CREATE TRIGGER ActionAuditEvents_NoDelete BEFORE DELETE ON ActionAuditEvents
BEGIN SELECT RAISE(ABORT,'Audit records are append-only'); END;
