CREATE TABLE DataConnectors (
 Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
 OrganizationId INTEGER NOT NULL DEFAULT 1 REFERENCES Organizations(Id) ON DELETE RESTRICT,
 Name TEXT NOT NULL, ProfileId TEXT NOT NULL, Category TEXT NOT NULL,
 Endpoint TEXT NOT NULL, Catalog TEXT NOT NULL, Schema TEXT NOT NULL,
 Enabled INTEGER NOT NULL, TablesJson TEXT NOT NULL,
 Status TEXT NOT NULL, TestedAt TEXT NULL, Version INTEGER NOT NULL
);
CREATE INDEX IX_DataConnectors_OrganizationId ON DataConnectors(OrganizationId);
CREATE TABLE ConnectorGrants (
 ConnectorId INTEGER NOT NULL REFERENCES DataConnectors(Id) ON DELETE CASCADE,
 RoleId INTEGER NOT NULL REFERENCES Roles(RoleId) ON DELETE RESTRICT,
 PRIMARY KEY (ConnectorId,RoleId)
);
CREATE INDEX IX_ConnectorGrants_RoleId ON ConnectorGrants(RoleId);
CREATE TRIGGER ConnectorGrant_Organization BEFORE INSERT ON ConnectorGrants
 WHEN (SELECT OrganizationId FROM DataConnectors WHERE Id=NEW.ConnectorId) <> (SELECT OrganizationId FROM Roles WHERE RoleId=NEW.RoleId)
 BEGIN SELECT RAISE(ABORT,'Cross-organization grant'); END;
CREATE TRIGGER ConnectorGrant_Organization_Update BEFORE UPDATE ON ConnectorGrants
 WHEN (SELECT OrganizationId FROM DataConnectors WHERE Id=NEW.ConnectorId) <> (SELECT OrganizationId FROM Roles WHERE RoleId=NEW.RoleId)
 BEGIN SELECT RAISE(ABORT,'Cross-organization grant'); END;
