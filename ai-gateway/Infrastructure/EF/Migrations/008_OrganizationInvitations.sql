CREATE TABLE OrganizationInvitations (
    Id TEXT NOT NULL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE RESTRICT,
    TokenHash TEXT NOT NULL,
    Role TEXT NOT NULL CHECK(Role IN ('Member','Viewer')),
    CreatedByUserId INTEGER NOT NULL REFERENCES Users(UserId) ON DELETE RESTRICT,
    IssuerMembershipVersion INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL, ExpiresAt TEXT NOT NULL,
    RevokedAt TEXT NULL, AcceptedAt TEXT NULL,
    AcceptedByUserId INTEGER NULL REFERENCES Users(UserId) ON DELETE RESTRICT,
    Version INTEGER NOT NULL
);
CREATE UNIQUE INDEX IX_OrganizationInvitations_TokenHash ON OrganizationInvitations(TokenHash);
CREATE INDEX IX_OrganizationInvitations_OrganizationId_ExpiresAt ON OrganizationInvitations(OrganizationId,ExpiresAt);
CREATE INDEX IX_OrganizationInvitations_CreatedByUserId ON OrganizationInvitations(CreatedByUserId);
CREATE INDEX IX_OrganizationInvitations_AcceptedByUserId ON OrganizationInvitations(AcceptedByUserId);
