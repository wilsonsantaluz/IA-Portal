ALTER TABLE OrganizationMemberships ADD COLUMN PolicyRole TEXT NULL CHECK (PolicyRole IS NULL OR PolicyRole IN ('Owner','Admin','Member','Viewer'));
ALTER TABLE OrganizationMemberships ADD COLUMN Version INTEGER NOT NULL DEFAULT 0;
-- Preserve custom group permissions; null means the existing group-based policy.
UPDATE OrganizationMemberships SET PolicyRole='Admin' WHERE EXISTS (
    SELECT 1 FROM UserRoles ur JOIN Roles r ON r.RoleId=ur.RoleId
    WHERE ur.UserId=OrganizationMemberships.UserId AND r.OrganizationId=OrganizationMemberships.OrganizationId AND r.RoleName='admin'
);
-- Deterministic initial owner: first active existing administrator in each organization.
UPDATE OrganizationMemberships SET PolicyRole='Owner' WHERE IsActive=1 AND PolicyRole='Admin'
AND UserId=(SELECT MIN(m.UserId) FROM OrganizationMemberships m JOIN Users u ON u.UserId=m.UserId
            WHERE m.OrganizationId=OrganizationMemberships.OrganizationId AND m.IsActive=1 AND u.IsActive=1
            AND EXISTS (SELECT 1 FROM UserRoles ur JOIN Roles r ON r.RoleId=ur.RoleId
                        WHERE ur.UserId=m.UserId AND r.OrganizationId=m.OrganizationId AND r.RoleName='admin'));
CREATE TABLE OrganizationAiDefaults (
    OrganizationId INTEGER NOT NULL PRIMARY KEY REFERENCES Organizations(Id) ON DELETE RESTRICT,
    Provider TEXT NOT NULL, Model TEXT NOT NULL, Version INTEGER NOT NULL
);
CREATE TABLE OrganizationAiPreferences (
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE RESTRICT,
    UserId INTEGER NOT NULL REFERENCES Users(UserId) ON DELETE RESTRICT,
    Provider TEXT NOT NULL, Model TEXT NOT NULL,
    PRIMARY KEY (OrganizationId,UserId)
);
CREATE INDEX IX_OrganizationAiPreferences_UserId ON OrganizationAiPreferences(UserId);
-- The previous account preference belongs to the default organization only.
INSERT INTO OrganizationAiPreferences (OrganizationId,UserId,Provider,Model)
SELECT 1,p.UserId,p.Provider,p.Model FROM AiUserPreferences p
WHERE EXISTS (SELECT 1 FROM OrganizationMemberships m WHERE m.OrganizationId=1 AND m.UserId=p.UserId);
