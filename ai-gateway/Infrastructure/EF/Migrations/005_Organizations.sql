CREATE TABLE Organizations (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Slug TEXT NOT NULL, IsActive INTEGER NOT NULL);
CREATE UNIQUE INDEX IX_Organizations_Slug ON Organizations(Slug);
INSERT INTO Organizations (Id, Name, Slug, IsActive) VALUES (1, 'Organização padrão', 'default', 1);
CREATE TABLE OrganizationMemberships (
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE RESTRICT,
    UserId INTEGER NOT NULL REFERENCES Users(UserId) ON DELETE RESTRICT,
    IsActive INTEGER NOT NULL,
    PRIMARY KEY (OrganizationId, UserId)
);
INSERT INTO OrganizationMemberships SELECT 1, UserId, 1 FROM Users;
ALTER TABLE Roles ADD COLUMN OrganizationId INTEGER REFERENCES Organizations(Id) ON DELETE RESTRICT;
ALTER TABLE KnowledgeBaseItem ADD COLUMN OrganizationId INTEGER REFERENCES Organizations(Id) ON DELETE RESTRICT;
ALTER TABLE ChatSession ADD COLUMN OrganizationId INTEGER REFERENCES Organizations(Id) ON DELETE RESTRICT;
ALTER TABLE ChatKbRatings ADD COLUMN OrganizationId INTEGER REFERENCES Organizations(Id) ON DELETE RESTRICT;
UPDATE Roles SET OrganizationId=1;
UPDATE KnowledgeBaseItem SET OrganizationId=1;
UPDATE ChatSession SET OrganizationId=1;
UPDATE ChatKbRatings SET OrganizationId=1;
DROP INDEX IX_Roles_RoleName;
CREATE UNIQUE INDEX IX_Roles_OrganizationId_RoleName ON Roles(OrganizationId, RoleName);
CREATE INDEX IX_KnowledgeBaseItem_OrganizationId_KeyName ON KnowledgeBaseItem(OrganizationId, KeyName);
CREATE INDEX IX_ChatSession_OrganizationId ON ChatSession(OrganizationId);
CREATE INDEX IX_ChatKbRatings_OrganizationId ON ChatKbRatings(OrganizationId);
-- ALTER cannot add a NOT NULL foreign key without rebuilding these legacy tables.
-- Preserve them in place; enforce null rejection with triggers, FKs reject unknown IDs.
CREATE TRIGGER Roles_Org_Insert BEFORE INSERT ON Roles WHEN NEW.OrganizationId IS NULL BEGIN SELECT RAISE(ABORT, 'Organization required'); END;
CREATE TRIGGER Roles_Org_Update BEFORE UPDATE OF OrganizationId ON Roles WHEN NEW.OrganizationId IS NULL BEGIN SELECT RAISE(ABORT, 'Organization required'); END;
CREATE TRIGGER Knowledge_Org_Insert BEFORE INSERT ON KnowledgeBaseItem WHEN NEW.OrganizationId IS NULL BEGIN SELECT RAISE(ABORT, 'Organization required'); END;
CREATE TRIGGER Knowledge_Org_Update BEFORE UPDATE OF OrganizationId ON KnowledgeBaseItem WHEN NEW.OrganizationId IS NULL BEGIN SELECT RAISE(ABORT, 'Organization required'); END;
CREATE TRIGGER Session_Org_Insert BEFORE INSERT ON ChatSession WHEN NEW.OrganizationId IS NULL BEGIN SELECT RAISE(ABORT, 'Organization required'); END;
CREATE TRIGGER Session_Org_Update BEFORE UPDATE OF OrganizationId ON ChatSession WHEN NEW.OrganizationId IS NULL BEGIN SELECT RAISE(ABORT, 'Organization required'); END;
CREATE TRIGGER Ratings_Org_Insert BEFORE INSERT ON ChatKbRatings WHEN NEW.OrganizationId IS NULL BEGIN SELECT RAISE(ABORT, 'Organization required'); END;
CREATE TRIGGER Ratings_Org_Update BEFORE UPDATE OF OrganizationId ON ChatKbRatings WHEN NEW.OrganizationId IS NULL BEGIN SELECT RAISE(ABORT, 'Organization required'); END;
